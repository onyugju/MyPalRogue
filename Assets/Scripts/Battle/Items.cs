using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    /// <summary>아이템 등급. 팰 스피어와 같은 이름을 쓴다 (포켓로그: 일반 / 슈퍼 / 하이퍼 / 로그 / 마스터)</summary>
    public enum ItemTier { Common, Giga, Hyper, Ultimate, Exotic }

    /// <summary>아이템을 누구에게 쓰는지</summary>
    public enum ItemTarget
    {
        None,      // 고르는 대상 없음 (스피어, 돈, 파티 전체 효과)
        Pal,       // 팰 1마리를 고른다
        PalSkill,  // 팰 1마리 → 기술 1개를 고른다 (PP 회복)
    }

    public static class ItemTiers
    {
        public static readonly string[] Names = { "일반", "기가", "하이퍼", "얼티밋", "엑조틱" };

        /// <summary>등급별 글자 색 (포켓로그 getModifierTierTextTint와 같은 값)</summary>
        public static readonly Color[] TextColors =
        {
            Hex(0xf8f8f8),   // 일반   (포켓로그 COMMON)
            Hex(0x4998f8),   // 기가   (GREAT)
            Hex(0xf8d038),   // 하이퍼 (ULTRA)
            Hex(0xdb4343),   // 얼티밋 (ROGUE)
            Hex(0xe331c5),   // 엑조틱 (MASTER)
        };

        /// <summary>보상 한 칸의 등급이 뽑힐 가중치 (포켓로그 tierWeights: 768 / 195 / 48 / 12 / 1, 합 1024)</summary>
        public static readonly int[] RollWeights = { 768, 195, 48, 12, 1 };

        public static Color Color(ItemTier t) => TextColors[(int)t];

        static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }

    /// <summary>아이템을 쓴 결과: 화면에 띄울 문장 + (이상한사탕처럼) 레벨이 올랐다면 그 정보</summary>
    public class ItemOutcome
    {
        public string Message;
        public List<(int level, List<SkillData> newSkills)> LevelUps;
        /// <summary>기술머신: 이 기술을 대상 팰에게 가르친다 (기술이 4개면 화면에서 잊을 기술을 고른다)</summary>
        public SkillData TeachSkill;
        public static ItemOutcome Say(string message) => new ItemOutcome { Message = message };
    }

    public class ItemContext
    {
        public Run Run;
        public Pal Target;         // ItemTarget.Pal / PalSkill
        public int SkillIndex = -1; // ItemTarget.PalSkill
    }

    /// <summary>아이템 한 종류의 정의 (이름, 설명, 이미지, 등급, 등장 가중치, 효과)</summary>
    public class ItemDef
    {
        public string Id;                 // 아이템 고유 이름. 보통 이미지 파일 이름과 같다 (Resources/2.아이템/{Id}.png)
        public string Icon;               // 이미지 이름이 Id와 다를 때 (예: 기술머신은 속성별 tm_fire 등)
        public string Name;
        public string Description;
        public ItemTier Tier;
        public ItemTarget Target;
        public bool IsHeld;               // 팰에게 지니게 하는 아이템 (효과가 계속 남는다)
        public SphereType? Sphere;        // 스피어 아이템이면 팰 스피어 이미지를 쓴다

        /// <summary>보상으로 나올 가중치. 0이면 이번에는 안 나온다 (예: 다친 팰이 없으면 상처약 0)</summary>
        public Func<Run, float> Weight = _ => 0f;
        /// <summary>이 팰에게 쓸 수 없으면 이유, 쓸 수 있으면 null</summary>
        public Func<Pal, string> CannotUse = _ => null;
        /// <summary>대상이 없는 아이템을 지금 쓸 수 없으면 이유, 쓸 수 있으면 null</summary>
        public Func<Run, string> CannotUseNow = _ => null;
        /// <summary>기술을 고르는 아이템(ItemTarget.PalSkill)에서 그 기술에 쓸 수 없으면 이유 (기본: PP가 가득이면 효과 없음)</summary>
        public Func<SkillInstance, string> CannotUseSkill = s => s.PP >= s.MaxPP ? ItemCatalog.NoEffect : null;
        public Func<ItemContext, ItemOutcome> Apply;

        /// <summary>상점 가격 배율 (가격 = ItemCatalog.ShopBaseCost(웨이브) × 배율)</summary>
        public float ShopPriceMultiplier;

        public string IconPath => "2.아이템/" + (Icon ?? Id);

        /// <summary>아이콘 그림과 잘라 쓸 영역 (RawImage용)</summary>
        public (Texture texture, Rect uv) LoadIcon()
        {
            if (Sphere.HasValue)
            {
                var sprite = PalSpheres.LoadSprite(Sphere.Value);
                if (sprite != null)
                {
                    var t = sprite.texture;
                    var r = sprite.textureRect;
                    return (t, new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height));
                }
            }
            return (Resources.Load<Texture2D>(IconPath), new Rect(0, 0, 1, 1));
        }
    }

    /// <summary>
    /// 팰로그 아이템 목록 + 보상 뽑기 + 상점 목록.
    /// 포켓로그(나무위키 PokéRogue/아이템)의 아이템 중 팰로그에서 쓸 수 있는 것만 골랐다.
    /// 등급 확률·가중치·상점 가격 배율·갱신 비용은 포켓로그와 같은 방식이다.
    /// </summary>
    public static class ItemCatalog
    {
        public const string NoEffect = "효과가 없을 것 같다.";

        // ───────── 상점 / 갱신 / 보상 개수 ─────────

        /// <summary>상점 기본 가격 (웨이브 1에서 180, 웨이브마다 5%씩 오른다). 상처약 = 0.2배 = 36</summary>
        public static int ShopBaseCost(int wave) => Mathf.RoundToInt(180f * (1f + (Mathf.Max(1, wave) - 1) * 0.05f));

        /// <summary>보상 갱신 비용 = 250 × ⌈웨이브/10⌉ × 2^(이번 보상에서 갱신한 횟수) (포켓로그와 같음)</summary>
        public static int RerollCost(int wave, int rerollCount) =>
            (int)Mathf.Min(int.MaxValue / 2, 250f * Mathf.CeilToInt(Mathf.Max(1, wave) / 10f) * Mathf.Pow(2, rerollCount));

        /// <summary>보상 개수: 처음 3개, 지역(20웨이브)을 클리어할 때마다 1개씩 늘어 최대 8개</summary>
        public static int RewardCount(int wave) =>
            Mathf.Clamp(3 + (Mathf.Max(1, wave) - 1) / GameConfig.AreaWaveStep, 3, MaxRewardCount);

        public const int MaxRewardCount = 8;

        /// <summary>황금 스피어까지 더한 보상 개수 (최대 8)</summary>
        public static int RewardCount(Run run) => Mathf.Min(MaxRewardCount, RewardCount(run.Wave) + run.ExtraRewards);

        /// <summary>상점에 한 번에 보이는 최대 개수 (넘치면 오래된 싼 아이템부터 빠진다)</summary>
        public const int MaxShopItems = 7;

        // 포켓로그 상점: 웨이브가 오를수록 묶음이 하나씩 열린다 (열린 묶음 수 = ⌈(웨이브+10)/30⌉)
        static readonly string[][] ShopGroups =
        {
            new[] { "potion", "ether", "revive" },
            new[] { "super_potion", "full_heal" },
            new[] { "elixir", "max_ether" },
            new[] { "hyper_potion", "max_revive" },
            new[] { "max_potion", "max_elixir" },
            new[] { "full_restore" },
            new[] { "sacred_ash" },
        };

        public static List<(ItemDef item, int price)> ShopItems(int wave)
        {
            int open = Mathf.Clamp(Mathf.CeilToInt((wave + 10) / 30f), 1, ShopGroups.Length);
            int baseCost = ShopBaseCost(wave);
            return ShopGroups.Take(open).SelectMany(g => g)
                .Select(id => Get(id)).Where(d => d != null)
                .Select(d => (d, Mathf.Max(1, Mathf.RoundToInt(baseCost * d.ShopPriceMultiplier))))
                .Reverse().Take(MaxShopItems).Reverse()
                .ToList();
        }

        // ───────── 보상 뽑기 ─────────

        /// <summary>보상 count개를 뽑는다. 칸마다 등급을 먼저 뽑고, 그 등급 안에서 가중치로 아이템을 뽑는다 (같은 아이템은 한 번만)</summary>
        public static List<ItemDef> RollRewards(Run run, int count, System.Random rng)
        {
            var picks = new List<ItemDef>();
            for (int n = 0; n < count; n++)
            {
                var tier = RollTier(rng);
                ItemDef pick = null;
                // 그 등급에 나올 아이템이 없으면 한 단계씩 낮춰 본다 (일반까지 없으면 높여 본다)
                for (int t = (int)tier; t >= 0 && pick == null; t--) pick = RollInTier(run, (ItemTier)t, picks, rng);
                for (int t = (int)tier + 1; t <= (int)ItemTier.Exotic && pick == null; t++) pick = RollInTier(run, (ItemTier)t, picks, rng);
                if (pick == null) break;
                picks.Add(pick);
            }
            return picks;
        }

        static ItemTier RollTier(System.Random rng)
        {
            int total = ItemTiers.RollWeights.Sum();
            int r = rng.Next(total);
            for (int i = 0; i < ItemTiers.RollWeights.Length; i++)
            {
                if (r < ItemTiers.RollWeights[i]) return (ItemTier)i;
                r -= ItemTiers.RollWeights[i];
            }
            return ItemTier.Common;
        }

        static ItemDef RollInTier(Run run, ItemTier tier, List<ItemDef> exclude, System.Random rng)
        {
            var pool = All.Where(d => d.Tier == tier && !exclude.Contains(d))
                          .Select(d => (d, w: Mathf.Max(0f, d.Weight(run)))).Where(x => x.w > 0f).ToList();
            float total = pool.Sum(x => x.w);
            if (total <= 0f) return null;
            double r = rng.NextDouble() * total;
            foreach (var (d, w) in pool)
            {
                if (r < w) return d;
                r -= w;
            }
            return pool[pool.Count - 1].d;
        }

        public static ItemDef Get(string id) => All.FirstOrDefault(d => d.Id == id);

        // ───────── 가중치 계산에 쓰는 파티 상태 ─────────
        static IEnumerable<Pal> Alive(Run r) => r.Party.Where(p => !p.IsFainted);
        static int HurtCount(Run r, float ratio) => Alive(r).Count(p => p.CurrentHP <= p.MaxHP * ratio);
        static int FaintedCount(Run r) => r.Party.Count(p => p.IsFainted);
        static int StatusCount(Run r) => Alive(r).Count(p => p.Status != StatusCondition.None);
        static int LowPpCount(Run r, float ratio) => r.Party.Count(p => p.Skills.Any(s => s.PP <= s.MaxPP * ratio));
        static float Up3(int n, float per) => Mathf.Min(n, 3) * per;   // 최대 3마리까지만 센다
        static bool PartyHasSkillType(Run r, PalType t) => r.Party.Any(p => p.Skills.Any(s => s.Data.type == t));

        // ───────── 효과에 쓰는 도우미 ─────────
        static string N(Pal p) => p.Data.palName;

        static ItemOutcome HealHp(Pal p, int flat, float ratio, bool cureStatus = false)
        {
            int amount = p.CurrentHP >= p.MaxHP ? 0 : Mathf.Max(flat, Mathf.RoundToInt(p.MaxHP * ratio));
            int healed = p.Heal(amount);
            bool cured = cureStatus && p.CureStatus();
            if (healed > 0 && cured) return ItemOutcome.Say($"{Josa.Eun(N(p))} HP를 {healed} 회복하고 상태이상이 나았다!");
            if (cured) return ItemOutcome.Say($"{N(p)}의 상태이상이 나았다!");
            return ItemOutcome.Say($"{Josa.Eun(N(p))} HP를 {healed} 회복했다!");
        }

        static string CannotHeal(Pal p) => p.IsFainted || p.CurrentHP >= p.MaxHP ? NoEffect : null;

        static ItemOutcome RestorePp(Pal p, int index, int amount)
        {
            var s = p.Skills[index];
            int before = s.PP;
            s.PP = amount < 0 ? s.MaxPP : Mathf.Min(s.MaxPP, s.PP + amount);
            return ItemOutcome.Say($"{s.Data.skillName}의 PP가 {s.PP - before} 회복되었다!");
        }

        static ItemOutcome RestoreAllPp(Pal p, int amount)
        {
            foreach (var s in p.Skills) s.PP = amount < 0 ? s.MaxPP : Mathf.Min(s.MaxPP, s.PP + amount);
            return ItemOutcome.Say($"{N(p)}의 기술 PP가 회복되었다!");
        }

        static ItemOutcome RaisePp(SkillInstance s, int ups)
        {
            int before = s.MaxPP;
            int add = Mathf.Min(ups, SkillInstance.MaxPpUps - s.PpUps);
            for (int i = 0; i < add; i++) s.BonusPP += Mathf.Max(1, s.Data.maxPP / 5);
            s.PpUps += add;
            s.PP += s.MaxPP - before;                                   // 늘어난 만큼 PP도 채운다
            return ItemOutcome.Say($"{s.Data.skillName}의 최대 PP가 {s.MaxPP - before} 늘었다!");
        }

        static string CannotRestoreAllPp(Pal p) => p.Skills.All(s => s.PP >= s.MaxPP) ? NoEffect : null;

        static ItemOutcome GiveSpheres(Run run, SphereType type, int count)
        {
            run.Spheres[(int)type] += count;
            return ItemOutcome.Say($"{PalSpheres.Name(type)}{(count > 1 ? $" {count}개" : "")}를 손에 넣었다!");
        }

        static ItemOutcome GiveMoney(Run run, float baseMultiplier)
        {
            int amount = Mathf.RoundToInt(ShopBaseCost(run.Wave) * baseMultiplier * run.MoneyMultiplier);
            run.Money += amount;
            return ItemOutcome.Say($"{amount:N0}G를 손에 넣었다!");
        }

        /// <summary>팰에게 지니게 하는 아이템 (효과 = BattleEffect, 이름은 아이템 이름)</summary>
        static ItemOutcome Hold(ItemContext c, string itemName, BattleEffect effect, string description)
        {
            effect.PerkName = itemName;
            effect.PerkDescription = description;
            effect.HeldItemId = itemName;
            c.Target.AddEffect(effect);
            return ItemOutcome.Say($"{N(c.Target)}에게 {Josa.Eul(itemName)} 지니게 했다!");
        }

        /// <summary>도둑 팰이 상대가 지닌 아이템 하나를 무작위로 빼앗는다 (미니 블랙홀은 못 빼앗음). 빼앗았으면 true</summary>
        public static bool StealHeldItem(Pal thief, Pal victim, BattleContext ctx, string via)
        {
            if (thief == null || victim == null || thief == victim || thief.IsFainted || victim.IsFainted) return false;
            var items = victim.Effects.Where(e => e.HeldItemId != null && e.HeldItemId != MiniBlackHoleName).ToList();
            if (items.Count == 0) return false;
            var item = items[UnityEngine.Random.Range(0, items.Count)];
            victim.RemoveEffect(item);
            thief.AddEffect(item);
            ctx.Log($"{thief.Data.palName}의 {Josa.I(via)} {victim.Data.palName}의 {Josa.Eul(item.HeldItemId)} 빼앗았다!");
            return true;
        }
        public const string MiniBlackHoleName = "미니 블랙홀";

        /// <summary>속성 강화 아이템(숯 등) id → 속성. 상대 팰에게 맞는 것만 쥐여 주기 위해</summary>
        public static readonly Dictionary<string, PalType> TypeBoosters = new();

        static ItemOutcome TempBoost(Run run, StatType stat, string statName)
        {
            foreach (var p in run.Party)
            {
                var existing = p.Effects.OfType<TempStatBoost>().FirstOrDefault(b => b.Stat == stat);
                if (existing != null) existing.WavesLeft = TempStatBoost.Duration;   // 이미 있으면 기간만 다시 채운다
                else p.AddEffect(new TempStatBoost(stat));
            }
            return ItemOutcome.Say($"{TempStatBoost.Duration}웨이브 동안 파티의 {Josa.I(statName)} 올라간다!");
        }

        // ═════════════════ 아이템 목록 ═════════════════
        public static readonly List<ItemDef> All = Build();

        static List<ItemDef> Build()
        {
            var list = new List<ItemDef>();
            void Add(ItemDef d) => list.Add(d);

            // ────────────── 일반 (포켓로그 일반) ──────────────
            Add(new ItemDef
            {
                Id = "pal_sphere_5", Name = "팰 스피어 5개", Tier = ItemTier.Common, Sphere = SphereType.Pal,
                Description = "팰 스피어를 5개 얻는다.",
                Weight = _ => 6, Apply = c => GiveSpheres(c.Run, SphereType.Pal, 5),
            });
            Add(new ItemDef
            {
                Id = "rare_candy", Name = "이상한사탕", Tier = ItemTier.Common, Target = ItemTarget.Pal,
                Description = "팰 1마리의 레벨이 1 오른다.",
                Weight = r => r.Party.Any(p => !p.IsMaxLevel) ? 2 : 0,
                CannotUse = p => p.IsMaxLevel ? NoEffect : null,
                Apply = c =>
                {
                    // 사탕단지 1개마다 레벨이 1 더 오른다
                    var ups = new List<(int, List<SkillData>)>();
                    for (int i = 0; i <= c.Run.CandyJars && !c.Target.IsMaxLevel; i++)
                        ups.Add((c.Target.Level + 1, c.Target.LevelUp()));
                    return new ItemOutcome { LevelUps = ups };
                },
            });
            Add(new ItemDef
            {
                Id = "potion", Name = "상처약", Tier = ItemTier.Common, Target = ItemTarget.Pal,
                Description = "팰 1마리의 HP를 20 또는 최대 HP의 10% 중 큰 만큼 회복한다.",
                Weight = r => Up3(HurtCount(r, 0.875f), 3), CannotUse = CannotHeal,
                Apply = c => HealHp(c.Target, 20, 0.1f), ShopPriceMultiplier = 0.2f,
            });
            Add(new ItemDef
            {
                Id = "super_potion", Name = "좋은상처약", Tier = ItemTier.Common, Target = ItemTarget.Pal,
                Description = "팰 1마리의 HP를 50 또는 최대 HP의 25% 중 큰 만큼 회복한다.",
                Weight = r => Up3(HurtCount(r, 0.75f), 1), CannotUse = CannotHeal,
                Apply = c => HealHp(c.Target, 50, 0.25f), ShopPriceMultiplier = 0.45f,
            });
            Add(new ItemDef
            {
                Id = "ether", Name = "PP에이드", Tier = ItemTier.Common, Target = ItemTarget.PalSkill,
                Description = "팰 1마리의 기술 1개의 PP를 10 회복한다.",
                Weight = r => Up3(LowPpCount(r, 0.5f), 3), CannotUse = CannotRestoreAllPp,
                Apply = c => RestorePp(c.Target, c.SkillIndex, 10), ShopPriceMultiplier = 0.4f,
            });
            Add(new ItemDef
            {
                Id = "max_ether", Name = "PP회복", Tier = ItemTier.Common, Target = ItemTarget.PalSkill,
                Description = "팰 1마리의 기술 1개의 PP를 모두 회복한다.",
                Weight = r => Up3(LowPpCount(r, 0.25f), 1), CannotUse = CannotRestoreAllPp,
                Apply = c => RestorePp(c.Target, c.SkillIndex, -1), ShopPriceMultiplier = 1f,
            });
            foreach (var (id, name, stat, statName) in new[]
                     {
                         ("x_attack", "플러스파워", StatType.Attack, "공격"),
                         ("x_defense", "디펜드업", StatType.Defense, "방어"),
                         ("x_speed", "스피드업", StatType.Speed, "스피드"),
                     })
            {
                Add(new ItemDef
                {
                    Id = id, Name = name, Tier = ItemTier.Common,
                    Description = $"{TempStatBoost.Duration}웨이브 동안 파티 전원의 {Josa.I(statName)} 1랭크(1.5배) 올라간다.",
                    Weight = _ => 4f / 3f, Apply = c => TempBoost(c.Run, stat, statName),
                });
            }
            Add(new ItemDef
            {
                Id = "sitrus_berry", Name = "자뭉열매", Tier = ItemTier.Common, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 HP가 절반 이하일 때 최대 HP의 1/4을 회복한다. (한 번 쓰면 사라짐)",
                Weight = _ => 2, Apply = c => Hold(c, "자뭉열매", new SitrusBerry(), "HP가 절반 이하가 되면 1/4 회복 (1회)"),
            });
            Add(new ItemDef
            {
                Id = "lum_berry", Name = "리샘열매", Tier = ItemTier.Common, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 상태이상에 걸렸을 때 턴이 끝나면 낫는다. (한 번 쓰면 사라짐)",
                Weight = _ => 2, Apply = c => Hold(c, "리샘열매", new LumBerry(), "상태이상을 1번 치료"),
            });

            // ────────────── 기가 (포켓로그 슈퍼) ──────────────
            Add(new ItemDef
            {
                Id = "giga_sphere_5", Name = "기가 스피어 5개", Tier = ItemTier.Giga, Sphere = SphereType.Giga,
                Description = "기가 스피어를 5개 얻는다. (포획률 1.5배)",
                Weight = _ => 6, Apply = c => GiveSpheres(c.Run, SphereType.Giga, 5),
            });
            Add(new ItemDef
            {
                Id = "full_heal", Name = "만병통치제", Tier = ItemTier.Giga, Target = ItemTarget.Pal,
                Description = "팰 1마리의 상태이상을 모두 치료한다.",
                Weight = r => Up3(StatusCount(r), 6),
                CannotUse = p => p.IsFainted || p.Status == StatusCondition.None ? NoEffect : null,
                Apply = c => { c.Target.CureStatus(); return ItemOutcome.Say($"{N(c.Target)}의 상태이상이 나았다!"); },
                ShopPriceMultiplier = 1f,
            });
            Add(new ItemDef
            {
                Id = "revive", Name = "기력의조각", Tier = ItemTier.Giga, Target = ItemTarget.Pal,
                Description = "기절한 팰 1마리를 최대 HP의 절반으로 되살린다.",
                Weight = r => Up3(FaintedCount(r), 9), CannotUse = p => p.IsFainted ? null : NoEffect,
                Apply = c => { c.Target.Revive(Mathf.Max(1, c.Target.MaxHP / 2)); return ItemOutcome.Say($"{Josa.Eun(N(c.Target))} 기운을 되찾았다!"); },
                ShopPriceMultiplier = 2f,
            });
            Add(new ItemDef
            {
                Id = "max_revive", Name = "기력의덩어리", Tier = ItemTier.Giga, Target = ItemTarget.Pal,
                Description = "기절한 팰 1마리를 HP를 모두 채워 되살린다.",
                Weight = r => Up3(FaintedCount(r), 3), CannotUse = p => p.IsFainted ? null : NoEffect,
                Apply = c => { c.Target.Revive(c.Target.MaxHP); return ItemOutcome.Say($"{Josa.Eun(N(c.Target))} 기운을 되찾았다!"); },
                ShopPriceMultiplier = 2.75f,
            });
            Add(new ItemDef
            {
                Id = "sacred_ash", Name = "성스러운분말", Tier = ItemTier.Giga,
                Description = "기절한 팰을 모두 HP를 가득 채워 되살린다.",
                Weight = r => FaintedCount(r) * 2 >= r.Party.Count ? 1 : 0,
                CannotUseNow = r => FaintedCount(r) == 0 ? NoEffect : null,
                Apply = c =>
                {
                    foreach (var p in c.Run.Party.Where(p => p.IsFainted)) p.Revive(p.MaxHP);
                    return ItemOutcome.Say("기절한 팰들이 모두 기운을 되찾았다!");
                },
                ShopPriceMultiplier = 10f,
            });
            Add(new ItemDef
            {
                Id = "hyper_potion", Name = "고급상처약", Tier = ItemTier.Giga, Target = ItemTarget.Pal,
                Description = "팰 1마리의 HP를 200 또는 최대 HP의 50% 중 큰 만큼 회복한다.",
                Weight = r => Up3(HurtCount(r, 0.625f), 3), CannotUse = CannotHeal,
                Apply = c => HealHp(c.Target, 200, 0.5f), ShopPriceMultiplier = 0.8f,
            });
            Add(new ItemDef
            {
                Id = "max_potion", Name = "풀회복약", Tier = ItemTier.Giga, Target = ItemTarget.Pal,
                Description = "팰 1마리의 HP를 모두 회복한다.",
                Weight = r => Up3(HurtCount(r, 0.5f), 1), CannotUse = CannotHeal,
                Apply = c => HealHp(c.Target, 0, 1f), ShopPriceMultiplier = 1.5f,
            });
            Add(new ItemDef
            {
                Id = "full_restore", Name = "회복약", Tier = ItemTier.Giga, Target = ItemTarget.Pal,
                Description = "팰 1마리의 HP와 상태이상을 모두 회복한다.",
                Weight = r => Up3(Alive(r).Count(p => p.CurrentHP <= p.MaxHP * 0.5f || p.Status != StatusCondition.None), 3),
                CannotUse = p => p.IsFainted || (p.CurrentHP >= p.MaxHP && p.Status == StatusCondition.None) ? NoEffect : null,
                Apply = c => HealHp(c.Target, 0, 1f, true), ShopPriceMultiplier = 2.25f,
            });
            Add(new ItemDef
            {
                Id = "elixir", Name = "PP에이더", Tier = ItemTier.Giga, Target = ItemTarget.Pal,
                Description = "팰 1마리의 모든 기술의 PP를 10씩 회복한다.",
                Weight = r => Up3(LowPpCount(r, 0.5f), 3), CannotUse = CannotRestoreAllPp,
                Apply = c => RestoreAllPp(c.Target, 10), ShopPriceMultiplier = 1f,
            });
            Add(new ItemDef
            {
                Id = "max_elixir", Name = "PP맥스", Tier = ItemTier.Giga, Target = ItemTarget.Pal,
                Description = "팰 1마리의 모든 기술의 PP를 모두 회복한다.",
                Weight = r => Up3(LowPpCount(r, 0.25f), 1), CannotUse = CannotRestoreAllPp,
                Apply = c => RestoreAllPp(c.Target, -1), ShopPriceMultiplier = 2.5f,
            });
            Add(new ItemDef
            {
                Id = "nugget", Name = "금구슬", Tier = ItemTier.Giga,
                Description = "팔면 돈이 되는 금구슬. 돈을 조금 얻는다.",
                Weight = _ => 5, Apply = c => GiveMoney(c.Run, 1f),
            });
            foreach (var (id, name, stat, statName) in new[]
                     {
                         ("protein", "타우린", StatType.Attack, "공격"),
                         ("iron", "사포닌", StatType.Defense, "방어"),
                         ("carbos", "알칼로이드", StatType.Speed, "스피드"),
                     })
            {
                Add(new ItemDef
                {
                    Id = id, Name = name, Tier = ItemTier.Giga, Target = ItemTarget.Pal,
                    Description = $"팰 1마리의 {Josa.I(statName)} 영구히 10% 오른다.",
                    Weight = _ => 0.75f,
                    Apply = c =>
                    {
                        c.Target.AddEffect(new StatPerk(stat, 1.1f) { PerkName = name, PerkDescription = $"{statName} +10%" });
                        return ItemOutcome.Say($"{N(c.Target)}의 {Josa.I(statName)} 올랐다!");
                    },
                });
            }
            Add(new ItemDef
            {
                Id = "hp_up", Name = "맥스업", Tier = ItemTier.Giga, Target = ItemTarget.Pal,
                Description = "팰 1마리의 최대 HP가 영구히 10% 오른다.",
                Weight = _ => 0.75f,
                Apply = c =>
                {
                    c.Target.AddEffect(new MaxHpPerk(1.1f) { PerkName = "맥스업", PerkDescription = "최대 HP +10%" });
                    return ItemOutcome.Say($"{N(c.Target)}의 최대 HP가 올랐다!");
                },
            });

            // ────────────── 하이퍼 (포켓로그 하이퍼) ──────────────
            Add(new ItemDef
            {
                Id = "hyper_sphere_5", Name = "하이퍼 스피어 5개", Tier = ItemTier.Hyper, Sphere = SphereType.Hyper,
                Description = "하이퍼 스피어를 5개 얻는다. (포획률 2배)",
                Weight = _ => 15, Apply = c => GiveSpheres(c.Run, SphereType.Hyper, 5),
            });
            Add(new ItemDef
            {
                Id = "big_nugget", Name = "큰금구슬", Tier = ItemTier.Hyper,
                Description = "커다란 금구슬. 금구슬의 2.5배만큼 돈을 얻는다.",
                Weight = _ => 12, Apply = c => GiveMoney(c.Run, 2.5f),
            });
            Add(new ItemDef
            {
                Id = "amulet_coin", Name = "부적금화", Tier = ItemTier.Hyper,
                Description = $"앞으로 받는 돈이 20% 늘어난다. (최대 {Run.MaxAmuletCoins}개)",
                Weight = r => r.AmuletCoins < Run.MaxAmuletCoins ? 3 : 0,
                Apply = c => { c.Run.AmuletCoins++; return ItemOutcome.Say("앞으로 받는 돈이 20% 늘어난다!"); },
            });
            Add(new ItemDef
            {
                Id = "reviver_seed", Name = "부활의씨앗", Tier = ItemTier.Hyper, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 쓰러질 때 최대 HP의 절반으로 되살아난다. (한 번 쓰면 사라짐)",
                Weight = _ => 4,
                CannotUse = p => p.Effects.OfType<ReviverSeed>().Any() ? "이미 지니고 있다." : null,
                Apply = c => Hold(c, "부활의씨앗", new ReviverSeed(), "쓰러지면 HP 절반으로 부활 (1회)"),
            });
            foreach (var (id, name, type) in new[]
                     {
                         ("silk_scarf", "실크스카프", PalType.Neutral),
                         ("charcoal", "목탄", PalType.Fire),
                         ("mystic_water", "신비의물방울", PalType.Water),
                         ("miracle_seed", "기적의씨", PalType.Grass),
                         ("magnet", "자석", PalType.Electric),
                         ("soft_sand", "부드러운모래", PalType.Ground),
                         ("never_melt_ice", "녹지않는얼음", PalType.Ice),
                         ("dragon_fang", "용의이빨", PalType.Dragon),
                         ("black_glasses", "검은안경", PalType.Dark),
                     })
            {
                string typeName = PalTypeNames.Korean(type);
                TypeBoosters[id] = type;
                Add(new ItemDef
                {
                    Id = id, Name = name, Tier = ItemTier.Hyper, Target = ItemTarget.Pal, IsHeld = true,
                    Description = $"지니게 하면 {typeName}속성 기술의 위력이 20% 올라간다.",
                    Weight = r => PartyHasSkillType(r, type) ? 0.6f : 0f,
                    Apply = c => Hold(c, name, new TypePowerPerk(type, 1.2f), $"{typeName}속성 기술 위력 +20%"),
                });
            }
            Add(new ItemDef
            {
                Id = "lucky_egg", Name = "행복의알", Tier = ItemTier.Hyper, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 그 팰이 받는 경험치가 50% 늘어난다.",
                Weight = _ => 4, Apply = c => Hold(c, "행복의알", new ExpBoostItem(1.5f), "받는 경험치 +50%"),
            });
            Add(new ItemDef
            {
                Id = "scope_lens", Name = "초점렌즈", Tier = ItemTier.Hyper, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 공격이 급소에 맞을 확률이 10% 올라간다.",
                Weight = _ => 4, Apply = c => Hold(c, "초점렌즈", new CritPerk(0.1f), "급소 확률 +10%"),
            });
            Add(new ItemDef
            {
                Id = "exp_charm", Name = "경험부적", Tier = ItemTier.Hyper,
                Description = "파티가 받는 경험치가 25% 늘어난다.",
                Weight = _ => 4, Apply = c => { c.Run.ExpBonus += 0.25f; return ItemOutcome.Say("파티가 받는 경험치가 25% 늘어난다!"); },
            });
            Add(new ItemDef
            {
                Id = "exp_share", Name = "학습장치", Tier = ItemTier.Hyper,
                Description = $"싸움에 나가지 않은 팰도 경험치의 20%를 받는다. (최대 {Run.MaxExpShare}개, 개수만큼 늘어남)",
                Weight = r => r.ExpShareCount < Run.MaxExpShare ? 3 : 0,
                Apply = c =>
                {
                    c.Run.ExpShareCount = Mathf.Min(Run.MaxExpShare, c.Run.ExpShareCount + 1);
                    return ItemOutcome.Say($"싸우지 않은 팰도 경험치의 {c.Run.ExpShareCount * 20}%를 받는다!");
                },
            });

            // ────────────── 얼티밋 (포켓로그 로그) ──────────────
            Add(new ItemDef
            {
                Id = "ultimate_sphere_5", Name = "얼티밋 스피어 5개", Tier = ItemTier.Ultimate, Sphere = SphereType.Ultimate,
                Description = "얼티밋 스피어를 5개 얻는다. (포획률 3배)",
                Weight = _ => 16, Apply = c => GiveSpheres(c.Run, SphereType.Ultimate, 5),
            });
            Add(new ItemDef
            {
                Id = "leftovers", Name = "먹다남은음식", Tier = ItemTier.Ultimate, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 매 턴이 끝날 때 최대 HP의 1/16을 회복한다.",
                Weight = _ => 4, Apply = c => Hold(c, "먹다남은음식", new RegenPerk(16), "매 턴 HP 1/16 회복"),
            });
            Add(new ItemDef
            {
                Id = "shell_bell", Name = "조개껍질방울", Tier = ItemTier.Ultimate, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 준 데미지의 1/8만큼 HP를 회복한다.",
                Weight = _ => 4, Apply = c => Hold(c, "조개껍질방울", new DrainPerk(8), "준 데미지의 1/8 흡수"),
            });
            Add(new ItemDef
            {
                Id = "focus_band", Name = "기합의머리띠", Tier = ItemTier.Ultimate, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 쓰러질 공격을 받아도 10% 확률로 HP 1로 버틴다.",
                Weight = _ => 4, Apply = c => Hold(c, "기합의머리띠", new FocusBand(0.1f), "10% 확률로 HP 1로 버팀"),
            });
            Add(new ItemDef
            {
                Id = "super_exp_charm", Name = "슈퍼경험부적", Tier = ItemTier.Ultimate,
                Description = "파티가 받는 경험치가 60% 늘어난다.",
                Weight = _ => 3, Apply = c => { c.Run.ExpBonus += 0.6f; return ItemOutcome.Say("파티가 받는 경험치가 60% 늘어난다!"); },
            });

            // ────────────── 엑조틱 (포켓로그 마스터) ──────────────
            Add(new ItemDef
            {
                Id = "exotic_sphere", Name = "엑조틱 스피어", Tier = ItemTier.Exotic, Sphere = SphereType.Exotic,
                Description = "엑조틱 스피어를 1개 얻는다. (반드시 잡힌다)",
                Weight = _ => 24, Apply = c => GiveSpheres(c.Run, SphereType.Exotic, 1),
            });
            Add(new ItemDef
            {
                Id = "golden_exp_charm", Name = "황금경험부적", Tier = ItemTier.Exotic,
                Description = "파티가 받는 경험치가 100% 늘어난다.",
                Weight = _ => 2, Apply = c => { c.Run.ExpBonus += 1f; return ItemOutcome.Say("파티가 받는 경험치가 100% 늘어난다!"); },
            });

            // ═════════════════ 추가 아이템 (2차) ═════════════════
            // 포켓로그 아이템 중 팰로그에서 쓸 수 있는 것 + 팰월드 아이템(그림은 팰로그에서 못 쓰는 포켓로그 아이템 그림을 빌려 씀)

            // ────────────── 일반 ──────────────
            Add(new ItemDef
            {
                Id = "leppa_berry", Name = "과사열매", Tier = ItemTier.Common, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 기술의 PP가 0이 됐을 때 PP를 10 회복한다. (한 번 쓰면 사라짐)",
                Weight = _ => 2, Apply = c => Hold(c, "과사열매", new LeppaBerry(), "PP가 0이 되면 10 회복 (1회)"),
            });
            Add(new ItemDef
            {
                Id = "enigma_berry", Name = "의문열매", Tier = ItemTier.Common, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 효과가 굉장한 공격을 맞았을 때 최대 HP의 1/4을 회복한다. (한 번 쓰면 사라짐)",
                Weight = _ => 2, Apply = c => Hold(c, "의문열매", new EnigmaBerry(), "약점을 맞으면 HP 1/4 회복 (1회)"),
            });
            foreach (var (id, name, stat) in new[]
                     {
                         ("liechi_berry", "치리열매", StatType.Attack),
                         ("ganlon_berry", "용아열매", StatType.Defense),
                         ("salac_berry", "캄라열매", StatType.Speed),
                     })
            {
                string statName = StatNames.Korean(stat);
                Add(new ItemDef
                {
                    Id = id, Name = name, Tier = ItemTier.Common, Target = ItemTarget.Pal, IsHeld = true,
                    Description = $"지니게 하면 HP가 1/4 이하가 됐을 때 {Josa.I(statName)} 1랭크 올라간다. (한 번 쓰면 사라짐)",
                    Weight = _ => 2f / 3f,
                    Apply = c => Hold(c, name, new PinchBerry(name, stat), $"위기 때 {statName} +1랭크 (1회)"),
                });
            }
            AddSoul("dawn_stone", "작은 팰 영혼", ItemTier.Common, 0.03f);
            AddManual("scroll_of_waters", "수련의 서 S", ItemTier.Common, 0.5f);
            AddImplant("medichamite", "용감", ItemTier.Common, "공격 +10%", 0.7f, atk: 1.1f);
            AddImplant("aggronite", "단단한 피부", ItemTier.Common, "방어 +10%", 0.7f, def: 1.1f);
            AddImplant("manectite", "재빠름", ItemTier.Common, "스피드 +10%", 0.7f, spd: 1.1f);

            // ────────────── 기가 ──────────────
            Add(new ItemDef
            {
                Id = "dire_hit", Name = "크리티컬커터", Tier = ItemTier.Giga,
                Description = $"{TempCritBoost.Duration}웨이브 동안 파티 전원의 공격이 급소에 맞을 확률이 15% 올라간다.",
                Weight = _ => 4,
                Apply = c =>
                {
                    foreach (var p in c.Run.Party)
                    {
                        var existing = p.Effects.OfType<TempCritBoost>().FirstOrDefault();
                        if (existing != null) existing.WavesLeft = TempCritBoost.Duration;
                        else p.AddEffect(new TempCritBoost());
                    }
                    return ItemOutcome.Say($"{TempCritBoost.Duration}웨이브 동안 파티의 급소율이 올라간다!");
                },
            });
            Add(new ItemDef
            {
                Id = "pp_up", Name = "포인트업", Tier = ItemTier.Giga, Target = ItemTarget.PalSkill,
                Description = $"팰 1마리의 기술 1개의 최대 PP가 원래의 1/5만큼 늘어난다. (기술당 {SkillInstance.MaxPpUps}번까지)",
                Weight = _ => 2,
                CannotUse = p => p.Skills.All(s => s.PpUps >= SkillInstance.MaxPpUps) ? NoEffect : null,
                CannotUseSkill = s => s.PpUps >= SkillInstance.MaxPpUps ? "더 이상 늘릴 수 없다." : null,
                Apply = c => RaisePp(c.Target.Skills[c.SkillIndex], 1),
            });
            AddSoul("dusk_stone", "중간 팰 영혼", ItemTier.Giga, 0.05f);
            AddManual("scroll_of_waters", "수련의 서 M", ItemTier.Giga, 1f);
            AddImplant("gyaradosite", "사나움", ItemTier.Giga, "공격 +20%", 1f, atk: 1.2f);
            foreach (var (gem, type) in new[]
                     {
                         ("normal_gem", PalType.Neutral), ("fire_gem", PalType.Fire), ("water_gem", PalType.Water),
                         ("grass_gem", PalType.Grass), ("electric_gem", PalType.Electric), ("ground_gem", PalType.Ground),
                         ("ice_gem", PalType.Ice), ("dragon_gem", PalType.Dragon), ("dark_gem", PalType.Dark),
                     })
            {
                string typeName = PalTypeNames.Korean(type);
                string name = $"응원 호루라기: {typeName}";
                Add(new ItemDef
                {
                    Id = "whistle_" + gem, Icon = gem, Name = name, Tier = ItemTier.Giga,
                    Description = $"{TempTypeBoost.Duration}웨이브 동안 파티 전원의 {typeName}속성 기술 위력이 30% 올라간다.",
                    Weight = r => PartyHasSkillType(r, type) ? 0.4f : 0f,
                    Apply = c =>
                    {
                        foreach (var p in c.Run.Party)
                        {
                            var existing = p.Effects.OfType<TempTypeBoost>().FirstOrDefault(b => b.Type == type);
                            if (existing != null) existing.WavesLeft = TempTypeBoost.Duration;
                            else p.AddEffect(new TempTypeBoost(type));
                        }
                        return ItemOutcome.Say($"{TempTypeBoost.Duration}웨이브 동안 파티의 {typeName}속성 기술이 강해진다!");
                    },
                });
            }

            // ────────────── 하이퍼 ──────────────
            Add(new ItemDef
            {
                Id = "candy_jar", Name = "사탕단지", Tier = ItemTier.Hyper,
                Description = $"이상한사탕을 쓸 때마다 레벨이 1 더 오른다. (최대 {Run.MaxCandyJars}개)",
                Weight = r => r.CandyJars < Run.MaxCandyJars ? 3 : 0,
                Apply = c =>
                {
                    c.Run.CandyJars++;
                    return ItemOutcome.Say($"이제 이상한사탕을 쓰면 레벨이 {1 + c.Run.CandyJars} 오른다!");
                },
            });
            Add(new ItemDef
            {
                Id = "pp_max", Name = "포인트맥스", Tier = ItemTier.Hyper, Target = ItemTarget.PalSkill,
                Description = "팰 1마리의 기술 1개의 최대 PP를 한계까지(원래의 3/5만큼) 늘린다.",
                Weight = _ => 2,
                CannotUse = p => p.Skills.All(s => s.PpUps >= SkillInstance.MaxPpUps) ? NoEffect : null,
                CannotUseSkill = s => s.PpUps >= SkillInstance.MaxPpUps ? "더 이상 늘릴 수 없다." : null,
                Apply = c => RaisePp(c.Target.Skills[c.SkillIndex], SkillInstance.MaxPpUps),
            });
            Add(new ItemDef
            {
                Id = "revival_drug", Icon = "tera_orb", Name = "부활약물", Tier = ItemTier.Hyper,
                Description = "(팰월드) 기절한 팰을 모두 최대 HP의 절반으로 되살린다.",
                Weight = r => FaintedCount(r) > 0 ? 3 : 0,
                CannotUseNow = r => FaintedCount(r) == 0 ? NoEffect : null,
                Apply = c =>
                {
                    foreach (var p in c.Run.Party.Where(p => p.IsFainted)) p.Revive(Mathf.Max(1, p.MaxHP / 2));
                    return ItemOutcome.Say("기절한 팰들이 모두 다시 일어섰다!");
                },
            });
            AddSoul("shiny_stone", "대형 팰 영혼", ItemTier.Hyper, 0.08f);
            AddManual("scroll_of_darkness", "수련의 서 L", ItemTier.Hyper, 2f);
            AddLotus("mint_spdef", "라이프 로터스", "최대 HP", 0.8f, hp: 1.15f);
            AddLotus("mint_atk", "파워 로터스", "공격", 0.8f, atk: 1.15f);
            AddLotus("mint_spd", "스피드 로터스", "스피드", 0.8f, spd: 1.15f);
            AddImplant("garchompite", "귀신", ItemTier.Hyper, "공격 +30%, 방어 +5%", 0.8f, atk: 1.3f, def: 1.05f);
            AddImplant("metagrossite", "다이아몬드 보디", ItemTier.Hyper, "방어 +30%", 0.8f, def: 1.3f);
            AddImplant("salamencite", "신속", ItemTier.Hyper, "스피드 +30%", 0.8f, spd: 1.3f);
            AddImplant("gengarite", "흡혈귀", ItemTier.Hyper, "준 데미지의 1/10 흡수", 0.8f, drain: 10);
            AddPendant("venusaurite", "생명의 펜던트", "최대 HP", hp: 1.2f);
            AddPendant("charizardite_x", "공격의 펜던트", "공격", atk: 1.2f);
            AddPendant("blastoisinite", "방어의 펜던트", "방어", def: 1.2f);

            // ────────────── 얼티밋 ──────────────
            AddSoul("sun_stone", "극한 팰 영혼", ItemTier.Ultimate, 0.12f);
            AddManual("scroll_of_darkness", "수련의 서 XL", ItemTier.Ultimate, 4f);
            AddFruit("grass_tera_shard", "생명의 열매", "최대 HP", hp: 1.25f);
            AddFruit("fire_tera_shard", "괴력의 열매", "공격", atk: 1.25f);
            AddFruit("rock_tera_shard", "견고의 열매", "방어", def: 1.25f);
            AddImplant("mewtwonite_x", "전설", ItemTier.Ultimate, "공격 +20%, 방어 +20%, 스피드 +15%", 2f, atk: 1.2f, def: 1.2f, spd: 1.15f);

            // ────────────── 엑조틱 ──────────────
            Add(new ItemDef
            {
                Id = "pb_gold", Name = "황금 스피어", Tier = ItemTier.Exotic,
                Description = "보상으로 고를 수 있는 아이템이 1개 늘어난다. (최대 8개까지)",
                Weight = r => ItemCatalog.RewardCount(r) < MaxRewardCount ? 6 : 0,
                Apply = c =>
                {
                    c.Run.ExtraRewards++;
                    return ItemOutcome.Say("앞으로 보상을 하나 더 고를 수 있게 되었다!");
                },
            });

            // ═════════════════ 추가 아이템 (3차) ═════════════════
            // ────────────── 얼티밋 ──────────────
            Add(new ItemDef
            {
                Id = "relic_gold", Name = "고대의 금화", Tier = ItemTier.Ultimate,
                Description = "아주 귀한 금화. 많은 돈을 받는다. (금구슬의 10배)",
                Weight = _ => 3, Apply = c => GiveMoney(c.Run, 10f),
            });
            Add(new ItemDef
            {
                Id = "grip_claw", Name = "끈기갈고리손톱", Tier = ItemTier.Ultimate, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 공격을 맞힐 때마다 10% 확률로 상대가 지닌 아이템을 하나 빼앗는다.",
                Weight = _ => 3, Apply = c => Hold(c, "끈기갈고리손톱", new GripClaw(0.1f), "맞힐 때 10% 확률로 상대 아이템 빼앗기"),
            });
            Add(new ItemDef
            {
                Id = "berry_pouch", Name = "열매 주머니", Tier = ItemTier.Ultimate,
                Description = $"내 팰이 지닌 열매를 먹어도 30% 확률로 남는다. 겹치면 확률이 오른다. (최대 {Run.MaxBerryPouches}개)",
                Weight = r => r.BerryPouches < Run.MaxBerryPouches ? 3 : 0,
                CannotUseNow = r => r.BerryPouches >= Run.MaxBerryPouches ? "열매 주머니는 더 가질 수 없다." : null,
                Apply = c =>
                {
                    c.Run.BerryPouches++;
                    return ItemOutcome.Say($"열매를 먹어도 {Mathf.RoundToInt(c.Run.BerrySave * 100)}% 확률로 남게 되었다!");
                },
            });

            // ────────────── 엑조틱 ──────────────
            Add(new ItemDef
            {
                Id = "mini_black_hole", Name = MiniBlackHoleName, Tier = ItemTier.Exotic, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 매 턴이 끝날 때마다 상대가 지닌 아이템을 하나 빨아들여 빼앗는다.",
                Weight = _ => 1, Apply = c => Hold(c, MiniBlackHoleName, new MiniBlackHole(), "매 턴 상대 아이템 1개 빼앗기"),
            });
            Add(new ItemDef
            {
                Id = "multi_lens", Name = "멀티렌즈", Tier = ItemTier.Exotic, Target = ItemTarget.Pal, IsHeld = true,
                Description = "지니게 하면 공격할 때마다 그 데미지의 1/4만큼 한 번 더 때린다.",
                Weight = _ => 2, Apply = c => Hold(c, "멀티렌즈", new MultiLens(), "공격 후 1/4 위력으로 한 번 더"),
            });

            // ───── 위 아이템들을 만드는 도우미 ─────
            // 팰 영혼(팰월드): 팰 1마리 모든 능력치(HP·공격·방어·스피드)가 영구히 오른다
            void AddSoul(string icon, string name, ItemTier tier, float bonus)
            {
                Add(new ItemDef
                {
                    Id = "soul_" + icon, Icon = icon, Name = name, Tier = tier, Target = ItemTarget.Pal,
                    Description = $"(팰월드) 팰 1마리의 모든 능력치가 영구히 {Mathf.RoundToInt(bonus * 100)}% 오른다.",
                    Weight = _ => 2,
                    Apply = c =>
                    {
                        c.Target.AddEffect(new StatBundle(1f + bonus, 1f + bonus, 1f + bonus, 1f + bonus) { PerkName = name, PerkDescription = $"모든 능력치 +{Mathf.RoundToInt(bonus * 100)}%" });
                        return ItemOutcome.Say($"{N(c.Target)}의 모든 능력치가 올랐다!");
                    },
                });
            }

            // 수련의 서(팰월드): 팰 1마리가 경험치를 얻는다 (지금 레벨에서 다음 레벨까지 필요한 양 × levels)
            void AddManual(string icon, string name, ItemTier tier, float levels)
            {
                Add(new ItemDef
                {
                    Id = "manual_" + name.Replace(" ", "_"), Icon = icon, Name = name, Tier = tier, Target = ItemTarget.Pal,
                    Description = levels < 1f ? "(팰월드) 팰 1마리가 다음 레벨까지 필요한 경험치의 절반을 얻는다."
                                              : $"(팰월드) 팰 1마리가 레벨 {levels:0}개만큼의 경험치를 얻는다.",
                    Weight = r => r.Party.Any(p => !p.IsMaxLevel) ? 2 : 0,
                    CannotUse = p => p.IsMaxLevel ? NoEffect : null,
                    Apply = c =>
                    {
                        int amount = Mathf.Max(1, Mathf.RoundToInt(c.Target.ExpToNext * levels));
                        var ups = c.Target.GainExp(amount);
                        return new ItemOutcome { Message = $"{Josa.Eun(N(c.Target))} 경험치를 {amount} 얻었다!", LevelUps = ups };
                    },
                });
            }

            // 패시브 스킬 임플란트(팰월드): 팰 1마리에게 패시브 스킬을 영구히 심는다 (같은 것은 한 번만)
            void AddImplant(string icon, string skill, ItemTier tier, string effectText, float weight,
                            float atk = 1f, float def = 1f, float spd = 1f, int drain = 0)
            {
                string name = $"임플란트: {skill}";
                Add(new ItemDef
                {
                    Id = "implant_" + icon, Icon = icon, Name = name, Tier = tier, Target = ItemTarget.Pal,
                    Description = $"(팰월드) 팰 1마리에게 패시브 스킬 {Josa.Eul($"[{skill}]")} 심는다. {effectText} (영구)",
                    Weight = _ => weight,
                    CannotUse = p => p.Effects.Any(e => e.PerkName == skill) ? "이미 그 패시브 스킬을 가지고 있다." : null,
                    Apply = c =>
                    {
                        BattleEffect effect = drain > 0 ? new DrainPerk(drain) : new StatBundle(1f, atk, def, spd);
                        effect.PerkName = skill;
                        effect.PerkDescription = effectText;
                        c.Target.AddEffect(effect);
                        return ItemOutcome.Say($"{Josa.Eun(N(c.Target))} 패시브 스킬 {Josa.Eul($"[{skill}]")} 얻었다!");
                    },
                });
            }

            // 로터스(팰월드): 팰 1마리의 능력치 하나가 영구히 15% 오른다
            void AddLotus(string icon, string name, string statName, float weight, float hp = 1f, float atk = 1f, float spd = 1f)
            {
                Add(new ItemDef
                {
                    Id = "lotus_" + icon, Icon = icon, Name = name, Tier = ItemTier.Hyper, Target = ItemTarget.Pal,
                    Description = $"(팰월드) 팰 1마리의 {Josa.I(statName)} 영구히 15% 오른다.",
                    Weight = _ => weight,
                    Apply = c =>
                    {
                        c.Target.AddEffect(new StatBundle(hp, atk, 1f, spd) { PerkName = name, PerkDescription = $"{statName} +15%" });
                        return ItemOutcome.Say($"{N(c.Target)}의 {Josa.I(statName)} 올랐다!");
                    },
                });
            }

            // 펜던트(팰월드): 지니게 하면 능력치 하나 +20%
            void AddPendant(string icon, string name, string statName, float hp = 1f, float atk = 1f, float def = 1f)
            {
                Add(new ItemDef
                {
                    Id = "pendant_" + icon, Icon = icon, Name = name, Tier = ItemTier.Hyper, Target = ItemTarget.Pal, IsHeld = true,
                    Description = $"(팰월드) 지니게 하면 {Josa.I(statName)} 20% 오른다.",
                    Weight = _ => 0.7f,
                    CannotUse = p => p.Effects.Any(e => e.PerkName == name) ? "이미 지니고 있다." : null,
                    Apply = c => Hold(c, name, new StatBundle(hp, atk, def, 1f), $"{statName} +20%"),
                });
            }

            // 능력 향상 열매(팰월드): 팰 1마리의 능력치 하나가 영구히 25% 오른다
            void AddFruit(string icon, string name, string statName, float hp = 1f, float atk = 1f, float def = 1f)
            {
                Add(new ItemDef
                {
                    Id = "fruit_" + icon, Icon = icon, Name = name, Tier = ItemTier.Ultimate, Target = ItemTarget.Pal,
                    Description = $"(팰월드) 팰 1마리의 {Josa.I(statName)} 영구히 25% 오른다.",
                    Weight = _ => 1,
                    Apply = c =>
                    {
                        c.Target.AddEffect(new StatBundle(hp, atk, def, 1f) { PerkName = name, PerkDescription = $"{statName} +25%" });
                        return ItemOutcome.Say($"{N(c.Target)}의 {Josa.I(statName)} 크게 올랐다!");
                    },
                });
            }

            return list;
        }

        /// <summary>프로젝트로 복사해야 하는 아이템 이미지 이름 (스피어는 팰 스피어 그림을 쓰므로 제외)</summary>
        public static IEnumerable<string> IconIds =>
            All.Where(d => !d.Sphere.HasValue).Select(d => d.Icon ?? d.Id).Concat(TmIcons.Values).Distinct();

        // ═════════════════ 기술머신 (팰월드 기술 열매) ═════════════════
        // 이름은 "기술머신: 기술이름", 그림은 포켓몬 기술머신(속성별 색).
        // 등급은 op.gg 팰월드 기술 열매 레어도를 옮겼다: 비범 → 일반, 희귀 → 기가, 영웅 → 하이퍼.
        // 팰월드처럼 어떤 팰이든 배울 수 있다.

        static readonly Dictionary<PalType, string> TmIcons = new Dictionary<PalType, string>
        {
            { PalType.Neutral, "tm_normal" }, { PalType.Fire, "tm_fire" }, { PalType.Water, "tm_water" },
            { PalType.Grass, "tm_grass" }, { PalType.Electric, "tm_electric" }, { PalType.Ground, "tm_ground" },
            { PalType.Ice, "tm_ice" }, { PalType.Dragon, "tm_dragon" }, { PalType.Dark, "tm_dark" },
            { PalType.Void, "tm_ghost" },
        };

        /// <summary>기술 이름 → 등급 (op.gg 기술 열매 레어도)</summary>
        static readonly Dictionary<string, ItemTier> TmTiers = new Dictionary<string, ItemTier>
        {
            // 무
            { "공기 대포", ItemTier.Common }, { "파워 샷", ItemTier.Common }, { "파워 폭탄", ItemTier.Giga },
            { "팰 블래스트", ItemTier.Hyper }, { "신성 폭발", ItemTier.Hyper },
            // 화염
            { "파이어 샷", ItemTier.Common }, { "파이어 브레스", ItemTier.Giga },
            { "플레임 판넬", ItemTier.Hyper }, { "화염구", ItemTier.Hyper },
            // 물
            { "워터 제트", ItemTier.Common }, { "버블 샷", ItemTier.Giga }, { "라인 스플래시", ItemTier.Giga },
            { "하이드로 스트림", ItemTier.Hyper }, { "물기둥 분출", ItemTier.Hyper },
            // 풀
            { "바람의 칼날", ItemTier.Common }, { "씨앗 기관총", ItemTier.Common }, { "멀티 커터", ItemTier.Giga },
            { "리플렉트 리프", ItemTier.Hyper }, { "윈드 블래스트", ItemTier.Hyper },
            // 번개
            { "번개 창", ItemTier.Common }, { "전기 파장", ItemTier.Common }, { "라인 썬더", ItemTier.Giga },
            { "번개 일격", ItemTier.Hyper }, { "번개 폭풍", ItemTier.Hyper },
            // 땅
            { "머드 슛", ItemTier.Common }, { "바위 폭발", ItemTier.Common }, { "바위 대포", ItemTier.Common },
            { "바위 창", ItemTier.Hyper }, { "스톤 비트", ItemTier.Hyper },
            // 얼음
            { "얼음 미사일", ItemTier.Common }, { "얼음 칼날", ItemTier.Common }, { "서리 낀 입김", ItemTier.Giga },
            { "아이시클 라인", ItemTier.Hyper }, { "다이아몬드 폴", ItemTier.Hyper },
            // 용
            { "용 대포", ItemTier.Common }, { "용의 파장", ItemTier.Common }, { "용의 숨결", ItemTier.Giga },
            { "빔 슬라이서", ItemTier.Hyper }, { "용의 운석", ItemTier.Hyper },
            // 어둠
            { "다크 샷", ItemTier.Common }, { "그림자 폭발", ItemTier.Common }, { "어둠 화살", ItemTier.Giga },
            { "어둠의 레이저", ItemTier.Hyper }, { "다크 위스프", ItemTier.Hyper },
        };

        /// <summary>등급별 기술머신 전체 가중치 (포켓로그 기술머신: 일반 2 / 슈퍼 3 / 하이퍼 11). 같은 등급 기술머신끼리 나눠 가진다</summary>
        static readonly Dictionary<ItemTier, float> TmTierWeights = new Dictionary<ItemTier, float>
        {
            { ItemTier.Common, 2f }, { ItemTier.Giga, 3f }, { ItemTier.Hyper, 11f },
        };

        const string TmPrefix = "tm_skill_";

        /// <summary>
        /// 게임에 있는 기술(SkillData) 목록으로 기술머신 아이템을 만든다. 기술 에셋은 Resources가 아니라서
        /// 화면(BattleScreen)이 팰들의 학습 목록에서 모은 기술을 넘겨준다. 다시 부르면 새로 만든다.
        /// </summary>
        public static void SetSkillPool(IEnumerable<SkillData> skills)
        {
            All.RemoveAll(d => d.Id.StartsWith(TmPrefix));
            var tms = skills.Where(s => s != null && TmTiers.ContainsKey(s.skillName))
                            .GroupBy(s => s.skillName).Select(g => g.First()).ToList();
            foreach (var skill in tms)
            {
                var tier = TmTiers[skill.skillName];
                int sameTier = tms.Count(s => TmTiers[s.skillName] == tier);
                float weight = TmTierWeights[tier] / Mathf.Max(1, sameTier);
                string typeName = PalTypeNames.Korean(skill.type);
                All.Add(new ItemDef
                {
                    Id = TmPrefix + skill.name,
                    Icon = TmIcons.TryGetValue(skill.type, out var icon) ? icon : "tm_normal",
                    Name = $"기술머신: {skill.skillName}",
                    Tier = tier,
                    Target = ItemTarget.Pal,
                    Description = $"팰 1마리에게 {Josa.Eul(skill.skillName)} 가르친다.\n" +
                                  $"{typeName}속성 · 위력 {(skill.power > 0 ? skill.power.ToString() : "-")} · 명중 {skill.accuracy} · PP {skill.maxPP}",
                    // 아직 이 기술을 모르는 팰이 있을 때만 나온다
                    Weight = r => r.Party.Any(p => p.Skills.All(s => s.Data != skill)) ? weight : 0f,
                    CannotUse = p => p.Skills.Any(s => s.Data == skill) ? "이미 그 기술을 알고 있다." : null,
                    Apply = c => new ItemOutcome { TeachSkill = skill },
                });
            }
        }
    }

    // ═════════════════ 지니는 아이템 / 임시 효과 ═════════════════

    /// <summary>몇 웨이브 동안만 가는 효과. 웨이브가 끝날 때마다 Run이 WavesLeft를 1씩 줄이고 0이 되면 뗀다</summary>
    public interface ITimedEffect
    {
        int WavesLeft { get; set; }
    }

    /// <summary>랭크업 도구: 몇 웨이브 동안 능력치 1랭크(1.5배). 웨이브가 끝날 때마다 Run이 1씩 줄인다</summary>
    public class TempStatBoost : BattleEffect, ITimedEffect
    {
        public const int Duration = 5;
        public StatType Stat { get; }
        public int WavesLeft { get; set; } = Duration;

        public TempStatBoost(StatType stat)
        {
            Stat = stat;
            PerkName = "랭크업";
        }

        public override float StatMultiplier(StatType s) => s == Stat ? 1.5f : 1f;
    }

    /// <summary>
    /// 열매 공통: 효과를 낸 뒤 사라진다. 단, 내 팰이면 열매 주머니 개수만큼 확률로 남는다 (ctx.BerrySaveChance)
    /// </summary>
    public abstract class HeldBerry : BattleEffect
    {
        protected void Consume(Pal self, BattleContext ctx)
        {
            if (UnityEngine.Random.value < ctx.BerrySaveChance(self))
            {
                ctx.Log($"열매 주머니 덕분에 {Josa.I(PerkName ?? "열매")} 남았다!");
                return;
            }
            self.RemoveEffect(this);
        }
    }

    /// <summary>자뭉열매: HP가 절반 이하가 되면 최대 HP의 1/4 회복 (1회)</summary>
    public class SitrusBerry : HeldBerry
    {
        public override void OnDamaged(Pal self, DamageInfo info, BattleContext ctx) => Check(self, ctx);
        public override void OnTurnEnd(Pal self, BattleContext ctx) => Check(self, ctx);

        void Check(Pal self, BattleContext ctx)
        {
            if (self.IsFainted || self.CurrentHP * 2 > self.MaxHP) return;
            self.Heal(Mathf.Max(1, self.MaxHP / 4));
            ctx.Log($"{Josa.Eun(self.Data.palName)} 자뭉열매로 HP를 회복했다!");
            Consume(self, ctx);
        }
    }

    /// <summary>리샘열매: 상태이상에 걸리면 턴이 끝날 때 치료 (1회)</summary>
    public class LumBerry : HeldBerry
    {
        public override void OnTurnEnd(Pal self, BattleContext ctx)
        {
            if (self.IsFainted || self.Status == StatusCondition.None) return;
            self.CureStatus();
            ctx.Log($"{Josa.Eun(self.Data.palName)} 리샘열매로 상태이상이 나았다!");
            Consume(self, ctx);
        }
    }

    /// <summary>부활의씨앗: 쓰러지면 최대 HP 절반으로 되살아난다 (1회)</summary>
    public class ReviverSeed : BattleEffect
    {
        public override bool TryPreventFaint(Pal self, BattleContext ctx)
        {
            self.RemoveEffect(this);
            self.Revive(Mathf.Max(1, self.MaxHP / 2));
            ctx.Log($"{Josa.Eun(self.Data.palName)} 부활의씨앗으로 다시 일어섰다!");
            return true;
        }
    }

    /// <summary>기합의머리띠: 쓰러질 공격을 확률로 HP 1로 버틴다</summary>
    public class FocusBand : BattleEffect
    {
        readonly float chance;
        public FocusBand(float chance) { this.chance = chance; }

        public override bool TryPreventFaint(Pal self, BattleContext ctx)
        {
            if (UnityEngine.Random.value >= chance) return false;
            self.Revive(1);
            ctx.Log($"{Josa.Eun(self.Data.palName)} 기합의머리띠로 버텼다!");
            return true;
        }
    }

    /// <summary>행복의알: 이 팰이 받는 경험치 배율</summary>
    public class ExpBoostItem : BattleEffect
    {
        readonly float mult;
        public ExpBoostItem(float mult) { this.mult = mult; }
        public override float ExpMultiplier => mult;
    }
}

