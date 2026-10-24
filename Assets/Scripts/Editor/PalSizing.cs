#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 팰 크기 자동 맞춤 (메뉴 PalRogue/7-2)
    ///
    /// 예전 방식은 그림의 "긴 쪽"을 같은 길이에 맞춰서, 씨무기·레비드라처럼 가로로 긴 팰은 가늘고 작게,
    /// 덩치 큰 팰과 작은 팰이 모두 같은 크기로 보였다. 이제는:
    ///  ① 그림의 "넓이"(가로×세로)가 같아지도록 맞춘다 → 길쭉한 팰도 몸집만큼 보인다
    ///  ② 능력치 합계로 몸집을 정한다: 초반 팰 0.9배 ~ 전설급 1.25배 (도로롱은 작게, 제트래곤은 크게)
    ///  ③ 화면을 벗어나지 않게 가로·세로 최대 크기를 둔다 (상대: 정보창·하늘, 나: 화면 끝)
    /// 단위는 월드 유닛 (1유닛 = 화면 18픽셀). 앞모습 = 상대 자리, 뒷모습 = 내 자리(더 가까워서 크게).
    /// </summary>
    public static class PalSizing
    {
        // 기준 몸집 (넓이의 제곱근, 유닛)
        public const float FrontTarget = 3.0f, BackTarget = 3.9f;
        // 화면 안에 들어가는 최대 크기 (유닛)
        public const float FrontMaxW = 5.2f, FrontMaxH = 4.0f;
        public const float BackMaxW = 6.4f, BackMaxH = 4.8f;
        // 능력치 합계 → 몸집 배율
        const float SmallTotal = 300f, BigTotal = 620f, SmallSize = 0.9f, BigSize = 1.25f;

        public static float SizeFactor(PalData d)
        {
            float total = d.baseHP + d.baseAttack + d.baseDefense + d.baseSpeed;
            return Mathf.Lerp(SmallSize, BigSize, Mathf.InverseLerp(SmallTotal, BigTotal, total));
        }

        public static float Scale(Sprite s, PalData d, bool back)
        {
            if (s == null) return 1f;
            var size = s.bounds.size;                                   // 배율 1일 때 크기 (유닛)
            float w = Mathf.Max(0.01f, size.x), h = Mathf.Max(0.01f, size.y);
            float target = (back ? BackTarget : FrontTarget) * SizeFactor(d);
            float scale = target / Mathf.Sqrt(w * h);
            scale = Mathf.Min(scale, (back ? BackMaxW : FrontMaxW) / w, (back ? BackMaxH : FrontMaxH) / h);
            return scale;
        }

        /// <summary>팰 데이터의 앞·뒤 배율을 다시 계산한다. 바뀌었으면 true</summary>
        public static bool Apply(PalData d)
        {
            if (d == null) return false;
            float front = d.frontSprite != null ? Scale(d.frontSprite, d, false) : d.frontScale;
            float back = d.backSprite != null ? Scale(d.backSprite, d, true) : d.backScale;
            bool changed = !Mathf.Approximately(front, d.frontScale) || !Mathf.Approximately(back, d.backScale);
            d.frontScale = front;
            d.backScale = back;
            if (changed) EditorUtility.SetDirty(d);
            return changed;
        }

        [MenuItem("PalRogue/7-2. 팰 크기 자동 맞춤 (전체)")]
        static void ApplyAll()
        {
            var lines = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:PalData", new[] { "Assets/Data/Pals" }))
            {
                var d = AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(guid));
                if (d == null) continue;
                float before = d.frontScale;
                if (Apply(d)) lines.Add($"{d.palName} {before:0.00}→{d.frontScale:0.00}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[팰 크기] {lines.Count}종 크기를 다시 맞췄습니다 (앞모습 배율): {string.Join(", ", lines)}");
        }
    }
}
#endif
