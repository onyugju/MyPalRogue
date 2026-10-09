using System.Collections.Generic;

namespace PalRogue
{
    /// <summary>
    /// 속성 상성표. "강한 상대" 목록만 정의하면 약한 쪽은 역방향으로 자동 계산된다.
    /// 밸런스 조절은 아래 상수 3개만 바꾸면 된다.
    /// </summary>
    public static class TypeChart
    {
        public const float SuperEffective = 1.5f;    // 효과 굉장 (원작 느낌으로 낮추려면 1.5f)
        public const float NotVeryEffective = 0.5f;  // 효과 별로
        public const float Stab = 1.2f;  // 자속 보너스

        // 팰월드 원작 상성 (공격 속성 -> 강한 방어 속성들)
        private static readonly Dictionary<PalType, PalType[]> strongAgainst = new()
        {
            { PalType.Electric, new[] { PalType.Water } },
            { PalType.Water,    new[] { PalType.Fire } },
            { PalType.Fire,     new[] { PalType.Grass, PalType.Ice } },
            { PalType.Grass,    new[] { PalType.Ground } },
            { PalType.Ground,   new[] { PalType.Electric } },
            { PalType.Ice,      new[] { PalType.Dragon } },
            { PalType.Dragon,   new[] { PalType.Dark } },
            { PalType.Dark,     new[] { PalType.Neutral } },
            { PalType.Neutral,  new PalType[0] },
        };

        /// <summary>단일 방어 속성에 대한 배율</summary>
        public static float GetMultiplier(PalType attack, PalType defense)
        {
            // 고유속성(Void)은 공격할 때도 방어할 때도 상성에 관여하지 않는다
            if (attack == PalType.Void || defense == PalType.Void) return 1f;
            if (IsStrong(attack, defense)) return SuperEffective;   // 공격이 강함
            if (IsStrong(defense, attack)) return NotVeryEffective; // 방어가 강함 (역방향)
            return 1f;
        }

        /// <summary>복합 속성 방어 배율 (두 배율을 곱함). defense2가 null이면 단일 속성.</summary>
        public static float GetMultiplier(PalType attack, PalType defense1, PalType? defense2)
        {
            float m = GetMultiplier(attack, defense1);
            if (defense2.HasValue) m *= GetMultiplier(attack, defense2.Value);
            return m;
        }

        /// <summary>자속 보너스: 스킬 속성이 사용자 속성 중 하나와 같으면 1.2배</summary>
        public static float GetStab(PalType skillType, PalType user1, PalType? user2)
        {
            bool match = skillType == user1 || (user2.HasValue && skillType == user2.Value);
            return match ? Stab : 1f;
        }

        private static bool IsStrong(PalType attack, PalType defense)
        {
            // Void처럼 표에 없는 속성은 "강한 상대 없음"으로 처리
            if (!strongAgainst.TryGetValue(attack, out var targets)) return false;
            foreach (var t in targets)
                if (t == defense) return true;
            return false;
        }
    }
}