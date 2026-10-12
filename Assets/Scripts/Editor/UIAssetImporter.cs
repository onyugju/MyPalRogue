#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/8: 포켓로그 UI 이미지의 임포트 설정을 도트 게임용으로 바꾼다.
    ///  - Sprite(Single), Point 필터, 무압축  (도트가 번지지 않게)
    ///  - arenas 는 PPU 18  (월드 1유닛 = 화면 18픽셀로 맞추기 위해)
    ///  - windows 는 9-slice 테두리 지정
    ///  - 고해상도 속성 아이콘(2.Elements)은 밉맵 + Trilinear (작게 줄여도 깨지지 않게)
    /// </summary>
    public static class UIAssetImporter
    {
        const string Core = "Assets/palrogue_assets_core/images";

        // 9-slice 테두리 두께(픽셀). 가장자리가 어색하면 Sprite Editor에서 직접 조절.
        const int WindowBorder = 8, ThinBorder = 6, XThinBorder = 4;

        [MenuItem("PalRogue/8. UI 이미지 임포트 설정 적용")]
        static void Run()
        {
            int n = 0;
            n += Apply($"{Core}/ui", 100f, false);
            n += Apply($"{Core}/pokeball", 100f, false);
            n += Apply($"{Core}/arenas", 18f, false);
            n += Apply("Assets/Resources/1.팰/2.Elements", 100f, true);
            Debug.Log($"[PalRogue] UI 이미지 {n}개의 임포트 설정을 바꿨습니다.");
        }

        static int Apply(string folder, float ppu, bool smooth)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[PalRogue] 폴더를 찾을 수 없음: {folder}");
                return 0;
            }

            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png")) continue;
                if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;

                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = ppu;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = smooth;
                imp.filterMode = smooth ? FilterMode.Trilinear : FilterMode.Point;

                if (path.Contains("/windows/"))
                {
                    int b = path.Contains("xthin") ? XThinBorder : path.Contains("thin") ? ThinBorder : WindowBorder;
                    imp.spriteBorder = new Vector4(b, b, b, b);
                }

                imp.SaveAndReimport();
                n++;
            }
            return n;
        }
    }
}
#endif
