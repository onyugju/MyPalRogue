#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/81. 윈도우 빌드 (exe) — 프로젝트 폴더 옆 PalRogue_Build/PalRogue.exe 로 빌드하고,
    /// 결과(성공·실패, 크기, 오류·경고)를 프로젝트 폴더의 BuildReport.txt 에 적는다.
    /// 빌드 전에 팰 목록(Resources/PalRegistry.asset)을 최신으로 채운다 (빌드에서는 AssetDatabase를 못 쓰므로).
    /// </summary>
    public class BuildTool : IPreprocessBuildWithReport
    {
        public const string OutDir = "../PalRogue_Build";
        const string RegistryPath = "Assets/Resources/PalRegistry.asset";
        public int callbackOrder => 0;

        /// <summary>Unity의 Build 버튼으로 빌드해도 팰 목록은 먼저 채운다</summary>
        public void OnPreprocessBuild(BuildReport report) => FillRegistry();

        [MenuItem("PalRogue/80. 팰 목록 파일 갱신 (빌드용)", false, 800)]
        public static void FillRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<PalRegistry>(RegistryPath);
            if (reg == null)
            {
                reg = ScriptableObject.CreateInstance<PalRegistry>();
                AssetDatabase.CreateAsset(reg, RegistryPath);
            }
            reg.pals = AssetDatabase.FindAssets("t:PalData", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).OrderBy(p => p.name).ToList();
            EditorUtility.SetDirty(reg);
            AssetDatabase.SaveAssets();
            Debug.Log($"[빌드] 팰 목록 {reg.pals.Count}종을 {RegistryPath}에 저장");
        }

        [MenuItem("PalRogue/81. 윈도우 빌드 (exe)", false, 801)]
        public static void BuildWindows()
        {
            FillRegistry();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            string dir = Path.GetFullPath(OutDir);
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "PalRogue.exe");

            var started = DateTime.Now;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            var sb = new StringBuilder();
            var s = report.summary;
            sb.AppendLine($"결과: {s.result}");
            sb.AppendLine($"시각: {started:yyyy-MM-dd HH:mm:ss}  걸린 시간: {(DateTime.Now - started).TotalSeconds:0}초");
            sb.AppendLine($"위치: {exe}");
            sb.AppendLine($"크기: {s.totalSize / 1024f / 1024f:0.0} MB   오류 {s.totalErrors}개, 경고 {s.totalWarnings}개");
            sb.AppendLine($"씬: {string.Join(", ", scenes)}");
            if (Directory.Exists(dir))
            {
                long folder = new DirectoryInfo(dir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                sb.AppendLine($"빌드 폴더 전체: {folder / 1024f / 1024f:0.0} MB");
            }
            sb.AppendLine();
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Warning)
                        sb.AppendLine($"[{m.type}] ({step.name}) {m.content}");
            File.WriteAllText(Path.GetFullPath("BuildReport.txt"), sb.ToString());
            Debug.Log($"[빌드] {s.result} — {exe} (BuildReport.txt)");
        }
    }
}
#endif
