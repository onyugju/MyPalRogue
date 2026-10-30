#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/36. 사운드 파일 복사 — 포켓로그 audio 에셋 중 게임에 쓰는 것만 Resources/Audio 로 복사한다.
    ///  - 배경음악(bgm): 스트리밍(메모리를 적게), 효과음(se·ui): 미리 풀어 두기(바로 소리 나게)
    /// 에디터가 켜질 때 복사본이 없으면 자동으로 한 번 실행된다. 쓰는 곡을 바꾸려면 아래 목록을 고치면 된다.
    /// </summary>
    public static class AudioSetup
    {
        const string Source = "Assets/palrogue_assets_core/audio";
        const string Target = "Assets/Resources/Audio";

        static readonly string[] Bgm =
        {
            "title", "menu", "end", "heal",
            "plains", "volcano", "forest", "sea", "snowy_forest", "desert", "mountain", "wasteland", "abyss", "space",   // 지역 10곳
            "battle_wild_strong", "battle_final",                                                                         // 보스·최종 보스
        };
        static readonly string[] Se =
        {
            "hit", "hit_strong", "hit_weak", "faint", "flee", "exp", "level_up", "stat_up", "stat_down",
            "pb_throw", "pb_bounce_1", "pb_catch", "pb_rel", "save", "buy", "shine", "achv",
        };
        static readonly string[] Ui = { "select", "menu_open", "error" };

        [InitializeOnLoadMethod]
        static void Auto() => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists($"{Target}/bgm/title.mp3") && File.Exists($"{Source}/bgm/title.mp3")) Copy();
        };

        [MenuItem("PalRogue/36. 사운드 파일 복사 (포켓로그 audio → Resources/Audio)")]
        public static void Copy()
        {
            int copied = 0;
            var missing = new List<string>();
            Ensure(Target);
            foreach (var g in new[] { "bgm", "se", "ui" }) Ensure($"{Target}/{g}");   // 폴더는 묶음 작업 전에 만든다
            AssetDatabase.StartAssetEditing();
            try
            {
                copied += CopyGroup("bgm", ".mp3", Bgm, missing);
                copied += CopyGroup("se", ".wav", Se, missing);
                copied += CopyGroup("ui", ".wav", Ui, missing);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();

            // 가져오기 설정
            foreach (var group in new[] { "bgm", "se", "ui" })
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { $"{Target}/{group}" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not AudioImporter imp) continue;
                    var s = imp.defaultSampleSettings;
                    var want = group == "bgm" ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                    if (s.loadType == want && imp.loadInBackground == (group == "bgm")) continue;
                    s.loadType = want;
                    s.compressionFormat = group == "bgm" ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                    imp.defaultSampleSettings = s;
                    imp.loadInBackground = group == "bgm";
                    imp.SaveAndReimport();
                }
            }
            Debug.Log($"[사운드] Resources/Audio 에 {copied}개 복사" + (missing.Count > 0 ? $", 원본 없음: {string.Join(", ", missing)}" : " (빠진 파일 없음)"));
        }

        static int CopyGroup(string group, string ext, string[] names, List<string> missing)
        {
            int n = 0;
            foreach (var name in names)
            {
                string from = $"{Source}/{group}/{name}{ext}", to = $"{Target}/{group}/{name}{ext}";
                if (File.Exists(to)) continue;
                if (!File.Exists(from)) { missing.Add($"{group}/{name}"); continue; }
                if (AssetDatabase.CopyAsset(from, to)) n++;
            }
            return n;
        }

        static void Ensure(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        }
    }
}
#endif