namespace PalRogue
{
    /// <summary>크리티컬커터: 몇 웨이브 동안 급소 확률 +15%</summary>
    public class TempCritBoost : CritPerk, ITimedEffect
    {
        public const int Duration = 5;
        public int WavesLeft { get; set; } = Duration;
        public TempCritBoost() : base(0.15f) { PerkName = "크리티컬커터"; }
    }

    /// <summary>응원 호루라기(팰월드): 몇 웨이브 동안 한 속성 기술 위력 +30%</summary>
    public class TempTypeBoost : TypePowerPerk, ITimedEffect
    {
        public const int Duration = 5;
        public PalType Type { get; }
        public int WavesLeft { get; set; } = Duration;
        public TempTypeBoost(PalType type) : base(type, 1.3f) { Type = type; PerkName = "응원 호루라기"; }
    }

    /// <summary>능력치 배율 묶음 (최대 HP, 공격, 방어, 스피드). 팰 영혼·임플란트·로터스·펜던트·열매에 쓴다</summary>
    public class StatBundle : BattleEffect
    {
        readonly float hp, atk, def, spd;
        public StatBundle(float hp, float atk, float def, float spd) { this.hp = hp; this.atk = atk; this.def = def; this.spd = spd; }
        public override float MaxHpMultiplier => hp;
        public override float StatMultiplier(StatType s) => s switch
        {
            StatType.Attack => atk,
            StatType.Defense => def,
            _ => spd,
        };
    }

