using System.IO;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 지역 배경 그림 준비.
    ///  - 지역마다 쓰는 포켓로그 배경(GameConfig.AreaBiomes)의 그림을 palrogue_assets_core/images/arenas 에서
    ///    Resources/Arenas 로 복사한다 (게임 중에 이름으로 불러오기 위해).
    ///    {biome}_bg, _a, _b, _b_1~3 (장식은 있는 것만)
    ///  - 에디터가 켜지거나 스크립트가 바뀌면 빠진 그림만 자동으로 복사한다.
    ///  - Arenas 폴더 그림은 도트가 번지지 않게 Point 필터 / 무압축으로 가져온다.
    /// </summary>
    public class ArenaSetup : AssetPostprocessor
    {
        const string SourceDir = "Assets/palrogue_assets_core/images/arenas";
        const string TargetDir = "Assets/Resources/" + AreaScenery.Folder;

        [InitializeOnLoadMethod]
        static void AutoCopy() => EditorApplication.delayCall += () => Copy(false);

        static void Copy(bool report)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder(SourceDir))
            {
                if (report) Debug.LogWarning($"[지역 배경] {SourceDir} 폴더가 없습니다.");
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(TargetDir)) AssetDatabase.CreateFolder("Assets/Resources", AreaScenery.Folder);

            int copied = 0;
            foreach (var biome in GameConfig.AreaBiomes)
                foreach (var part in new[] { "bg", "a", "b", "b_1", "b_2", "b_3" })
                {
                    string to = $"{TargetDir}/{biome}_{part}.png";
                    if (File.Exists(to)) continue;
                    string from = $"{SourceDir}/{biome}_{part}.png";
                    if (!File.Exists(from)) continue;               // 장식이 없는 배경도 있다
                    if (AssetDatabase.CopyAsset(from, to)) copied++;
                }
            if (copied > 0) AssetDatabase.Refresh();
            if (copied > 0 || report) Debug.Log($"[지역 배경] 그림 {copied}개 복사 → {TargetDir}");
        }

        [MenuItem("PalRogue/24-1. 지역 배경 그림 다시 복사")]
        static void CopyFromMenu() => Copy(true);

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(TargetDir + "/")) return;
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
