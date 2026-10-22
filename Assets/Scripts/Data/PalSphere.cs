using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 팰 스피어 5종. 포켓로그 볼과 역할이 같다 (몬스터볼 / 슈퍼볼 / 하이퍼볼 / 로그볼 / 마스터볼).
    /// 숫자 값은 보유 개수 배열의 칸 번호로도 쓰이므로 순서를 바꾸지 말 것.
    /// </summary>
    public enum SphereType
    {
        Pal = 0,       // 몬스터볼
        Giga = 1,      // 슈퍼볼
        Hyper = 2,     // 하이퍼볼
        Ultimate = 3,  // 로그볼
        Exotic = 4,    // 마스터볼 (반드시 잡힘)
    }

    public static class PalSpheres
    {
        public const int Count = 5;

        /// <summary>메뉴에 보이는 이름. 바꾸고 싶으면 여기만 고치면 된다</summary>
        public static readonly string[] Names = { "팰 스피어", "기가 스피어", "하이퍼 스피어", "얼티밋 스피어", "엑조틱 스피어" };

        /// <summary>포획 배율 (포켓로그와 같음: 몬스터볼 1, 슈퍼볼 1.5, 하이퍼볼 2, 로그볼 3)</summary>
        static readonly float[] CatchMultipliers = { 1f, 1.5f, 2f, 3f, 255f };

        /// <summary>Resources 안의 이미지 경로</summary>
        static readonly string[] ImagePaths =
        {
            "1.팰/3.Pal Spheres/0_Pal Sphere", "1.팰/3.Pal Spheres/1_Giga Sphere", "1.팰/3.Pal Spheres/2_Hyper Sphere",
            "1.팰/3.Pal Spheres/3_Ultimate Sphere", "1.팰/3.Pal Spheres/4_Exotic Sphere",
        };

        public static string Name(SphereType t) => Names[(int)t];
        public static float CatchMultiplier(SphereType t) => CatchMultipliers[(int)t];
        public static bool AlwaysCatches(SphereType t) => t == SphereType.Exotic;

        /// <summary>스피어 그림. 여러 조각으로 잘린 이미지여도 가장 큰 조각을 돌려준다</summary>
        public static Sprite LoadSprite(SphereType t)
        {
            Sprite best = null;
            foreach (var s in Resources.LoadAll<Sprite>(ImagePaths[(int)t]))
                if (best == null || s.rect.width * s.rect.height > best.rect.width * best.rect.height) best = s;
            return best;
        }
    }
}
