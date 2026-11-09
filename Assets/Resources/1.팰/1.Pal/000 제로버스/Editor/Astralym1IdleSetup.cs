using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Astralym1IdleSetup
{
    private const string Root = "Assets/Astralym1_Idle";
    private const string FramesFolder = Root + "/Frames";
    private const string ClipPath = Root + "/Astralym1_Idle.anim";
    private const float SampleRate = 8.49f; // 75 frames over the source's ~8.83 seconds.

    [MenuItem("Tools/Astralym1_Idle/Configure Sprites + Create Loop Clip")]
    public static void ConfigureAndCreate()
    {
        if (!AssetDatabase.IsValidFolder(FramesFolder))
        {
            EditorUtility.DisplayDialog("Astralym1_Idle", "Assets/Astralym1_Idle/Frames 폴더를 찾을 수 없습니다.\n패키지 폴더를 Assets 안에 먼저 복사해 주세요.", "확인");
            return;
        }

        string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { FramesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        if (paths.Length == 0)
        {
            EditorUtility.DisplayDialog("Astralym1_Idle", "Frames 폴더에서 PNG 프레임을 찾지 못했습니다.", "확인");
            return;
        }

        foreach (string path in paths)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();
        var sprites = new List<Sprite>();
        foreach (string path in paths)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) sprites.Add(sprite);
        }

        if (sprites.Count == 0)
        {
            EditorUtility.DisplayDialog("Astralym1_Idle", "스프라이트를 불러오지 못했습니다. Import 상태를 확인해 주세요.", "확인");
            return;
        }

        var clip = new AnimationClip { name = "Astralym1_Idle", frameRate = SampleRate };
        var keys = new List<ObjectReferenceKeyframe>(sprites.Count + 1);
        for (int i = 0; i < sprites.Count; i++)
        {
            keys.Add(new ObjectReferenceKeyframe { time = i / SampleRate, value = sprites[i] });
        }
        // Repeat frame 1 at the loop boundary to keep the clip length aligned with all 75 frame slots.
        keys.Add(new ObjectReferenceKeyframe { time = sprites.Count / SampleRate, value = sprites[0] });

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.wrapMode = WrapMode.Loop;

        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(clip);
        }
        else
        {
            AssetDatabase.CreateAsset(clip, ClipPath);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Astralym1_Idle", sprites.Count + "개 프레임의 Sprite 설정을 적용하고 루프 Animation Clip을 만들었습니다.\n\n" + ClipPath, "확인");
    }
}
