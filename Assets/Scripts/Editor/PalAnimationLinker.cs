#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/7-1. 팰 애니메이션 자동 연결
    /// 팰 폴더(Resources/1.팰/1.Pal/"번호 이름")에 새로 만든 애니메이션(Animator Controller)을 찾아서 PalData에 연결한다.
    ///  - 이름이 _back 으로 끝나면 뒷모습(내 팰), 아니면 앞모습(상대 팰). 폴더 안 어디에 있어도(Animations 하위 폴더 등) 찾는다.
    ///  - 애니메이션의 첫 프레임을 대표 그림으로, 그 크기로 화면 배율을 다시 맞춘다 (메뉴 2와 같은 기준).
    ///  - 모든 팰 애니메이션을 15 FPS로 맞춘다 (75장 = 5초 반복). 다른 FPS로 만들어졌으면 프레임 순서는 그대로 두고 시간만 고친다.
    /// 이미 연결된 애니메이션은 건드리지 않는다 (직접 조절한 배율 보호).
    /// 에디터가 켜지거나 스크립트가 바뀌면 자동으로 한 번 실행된다.
    /// </summary>
    public static class PalAnimationLinker
    {
        const string PalRoot = "Assets/Resources/1.팰/1.Pal";
        const string PalDir = "Assets/Data/Pals";
        const float FrontBox = 3f, BackBox = 4f;     // PalDataBuilder와 같은 값
        public const float Fps = 15f;

        [InitializeOnLoadMethod]
        static void Auto() => EditorApplication.delayCall += () => Run(false);

        [MenuItem("PalRogue/7-1. 팰 애니메이션 자동 연결")]
        static void FromMenu() => Run(true);

        public static void Run(bool report)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.IsValidFolder(PalRoot)) return;
            var log = new List<string>();
            int retimed = 0;
            int built = BuildMissingControllers(log);

            foreach (var guid in AssetDatabase.FindAssets("t:PalData", new[] { PalDir }))
            {
                var pal = AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(guid));
                if (pal == null) continue;
                string dir = $"{PalRoot}/{pal.palId} {pal.palName}";
                if (!AssetDatabase.IsValidFolder(dir)) continue;

                var controllers = AssetDatabase.FindAssets("t:AnimatorController", new[] { dir })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(p => (path: p, ctrl: AssetDatabase.LoadAssetAtPath<AnimatorController>(p)))
                    .Where(c => c.ctrl != null).ToList();
                bool IsBack(string p) => Path.GetFileNameWithoutExtension(p).EndsWith("_back", StringComparison.OrdinalIgnoreCase);

                foreach (var (path, ctrl) in controllers)
                    foreach (var clip in ctrl.animationClips.Distinct())
                        if (Retime(clip)) retimed++;

                var front = controllers.FirstOrDefault(c => !IsBack(c.path)).ctrl;
                var back = controllers.FirstOrDefault(c => IsBack(c.path)).ctrl;
                bool changed = false;
                bool Same(RuntimeAnimatorController a, RuntimeAnimatorController b) =>
                    a != null && AssetDatabase.GetAssetPath(a) == AssetDatabase.GetAssetPath(b);

                if (front != null && !Same(pal.frontAnimator, front))
                {
                    var first = FirstSprite(front);
                    pal.frontAnimator = front;
                    if (first != null) { pal.frontSprite = first; pal.frontScale = FitScale(first, FrontBox); }
                    log.Add($"{pal.palName} 앞모습");
                    changed = true;
                }
                if (back != null && !Same(pal.backAnimator, back))
                {
                    var first = FirstSprite(back);
                    pal.backAnimator = back;
                    if (first != null) { pal.backSprite = first; pal.backScale = FitScale(first, BackBox); }
                    log.Add($"{pal.palName} 뒷모습");
                    changed = true;
                }
                // 뒷모습 애니메이션은 없지만 폴더에 정지 그림 "…_back.png"가 있으면 그걸 뒷모습으로 연결한다
                if (pal.backSprite == null && pal.backAnimator == null)
                {
                    var backPng = StillPng(dir, true);
                    if (backPng != null)
                    {
                        pal.backSprite = PalSizing.Trim(backPng);
                        log.Add($"{pal.palName} 뒷모습(정지 그림)");
                        changed = true;
                    }
                }
                if (pal.frontSprite == null && pal.frontAnimator == null)
                {
                    var frontPng = StillPng(dir, false);
                    if (frontPng != null) { pal.frontSprite = PalSizing.Trim(frontPng); log.Add($"{pal.palName} 앞모습(정지 그림)"); changed = true; }
                }
                // 앞모습 그림이 지워졌는데(참조가 비었는데) 애니메이션이 있으면 첫 프레임으로 채운다
                if (pal.frontSprite == null && pal.frontAnimator != null) { pal.frontSprite = FirstSprite(pal.frontAnimator); changed = true; }
                if (pal.backSprite == null && pal.backAnimator != null) { pal.backSprite = FirstSprite(pal.backAnimator); changed = true; }
                if (changed) { PalSizing.Apply(pal); EditorUtility.SetDirty(pal); }
            }

            if (log.Count > 0 || retimed > 0) AssetDatabase.SaveAssets();
            if (log.Count > 0 || retimed > 0 || report)
                Debug.Log($"[팰 애니메이션] 프레임으로 새로 만든 애니메이션 {built}개, 새로 연결 {log.Count - built}개" +
                          $"{(log.Count > 0 ? $" ({string.Join(", ", log)})" : "")}, {Fps:0} FPS로 맞춘 애니메이션 {retimed}개");
        }

        /// <summary>
        /// 프레임 PNG만 넣고 아직 애니메이션(컨트롤러)이 없는 폴더에 15 FPS 반복 애니메이션 + 컨트롤러를 만든다.
        /// 예) "000 벨라루주/000_Bellanoir Libero/Frames/*.png" → "000_Bellanoir Libero/000_Bellanoir Libero.controller"
        ///  - 프레임 폴더 이름이 Frames(…)이면 그 위 폴더 이름으로, 아니면 프레임 폴더 이름으로 만든다
        ///  - 폴더 이름이 _back 으로 끝나면 뒷모습 (연결은 Run이 한다)
        ///  - 프레임 그림은 Sprite · Point 필터 · 무압축 · 밉맵 끔으로 맞춘다
        /// </summary>
        static int BuildMissingControllers(List<string> log)
        {
            int built = 0;
            var folders = new List<string>();
            void Walk(string f) { foreach (var sub in AssetDatabase.GetSubFolders(f)) { folders.Add(sub); Walk(sub); } }
            Walk(PalRoot);

            foreach (var folder in folders)
            {
                var frames = Directory.GetFiles(folder, "*.png").Select(p => p.Replace('\\', '/'))
                    .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToList();
                if (frames.Count < 8) continue;                                   // 그림 몇 장뿐이면 애니메이션 폴더가 아님
                string folderName = Path.GetFileName(folder);
                string owner = folderName.StartsWith("Frames", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetDirectoryName(folder).Replace('\\', '/') : folder;
                if (AssetDatabase.FindAssets("t:AnimatorController", new[] { owner }).Length > 0) continue;   // 이미 있음

                foreach (var f in frames)
                {
                    if (AssetImporter.GetAtPath(f) is not TextureImporter imp) continue;
                    if (imp.textureType == TextureImporterType.Sprite && imp.spriteImportMode == SpriteImportMode.Single
                        && imp.filterMode == FilterMode.Point && !imp.mipmapEnabled) continue;
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.spritePixelsPerUnit = 100f;
                    imp.filterMode = FilterMode.Point;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.SaveAndReimport();
                }
                var sprites = frames.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(x => x != null).ToArray();
                if (sprites.Length < 2) continue;

                string name = Path.GetFileName(owner);
                var clip = new AnimationClip { name = name + "_Idle", frameRate = Fps };
                var keys = sprites.Select((sp, i) => new ObjectReferenceKeyframe { time = i / Fps, value = sp }).ToArray();
                AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                settings.stopTime = sprites.Length / Fps;                         // 마지막 프레임도 1/15초 보여 줘서 끊김 없이 반복
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, $"{owner}/{name}_Idle.anim");

                var ctrl = AnimatorController.CreateAnimatorControllerAtPath($"{owner}/{name}.controller");
                var state = ctrl.layers[0].stateMachine.AddState("Idle");
                state.motion = clip;
                ctrl.layers[0].stateMachine.defaultState = state;
                log.Add($"{name} ({sprites.Length}장)");
                built++;
            }
            if (built > 0) AssetDatabase.SaveAssets();
            return built;
        }

        /// <summary>팰 폴더 바로 아래의 정지 그림 PNG (back=true면 이름이 _back 으로 끝나는 것). 스프라이트로 안 들어와 있으면 맞춰서 불러온다</summary>
        static Sprite StillPng(string dir, bool back)
        {
            var png = Directory.GetFiles(dir, "*.png").Select(p => p.Replace('\\', '/'))
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).EndsWith("_back", StringComparison.OrdinalIgnoreCase) == back);
            if (png == null) return null;
            var sprite = PalDataBuilder.LoadSprite(png);
            if (sprite != null) return sprite;
            if (AssetImporter.GetAtPath(png) is not TextureImporter imp) return null;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return PalDataBuilder.LoadSprite(png);
        }

        static readonly EditorCurveBinding SpriteBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        public static Sprite FirstSprite(RuntimeAnimatorController ctrl)
        {
            foreach (var clip in ctrl.animationClips)
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, SpriteBinding);
                if (keys != null && keys.Length > 0) return keys.OrderBy(k => k.time).First().value as Sprite;
            }
            return null;
        }

        /// <summary>그림 넘기기 애니메이션을 15 FPS로 (프레임 순서 유지, 반복). 바꿨으면 true</summary>
        static bool Retime(AnimationClip clip)
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, SpriteBinding);
            if (keys == null || keys.Length < 2) return false;
            keys = keys.OrderBy(k => k.time).ToArray();
            bool already = Mathf.Approximately(clip.frameRate, Fps)
                           && keys.Select((k, i) => Mathf.Abs(k.time - i / Fps) < 0.001f).All(ok => ok);
            if (already) return false;

            for (int i = 0; i < keys.Length; i++) keys[i].time = i / Fps;
            clip.frameRate = Fps;
            AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.stopTime = keys.Length / Fps;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return true;
        }

        static float FitScale(Sprite s, float box)
        {
            if (s == null) return 1f;
            var size = s.bounds.size;
            return box / Mathf.Max(size.x, size.y);
        }
    }
}
#endif
