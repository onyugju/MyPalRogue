using System.Collections.Generic;
using System.Linq;
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

        static int badgePreview;

        // 미리보기 순서: (상대 속성, 상대 보스?, 내 속성). 속성 2개는 그 자리에서 만든 가짜 팰 데이터로 보여 준다
        static readonly (PalType a1, PalType? a2, bool boss, PalType b1, PalType? b2)[] BadgeScenes =
        {
            (PalType.Fire, null, false, PalType.Grass, null),
            (PalType.Ice, PalType.Dragon, false, PalType.Ice, PalType.Dragon),
            (PalType.Dark, PalType.Fire, true, PalType.Water, PalType.Electric),
            (PalType.Void, null, true, PalType.Neutral, null),
            (PalType.Ground, PalType.Neutral, false, PalType.Dragon, PalType.Dark),
            (PalType.Electric, null, true, PalType.Grass, PalType.Ground),
        };

        /// <summary>
        /// 플레이 중에 누를 때마다 정보창 속성 뱃지를 다음 상황으로 바꿔 본다 (확인용):
        /// 속성 1개 / 2개, 보스 정보창(가로로 긴 모양), 내 쪽 속성 2개 등
        /// </summary>
        [MenuItem("PalRogue/28. 속성 뱃지 미리보기 (플레이 중)")]
        static void PreviewBadges()
        {
            if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("플레이 중에 실행", "플레이 버튼을 누른 뒤에 실행해 주세요.", "확인"); return; }
            var any = AssetDatabase.FindAssets("t:PalData")
                .Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(p => p != null);
            if (any == null) return;
            var sc = BadgeScenes[badgePreview % BadgeScenes.Length];
            badgePreview++;

            PalData Fake(PalType t1, PalType? t2)
            {
                var d = Object.Instantiate(any);
                d.palName = PalTypeNames.Korean(t1) + (t2.HasValue ? "/" + PalTypeNames.Korean(t2.Value) : "");
                d.primaryType = t1;
                d.hasSecondaryType = t2.HasValue;
                if (t2.HasValue) d.secondaryType = t2.Value;
                return d;
            }

            foreach (var box in Object.FindObjectsByType<PalInfoBox>())
            {
                bool enemy = box.expFill == null;
                var pal = enemy ? new Pal(Fake(sc.a1, sc.a2), 30) : new Pal(Fake(sc.b1, sc.b2), 30);
                if (enemy && sc.boss) pal.MakeBoss(3);
                box.gameObject.SetActive(true);
                box.Refresh(pal);
            }
            Debug.Log($"[속성 뱃지 {(badgePreview - 1) % BadgeScenes.Length + 1}/{BadgeScenes.Length}] 상대 {sc.a1}{(sc.a2 != null ? "/" + sc.a2 : "")}{(sc.boss ? " (보스)" : "")}, 나 {sc.b1}{(sc.b2 != null ? "/" + sc.b2 : "")}");
        }

        /// <summary>속성 뱃지 그림이 빠짐없이 있는지 (상대·나 / 속성 1개·2개 반쪽)</summary>
        [MenuItem("PalRogue/29. 속성 뱃지 자동 테스트")]
        static void TestBadges()
        {
            var types = ((PalType[])System.Enum.GetValues(typeof(PalType))).ToList();
            var missing = new List<string>();
            int found = 0;
            foreach (var side in new[] { "enemy_type", "player_type" })
                foreach (var t in types)
                {
                    bool needed = !(t == PalType.Void && side == "player_type");      // 제로버스는 잡을 수 없어서 내 쪽 공허는 필요 없음
                    string single = $"1.팰/2.Elements/{side}/single_type/Single_{t}";
                    if (Resources.Load<Sprite>(single) != null) found++; else if (needed) missing.Add(single);
                    if (t == PalType.Void) continue;                                  // 공허는 속성 1개뿐
                    foreach (var part in new[] { "Type1", "Type2" })
                    {
                        string half = $"1.팰/2.Elements/{side}/double_type/{t}/Double_{t}_{part}";
                        if (Resources.Load<Sprite>(half) != null) found++; else missing.Add(half);
                    }
                }
            // 지금 있는 팰 데이터 중 그림이 없는 속성 조합
            var pals = AssetDatabase.FindAssets("t:PalData")
                .Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != null).ToList();
            var palProblems = new List<string>();
            foreach (var p in pals)
            {
                if (!p.hasSecondaryType)
                {
                    if (Resources.Load<Sprite>($"1.팰/2.Elements/enemy_type/single_type/Single_{p.primaryType}") == null) palProblems.Add(p.palName);
                }
                else if (Resources.Load<Sprite>($"1.팰/2.Elements/enemy_type/double_type/{p.primaryType}/Double_{p.primaryType}_Type2") == null
                      || Resources.Load<Sprite>($"1.팰/2.Elements/enemy_type/double_type/{p.secondaryType}/Double_{p.secondaryType}_Type1") == null)
                    palProblems.Add(p.palName);
            }

            var report = new StringBuilder();
            report.AppendLine($"  {(missing.Count == 0 ? "✔" : "✘")} 뱃지 그림 {found}개 있음" + (missing.Count > 0 ? $", 없음 {missing.Count}개: {string.Join(", ", missing)}" : ""));
            report.AppendLine($"  {(palProblems.Count == 0 ? "✔" : "✘")} 팰 {pals.Count}종 모두 새 뱃지로 표시" + (palProblems.Count > 0 ? $" (예전 아이콘: {string.Join(", ", palProblems)})" : ""));
            string head = $"[속성 뱃지 테스트] {(missing.Count == 0 && palProblems.Count == 0 ? "통과" : "실패")} (메뉴 28로 플레이 중에 볼 수 있어요)";
            if (missing.Count > 0 || palProblems.Count > 0) Debug.LogError(head + "\n" + report); else Debug.Log(head + "\n" + report);
        }

        static int palPreview;

        /// <summary>
        /// 플레이 중에 누를 때마다 다음 팰을 상대 자리(앞모습)와 내 자리(뒷모습)에 세워 본다.
        /// 새로 넣은 그림의 크기·발 위치·애니메이션을 확인하는 용도. 번호 순서대로, 마지막 다음엔 처음으로.
        /// </summary>
        [MenuItem("PalRogue/30. 팰 모습 미리보기 (플레이 중)")]
        static void PreviewPals()
        {
            if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("플레이 중에 실행", "플레이 버튼을 누른 뒤에 실행해 주세요.", "확인"); return; }
            var screen = Object.FindAnyObjectByType<BattleScreen>();
            if (screen == null) return;
            var pals = AssetDatabase.FindAssets("t:PalData")
                .Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).OrderBy(p => p.name).ToList();
            if (pals.Count == 0) return;
            var data = pals[palPreview % pals.Count];
            palPreview++;
            screen.enemyView.Show(data, false);
            screen.playerView.Show(data, true);
            screen.enemyInfo.gameObject.SetActive(true);
            screen.playerInfo.gameObject.SetActive(true);
            screen.enemyInfo.Refresh(new Pal(data, 30));
            screen.playerInfo.Refresh(new Pal(data, 30));
            Debug.Log($"[팰 미리보기 {(palPreview - 1) % pals.Count + 1}/{pals.Count}] {data.palId} {data.palName} — 앞 {(data.frontAnimator != null ? "애니메이션" : "정지 그림")}, " +
                      $"뒤 {(data.backAnimator != null ? "애니메이션" : data.backSprite != null ? "정지 그림" : "없음(앞모습으로 대신)")}");
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
