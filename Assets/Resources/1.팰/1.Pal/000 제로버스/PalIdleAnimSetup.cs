#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팰 대기 애니메이션 자동 설정 (팰로그)
/// 메뉴: Tools > PalRogue > ...
///  1) PNG 임포트 설정: Sprite (2D and UI), Point 필터, 무압축, 밉맵 끔
///  2) AnimationClip 생성: 15 FPS, Loop Time 켬
///  3) AnimatorController 생성: Idle 상태 하나, Speed 1
/// </summary>
public static class PalIdleAnimSetup
{
    // ===== 제로버스 기본값 =====
    const string ZerobusFolder = "Assets/Resources/1.팰/1.Pal/000 제로버스";
    const string ZerobusPrefix = "zerobus_idle_";

    // ===== 공통 설정 =====
    const float FPS = 15f;
    const float PixelsPerUnit = 100f;   // 다른 팰 스프라이트와 같은 값으로 맞추세요

    [MenuItem("Tools/PalRogue/Zerobus Idle 설정 (SpriteRenderer용)")]
    static void ZerobusSprite() => Setup(FindZerobusFolder(), ZerobusPrefix, false);

    [MenuItem("Tools/PalRogue/Zerobus Idle 설정 (UI Image용)")]
    static void ZerobusUI() => Setup(FindZerobusFolder(), ZerobusPrefix, true);

    // PNG를 어느 폴더에 넣었든 zerobus_idle_00.png 위치를 찾아서 사용
    static string FindZerobusFolder()
    {
        foreach (var guid in AssetDatabase.FindAssets("zerobus_idle_00"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith(".png")) return Path.GetDirectoryName(p).Replace('\\', '/');
        }
        return ZerobusFolder;
    }

    // Project 창에서 폴더를 선택하고 실행하면 그 폴더의 PNG 전부로 만들어 줌 (다른 팰에도 사용)
    [MenuItem("Assets/PalRogue/선택한 폴더로 Idle 애니메이션 만들기 (SpriteRenderer)")]
    static void SelectedSprite() { var f = SelectedFolder(); if (f != null) Setup(f, "", false); }

    [MenuItem("Assets/PalRogue/선택한 폴더로 Idle 애니메이션 만들기 (UI Image)")]
    static void SelectedUI() { var f = SelectedFolder(); if (f != null) Setup(f, "", true); }

    static string SelectedFolder()
    {
        var path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (AssetDatabase.IsValidFolder(path)) return path;
        EditorUtility.DisplayDialog("PalRogue", "Project 창에서 PNG가 들어있는 폴더를 선택하세요.", "확인");
        return null;
    }

    public static void Setup(string folder, string prefix, bool forUI)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            EditorUtility.DisplayDialog("PalRogue", $"폴더를 찾을 수 없어요:\n{folder}", "확인");
            return;
        }

        // 1) PNG 목록 (이름순 = 프레임 순서)
        var pngs = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => Path.GetFileName(p).StartsWith(prefix))
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .ToArray();
        if (pngs.Length == 0)
        {
            EditorUtility.DisplayDialog("PalRogue", $"PNG가 없어요: {folder}/{prefix}*.png", "확인");
            return;
        }

        // 2) 임포트 설정
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var p in pngs)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = PixelsPerUnit;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteAlignment = (int)SpriteAlignment.Center;
                s.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(s);
                ti.SaveAndReimport();
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();

        var sprites = pngs.Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).Where(s => s != null).ToArray();

        // 3) AnimationClip
        string baseName = string.IsNullOrEmpty(prefix) ? new DirectoryInfo(folder).Name + "_idle" : prefix.TrimEnd('_');
        string suffix = forUI ? "_UI" : "";
        string clipPath = $"{folder}/{baseName}{suffix}.anim";
        string ctrlPath = $"{folder}/{baseName}{suffix}.controller";

        var clip = new AnimationClip { frameRate = FPS, name = baseName };
        var binding = forUI
            ? EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite")
            : EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        // 마지막 프레임도 1/15초 동안 보이도록 끝에 키 하나 더 (루프가 끊기지 않음)
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / FPS, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / FPS, value = sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);

        // 4) AnimatorController (Idle 상태, Speed 1)
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = clip;
        idle.speed = 1f;
        sm.defaultState = idle;

        AssetDatabase.SaveAssets();

        // 5) 선택된 오브젝트가 있으면 바로 붙여줌
        var go = Selection.activeGameObject;
        if (go != null && go.scene.IsValid())
        {
            if (forUI)
            {
                var img = go.GetComponent<Image>();
                if (img == null) img = Undo.AddComponent<Image>(go);
                img.sprite = sprites[0]; img.preserveAspect = true; img.SetNativeSize();
            }
            else
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) sr = Undo.AddComponent<SpriteRenderer>(go);
                sr.sprite = sprites[0];
            }
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = Undo.AddComponent<Animator>(go);
            anim.runtimeAnimatorController = ctrl;
            anim.speed = 1f;
            EditorUtility.SetDirty(go);
        }

        EditorGUIUtility.PingObject(clip);
        Debug.Log($"[PalRogue] {sprites.Length}프레임, {FPS}fps, {sprites.Length / FPS:0.00}초 루프 생성 완료\n{clipPath}\n{ctrlPath}");
    }
}
#endif
