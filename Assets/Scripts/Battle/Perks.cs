using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    public enum PerkRarity { Common, Rare, Epic, Legendary }

    public static class PerkRarities
    {
        public static readonly string[] Names = { "일반", "희귀", "영웅", "전설" };
        public static readonly Color[] Colors = { Hex(0xf0f0f0), Hex(0x6cb4ff), Hex(0xc890ff), Hex(0xffcc40) };
        public static Color Color(PerkRarity r) => Colors[(int)r];
        public static string Name(PerkRarity r) => Names[(int)r];

        /// <summary>
        /// 카드 한 장의 등급 뽑기. 레벨이 높을수록 영웅이 잘 나오고, 전설은 Lv.40부터 나온다.
        ///  Lv10: 일반 60 / 희귀 28 / 영웅 11 / 전설 0      Lv90: 60 / 28 / 19 / 6.5  (비율)
        /// </summary>
        public static PerkRarity Roll(int level, System.Random rng, PerkRarity min = PerkRarity.Common)
        {
            float[] w = { 60f, 28f, 10f + level / 10f, level >= GameConfig.LegendaryPerkLevel ? 2f + level / 20f : 0f };
            for (int i = 0; i < (int)min; i++) w[i] = 0f;
            float roll = (float)rng.NextDouble() * w.Sum();
            for (int i = 0; i < w.Length; i++)
            {
                if (roll < w[i]) return (PerkRarity)i;
                roll -= w[i];
            }
            return min;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }

    /// <summary>
    /// 강화 효과(퍽) 하나의 정보. 고르면 Create(Lv)로 만든 BattleEffect가 그 팰에 영구히 붙는다.
    /// 같은 퍽을 또 고르면 Lv이 오른다 (최대 MaxRank). Type이 있으면 그 속성 팰에게만 나온다.
    /// </summary>
    public class Perk
    {
        public string Id;
        public string Name;
        public PalType? Type;
        public PerkRarity Rarity;
        public int MaxRank = 1;
        public Func<int, string> Describe;          // Lv → 설명
        public Func<int, BattleEffect> Make;        // Lv → 효과

        public BattleEffect Create(int rank)
        {
            var e = Make(rank);
            e.PerkId = Id;
            e.PerkName = MaxRank > 1 ? $"{Name} Lv.{rank}" : Name;
            e.PerkRank = rank;
            e.PerkDescription = Describe(rank);
            return e;
        }
    }

    /// <summary>카드 한 장: 어떤 퍽을 몇 Lv로 얻는지</summary>
    public class PerkOffer
    {
        public Perk Perk;
        public int Rank;                            // 고르면 이 Lv이 된다 (1 = 새로 얻음)
    }

    /// <summary>
    /// 강화 효과 목록. 10레벨마다 카드 3장(등급은 무작위)을 보여주고 1장을 고른다.
    /// 이미 가진 퍽이 나오면 Lv 업 카드가 된다. 새 퍽은 All에 한 줄 추가하면 된다.
    /// </summary>
    public static class PerkCatalog
    {
        static string Pct(float v) => $"{Mathf.RoundToInt(v * 100)}%";

        public static readonly List<Perk> All = BuildAll();

        static List<Perk> BuildAll()
        {
            var list = new List<Perk>
            {
                // ───── 일반 (Lv.3까지) ─────
                P("atk_up", "힘 단련",   PerkRarity.Common, 3, r => $"공격이 {r * 10}% 올라간다.",    r => new StatPerk(StatType.Attack, 1f + 0.1f * r)),
                P("def_up", "철갑",      PerkRarity.Common, 3, r => $"방어가 {r * 10}% 올라간다.",    r => new StatPerk(StatType.Defense, 1f + 0.1f * r)),
                P("spd_up", "질풍",      PerkRarity.Common, 3, r => $"스피드가 {r * 10}% 올라간다.",  r => new StatPerk(StatType.Speed, 1f + 0.1f * r)),
                P("hp_up",  "튼튼한 몸", PerkRarity.Common, 3, r => $"최대 HP가 {r * 10}% 늘어난다.", r => new MaxHpPerk(1f + 0.1f * r)),

                // ───── 희귀 ─────
                P("regen", "재생",        PerkRarity.Rare, 3, r => $"턴이 끝날 때마다 최대 HP의 1/{Regen(r)}을 회복한다.", r => new RegenPerk(Regen(r))),
                P("crit",  "급소 노리기", PerkRarity.Rare, 3, r => $"급소에 맞힐 확률이 {r * 8}% 늘어난다.",              r => new CritPerk(0.08f * r)),
                P("drain", "흡혈",        PerkRarity.Rare, 3, r => $"공격으로 준 데미지의 1/{Drain(r)}만큼 회복한다.",     r => new DrainPerk(Drain(r))),
                P("guard", "방어 태세",   PerkRarity.Rare, 3, r => $"받는 데미지가 {r * 7}% 줄어든다.",                   r => new DamageTakenPerk(1f - 0.07f * r)),
                P("last",  "배수의 진",   PerkRarity.Rare, 2, r => $"HP가 절반 이하면 주는 데미지가 {r * 25}% 늘어난다.",  r => new LowHpPowerPerk(1f + 0.25f * r)),

                // ───── 영웅 ─────
                P("stab",     "적응력",     PerkRarity.Epic, 2, r => $"자기 속성 기술의 위력이 {r * 20}% 늘어난다.",          r => new StabPerk(1f + 0.2f * r)),
                P("allstat",  "균형",       PerkRarity.Epic, 2, r => $"공격·방어·스피드가 모두 {r * 8}% 올라간다.",            r => new AllStatPerk(1f + 0.08f * r)),
                P("followup", "추격",       PerkRarity.Epic, 2, r => $"공격한 뒤 {(r == 1 ? 25 : 40)}% 확률로 1/3 위력의 추가타를 넣는다.", r => new FollowUpPerk(r == 1 ? 0.25f : 0.4f)),
                P("weak",     "약점 간파",  PerkRarity.Epic, 2, r => $"효과가 굉장한 공격의 데미지가 {r * 25}% 늘어난다.",     r => new SuperEffectivePerk(1f + 0.25f * r)),
                P("endure",   "버티기",     PerkRarity.Epic, 1, _ => "전투마다 한 번, 쓰러질 공격을 HP 1로 버틴다.",          _ => new EndurePerk()),
                P("heat",     "전투의 열기", PerkRarity.Epic, 1, _ => "공격을 맞히면 공격이 1랭크 오른다. (전투마다 2번까지)", _ => new BattleHeatPerk(2)),
                P("accel",    "가속",       PerkRarity.Epic, 1, _ => "턴이 끝날 때마다 스피드가 1랭크 오른다.",               _ => new AccelPerk()),

                // ───── 전설 (Lv.40부터) ─────
                P("phoenix",  "불사조",     PerkRarity.Legendary, 1, _ => "전투마다 한 번, 쓰러지면 최대 HP의 절반으로 되살아난다.", _ => new PhoenixPerk()),
                P("berserk",  "광전사",     PerkRarity.Legendary, 1, _ => "공격이 35% 오르지만 방어가 10% 내려간다.",            _ => new StatBundle(1f, 1.35f, 0.9f, 1f)),
                P("vampire",  "흡혈귀",     PerkRarity.Legendary, 1, _ => "공격으로 준 데미지의 1/3만큼 회복한다.",              _ => new DrainPerk(3)),
                P("blood",    "전설의 피",  PerkRarity.Legendary, 1, _ => "최대 HP와 공격·방어·스피드가 모두 15% 올라간다.",      _ => new StatBundle(1.15f, 1.15f, 1.15f, 1.15f)),
                P("first",    "선수필승",   PerkRarity.Legendary, 1, _ => "전투에서 처음 하는 공격의 데미지가 2배가 된다.",       _ => new FirstStrikePerk(2f)),
                P("intim",    "위압",       PerkRarity.Legendary, 1, _ => "전투 첫 턴에 나와 있으면 상대의 공격을 1랭크 떨어뜨린다.", _ => new IntimidatePerk()),
            };

            // ───── 속성 전용: 그 속성 기술 위력 (일반, Lv.3까지) ─────
            foreach (var (type, name) in new[]
            {
                (PalType.Neutral, "무속성 숙련"), (PalType.Fire, "불꽃 숙련"), (PalType.Water, "물결 숙련"), (PalType.Grass, "초목 숙련"),
                (PalType.Electric, "번개 숙련"), (PalType.Ground, "대지 숙련"), (PalType.Ice, "냉기 숙련"), (PalType.Dragon, "용의 숙련"),
                (PalType.Dark, "어둠 숙련"), (PalType.Void, "공허 숙련"),
            })
            {
                string typeName = PalTypeNames.Korean(type);
                list.Add(P($"power_{type}", name, PerkRarity.Common, 3, r => $"{typeName} 속성 기술의 위력이 {r * 15}% 늘어난다.",
                           r => new TypePowerPerk(type, 1f + 0.15f * r), type));
            }

            // ───── 속성 전용: 특색 있는 효과 (희귀, Lv.2까지) ─────
            list.AddRange(new[]
            {
                P("fire_burn",   "불씨",       PerkRarity.Rare, 2, r => $"공격하면 {r * 15}% 확률로 상대가 화상을 입는다.",       r => new StatusOnHitPerk(StatusCondition.Burn, 0.15f * r), PalType.Fire),
                P("ice_freeze",  "서리",       PerkRarity.Rare, 2, r => $"공격하면 {(r == 1 ? 8 : 15)}% 확률로 상대를 얼린다.",    r => new StatusOnHitPerk(StatusCondition.Freeze, r == 1 ? 0.08f : 0.15f), PalType.Ice),
                P("elec_static", "정전기",     PerkRarity.Rare, 2, r => $"공격을 받으면 {r * 25}% 확률로 상대를 마비시킨다.",       r => new StatusWhenHitPerk(StatusCondition.Paralysis, 0.25f * r), PalType.Electric),
                P("dark_poison", "독침",       PerkRarity.Rare, 2, r => $"공격하면 {r * 15}% 확률로 상대를 독에 걸리게 한다.",     r => new StatusOnHitPerk(StatusCondition.Poison, 0.15f * r), PalType.Dark),
                P("grass_photo", "광합성",     PerkRarity.Rare, 2, r => $"턴이 끝날 때마다 최대 HP의 1/{(r == 1 ? 12 : 8)}을 회복한다.", r => new RegenPerk(r == 1 ? 12 : 8), PalType.Grass),
                P("water_veil",  "물의 베일",  PerkRarity.Rare, 2, r => $"HP가 절반보다 많으면 받는 데미지가 {(r == 1 ? 20 : 35)}% 줄어든다.", r => new HealthyGuardPerk(r == 1 ? 0.8f : 0.65f), PalType.Water),
                P("ground_rock", "바위 피부",  PerkRarity.Rare, 2, r => $"방어가 {r * 20}% 올라간다.",                            r => new StatPerk(StatType.Defense, 1f + 0.2f * r), PalType.Ground),
                P("dragon_fury", "용의 기백",  PerkRarity.Rare, 2, r => $"공격이 {r * 20}% 올라간다.",                            r => new StatPerk(StatType.Attack, 1f + 0.2f * r), PalType.Dragon),
                P("neutral_all", "만능",       PerkRarity.Rare, 2, r => $"공격·방어·스피드가 모두 {r * 8}% 올라간다.",              r => new AllStatPerk(1f + 0.08f * r), PalType.Neutral),
                P("void_shell",  "공허의 껍질", PerkRarity.Rare, 2, r => $"받는 데미지가 {r * 15}% 줄어든다.",                     r => new DamageTakenPerk(1f - 0.15f * r), PalType.Void),
            });
            return list;
        }

        static int Regen(int r) => r switch { 1 => 16, 2 => 12, _ => 8 };
        static int Drain(int r) => r switch { 1 => 8, 2 => 6, _ => 4 };

        static Perk P(string id, string name, PerkRarity rarity, int maxRank, Func<int, string> describe, Func<int, BattleEffect> make,
                      PalType? type = null) =>
            new Perk { Id = id, Name = name, Rarity = rarity, MaxRank = maxRank, Describe = describe, Make = make, Type = type };

        public static Perk Get(string id) => All.FirstOrDefault(p => p.Id == id);

        /// <summary>이 팰이 가진 그 퍽의 Lv (없으면 0)</summary>
        public static int RankOf(Pal pal, string perkId) =>
            pal.Effects.Where(e => e.PerkId == perkId).Select(e => e.PerkRank).DefaultIfEmpty(0).Max();

        static bool CanOffer(Perk p, Pal pal) =>
            (p.Type == null || pal.Data.HasType(p.Type.Value)) && RankOf(pal, p.Id) < p.MaxRank;

        /// <summary>
        /// 카드 count장: 카드마다 등급을 뽑고, 그 등급에서 아직 최대 Lv이 아닌 퍽을 고른다.
        /// 그 등급에 남은 퍽이 없으면 가까운 등급(아래 → 위)에서 찾는다. 같은 퍽은 두 번 나오지 않는다.
        /// </summary>
        public static List<PerkOffer> RollChoices(Pal pal, int count, System.Random rng, PerkRarity minRarity = PerkRarity.Common)
        {
            var offers = new List<PerkOffer>();
            for (int i = 0; i < count; i++)
            {
                var want = PerkRarities.Roll(pal.Level, rng, minRarity);
                Perk pick = null;
                var order = Enumerable.Range(0, 4).Where(t => t >= (int)minRarity)
                                      .OrderBy(t => Math.Abs(t - (int)want) * 2 + (t > (int)want ? 1 : 0));   // 원하는 등급 → 한 단계 아래 → 위 …
                foreach (int t in order)
                {
                    if (t == (int)PerkRarity.Legendary && pal.Level < GameConfig.LegendaryPerkLevel && minRarity < PerkRarity.Legendary) continue;
                    var pool = All.Where(p => (int)p.Rarity == t && CanOffer(p, pal) && offers.All(o => o.Perk != p)).ToList();
                    if (pool.Count == 0) continue;
                    pick = pool[rng.Next(pool.Count)];
                    break;
                }
                if (pick == null) break;
                offers.Add(new PerkOffer { Perk = pick, Rank = RankOf(pal, pick.Id) + 1 });
            }
            return offers;
        }

        /// <summary>고른 카드를 팰에게 적용한다 (Lv 업이면 예전 효과를 새 Lv 효과로 바꾼다)</summary>
        public static void Apply(Pal pal, PerkOffer offer)
        {
            foreach (var old in pal.Effects.Where(e => e.PerkId == offer.Perk.Id).ToList()) pal.RemoveEffect(old);
            pal.AddEffect(offer.Perk.Create(offer.Rank));
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

    /// <summary>주는 데미지 배율 (모든 기술)</summary>
    public class PowerPerk : BattleEffect
    {
        readonly float mult;
        public PowerPerk(float mult) { this.mult = mult; }
        public override void ModifyOutgoingDamage(Pal self, DamageInfo info) => info.ExtraMultiplier *= mult;
    }

    /// <summary>효과가 굉장한 공격의 데미지 배율</summary>
    public class SuperEffectivePerk : BattleEffect
    {
        readonly float mult;
        public SuperEffectivePerk(float mult) { this.mult = mult; }
        public override void ModifyOutgoingDamage(Pal self, DamageInfo info)
        {
            if (info.TypeMultiplier > 1f) info.ExtraMultiplier *= mult;
        }
    }

    /// <summary>추격: 공격 후 확률로 1/3 위력 추가타</summary>
    public class FollowUpPerk : BattleEffect
    {
        readonly float chance;
        public FollowUpPerk(float chance) { this.chance = chance; }
        public override void OnAfterAttack(Pal self, DamageInfo info, BattleContext ctx)
        {
            var target = info.Defender;
            if (self.IsFainted || target == null || target.IsFainted || info.FinalDamage <= 0) return;
            if (UnityEngine.Random.value >= chance) return;
            target.TakeDamage(Mathf.Max(1, info.FinalDamage / 3));
            ctx.Log($"{Josa.I(self.Data.palName)} 추격했다!");
        }
    }

    /// <summary>전투의 열기: 공격을 맞히면 공격 1랭크 상승 (전투마다 max번)</summary>
    public class BattleHeatPerk : BattleEffect
    {
        readonly int max; int used;
        public BattleHeatPerk(int max) { this.max = max; }
        public override void OnBattleStart(Pal self, BattleContext ctx) => used = 0;
        public override void OnAfterAttack(Pal self, DamageInfo info, BattleContext ctx)
        {
            if (self.IsFainted || info.FinalDamage <= 0 || used >= max) return;
            if (self.ModifyStage(StatType.Attack, 1) <= 0) return;
            used++;
            ctx.Log($"전투의 열기로 {self.Data.palName}의 공격이 올라갔다!");
        }
    }

    /// <summary>가속: 턴이 끝날 때마다 스피드 1랭크 상승</summary>
    public class AccelPerk : BattleEffect
    {
        public override void OnTurnEnd(Pal self, BattleContext ctx)
        {
            if (self.IsFainted || ctx.OpponentOf(ctx.OpponentOf(self)) != self) return;   // 나와 있는 팰만
            if (self.ModifyStage(StatType.Speed, 1) > 0) ctx.Log($"가속으로 {self.Data.palName}의 스피드가 올라갔다!");
        }
    }

    /// <summary>불사조: 전투마다 한 번, 쓰러지면 최대 HP 절반으로 부활</summary>
    public class PhoenixPerk : BattleEffect
    {
        bool used;
        public override void OnBattleStart(Pal self, BattleContext ctx) => used = false;
        public override bool TryPreventFaint(Pal self, BattleContext ctx)
        {
            if (used) return false;
            used = true;
            self.Revive(Mathf.Max(1, self.MaxHP / 2));
            ctx.Log($"{Josa.I(self.Data.palName)} 불꽃 속에서 되살아났다!");
            return true;
        }
    }

    /// <summary>선수필승: 전투에서 처음 하는 공격의 데미지 배율</summary>
    public class FirstStrikePerk : BattleEffect
    {
        readonly float mult; bool used;
        public FirstStrikePerk(float mult) { this.mult = mult; }
        public override void OnBattleStart(Pal self, BattleContext ctx) => used = false;
        public override void ModifyOutgoingDamage(Pal self, DamageInfo info)
        {
            if (used) return;
            used = true;
            info.ExtraMultiplier *= mult;
        }
    }

    /// <summary>위압: 전투 시작 때 나와 있으면 상대 공격 1랭크 하락</summary>
    public class IntimidatePerk : BattleEffect
    {
        bool done;
        public override void OnBattleStart(Pal self, BattleContext ctx) => done = false;
        public override void OnTurnStart(Pal self, BattleContext ctx)
        {
            if (done) return;
            done = true;                                                                   // 첫 턴에 나와 있을 때만
            var foe = ctx.OpponentOf(self);
            if (foe == null || self.IsFainted || foe.IsFainted || ctx.OpponentOf(foe) != self) return;
            if (foe.ModifyStage(StatType.Attack, -1) < 0) ctx.Log($"{self.Data.palName}의 위압에 {foe.Data.palName}의 공격이 떨어졌다!");
        }
    }
}
