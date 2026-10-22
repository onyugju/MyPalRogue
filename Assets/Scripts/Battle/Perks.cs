using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 강화 효과(퍽) 하나의 정보. 고르면 Create()로 만든 BattleEffect가 그 팰에 영구히 붙는다.
    /// Type이 비어 있으면 모든 팰에게 나오는 공용 강화, 있으면 그 속성을 가진 팰에게만 나온다.
    /// </summary>
    public class Perk
    {
        public string Id;
        public string Name;
        public string Description;
        public PalType? Type;
        public Func<BattleEffect> Create;
    }

    /// <summary>
    /// 강화 효과 목록. 새 강화를 넣고 싶으면 All 목록에 한 줄 추가하면 된다.
    /// 10레벨마다 (공용 + 내 속성 강화) 중에서 아직 안 가진 것 3개를 무작위로 보여준다.
    /// </summary>
    public static class PerkCatalog
    {
        public static readonly List<Perk> All = new List<Perk>
        {
            // ───── 공용 ─────
            P("atk_up",   "힘 단련",     "공격이 15% 올라간다.",                        null, () => new StatPerk(StatType.Attack, 1.15f)),
            P("def_up",   "철갑",        "방어가 15% 올라간다.",                        null, () => new StatPerk(StatType.Defense, 1.15f)),
            P("spd_up",   "질풍",        "스피드가 15% 올라간다.",                      null, () => new StatPerk(StatType.Speed, 1.15f)),
            P("hp_up",    "튼튼한 몸",   "최대 HP가 15% 늘어난다.",                     null, () => new MaxHpPerk(1.15f)),
            P("last",     "배수의 진",   "HP가 절반 이하면 주는 데미지가 30% 늘어난다.", null, () => new LowHpPowerPerk(1.3f)),
            P("endure",   "버티기",      "전투마다 한 번, 쓰러질 공격을 HP 1로 버틴다.", null, () => new EndurePerk()),
            P("regen",    "재생",        "턴이 끝날 때마다 최대 HP의 1/16을 회복한다.",  null, () => new RegenPerk(16)),
            P("crit",     "급소 노리기", "급소에 맞을 확률이 크게 늘어난다.",           null, () => new CritPerk(0.15f)),
            P("stab",     "적응력",      "자기 속성 기술의 위력이 25% 늘어난다.",       null, () => new StabPerk(1.25f)),
            P("guard",    "방어 태세",   "받는 데미지가 10% 줄어든다.",                 null, () => new DamageTakenPerk(0.9f)),
            P("drain",    "흡혈",        "공격으로 준 데미지의 1/8만큼 회복한다.",       null, () => new DrainPerk(8)),

            // ───── 속성 전용: 그 속성 기술 위력 +25% ─────
            TypePower(PalType.Neutral,  "무속성 숙련"),
            TypePower(PalType.Fire,     "불꽃 숙련"),
            TypePower(PalType.Water,    "물결 숙련"),
            TypePower(PalType.Grass,    "초목 숙련"),
            TypePower(PalType.Electric, "번개 숙련"),
            TypePower(PalType.Ground,   "대지 숙련"),
            TypePower(PalType.Ice,      "냉기 숙련"),
            TypePower(PalType.Dragon,   "용의 숙련"),
            TypePower(PalType.Dark,     "어둠 숙련"),
            TypePower(PalType.Void,     "공허 숙련"),

            // ───── 속성 전용: 특색 있는 효과 ─────
            P("fire_burn",   "불씨",       "공격하면 20% 확률로 상대가 화상을 입는다.",       PalType.Fire,     () => new StatusOnHitPerk(StatusCondition.Burn, 0.2f)),
            P("ice_freeze",  "서리",       "공격하면 10% 확률로 상대를 얼린다.",             PalType.Ice,      () => new StatusOnHitPerk(StatusCondition.Freeze, 0.1f)),
            P("elec_static", "정전기",     "공격을 받으면 30% 확률로 상대를 마비시킨다.",     PalType.Electric, () => new StatusWhenHitPerk(StatusCondition.Paralysis, 0.3f)),
            P("dark_poison", "독침",       "공격하면 20% 확률로 상대를 독에 걸리게 한다.",   PalType.Dark,     () => new StatusOnHitPerk(StatusCondition.Poison, 0.2f)),
            P("grass_photo", "광합성",     "턴이 끝날 때마다 최대 HP의 1/10을 회복한다.",    PalType.Grass,    () => new RegenPerk(10)),
            P("water_veil",  "물의 베일",  "HP가 절반보다 많으면 받는 데미지가 25% 줄어든다.", PalType.Water,  () => new HealthyGuardPerk(0.75f)),
            P("ground_rock", "바위 피부",  "방어가 25% 올라간다.",                          PalType.Ground,   () => new StatPerk(StatType.Defense, 1.25f)),
            P("dragon_fury", "용의 기백",  "공격이 25% 올라간다.",                          PalType.Dragon,   () => new StatPerk(StatType.Attack, 1.25f)),
            P("neutral_all", "만능",       "공격·방어·스피드가 모두 8% 올라간다.",           PalType.Neutral,  () => new AllStatPerk(1.08f)),
            P("void_shell",  "공허의 껍질", "받는 데미지가 20% 줄어든다.",                   PalType.Void,     () => new DamageTakenPerk(0.8f)),
        };

        static Perk P(string id, string name, string desc, PalType? type, Func<BattleEffect> create) =>
            new Perk { Id = id, Name = name, Description = desc, Type = type, Create = () => { var e = create(); e.PerkId = id; e.PerkName = name; e.PerkDescription = desc; return e; } };

        static Perk TypePower(PalType type, string name) =>
            P($"power_{type}", name, $"{PalTypeNames.Korean(type)} 속성 기술의 위력이 25% 늘어난다.", type, () => new TypePowerPerk(type, 1.25f));

        /// <summary>이 팰이 고를 수 있는 후보 (공용 + 내 속성, 이미 가진 것 제외) 중 count개를 무작위로</summary>
        public static List<Perk> RollChoices(Pal pal, int count, System.Random rng)
        {
            var owned = new HashSet<string>(pal.Effects.Where(e => e.PerkId != null).Select(e => e.PerkId));
            var pool = All.Where(p => !owned.Contains(p.Id) && (p.Type == null || pal.Data.HasType(p.Type.Value))).ToList();
            var picks = new List<Perk>();
            while (picks.Count < count && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                picks.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return picks;
        }
    }

    // ═════════════════ 강화 효과 동작 ═════════════════
    public class StatPerk : BattleEffect
    {
        readonly StatType stat; readonly float mult;
        public StatPerk(StatType stat, float mult) { this.stat = stat; this.mult = mult; }
        public override float StatMultiplier(StatType s) => s == stat ? mult : 1f;
    }

    public class AllStatPerk : BattleEffect
    {
        readonly float mult;
        public AllStatPerk(float mult) { this.mult = mult; }
        public override float StatMultiplier(StatType s) => mult;
    }

    public class MaxHpPerk : BattleEffect
    {
        readonly float mult;
        public MaxHpPerk(float mult) { this.mult = mult; }
        public override float MaxHpMultiplier => mult;
    }

    public class LowHpPowerPerk : BattleEffect
    {
        readonly float mult;
        public LowHpPowerPerk(float mult) { this.mult = mult; }
        public override void ModifyOutgoingDamage(Pal self, DamageInfo info)
        {
            if (self.CurrentHP * 2 <= self.MaxHP) info.ExtraMultiplier *= mult;
        }
    }

    /// <summary>전투마다 한 번, 쓰러질 공격을 HP 1로 버틴다</summary>
    public class EndurePerk : BattleEffect
    {
        bool usedThisBattle;
        public override void OnBattleStart(Pal self, BattleContext ctx) => usedThisBattle = false;
        public override bool TryPreventFaint(Pal self, BattleContext ctx)
        {
            if (usedThisBattle) return false;
            usedThisBattle = true;
            self.Revive(1);
            ctx.Log($"{Josa.Eun(self.Data.palName)} 공격을 버텼다!");
            return true;
        }
    }

    public class RegenPerk : BattleEffect
    {
        readonly int divisor;
        public RegenPerk(int divisor) { this.divisor = divisor; }
        public override void OnTurnEnd(Pal self, BattleContext ctx)
        {
            if (self.IsFainted || self.CurrentHP >= self.MaxHP) return;
            if (self.Heal(Mathf.Max(1, self.MaxHP / divisor)) > 0) ctx.Log($"{Josa.Eun(self.Data.palName)} 체력을 조금 회복했다!");
        }
    }

    /// <summary>급소가 아니었던 공격을 일정 확률로 급소로 바꾼다</summary>
    public class CritPerk : BattleEffect
    {
        readonly float extraChance;
        public CritPerk(float extraChance) { this.extraChance = extraChance; }
        public override void ModifyOutgoingDamage(Pal self, DamageInfo info)
        {
            if (info.IsCritical || UnityEngine.Random.value >= extraChance) return;
            info.IsCritical = true;
            info.CritMultiplier = BattleCalc.CritMultiplier;
        }
    }

    public class StabPerk : BattleEffect
    {
        readonly float mult;
        public StabPerk(float mult) { this.mult = mult; }
        public override void ModifyOutgoingDamage(Pal self, DamageInfo info)
        {
            if (info.StabMultiplier > 1f) info.ExtraMultiplier *= mult;
        }
    }

    public class TypePowerPerk : BattleEffect
    {
        readonly PalType type; readonly float mult;
        public TypePowerPerk(PalType type, float mult) { this.type = type; this.mult = mult; }
        public override void ModifyOutgoingDamage(Pal self, DamageInfo info)
        {
            if (info.Skill != null && info.Skill.type == type) info.ExtraMultiplier *= mult;
        }
    }

    public class DamageTakenPerk : BattleEffect
    {
        readonly float mult;
        public DamageTakenPerk(float mult) { this.mult = mult; }
        public override void ModifyIncomingDamage(Pal self, DamageInfo info) => info.ExtraMultiplier *= mult;
    }

    public class HealthyGuardPerk : BattleEffect
    {
        readonly float mult;
        public HealthyGuardPerk(float mult) { this.mult = mult; }
        public override void ModifyIncomingDamage(Pal self, DamageInfo info)
        {
            if (self.CurrentHP * 2 > self.MaxHP) info.ExtraMultiplier *= mult;
        }
    }

    public class DrainPerk : BattleEffect
    {
        readonly int divisor;
        public DrainPerk(int divisor) { this.divisor = divisor; }
        public override void OnAfterAttack(Pal self, DamageInfo info, BattleContext ctx)
        {
            if (self.IsFainted || info.FinalDamage <= 0 || self.CurrentHP >= self.MaxHP) return;
            if (self.Heal(Mathf.Max(1, info.FinalDamage / divisor)) > 0) ctx.Log($"{Josa.Eun(self.Data.palName)} 체력을 흡수했다!");
        }
    }

    /// <summary>내가 공격해서 맞히면 확률로 상대에게 상태이상</summary>
    public class StatusOnHitPerk : BattleEffect
    {
        readonly StatusCondition status; readonly float chance;
        public StatusOnHitPerk(StatusCondition status, float chance) { this.status = status; this.chance = chance; }
        public override void OnAfterAttack(Pal self, DamageInfo info, BattleContext ctx)
        {
            var target = info.Defender;
            if (target == null || target.IsFainted || info.FinalDamage <= 0) return;
            if (UnityEngine.Random.value >= chance) return;
            if (target.TrySetStatus(status)) ctx.Log($"{Josa.Eun(target.Data.palName)} {StatusRules.InflictText(status)}");
        }
    }

    /// <summary>공격을 받으면 확률로 공격한 상대에게 상태이상</summary>
    public class StatusWhenHitPerk : BattleEffect
    {
        readonly StatusCondition status; readonly float chance;
        public StatusWhenHitPerk(StatusCondition status, float chance) { this.status = status; this.chance = chance; }
        public override void OnDamaged(Pal self, DamageInfo info, BattleContext ctx)
        {
            var attacker = info.Attacker;
            if (attacker == null || attacker.IsFainted || UnityEngine.Random.value >= chance) return;
            if (attacker.TrySetStatus(status)) ctx.Log($"{Josa.Eun(attacker.Data.palName)} {StatusRules.InflictText(status)}");
        }
    }
}
