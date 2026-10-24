#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
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
    ///  ④ (먼저) 그림 둘레의 "거의 투명한" 픽셀까지 그림으로 잘린 스프라이트를 실제 그림 크기로 다시 자른다.
    ///     AI로 만든 그림은 배경에 눈에 안 보이는 옅은 픽셀이 남아 있어서, 그대로 두면 팰이 작아지고 발판 위에 떠 보인다.
    /// 단위는 월드 유닛 (1유닛 = 화면 18픽셀). 앞모습 = 상대 자리, 뒷모습 = 내 자리(더 가까워서 크게).
    /// </summary>
    public static class PalSizing
    {
        // 기준 몸집 (넓이의 제곱근, 유닛)
        public const float FrontTarget = 3.0f, BackTarget = 3.9f;
        // 화면 안에 들어가는 최대 크기 (유닛)
        public const float FrontMaxW = 5.2f, FrontMaxH = 4.0f;
        public const float BackMaxW = 6.4f, BackMaxH = 4.8f;
        // 크게 보정한 팰이라도 절대 넘지 않는 크기 (상대 발 위치에서 화면 위 끝까지 ≈ 4.6유닛)
        const float FrontCapW = 6.6f, FrontCapH = 4.5f, BackCapW = 7.6f, BackCapH = 5.6f;
        // 능력치 합계 → 몸집 배율
        const float SmallTotal = 300f, BigTotal = 620f, SmallSize = 0.9f, BigSize = 1.25f;

        /// <summary>
        /// 팰마다 원작 몸집에 맞춘 보정 (포켓몬처럼: 작은 팰은 작게, 거대한 팰은 크게). 에셋 이름 → 배율.
        /// 그림 넓이만으로는 동그란 팰(도로롱·펭키)은 커 보이고, 가늘고 긴 팰(제로버스)은 작아 보여서 손으로 맞춘 값이다.
        /// 더 바꾸고 싶으면 PalData의 sizeTune(인스펙터)을 조절하면 이 값에 곱해진다.
        /// </summary>
        static readonly Dictionary<string, float> BodyTune = new Dictionary<string, float>
        {
            ["000_Zerobus"] = 2.2f,       // 최종 보스: 화면을 압도하는 크기 (화면 위로 넘치지 않는 최대치까지)
            ["202_Jetragon"] = 1.15f,     // 전설의 용
            ["000_Bellanoir_Libero"] = 1.35f, // 보스: 날개 펼친 거대한 몸집
            ["184_Aegidron"] = 1.35f,     // 거대한 공룡
            ["121_Jormuntide"] = 1.35f,   // 거대한 바다뱀 용
            ["186_Lyleen"] = 1.3f,        // 키 큰 여왕
            ["198_Paladius"] = 1.3f,      // 전설의 기사 말
            ["200_Frostallion"] = 1.3f,   // 전설의 얼음 말
            ["201_Neptilius"] = 1.35f,    // 거대한 바다 용
            ["001_Lamball"] = 0.8f,       // 작은 양
            ["017_Pengullet"] = 0.72f,    // 작은 펭귄
            ["004_Lifmunk"] = 1.25f,      // 그림이 가늘어서 너무 작게 보이던 것
        };

        /// <summary>
        /// 상대 자리 크기를 손으로 정한 팰 (에셋 이름 → 앞모습 배율). 화면 크기 제한도 무시한다.
        /// 커진 만큼 발 위치를 아래로 내려서, 원래 크기일 때의 몸 중심 위치를 그대로 지킨다 (화면 위로 덜 넘치게).
        /// </summary>
        static readonly Dictionary<string, float> FixedFront = new Dictionary<string, float>
        {
            ["000_Zerobus"] = 2.4f,       // 최종 보스: 화면을 꽉 채우는 압도적인 크기 (직접 보고 정한 값)
        };

        public static float Tune(PalData d) =>
            (d.sizeTune > 0f ? d.sizeTune : 1f) * (BodyTune.TryGetValue(d.name, out var t) ? t : 1f);

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
            float target = (back ? BackTarget : FrontTarget) * SizeFactor(d) * Tune(d);
            float scale = target / Mathf.Sqrt(w * h);
            // 크게 보정한 팰(보스·전설)은 최대 크기도 조금 늘려 준다. 단, 화면(상대: 위쪽 끝, 나: 화면 끝)을 넘지 않는 선까지
            float grow = Mathf.Sqrt(Mathf.Max(1f, Tune(d)));
            float maxW = back ? Mathf.Min(BackMaxW * grow, BackCapW) : Mathf.Min(FrontMaxW * grow, FrontCapW);
            float maxH = back ? Mathf.Min(BackMaxH * grow, BackCapH) : Mathf.Min(FrontMaxH * grow, FrontCapH);
            scale = Mathf.Min(scale, maxW / w, maxH / h);
            return scale;
        }

        /// <summary>팰 데이터의 앞·뒤 배율을 다시 계산한다. 바뀌었으면 true</summary>
        public static bool Apply(PalData d)
        {
            if (d == null) return false;
            float front = d.frontSprite != null ? Scale(d.frontSprite, d, false) : d.frontScale;
            float back = d.backSprite != null ? Scale(d.backSprite, d, true) : d.backScale;
            float yOffset = 0f;
            if (d.frontSprite != null && FixedFront.TryGetValue(d.name, out var fixedScale))
            {
                yOffset = -(fixedScale - front) * d.frontSprite.bounds.size.y / 2f;
                if (d.backSprite == null) back = front * 1.3f;          // 내 자리(뒤집은 앞모습)는 원래 크기 기준으로
                front = fixedScale;
            }
            bool changed = !Mathf.Approximately(front, d.frontScale) || !Mathf.Approximately(back, d.backScale)
                           || !Mathf.Approximately(yOffset, d.frontYOffset);
            d.frontYOffset = yOffset;
            d.frontScale = front;
            d.backScale = back;
            if (changed) EditorUtility.SetDirty(d);
            return changed;
        }

        // ───────── 옅은 테두리 잘라내기 ─────────
        const byte AlphaCut = 24;          // 이보다 투명한 픽셀은 그림이 아닌 것으로 본다
        const float TrimMargin = 0.04f;    // 실제 그림이 스프라이트보다 4% 넘게 작을 때만 다시 자른다

        /// <summary>
        /// 정지 그림(자동 슬라이스된 Multiple 스프라이트)이 옅은 테두리까지 포함해 크게 잘려 있으면 실제 그림 크기로 다시 자른다.
        /// 스프라이트 ID는 그대로라서 팰 데이터의 연결은 끊기지 않는다. 다시 잘랐으면 새로 불러온 스프라이트를 돌려준다.
        /// </summary>
        public static Sprite Trim(Sprite sprite)
        {
            if (sprite == null) return null;
            string path = AssetDatabase.GetAssetPath(sprite);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp) || imp.spriteImportMode != SpriteImportMode.Multiple) return sprite;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(imp);
            dp.InitSpriteEditorDataProvider();
            var rects = dp.GetSpriteRects();
            var target = rects.FirstOrDefault(r => r.name == sprite.name);
            if (target == null) return sprite;

            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(File.ReadAllBytes(path))) { Object.DestroyImmediate(tex); return sprite; }
            var px = tex.GetPixels32();
            int texW = tex.width;
            Object.DestroyImmediate(tex);

            var r0 = target.rect;
            int x0 = (int)r0.xMin, y0 = (int)r0.yMin, x1 = (int)r0.xMax, y1 = (int)r0.yMax;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    if (px[y * texW + x].a > AlphaCut)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            if (maxX < 0) return sprite;
            var tight = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            if (tight.width > r0.width * (1f - TrimMargin) && tight.height > r0.height * (1f - TrimMargin)) return sprite;   // 이미 딱 맞음

            target.rect = tight;
            target.alignment = SpriteAlignment.Center;
            target.pivot = new Vector2(0.5f, 0.5f);
            dp.SetSpriteRects(rects);
            dp.Apply();
            imp.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(x => x.name == sprite.name) ?? sprite;
        }

        /// <summary>팰 데이터의 앞·뒤 정지 그림을 다시 자르고(필요하면) 배율을 맞춘다. 다시 자른 그림 수를 돌려준다</summary>
        public static int TrimAndApply(PalData d)
        {
            if (d == null) return 0;
            int trimmed = 0;
            Rect? fr = d.frontSprite != null ? d.frontSprite.rect : (Rect?)null;
            Rect? br = d.backSprite != null ? d.backSprite.rect : (Rect?)null;
            d.frontSprite = Trim(d.frontSprite);
            d.backSprite = Trim(d.backSprite);
            if (fr.HasValue && d.frontSprite != null && d.frontSprite.rect != fr.Value) trimmed++;
            if (br.HasValue && d.backSprite != null && d.backSprite.rect != br.Value) trimmed++;
            if (trimmed > 0) EditorUtility.SetDirty(d);
            Apply(d);
            return trimmed;
        }

        [MenuItem("PalRogue/7-2. 팰 크기 자동 맞춤 (전체)")]
        public static void ApplyAll()
        {
            var lines = new List<string>();
            var trimmedNames = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:PalData", new[] { "Assets/Data/Pals" }))
            {
                var d = AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(guid));
                if (d == null) continue;
                float before = d.frontScale;
                if (TrimAndApply(d) > 0) trimmedNames.Add(d.palName);
                if (!Mathf.Approximately(before, d.frontScale)) lines.Add($"{d.palName} {before:0.00}→{d.frontScale:0.00}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[팰 크기] 옅은 테두리를 잘라낸 그림 {trimmedNames.Count}개" +
                      (trimmedNames.Count > 0 ? $" ({string.Join(", ", trimmedNames)})" : "") +
                      $", 크기를 다시 맞춘 팰 {lines.Count}종 (앞모습 배율): {string.Join(", ", lines)}");
        }
    }
}
#endif
