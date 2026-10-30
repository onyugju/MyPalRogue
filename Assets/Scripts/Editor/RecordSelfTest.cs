#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/37. 결과 화면 미리보기 (플레이 중) — 게임 오버 때 나오는 결과 화면을 기록 없이 띄워 본다
    /// 메뉴 PalRogue/38. 밸런스 표 — 웨이브별 상대 레벨, 보스·전설 레벨과 강화 배율, 같은 레벨 한 마리 경험치를 콘솔에 보여 준다
    /// </summary>
    public static class RecordSelfTest
    {
        [MenuItem("PalRogue/37. 결과 화면 미리보기 (플레이 중)")]
        static void PreviewResult()
        {
            if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("플레이 중에 실행", "전투에서 명령을 고르는 화면일 때 실행해 주세요.", "확인"); return; }
            var screen = Object.FindAnyObjectByType<BattleScreen>();
            if (screen != null) screen.DebugShowResult();
        }

        [MenuItem("PalRogue/39. (테스트) 아이템 줄 채우기 (플레이 중)")]
        static void FillItems()
        {
            if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("플레이 중에 실행", "전투 중에 실행해 주세요.", "확인"); return; }
            var screen = Object.FindAnyObjectByType<BattleScreen>();
            if (screen != null) screen.DebugFillItems(6);
        }

        [MenuItem("PalRogue/40. 게임 화면 캡처 (Captures 폴더) _F9")]
        static void Capture()
        {
            System.IO.Directory.CreateDirectory("Captures");
            string path = $"Captures/shot_{System.DateTime.Now:HHmmss}.png";
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[캡처] " + path);
        }

        [MenuItem("PalRogue/38. 밸런스 표 (웨이브별 상대 레벨·경험치)")]
        static void BalanceTable()
        {
            var sb = new StringBuilder();
            var rng = new System.Random(0);
            sb.AppendLine("웨이브 | 야생 Lv | 보스 Lv | 전설 Lv (능력×, HP×) | 야생 범위");
            foreach (int w in new[] { 1, 5, 10, 15, 20, 30, 40, 50, 60, 80, 100, 120, 150, 180, 189 })
            {
                int wild = GameConfig.EnemyLevel(w, BossKind.None, new System.Random(1)) - 0;
                string boss = w % 10 == 0 ? $"{GameConfig.EnemyLevel(w, BossKind.Boss, new System.Random(1))}" : "-";
                string legend = GameConfig.LegendAssetFor(w) != null
                    ? $"{GameConfig.EnemyLevel(w, BossKind.Legend, new System.Random(1))} (×{GameConfig.LegendStat(w):0.00}, ×{GameConfig.LegendHp(w):0.00})" : "-";
                sb.AppendLine($"{w,4} | {wild,3} | {boss,3} | {legend} | 하위 {GameConfig.WildPoolShare(w) * 100:0}%");
            }
            sb.AppendLine("같은 레벨 상대(능력치 합 200) 한 마리 경험치: " + string.Join(", ",
                new[] { 5, 10, 20, 40 }.Select(l => $"Lv{l} → {Mathf.RoundToInt(200 * l / GameConfig.ExpYieldDivisor)} (다음 레벨까지 {GameConfig.ExpToNext(l)})")));
            sb.AppendLine("레벨 차이 보정: " + string.Join(", ", new[] { -10, -5, 0, 5, 10, 20 }.Select(d => $"{(d >= 0 ? "+" : "")}{d}Lv ×{GameConfig.ExpLevelScale(20, 20 + d):0.00}")));
            Debug.Log("[밸런스 표]\n" + sb);
        }
    }
}
#endif
