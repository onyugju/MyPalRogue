using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/24. 지역 배경 자동 테스트 — 지역마다 정한 포켓로그 배경 그림(배경·발판 2개)이 Resources/Arenas 에 다 있는지 확인
    /// 메뉴 PalRogue/25. 다음 지역으로 이동 (플레이 중) — 다음 지역 첫 웨이브로 건너뛰며 검은 페이드 연출을 보여 준다
    /// </summary>
    public static class AreaSelfTest
    {
        [MenuItem("PalRogue/24. 지역 배경 자동 테스트")]
        static void Test()
        {
            var report = new StringBuilder();
            int fail = 0;

            if (GameConfig.AreaBiomes.Length < GameConfig.AreaNames.Length)
            {
                fail++;
                report.AppendLine($"  ✘ 지역 {GameConfig.AreaNames.Length}개인데 배경은 {GameConfig.AreaBiomes.Length}개만 정해짐");
            }

            for (int i = 0; i < GameConfig.AreaNames.Length; i++)
            {
                int wave = i * GameConfig.AreaWaveStep + 1;
                string biome = GameConfig.AreaBiome(wave);
                var missing = new List<string>();
                foreach (var part in new[] { "bg", "a", "b" })
                    if (AreaScenery.Load($"{biome}_{part}") == null) missing.Add($"{biome}_{part}");

                int props = 0;
                var baseSprite = AreaScenery.Load(biome + "_b");
                for (int p = 1; p <= AreaScenery.MaxProps; p++)
                {
                    var prop = AreaScenery.Load($"{biome}_b_{p}");
                    if (prop != null && baseSprite != null && prop.rect.size == baseSprite.rect.size) props++;
                }

                if (missing.Count > 0) { fail++; report.AppendLine($"  ✘ 웨이브 {wave}~ {GameConfig.AreaNames[i]} ({biome}): 없는 그림 {string.Join(", ", missing)}"); }
                else report.AppendLine($"  ✔ 웨이브 {wave}~ {GameConfig.AreaNames[i]} → {biome} (장식 {props}개)");
            }

            string head = $"[지역 배경 테스트] {(fail == 0 ? "통과" : "실패")} (메뉴 25로 플레이 중에 지역 이동을 볼 수 있어요)";
            if (fail > 0) Debug.LogError(head + "\n" + report); else Debug.Log(head + "\n" + report);
        }

        [MenuItem("PalRogue/25. 다음 지역으로 이동 (플레이 중)")]
        static void NextArea()
        {
            if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("플레이 중에 실행", "플레이 버튼을 누르고 명령을 고르는 화면이 나온 뒤에 실행해 주세요.", "확인"); return; }
            var screen = Object.FindAnyObjectByType<BattleScreen>();
            if (screen == null) { Debug.LogError("[지역 이동] BattleScreen을 찾지 못했습니다."); return; }
            screen.DebugNextArea();
        }
    }
}
