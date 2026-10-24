using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Imports the 004_LIfmunk PNG frames as transparent sprites, creates a looping
/// AnimationClip and Animator Controller, and saves a ready-to-use prefab.
/// Put this file at Assets/004_LIfmunk/Editor/004_LIfmunkSetup.cs.
/// </summary>
public static class LIfmunk004Setup
{
    private const string Root = "Assets/Resources/1.팰/1.Pal/004 큐룰리스/004_LIfmunk";
    private const string FramesFolder = Root + "/Frames";
    private const string AnimationsFolder = Root + "/Animations";
    private const string PrefabsFolder = Root + "/Prefabs";
    private const string ClipPath = AnimationsFolder + "/004_LIfmunk_Idle.anim";
    private const string ControllerPath = AnimationsFolder + "/004_LIfmunk.controller";
    private const string PrefabPath = PrefabsFolder + "/004_LIfmunk.prefab";
    private const float SampleRate = 7.5f;
    private const float PixelsPerUnit = 100f;

    [MenuItem("Tools/004_LIfmunk/Setup Sprites and Create Idle Animation")]
    public static void Setup()
    {
        if (!Directory.Exists(FramesFolder))
        {
            EditorUtility.DisplayDialog(
                "004_LIfmunk Setup",
                "Frames 폴더를 찾을 수 없습니다. 004_LIfmunk 폴더가 Assets 바로 아래에 있는지 확인하세요.",
                "확인");
            return;
        }

        Directory.CreateDirectory(AnimationsFolder);
        Directory.CreateDirectory(PrefabsFolder);

        string[] framePaths = Directory.GetFiles(FramesFolder, "*.png")
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .Select(path => path.Replace('\\', '/'))
            .ToArray();

        if (framePaths.Length == 0)
        {
            EditorUtility.DisplayDialog("004_LIfmunk Setup", "Frames 폴더에 PNG 프레임이 없습니다.", "확인");
            return;
        }

        // Import every frame as a transparent, uncompressed Sprite.
        foreach (string assetPath in framePaths)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();

        Sprite[] sprites = framePaths
            .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path))
            .Where(sprite => sprite != null)
            .ToArray();

        if (sprites.Length == 0)
        {
            EditorUtility.DisplayDialog("004_LIfmunk Setup", "Sprite를 불러오지 못했습니다. Unity Console을 확인하세요.", "확인");
            return;
        }

        // Build a looping sprite-sheet animation clip.
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath) != null)
            AssetDatabase.DeleteAsset(ClipPath);

        AnimationClip clip = new AnimationClip
        {
            name = "004_LIfmunk_Idle",
            frameRate = SampleRate,
            wrapMode = WrapMode.Loop
        };

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(
            string.Empty,
            typeof(SpriteRenderer),
            "m_Sprite");

        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / SampleRate,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.stopTime = sprites.Length / SampleRate;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, ClipPath);

        // Build a controller with Idle as the default state.
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(250f, 100f, 0f));
        idleState.motion = clip;
        stateMachine.defaultState = idleState;

        // Save a prefab with SpriteRenderer + Animator for quick scene placement.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            AssetDatabase.DeleteAsset(PrefabPath);

        GameObject character = new GameObject("004_LIfmunk");
        SpriteRenderer spriteRenderer = character.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprites[0];
        Animator animator = character.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        PrefabUtility.SaveAsPrefabAsset(character, PrefabPath);
        UnityEngine.Object.DestroyImmediate(character);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "004_LIfmunk Setup Complete",
            string.Format(
                "완료!\n프레임: {0}장\n애니메이션: {1}\nAnimator Controller와 Prefab도 생성했습니다.",
                sprites.Length,
                ClipPath),
            "확인");
    }
}
