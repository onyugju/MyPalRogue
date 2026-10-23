using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    /// <summary>보스 패턴이 화면에 맡기는 연출 종류</summary>
    public enum BossFx
    {
        Charge,        // 힘을 모은다 (붉게 맥동하는 기운이 계속 남는다)
        Release,       // 모은 힘을 터뜨린다 (화면이 크게 흔들림)
        ChargeBroken,  // 모으던 힘이 흐트러진다
        Enrage,        // 분노 (붉게 번쩍 + 흔들림)
        Purify,        // 약화를 떨쳐낸다 (성스러운 빛)
        Curse,         // 상대에게 저주·냉기 등을 건다 (대상 쪽에 연출)
        Heal,          // 회복
        Aura,          // 몸에 두른 기운이 반응 (작열 오라 등)
    }

    /// <summary>
    /// 보스 패턴. 보스(10웨이브마다)·속성의 왕·최종 보스에게 붙는 전투 효과다.
    /// 잡으면 떨어진다 (Battle.ThrowSphere). 전투가 시작되면 Intro 문구로 어떤 패턴이 있는지 알려 준다.
    ///
    /// 보스:   힘 모으기 (웨이브 50부터 분노, 100부터 실드 정화도)
    /// 왕:     힘 모으기 + 실드 정화 + 속성별 고유 패턴 1개
    /// 제로버스: 힘 모으기(2배) + 공허의 침식 + 실드 정화 + 분노
    /// </summary>
    public abstract class BossPattern : BattleEffect
    {
        protected BossPattern(string name, string description)
        {
            PerkName = name;
            PerkDescription = description;
        }

        /// <summary>전투가 시작될 때 알려 줄 문구 (없으면 null)</summary>
        public virtual string Intro(Pal self) => null;

        protected static string N(Pal p) => p.Data.palName;

        // ───────── 붙이기 ─────────
        public static void Attach(Pal pal, BossKind kind, int wave)
        {
            if (kind != BossKind.None) GiveSupportMove(pal);
            switch (kind)
            {
                case BossKind.Boss:
                    pal.AddEffect(new ChargeStrike(3, 1.5f));
                    if (wave >= 50) pal.AddEffect(new Enrage(1f / 3f, "분노", (StatType.Attack, 1), (StatType.Speed, 1)));
                    if (wave >= 100) pal.AddEffect(new ShieldPurify("실드 정화"));
                    break;

                case BossKind.Legend:
                    var type = pal.Data.primaryType;
                    pal.AddEffect(type == PalType.Water ? new ChargeStrike(2, 1.6f, "해일 충전") : new ChargeStrike(3, 1.7f));
                    pal.AddEffect(type == PalType.Neutral
                        ? new ShieldPurify("성기사의 가호", (StatType.Defense, 1))
                        : new ShieldPurify("실드 정화"));
                    var signature = Signature(type);
                    if (signature != null) pal.AddEffect(signature);
                    break;

                case BossKind.Final:
                    pal.AddEffect(new ChargeStrike(3, 2f, "공허 응축"));
                    pal.AddEffect(new PeriodicCurse(4, "공허의 침식", (StatType.Attack, -1), (StatType.Defense, -1), (StatType.Speed, -1)));
                    pal.AddEffect(new ShieldPurify("공허의 재구성"));
                    pal.AddEffect(new Enrage(1f / 3f, "공허 폭주", (StatType.Attack, 1), (StatType.Defense, 1), (StatType.Speed, 1)));
                    break;
            }
        }

        /// <summary>
        /// 보스는 배운 변화 기술(버프·디버프·회복)이 있으면 꼭 하나 들고 나온다 (가장 약한 공격 기술과 바꿈).
        /// 어떤 상황에 쓸지는 상대 AI가 판단한다.
        /// </summary>
        public static void GiveSupportMove(Pal pal)
        {
            if (pal.Skills.Any(s => s.Data.category == SkillCategory.Status)) return;
            var support = pal.Data.learnset
                .Where(e => e.skill != null && e.level <= pal.Level && e.skill.category == SkillCategory.Status)
                .OrderByDescending(e => e.level).Select(e => e.skill).FirstOrDefault();
            if (support == null) return;
            if (pal.Skills.Count < Pal.MaxSkills) { pal.Skills.Add(new SkillInstance(support)); return; }
            int weakest = 0;
            for (int i = 1; i < pal.Skills.Count; i++)
                if (pal.Skills[i].Data.power < pal.Skills[weakest].Data.power) weakest = i;
            pal.Skills[weakest] = new SkillInstance(support);
        }

        /// <summary>속성의 왕마다 다른 고유 패턴</summary>
        static BossPattern Signature(PalType type) => type switch
        {
            PalType.Fire => new BurnAura(0.3f),
            PalType.Grass => new Regenerate(12),
            PalType.Ice => new PeriodicCurse(2, "혹한의 오라", (StatType.Speed, -1)),
            PalType.Ground => new EarthArmor(0.8f),
            PalType.Electric => new Accelerate(3),
            PalType.Dragon => new Enrage(0.5f, "용의 분노", (StatType.Attack, 2), (StatType.Speed, 1)),
            PalType.Dark => new PeriodicCurse(3, "저주의 낙인", (StatType.Attack, -1), (StatType.Defense, -1)),
            _ => null,
        };
    }

    /// <summary>
    /// 힘 모으기: period턴마다 턴 끝에 힘을 모으고(예고), 다음 공격 기술의 데미지가 mult배.
    /// 모으는 중에 효과가 굉장한 공격이나 급소에 맞으면 흐트러진다 (반격 기회).
    /// </summary>
    public class ChargeStrike : BossPattern
    {
        readonly int period;
        public readonly float Multiplier;
        int turns;
        bool releasing;
        public bool Charged { get; private set; }

        public ChargeStrike(int period, float mult, string name = "힘 모으기")
            : base(name, $"{period}턴마다 힘을 모아 다음 공격이 {mult:0.#}배. 효과가 굉장한 공격이나 급소에 맞으면 흐트러진다.")
        {
            this.period = Mathf.Max(1, period);
            Multiplier = mult;
        }

        public override string Intro(Pal self) => $"{Josa.Eun(N(self))} 때때로 힘을 모아 강한 일격을 노린다!";

        public override void OnBattleStart(Pal self, BattleContext ctx) { turns = 0; Charged = false; releasing = false; }

        public override void OnTurnEnd(Pal self, BattleContext ctx)
        {
            if (self.IsFainted || Charged) return;
            if (++turns < period) return;
            turns = 0;
            Charged = true;
            ctx.PatternFx(self, BossFx.Charge, self);
            ctx.Log($"{Josa.I(N(self))} {Josa.Eul(PerkName)} 시작했다…!\n다음 공격을 조심하자!");
        }

        public override void ModifyOutgoingDamage(Pal self, DamageInfo info)
        {
            if (!Charged || info.Skill == null || info.Skill.category != SkillCategory.Attack) return;
            info.ExtraMultiplier *= Multiplier;
            releasing = true;
        }

        public override void OnAfterAttack(Pal self, DamageInfo info, BattleContext ctx)
        {
            if (!releasing) return;
            releasing = false;
            Charged = false;
            ctx.PatternFx(self, BossFx.Release, info.Defender);
            ctx.Log("모아 둔 힘을 한꺼번에 터뜨렸다!");
        }

        public override void OnDamaged(Pal self, DamageInfo info, BattleContext ctx)
        {
            if (!Charged || releasing || self.IsFainted) return;
            if (!info.IsCritical && info.TypeMultiplier <= 1f) return;
            Charged = false;
            turns = 0;
            ctx.PatternFx(self, BossFx.ChargeBroken, self);
            ctx.Log($"{N(self)}의 집중이 흐트러졌다!\n모으던 힘이 흩어졌다!");
        }
    }

    /// <summary>분노: HP가 threshold 이하가 되면 한 번 능력치가 오른다</summary>
    public class Enrage : BossPattern
    {
        readonly float threshold;
        readonly (StatType stat, int delta)[] boosts;
        bool done;

        public Enrage(float threshold, string name, params (StatType, int)[] boosts)
            : base(name, $"HP가 {Mathf.RoundToInt(threshold * 100)}% 이하가 되면 분노해서 능력치가 오른다.")
        {
            this.threshold = threshold;
            this.boosts = boosts;
        }

        public override void OnBattleStart(Pal self, BattleContext ctx) => done = false;
        public override void OnDamaged(Pal self, DamageInfo info, BattleContext ctx) => Check(self, ctx);
        public override void OnTurnEnd(Pal self, BattleContext ctx) => Check(self, ctx);

        void Check(Pal self, BattleContext ctx)
        {
            if (done || self.IsFainted || self.CurrentHP > self.MaxHP * threshold) return;
            done = true;
            ctx.PatternFx(self, BossFx.Enrage, self);
            ctx.Log(PerkName == "분노" ? $"{Josa.I(N(self))} 분노했다!" : $"{N(self)}의 {PerkName}!");
            StatStages.ChangeMany(self, boosts, ctx.Log);
        }
    }

    /// <summary>실드 정화: 실드가 깨질 때 떨어진 능력치를 원래대로 (+ 추가로 능력치 상승)</summary>
    public class ShieldPurify : BossPattern
    {
        readonly (StatType stat, int delta)[] extra;
        int lastShields;

        public ShieldPurify(string name, params (StatType, int)[] extra)
            : base(name, "실드가 깨질 때마다 떨어진 능력치를 원래대로 되돌린다." + (extra.Length > 0 ? " 방어도 오른다." : ""))
        {
            this.extra = extra;
        }

        public override void OnBattleStart(Pal self, BattleContext ctx) => lastShields = self.ShieldsLeft;

        public override void OnDamaged(Pal self, DamageInfo info, BattleContext ctx)
        {
            if (self.ShieldsLeft >= lastShields) return;
            lastShields = self.ShieldsLeft;
            if (self.IsFainted) return;
            bool hadDebuffs = Enum.GetValues(typeof(StatType)).Cast<StatType>().Any(s => self.GetStage(s) < 0);
            if (!hadDebuffs && extra.Length == 0) return;
            ctx.PatternFx(self, BossFx.Purify, self);
            if (hadDebuffs)
            {
                self.ClearNegativeStages();
                ctx.Log($"{N(self)}의 {PerkName}!\n떨어진 능력치가 원래대로 돌아왔다!");
            }
            else ctx.Log($"{N(self)}의 {PerkName}!");
            if (extra.Length > 0) StatStages.ChangeMany(self, extra, ctx.Log);
        }
    }

    /// <summary>주기적으로 상대의 능력치를 떨어뜨린다 (혹한의 오라, 저주의 낙인, 공허의 침식)</summary>
    public class PeriodicCurse : BossPattern
    {
        readonly int period;
        readonly (StatType stat, int delta)[] changes;
        int turns;

        public PeriodicCurse(int period, string name, params (StatType, int)[] changes)
            : base(name, $"{period}턴마다 상대의 " + Josa.Eul(string.Join("·", changes.Select(c => StatStages.Short(c.Item1)))) + " 떨어뜨린다.")
        {
            this.period = Mathf.Max(1, period);
            this.changes = changes;
        }

        public override string Intro(Pal self) => $"{Josa.Eun(N(self))} {Josa.Eul(PerkName)} 두르고 있다!";
        public override void OnBattleStart(Pal self, BattleContext ctx) => turns = 0;

        public override void OnTurnEnd(Pal self, BattleContext ctx)
        {
            if (self.IsFainted || ++turns < period) return;
            turns = 0;
            var foe = ctx.OpponentOf(self);
            if (foe == null || foe.IsFainted) return;
            ctx.PatternFx(self, BossFx.Curse, foe);
            ctx.Log($"{N(self)}의 {PerkName}!");
            StatStages.ChangeMany(foe, changes, ctx.Log);
        }
    }

    /// <summary>재생 (풀속성 왕): 턴 끝마다 최대 HP의 1/divisor 회복. 지금 실드 칸 위로는 차오르지 않는다</summary>
    public class Regenerate : BossPattern
    {
        readonly int divisor;
        public Regenerate(int divisor) : base("생명의 축복", $"턴 끝마다 최대 HP의 1/{divisor}을 회복한다.") { this.divisor = divisor; }

        public override string Intro(Pal self) => $"{Josa.Eun(N(self))} 생명의 축복을 받고 있다!";

        public override void OnTurnEnd(Pal self, BattleContext ctx)
        {
            if (self.IsFainted) return;
            int top = self.IsBoss ? self.ShieldHp(self.ShieldsLeft + 1) : self.MaxHP;     // 깨진 실드 위로는 회복하지 않는다
            int amount = Mathf.Min(Mathf.Max(1, self.MaxHP / divisor), top - self.CurrentHP);
            if (amount <= 0) return;
            ctx.PatternFx(self, BossFx.Heal, self);
            self.Heal(amount);
            ctx.Log($"{Josa.Eun(N(self))} 생명의 축복으로 HP를 회복했다!");
        }
    }

    /// <summary>작열 오라 (불속성 왕): 공격한 상대를 chance 확률로 화상</summary>
    public class BurnAura : BossPattern
    {
        readonly float chance;
        static readonly System.Random rng = new System.Random();
        public BurnAura(float chance) : base("작열 오라", $"공격해 온 상대를 {Mathf.RoundToInt(chance * 100)}% 확률로 화상 입힌다.") { this.chance = chance; }

        public override string Intro(Pal self) => $"{Josa.Eun(N(self))} 작열 오라를 두르고 있다!";

        public override void OnDamaged(Pal self, DamageInfo info, BattleContext ctx)
        {
            var attacker = info.Attacker;
            if (attacker == null || attacker == self || attacker.IsFainted || attacker.Status != StatusCondition.None) return;
            if (rng.NextDouble() >= chance || !attacker.CanReceiveStatus(StatusCondition.Burn)) return;
            ctx.PatternFx(self, BossFx.Aura, attacker);
            if (attacker.TrySetStatus(StatusCondition.Burn))
                ctx.Log($"{Josa.Eun(attacker.Data.palName)} 작열 오라에 닿아 화상을 입었다!");
        }
    }

    /// <summary>대지의 갑옷 (땅속성 왕): 실드가 남아 있는 동안 받는 데미지 감소</summary>
    public class EarthArmor : BossPattern
    {
        readonly float mult;
        public EarthArmor(float mult) : base("대지의 갑옷", $"실드가 남아 있는 동안 받는 데미지가 {Mathf.RoundToInt((1f - mult) * 100)}% 줄어든다.") { this.mult = mult; }

        public override string Intro(Pal self) => $"{Josa.Eun(N(self))} 대지의 갑옷으로 몸을 지키고 있다!";

        public override void ModifyIncomingDamage(Pal self, DamageInfo info)
        {
            if (self.ShieldsLeft > 0) info.ExtraMultiplier *= mult;
        }
    }

    /// <summary>번개 가속 (번개속성 왕): 턴 끝마다 스피드 +1 (이 효과로는 +max까지)</summary>
    public class Accelerate : BossPattern
    {
        readonly int max;
        public Accelerate(int max) : base("번개 가속", $"턴 끝마다 스피드가 오른다 (최대 +{max}).") { this.max = max; }

        public override string Intro(Pal self) => $"{Josa.Eun(N(self))} 번개처럼 점점 빨라진다!";

        public override void OnTurnEnd(Pal self, BattleContext ctx)
        {
            if (self.IsFainted || self.GetStage(StatType.Speed) >= max) return;
            ctx.PatternFx(self, BossFx.Aura, self);
            ctx.Log($"{N(self)}의 번개 가속!");
            StatStages.Change(self, StatType.Speed, 1, ctx.Log);
        }
    }
}
