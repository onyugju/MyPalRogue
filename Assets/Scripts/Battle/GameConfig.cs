using UnityEngine;

namespace PalRogue
{
    /// <summary>게임 전체 설정값. 밸런스 조절은 여기서.</summary>
    public static class GameConfig
    {
        public const int MaxLevel = 100;
        /// <summary>능력치 화면 "어버이/" 뒤에 나오는 트레이너 이름</summary>
        public const string PlayerName = "트레이너";

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

        /// <summary>상대 팰의 ★: 50웨이브마다 1개, 전설·최종 보스는 +1 (최대 ★4)</summary>
        public static int EnemyStars(int wave, BossKind kind) =>
            Mathf.Clamp(wave / 50 + (kind == BossKind.Legend || kind == BossKind.Final ? 1 : 0), 0, MaxStars);

        // ───── 경험치 ─────
        // 다음 레벨까지 필요한 경험치 = ExpBase + ExpPerLevel × 지금 레벨   (Lv10→11: 120, Lv50→51: 520)
        public const int ExpBase = 20;
        public const int ExpPerLevel = 10;
        // 쓰러뜨린 상대가 주는 경험치 = 상대 능력치 총합(HP+공격+방어+스피드) × 상대 레벨 ÷ ExpYieldDivisor
        // (초반 팰은 총합이 200 정도라, 같은 레벨 상대 한 마리에 대략 한 레벨 가까이 오른다)
        public const float ExpYieldDivisor = 20f;

        public static int ExpToNext(int level) => level >= MaxLevel ? 0 : ExpBase + ExpPerLevel * level;

        public static int ExpYield(PalData defeated, int defeatedLevel)
        {
            int statTotal = defeated.baseHP + defeated.baseAttack + defeated.baseDefense + defeated.baseSpeed;
            return Mathf.Max(1, Mathf.RoundToInt(statTotal * defeatedLevel / ExpYieldDivisor));
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

        public static bool IsBossWave(int wave) => wave > 0 && wave % BossWaveStep == 0;

        static int AreaIndex(int wave) => Mathf.Clamp((Mathf.Max(1, wave) - 1) / AreaWaveStep, 0, AreaNames.Length - 1);
        public static string AreaName(int wave) => AreaNames[AreaIndex(wave)];
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
        public const float LegendStatMultiplier = 1.25f, LegendHpMultiplier = 1.5f;
        public const float FinalBossStatMultiplier = 1.35f, FinalBossHpMultiplier = 2f;

        // 적 레벨 = EnemyLevelBase + 웨이브 × EnemyLevelPerWave + (0 ~ EnemyLevelSpread 무작위)
        //          보스 + BossLevelBonus, 전설 + LegendLevelBonus, 최종 보스는 MaxLevel
        // (웨이브 1: Lv.4~6,  웨이브 20 전설: Lv.24~26,  웨이브 100 전설: Lv.64~66,  웨이브 190: Lv.100)
        public const int EnemyLevelBase = 4;
        public const float EnemyLevelPerWave = 0.5f;
        public const int EnemyLevelSpread = 2;
        public const int BossLevelBonus = 5;
        public const int LegendLevelBonus = 10;

        public static int EnemyLevel(int wave, BossKind kind, System.Random rng)
        {
            if (kind == BossKind.Final) return MaxLevel;
            int level = EnemyLevelBase + Mathf.RoundToInt(wave * EnemyLevelPerWave) + rng.Next(0, EnemyLevelSpread + 1);
            if (kind == BossKind.Boss) level += BossLevelBonus;
            if (kind == BossKind.Legend) level += LegendLevelBonus;
            return Mathf.Clamp(level, 1, MaxLevel);
        }

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
    }
}
