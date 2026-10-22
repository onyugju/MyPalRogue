using UnityEngine;

namespace PalRogue
{
    /// <summary>게임 전체 설정값. 밸런스 조절은 여기서.</summary>
    public static class GameConfig
    {
        public const int MaxLevel = 100;

        // ───── 강화 효과(퍽) ─────
        /// <summary>이 간격의 레벨(10, 20, … 100)에 도달하면 강화 효과 3개 중 1개를 고른다</summary>
        public const int PerkLevelStep = 10;
        public const int PerkChoices = 3;

        public static bool IsPerkLevel(int level) => level > 0 && level % PerkLevelStep == 0;

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
    }
}
