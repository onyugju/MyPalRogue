#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 메뉴: Tools > Astralym_Phase2 > Setup
// 1) Frames 폴더의 PNG를 전부 Sprite (2D and UI) / Single 로 설정
// 2) Astralym_Phase2_Idle.anim 생성 (12 FPS, Loop, 왕복 재생이라 이음매 없음)
public static class Astralym_Phase2_Setup
{
    const string Name = "Astralym_Phase2";
    const float Fps = 12f;          // 원본 속도. 더 빠르게 하려면 15
    const bool PingPong = true;     // false 로 바꾸면 000→105 한 방향 반복

    [MenuItem("Tools/Astralym_Phase2/Setup")]
    public static void Setup()
    {
        string framesDir = AssetDatabase.FindAssets(Name + "_000 t:Texture2D")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(p => Path.GetDirectoryName(p).Replace('\\', '/'))
            .FirstOrDefault();
        if (framesDir == null)
        {
            Debug.LogError(Name + ": Frames 폴더를 찾지 못했습니다.");
            return;
        }

        string[] paths = Directory.GetFiles(framesDir, Name + "_*.png")
            .Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();

        foreach (string p in paths)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(p);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 1024;
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            imp.SetTextureSettings(s);
            imp.SaveAndReimport();
        }

        List<Sprite> sprites = paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(s => s != null).ToList();
        List<Sprite> order = new List<Sprite>(sprites);
        if (PingPong)
            for (int i = sprites.Count - 2; i >= 1; i--) order.Add(sprites[i]);

        var keys = new ObjectReferenceKeyframe[order.Count + 1];
        for (int i = 0; i < order.Count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = order[i] };
        // 마지막 프레임 길이를 지키기 위한 끝 키
        keys[order.Count] = new ObjectReferenceKeyframe { time = order.Count / Fps, value = order[0] };

        var clip = new AnimationClip { frameRate = Fps };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var cs = AnimationUtility.GetAnimationClipSettings(clip);
        cs.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, cs);

        string clipPath = Path.GetDirectoryName(framesDir).Replace('\\', '/') + "/" + Name + "_Idle.anim";
        AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);
        AssetDatabase.SaveAssets();
        Debug.Log(Name + ": 스프라이트 " + sprites.Count + "장 설정, 클립 생성 → " + clipPath);
    }
}
#endif
