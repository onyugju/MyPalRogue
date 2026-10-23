using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    // ═══════════════════════════════════════════════════════════════
    //  팰로그 성장 시스템 (포켓몬의 진화·메가진화·다이맥스 대신)
    //   ① 강화 효과(퍽)  : 10레벨마다 카드 3장 중 1장. 등급(일반~전설) + 같은 퍽을 또 고르면 Lv 업  → Perks.cs
    //   ② 각성           : Lv.50에 각성 하나를 고른다. 전투 중 HP가 절반 이하가 되면 깨어나 그 전투 동안 강해진다.
    //                      Lv.100이면 진각성(더 강해짐). 전설·최종 보스도 각성한다.
    //   ③ 응축 ★         : 파티에 있는 팰과 같은 팰을 잡으면 응축해서 ★을 올린다 (팰월드 팰 응축). ★당 모든 능력치 +10%
    //   ④ 속성 시너지    : 파티에 같은 속성 팰이 2/3/4마리 이상이면 그 속성 팰들이 보너스를 받는다
    // ═══════════════════════════════════════════════════════════════

    /// <summary>여러 효과를 하나로 묶어 대신 실행하는 효과 (각성·시너지가 쓴다). IsOn이 false면 아무것도 안 한다</summary>
    public abstract class CompositeEffect : BattleEffect
    {
        protected readonly List<BattleEffect> Parts = new List<BattleEffect>();
        protected virtual bool IsOn => true;

        public override float MaxHpMultiplier
        {
            get { float m = 1f; if (IsOn) foreach (var p in Parts) m *= p.MaxHpMultiplier; return m; }
        }
        public override float StatMultiplier(StatType stat)
        {
            float m = 1f;
            if (IsOn) foreach (var p in Parts) m *= p.StatMultiplier(stat);
            return m;
        }
        public override void OnBattleStart(Pal self, BattleContext ctx) { foreach (var p in Parts) p.OnBattleStart(self, ctx); }
        public override void OnTurnStart(Pal self, BattleContext ctx) { if (IsOn) foreach (var p in Parts) p.OnTurnStart(self, ctx); }
        public override void ModifyOutgoingDamage(Pal self, DamageInfo info) { if (IsOn) foreach (var p in Parts) p.ModifyOutgoingDamage(self, info); }
        public override void ModifyIncomingDamage(Pal self, DamageInfo info) { if (IsOn) foreach (var p in Parts) p.ModifyIncomingDamage(self, info); }
        public override void OnAfterAttack(Pal self, DamageInfo info, BattleContext ctx) { if (IsOn) foreach (var p in Parts) p.OnAfterAttack(self, info, ctx); }
        public override void OnDamaged(Pal self, DamageInfo info, BattleContext ctx) { if (IsOn) foreach (var p in Parts) p.OnDamaged(self, info, ctx); }
        public override bool TryPreventFaint(Pal self, BattleContext ctx)
        {
            if (!IsOn) return false;
            foreach (var p in Parts) if (p.TryPreventFaint(self, ctx)) return true;
            return false;
        }
        public override void OnFainted(Pal self, BattleContext ctx) { if (IsOn) foreach (var p in Parts) p.OnFainted(self, ctx); }
        public override void OnTurnEnd(Pal self, BattleContext ctx) { if (IsOn) foreach (var p in Parts) p.OnTurnEnd(self, ctx); }
    }

    // ═════════════════ ② 각성 ═════════════════

    /// <summary>각성 종류 하나 (고르기 카드에 보이는 정보 + 각성했을 때 붙는 힘)</summary>
    public class AwakeningDef
    {
        public string Id, Name, Description;
        public PalType? Type;                       // 비어 있으면 모든 팰
        public Func<List<BattleEffect>> Powers;     // 각성 중에만 켜지는 효과들
        public Action<Pal, BattleContext> OnAwaken; // 각성하는 순간 한 번 (회복, 랭크업 등)
    }

    /// <summary>
    /// 각성: 전투마다 한 번, HP가 절반 이하가 되면 깨어나 그 전투가 끝날 때까지 모든 능력치 ×1.2 (진각성 ×1.35) + 고유 효과.
    /// </summary>
    public class Awakening : CompositeEffect
    {
        public AwakeningDef Def { get; }
        public bool Active { get; private set; }
        /// <summary>진각성 (Lv.100 또는 최종 보스)</summary>
        public bool IsTrue { get; private set; }

        public Awakening(AwakeningDef def, bool isTrue = false)
        {
            Def = def;
            Parts.AddRange(def.Powers());
            PerkId = "awaken_" + def.Id;
            PerkName = def.Name;
            SetTrue(isTrue);
        }

        public void SetTrue(bool on)
        {
            IsTrue = on;
            float m = on ? GameConfig.TrueAwakenStatMultiplier : GameConfig.AwakenStatMultiplier;
            PerkDescription = $"HP 절반 이하에서 각성: 모든 능력치 ×{m:0.##}, {Def.Description}";
        }

        protected override bool IsOn => Active;

        public override float StatMultiplier(StatType stat)
        {
            if (!Active) return 1f;
            return base.StatMultiplier(stat) * (IsTrue ? GameConfig.TrueAwakenStatMultiplier : GameConfig.AwakenStatMultiplier);
        }

        public override void OnBattleStart(Pal self, BattleContext ctx)
        {
            Active = false;                                         // 전투마다 다시 잠든다
            base.OnBattleStart(self, ctx);
        }

        /// <summary>전투가 끝나면 다시 잠든다</summary>
        public void Sleep() => Active = false;

        public override void OnDamaged(Pal self, DamageInfo info, BattleContext ctx)
        {
            base.OnDamaged(self, info, ctx);
            TryAwaken(self, ctx);
        }

        public override void OnTurnStart(Pal self, BattleContext ctx)
        {
            TryAwaken(self, ctx);                                    // 이미 HP가 절반 이하로 싸움에 나왔으면 첫 턴에 바로
            base.OnTurnStart(self, ctx);
        }

        public override void OnTurnEnd(Pal self, BattleContext ctx)
        {
            base.OnTurnEnd(self, ctx);
            TryAwaken(self, ctx);                                    // 독·화상으로 HP가 줄어도
        }

        void TryAwaken(Pal self, BattleContext ctx)
        {
            if (Active || self.IsFainted || self.CurrentHP > self.MaxHP * GameConfig.AwakenHpRatio) return;
            Active = true;
            ctx.Awakened(self);                                     // 화면: 반짝이는 연출
            string name = self.Data.palName;
            ctx.Log(IsTrue ? $"{name}의 진정한 힘이 깨어났다!\n진각성 · {Def.Name}!" : $"{name}의 숨겨진 힘이 깨어났다!\n각성 · {Def.Name}!");
            Def.OnAwaken?.Invoke(self, ctx);
        }
    }

    public static class AwakeningCatalog
    {
        public static readonly List<AwakeningDef> All = new List<AwakeningDef>
        {
            // ───── 공용 ─────
            A("fighting", "투지 각성", null, "각성하는 순간 공격·방어·스피드 1랭크 상승", () => new List<BattleEffect>(),
              (p, ctx) =>
              {
                  foreach (StatType s in Enum.GetValues(typeof(StatType))) p.ModifyStage(s, 1);
                  ctx.Log($"{p.Data.palName}의 공격·방어·스피드가 올라갔다!");
              }),
            A("unyielding", "불굴 각성", null, "각성하는 순간 최대 HP의 1/3 회복", () => new List<BattleEffect>(),
              (p, ctx) => { if (p.Heal(p.MaxHP / 3) > 0) ctx.Log($"{Josa.Eun(p.Data.palName)} 힘을 되찾았다!"); }),
            A("frenzy", "광폭 각성", null, "주는 데미지 +30%", () => new List<BattleEffect> { new PowerPerk(1.3f) }),

            // ───── 속성 ─────
            A("fire", "업화 각성", PalType.Fire, "불 기술 위력 +50%, 공격 시 30% 화상",
              () => new List<BattleEffect> { new TypePowerPerk(PalType.Fire, 1.5f), new StatusOnHitPerk(StatusCondition.Burn, 0.3f) }),
            A("water", "해류 각성", PalType.Water, "매 턴 최대 HP의 1/8 회복", () => new List<BattleEffect> { new RegenPerk(8) }),
            A("grass", "만개 각성", PalType.Grass, "준 데미지의 1/2 흡수", () => new List<BattleEffect> { new DrainPerk(2) }),
            A("electric", "뇌신 각성", PalType.Electric, "스피드 +50%, 공격 시 30% 마비",
              () => new List<BattleEffect> { new StatPerk(StatType.Speed, 1.5f), new StatusOnHitPerk(StatusCondition.Paralysis, 0.3f) }),
            A("ground", "대지 각성", PalType.Ground, "받는 데미지 -35%", () => new List<BattleEffect> { new DamageTakenPerk(0.65f) }),
            A("ice", "빙하 각성", PalType.Ice, "공격 시 25% 얼림, 받는 데미지 -15%",
              () => new List<BattleEffect> { new StatusOnHitPerk(StatusCondition.Freeze, 0.25f), new DamageTakenPerk(0.85f) }),
            A("dragon", "용왕 각성", PalType.Dragon, "공격 +40%", () => new List<BattleEffect> { new StatPerk(StatType.Attack, 1.4f) }),
            A("dark", "그림자 각성", PalType.Dark, "공격이 50% 확률로 급소", () => new List<BattleEffect> { new CritPerk(0.5f) }),
            A("neutral", "무아 각성", PalType.Neutral, "모든 기술 위력 +25%, 받는 데미지 -10%",
              () => new List<BattleEffect> { new PowerPerk(1.25f), new DamageTakenPerk(0.9f) }),
            A("void", "공허 각성", PalType.Void, "주는 데미지 +30%, 받는 데미지 -30%",
              () => new List<BattleEffect> { new PowerPerk(1.3f), new DamageTakenPerk(0.7f) }),
        };

        static AwakeningDef A(string id, string name, PalType? type, string desc, Func<List<BattleEffect>> powers,
                              Action<Pal, BattleContext> onAwaken = null) =>
            new AwakeningDef { Id = id, Name = name, Type = type, Description = desc, Powers = powers, OnAwaken = onAwaken };

        public static AwakeningDef Get(string id) => All.FirstOrDefault(a => a.Id == id);

        /// <summary>이 팰이 고를 수 있는 각성 count개: 내 속성 각성을 먼저 넣고 나머지는 공용에서</summary>
        public static List<AwakeningDef> RollChoices(Pal pal, int count, System.Random rng)
        {
            var own = All.Where(a => a.Type.HasValue && pal.Data.HasType(a.Type.Value)).OrderBy(_ => rng.Next()).ToList();
            var common = All.Where(a => a.Type == null).OrderBy(_ => rng.Next()).ToList();
            return own.Concat(common).Take(count).ToList();
        }

        /// <summary>상대(전설·최종 보스)용: 그 팰 속성의 각성, 없으면 광폭 각성</summary>
        public static AwakeningDef ForEnemy(Pal pal) =>
            All.FirstOrDefault(a => a.Type.HasValue && a.Type.Value == pal.Data.primaryType) ?? Get("frenzy");

        public static Awakening Of(Pal pal) => pal.Effects.OfType<Awakening>().FirstOrDefault();
    }

    // ═════════════════ ④ 속성 시너지 ═════════════════

    /// <summary>시너지로 붙은 효과 (파티가 바뀔 때마다 다시 계산한다)</summary>
    public class SynergyEffect : CompositeEffect
    {
        public PalType Type { get; }
        public int Level { get; }
        public SynergyEffect(PalType type, int level, BattleEffect power, string description)
        {
            Type = type; Level = level;
            Parts.Add(power);
            PerkName = $"{PalTypeNames.Korean(type)} 시너지 Lv.{level}";
            PerkDescription = description;
        }
    }

    public static class Synergies
    {
        /// <summary>같은 속성 팰이 이만큼 있으면 시너지 Lv.1 / 2 / 3</summary>
        public static readonly int[] Thresholds = { 2, 3, 4 };

        /// <summary>속성별 시너지: (레벨 → 설명, 레벨 → 효과). Lv은 1~3</summary>
        static (Func<int, string> describe, Func<int, BattleEffect> make)? Def(PalType type)
        {
            float[] small = { 0f, 0.05f, 0.10f, 0.20f };
            string Pct(float v) => $"{Mathf.RoundToInt(v * 100)}%";
            switch (type)
            {
                case PalType.Fire:     return (l => $"공격 +{Pct(small[l])}",  l => new StatPerk(StatType.Attack, 1f + small[l]));
                case PalType.Water:    return (l => $"최대 HP +{Pct(small[l])}", l => new MaxHpPerk(1f + small[l]));
                case PalType.Grass:    return (l => $"매 턴 최대 HP의 1/{new[] { 0, 32, 24, 16 }[l]} 회복", l => new RegenPerk(new[] { 0, 32, 24, 16 }[l]));
                case PalType.Electric: return (l => $"스피드 +{Pct(small[l])}", l => new StatPerk(StatType.Speed, 1f + small[l]));
                case PalType.Ground:   return (l => $"방어 +{Pct(small[l])}",  l => new StatPerk(StatType.Defense, 1f + small[l]));
                case PalType.Ice:      return (l => $"공격 시 {new[] { 0, 5, 10, 15 }[l]}% 얼림", l => new StatusOnHitPerk(StatusCondition.Freeze, new[] { 0f, 0.05f, 0.10f, 0.15f }[l]));
                case PalType.Dragon:   return (l => $"드래곤 기술 위력 +{l * 10}%", l => new TypePowerPerk(PalType.Dragon, 1f + l * 0.1f));
                case PalType.Dark:     return (l => $"급소 확률 +{l * 5}%", l => new CritPerk(l * 0.05f));
                case PalType.Neutral:  return (l => $"받는 데미지 -{l * 4}%", l => new DamageTakenPerk(1f - l * 0.04f));
                default: return null;
            }
        }

        public static int LevelFor(int count)
        {
            int level = 0;
            foreach (int t in Thresholds) if (count >= t) level++;
            return level;
        }

        /// <summary>파티의 속성별 팰 수 (복합 속성은 두 속성 모두 센다)</summary>
        public static Dictionary<PalType, int> Count(IEnumerable<Pal> party)
        {
            var counts = new Dictionary<PalType, int>();
            foreach (var p in party)
                foreach (PalType t in Enum.GetValues(typeof(PalType)))
                    if (p.Data.HasType(t)) counts[t] = counts.TryGetValue(t, out int c) ? c + 1 : 1;
            return counts;
        }

        /// <summary>지금 켜진 시너지 (속성 → 레벨)</summary>
        public static Dictionary<PalType, int> ActiveLevels(IEnumerable<Pal> party) =>
            Count(party).Where(kv => Def(kv.Key) != null && LevelFor(kv.Value) > 0)
                        .ToDictionary(kv => kv.Key, kv => LevelFor(kv.Value));

        public static string Describe(PalType type, int level) => Def(type)?.describe(level) ?? "";

        /// <summary>
        /// 파티의 시너지 효과를 다시 붙인다. 바뀐 게 없으면 건드리지 않는다.
        /// 새로 켜지거나 레벨이 바뀐 시너지 / 꺼진 시너지를 알림 문장으로 돌려준다.
        /// </summary>
        public static List<string> Apply(IList<Pal> party)
        {
            var now = ActiveLevels(party);
            var before = new Dictionary<PalType, int>();
            foreach (var p in party)
                foreach (var s in p.Effects.OfType<SynergyEffect>())
                    before[s.Type] = s.Level;

            // 각 팰이 지금 가져야 할 시너지와 실제로 가진 시너지가 모두 같으면 그대로 둔다
            bool same = party.All(p =>
            {
                var want = now.Where(kv => p.Data.HasType(kv.Key)).Select(kv => (kv.Key, kv.Value)).OrderBy(x => x.Key).ToList();
                var have = p.Effects.OfType<SynergyEffect>().Select(s => (s.Type, s.Level)).OrderBy(x => x.Type).ToList();
                return want.SequenceEqual(have);
            });
            var messages = new List<string>();
            if (same) return messages;

            foreach (var p in party)
            {
                foreach (var old in p.Effects.OfType<SynergyEffect>().ToList()) p.RemoveEffect(old);
                foreach (var kv in now)
                {
                    if (!p.Data.HasType(kv.Key)) continue;
                    var def = Def(kv.Key).Value;
                    p.AddEffect(new SynergyEffect(kv.Key, kv.Value, def.make(kv.Value), def.describe(kv.Value)));
                }
            }

            foreach (var kv in now.OrderBy(kv => kv.Key))
            {
                var type = kv.Key; int level = kv.Value;
                before.TryGetValue(type, out int old);
                if (level > old)
                    messages.Add($"{PalTypeNames.Korean(type)} 시너지 Lv.{level} 발동!\n{PalTypeNames.Korean(type)}속성 팰: {Describe(type, level)}");
                else if (level < old)
                    messages.Add($"{PalTypeNames.Korean(type)} 시너지가 Lv.{level}로 내려갔다.\n{PalTypeNames.Korean(type)}속성 팰: {Describe(type, level)}");
            }
            foreach (var type in before.Keys.Where(t => !now.ContainsKey(t)).OrderBy(t => t))
                messages.Add($"{PalTypeNames.Korean(type)} 시너지가 사라졌다.");
            return messages;
        }
    }
}
