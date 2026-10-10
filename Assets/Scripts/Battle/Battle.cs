using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    public enum BattleState { Ongoing, WaitingPlayerSwitch, PlayerWon, PlayerLost }

    /// <summary>한 턴에 고를 수 있는 행동: 스킬 사용 또는 교체</summary>
    public readonly struct BattleAction
    {
        public enum Kind { UseSkill, Switch }
        public Kind Type { get; }
        public int Index { get; }
        BattleAction(Kind type, int index) { Type = type; Index = index; }

        /// <summary>skillIndex = -1 이면 "발버둥" (모든 스킬의 PP가 0일 때만 선택 가능)</summary>
        public static BattleAction Skill(int skillIndex) => new(Kind.UseSkill, skillIndex);
        public static BattleAction SwitchTo(int partyIndex) => new(Kind.Switch, partyIndex);
    }

    /// <summary>
    /// 1:1 턴제 전투 진행기. MonoBehaviour가 아닌 일반 클래스라서 UI 없이도 돌릴 수 있다.
    /// 사용법:  var b = new Battle(내파티, 적파티, rng, log);
    ///          b.ExecuteTurn(BattleAction.Skill(0));   // 한 턴 진행
    ///          if (b.State == BattleState.WaitingPlayerSwitch) b.SwitchAfterFaint(index);
    /// </summary>
    public class Battle
    {
        public IReadOnlyList<Pal> PlayerParty { get; }
        public IReadOnlyList<Pal> EnemyParty { get; }
        public int PlayerIndex { get; private set; }
        public int EnemyIndex { get; private set; }
        public Pal Player => PlayerParty[PlayerIndex];
        public Pal Enemy => EnemyParty[EnemyIndex];
        public BattleState State { get; private set; } = BattleState.Ongoing;
        public BattleContext Ctx { get; } = new BattleContext();

        /// <summary>어느 쪽이든 팰이 쓰러질 때 호출 (경험치 지급 등에 사용)</summary>
        public event Action<Pal> PalFainted;

        readonly System.Random rng;

        // 발버둥: PP가 모두 바닥났을 때 쓰는 기술 (반동 데미지는 아직 없음)
        static SkillData struggle;
        static SkillData Struggle
        {
            get
            {
                if (struggle == null)
                {
                    struggle = ScriptableObject.CreateInstance<SkillData>();
                    struggle.name = "Struggle";
                    struggle.skillName = "발버둥";
                    struggle.type = PalType.Neutral;
                    struggle.category = SkillCategory.Physical;
                    struggle.power = 50;
                    struggle.accuracy = 100;
                    struggle.maxPP = 1;
                    struggle.hideFlags = HideFlags.HideAndDontSave;
                }
                return struggle;
            }
        }

        public Battle(IList<Pal> player, IList<Pal> enemy, System.Random rng = null, Action<string> log = null)
        {
            PlayerParty = new List<Pal>(player);
            EnemyParty = new List<Pal>(enemy);
            this.rng = rng ?? new System.Random();
            if (log != null) Ctx.Log = log;

            PlayerIndex = FirstAlive(PlayerParty);
            EnemyIndex = FirstAlive(EnemyParty);
            if (PlayerIndex < 0 || EnemyIndex < 0)
            {
                State = PlayerIndex < 0 ? BattleState.PlayerLost : BattleState.PlayerWon;
                PlayerIndex = Mathf.Max(0, PlayerIndex);
                EnemyIndex = Mathf.Max(0, EnemyIndex);
                return;
            }

            foreach (var p in PlayerParty.Concat(EnemyParty))
            {
                p.ResetBattleState();
                foreach (var e in p.EffectsSnapshot()) e.OnBattleStart(p, Ctx);
            }
            Log($"야생의 {Enemy.Data.palName}(이)가 나타났다!");
            Log($"가라, {Player.Data.palName}!");
        }

        // ───────── 행동 검증 ─────────
        public bool IsValidPlayerAction(BattleAction a)
        {
            if (State != BattleState.Ongoing) return false;
            if (a.Type == BattleAction.Kind.Switch)
                return a.Index >= 0 && a.Index < PlayerParty.Count
                       && a.Index != PlayerIndex && !PlayerParty[a.Index].IsFainted;
            return IsValidSkillChoice(Player, a.Index);
        }

        static bool IsValidSkillChoice(Pal pal, int index)
        {
            if (index < 0) return !pal.Skills.Any(s => s.HasPP);          // 발버둥은 PP가 전부 0일 때만
            return index < pal.Skills.Count && pal.Skills[index].HasPP;
        }

        // ───────── 턴 진행 ─────────
        class Entry
        {
            public bool IsPlayer; public Pal Actor; public BattleAction Action;
            public int Priority, Speed, Tie;
        }

        Entry MakeEntry(bool isPlayer, Pal actor, BattleAction action)
        {
            int priority = 100;                                            // 교체는 항상 가장 먼저
            if (action.Type == BattleAction.Kind.UseSkill)
                priority = action.Index >= 0 ? actor.Skills[action.Index].Data.priority : 0;
            return new Entry
            {
                IsPlayer = isPlayer, Actor = actor, Action = action,
                Priority = priority, Speed = actor.GetStat(StatType.Speed), Tie = rng.Next(),
            };
        }

        /// <summary>한 턴을 진행한다. 잘못된 행동이면 false를 반환하고 아무 일도 일어나지 않는다.</summary>
        public bool ExecuteTurn(BattleAction playerAction)
        {
            if (!IsValidPlayerAction(playerAction)) return false;

            Ctx.Turn++;
            Log($"── 턴 {Ctx.Turn} ──");
            foreach (var p in new[] { Player, Enemy })
                foreach (var e in p.EffectsSnapshot()) e.OnTurnStart(p, Ctx);

            var entries = new List<Entry>
            {
                MakeEntry(true, Player, playerAction),
                MakeEntry(false, Enemy, ChooseEnemyAction()),
            }.OrderByDescending(e => e.Priority).ThenByDescending(e => e.Speed).ThenBy(e => e.Tie).ToList();

            foreach (var en in entries)
            {
                if (State != BattleState.Ongoing) break;
                bool stillActive = en.Actor == (en.IsPlayer ? Player : Enemy);
                if (en.Actor.IsFainted || !stillActive) continue;   // 쓰러졌거나 교체돼서 못 움직임

                Perform(en);
                ResolveFaints();
            }

            if (State == BattleState.Ongoing)
            {
                foreach (var p in new[] { Player, Enemy })
                    foreach (var e in p.EffectsSnapshot()) e.OnTurnEnd(p, Ctx);
                ResolveFaints();
            }
            return true;
        }

        /// <summary>내 팰이 쓰러져서 State가 WaitingPlayerSwitch일 때, 다음 팰을 내보낸다.</summary>
        public bool SwitchAfterFaint(int partyIndex)
        {
            if (State != BattleState.WaitingPlayerSwitch) return false;
            if (partyIndex < 0 || partyIndex >= PlayerParty.Count || PlayerParty[partyIndex].IsFainted) return false;
            PlayerIndex = partyIndex;
            State = BattleState.Ongoing;
            Log($"가라, {Player.Data.palName}!");
            return true;
        }

        // 적 AI: 지금은 PP가 남은 스킬 중 무작위
        BattleAction ChooseEnemyAction()
        {
            var usable = Enemy.Skills.Select((s, i) => (s, i)).Where(t => t.s.HasPP).ToList();
            if (usable.Count == 0) return BattleAction.Skill(-1);
            return BattleAction.Skill(usable[rng.Next(usable.Count)].i);
        }

        void Perform(Entry en)
        {
            if (en.Action.Type == BattleAction.Kind.Switch)
            {
                Log($"{Player.Data.palName}, 돌아와!");
                Player.ResetBattleState();
                PlayerIndex = en.Action.Index;
                Log($"가라, {Player.Data.palName}!");
                return;
            }

            var user = en.Actor;
            var target = en.IsPlayer ? Enemy : Player;
            SkillInstance inst = en.Action.Index >= 0 ? user.Skills[en.Action.Index] : null;
            UseSkill(user, target, inst, inst != null ? inst.Data : Struggle);
        }

        void UseSkill(Pal user, Pal target, SkillInstance inst, SkillData skill)
        {
            Log($"{user.Data.palName}의 {skill.skillName}!");
            if (inst != null) inst.PP--;

            if (skill.category == SkillCategory.Status)
            {
                Log("하지만 아무 일도 일어나지 않았다! (변화기 효과는 아직 미구현)");
                return;
            }
            if (!BattleCalc.CheckHit(skill, rng))
            {
                Log("공격은 빗나갔다!");
                return;
            }

            var info = BattleCalc.CalculateDamage(user, target, skill, rng);
            if (info.IsCritical) Log("급소에 맞았다!");
            if (!string.IsNullOrEmpty(info.EffectivenessText)) Log(info.EffectivenessText);

            int dealt = BattleCalc.ApplyDamage(info, Ctx);
            Log($"  → {target.Data.palName}에게 {dealt} 데미지 (HP {target.CurrentHP}/{target.MaxHP})");
        }

        // ───────── 기절 처리 ─────────
        void ResolveFaints()
        {
            if (Enemy.IsFainted)
            {
                Log($"{Enemy.Data.palName}(은)는 쓰러졌다!");
                PalFainted?.Invoke(Enemy);
                int next = FirstAlive(EnemyParty);
                if (next < 0) { State = BattleState.PlayerWon; Log("전투에서 승리했다!"); return; }
                EnemyIndex = next;
                Log($"상대는 {Enemy.Data.palName}(을)를 내보냈다!");
            }
            if (Player.IsFainted)
            {
                Log($"{Player.Data.palName}(은)는 쓰러졌다!");
                PalFainted?.Invoke(Player);
                if (FirstAlive(PlayerParty) < 0) { State = BattleState.PlayerLost; Log("눈앞이 깜깜해졌다..."); return; }
                State = BattleState.WaitingPlayerSwitch;
            }
        }

        static int FirstAlive(IReadOnlyList<Pal> party)
        {
            for (int i = 0; i < party.Count; i++) if (!party[i].IsFainted) return i;
            return -1;
        }

        void Log(string message) => Ctx.Log(message);
    }
}
