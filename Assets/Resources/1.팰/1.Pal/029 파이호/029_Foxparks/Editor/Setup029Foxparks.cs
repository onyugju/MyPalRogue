using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class Setup029Foxparks
{
    private const string Root = "Assets/Resources/1.팰/1.Pal/029 파이호/029_Foxparks";
    private const string FramesFolder = Root + "/Frames";
    private const string ClipPath = Root + "/029_Foxparks_Idle.anim";
    private const string ControllerPath = Root + "/029_Foxparks.controller";
    private const float FramesPerSecond = 15f;

    [MenuItem("Tools/029_Foxparks/Setup Idle Animation")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder(FramesFolder))
        {
            EditorUtility.DisplayDialog("029_Foxparks", "폴더를 Assets/029_Foxparks/Frames 위치에 넣어 주세요.", "확인");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { FramesFolder });
        string[] paths = guids.Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToArray();
        if (paths.Length == 0)
        {
            EditorUtility.DisplayDialog("029_Foxparks", "Frames 폴더에서 PNG 프레임을 찾지 못했어요.", "확인");
            return;
        }

        foreach (string path in paths)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        AssetDatabase.Refresh();

        Sprite[] sprites = paths.Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).Where(s => s != null).ToArray();
        if (sprites.Length == 0)
        {
            EditorUtility.DisplayDialog("029_Foxparks", "Sprite 변환에 실패했어요. Unity Console을 확인해 주세요.", "확인");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath) != null) AssetDatabase.DeleteAsset(ClipPath);
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
        var clip = new AnimationClip { name = "029_Foxparks_Idle", frameRate = FramesPerSecond, wrapMode = WrapMode.Loop };
        var keys = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / FramesPerSecond, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        AssetDatabase.CreateAsset(clip, ClipPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var state = controller.layers[0].stateMachine.AddState("029_Foxparks_Idle");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);

        GameObject selected = Selection.activeGameObject;
        bool assigned = false;
        if (selected != null && selected.GetComponent<SpriteRenderer>() != null)
        {
            var animator = selected.GetComponent<Animator>();
            if (animator == null) animator = selected.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            assigned = true;
        }

        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(clip);
        string message = "완료! " + sprites.Length + "개 프레임, 15 FPS 반복 애니메이션을 만들었어요.";
        if (!assigned) message += "\nAnimator Controller도 만들었어요. SpriteRenderer가 있는 캐릭터 오브젝트의 Animator에 이 Controller를 넣어 주세요.";
        EditorUtility.DisplayDialog("029_Foxparks", message, "확인");
    }
}
