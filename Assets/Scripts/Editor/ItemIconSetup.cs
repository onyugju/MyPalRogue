using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 보상 아이템 이미지 준비.
    ///  - 아이템 목록(ItemCatalog)에 있는 이미지를 palrogue_assets_core/images/items 에서
    ///    Resources/2.아이템 으로 복사한다 (게임 중에 이름으로 불러오기 위해).
    ///  - 에디터가 켜지거나 스크립트가 바뀌면 빠진 이미지만 자동으로 복사한다. 메뉴 18로 직접 실행해도 된다.
    ///  - 2.아이템 폴더 이미지는 도트가 번지지 않게 Point 필터 / 무압축으로 가져온다.
    /// </summary>
    public class ItemIconSetup : AssetPostprocessor
    {
        const string SourceDir = "Assets/palrogue_assets_core/images/items";
        const string TargetDir = "Assets/Resources/2.아이템";

        [InitializeOnLoadMethod]
        static void AutoCopy() => EditorApplication.delayCall += () => CopyIcons(false);

        [MenuItem("PalRogue/18. 아이템 이미지 복사 (보상 화면)")]
        static void CopyFromMenu() => CopyIcons(true);

        static void CopyIcons(bool report)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder(SourceDir))
            {
                if (report) Debug.LogWarning($"[아이템 이미지] {SourceDir} 폴더가 없습니다.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(TargetDir)) AssetDatabase.CreateFolder("Assets/Resources", "2.아이템");

            int copied = 0;
            var missing = new System.Collections.Generic.List<string>();
            foreach (var id in ItemCatalog.IconIds.Distinct())
            {
                string to = $"{TargetDir}/{id}.png";
                if (File.Exists(to)) continue;
                string from = $"{SourceDir}/{id}.png";
                if (!File.Exists(from)) { missing.Add(id); continue; }
                if (AssetDatabase.CopyAsset(from, to)) copied++;
            }
            if (copied > 0) AssetDatabase.Refresh();
            if (copied > 0 || report)
                Debug.Log($"[아이템 이미지] {copied}개 복사 → {TargetDir}" + (missing.Count > 0 ? $"  (원본 없음: {string.Join(", ", missing)})" : ""));
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(TargetDir + "/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
