using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/22. 기술 연출 자동 테스트 — 모든 기술에 연출이 정해져 있는지, 쓰는 VFX 그림이 다 있는지 확인
    /// 메뉴 PalRogue/23. 기술 연출 미리보기 — 플레이 중에 실행하면 지금 전투 화면에서 모든 기술 연출을 차례로 보여준다
    /// </summary>
    public static class SkillFxSelfTest
    {
        const string Root = "3.VFX/Pixel VFX Essentials/Spritesheets/";

        static List<SkillData> AllSkills() => AssetDatabase.FindAssets("t:SkillData")
            .Select(g => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null).OrderBy(s => s.type).ThenBy(s => s.power).ToList();

        [MenuItem("PalRogue/22. 기술 연출 자동 테스트")]
        static void Test()
        {
            var report = new StringBuilder();
            int fail = 0;

            var sheets = SkillFx.AllSheets().Distinct().ToList();
            var missing = new List<string>();
            foreach (var s in sheets)
            {
                var tex = Resources.Load<Texture2D>(Root + s);
                if (tex == null) { missing.Add(s); continue; }
                if (tex.width % tex.height != 0) missing.Add($"{s} (칸 크기가 맞지 않음 {tex.width}x{tex.height})");
            }
            if (missing.Count > 0) { fail++; report.AppendLine($"  ✘ 없는 그림 {missing.Count}개: {string.Join(", ", missing)}"); }
            else report.AppendLine($"  ✔ VFX 그림 {sheets.Count}종 모두 있음");

            var skills = AllSkills();
            var noFx = skills.Where(s => !SkillFx.HasCustomFx(s)).Select(s => $"{s.skillName}({s.name})").ToList();
            report.AppendLine(noFx.Count == 0
                ? $"  ✔ 기술 {skills.Count}개 모두 전용 연출"
                : $"  ✔ 기술 {skills.Count}개 중 {skills.Count - noFx.Count}개 전용 연출, {noFx.Count}개는 속성 기본 연출: {string.Join(", ", noFx)}");

            string head = $"[연출 테스트] {(fail == 0 ? "통과" : "실패")} (메뉴 23으로 플레이 중에 직접 볼 수 있어요)";
            if (fail > 0) Debug.LogError(head + "\n" + report); else Debug.Log(head + "\n" + report);
        }

        [MenuItem("PalRogue/23. 기술 연출 미리보기 (플레이 중)")]
        static void Preview()
        {
            if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("플레이 중에 실행", "플레이 버튼을 누르고 전투 화면이 나온 뒤에 실행해 주세요.", "확인"); return; }
            var screen = Object.FindAnyObjectByType<BattleScreen>();
            if (screen == null) { Debug.LogError("[연출 미리보기] BattleScreen을 찾지 못했습니다."); return; }
            screen.PreviewSkillFx(AllSkills());
        }
    }
}
