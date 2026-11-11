#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/82. 빌드 용량 줄이기 (이미지 압축)
    /// 빌드에 들어가는 그림은 지금 압축 없이(한 화소 4바이트) 들어가서 빌드가 1.8GB나 된다.
    ///  - 윈도우(Standalone) 빌드에서만 BC7로 압축한다 (화소당 1바이트 → 약 1/4). 도트 그림도 거의 티가 안 나는 고화질 압축.
    ///    원본 PNG·에디터 화면은 그대로이고, 그림 크기(화소 수)도 그대로라 화면 배치·크기는 바뀌지 않는다.
    ///  - 가로·세로가 4의 배수가 아닌 그림은 BC7로 압축할 수 없어서 그대로 둔다.
    ///  - 코드가 화소를 직접 읽는 그림(Read/Write 켜짐)도 그대로 둔다.
    ///  - 속성 뱃지(2.Elements)는 화면에 아주 작게 나오는데 원본이 1659×948이라 최대 512로 줄인다 (코드는 비율로만 쓴다).
    ///  - 스프라이트의 밉맵은 끈다 (2D 화면에서는 쓰지 않는데 용량만 1/3 더 든다).
    /// 결과는 프로젝트 폴더의 SizeReport.txt 에 적는다. 되돌리려면 메뉴 83.
    /// </summary>
    public static class BuildSizeOptimizer
    {
        static readonly string[] Roots = { "Assets/Resources", "Assets/palrogue_assets_core" };
        const string Platform = "Standalone";
        const string BadgeDir = "Assets/Resources/1.팰/2.Elements/";
        const int BadgeMax = 512;

        [MenuItem("PalRogue/82. 빌드 용량 줄이기 (이미지 압축)", false, 802)]
        public static void Optimize()
        {
            var paths = AssetDatabase.FindAssets("t:Texture2D", Roots.Where(AssetDatabase.IsValidFolder).ToArray())
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            int compressed = 0, skippedOdd = 0, skippedReadable = 0, already = 0, badges = 0, mips = 0;
            long before = 0, after = 0;
            var odd = new List<string>();
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    if (i % 25 == 0) EditorUtility.DisplayProgressBar("이미지 압축 설정", path, i / (float)paths.Count);
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
                    imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                    bool changed = false;

                    int max = path.StartsWith(BadgeDir) ? BadgeMax : Mathf.Max(imp.maxTextureSize, 2048);
                    float scale = Mathf.Min(1f, max / (float)Mathf.Max(w, h));
                    int ew = Mathf.Max(1, Mathf.RoundToInt(w * scale)), eh = Mathf.Max(1, Mathf.RoundToInt(h * scale));
                    before += (long)w * h * 4;

                    if (imp.textureType == TextureImporterType.Sprite && imp.mipmapEnabled) { imp.mipmapEnabled = false; changed = true; mips++; }

                    var ps = imp.GetPlatformTextureSettings(Platform);
                    var want = TextureImporterFormat.BC7;
                    bool canCompress = !imp.isReadable && ew % 4 == 0 && eh % 4 == 0;
                    if (imp.isReadable) skippedReadable++;
                    else if (!canCompress) { skippedOdd++; if (odd.Count < 40) odd.Add($"{path} ({ew}x{eh})"); }

                    var fmt = canCompress ? want : TextureImporterFormat.RGBA32;
                    if (!ps.overridden || ps.maxTextureSize != max || ps.format != fmt)
                    {
                        ps.overridden = true;
                        ps.maxTextureSize = max;
                        ps.format = fmt;
                        ps.compressionQuality = 100;
                        imp.SetPlatformTextureSettings(ps);
                        changed = true;
                        if (canCompress) compressed++;
                        if (path.StartsWith(BadgeDir) && scale < 1f) badges++;
                    }
                    else if (canCompress) already++;

                    after += canCompress ? (long)ew * eh : (long)ew * eh * 4;
                    if (changed) imp.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[빌드 용량 줄이기] 그림 {paths.Count}장 검사");
            sb.AppendLine($"  BC7로 새로 압축 {compressed}장 (이미 압축 {already}장), 뱃지 512로 줄임 {badges}장, 밉맵 끔 {mips}장");
            sb.AppendLine($"  그대로 둔 그림: 4의 배수가 아님 {skippedOdd}장, 코드가 화소를 읽음 {skippedReadable}장");
            sb.AppendLine($"  예상 그림 용량: {before / 1048576f:0} MB → {after / 1048576f:0} MB");
            if (odd.Count > 0) { sb.AppendLine("  4의 배수가 아니라 압축 못 한 그림 (앞 40개):"); foreach (var o in odd) sb.AppendLine("    " + o); }
            File.WriteAllText(Path.GetFullPath("SizeReport.txt"), sb.ToString());
            Debug.Log(sb.ToString());
        }

        const string PalRoot = "Assets/Resources/1.팰/1.Pal";
        const string PadBackupDir = "../PalRogue_FrameBackup/pad4";

        /// <summary>
        /// 팰 그림(애니메이션 프레임·정지 그림) 중 가로·세로가 4의 배수가 아닌 것을 오른쪽·위쪽에 투명한 줄 1~3개를 붙여 4의 배수로 만든다.
        /// 그래야 BC7 압축이 된다. 기준점(피벗)은 원래 그림의 같은 화소에 그대로 맞춰서 화면에서 보이는 위치·크기는 바뀌지 않는다.
        /// 원본은 프로젝트 폴더 옆 PalRogue_FrameBackup/pad4 에 보관한다. 스프라이트(한 장·여러 조각)만 바꾼다.
        /// </summary>
        [MenuItem("PalRogue/84. 팰 그림 크기를 4의 배수로 맞추기 (압축용)", false, 804)]
        public static void PadPalImages()
        {
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { PalRoot }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
            int padded = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    if (i % 25 == 0) EditorUtility.DisplayProgressBar("4의 배수로 맞추기", path, i / (float)paths.Count);
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter imp) || imp.textureType != TextureImporterType.Sprite
                        || imp.spriteImportMode == SpriteImportMode.None) continue;
                    bool single = imp.spriteImportMode == SpriteImportMode.Single;   // 여러 조각(Multiple)은 왼쪽 아래 기준 좌표라 오른쪽·위에 붙여도 그대로
                    var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!src.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(src); continue; }
                    int w = src.width, h = src.height, nw = (w + 3) / 4 * 4, nh = (h + 3) / 4 * 4;
                    if (nw == w && nh == h) { UnityEngine.Object.DestroyImmediate(src); continue; }

                    // 원본 보관
                    string backup = Path.Combine(PadBackupDir, path);
                    if (!File.Exists(backup)) { Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(path, backup); }

                    // 왼쪽 아래(0,0)는 그대로 두고 오른쪽·위쪽에 투명 줄을 붙인다
                    var dst = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
                    var clear = new Color32[nw * nh];
                    dst.SetPixels32(clear);
                    dst.SetPixels32(0, 0, w, h, src.GetPixels32());
                    dst.Apply();
                    File.WriteAllBytes(path, dst.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(src);
                    UnityEngine.Object.DestroyImmediate(dst);

                    // 기준점: 원래 그림에서 가리키던 화소를 그대로 가리키게 (한 장짜리만)
                    if (!single) { imp.SaveAndReimport(); padded++; continue; }
                    var ts = new TextureImporterSettings();
                    imp.ReadTextureSettings(ts);
                    Vector2 pv = PivotOf((SpriteAlignment)ts.spriteAlignment, ts.spritePivot);
                    ts.spriteAlignment = (int)SpriteAlignment.Custom;
                    ts.spritePivot = new Vector2(pv.x * w / nw, pv.y * h / nh);
                    imp.SetTextureSettings(ts);
                    imp.SaveAndReimport();
                    padded++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }
            Debug.Log($"[빌드 용량 줄이기] 팰 그림 {padded}장을 4의 배수 크기로 맞췄습니다 (원본: PalRogue_FrameBackup/pad4)");
        }

        static Vector2 PivotOf(SpriteAlignment a, Vector2 custom)
        {
            switch (a)
            {
                case SpriteAlignment.Center: return new Vector2(0.5f, 0.5f);
                case SpriteAlignment.TopLeft: return new Vector2(0f, 1f);
                case SpriteAlignment.TopCenter: return new Vector2(0.5f, 1f);
                case SpriteAlignment.TopRight: return new Vector2(1f, 1f);
                case SpriteAlignment.LeftCenter: return new Vector2(0f, 0.5f);
                case SpriteAlignment.RightCenter: return new Vector2(1f, 0.5f);
                case SpriteAlignment.BottomLeft: return new Vector2(0f, 0f);
                case SpriteAlignment.BottomCenter: return new Vector2(0.5f, 0f);
                case SpriteAlignment.BottomRight: return new Vector2(1f, 0f);
                default: return custom;
            }
        }

        [MenuItem("PalRogue/83. 이미지 압축 되돌리기", false, 803)]
        public static void Revert()
        {
            var paths = AssetDatabase.FindAssets("t:Texture2D", Roots.Where(AssetDatabase.IsValidFolder).ToArray())
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            int n = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var path in paths)
                {
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
                    var ps = imp.GetPlatformTextureSettings(Platform);
                    if (!ps.overridden) continue;
                    ps.overridden = false;
                    imp.SetPlatformTextureSettings(ps);
                    imp.SaveAndReimport();
                    n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log($"[빌드 용량 줄이기] 압축 설정을 {n}장 되돌렸습니다.");
        }
    }
}
#endif
