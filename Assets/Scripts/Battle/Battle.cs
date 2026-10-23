using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    public enum BattleState { Ongoing, WaitingPlayerSwitch, PlayerWon, PlayerLost, Caught }

    public enum BattleEventType { Message, HpChanged, Fainted, SentOut, StatusChanged, SphereThrown, SphereShakes, SphereCaught, SphereBroke, ExpBar, LevelUp, Recalled, ShieldBroken, Awakened }

    /// <summary>화면이 순서대로 재생하는 전투 진행 기록 한 줄</summary>
    public class BattleEvent
    {
        public BattleEventType Type;
        public string Text;           // Message
        public Pal Pal;               // HpChanged / Fainted / SentOut
        public bool IsPlayerSide;     // 어느 쪽 팰인지
        public int From, To;          // HpChanged
        public StatusCondition Status; // StatusChanged (None이면 풀림)
        public SphereType Sphere;      // SphereThrown: 던진 스피어
        public int Count;              // SphereShakes: 흔들린 횟수 (0~3)
        public float FromRatio, ToRatio;          // ExpBar: 지금 레벨 안에서 EXP바가 채워지는 구간 (0~1)
        public int Level;                         // LevelUp: 새 레벨
        public List<SkillData> NewSkills;         // LevelUp: 이 레벨에 배울 수 있는 스킬
    }

    /// <summary>한 턴에 고를 수 있는 행동: 스킬 사용 또는 교체</summary>
    public readonly struct BattleAction
    {
        public enum Kind { UseSkill, Switch, Sphere }
        public Kind Type { get; }
        public int Index { get; }
        BattleAction(Kind type, int index) { Type = type; Index = index; }

        /// <summary>skillIndex = -1 이면 "발버둥" (모든 스킬의 PP가 0일 때만 선택 가능)</summary>
        public static BattleAction Skill(int skillIndex) => new(Kind.UseSkill, skillIndex);
        public static BattleAction SwitchTo(int partyIndex) => new(Kind.Switch, partyIndex);
        public static BattleAction Throw(SphereType sphere) => new(Kind.Sphere, (int)sphere);
    }

    /// <summary>
    /// 1:1 턴제 전투 진행기. MonoBehaviour가 아닌 일반 클래스라서 UI 없이도 돌릴 수 있다.
    /// 사용법:  var b = new Battle(내파티, 적파티, rng, log);
    ///          b.ExecuteTurn(BattleAction.Skill(0));   // 한 턴 진행
    ///          if (b.State == BattleState.WaitingPlayerSwitch) b.SwitchAfterFaint(index);
    /// </summary>
    public class Battle
    {
        /// <summary>내 파티. 지금 싸우는 팰이 항상 0번이다 (교체하면 순서가 바뀐다, 포켓로그와 같음)</summary>
        public IReadOnlyList<Pal> PlayerParty => playerList;
        readonly List<Pal> playerList;
        public IReadOnlyList<Pal> EnemyParty { get; }
        public int PlayerIndex { get; private set; }
        public int EnemyIndex { get; private set; }
        public Pal Player => PlayerParty[PlayerIndex];
        public Pal Enemy => EnemyParty[EnemyIndex];
        public BattleState State { get; private set; } = BattleState.Ongoing;
        /// <summary>전투 시작 때 내 팰을 새로 내보내는지 (첫 웨이브, 또는 앞 팰이 쓰러져 있어서 다른 팰이 나갈 때)</summary>
        public bool PlayerSentOutAtStart { get; private set; }
        public BattleContext Ctx { get; } = new BattleContext();
        /// <summary>파티 전체 경험치 배율 (경험부적 등, Run.ExpMultiplier)</summary>
        public float ExpMultiplier { get; set; } = 1f;
        /// <summary>학습장치: 싸우지 않은 팰이 받는 경험치 비율 (1개당 0.2, Run.ExpShareRatio)</summary>
        public float ExpShareRatio { get; set; }
        /// <summary>열매 주머니: 내 팰의 열매가 먹어도 남을 확률 (Run.BerrySave)</summary>
        public float PlayerBerrySave { get; set; }

        /// <summary>어느 쪽이든 팰이 쓰러질 때 호출 (경험치 지급 등에 사용)</summary>
        public event Action<Pal> PalFainted;

        // ───────── 화면용 진행 기록 ─────────
        readonly Queue<BattleEvent> events = new Queue<BattleEvent>();

        /// <summary>UI가 일어난 일을 오래된 순서대로 하나씩 꺼내 쓰는 창구. 비었으면 false.</summary>
        public bool TryDequeueEvent(out BattleEvent e) => events.TryDequeue(out e);

        void Emit(BattleEvent e) => events.Enqueue(e);
        bool IsPlayerPal(Pal p) => PlayerParty.Contains(p);

        void OnStatusChanged(Pal p, StatusCondition s) =>
            Emit(new BattleEvent { Type = BattleEventType.StatusChanged, Pal = p, IsPlayerSide = IsPlayerPal(p), Status = s });

        /// <summary>
        /// 보스 실드가 깨질 때마다: 화면에 알리고, 깨진 실드 하나당 무작위 능력치 1단계 상승 (포켓로그 보스와 같음).
        /// 마지막 실드가 깨지면(맨 아래 칸만 남음) 분노해서 한 단계 더 오른다.
        /// </summary>
        void OnShieldsBroken(Pal p, int count)
        {
            Emit(new BattleEvent { Type = BattleEventType.ShieldBroken, Pal = p, IsPlayerSide = IsPlayerPal(p), Count = count });
            Log(count > 1 ? $"{p.Data.palName}의 실드가 {count}개 한꺼번에 깨졌다!" : $"{p.Data.palName}의 실드가 깨졌다!");
            if (p.IsFainted) return;
            int boosts = count + (p.ShieldsLeft == 0 ? 1 : 0);
            var stats = (StatType[])Enum.GetValues(typeof(StatType));
            for (int i = 0; i < boosts; i++)
            {
                var stat = stats[rng.Next(stats.Length)];
                if (p.ModifyStage(stat, 1) > 0) Log($"{p.Data.palName}의 {Josa.I(StatNames.Korean(stat))} 올라갔다!");
            }
        }

        void OnHpChanged(Pal p, int from, int to) =>
            Emit(new BattleEvent { Type = BattleEventType.HpChanged, Pal = p, IsPlayerSide = IsPlayerPal(p), From = from, To = to });

        /// <summary>팰의 HP 이벤트 구독을 끊는다. 전투가 끝나거나 화면이 사라질 때 호출 (여러 번 불러도 안전).</summary>
        public void Detach()
        {
            foreach (var p in PlayerParty.Concat(EnemyParty))
            {
                p.HpChanged -= OnHpChanged;
                p.StatusChanged -= OnStatusChanged;
                p.ShieldsBroken -= OnShieldsBroken;
            }
        }

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
                    struggle.category = SkillCategory.Attack;
                    struggle.power = 50;
                    struggle.accuracy = 100;
                    struggle.maxPP = 1;
                    struggle.hideFlags = HideFlags.HideAndDontSave;
                }
                return struggle;
            }
        }

        /// <param name="announcePlayer">"가라, ○○!"를 띄울지. 웨이브가 이어질 때는 이미 나와 있으므로 false
        /// (단, 지금 팰이 쓰러져 있어서 다른 팰이 나가면 띄운다)</param>
        /// <param name="bossKind">보스·전설·최종 보스면 등장 문구가 달라진다</param>
        public Battle(IList<Pal> player, IList<Pal> enemy, System.Random rng = null, Action<string> log = null,
                      bool announcePlayer = true, BossKind bossKind = BossKind.None)
        {
            playerList = new List<Pal>(player);
            EnemyParty = new List<Pal>(enemy);
            this.rng = rng ?? new System.Random();
            // 로그는 바깥으로 내보내면서 동시에 화면용 메시지 이벤트로도 쌓는다 (효과 쪽에서 ctx.Log를 써도 포함됨)
            Ctx.Log = msg =>
            {
                log?.Invoke(msg);
                Emit(new BattleEvent { Type = BattleEventType.Message, Text = msg });
            };

            Ctx.OpponentOf = p => IsPlayerPal(p) ? Enemy : Player;
            Ctx.BerrySaveChance = p => IsPlayerPal(p) ? PlayerBerrySave : 0f;
            Ctx.Awakened = p => Emit(new BattleEvent { Type = BattleEventType.Awakened, Pal = p, IsPlayerSide = IsPlayerPal(p) });

            PlayerIndex = FirstAlive(PlayerParty);
            bool frontChanged = PlayerIndex > 0;
            if (PlayerIndex > 0) { BringToFront(PlayerIndex); PlayerIndex = 0; }   // 기절하지 않은 첫 팰을 0번으로
            EnemyIndex = FirstAlive(EnemyParty);
            if (PlayerIndex < 0 || EnemyIndex < 0)
            {
                State = PlayerIndex < 0 ? BattleState.PlayerLost : BattleState.PlayerWon;
                PlayerIndex = Mathf.Max(0, PlayerIndex);
                EnemyIndex = Mathf.Max(0, EnemyIndex);
                return;
            }

            participants.Add(Player);                                   // 처음 나온 팰도 경험치를 받는다
            foreach (var p0 in PlayerParty.Concat(EnemyParty))
            {
                p0.HpChanged += OnHpChanged;
                p0.StatusChanged += OnStatusChanged;
                p0.ShieldsBroken += OnShieldsBroken;
            }
            foreach (var p in PlayerParty.Concat(EnemyParty))
            {
                p.ResetBattleState();
                foreach (var e in p.EffectsSnapshot()) e.OnBattleStart(p, Ctx);
            }
            string enemyName = Enemy.Data.palName;
            Log(bossKind switch
            {
                BossKind.Boss   => $"강한 기운이 느껴진다…!\n보스 {Josa.I(enemyName)} 나타났다!",
                BossKind.Legend => $"대지가 울린다…!\n{PalTypeNames.Korean(Enemy.Data.primaryType)}속성의 왕, 전설의 {Josa.I(enemyName)} 나타났다!",
                BossKind.Final  => $"공허가 모든 것을 삼키려 한다…!\n최종 보스 {Josa.I(enemyName)} 나타났다!",
                _               => $"야생의 {Josa.I(enemyName)} 나타났다!",
            });
            // 상대가 지닌 아이템 알려주기 (많으면 3개까지 + 외 n개)
            var held = Enemy.Effects.Where(e => e.HeldItemId != null).Select(e => e.HeldItemId).ToList();
            if (held.Count > 0)
            {
                string list = string.Join(", ", held.Take(3)) + (held.Count > 3 ? $" 외 {held.Count - 3}개" : "");
                Log($"{Josa.Eun(enemyName)} {Josa.Eul(list)} 지니고 있다!");
            }
            PlayerSentOutAtStart = announcePlayer || frontChanged;
            if (PlayerSentOutAtStart)
            {
                Log($"가라, {Player.Data.palName}!");
                Emit(new BattleEvent { Type = BattleEventType.SentOut, Pal = Player, IsPlayerSide = true });   // 화면: 스피어를 던져 내보낸다
            }
        }

        // ───────── 행동 검증 ─────────
        public bool IsValidPlayerAction(BattleAction a)
        {
            if (State != BattleState.Ongoing) return false;
            if (a.Type == BattleAction.Kind.Sphere)
                return a.Index >= 0 && a.Index < PalSpheres.Count && CatchBlockReason() == null;
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
            int priority = 100;                                            // 교체·스피어는 항상 가장 먼저
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
                foreach (var p in new[] { Player, Enemy }) ApplyResidualDamage(p);
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
            BringToFront(partyIndex);
            participants.Add(playerList[0]);
            PlayerIndex = 0;
            State = BattleState.Ongoing;
            Log($"가라, {Player.Data.palName}!");
            Emit(new BattleEvent { Type = BattleEventType.SentOut, Pal = Player, IsPlayerSide = true });
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
            if (en.Action.Type == BattleAction.Kind.Sphere)
            {
                ThrowSphere((SphereType)en.Action.Index);
                return;
            }
            if (en.Action.Type == BattleAction.Kind.Switch)
            {
                Log($"{Player.Data.palName}, 돌아와!");
                Emit(new BattleEvent { Type = BattleEventType.Recalled, Pal = Player, IsPlayerSide = true });   // 화면: 스피어로 돌아간다
                Player.ResetBattleState();
                BringToFront(en.Action.Index);
                participants.Add(playerList[0]);
                PlayerIndex = 0;
                Log($"가라, {Player.Data.palName}!");
                Emit(new BattleEvent { Type = BattleEventType.SentOut, Pal = Player, IsPlayerSide = true });
                return;
            }

            var user = en.Actor;
            var target = en.IsPlayer ? Enemy : Player;
            if (!CanAct(user)) return;                                   // 얼음/마비로 행동 불가 (PP도 쓰지 않음)
            SkillInstance inst = en.Action.Index >= 0 ? user.Skills[en.Action.Index] : null;
            UseSkill(user, target, inst, inst != null ? inst.Data : Struggle);
        }

        void UseSkill(Pal user, Pal target, SkillInstance inst, SkillData skill)
        {
            Log($"{user.Data.palName}의 {skill.skillName}!");
            if (inst != null) inst.PP--;

            if (!BattleCalc.CheckHit(skill, rng))
            {
                Log("공격은 빗나갔다!");
                return;
            }

            if (skill.category == SkillCategory.Status)
            {
                // 변화기: 지금은 상태이상을 거는 효과만 있다 (능력치 변화 등은 나중에 추가)
                if (skill.statusEffect == StatusCondition.None || !TryInflict(target, skill.statusEffect, skill.statusChance))
                    Log("그러나 아무 일도 일어나지 않았다!");
                return;
            }

            var info = BattleCalc.CalculateDamage(user, target, skill, rng);
            if (info.IsCritical) Log("급소에 맞았다!");
            if (!string.IsNullOrEmpty(info.EffectivenessText)) Log(info.EffectivenessText);

            int dealt = BattleCalc.ApplyDamage(info, Ctx);

            if (target.IsFainted || dealt <= 0) return;

            // 얼어 있는 상대가 불 속성 공격을 맞으면 녹는다
            if (target.Status == StatusCondition.Freeze && skill.type == PalType.Fire && target.CureStatus())
                Log($"{target.Data.palName}의 얼음이 녹았다!");

            // 추가 효과: 정해진 확률로 상태이상
            TryInflict(target, skill.statusEffect, skill.statusChance);
        }

        // ───────── 상태이상 ─────────
        /// <summary>행동하기 직전 확인. 얼음(20%로 녹음) / 마비(25%로 행동 불가)</summary>
        bool CanAct(Pal user)
        {
            string name = user.Data.palName;
            switch (user.Status)
            {
                case StatusCondition.Freeze:
                    if (rng.NextDouble() < StatusRules.FreezeThawChance)
                    {
                        user.CureStatus();
                        Log($"{name}의 얼음이 녹았다!");
                        return true;
                    }
                    Log($"{Josa.Eun(name)} 얼어붙어서 움직일 수 없다!");
                    return false;

                case StatusCondition.Paralysis:
                    if (rng.NextDouble() < StatusRules.ParalysisSkipChance)
                    {
                        Log($"{Josa.Eun(name)} 몸이 저려서 움직일 수 없다!");
                        return false;
                    }
                    return true;

                default:
                    return true;
            }
        }

        /// <summary>chance(%) 확률로 상태이상을 건다. 걸렸으면 메시지를 남기고 true.</summary>
        bool TryInflict(Pal target, StatusCondition status, int chance)
        {
            if (status == StatusCondition.None || chance <= 0) return false;
            if (rng.Next(100) >= chance) return false;
            if (!target.TrySetStatus(status)) return false;               // 이미 다른 상태이거나 속성 면역
            Log($"{Josa.Eun(target.Data.palName)} {StatusRules.InflictText(status)}");
            return true;
        }

        /// <summary>턴 끝: 화상(최대 HP 1/16) / 독(1/8) 데미지</summary>
        void ApplyResidualDamage(Pal p)
        {
            if (p.IsFainted) return;
            int divisor = p.Status switch
            {
                StatusCondition.Burn => StatusRules.BurnDamageDivisor,
                StatusCondition.Poison => StatusRules.PoisonDamageDivisor,
                _ => 0,
            };
            if (divisor == 0) return;

            string reason = p.Status == StatusCondition.Burn ? "화상" : "독";
            p.TakeDamage(Mathf.Max(1, p.MaxHP / divisor));
            Log($"{Josa.Eun(p.Data.palName)} {reason} 데미지를 입고 있다!");
        }

        // ───────── 기절 처리 ─────────
        void ResolveFaints()
        {
            if (Enemy.IsFainted)
            {
                Enemy.ClearStatusOnFaint();
                Emit(new BattleEvent { Type = BattleEventType.Fainted, Pal = Enemy, IsPlayerSide = false });
                Log($"{Josa.Eun(Enemy.Data.palName)} 쓰러졌다!");
                PalFainted?.Invoke(Enemy);
                AwardExp(Enemy);
                int next = FirstAlive(EnemyParty);
                if (next < 0)
                {
                    // 같은 턴에 내 팰도 쓰러졌다면(반동·독 등) 그 연출도 보여준다. 다음 웨이브에서 다른 팰이 나간다
                    if (Player.IsFainted)
                    {
                        Player.ClearStatusOnFaint();
                        Emit(new BattleEvent { Type = BattleEventType.Fainted, Pal = Player, IsPlayerSide = true });
                        Log($"{Josa.Eun(Player.Data.palName)} 쓰러졌다!");
                        PalFainted?.Invoke(Player);
                    }
                    State = BattleState.PlayerWon;
                    Detach();
                    return;
                }
                EnemyIndex = next;
                Emit(new BattleEvent { Type = BattleEventType.SentOut, Pal = Enemy, IsPlayerSide = false });
                Log($"상대는 {Josa.Eul(Enemy.Data.palName)} 내보냈다!");
            }
            if (Player.IsFainted)
            {
                Player.ClearStatusOnFaint();
                Emit(new BattleEvent { Type = BattleEventType.Fainted, Pal = Player, IsPlayerSide = true });
                Log($"{Josa.Eun(Player.Data.palName)} 쓰러졌다!");
                PalFainted?.Invoke(Player);
                if (FirstAlive(PlayerParty) < 0) { State = BattleState.PlayerLost; Log("눈앞이 깜깜해졌다..."); Detach(); return; }
                State = BattleState.WaitingPlayerSwitch;
            }
        }

        // ───────── 경험치 ─────────
        /// <summary>이번 상대와 싸우는 동안 한 번이라도 나왔던 내 팰들 (상대가 쓰러지면 경험치를 나눠 받는다)</summary>
        readonly HashSet<Pal> participants = new HashSet<Pal>();

        /// <summary>
        /// 상대를 쓰러뜨리면 싸움에 나왔던 팰들(쓰러진 팰 제외)이 경험치를 똑같이 나눠 받는다 (포켓몬 5세대 이전 방식).
        /// 레벨이 오르면 화면이 새 스킬 배우기 / 강화 효과 고르기를 처리하도록 LevelUp 이벤트를 남긴다.
        /// </summary>
        void AwardExp(Pal defeated)
        {
            var receivers = playerList.Where(p => participants.Contains(p) && !p.IsFainted && !p.IsMaxLevel).ToList();
            participants.Clear();
            if (!Player.IsFainted) participants.Add(Player);            // 다음 상대를 위해 지금 팰부터 다시 센다
            int total = GameConfig.ExpYield(defeated.Data, defeated.Level);
            ShareExp(receivers, total);
            if (receivers.Count == 0) return;

            float baseShare = (float)total / receivers.Count * ExpMultiplier;
            foreach (var pal in receivers)
            {
                float palMult = 1f;
                foreach (var e in pal.Effects) palMult *= e.ExpMultiplier;      // 행복의알 등
                int share = Mathf.Max(1, Mathf.RoundToInt(baseShare * palMult));
                Log($"{Josa.Eun(pal.Data.palName)} {share} 경험치를 얻었다!");
                float from = pal.ExpRatio;
                var ups = pal.GainExp(share);
                foreach (var (level, skills) in ups)
                {
                    Emit(new BattleEvent { Type = BattleEventType.ExpBar, Pal = pal, IsPlayerSide = true, FromRatio = from, ToRatio = 1f });
                    Emit(new BattleEvent { Type = BattleEventType.LevelUp, Pal = pal, IsPlayerSide = true, Level = level, NewSkills = skills });
                    from = 0f;
                }
                Emit(new BattleEvent { Type = BattleEventType.ExpBar, Pal = pal, IsPlayerSide = true, FromRatio = from, ToRatio = pal.ExpRatio });
            }
        }

        /// <summary>학습장치: 싸움에 나가지 않은(기절하지 않은) 팰들도 경험치의 일부를 받는다</summary>
        void ShareExp(List<Pal> fighters, int total)
        {
            if (ExpShareRatio <= 0f) return;
            var others = playerList.Where(p => !fighters.Contains(p) && !p.IsFainted && !p.IsMaxLevel).ToList();
            if (others.Count == 0) return;
            Log("학습장치로 다른 팰들도 경험치를 얻었다!");
            foreach (var pal in others)
            {
                float palMult = 1f;
                foreach (var e in pal.Effects) palMult *= e.ExpMultiplier;
                int amount = Mathf.Max(1, Mathf.RoundToInt(total * ExpShareRatio * ExpMultiplier * palMult));
                foreach (var (level, skills) in pal.GainExp(amount))
                    Emit(new BattleEvent { Type = BattleEventType.LevelUp, Pal = pal, IsPlayerSide = true, Level = level, NewSkills = skills });
            }
        }

        // ───────── 포획 ─────────
        public const int MaxPartySize = 6;
        const int CatchShakes = 3;   // 포켓로그처럼 3번 흔들리면 포획 성공

        /// <summary>
        /// 잡았지만 파티(6마리)가 가득 차서 아직 들어오지 못한 팰. 없으면 null.
        /// 화면에서 "다른 팰을 놓아주시겠습니까?"를 물은 뒤 ReplaceWithCaught 또는 ReleaseCaught를 부른다.
        /// </summary>
        public Pal PendingCatch { get; private set; }

        /// <summary>스피어를 던질 수 없는 이유. 던질 수 있으면 null (파티가 가득 차도 던질 수 있다, 포켓로그와 같음)</summary>
        public string CatchBlockReason()
        {
            if (!Enemy.Data.canBeCaught) return $"{Josa.Eun(Enemy.Data.palName)} 잡을 수 없는 팰이다!";
            if (Enemy.ShieldsLeft > 0) return $"{Enemy.Data.palName}의 실드가 아직 남아 있다!\n실드를 모두 깨야 잡을 수 있다.";
            return null;
        }

        /// <summary>파티의 index번 팰을 놓아주고, 잡은 팰을 파티 맨 뒤에 넣는다. 놓아준 팰을 돌려준다</summary>
        public Pal ReplaceWithCaught(int index)
        {
            if (PendingCatch == null || index < 0 || index >= playerList.Count) return null;
            var released = playerList[index];
            playerList.RemoveAt(index);
            playerList.Add(PendingCatch);
            PendingCatch = null;
            return released;
        }

        /// <summary>잡은 팰을 파티에 넣지 않고 놓아준다</summary>
        public void ReleaseCaught() => PendingCatch = null;

        /// <summary>잡은 팰을 응축할 수 있는 파티의 같은 팰 (★이 4가 아닌 것 중 레벨이 가장 높은 팰). 없으면 null</summary>
        public Pal CondenseTarget(Pal caught) =>
            caught == null ? null : playerList.Where(p => p != caught && p.Data == caught.Data && p.CanCondense)
                                              .OrderByDescending(p => p.Level).FirstOrDefault();

        /// <summary>잡은 팰(PendingCatch)을 같은 팰에 응축한다: ★ +1, 잡은 팰이 지닌 아이템도 넘어간다. 응축된 팰을 돌려준다</summary>
        public Pal CondenseCaught(out List<string> movedItems)
        {
            movedItems = new List<string>();
            var target = CondenseTarget(PendingCatch);
            if (target == null) return null;
            foreach (var item in PendingCatch.Effects.Where(e => e.HeldItemId != null).ToList())
            {
                PendingCatch.RemoveEffect(item);
                target.AddEffect(item);
                movedItems.Add(item.HeldItemId);
            }
            target.AddStar();
            target.InheritBetterIVs(PendingCatch);           // 더 좋은 개체값을 이어받는다
            target.Heal(target.MaxHP);                       // 응축하면 기운도 가득
            PendingCatch = null;
            return target;
        }

        /// <summary>파티에 자리가 있으면 잡은 팰을 그냥 파티에 넣는다 (응축하지 않기로 했을 때)</summary>
        public bool AddCaughtToParty()
        {
            if (PendingCatch == null || playerList.Count >= MaxPartySize) return false;
            playerList.Add(PendingCatch);
            PendingCatch = null;
            return true;
        }

        /// <summary>
        /// 포획 확률 (포켓몬 3세대 공식을 단순화):
        ///  a = (3×최대HP − 2×현재HP) / (3×최대HP) × 포획률(1~255) × 스피어 배율 × 상태이상 배율
        ///  잡힐 확률 = a / 255  (HP가 적을수록, 포획률이 높을수록 잘 잡힌다)
        ///  흔들림 한 번을 버틸 확률 = (a / 255)^(1/3), 3번 모두 버티면 포획
        /// </summary>
        public static float CatchChance(Pal target, SphereType sphere)
        {
            if (PalSpheres.AlwaysCatches(sphere)) return 1f;
            float max = Mathf.Max(1, target.MaxHP);
            float hpFactor = (3f * max - 2f * target.CurrentHP) / (3f * max);
            float statusMult = target.Status == StatusCondition.Freeze ? 2f
                             : target.Status != StatusCondition.None ? 1.5f : 1f;
            int catchRate = GameConfig.IsLegendary(target.Data)
                ? Mathf.Min(target.Data.catchRate, GameConfig.LegendCatchRate)   // 전설 팰은 잘 안 잡힌다
                : target.Data.catchRate;
            float a = hpFactor * catchRate * PalSpheres.CatchMultiplier(sphere) * statusMult;
            return Mathf.Clamp01(a / 255f);
        }

        void ThrowSphere(SphereType sphere)
        {
            var target = Enemy;
            string name = target.Data.palName;
            Log($"{Josa.Eul(PalSpheres.Name(sphere))} 던졌다!");
            Emit(new BattleEvent { Type = BattleEventType.SphereThrown, Pal = target, Sphere = sphere });

            float chance = CatchChance(target, sphere);
            double perShake = Math.Pow(chance, 1.0 / CatchShakes);
            int shakes = 0;
            while (shakes < CatchShakes && rng.NextDouble() < perShake) shakes++;
            if (chance >= 1f) shakes = CatchShakes;

            Emit(new BattleEvent { Type = BattleEventType.SphereShakes, Pal = target, Count = shakes });

            if (shakes >= CatchShakes)
            {
                Emit(new BattleEvent { Type = BattleEventType.SphereCaught, Pal = target, Sphere = sphere });
                Log($"신난다! {Josa.Eul(name)} 잡았다!");
                target.ResetBattleState();
                target.CaughtSphere = sphere;                // 나중에 내보낼 때 이 스피어를 던진다
                target.ClearBoss();
                if (playerList.Count < MaxPartySize && CondenseTarget(target) == null)
                {
                    playerList.Add(target);                  // 파티 맨 뒤에 들어온다
                    Log($"{Josa.I(name)} 파티에 들어왔다!");
                }
                else PendingCatch = target;                  // 같은 팰이 있거나(응축) 가득 참: 화면에서 묻는다
                State = BattleState.Caught;
                Detach();
                return;
            }

            Emit(new BattleEvent { Type = BattleEventType.SphereBroke, Pal = target, Sphere = sphere });
            Log(shakes switch
            {
                0 => $"앗! {Josa.I(name)} 스피어에서 나와 버렸다!",
                1 => "아아! 잡았다고 생각했는데!",
                _ => "아쉽다! 조금만 더 하면 잡을 수 있었는데!",
            });
        }

        /// <summary>파티의 index번 팰을 0번(싸우는 자리)과 맞바꾼다. 파티 화면 왼쪽 큰 칸에 항상 지금 팰이 나오게 하기 위해서다</summary>
        void BringToFront(int index)
        {
            if (index <= 0 || index >= playerList.Count) return;
            (playerList[0], playerList[index]) = (playerList[index], playerList[0]);
        }

        static int FirstAlive(IReadOnlyList<Pal> party)
        {
            for (int i = 0; i < party.Count; i++) if (!party[i].IsFainted) return i;
            return -1;
        }

        void Log(string message) => Ctx.Log(message);
    }
}
