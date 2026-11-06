#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 프로젝트 정리 (메뉴 PalRogue/91, 92)
    ///  91. 안 쓰는 파일 찾기 — 지우지 않고 보고서(프로젝트 폴더/CleanupReport.txt)만 만든다
    ///  92. 안 쓰는 파일 휴지통으로 — 91과 같은 기준으로 고른 파일을 윈도우 휴지통으로 옮긴다 (되살릴 수 있음)
    ///
    /// 정리 대상 폴더: Assets/Resources, Assets/palrogue_assets_core, Assets/Welcome
    /// "쓰는 파일" 기준:
    ///  ① 씬(빌드 목록)·팰/기술 데이터(Assets/Data)·설정(Assets/Settings)·TMP 폰트가 참조하는 모든 파일 (애니메이션 프레임 포함)
    ///  ② 코드가 이름으로 불러오는 Resources 파일
    ///     - 기술 연출 그림(SkillFx 표), 아이템 그림(ItemCatalog), 지역 배경(GameConfig.AreaBiomes)
    ///     - 소리·UI·상태 그림: 코드 안의 글자("select", "UI/star" …)와 이름이 같은 파일
    ///     - 속성 아이콘(1.팰/2.Elements), 스피어 그림(1.팰/3.Pal Spheres), 상태 아이콘(statuses), UI 폴더는 통째로 둔다
    ///  ③ palrogue_assets_core: 에디터 도구가 쓰는 UI 그림(images/ui)·폰트(fonts)는 통째로,
    ///     아이템·배경·소리 원본은 게임에서 쓰는 것만 남긴다
    /// </summary>
    public static class ProjectCleanup
    {
        static readonly string[] Targets = { "Assets/Resources", "Assets/palrogue_assets_core", "Assets/Welcome" };

        static readonly string[] KeepWholeFolders =
        {
            "Assets/Resources/1.팰/2.Elements/", "Assets/Resources/1.팰/3.Pal Spheres/", "Assets/Resources/statuses/",
            "Assets/Resources/UI/", "Assets/palrogue_assets_core/images/ui/", "Assets/palrogue_assets_core/fonts/",
        };

        static readonly string[] ExtraBgm = { "battle_final", "battle_wild_strong", "menu", "title", "end" };

        // 프로젝트 폴더에 CleanupRequest.txt ("report" 또는 "clean")를 넣으면 메뉴를 누르지 않아도 한 번 실행한다 (원격 작업용)
        const string RequestFile = "CleanupRequest.txt";
        static double nextCheck;

        [InitializeOnLoadMethod]
        static void WatchRequest()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 2.0;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(RequestFile)) return;
            string raw = File.ReadAllText(RequestFile).Trim();
            string what = raw.ToLowerInvariant();
            File.Delete(RequestFile);
            // "menu:" 다음 줄들 = 실행할 메뉴 경로, "trash:" 다음 줄들 = 휴지통으로 보낼 에셋 경로
            var lines = raw.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Skip(1).ToArray();
            if (what.StartsWith("menu:")) { foreach (var m in lines) if (!EditorApplication.ExecuteMenuItem(m)) Debug.LogWarning($"[요청] 메뉴 없음: {m}"); return; }
            if (what.StartsWith("move:"))   // 줄마다 "원래경로|새경로" (메타·참조 유지한 채 이름 바꾸기)
            {
                foreach (var l in lines)
                {
                    var parts = l.Split('|');
                    if (parts.Length != 2) continue;
                    string err = AssetDatabase.MoveAsset(parts[0].Trim(), parts[1].Trim());
                    Debug.Log(string.IsNullOrEmpty(err) ? $"[요청] 이동: {parts[0]} → {parts[1]}" : $"[요청] 이동 실패: {err}");
                }
                AssetDatabase.Refresh();
                return;
            }
            if (what.StartsWith("trash:"))
            {
                var failed = new List<string>();
                var paths = lines.Where(l => File.Exists(l) || Directory.Exists(l)).ToArray();
                AssetDatabase.MoveAssetsToTrash(paths, failed);
                AssetDatabase.Refresh();
                Debug.Log($"[요청] 휴지통으로 {paths.Length - failed.Count}개" + (failed.Count > 0 ? $", 실패: {string.Join(", ", failed)}" : ""));
                return;
            }
            if (what == "test") RunTests();
            else Run(what == "clean");
        }

        /// <summary>자동 테스트 메뉴들을 차례로 실행하고, 그동안 나온 로그를 TestReport.txt 에 모은다</summary>
        static void RunTests()
        {
            var log = new StringBuilder();
            Application.LogCallback hook = (msg, stack, type) =>
                log.AppendLine($"[{type}] {msg}" + (type == LogType.Exception || type == LogType.Error ? "\n" + stack : ""));
            Application.logMessageReceived += hook;
            try
            {
                foreach (var menu in new[]
                {
                    "PalRogue/12. 기술 연출 자동 테스트", "PalRogue/13. 아이템 자동 테스트", "PalRogue/17. 지역 배경 자동 테스트",
                    "PalRogue/18. 속성 뱃지 자동 테스트", "PalRogue/19. 스타팅 팰 자동 테스트", "PalRogue/14. 성장 시스템 자동 테스트",
                    "PalRogue/15. 상대 AI 자동 테스트", "PalRogue/16. 버프·디버프·보스 패턴 자동 테스트",
                    "PalRogue/11. 자동 플레이 점검 (모험 40번, 버그 찾기)",
                })
                {
                    log.AppendLine($"── {menu}");
                    try { if (!EditorApplication.ExecuteMenuItem(menu)) log.AppendLine("  (메뉴 없음)"); }
                    catch (Exception e) { log.AppendLine("  예외: " + e); }
                }
                // 팰 그림이 빠진 데이터가 없는지
                foreach (var g in AssetDatabase.FindAssets("t:PalData", new[] { "Assets/Data/Pals" }))
                {
                    var d = AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g));
                    bool needsBack = d != null && d.primaryType != PalType.Void;   // 제로버스(포획 불가)는 뒷모습이 없다
                    if (d != null && (d.frontSprite == null || (needsBack && d.backSprite == null && d.backAnimator == null)))
                        log.AppendLine($"[Error] 그림이 빠진 팰: {d.palName}");
                }
            }
            finally { Application.logMessageReceived -= hook; }
            File.WriteAllText("TestReport.txt", log.ToString(), new UTF8Encoding(false));
            Debug.Log("[테스트] 끝. 프로젝트 폴더/TestReport.txt");
        }

        [MenuItem("PalRogue/91. 정리: 안 쓰는 파일 찾기 (보고서만)", false, 901)]
        static void Report() => Run(false);

        [MenuItem("PalRogue/92. 정리: 안 쓰는 파일 휴지통으로", false, 902)]
        static void Clean()
        {
            var unused = FindUnused(out _);
            long total = unused.Sum(SizeOf);
            if (!EditorUtility.DisplayDialog("안 쓰는 파일 정리",
                    $"안 쓰는 파일 {unused.Count}개 ({Mb(total)})를 휴지통으로 옮깁니다.\n(휴지통에서 되살릴 수 있어요)", "옮긴다", "취소")) return;
            Run(true);
        }

        public static void Run(bool delete)
        {
            var unused = FindUnused(out var kept);
            var sb = new StringBuilder();
            long total = unused.Sum(SizeOf);
            sb.AppendLine($"[정리] 안 쓰는 파일 {unused.Count}개, {Mb(total)} (남기는 파일 {kept.Count}개, {Mb(kept.Sum(SizeOf))})");
            sb.AppendLine();
            sb.AppendLine("── 폴더별 (안 쓰는 용량 큰 순) ──");
            foreach (var g in unused.GroupBy(FolderKey).OrderByDescending(g => g.Sum(SizeOf)))
                sb.AppendLine($"{Mb(g.Sum(SizeOf)),10}  {g.Count(),5}개  {g.Key}");
            sb.AppendLine();
            sb.AppendLine("── 남기는 파일 폴더별 ──");
            foreach (var g in kept.GroupBy(FolderKey).OrderByDescending(g => g.Sum(SizeOf)))
                sb.AppendLine($"{Mb(g.Sum(SizeOf)),10}  {g.Count(),5}개  {g.Key}   ({string.Join(", ", g.Select(p => Reasons.TryGetValue(p, out var r) ? r : "?").Distinct())})");
            sb.AppendLine();
            sb.AppendLine("── 안 쓰는 파일 전체 목록 ──");
            foreach (var p in unused.OrderBy(p => p, StringComparer.Ordinal)) sb.AppendLine(p);
            File.WriteAllText("CleanupReport.txt", sb.ToString(), new UTF8Encoding(false));

            if (!delete)
            {
                Debug.Log($"[정리] 안 쓰는 파일 {unused.Count}개 ({Mb(total)}). 자세한 목록: 프로젝트 폴더/CleanupReport.txt");
                return;
            }

            var failed = new List<string>();
            AssetDatabase.MoveAssetsToTrash(unused.ToArray(), failed);
            int folders = RemoveEmptyFolders();
            AssetDatabase.Refresh();
            Debug.Log($"[정리] 휴지통으로 옮긴 파일 {unused.Count - failed.Count}개 ({Mb(total)}), 빈 폴더 {folders}개 정리" +
                      (failed.Count > 0 ? $", 실패 {failed.Count}개: {string.Join(", ", failed.Take(20))}" : ""));
        }

        /// <summary>정리 대상 폴더 안에서 쓰지 않는 파일 (에셋 경로)</summary>
        public static List<string> FindUnused(out List<string> kept)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ① 참조 (씬·데이터·설정·폰트)
            var roots = new List<string>();
            roots.AddRange(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
            roots.AddRange(AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath));
            foreach (var dir in new[] { "Assets/Data", "Assets/Settings", "Assets/TextMesh Pro" })
                if (AssetDatabase.IsValidFolder(dir))
                    roots.AddRange(AssetDatabase.FindAssets("", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath));
            roots.AddRange(AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath));
            foreach (var d in AssetDatabase.GetDependencies(roots.Distinct().ToArray(), true)) used.Add(d);

            // ② 코드가 이름으로 부르는 것
            var literals = CodeLiterals();
            var biomes = new HashSet<string>(GameConfig.AreaBiomes, StringComparer.OrdinalIgnoreCase) { "plains" };
            var sounds = new HashSet<string>(literals.Where(l => Regex.IsMatch(l, "^[a-z0-9_]+$")), StringComparer.OrdinalIgnoreCase);
            foreach (var b in biomes.Concat(ExtraBgm)) sounds.Add(b);

            var fxPaths = new HashSet<string>(SkillFx.AllSheets().Where(s => !string.IsNullOrEmpty(s))
                .Select(s => "Assets/Resources/" + SkillFx.TexturePath(s)), StringComparer.OrdinalIgnoreCase)
                { "Assets/Resources/3.VFX/PixelFx/manifest" };
            var itemPaths = new HashSet<string>(ItemCatalog.All.Select(d => "Assets/Resources/" + d.IconPath), StringComparer.OrdinalIgnoreCase);
            var itemNames = new HashSet<string>(ItemCatalog.All.Select(d => Path.GetFileName(d.IconPath)), StringComparer.OrdinalIgnoreCase);

            var all = new List<string>();
            foreach (var t in Targets)
                if (AssetDatabase.IsValidFolder(t))
                    all.AddRange(AssetDatabase.FindAssets("", new[] { t }).Select(AssetDatabase.GUIDToAssetPath)
                        .Where(p => !AssetDatabase.IsValidFolder(p)));
            all = all.Distinct().ToList();

            var unused = new List<string>();
            kept = new List<string>();
            Reasons.Clear();
            foreach (var path in all)
            {
                if (IsUsed(path, used, literals, sounds, biomes, fxPaths, itemPaths, itemNames)) { kept.Add(path); Reasons[path] = lastReason; }
                else unused.Add(path);
            }
            return unused;
        }

        static string lastReason = "";
        public static readonly Dictionary<string, string> Reasons = new Dictionary<string, string>();

        static bool IsUsed(string path, HashSet<string> used, HashSet<string> literals, HashSet<string> sounds, HashSet<string> biomes,
                           HashSet<string> fxPaths, HashSet<string> itemPaths, HashSet<string> itemNames)
        {
            if (used.Contains(path)) { lastReason = "참조됨"; return true; }
            if (KeepWholeFolders.Any(f => path.StartsWith(f, StringComparison.OrdinalIgnoreCase))) { lastReason = "통째로 두는 폴더"; return true; }
            string fileName = Path.GetFileName(path);
            bool usedPack = path.Contains("/3.VFX/PixelFx/") || path.Contains("/3.VFX/Pixel VFX Essentials/");
            if (usedPack && fileName.IndexOf("license", StringComparison.OrdinalIgnoreCase) >= 0) { lastReason = "라이선스"; return true; }   // 쓰는 에셋의 라이선스는 남긴다
            lastReason = "코드에서 이름으로 부름";

            string noExt = Path.ChangeExtension(path, null).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(path);
            string dir = Path.GetDirectoryName(path).Replace('\\', '/') + "/";

            if (path.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
            {
                string rel = noExt.Substring("Assets/Resources/".Length);
                if (rel.StartsWith("3.VFX/", StringComparison.OrdinalIgnoreCase))                 // 기술 연출 표에 있는 그림 (+ 코드에 이름이 적힌 그림)
                {
                    if (fxPaths.Contains(noExt)) return true;
                    const string sheets = "3.VFX/Pixel VFX Essentials/Spritesheets/", px = "3.VFX/PixelFx/";
                    if (rel.StartsWith(sheets, StringComparison.OrdinalIgnoreCase) && literals.Contains(rel.Substring(sheets.Length))) return true;
                    if (rel.StartsWith(px, StringComparison.OrdinalIgnoreCase) && literals.Contains(name)) return true;
                    return false;
                }
                if (literals.Contains(rel)) return true;                                       // "UI/star", "Attack Icon" …
                // "UI/Summary/"처럼 두 단계 이상인 폴더 경로 글자 (한 단계짜리 "3.VFX/" 같은 건 너무 넓어서 쓰지 않는다)
                if (literals.Any(l => l.EndsWith("/") && l.TrimEnd('/').Contains('/') && rel.StartsWith(l, StringComparison.OrdinalIgnoreCase))) return true;
                if (rel.StartsWith("2.아이템/", StringComparison.Ordinal)) return itemPaths.Contains(noExt) || literals.Contains(name);   // 기술머신 그림(tm_fire …) 등
                if (rel.StartsWith("Arenas/", StringComparison.OrdinalIgnoreCase)) return biomes.Any(b => name.StartsWith(b + "_", StringComparison.OrdinalIgnoreCase));
                if (rel.StartsWith("Audio/", StringComparison.OrdinalIgnoreCase)) return sounds.Contains(name);
                return false;                                                                  // 1.팰/1.Pal 등: 참조된 것만
            }
            if (path.StartsWith("Assets/palrogue_assets_core/", StringComparison.OrdinalIgnoreCase))
            {
                if (dir.Contains("/images/items/")) return itemNames.Contains(name) || literals.Contains(name);
                if (dir.Contains("/images/arenas/")) return biomes.Any(b => name.StartsWith(b + "_", StringComparison.OrdinalIgnoreCase));
                if (dir.Contains("/audio/")) return sounds.Contains(name);
                return false;
            }
            return false;                                                                      // Welcome
        }

        /// <summary>스크립트 안의 모든 "문자열" (소리 이름·Resources 경로 찾기용)</summary>
        static HashSet<string> CodeLiterals()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rx = new Regex("\"((?:[^\"\\\\\\n]|\\\\.)*)\"");
            foreach (var file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
                foreach (Match m in rx.Matches(File.ReadAllText(file)))
                {
                    var s = m.Groups[1].Value;
                    if (s.Length >= 2 && s.Length <= 80) set.Add(s);
                }
            return set;
        }

        static int RemoveEmptyFolders()
        {
            int removed = 0;
            foreach (var t in Targets)
            {
                if (!Directory.Exists(t)) continue;
                foreach (var dir in Directory.GetDirectories(t, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    if (Directory.GetFiles(dir).Any(f => !f.EndsWith(".meta")) || Directory.GetDirectories(dir).Length > 0) continue;
                    if (AssetDatabase.DeleteAsset(dir.Replace('\\', '/'))) removed++;
                }
            }
            return removed;
        }

        static string FolderKey(string path)
        {
            var parts = path.Split('/');
            return string.Join("/", parts.Take(Math.Min(parts.Length - 1, 5)));
        }

        static long SizeOf(string path)
        {
            try { return new FileInfo(path).Length; } catch { return 0; }
        }

        static string Mb(long bytes) => $"{bytes / 1048576f:0.0}MB";
    }
}
#endif
