using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class Setup029FoxparksBack
{
    private const string RootFolderName = "029_Foxparks_back";
    private const string FramesFolderName = "Frames_back";
    private const string ClipFileName = "029_Foxparks_back_Idle.anim";
    private const string ControllerFileName = "029_Foxparks_back.controller";
    private const float FramesPerSecond = 15f;

    [MenuItem("Tools/029_Foxparks_back/Setup Idle Animation")]
    public static void Setup()
    {
        string framesFolder = FindFramesFolder();
        if (string.IsNullOrEmpty(framesFolder))
        {
            EditorUtility.DisplayDialog(
                RootFolderName,
                "Frames_back 폴더를 찾지 못했어요. Assets 폴더 안에 029_Foxparks_back/Frames_back 폴더가 있는지 확인해 주세요.",
                "확인");
            return;
        }

        string root = framesFolder.Substring(0, framesFolder.Length - ("/" + FramesFolderName).Length);
        string clipPath = root + "/" + ClipFileName;
        string controllerPath = root + "/" + ControllerFileName;

        string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { framesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal)
            .ToArray();

        if (paths.Length == 0)
        {
            EditorUtility.DisplayDialog(RootFolderName, "Frames_back 폴더에서 PNG 프레임을 찾지 못했어요.", "확인");
            return;
        }

        // 픽셀 아트의 선명도를 유지하고 투명도를 활용하도록 PNG 임포트 설정
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

        Sprite[] sprites = paths
            .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p))
            .Where(s => s != null)
            .ToArray();

        if (sprites.Length == 0)
        {
            EditorUtility.DisplayDialog(RootFolderName, "Sprite 변환에 실패했어요. Unity Console을 확인해 주세요.", "확인");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        var clip = new AnimationClip
        {
            name = "029_Foxparks_back_Idle",
            frameRate = FramesPerSecond,
            wrapMode = WrapMode.Loop
        };

        var keys = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / FramesPerSecond,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(
            clip,
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
            keys);

        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        AssetDatabase.CreateAsset(clip, clipPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var state = controller.layers[0].stateMachine.AddState("029_Foxparks_back_Idle");
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

        string message = "완료! " + sprites.Length + "개 프레임, 15 FPS 반복 애니메이션을 만들었어요.\n\n생성 위치: " + root;
        if (!assigned)
            message += "\n\nAnimator Controller도 만들었어요. SpriteRenderer가 있는 캐릭터 오브젝트를 선택한 뒤 Animator에 이 Controller를 넣어 주세요.";
        EditorUtility.DisplayDialog(RootFolderName, message, "확인");
    }

    // 폴더가 Assets 바로 아래 있지 않고 Resources/펫 폴더 안에 있어도 찾을 수 있게 처리
    private static string FindFramesFolder()
    {
        string directPath = "Assets/" + RootFolderName + "/" + FramesFolderName;
        if (AssetDatabase.IsValidFolder(directPath)) return directPath;

        string[] candidates = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                     && p.EndsWith("/" + RootFolderName + "/" + FramesFolderName, StringComparison.Ordinal)
                     && AssetDatabase.IsValidFolder(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        if (candidates.Length > 0) return candidates[0];

        // Unity의 에셋 목록에 폴더 경로가 바로 잡히지 않는 경우를 위한 보조 검색
        string assetsAbsolute = Application.dataPath;
        if (!Directory.Exists(assetsAbsolute)) return null;

        string[] found = Directory.GetDirectories(assetsAbsolute, FramesFolderName, SearchOption.AllDirectories)
            .Where(p => Directory.GetParent(p) != null && Directory.GetParent(p).Name == RootFolderName)
            .ToArray();
        if (found.Length == 0) return null;

        string absolute = found[0].Replace('\\', '/');
        string dataPath = assetsAbsolute.Replace('\\', '/');
        return "Assets" + absolute.Substring(dataPath.Length);
    }
}
