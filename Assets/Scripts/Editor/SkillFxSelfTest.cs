using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/12. 기술 연출 자동 테스트 — 모든 기술에 연출이 정해져 있는지, 쓰는 VFX 그림이 다 있는지 확인
    /// 메뉴 PalRogue/31. 기술 연출 미리보기 — 플레이 중에 실행하면 지금 전투 화면에서 모든 기술 연출을 차례로 보여준다
    /// </summary>
    public static class SkillFxSelfTest
    {
        static List<SkillData> AllSkills() => AssetDatabase.FindAssets("t:SkillData")
            .Select(g => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null).OrderBy(s => s.type).ThenBy(s => s.power).ToList();

        [MenuItem("PalRogue/12. 기술 연출 자동 테스트", false, 202)]
        static void Test()
        {
            var report = new StringBuilder();
            int fail = 0;

            var sheets = SkillFx.AllSheets().Distinct().ToList();
            var missing = new List<string>();
            foreach (var s in sheets)
            {
                var tex = Resources.Load<Texture2D>(SkillFx.TexturePath(s));
                if (tex == null) { missing.Add(s); continue; }
                if (!SkillFx.IsPxSheet(s) && tex.width % tex.height != 0) missing.Add($"{s} (칸 크기가 맞지 않음 {tex.width}x{tex.height})");
            }
            if (missing.Count > 0) { fail++; report.AppendLine($"  ✘ 없는 그림 {missing.Count}개: {string.Join(", ", missing)}"); }
            else report.AppendLine($"  ✔ VFX 그림 {sheets.Count}종 모두 있음");

            var skills = AllSkills();
            var noFx = skills.Where(s => !SkillFx.HasCustomFx(s)).Select(s => $"{s.skillName}({s.name})").ToList();
            report.AppendLine(noFx.Count == 0
                ? $"  ✔ 기술 {skills.Count}개 모두 전용 연출"
                : $"  ✔ 기술 {skills.Count}개 중 {skills.Count - noFx.Count}개 전용 연출, {noFx.Count}개는 속성 기본 연출: {string.Join(", ", noFx)}");

            string head = $"[연출 테스트] {(fail == 0 ? "통과" : "실패")} (메뉴 31로 플레이 중에 직접 볼 수 있어요)";
            if (fail > 0) Debug.LogError(head + "\n" + report); else Debug.Log(head + "\n" + report);
        }

        [MenuItem("PalRogue/31. 기술 연출 미리보기 (플레이 중)", false, 401)]
        static void Preview()
        {
            if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("플레이 중에 실행", "플레이 버튼을 누르고 전투 화면이 나온 뒤에 실행해 주세요.", "확인"); return; }
            var screen = Object.FindAnyObjectByType<BattleScreen>();
            if (screen == null) { Debug.LogError("[연출 미리보기] BattleScreen을 찾지 못했습니다."); return; }
            screen.PreviewSkillFx(AllSkills());
        }

        [MenuItem("PalRogue/39. 팰월드 기술 새 연출 미리보기 (플레이 중)", false, 403)]
        static void PreviewPalworld()
        {
            if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("플레이 중에 실행", "플레이 버튼을 누르고 전투 화면이 나온 뒤에 실행해 주세요.", "확인"); return; }
            var screen = Object.FindAnyObjectByType<BattleScreen>();
            if (screen == null) { Debug.LogError("[연출 미리보기] BattleScreen을 찾지 못했습니다."); return; }
            var names = new HashSet<string>(SkillFx.PwSkillNames);
            var all = AllSkills().Where(s => names.Contains(s.name)).ToList();
            // PwPreview.txt(프로젝트 폴더)에 속성 이름을 적으면 그 속성만 본다 (예: Water,Grass). 없으면 가장 최근에 등록한 속성
            var last = all.FirstOrDefault(s => s.name == SkillFx.PwSkillNames.Last());
            List<SkillData> list;
            if (System.IO.File.Exists("PwPreview.txt"))
            {
                var want = System.IO.File.ReadAllText("PwPreview.txt").Split(',', '\n', ' ').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                list = want.Contains("All") ? all : all.Where(s => want.Contains(s.type.ToString())).ToList();
            }
            else list = last == null ? all : all.Where(s => s.type == last.type).ToList();
            Debug.Log($"[연출 미리보기] 새 연출 기술 {list.Count}개");
            screen.PreviewSkillFx(list, enemyOnly: false, record: true);         // 내 팰·상대가 번갈아 쓴다, Temp/PhaseShiftFrames에 프레임 저장
        }

        [MenuItem("PalRogue/38. 제로버스 기술 연출 미리보기 (플레이 중)", false, 402)]
        static void PreviewAstralym()
        {
            if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("플레이 중에 실행", "플레이 버튼을 누르고 전투 화면이 나온 뒤에 실행해 주세요. (F6 제로버스 테스트 중이면 더 좋아요)", "확인"); return; }
            var screen = Object.FindAnyObjectByType<BattleScreen>();
            if (screen == null) { Debug.LogError("[연출 미리보기] BattleScreen을 찾지 못했습니다."); return; }
            var list = AstralymPhase.PhaseOneSkills.Concat(AstralymPhase.PhaseTwoSkills).ToList();
            screen.PreviewSkillFx(list, enemyOnly: true, record: true);     // 제로버스 쪽에서 쓰는 모습, Temp/PhaseShiftFrames에 프레임 저장
        }
    }
}
