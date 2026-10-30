using System.Collections.Generic;
using UnityEngine;

namespace PalRogue
{
    /// <summary>게임 전체 설정값. 밸런스 조절은 여기서.</summary>
    public static class GameConfig
    {
        public const int MaxLevel = 100;
        /// <summary>능력치 화면 "어버이/" 뒤에 나오는 트레이너 이름</summary>
        public const string PlayerName = "트레이너";

        // ───── 상대 AI (EnemyAI) ─────
        /// <summary>
        /// 상대의 똑똑함 (0~1). 야생 팰은 웨이브 1에서 0.3 → 웨이브 90쯤 0.9까지 점점 똑똑해진다.
        /// 보스는 0.95, 전설·최종 보스는 1 (항상 가장 좋은 기술).
        /// </summary>
        public static float EnemySmartness(int wave, BossKind kind) => kind switch
        {
            BossKind.Legend or BossKind.Final => 1f,
            BossKind.Boss => 0.95f,
            _ => Mathf.Clamp(0.3f + wave / 150f, 0.3f, 0.9f),
        };

        // ───── 강화 효과(퍽) ─────
        /// <summary>이 간격의 레벨(10, 20, … 100)에 도달하면 강화 효과 3개 중 1개를 고른다</summary>
        public const int PerkLevelStep = 10;
        public const int PerkChoices = 3;

        public static bool IsPerkLevel(int level) => level > 0 && level % PerkLevelStep == 0;
        /// <summary>전설 등급 강화 효과가 나오기 시작하는 레벨</summary>
        public const int LegendaryPerkLevel = 40;
        /// <summary>강화 효과 카드를 공짜로 다시 뽑을 수 있는 횟수 (고를 때마다)</summary>
        public const int PerkFreeRerolls = 1;

        // ───── 각성 (메가진화·다이맥스 대신) ─────
        /// <summary>이 레벨부터 강화 레벨(10단위)에 아직 각성이 없으면 강화 효과 대신 각성을 하나 고른다</summary>
        public const int AwakenLevel = 50;
        /// <summary>이 레벨에 각성이 진각성이 된다</summary>
        public const int TrueAwakenLevel = 100;
        /// <summary>HP가 이 비율 이하가 되면 각성한다 (전투마다 한 번)</summary>
        public const float AwakenHpRatio = 0.5f;
        public const float AwakenStatMultiplier = 1.2f;
        public const float TrueAwakenStatMultiplier = 1.35f;

        // ───── 응축 ★ (진화 대신, 팰월드 팰 응축) ─────
        public const int MaxStars = 4;
        /// <summary>★ 하나당 최대 HP·공격·방어·스피드 +10%</summary>
        public const float StarStatBonus = 0.1f;
        /// <summary>★4를 찍으면 영웅 등급 이상 강화 효과를 하나 더 고른다</summary>
        public const bool BonusPerkAtMaxStars = true;

        /// <summary>상대 팰의 ★: 50웨이브마다 1개, 최종 보스와 60웨이브부터의 전설은 +1 (최대 ★4)</summary>
        public static int EnemyStars(int wave, BossKind kind) =>
            Mathf.Clamp(wave / 50 + (kind == BossKind.Final || (kind == BossKind.Legend && wave >= 60) ? 1 : 0), 0, MaxStars);

        // ───── 경험치 ─────
        // 다음 레벨까지 필요한 경험치 = ExpBase + ExpPerLevel × 지금 레벨   (Lv10→11: 120, Lv50→51: 520)
        public const int ExpBase = 20;
        public const int ExpPerLevel = 10;
        // 쓰러뜨린 상대가 주는 경험치 = 상대 능력치 총합(HP+공격+방어+스피드) × 상대 레벨 ÷ ExpYieldDivisor × 레벨 차이 보정
        // 레벨 차이 보정 (포켓몬 5세대 방식): ((2×상대Lv + 10) ÷ (상대Lv + 내Lv + 10))^ExpLevelPower
        //  - 상대보다 낮으면 더 많이 받아서 금방 따라잡고, 높으면 적게 받아서 혼자 너무 앞서가지 않는다
        //  - 같은 레벨이면 1배. 초반(Lv5, 총합 200)은 한 마리에 1~2레벨쯤 오른다
        public const float ExpYieldDivisor = 8f;
        public const float ExpLevelPower = 4f;
        public const float ExpLevelScaleMin = 0.15f, ExpLevelScaleMax = 3f;

        public static int ExpToNext(int level) => level >= MaxLevel ? 0 : ExpBase + ExpPerLevel * level;

