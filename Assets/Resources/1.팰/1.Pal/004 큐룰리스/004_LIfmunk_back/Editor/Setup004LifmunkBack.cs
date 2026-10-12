using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds a looping 15 FPS idle animation from the PNGs in Frames_back.
/// The root folder is detected from this script's location, so the whole
/// 004_LIfmunk_back folder can be moved within Assets without changing paths.
/// </summary>
public static class Setup004LifmunkBack
{
    private const float FramesPerSecond = 15f;

    [MenuItem("Tools/004_LIfmunk_back/Setup Idle Animation")]
    public static void SetupIdleAnimation()
    {
        string scriptGuid = AssetDatabase.FindAssets("Setup004LifmunkBack t:MonoScript").FirstOrDefault();
        if (string.IsNullOrEmpty(scriptGuid))
        {
            Debug.LogError("[004_LIfmunk_back] Editor 스크립트 위치를 찾을 수 없습니다.");
            return;
        }

        string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid).Replace("\\", "/");
        string editorFolder = Path.GetDirectoryName(scriptPath).Replace("\\", "/");
        string root = Path.GetDirectoryName(editorFolder).Replace("\\", "/");
        string framesFolder = root + "/Frames_back";

        if (!AssetDatabase.IsValidFolder(framesFolder))
        {
            Debug.LogError("[004_LIfmunk_back] Frames_back 폴더를 찾을 수 없습니다: " + framesFolder);
            return;
        }

        string[] texturePaths = Directory.GetFiles(framesFolder, "*.png")
            .Select(p => p.Replace("\\", "/"))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (texturePaths.Length == 0)
        {
            Debug.LogError("[004_LIfmunk_back] Frames_back 안에 PNG 프레임이 없습니다.");
            return;
        }

        // Set Unity import options that are appropriate for pixel-art sprites.
        foreach (string path in texturePaths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();
        Sprite[] sprites = texturePaths
            .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p))
            .Where(s => s != null)
            .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogError("[004_LIfmunk_back] PNG를 Sprite로 불러오지 못했습니다. Import Settings를 확인해 주세요.");
            return;
        }

        string clipPath = root + "/004_LIfmunk_back_Idle.anim";
        string controllerPath = root + "/004_LIfmunk_back.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        AnimationClip clip = new AnimationClip
        {
            name = "004_LIfmunk_back_Idle",
            frameRate = FramesPerSecond
        };

        // One key per image plus a closing key at exactly five seconds.
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / FramesPerSecond,
                value = sprites[i]
            };
        }
        keys[sprites.Length] = new ObjectReferenceKeyframe
        {
            time = sprites.Length / FramesPerSecond,
            value = sprites[0]
        };

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(
            string.Empty, typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        AssetDatabase.CreateAsset(clip, clipPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorState state = controller.layers[0].stateMachine.AddState("004_LIfmunk_back_Idle");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);

        // If a scene GameObject is selected, hook the animation up automatically.
        GameObject selected = Selection.activeGameObject;
        if (selected != null && !EditorUtility.IsPersistent(selected))
        {
            SpriteRenderer renderer = selected.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = selected.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites[0];

            Animator animator = selected.GetComponent<Animator>();
            if (animator == null) animator = selected.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(selected);
            Debug.Log("[004_LIfmunk_back] 애니메이션 설정 완료: " + selected.name);
        }
        else
        {
            Debug.Log("[004_LIfmunk_back] 애니메이션과 컨트롤러를 생성했습니다. 필요한 오브젝트의 Animator에 004_LIfmunk_back.controller를 연결하세요.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "004_LIfmunk_back 설정 완료",
            sprites.Length + "개 프레임으로 15 FPS 반복 애니메이션을 만들었습니다.\n\n" +
            "생성 파일:\n004_LIfmunk_back_Idle.anim\n004_LIfmunk_back.controller",
            "확인");
    }
}
