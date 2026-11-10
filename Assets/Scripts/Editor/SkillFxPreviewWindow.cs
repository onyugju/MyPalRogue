#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/50. 기술 연출 모아 보기 — 모든 기술(팰월드 기술 + 제로버스 기술)을 속성별로 한 창에 모아,
    /// 누르면 지금 전투 화면에서 그 연출을 바로 보여 준다. 플레이 중(전투 화면)에만 재생된다. F6으로 시작하면 편하다.
    ///  - 기술 이름 버튼: 하나만 보기 / 속성 줄의 ▶ 전부: 그 속성 기술을 차례로 / 맨 위 ▶ 전부 보기: 모든 기술
    ///  - 누가 쓰는지(내 팰·상대·번갈아), 급소·빗나감·효과 굉장을 골라서 볼 수 있다
    /// </summary>
    public class SkillFxPreviewWindow : EditorWindow
    {
        [MenuItem("PalRogue/50. 기술 연출 모아 보기 (창)", false, 500)]
        static void Open()
        {
            var w = GetWindow<SkillFxPreviewWindow>("기술 연출 모아 보기");
            w.minSize = new Vector2(420, 360);
            w.Show();
        }

        static readonly string[] SideNames = { "번갈아", "내 팰이", "상대가" };
        int side = 1;
        bool critical, missed, superEffective, record;
        string search = "";
        Vector2 scroll;
        readonly HashSet<PalType> folded = new HashSet<PalType>();
        bool astralymFolded;
        List<SkillData> skills;

        void OnEnable() => Reload();
        void OnFocus() { if (skills == null || skills.Any(s => s == null)) Reload(); }

        void Reload()
        {
            skills = AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null && s.type != PalType.Void)
                .OrderBy(s => s.type).ThenBy(s => s.category == SkillCategory.Status ? 1 : 0).ThenBy(s => s.power).ThenBy(s => s.skillName)
                .ToList();
        }

        void Update()
        {
            if (EditorApplication.isPlaying) Repaint();       // 지금 무엇을 보여 주는지 계속 갱신
        }

        void OnGUI()
        {
            var screen = EditorApplication.isPlaying ? Object.FindAnyObjectByType<BattleScreen>() : null;

            // ── 위: 상태 ──
            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("플레이 중에만 재생돼요. F6(제로버스 테스트)이나 아무 전투 화면에서 써 주세요.", MessageType.Info);
            else if (screen == null)
                EditorGUILayout.HelpBox("전투 화면이 아니에요. 전투가 시작된 뒤에 눌러 주세요.", MessageType.Warning);
            else if (screen.IsPreviewingFx)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox($"재생 중: {screen.PreviewingFxName}", MessageType.None);
                    if (GUILayout.Button("■ 멈춤", GUILayout.Width(70), GUILayout.Height(38))) screen.StopSkillFxPreview();
                }
            }

            // ── 설정 ──
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("누가 쓰나", GUILayout.Width(56));
                side = GUILayout.Toolbar(side, SideNames);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                critical = GUILayout.Toggle(critical, "급소", "Button");
                superEffective = GUILayout.Toggle(superEffective, "효과 굉장", "Button");
                missed = GUILayout.Toggle(missed, "빗나감", "Button");
                record = GUILayout.Toggle(record, new GUIContent("녹화", "Temp/PhaseShiftFrames에 프레임과 모음 그림(sheet_00.jpg …)을 저장"), "Button");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("새로고침", GUILayout.Width(64))) Reload();
            }

            var shown = Filtered().ToList();
            var astralym = EditorApplication.isPlaying ? AstralymSkills().Where(Match).ToList() : new List<SkillData>();
            if (GUILayout.Button($"▶ 전부 보기 ({shown.Count + astralym.Count}개)", GUILayout.Height(24))) Play(screen, shown.Concat(astralym).ToList());

            EditorGUILayout.Space(4);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (var group in shown.GroupBy(s => s.type))
                DrawGroup(screen, $"{PalTypeNames.Korean(group.Key)} 속성", group.ToList(),
                          !folded.Contains(group.Key), open => { if (open) folded.Remove(group.Key); else folded.Add(group.Key); });

            if (astralym.Count > 0)
                DrawGroup(screen, "제로버스 (보스 전용)", astralym, !astralymFolded, open => astralymFolded = !open);
            else if (!EditorApplication.isPlaying)
                EditorGUILayout.LabelField("제로버스 기술은 플레이 중에 목록에 나와요.", EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }

        void DrawGroup(BattleScreen screen, string title, List<SkillData> list, bool open, System.Action<bool> setOpen)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool now = EditorGUILayout.Foldout(open, $"{title}  ({list.Count})", true);
                if (now != open) setOpen(now);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("▶ 전부", EditorStyles.toolbarButton, GUILayout.Width(56))) Play(screen, list);
            }
            if (!open) return;

            const int cols = 2;
            for (int i = 0; i < list.Count; i += cols)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int c = 0; c < cols; c++)
                    {
                        if (i + c >= list.Count) { GUILayout.FlexibleSpace(); continue; }
                        var s = list[i + c];
                        string info = s.category == SkillCategory.Status ? "변화" : $"위력 {s.power}";
                        if (GUILayout.Button(new GUIContent($"{s.skillName}  <{info}>", $"{s.name}\n{s.description}"), GUILayout.MinWidth(180)))
                            Play(screen, new List<SkillData> { s });
                    }
                }
            }
            EditorGUILayout.Space(2);
        }

        IEnumerable<SkillData> Filtered() => (skills ?? new List<SkillData>()).Where(s => s != null && Match(s));

        bool Match(SkillData s) => string.IsNullOrWhiteSpace(search)
            || (s.skillName ?? "").Contains(search.Trim()) || s.name.ToLowerInvariant().Contains(search.Trim().ToLowerInvariant());

        static IEnumerable<SkillData> AstralymSkills()
        {
            foreach (var s in AstralymPhase.PhaseOneSkills) if (s != null) yield return s;
            foreach (var s in AstralymPhase.PhaseTwoSkills) if (s != null) yield return s;
        }

        void Play(BattleScreen screen, List<SkillData> list)
        {
            if (list.Count == 0) return;
            if (!EditorApplication.isPlaying || screen == null)
            {
                ShowNotification(new GUIContent("플레이 중인 전투 화면에서만 재생돼요"));
                return;
            }
            if (screen.IsPreviewingFx) { ShowNotification(new GUIContent("지금 연출이 끝나면 눌러 주세요 (■ 멈춤)")); return; }
            screen.PreviewSkillFx(list, side == 0 ? 0 : side, critical, missed, record, superEffective ? 2f : 1f);
        }
    }
}
#endif