        /// <summary>쓰러뜨린 상대가 주는 기본 경험치 (레벨 차이 보정 전)</summary>
        public static int ExpYield(PalData defeated, int defeatedLevel)
        {
            int statTotal = defeated.baseHP + defeated.baseAttack + defeated.baseDefense + defeated.baseSpeed;
            return Mathf.Max(1, Mathf.RoundToInt(statTotal * defeatedLevel / ExpYieldDivisor));
        }

        /// <summary>받는 팰의 레벨에 따른 경험치 배율</summary>
        public static float ExpLevelScale(int defeatedLevel, int receiverLevel)
        {
            float k = (2f * defeatedLevel + 10f) / (defeatedLevel + receiverLevel + 10f);
            return Mathf.Clamp(Mathf.Pow(k, ExpLevelPower), ExpLevelScaleMin, ExpLevelScaleMax);
        }

        // ───── 웨이브(런) ─────
        /// <summary>이 웨이브까지 이기면 모험 성공 (마지막 웨이브는 최종 보스 제로버스)</summary>
        public const int FinalWave = 190;
        /// <summary>이 간격의 웨이브(10, 20, …)마다 보스가 나온다</summary>
        public const int BossWaveStep = 10;

        /// <summary>이 간격마다 지역이 바뀐다. 각 지역의 마지막 웨이브(20, 40, …)에 그 지역 속성의 왕(전설 팰)이 나올 수 있다</summary>
        public const int AreaWaveStep = 20;
        public static readonly string[] AreaNames =
            { "초원", "화산", "숲", "바다", "설원", "사막", "뇌운 고원", "용의 둥지", "어둠의 성", "공허" };
        /// <summary>지역마다 자주 나오는 속성 (그 속성 야생 팰이 2배로 잘 나온다)</summary>
        public static readonly PalType[] AreaTypes =
        {
            PalType.Neutral, PalType.Fire, PalType.Grass, PalType.Water, PalType.Ice,
            PalType.Ground, PalType.Electric, PalType.Dragon, PalType.Dark, PalType.Void,
        };
        /// <summary>
        /// 지역마다 쓰는 포켓로그 배경(arenas 폴더의 이름). {이름}_bg = 배경, _a = 내 발판, _b = 상대 발판, _b_1~3 = 상대 발판 장식.
        /// 그림은 Assets/Resources/Arenas 에 있다 (메뉴 24로 확인).
        /// </summary>
        public static readonly string[] AreaBiomes =
            { "plains", "volcano", "forest", "sea", "snowy_forest", "desert", "mountain", "wasteland", "abyss", "space" };

        public static bool IsBossWave(int wave) => wave > 0 && wave % BossWaveStep == 0;

        public static int AreaIndex(int wave) => Mathf.Clamp((Mathf.Max(1, wave) - 1) / AreaWaveStep, 0, AreaNames.Length - 1);
        public static string AreaName(int wave) => AreaNames[AreaIndex(wave)];
        public static string AreaBiome(int wave) => AreaBiomes[Mathf.Min(AreaIndex(wave), AreaBiomes.Length - 1)];
        public static PalType AreaType(int wave) => AreaTypes[Mathf.Min(AreaIndex(wave), AreaTypes.Length - 1)];

        /// <summary>
        /// 일반 웨이브에 나올 수 있는 팰의 범위 (능력치 총합이 낮은 순서로 앞에서부터 이 비율만큼).
        /// 웨이브 1: 하위 40% → 웨이브 60부터: 전체
        /// </summary>
        public static float WildPoolShare(int wave) => Mathf.Clamp01(0.4f + 0.6f * (wave - 1) / 59f);

        // ───── 전설 팰 (속성의 왕) ─────
        // 20웨이브마다 LegendChance 확률로 그 웨이브의 보스가 속성의 왕으로 바뀐다.
        // 전설 팰은 일반 웨이브·일반 보스로는 나오지 않는다. (에셋 이름으로 구분)
        public static readonly (int wave, string asset)[] LegendKings =
        {
            (20,  "198_Paladius"),         // 무속성 왕 팔라디우스
            (40,  "174_Flaracle"),         // 불속성 왕 센코
            (60,  "186_Lyleen"),           // 풀속성 왕 릴린
            (80,  "201_Neptilius"),        // 물속성 왕 넵티오스
            (100, "200_Frostallion"),      // 얼음속성 왕 빙천마
            (120, "184_Aegidron"),         // 땅속성 왕 셀가드라
            (140, "172_Dynamoff"),         // 번개속성 왕 뇌운조
            (160, "202_Jetragon"),         // 드래곤속성 왕 제트래곤
            (180, "000_Bellanoir_Libero"), // 어둠속성 왕 벨라루주
        };
        /// <summary>마지막 웨이브에 반드시 나오는 최종 보스 (공허속성 제로버스)</summary>
        public const string FinalBossAsset = "000_Zerobus";
        /// <summary>20웨이브마다 속성의 왕이 나올 확률 (0~1). 안 나오면 일반 보스</summary>
        public const float LegendChance = 0.5f;
        /// <summary>전설 팰을 잡을 때 쓰는 포획률 상한 (원래 포획률이 더 높아도 이 값까지만)</summary>
        public const int LegendCatchRate = 15;

