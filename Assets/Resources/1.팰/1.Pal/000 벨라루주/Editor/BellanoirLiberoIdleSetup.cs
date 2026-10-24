#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 벨라루주 리베로 대기 애니메이션 자동 설정 (앞/뒤)
/// 폴더 구조 (어디에 두든 자동으로 찾음):
///   000_Bellanoir Libero/Frames/bellanoir_libero_idle_00~74.png            (앞)
///   000_Bellanoir Libero_back/Frames_back/bellanoir_libero_idle_back_00~74.png (뒤)
/// 실행하면 각 폴더(Frames의 상위 폴더)에 .anim / .controller 생성
///   - PNG: Sprite (2D and UI), Point 필터, 무압축, 밉맵 끔
///   - Clip: 15 FPS, Loop Time 켬 (마지막 프레임도 1/15초 유지 -> 끊김 없는 루프)
///   - Controller: Idle 상태 1개, Speed 1
/// </summary>
public static class BellanoirLiberoIdleSetup
{
    const float FPS = 15f;
    const float PixelsPerUnit = 100f;   // 다른 팰과 크기를 맞출 때 이 값만 바꾸세요

    const string FrontPrefix = "bellanoir_libero_idle_";
    const string BackPrefix = "bellanoir_libero_idle_back_";
    const string FrontFallback = "Assets/Resources/1.팰/1.Pal/000 벨라루주/000_Bellanoir Libero/Frames";
    const string BackFallback = "Assets/Resources/1.팰/1.Pal/000 벨라루주/000_Bellanoir Libero_back/Frames_back";

    // ---------- 메뉴 ----------
    [MenuItem("Tools/PalRogue/Bellanoir Libero/앞+뒤 한 번에 설정")]
    static void Both()
    {
        Setup(false, false, false);
        Setup(true, false, false);
        Debug.Log("[PalRogue] 벨라루주 리베로 앞/뒤 애니메이션 생성 완료");
    }

    [MenuItem("Tools/PalRogue/Bellanoir Libero/앞 (SpriteRenderer)")]
    static void FrontSprite() => Setup(false, false, true);

    [MenuItem("Tools/PalRogue/Bellanoir Libero/앞 (UI Image)")]
    static void FrontUI() => Setup(false, true, true);

    [MenuItem("Tools/PalRogue/Bellanoir Libero/뒤 (SpriteRenderer)")]
    static void BackSprite() => Setup(true, false, true);

    [MenuItem("Tools/PalRogue/Bellanoir Libero/뒤 (UI Image)")]
    static void BackUI() => Setup(true, true, true);

    // ---------- 구현 ----------
    static string FindFramesFolder(bool back)
    {
        string prefix = back ? BackPrefix : FrontPrefix;
        foreach (var guid in AssetDatabase.FindAssets(prefix + "00"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.EndsWith(".png")) continue;
            var name = Path.GetFileNameWithoutExtension(p);
            // 앞 프레임 검색 시 _back 파일이 섞이지 않도록
            if (!back && name.StartsWith(BackPrefix)) continue;
            return Path.GetDirectoryName(p).Replace('\\', '/');
        }
        return back ? BackFallback : FrontFallback;
    }

    static void Setup(bool back, bool forUI, bool attachToSelection)
    {
        string prefix = back ? BackPrefix : FrontPrefix;
        string frames = FindFramesFolder(back);
        if (!AssetDatabase.IsValidFolder(frames))
        {
            EditorUtility.DisplayDialog("PalRogue", $"프레임 폴더를 찾을 수 없어요:\n{frames}\n\nPNG를 프로젝트에 먼저 넣어주세요.", "확인");
            return;
        }

        var pngs = Directory.GetFiles(frames, "*.png", SearchOption.TopDirectoryOnly)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => Path.GetFileName(p).StartsWith(prefix))
            .Where(p => back || !Path.GetFileName(p).StartsWith(BackPrefix))
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .ToArray();
        if (pngs.Length == 0)
        {
            EditorUtility.DisplayDialog("PalRogue", $"PNG가 없어요: {frames}/{prefix}*.png", "확인");
            return;
        }

        // 1) 임포트 설정
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

        // 2) 클립/컨트롤러는 Frames 폴더의 상위(000_Bellanoir Libero / _back)에 생성
        string root = Path.GetDirectoryName(frames).Replace('\\', '/');
        string baseName = prefix.TrimEnd('_') + (forUI ? "_UI" : "");
        string clipPath = $"{root}/{baseName}.anim";
        string ctrlPath = $"{root}/{baseName}.controller";

        var clip = new AnimationClip { frameRate = FPS, name = baseName };
        var binding = forUI
            ? EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite")
            : EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
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

        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = clip;
        idle.speed = 1f;
        sm.defaultState = idle;
        AssetDatabase.SaveAssets();

        // 3) 선택된 오브젝트에 바로 연결 (단일 메뉴 실행 시)
        var go = attachToSelection ? Selection.activeGameObject : null;
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
        Debug.Log($"[PalRogue] {(back ? "뒤" : "앞")} {sprites.Length}프레임, {FPS}fps, {sprites.Length / FPS:0.00}초 루프\n{clipPath}\n{ctrlPath}");
    }
}
#endif
