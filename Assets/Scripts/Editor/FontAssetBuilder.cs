#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/11: 한글이 들어 있는 TTF/OTF 폰트로 TMP 폰트 에셋을 새로 만들고,
    /// 현재 씬의 모든 TMP 텍스트에 적용한다.
    ///
    /// 사용법
    ///  1) 한글이 되는 폰트 파일(.ttf/.otf)을 프로젝트에 넣는다 (예: Assets/palrogue_assets_core/fonts/pokemon-emerald-pro.ttf)
    ///  2) Project 창에서 그 폰트 파일을 클릭해서 선택한 채로 이 메뉴를 실행한다
    ///     (선택하지 않으면 fonts 폴더에서 이름에 'pokemon-emerald-pro'가 들어간 폰트를 찾는다)
    ///
    /// 글자는 "필요할 때 하나씩 추가되는 방식(Dynamic)"이라, 처음 쓰는 글자는 그때 만들어진다.
    /// </summary>
    public static class FontAssetBuilder
    {
        const string FontDir = "Assets/palrogue_assets_core/fonts";
        const string PreferredName = "pokemon-emerald-pro";
        const string OutName = "pokemon-emerald-pro KR SDF";

        [MenuItem("PalRogue/11. 한글 폰트 에셋 만들기", false, 1011)]
        static void Build()
        {
            var font = PickFont();
            if (font == null)
            {
                EditorUtility.DisplayDialog("폰트 파일이 필요해요",
                    "한글이 들어 있는 폰트 파일(.ttf/.otf)을 프로젝트에 넣고,\nProject 창에서 그 파일을 클릭한 뒤 이 메뉴를 다시 실행해 주세요.", "확인");
                return;
            }
            if (!font.HasCharacter('어') || !font.HasCharacter('싸'))
            {
                EditorUtility.DisplayDialog("한글이 없는 폰트예요",
                    $"'{font.name}' 폰트에는 한글 글자가 들어 있지 않아요.\n한글이 되는 다른 폰트 파일을 선택해 주세요.", "확인");
                return;
            }

            string outPath = $"{FontDir}/{OutName}.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath) != null) AssetDatabase.DeleteAsset(outPath);

            var fa = TMP_FontAsset.CreateFontAsset(font, 32, 5, GlyphRenderMode.SDFAA, 1024, 1024,
                                                   AtlasPopulationMode.Dynamic, true);
            if (fa == null)
            {
                Debug.LogError("[PalRogue] 폰트 에셋을 만들지 못했습니다. 선택한 폰트 파일을 확인해 주세요.");
                return;
            }

            fa.name = OutName;
            AssetDatabase.CreateAsset(fa, outPath);
            // 글자 이미지(아틀라스)와 머티리얼은 폰트 에셋 안에 함께 저장해야 한다
            if (fa.atlasTexture != null) { fa.atlasTexture.name = OutName + " Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa); }
            if (fa.material != null) { fa.material.name = OutName + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();

            // 현재 씬의 모든 TMP 텍스트(꺼져 있는 것 포함)에 새 폰트 적용
            int n = 0;
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                t.font = fa;
                EditorUtility.SetDirty(t);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[PalRogue] 한글 폰트 에셋 생성 완료: {outPath} (텍스트 {n}개에 적용)");
        }

        static Font PickFont()
        {
            // 1) Project 창에서 고른 폰트 파일
            if (Selection.activeObject is Font selected) return selected;

            // 2) fonts 폴더에서 이름으로 찾기
            if (!AssetDatabase.IsValidFolder(FontDir)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { FontDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.ToLowerInvariant().Contains(PreferredName)) continue;
                var f = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (f != null) return f;
            }
            return null;
        }
    }
}
#endif