        public static string LegendAssetFor(int wave)
        {
            foreach (var (w, asset) in LegendKings) if (w == wave) return asset;
            return null;
        }

        public static bool IsLegendary(PalData d) =>
            d != null && (d.name == FinalBossAsset || System.Array.Exists(LegendKings, k => k.asset == d.name));

        // 보스가 받는 강화 (능력치 배율 / 최대 HP 배율)
        // 전설(속성의 왕)은 웨이브가 깊을수록 세진다: 웨이브 20 ×1.03/×1.05 → 웨이브 100부터 ×1.25/×1.5
        public const float LegendStatMultiplier = 1.25f, LegendHpMultiplier = 1.5f;
        static float LegendStrength(int wave) => Mathf.Clamp01(0.1f + 0.9f * (wave - 20) / 80f);
        public static float LegendStat(int wave) => 1f + (LegendStatMultiplier - 1f) * LegendStrength(wave);
        public static float LegendHp(int wave) => 1f + (LegendHpMultiplier - 1f) * LegendStrength(wave);
        public const float FinalBossStatMultiplier = 1.35f, FinalBossHpMultiplier = 2f;

        // 적 레벨 = 3 + 웨이브 × 0.5 (+ 웨이브 30부터는 웨이브마다 0.15씩 더 빨리) + (0 ~ EnemyLevelSpread 무작위)
        //          보스 + BossLevelBonus(웨이브), 전설 + LegendLevelBonus(웨이브), 최종 보스는 MaxLevel
        // (웨이브 1: Lv.4~6, 웨이브 10 보스: Lv.9~11, 웨이브 20 전설: Lv.14~16, 웨이브 60 전설: Lv.43~45, 웨이브 100: Lv.64~66)
        // 시뮬레이션으로 맞춘 값: 초반은 내 팰이 상대보다 3~6레벨 높게 여유 있게, 웨이브 30부터 조금씩 따라잡힌다
        public const int EnemyLevelBase = 3;
        public const float EnemyLevelPerWave = 0.5f;
        public const float EnemyLevelLateAccel = 0.15f;
        public const int EnemyLevelLateFrom = 30;
        public const int EnemyLevelSpread = 2;

        /// <summary>보스 레벨 보너스: 웨이브 10 +1, 20~30 +3, 40부터 +5</summary>
        public static int BossLevelBonus(int wave) => wave < 20 ? 1 : wave < 40 ? 3 : 5;
        /// <summary>전설 레벨 보너스: 웨이브 20 +1, 40 +3, 60 +5, … 최대 +10</summary>
        public static int LegendLevelBonus(int wave) => Mathf.Clamp((wave - 10) / 10, 0, 10);

        public static int EnemyLevel(int wave, BossKind kind, System.Random rng)
        {
            if (kind == BossKind.Final) return MaxLevel;
            float baseLevel = EnemyLevelBase + wave * EnemyLevelPerWave + Mathf.Max(0, wave - EnemyLevelLateFrom) * EnemyLevelLateAccel;
            int level = Mathf.RoundToInt(baseLevel) + rng.Next(0, EnemyLevelSpread + 1);
            if (kind == BossKind.Boss) level += BossLevelBonus(wave);
            if (kind == BossKind.Legend) level += LegendLevelBonus(wave);
            return Mathf.Clamp(level, 1, MaxLevel);
        }

        /// <summary>일반 보스 후보: 그 웨이브 야생 범위보다 조금 넓은 곳에서 능력치 총합 상위 1/3 (초반 보스가 후반 팰이 되지 않게)</summary>
        public const float BossPoolExtraShare = 0.1f;

        // ───── 보스 실드 (HP바 칸 수) ─────
        // 보스: Lv.24까지 2칸, Lv.25~49 3칸, Lv.50 이상 4칸 / 전설: 한 칸 더 (최대 5) / 최종 보스: 5칸
        public static int BossSegments(BossKind kind, int level) => kind switch
        {
            BossKind.Boss => Mathf.Clamp(level / 25 + 2, 2, 4),
            BossKind.Legend => Mathf.Clamp(level / 25 + 3, 3, 5),
            BossKind.Final => 5,
            _ => 1,
        };