    /// <summary>과사열매: 기술 PP가 0이 되면 턴 끝에 10 회복 (1회)</summary>
    public class LeppaBerry : HeldBerry
    {
        public override void OnTurnEnd(Pal self, BattleContext ctx)
        {
            if (self.IsFainted) return;
            var empty = self.Skills.FirstOrDefault(s => s.PP <= 0);
            if (empty == null) return;
            empty.PP = Mathf.Min(empty.MaxPP, 10);
            ctx.Log($"{Josa.Eun(self.Data.palName)} 과사열매로 {empty.Data.skillName}의 PP를 회복했다!");
            Consume(self, ctx);
        }
    }

    /// <summary>의문열매: 효과가 굉장한 공격을 맞으면 HP 1/4 회복 (1회)</summary>
    public class EnigmaBerry : HeldBerry
    {
        public override void OnDamaged(Pal self, DamageInfo info, BattleContext ctx)
        {
            if (self.IsFainted || info.TypeMultiplier <= 1f) return;
            self.Heal(Mathf.Max(1, self.MaxHP / 4));
            ctx.Log($"{Josa.Eun(self.Data.palName)} 의문열매로 HP를 회복했다!");
            Consume(self, ctx);
        }
    }

    /// <summary>치리·용아·캄라열매: HP가 1/4 이하가 되면 능력치 1랭크 상승 (1회)</summary>
    public class PinchBerry : HeldBerry
    {
        readonly string berry;
        readonly StatType stat;
        public PinchBerry(string berry, StatType stat) { this.berry = berry; this.stat = stat; }

