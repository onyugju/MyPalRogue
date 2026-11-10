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
    /// 메뉴 PalRogue/12. 팰 애니메이션 자동 연결
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

        /// <summary>애니메이션마다 따로 정한 FPS (이름에 들어 있으면 적용). 나머지는 15 FPS</summary>
        static readonly (string name, float fps)[] FpsOverride = { ("Astralym_Phase2", 15f) };
        public static float FpsFor(string name) { foreach (var o in FpsOverride) if (name != null && name.Contains(o.name)) return o.fps; return Fps; }

        /// <summary>팰 모습(앞/뒤)으로 연결하지 않는 애니메이션 (제로버스 2페이즈처럼 전투 중에 따로 트는 것)</summary>
        static bool IsExtraForm(string path) => Path.GetFileNameWithoutExtension(path).Contains("Phase2");

        [InitializeOnLoadMethod]
        static void Auto() => EditorApplication.delayCall += () => Run(false);

        [MenuItem("PalRogue/12. 팰 애니메이션 자동 연결", false, 102)]
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

                var front = controllers.FirstOrDefault(c => !IsBack(c.path) && !IsExtraForm(c.path)).ctrl;
                var back = controllers.FirstOrDefault(c => IsBack(c.path) && !IsExtraForm(c.path)).ctrl;
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
                float fps = FpsFor(name);
                var clip = new AnimationClip { name = name + "_Idle", frameRate = fps };
                var keys = sprites.Select((sp, i) => new ObjectReferenceKeyframe { time = i / fps, value = sp }).ToArray();
                AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                settings.stopTime = sprites.Length / fps;                         // 마지막 프레임도 한 칸 보여 줘서 끊김 없이 반복
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, $"{owner}/{name}_Idle.anim");
                if (MakeSeamless(clip, out string how)) log.Add($"{name} 반복 이음새: {how}");   // AI 영상에서 뽑은 프레임은 끝과 처음이 안 맞는 경우가 많다

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
            float Fps = FpsFor(clip.name);
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

        // ═════════════════ 반복 이음새 맞추기 ═════════════════
        [MenuItem("PalRogue/15. 애니메이션 반복 이음새 자동 맞춤", false, 105)]
        static void SeamlessMenu()
        {
            var log = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { PalRoot }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null && MakeSeamless(clip, out string how)) log.Add($"{clip.name}: {how}");
            }
            if (log.Count > 0) AssetDatabase.SaveAssets();
            Debug.Log($"[팰 애니메이션] 반복 이음새를 고친 애니메이션 {log.Count}개{(log.Count > 0 ? "\n" + string.Join("\n", log) : " (모두 자연스럽게 이어짐)")}");
        }

        const int SeamGrid = 48;                 // 비교할 때 그림을 이 칸 수로 줄여서 본다

        /// <summary>
        /// 마지막 프레임 → 첫 프레임으로 넘어갈 때 그림이 튀면(보통 프레임 사이 차이보다 훨씬 크면),
        /// 영상 안에서 처음과 거의 같은 모습으로 돌아오는 지점을 찾아 거기서 끊는다 (i번째 ~ j-1번째 프레임만 반복, j ≈ i).
        /// 그런 지점이 없으면 앞으로 갔다가 거꾸로 돌아오는 왕복 반복으로 바꾼다. 이미 자연스러우면 그대로 둔다.
        /// </summary>
        public static bool MakeSeamless(AnimationClip clip, out string how)
        {
            how = null;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, SpriteBinding);
            if (keys == null || keys.Length < 16) return false;
            var sprites = keys.OrderBy(k => k.time).Select(k => k.value as Sprite).ToList();
            if (sprites.Any(x => x == null)) return false;

            var cache = new Dictionary<Sprite, float[]>();
            float[] Px(Sprite sp)
            {
                if (cache.TryGetValue(sp, out var v)) return v;
                v = Thumb(sp);
                cache[sp] = v;
                return v;
            }
            float Diff(int a, int b)
            {
                var x = Px(sprites[a]); var y = Px(sprites[b]);
                if (x == null || y == null) return 0f;
                float sum = 0f;
                for (int i = 0; i < x.Length; i++) sum += Mathf.Abs(x[i] - y[i]);
                return sum / x.Length;
            }

            int n = sprites.Count;
            var steps = Enumerable.Range(0, n - 1).Select(i => Diff(i, i + 1)).OrderBy(d => d).ToList();
            float step = steps[steps.Count / 2];                         // 보통 한 프레임 사이 차이
            if (step <= 0f) return false;
            float seam = Diff(n - 1, 0);
            if (seam <= step * 2f) return false;                         // 이미 자연스럽다 (프레임 사이 차이의 2배까지는 괜찮다)

            // 처음 1/4 안의 i와 절반 뒤의 j 중에서 모습이 가장 비슷한 쌍
            int bestI = -1, bestJ = -1;
            float best = float.MaxValue;
            for (int i = 0; i < n / 4; i++)
                for (int j = i + n / 2; j < n; j++)
                {
                    float d = Diff(j, i);
                    if (d < best) { best = d; bestI = i; bestJ = j; }
                }

            List<Sprite> loop;
            if (BestCut(sprites, out int ci, out int cj, out float ratio) && ratio <= 2f)
            {
                loop = sprites.GetRange(ci, cj - ci);
                how = $"{ci + 1}~{cj}번째 프레임만 반복 (이음새 = 보통 프레임 사이의 {ratio:0.0}배, {n}→{loop.Count}장)";
            }
            else if (bestI >= 0 && best <= step * 1.2f)
            {
                loop = sprites.GetRange(bestI, bestJ - bestI);
                how = $"{bestI + 1}~{bestJ}번째 프레임만 반복 ({bestJ + 1}번째가 {bestI + 1}번째와 거의 같음, {n}→{loop.Count}장)";
            }
            else if ((loop = CrossfadeLoop(sprites, out how)) == null)
            {
                loop = new List<Sprite>(sprites);
                for (int i = n - 2; i >= 1; i--) loop.Add(sprites[i]);
                how = $"왕복 반복 ({n}→{loop.Count}장)";
            }

            float fps = clip.frameRate > 0f ? clip.frameRate : Fps;
            var newKeys = loop.Select((sp, i) => new ObjectReferenceKeyframe { time = i / fps, value = sp }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, newKeys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.stopTime = loop.Count / fps;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return true;
        }

        /// <summary>
        /// 거꾸로 돌아가는 왕복 반복 대신, 끝 몇 장을 처음 몇 장 쪽으로 서서히 겹쳐(크로스페이드) 앞으로만 도는 반복을 만든다.
        /// 섞은 그림은 프레임 폴더 옆 "Frames_Loop" 폴더에 새 PNG로 저장한다 (원본 프레임은 그대로).
        /// 반복 = 원본 K~n-K-1번째 + 섞은 K장 (마지막 섞은 그림이 K-1번째와 거의 같아서 K번째로 자연스럽게 이어진다)
        /// </summary>
        static List<Sprite> CrossfadeLoop(List<Sprite> sprites, out string how)
        {
            how = null;
            int n = sprites.Count;
            int k = Mathf.Clamp(n / 12, 4, 8);
            if (n < k * 4) return null;
            // 겹칠 두 구간이 가장 비슷한 곳을 고른다: 반복 = i ~ j-1번째, 끝 k장(j-k ~ j-1)을 i-k ~ i-1번째 쪽으로 섞는다
            var thumbs = sprites.Select(Thumb).ToList();
            if (thumbs.Any(t => t == null)) return null;
            float D(int a, int b) { var x = thumbs[a]; var y = thumbs[b]; float sum = 0f; for (int q = 0; q < x.Length; q++) sum += Mathf.Abs(x[q] - y[q]); return sum / x.Length; }
            int bi = -1, bj = -1; float best = float.MaxValue;
            for (int i = k; i <= n / 3; i++)
                for (int j = i + n / 2; j <= n; j++)
                {
                    float sum = 0f;
                    for (int s = 0; s < k; s++) sum += D(j - k + s, i - k + s);
                    if (sum < best) { best = sum; bi = i; bj = j; }
                }
            if (bi < 0) return null;
            string firstPath = AssetDatabase.GetAssetPath(sprites[0]);
            if (string.IsNullOrEmpty(firstPath)) return null;
            string dir = Path.GetDirectoryName(firstPath).Replace('\\', '/');
            string outDir = (Path.GetFileName(dir).StartsWith("Frames", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(dir).Replace('\\', '/') : dir) + "/Frames_Loop";
            if (!AssetDatabase.IsValidFolder(outDir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(outDir).Replace('\\', '/'), "Frames_Loop");
            var srcImp = AssetImporter.GetAtPath(firstPath) as TextureImporter;
            string baseName = Path.GetFileNameWithoutExtension(firstPath);
            var made = new List<string>();
            for (int s = 0; s < k; s++)
            {
                var a = LoadPng(AssetDatabase.GetAssetPath(sprites[bj - k + s]));
                var b = LoadPng(AssetDatabase.GetAssetPath(sprites[bi - k + s]));
                if (a == null || b == null || a.width != b.width || a.height != b.height)
                {
                    if (a != null) UnityEngine.Object.DestroyImmediate(a);
                    if (b != null) UnityEngine.Object.DestroyImmediate(b);
                    return null;                                               // 크기가 다르면 섞을 수 없다 → 왕복 반복
                }
                float w = (s + 1f) / (k + 1f);
                var pa = a.GetPixels32(); var pb = b.GetPixels32();
                var po = new Color32[pa.Length];
                for (int i = 0; i < pa.Length; i++)
                {
                    float aa = pa[i].a / 255f * (1f - w), ab = pb[i].a / 255f * w, ao = aa + ab;
                    if (ao <= 0.001f) { po[i] = new Color32(0, 0, 0, 0); continue; }
                    po[i] = new Color32((byte)((pa[i].r * aa + pb[i].r * ab) / ao), (byte)((pa[i].g * aa + pb[i].g * ab) / ao),
                                        (byte)((pa[i].b * aa + pb[i].b * ab) / ao), (byte)Mathf.Clamp(Mathf.RoundToInt(ao * 255f), 0, 255));
                }
                var o = new Texture2D(a.width, a.height, TextureFormat.RGBA32, false);
                o.SetPixels32(po);
                string path = $"{outDir}/{baseName}_loop{s:00}.png";
                File.WriteAllBytes(path, o.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); UnityEngine.Object.DestroyImmediate(o);
                made.Add(path);
            }
            var loop = new List<Sprite>(sprites.GetRange(bi, bj - k - bi));
            foreach (var path in made)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(path) is TextureImporter imp)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.spritePixelsPerUnit = srcImp != null ? srcImp.spritePixelsPerUnit : 100f;
                    imp.filterMode = srcImp != null ? srcImp.filterMode : FilterMode.Point;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    if (srcImp != null) { var ts = new TextureImporterSettings(); srcImp.ReadTextureSettings(ts); var ds = new TextureImporterSettings(); imp.ReadTextureSettings(ds); ds.spriteAlignment = ts.spriteAlignment; ds.spritePivot = ts.spritePivot; imp.SetTextureSettings(ds); }
                    imp.SaveAndReimport();
                }
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null) return null;
                loop.Add(sp);
            }
            how = $"{bi + 1}~{bj}번째를 반복, 끝 {k}장을 {bi - k + 1}~{bi}번째 쪽으로 겹쳐 앞으로만 반복 ({n}→{loop.Count}장)";
            return loop;
        }

        /// <summary>
        /// 그대로 잘라서 반복할 구간 찾기: i ~ j-1번째를 반복할 때 j번째가 i번째와 가장 비슷한 곳 (구간은 전체의 40% 이상).
        /// ratio = 이음새 차이 / 보통 한 프레임 사이 차이 (1에 가까울수록 이음새가 안 보인다)
        /// </summary>
        static bool BestCut(List<Sprite> sprites, out int bi, out int bj, out float ratio)
        {
            bi = bj = -1; ratio = float.MaxValue;
            int n = sprites.Count;
            var t = sprites.Select(Thumb).ToList();
            if (t.Any(x => x == null)) return false;
            float D(int a, int b) { var x = t[a]; var y = t[b]; float sum = 0f; for (int q = 0; q < x.Length; q++) sum += Mathf.Abs(x[q] - y[q]); return sum / x.Length; }
            var steps = Enumerable.Range(0, n - 1).Select(i => D(i, i + 1)).OrderBy(d => d).ToList();
            float step = steps[steps.Count / 2];
            if (step <= 0f) return false;
            int minLen = Mathf.Max(16, Mathf.RoundToInt(n * 0.4f));
            for (int i = 0; i < n; i++)
                for (int j = i + minLen; j < n; j++)
                {
                    float r = D(j, i) / step;
                    if (r < ratio - 0.05f || (r < ratio + 0.05f && j - i > bj - bi)) { if (r < ratio) ratio = r; bi = i; bj = j; }
                }
            // 마지막 프레임 → 첫 프레임 (그대로 반복)도 후보
            float wrap = D(n - 1, 0) / step;
            if (wrap <= ratio + 0.05f) { ratio = wrap; bi = 0; bj = n; }
            return bi >= 0;
        }

        static Texture2D LoadPng(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (t.LoadImage(File.ReadAllBytes(path))) return t;
            UnityEngine.Object.DestroyImmediate(t);
            return null;
        }

        /// <summary>왕복(앞으로 갔다 거꾸로 돌아오는) 반복이면 앞으로 가는 프레임만 돌려준다</summary>
        static List<Sprite> PingPongForward(List<Sprite> seq)
        {
            int m = seq.Count;
            if (m < 6 || m % 2 != 0) return null;
            int u = m / 2 + 1;                                                  // 왕복 길이 = 2u - 2
            for (int i = 1; i < u - 1; i++) if (seq[u - 1 + i] != seq[u - 1 - i]) return null;
            return seq.GetRange(0, u);
        }

        [MenuItem("PalRogue/16. 왕복(역재생) 반복 애니메이션을 앞으로만 도는 반복으로", false, 106)]
        public static void FixPingPongMenu()
        {
            var log = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { PalRoot }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip == null) continue;
                if (PingPongFilter != null && !clip.name.Contains(PingPongFilter)) continue;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, SpriteBinding);
                if (keys == null) continue;
                var seq = keys.OrderBy(x => x.time).Select(x => x.value as Sprite).ToList();
                if (seq.Any(x => x == null)) continue;
                var fwd = PingPongForward(seq);
                var loopFrame = seq.FirstOrDefault(x => AssetDatabase.GetAssetPath(x).Contains("/Frames_Loop/"));
                string loopOwner = null;
                if (fwd == null && loopFrame != null)                              // 이미 고친 것을 다시 만들 때: 원래 프레임 전부로
                {
                    string owner = loopOwner = Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(loopFrame))).Replace('\\', '/');
                    fwd = Directory.GetFiles(owner, "*.png").Select(x => x.Replace('\\', '/')).OrderBy(x => Path.GetFileName(x), StringComparer.Ordinal)
                        .Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(x => x != null).ToList();
                    foreach (var old in Directory.GetFiles(owner + "/Frames_Loop", "*.png")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
                }
                if (fwd == null) continue;
                List<Sprite> loop; string how;
                if (BestCut(fwd, out int ci, out int cj, out float ratio) && ratio <= 2f)
                {
                    loop = fwd.GetRange(ci, cj - ci);
                    how = $"{ci + 1}~{cj}번째 프레임을 그대로 반복 (이음새 차이 = 보통 프레임 사이의 {ratio:0.0}배, {fwd.Count}→{loop.Count}장)";
                    if (loopOwner != null) AssetDatabase.DeleteAsset(loopOwner + "/Frames_Loop");   // 섞은 그림은 이제 안 쓴다
                }
                else loop = CrossfadeLoop(fwd, out how);
                if (loop == null) continue;
                float fps = clip.frameRate > 0f ? clip.frameRate : Fps;
                AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, loop.Select((sp, i) => new ObjectReferenceKeyframe { time = i / fps, value = sp }).ToArray());
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                settings.stopTime = loop.Count / fps;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                log.Add($"{clip.name}: {how}");
            }
            if (log.Count > 0) AssetDatabase.SaveAssets();
            Debug.Log($"[팰 애니메이션] 왕복 반복을 고친 애니메이션 {log.Count}개{(log.Count > 0 ? "\n" + string.Join("\n", log) : "")}");
        }

        /// <summary>메뉴 16이 고칠 애니메이션 이름 (null이면 전부)</summary>
        public static string PingPongFilter = "Astralym";

        /// <summary>스프라이트 그림을 SeamGrid×SeamGrid 칸으로 줄인 RGBA 값 (파일에서 직접 읽는다)</summary>
        static float[] Thumb(Sprite sp)
        {
            string path = AssetDatabase.GetAssetPath(sp);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var tex = new Texture2D(2, 2);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
                var r = sp.rect;
                var v = new float[SeamGrid * SeamGrid * 4];
                for (int y = 0; y < SeamGrid; y++)
                    for (int x = 0; x < SeamGrid; x++)
                    {
                        var c = tex.GetPixel((int)(r.x + (x + 0.5f) / SeamGrid * r.width), (int)(r.y + (y + 0.5f) / SeamGrid * r.height));
                        int k = (y * SeamGrid + x) * 4;
                        v[k] = c.r * c.a; v[k + 1] = c.g * c.a; v[k + 2] = c.b * c.a; v[k + 3] = c.a;
                    }
                return v;
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        static float FitScale(Sprite s, float box)
        {
            if (s == null) return 1f;
            var size = s.bounds.size;
            return box / Mathf.Max(size.x, size.y);
        }

        // ═════════════════ 프레임 용량 줄이기 ═════════════════
        const int HalveMinSide = 600;                      // 긴 변이 이보다 작으면 이미 작은 그림이라 건너뛴다
        const string FrameBackupDir = "../PalRogue_FrameBackup";   // 원본 보관: 프로젝트 폴더 옆 (게임·git에 안 들어감)

        /// <summary>
        /// 팰 애니메이션 프레임(PNG)을 가로·세로 절반으로 줄인다 (용량 약 1/4).
        ///  - 2×2 화소를 투명도까지 고려해 평균 내서 줄인다 (가장자리가 검게 번지지 않게)
        ///  - 원본은 프로젝트 폴더 옆 PalRogue_FrameBackup 에 같은 경로로 복사해 둔다 (되돌리려면 그 파일을 다시 덮어쓰면 된다)
        ///  - 그림 하나짜리(Single) 스프라이트만, 긴 변이 600px 이상인 것만 줄인다
        ///  - 1유닛당 픽셀(PPU)도 절반으로 바꿔서 화면에 보이는 크기는 그대로 둔다
        /// </summary>
        [MenuItem("PalRogue/14. 애니메이션 프레임 절반으로 줄이기", false, 104)]
        public static void HalveFrames()
        {
            var frames = new SortedSet<string>(StringComparer.Ordinal);
            // 팰 폴더 안의 모든 애니메이션 (팰 앞/뒷모습 + 제로버스 2페이즈처럼 따로 트는 것)
            foreach (var guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { PalRoot }))
                foreach (var dep in AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid), true))
                    if (dep.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) frames.Add(dep);

            int done = 0, skipped = 0;
            long before = 0, after = 0;
            var halved = new List<string>();
            try
            {
                AssetDatabase.StartAssetEditing();
                int i = 0;
                foreach (var path in frames)
                {
                    i++;
                    if (i % 20 == 0) EditorUtility.DisplayProgressBar("프레임 줄이기", path, i / (float)frames.Count);
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null || imp.spriteImportMode != SpriteImportMode.Single) { skipped++; continue; }
                    byte[] bytes = File.ReadAllBytes(path);
                    var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!src.LoadImage(bytes) || Mathf.Max(src.width, src.height) < HalveMinSide) { UnityEngine.Object.DestroyImmediate(src); skipped++; continue; }

                    var dst = Downsample(src);
                    byte[] png = dst.EncodeToPNG();
                    UnityEngine.Object.DestroyImmediate(src);
                    UnityEngine.Object.DestroyImmediate(dst);

                    string backup = Path.Combine(FrameBackupDir, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    if (!File.Exists(backup)) File.WriteAllBytes(backup, bytes);   // 처음 원본만 보관
                    File.WriteAllBytes(path, png);
                    before += bytes.Length;
                    after += png.Length;
                    halved.Add(path);
                    done++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh();
            // 그림이 절반이 됐으니 1유닛당 픽셀도 절반으로 → 화면에 보이는 크기는 그대로 (배율을 다시 맞출 필요 없음)
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var path in halved)
                    if (AssetImporter.GetAtPath(path) is TextureImporter imp) { imp.spritePixelsPerUnit *= 0.5f; imp.SaveAndReimport(); }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Debug.Log($"[프레임 줄이기] {done}장 절반으로 줄임 ({before / 1048576f:0.0}MB → {after / 1048576f:0.0}MB), 건너뜀 {skipped}장. " +
                      $"화면 크기는 그대로. 원본: 프로젝트 폴더 옆 PalRogue_FrameBackup");
        }

        /// <summary>2×2 화소를 투명도 가중 평균으로 합쳐 가로·세로 절반 크기의 그림을 만든다</summary>
        static Texture2D Downsample(Texture2D src)
        {
            int sw = src.width, sh = src.height, w = Mathf.Max(1, sw / 2), h = Mathf.Max(1, sh / 2);
            var s = src.GetPixels32();
            var d = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                        {
                            var c = s[Mathf.Min(sh - 1, y * 2 + dy) * sw + Mathf.Min(sw - 1, x * 2 + dx)];
                            r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                        }
                    d[y * w + x] = a == 0 ? new Color32(0, 0, 0, 0)
                        : new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)((a + 2) / 4));
                }
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.SetPixels32(d);
            dst.Apply();
            return dst;
        }
    }
}
#endif