        // ───── 고정 보상 ─────
        /// <summary>이 웨이브를 이기면(잡아도) 보상 화면 전에 반드시 받는 아이템 (아이템 Id)</summary>
        public static readonly (int wave, string item)[] GuaranteedRewards =
        {
            (8,  "exp_share"),   // 학습장치
            (8,  "exp_charm"),   // 경험부적
            (25, "exp_share"),   // 학습장치
            (25, "exp_charm"),   // 경험부적
        };

        /// <summary>보스 웨이브(10, 20, …)를 이기면 다음 웨이브로 가기 전에 파티를 완전히 회복한다</summary>
        public const bool HealAfterBoss = true;

        // ───── 돈 ─────
        public const int StartingMoney = 1000;
        /// <summary>웨이브를 이기면(잡아도) 받는 돈. 보스는 3배</summary>
        public static int WinMoney(int wave, bool boss) => (50 + 10 * wave) * (boss ? 3 : 1);

        // ───── 스타팅 팰 선택 (포켓로그 "스타팅 포켓몬" 화면) ─────
        /// <summary>파티에 넣을 수 있는 코스트 합계</summary>
        public const int StarterBudget = 10;
        /// <summary>모험을 시작할 때 팰의 레벨</summary>
        public const int StarterLevel = 5;
        public const int MaxPartySize = 6;
        /// <summary>처음부터 열려 있는 스타팅 팰 5종 (코스트 3)</summary>
        public static readonly string[] StarterAssets = { "004_Lifmunk", "029_Foxparks", "015_Jolthog", "013_Pupperai", "005_Fuack" };
        public const int StarterPalCost = 3;

        /// <summary>
        /// 팰마다 스타팅 코스트 (에셋 이름 → 코스트). 원작 몸집·희귀도와 이 게임의 능력치 단계(초반/중반/후반/전설)로 정했다.
        /// 여기 없는 팰은 능력치 합계로 정한다 (StarterCost 참고).
        /// </summary>
        static readonly Dictionary<string, int> StarterCosts = new Dictionary<string, int>
        {
            // 초반 (1~2)
            ["001_Lamball"] = 1, ["002_Cattiva"] = 1,
            ["016_Depresso"] = 2, ["017_Pengullet"] = 2, ["023_Tanzee"] = 2, ["026_Rooby"] = 2, ["028_Rushoar"] = 2, ["042_Sparkit"] = 2,
            // 중반 (3~5)
            ["040_Mozzarina"] = 3,
            ["033_Direhowl"] = 4, ["058_Arsox"] = 4, ["075_Surfent"] = 4, ["076_Robinquill"] = 4, ["079_Katress"] = 4, ["084_Dinossom"] = 4,
            ["095_Foxcicle"] = 4, ["103_Chillet"] = 4, ["117_Maraith"] = 4, ["140_Sekhmet"] = 4,
            ["063_Elphidran"] = 5, ["093_Pyrin"] = 5, ["098_Rayhound"] = 5,
            // 후반 (6~7)
            ["080_Helzephyr"] = 6, ["102_Mossanda"] = 6, ["112_Blazehowl"] = 6, ["124_Quivern"] = 6, ["134_Wumpo"] = 6, ["139_Anubis"] = 6, ["185_Grizzbolt"] = 6,
            ["121_Jormuntide"] = 7,
            // 속성의 왕 (전설, 7~9)
            ["174_Flaracle"] = 7, ["172_Dynamoff"] = 7,
            ["198_Paladius"] = 8, ["186_Lyleen"] = 8, ["201_Neptilius"] = 8, ["200_Frostallion"] = 8, ["184_Aegidron"] = 8,
            ["202_Jetragon"] = 9, ["000_Bellanoir_Libero"] = 9,
        };

        public static bool IsStarterPal(PalData d) => d != null && System.Array.IndexOf(StarterAssets, d.name) >= 0;

        /// <summary>스타팅 선택 화면에 나올 수 있는 팰 (최종 보스 제로버스는 잡을 수 없어서 빠진다)</summary>
        public static bool CanBeStarter(PalData d) => d != null && d.name != FinalBossAsset && (d.canBeCaught || IsLegendary(d));

        public static int StarterCost(PalData d)
        {
            if (d == null) return 0;
            if (IsStarterPal(d)) return StarterPalCost;
            if (StarterCosts.TryGetValue(d.name, out var cost)) return cost;
            if (IsLegendary(d)) return 8;
            int total = d.baseHP + d.baseAttack + d.baseDefense + d.baseSpeed;   // 새로 추가한 팰: 능력치 합계로
            return total < 340 ? 2 : total < 440 ? 4 : total < 560 ? 6 : 8;
        }
    }
}