        public override void OnDamaged(Pal self, DamageInfo info, BattleContext ctx) => Check(self, ctx);
        public override void OnTurnEnd(Pal self, BattleContext ctx) => Check(self, ctx);

        void Check(Pal self, BattleContext ctx)
        {
            if (self.IsFainted || self.CurrentHP * 4 > self.MaxHP) return;
            if (self.ModifyStage(stat, 1) > 0)
                ctx.Log($"{Josa.Eun(self.Data.palName)} {berry}로 {Josa.I(StatNames.Korean(stat))} 올라갔다!");
            Consume(self, ctx);
        }
    }

    /// <summary>미니 블랙홀: 매 턴 끝에 상대 아이템 하나를 빼앗는다</summary>
    public class MiniBlackHole : BattleEffect
    {
        public override void OnTurnEnd(Pal self, BattleContext ctx) =>
            ItemCatalog.StealHeldItem(self, ctx.OpponentOf(self), ctx, ItemCatalog.MiniBlackHoleName);
    }

    /// <summary>끈기갈고리손톱: 공격을 맞히면 확률로 상대 아이템 하나를 빼앗는다</summary>
    public class GripClaw : BattleEffect
    {
        readonly float chance;
        public GripClaw(float chance) { this.chance = chance; }
        public override void OnAfterAttack(Pal self, DamageInfo info, BattleContext ctx)
        {
            if (info.FinalDamage <= 0 || UnityEngine.Random.value >= chance) return;
            ItemCatalog.StealHeldItem(self, info.Defender, ctx, PerkName ?? "끈기갈고리손톱");
        }
    }

