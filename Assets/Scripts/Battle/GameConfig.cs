using System;

namespace PalRogue
{
    /// <summary>게임 전체 설정값. 밸런스 조절은 여기서.</summary>
    public static class GameConfig
    {
        public const int MaxLevel = 50;

        /// <summary>이 레벨에 도달하면 강화 효과(퍽) 선택지를 띄운다.</summary>
        public static readonly int[] PerkLevels = { 10, 20, 30, 40 };

        public static bool IsPerkLevel(int level) => Array.IndexOf(PerkLevels, level) >= 0;
    }
}
