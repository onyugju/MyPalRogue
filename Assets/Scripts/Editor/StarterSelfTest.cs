#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/31. 스타팅 팰 자동 테스트 — 코스트 표·기본 스타팅 5종·해금 기록이 맞는지
    /// 메뉴 PalRogue/32. (테스트) 모든 팰 해금 — 잡지 않아도 선택 화면에서 전부 고를 수 있게
    /// 메뉴 PalRogue/33. (테스트) 해금 기록 초기화 — 처음 상태(스타팅 5종만)로
    /// </summary>
    public static class StarterSelfTest
    {
        static List<PalData> AllPals() => AssetDatabase.FindAssets("t:PalData")
            .Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null).OrderBy(p => p.name).ToList();

        [MenuItem("PalRogue/31. 스타팅 팰 자동 테스트")]
        static void Test()
        {
            var pals = AllPals();
            var report = new StringBuilder();
            int fail = 0;
            void Check(bool ok, string text) { if (!ok) fail++; report.AppendLine($"  {(ok ? "✔" : "✘")} {text}"); }

            // ① 기본 스타팅 5종
            var starters = GameConfig.StarterAssets.Select(a => pals.FirstOrDefault(p => p.name == a)).ToList();
            var missing = GameConfig.StarterAssets.Where((a, i) => starters[i] == null).ToList();
            Check(missing.Count == 0, $"기본 스타팅 팰 {GameConfig.StarterAssets.Length}종 있음" + (missing.Count > 0 ? $" (없음: {string.Join(", ", missing)})" : ""));
            Check(starters.All(s => s == null || (GameConfig.StarterCost(s) == GameConfig.StarterPalCost && PlayerSave.IsUnlocked(s))),
                  $"스타팅 팰은 코스트 {GameConfig.StarterPalCost}, 처음부터 열려 있음");

            // ② 고를 수 있는 팰 / 코스트
            var choosable = pals.Where(GameConfig.CanBeStarter).ToList();
            var zero = pals.FirstOrDefault(p => p.name == GameConfig.FinalBossAsset);
            Check(zero == null || !GameConfig.CanBeStarter(zero), "최종 보스 제로버스는 선택 화면에 안 나옴");
            var bad = choosable.Where(p => GameConfig.StarterCost(p) < 1 || GameConfig.StarterCost(p) > GameConfig.StarterBudget).ToList();
            Check(bad.Count == 0, $"선택 화면 팰 {choosable.Count}종 모두 코스트 1~{GameConfig.StarterBudget}" + (bad.Count > 0 ? $" (이상: {string.Join(", ", bad.Select(b => b.palName))})" : ""));
            var legends = choosable.Where(GameConfig.IsLegendary).ToList();
            Check(legends.All(l => GameConfig.StarterCost(l) >= 7), "속성의 왕(전설)은 코스트 7 이상");
            Check(starters.Where(s => s != null).Take(3).Sum(GameConfig.StarterCost) <= GameConfig.StarterBudget, $"스타팅 팰 3마리를 함께 데려갈 수 있음 (코스트 {GameConfig.StarterBudget})");

            // ③ 코스트 표와 지금 해금 상태
            report.AppendLine("  코스트 표: " + string.Join(", ", choosable.OrderBy(GameConfig.StarterCost).ThenBy(p => p.palId)
                .Select(p => $"{p.palName} {GameConfig.StarterCost(p)}")));
            int unlocked = choosable.Count(PlayerSave.IsUnlocked);
            report.AppendLine($"  지금 해금된 팰: {unlocked}/{choosable.Count}종 (저장 파일: {PlayerSave.FilePath})");

            string head = $"[스타팅 팰 테스트] {(fail == 0 ? "통과" : "실패")} (메뉴 32: 전부 해금 / 33: 기록 초기화)";
            if (fail > 0) Debug.LogError(head + "\n" + report); else Debug.Log(head + "\n" + report);
        }

        [MenuItem("PalRogue/32. (테스트) 모든 팰 해금")]
        static void UnlockAll()
        {
            PlayerSave.Unlock(AllPals());
            Debug.Log("[스타팅 팰] 잡을 수 있는 모든 팰을 해금했습니다. 선택 화면에서 전부 고를 수 있어요. (메뉴 33으로 되돌리기)");
        }

        [MenuItem("PalRogue/33. (테스트) 해금 기록 초기화")]
        static void ResetSave()
        {
            if (!EditorUtility.DisplayDialog("해금 기록 초기화", "잡은 팰 기록과 해금을 모두 지울까요?\n(스타팅 팰 5종만 남습니다)", "지우기", "취소")) return;
            PlayerSave.ResetAll();
            Debug.Log("[스타팅 팰] 해금 기록을 초기화했습니다.");
        }
    }
}
#endif