    /// <summary>멀티렌즈: 공격하고 나서 데미지의 1/4로 한 번 더 때린다</summary>
    public class MultiLens : BattleEffect
    {
        public override void OnAfterAttack(Pal self, DamageInfo info, BattleContext ctx)
        {
            var target = info.Defender;
            if (self.IsFainted || target == null || target.IsFainted || info.FinalDamage <= 0) return;
            target.TakeDamage(Mathf.Max(1, info.FinalDamage / 4));
            ctx.Log($"{Josa.Eun(self.Data.palName)} 멀티렌즈로 한 번 더 공격했다!");
        }
    }

    /// <summary>
    /// 상대 팰이 지니는 아이템. 웨이브가 깊어질수록 더 많이, 더 높은 등급의 아이템을 지닌다.
    /// 보스는 +1개, 전설은 +2개, 최종 보스는 +3개에 미니 블랙홀을 반드시 지닌다.
    /// 아이템은 그 팰에게 맞는 것만 고른다 (속성 강화 아이템은 그 팰이 쓰는 속성만).
    /// </summary>
    public static class EnemyLoadout
    {
        public const int MaxItems = 8;
        /// <summary>이 웨이브마다 지니는 아이템 수가 평균 1개씩 는다</summary>
        public const float WavesPerItem = 30f;
        static readonly HashSet<string> Excluded = new() { "lucky_egg", "mini_black_hole" };
        static readonly HashSet<string> Vitamins = new() { "hp_up", "protein", "iron", "carbos", "calcium", "zinc" };
        static readonly string[] PermanentPrefixes = { "soul_", "implant_", "lotus_", "pendant_", "fruit_" };

