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
        PhaseOmen,     // 제로버스 2페이즈 전조 (공허가 요동친다, 음악이 멎는다)
        PhaseShift,    // 제로버스 2페이즈 변신 (배경 End, 지역 ???, 대형 컷인)
        Blessing,      // 별의 가호 (내 파티 전원 회복·강화)
        Horizon,       // 사건의 지평선 (상대를 빨아들여 HP를 빼앗는다)
        Nova,          // 초신성 붕괴 (실드가 깨질 때 터지는 반격)
    }

    /// <summary>
    /// 보스 패턴. 보스(10웨이브마다)·속성의 왕·최종 보스에게 붙는 전투 효과다.
    /// 잡으면 떨어진다 (Battle.ThrowSphere). 전투가 시작되면 Intro 문구로 어떤 패턴이 있는지 알려 준다.
    ///
    /// 보스:   힘 모으기 (웨이브 50부터 분노, 100부터 실드 정화도)
    /// 왕:     힘 모으기 + 실드 정화 + 속성별 고유 패턴 1개
    /// 제로버스 1페이즈: 공허 응축(1.7배) + 공허의 침식 + 공허의 재구성 + 공허 폭주 + 멸성의 봉인(HP 1/5에서 2페이즈)
    /// 제로버스 2페이즈: 멸성 응축(1.8배) + 사건의 지평선 + 초신성 붕괴 + 멸성의 낙인 + 최후의 발악 (AstralymPhase)
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

        /// <summary>패턴이 직접 주는 데미지 (실드·버티기·부활 효과를 거친다). 실제로 깎인 HP</summary>
        protected static int Hurt(Pal target, int amount, BattleContext ctx)
        {
            if (target == null || target.IsFainted) return 0;
            int dealt = target.TakeDamage(Mathf.Max(1, amount));
            if (!target.IsFainted) return dealt;
            foreach (var e in target.EffectsSnapshot())
                if (e.TryPreventFaint(target, ctx)) break;
            if (target.IsFainted)
                foreach (var e in target.EffectsSnapshot()) e.OnFainted(target, ctx);
            return dealt;
        }

        // ───────── 붙이기 ─────────
        public static void Attach(Pal pal, BossKind kind, int wave)
        {
            if (kind != BossKind.None) GiveSupportMove(pal);
            switch (kind)
            {
                case BossKind.Boss:
                    pal.AddEffect(wave < GameConfig.EarlyBossUntil ? new ChargeStrike(4, 1.3f) : new ChargeStrike(3, 1.5f));   // 웨이브 10 보스는 약하게
                    if (wave >= 100) pal.AddEffect(new Enrage(1f / 3f, "분노", (StatType.Attack, 1), (StatType.Speed, 1)));
                    if (wave >= 130) pal.AddEffect(new ShieldPurify("실드 정화"));
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

                case BossKind.Final:                     // 1페이즈 (2페이즈보다 한 단계 약하게)
                    pal.AddEffect(new ChargeStrike(3, 1.7f, "공허 응축"));
                    pal.AddEffect(new PeriodicCurse(4, "공허의 침식", (StatType.Attack, -1), (StatType.Speed, -1)));
                    pal.AddEffect(new ShieldPurify("공허의 재구성"));
                    pal.AddEffect(new Enrage(1f / 3f, "공허 폭주", (StatType.Attack, 1), (StatType.Speed, 1)));
                    pal.AddEffect(new VoidSeal());
                    AstralymPhase.SetPhaseOneSkills(pal);
                    pal.BossPhase = 1;
                    pal.CannotFaint = true;
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
            if (!info.IsCritical && info.ChartMultiplier <= 1f) return;   // 공허 ×1.5 는 빼고, 진짜 약점·급소에만 흐트러진다
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

    // ═════════════════ 제로버스 2페이즈 ═════════════════

    /// <summary>멸성의 봉인 (제로버스 1페이즈): HP가 1/5까지 줄면 봉인이 풀려 2페이즈가 된다 (Battle이 확인). 그 전에는 쓰러지지 않는다</summary>
    public class VoidSeal : BossPattern
    {
        public VoidSeal() : base("멸성의 봉인", "HP가 1/5까지 줄어들면 봉인이 풀려 진정한 모습을 드러낸다.") { }
        public override string Intro(Pal self) => $"…{Josa.Eun(N(self))} 아직 힘의 일부만 드러내고 있는 것 같다.";
    }

    /// <summary>
    /// 사건의 지평선 (제로버스 2페이즈): period턴마다 턴 끝에 상대를 빨아들여 최대 HP의 1/divisor를 빼앗고 그만큼 회복한다.
    /// 회복은 지금 실드 칸 위로는 차오르지 않는다.
    /// </summary>
    public class EventHorizon : BossPattern
    {
        readonly int period, divisor;
        int turns;

        public EventHorizon(int period, int divisor)
            : base("사건의 지평선", $"{period}턴마다 상대를 빨아들여 최대 HP의 1/{divisor}을 빼앗고 그만큼 회복한다.")
        {
            this.period = Mathf.Max(1, period);
            this.divisor = Mathf.Max(1, divisor);
        }

        public override string Intro(Pal self) => $"{N(self)}의 주위로 빛조차 빠져나갈 수 없는 사건의 지평선이 열렸다!";
        public override void OnBattleStart(Pal self, BattleContext ctx) => turns = 0;

        public override void OnTurnEnd(Pal self, BattleContext ctx)
        {
            if (self.IsFainted || ++turns < period) return;
            turns = 0;
            var foe = ctx.OpponentOf(self);
            if (foe == null || foe.IsFainted) return;
            ctx.PatternFx(self, BossFx.Horizon, foe);
            ctx.Log($"{N(self)}의 {PerkName}!\n{Josa.I(foe.Data.palName)} 공허로 빨려 들어간다!");
            int dealt = Hurt(foe, foe.MaxHP / divisor, ctx);
            int top = self.IsBoss ? self.ShieldHp(self.ShieldsLeft + 1) : self.MaxHP;
            int heal = Mathf.Min(dealt, top - self.CurrentHP);
            if (heal > 0)
            {
                self.Heal(heal);
                ctx.Log($"{Josa.Eun(N(self))} 빼앗은 생명으로 HP를 회복했다!");
            }
        }
    }

    /// <summary>초신성 붕괴 (제로버스 2페이즈): 실드가 깨질 때마다 떨어진 능력치를 되돌리고, 공격한 상대에게 최대 HP의 1/divisor 반격</summary>
    public class Supernova : BossPattern
    {
        readonly int divisor;
        int lastShields;

        public Supernova(int divisor)
            : base("초신성 붕괴", $"실드가 깨질 때마다 떨어진 능력치를 되돌리고, 공격한 상대에게 최대 HP의 1/{divisor} 반격한다.")
        {
            this.divisor = Mathf.Max(1, divisor);
        }

        public override string Intro(Pal self) => $"{N(self)}의 실드 속에서 죽어 가는 별이 들끓고 있다!";
        public override void OnBattleStart(Pal self, BattleContext ctx) => lastShields = self.ShieldsLeft;

        public override void OnDamaged(Pal self, DamageInfo info, BattleContext ctx)
        {
            if (self.ShieldsLeft >= lastShields) { lastShields = self.ShieldsLeft; return; }
            lastShields = self.ShieldsLeft;
            if (self.IsFainted) return;
            var foe = info.Attacker != null && info.Attacker != self ? info.Attacker : ctx.OpponentOf(self);
            ctx.PatternFx(self, BossFx.Nova, foe ?? self);
            ctx.Log($"깨진 실드가 붕괴한다…!\n{N(self)}의 {PerkName}!");
            if (self.ClearNegativeStages()) ctx.Log("떨어진 능력치가 원래대로 돌아왔다!");
            if (foe != null && !foe.IsFainted)
            {
                Hurt(foe, foe.MaxHP / divisor, ctx);
                ctx.Log($"{Josa.I(foe.Data.palName)} 폭발에 휘말렸다!");
            }
        }
    }

    /// <summary>
    /// 제로버스 1페이즈 → 2페이즈 변신. Battle이 HP 1/5에서 부른다.
    /// 패턴·기운·기술·지닌 도구를 2페이즈 것으로 바꾸고 실드를 6칸으로 되살린다 (HP 회복과 화면 연출은 Battle이).
    /// </summary>
    public static class AstralymPhase
    {
        /// <summary>지금이 몇 번째 모험인지 (화면이 웨이브마다 넣어 준다. 0이면 모름)</summary>
        public static int RunNumber;

        /// <summary>2페이즈로 넘어가기 전에 제로버스가 하는 말 (몇 번째 모험인지 알고 있다)</summary>
        public static IEnumerable<string> Lines()
        {
            yield return "……또 왔구나.";
            if (RunNumber > 1)
            {
                yield return $"{RunNumber}번째로구나.";
                yield return "너희는 기억하지 못하겠지만.";
            }
            else
            {
                yield return "처음이라고 생각하겠지.";
                yield return "아니. 너희가 기억하지 못할 뿐이다.";
            }
            yield return "쓰러지면 초원에서 다시 눈을 뜨고,";
            yield return "같은 길을 걸어 결국 이곳에 선다.";
            yield return "별은 지고, 다시 뜨고, 또 진다.";
            yield return "그 끝없는 순환의 끝에는 언제나 내가 있다.";
            yield return "…이번 빛은 조금 다른가. 좋다, 보여 주마.";
            yield return "이것이, 멸성(滅星)의 재앙이다.";
        }

        /// <summary>파티가 별의 가호를 받은 뒤 제로버스의 말</summary>
        public static readonly string[] LastLines =
        {
            "…죽은 별들의 빛인가. 그 빛도 지난번에 보았다.",
            "와라. 이번에야말로 이 순환을 끊을 수 있는지 보자.",
        };

        /// <summary>테스트 메뉴(37): 다음에 나오는 제로버스를 마지막 실드까지 깎아 둔다 (한 번 쓰면 꺼짐)</summary>
        public static bool DebugNearShift;

        public static bool ShouldShift(Pal p) =>
            p != null && p.BossPhase == 1 && !p.IsFainted && p.CurrentHP <= Mathf.RoundToInt(p.MaxHP * GameConfig.FinalPhaseShiftRatio);

        /// <summary>보스 쪽 변신 (HP는 그대로: 늘어난 최대 HP만큼만 같이 는다. 가득 채우는 건 Battle이 이벤트와 함께)</summary>
        public static void Transform(Pal boss, BattleContext ctx)
        {
            // 1페이즈 패턴·기운은 버리고, 지니고 있던 부활의씨앗도 공허에 삼켜진다 (2페이즈를 쓰러뜨리면 끝)
            var remove = boss.Effects.Where(e => e is BossPattern || e is BossAura || e is ReviverSeed).ToList();
            boss.ChangeEffects(remove, new BattleEffect[]
            {
                new BossAura("멸성의 기운", GameConfig.FinalPhase2StatMultiplier, GameConfig.FinalPhase2HpMultiplier),
            });
            boss.BossPhase = 2;
            boss.CannotFaint = false;
            boss.CureStatus();
            boss.ResetBattleState();
            boss.MakeBoss(GameConfig.FinalPhase2Segments);

            // 각성은 다시 잠들었다가 2페이즈에서 진각성으로 깨어난다
            foreach (var aw in boss.Effects.OfType<Awakening>()) { aw.Sleep(); aw.SetTrue(true); }
            if (!boss.Effects.OfType<Awakening>().Any()) boss.AddEffect(new Awakening(AwakeningCatalog.ForEnemy(boss), true));

            var patterns = new BossPattern[]
            {
                new ChargeStrike(3, 1.8f, "멸성 응축"),
                new EventHorizon(4, 8),
                new Supernova(10),
                new PeriodicCurse(5, "멸성의 낙인", (StatType.Attack, -1), (StatType.Defense, -1)),
                new Enrage(1f / 3f, "최후의 발악", (StatType.Attack, 1), (StatType.Defense, 1), (StatType.Speed, 1)),
            };
            foreach (var p in patterns) { boss.AddEffect(p); p.OnBattleStart(boss, ctx); }

            SetPhaseTwoSkills(boss);

            // 미니 블랙홀: 2페이즈에서만. 빼앗은 도구는 공허에 삼켜져 사라진다 (부활의씨앗을 제로버스가 쓰지 않게)
            if (!boss.Effects.OfType<MiniBlackHole>().Any())
                boss.AddEffect(new MiniBlackHole
                {
                    Devour = true, HeldItemId = ItemCatalog.MiniBlackHoleName, PerkName = ItemCatalog.MiniBlackHoleName,
                    PerkDescription = "매 턴 상대 아이템 1개를 집어삼킨다",
                });
        }

        // ───── 제로버스 전용 기술 8개 (에셋 없이 실행 중에 만든다. 연출은 SkillFx.Astralym.cs의 같은 이름) ─────
        //  1페이즈: 별의 비 · 아스트랄 글라이딩 · 웜홀 · 팔디움 캐논
        //  2페이즈: 디바인 레이 · 천상의 꽃 · 창조의 종언 · 멸성
        public const string StarfallId = "Astralym_Starfall", GlideId = "Astralym_Glide", WormHoleId = "Astralym_WormHole", BurstId = "Astralym_Burst",
                            DivineRayId = "Astralym_DivineRay", BloomId = "Astralym_Bloom", CreationEndId = "Astralym_CreationEnd", ExtinctionId = "Astralym_Extinction";

        static readonly Dictionary<string, SkillData> skills = new Dictionary<string, SkillData>();

        static SkillData Get(string id, Func<SkillData> make)
        {
            if (skills.TryGetValue(id, out var s) && s != null) return s;
            return skills[id] = make();
        }

        public static SkillData Starfall => Get(StarfallId, () => Make(StarfallId, "별의 비", "별무리를 불러 유성의 비를 쏟아붓는다.", 90, 90, 10));

        public static SkillData Glide => Get(GlideId, () =>
        {
            var s = Make(GlideId, "아스트랄 글라이딩", "별빛을 타고 미끄러지듯 파고들어 벤다. 반드시 먼저 공격한다.", 70, 100, 15);
            s.priority = 1;
            return s;
        });

        public static SkillData WormHole => Get(WormHoleId, () =>
        {
            var s = Make(WormHoleId, "웜홀", "웜홀로 상대를 삼켰다가 다른 곳에서 내던진다. 30% 확률로 상대의 스피드를 떨어뜨린다.", 85, 95, 10);
            s.statChanges.Add(new StatChange(StatType.Speed, -1, false));
            s.statChangeChance = 30;
            return s;
        });

        public static SkillData Burst => Get(BurstId, () => Make(BurstId, "팔디움 캐논", "팔디움 에너지를 한껏 모아 거대한 포격을 쏜다.", 110, 85, 5));

        public static SkillData DivineRay => Get(DivineRayId, () =>
        {
            var s = Make(DivineRayId, "디바인 레이", "하늘에서 신성한 빛줄기가 연달아 내리꽂힌다. 30% 확률로 상대의 방어를 떨어뜨린다.", 95, 100, 10);
            s.statChanges.Add(new StatChange(StatType.Defense, -1, false));
            s.statChangeChance = 30;
            return s;
        });

        public static SkillData Bloom => Get(BloomId, () =>
        {
            var s = Make(BloomId, "천상의 꽃", "별빛의 꽃을 피워 자신의 공격과 방어를 올린다.", 0, 100, 5);
            s.category = SkillCategory.Status;
            s.statChanges.Add(new StatChange(StatType.Attack, 1, true));
            s.statChanges.Add(new StatChange(StatType.Defense, 1, true));
            return s;
        });

        public static SkillData CreationEnd => Get(CreationEndId, () => Make(CreationEndId, "창조의 종언", "은하 하나를 빚어내 상대 위에서 무너뜨린다.", 130, 90, 5));

        public static SkillData Extinction => Get(ExtinctionId, () => Make(ExtinctionId, "멸성", "별 하나를 태어나게 하고, 그 별의 죽음으로 모든 것을 지운다.", 150, 85, 3));

        public static SkillData[] PhaseOneSkills => new[] { Starfall, Glide, WormHole, Burst };
        public static SkillData[] PhaseTwoSkills => new[] { DivineRay, Bloom, CreationEnd, Extinction };

        static SkillData Make(string id, string name, string desc, int power, int accuracy, int pp)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.name = id;
            s.hideFlags = HideFlags.DontSave;
            s.skillName = name;
            s.description = desc;
            s.type = PalType.Void;
            s.category = SkillCategory.Attack;
            s.power = power;
            s.accuracy = accuracy;
            s.maxPP = pp;
            return s;
        }

        static void SetSkills(Pal boss, IEnumerable<SkillData> list)
        {
            boss.Skills.Clear();
            foreach (var s in list.Take(Pal.MaxSkills)) boss.Skills.Add(new SkillInstance(s));
        }

        /// <summary>1페이즈 기술: 별의 비 · 아스트랄 글라이딩 · 웜홀 · 팔디움 캐논</summary>
        public static void SetPhaseOneSkills(Pal boss) => SetSkills(boss, PhaseOneSkills);

        /// <summary>2페이즈 기술: 1페이즈와 전부 다른 디바인 레이 · 천상의 꽃 · 창조의 종언 · 멸성</summary>
        public static void SetPhaseTwoSkills(Pal boss) => SetSkills(boss, PhaseTwoSkills);
    }

    /// <summary>
    /// 별의 가호 (제로버스 2페이즈가 시작될 때 내 파티 전원): 그 전투 동안 모든 능력치 ×1.15,
    /// 쓰러질 공격을 한 번 HP 1로 버틴다. 저장되지 않는 전투용 효과다.
    /// </summary>
    public class StarBlessing : BattleEffect
    {
        bool used;
        public StarBlessing()
        {
            PerkName = "별의 가호";
            PerkDescription = $"모든 능력치 ×{GameConfig.StarBlessingStatMultiplier:0.##}, 쓰러질 공격을 한 번 버틴다";
        }

        public override float StatMultiplier(StatType stat) => GameConfig.StarBlessingStatMultiplier;

        public override bool TryPreventFaint(Pal self, BattleContext ctx)
        {
            if (used) return false;
            used = true;
            self.Revive(1);
            ctx.Log($"별의 가호가 {Josa.Eul(self.Data.palName)} 지켜 냈다!\n{Josa.Eun(self.Data.palName)} 가까스로 버텼다!");
            return true;
        }
    }
}