        public static int ItemCount(int wave, BossKind kind, System.Random rng)
        {
            float expected = wave / WavesPerItem;
            int count = Mathf.FloorToInt(expected) + (rng.NextDouble() < expected - Mathf.Floor(expected) ? 1 : 0);
            count += kind switch { BossKind.Boss => 1, BossKind.Legend => 2, BossKind.Final => 3, _ => 0 };
            return Mathf.Clamp(count, 0, MaxItems);
        }

        /// <summary>등급 뽑기: 보상과 같은 확률에서 웨이브가 깊을수록 한 단계씩 올라갈 확률이 커진다</summary>
        static ItemTier RollTier(int wave, BossKind kind, System.Random rng)
        {
            var w = ItemTiers.RollWeights;
            int total = w.Sum(), roll = rng.Next(total), t = 0;
            while (roll >= w[t]) { roll -= w[t]; t++; }
            float up = Mathf.Min(0.6f, wave / 250f) + (kind == BossKind.None ? 0f : 0.15f);
            int maxTier = kind == BossKind.None ? (int)ItemTier.Ultimate : (int)ItemTier.Exotic;   // 엑조틱은 보스만
            while (t < maxTier && rng.NextDouble() < up) t++;
            t = Mathf.Min(t, maxTier);
            return (ItemTier)t;
        }

        static bool Fits(ItemDef d, Pal pal)
        {
            if (d.Target != ItemTarget.Pal || Excluded.Contains(d.Id)) return false;
            if (!(d.IsHeld || Vitamins.Contains(d.Id) || PermanentPrefixes.Any(pre => d.Id.StartsWith(pre)))) return false;
            if (ItemCatalog.TypeBoosters.TryGetValue(d.Id, out var type) && !pal.Skills.Any(s => s.Data.type == type && s.Data.category != SkillCategory.Status))
                return false;
            return d.CannotUse(pal) == null;
        }

        /// <summary>상대 팰에게 아이템을 쥐여 준다. 지니게 한 아이템 이름 목록을 돌려준다</summary>
        public static List<string> Equip(Pal pal, Run run, int wave, BossKind kind, System.Random rng)
        {
            var given = new List<string>();
            var used = new HashSet<ItemDef>();
            void Give(ItemDef d)
            {
                used.Add(d);
                d.Apply(new ItemContext { Run = run, Target = pal });
                given.Add(d.Name);
            }

            if (kind == BossKind.Final) Give(ItemCatalog.Get("mini_black_hole"));   // 최종 보스 제로버스는 반드시

            int count = ItemCount(wave, kind, rng);
            for (int i = given.Count; i < count; i++)
            {
                var tier = RollTier(wave, kind, rng);
                ItemDef pick = null;
                // 그 등급에 맞는 게 없으면 한 단계씩 낮춰서 찾는다
                for (int t = (int)tier; t >= 0 && pick == null; t--)
                {
                    var pool = ItemCatalog.All.Where(d => d.Tier == (ItemTier)t && !used.Contains(d) && Fits(d, pal)).ToList();
                    if (pool.Count > 0) pick = pool[rng.Next(pool.Count)];
                }
                if (pick == null) break;
                Give(pick);
            }
            return given;
        }
    }
}
