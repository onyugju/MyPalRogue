#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/4  → Assets/Data/Skills 에 임시 스킬 21개 생성
    /// 메뉴 PalRogue/5  → 학습 스킬이 비어 있는 팰에 속성 기준으로 스킬 자동 연결
    /// 이미 있는 에셋/직접 채운 학습 목록은 건드리지 않는다.
    /// ※ 스킬 이름과 수치는 전부 임시 값(원작 데이터 아님). 밸런스는 나중에 인스펙터에서 조절.
    /// </summary>
    public static class SkillDataBuilder
    {
        const string SkillDir = "Assets/Data/Skills";
        const string PalDir = "Assets/Data/Pals";

        enum Slot { Basic, Strong, Extra }

        struct Row
        {
            public Slot slot; public PalType type; public string kor, eng;
            public SkillCategory cat; public int power, acc, pp, priority;
            public Row(Slot slot, PalType type, string kor, string eng, SkillCategory cat,
                       int power, int acc, int pp, int priority = 0)
            {
                this.slot = slot; this.type = type; this.kor = kor; this.eng = eng; this.cat = cat;
                this.power = power; this.acc = acc; this.pp = pp; this.priority = priority;
            }
        }

        const SkillCategory P = SkillCategory.Physical;
        const SkillCategory S = SkillCategory.Special;

        static readonly Row[] Rows =
        {
            // ── 기본기: 위력 40 / 명중 100 / PP 25 ──
            new(Slot.Basic, PalType.Neutral,  "몸통박치기",   "Tackle",        P, 40, 100, 25),
            new(Slot.Basic, PalType.Fire,     "불씨",         "Fire_Seed",     S, 40, 100, 25),
            new(Slot.Basic, PalType.Water,    "물방울",       "Bubble_Shot",   S, 40, 100, 25),
            new(Slot.Basic, PalType.Grass,    "씨앗 발사",    "Seed_Shot",     S, 40, 100, 25),
            new(Slot.Basic, PalType.Electric, "스파크",       "Spark",         S, 40, 100, 25),
            new(Slot.Basic, PalType.Ground,   "모래 뿌리기",  "Sand_Toss",     P, 40, 100, 25),
            new(Slot.Basic, PalType.Ice,      "얼음 조각",    "Ice_Shard",     P, 40, 100, 25),
            new(Slot.Basic, PalType.Dragon,   "용의 숨결",    "Dragon_Breath", S, 40, 100, 25),
            new(Slot.Basic, PalType.Dark,     "어둠 발톱",    "Shadow_Claw",   P, 40, 100, 25),
            new(Slot.Basic, PalType.Void,     "공허의 파동",  "Void_Pulse",    S, 50, 100, 20),   // 제로버스 전용

            // ── 강기술: 위력 80~110 / 명중 80~90 / PP 5~10 ──
            new(Slot.Strong, PalType.Neutral,  "강타",           "Heavy_Slam",       P,  90,  85, 10),
            new(Slot.Strong, PalType.Fire,     "화염구",         "Fire_Ball",        S,  80,  90, 10),
            new(Slot.Strong, PalType.Water,    "하이드로 제트",  "Hydro_Jet",        S,  80,  90, 10),
            new(Slot.Strong, PalType.Grass,    "풀 회오리",      "Grass_Tornado",    S,  80,  90, 10),
            new(Slot.Strong, PalType.Electric, "번개 강타",      "Lightning_Strike", S,  80,  90, 10),
            new(Slot.Strong, PalType.Ground,   "암석 낙하",      "Rock_Fall",        P,  80,  90, 10),
            new(Slot.Strong, PalType.Ice,      "아이스 미사일",  "Ice_Missile",      S,  80,  90, 10),
            new(Slot.Strong, PalType.Dragon,   "드래곤 메테오",  "Dragon_Meteor",    S,  90,  85,  5),
            new(Slot.Strong, PalType.Dark,     "암흑 광선",      "Dark_Laser",       S,  80,  90, 10),
            new(Slot.Strong, PalType.Void,     "소멸",           "Annihilate",       S, 110,  80,  5),   // 제로버스 전용

            // ── 선제기 ──
            new(Slot.Extra,  PalType.Neutral,  "기습",           "Quick_Strike",     P,  35, 100, 20, priority: 1),
        };

        const string QuickStrike = "Quick_Strike";

        // ───────── 4. 스킬 에셋 생성 ─────────
        [MenuItem("PalRogue/4. SkillData 에셋 생성")]
        static void CreateSkills()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(SkillDir);

            int created = 0, skipped = 0;
            foreach (var r in Rows)
            {
                string path = $"{SkillDir}/{r.eng}.asset";
                if (AssetDatabase.LoadAssetAtPath<SkillData>(path) != null) { skipped++; continue; }

                var sk = ScriptableObject.CreateInstance<SkillData>();
                sk.skillName = r.kor;
                sk.description = "(임시 데이터)";
                sk.type = r.type;
                sk.category = r.cat;
                sk.power = r.power;
                sk.accuracy = r.acc;
                sk.maxPP = r.pp;
                sk.priority = r.priority;
                AssetDatabase.CreateAsset(sk, path);
                created++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PalRogue] SkillData 생성 {created}개 / 건너뜀 {skipped}개");
        }

        // ───────── 5. 팰에 학습 스킬 연결 ─────────
        [MenuItem("PalRogue/5. 팰에 학습 스킬 자동 연결")]
        static void FillLearnsets()
        {
            var basic = new Dictionary<PalType, string>();
            var strong = new Dictionary<PalType, string>();
            foreach (var r in Rows)
            {
                if (r.slot == Slot.Basic) basic[r.type] = r.eng;
                if (r.slot == Slot.Strong) strong[r.type] = r.eng;
            }

            SkillData Get(string eng) => AssetDatabase.LoadAssetAtPath<SkillData>($"{SkillDir}/{eng}.asset");

            int filled = 0, skipped = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:PalData", new[] { PalDir }))
            {
                var pal = AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(guid));
                if (pal == null) continue;
                if (pal.learnset != null && pal.learnset.Count > 0) { skipped++; continue; }

                var list = new List<LearnsetEntry>();
                void Add(int lv, string eng)
                {
                    var sk = Get(eng);
                    if (sk == null) { Debug.LogWarning($"[PalRogue] 스킬 없음: {eng} (메뉴 4번을 먼저 실행하세요)"); return; }
                    list.Add(new LearnsetEntry { level = lv, skill = sk });
                }

                var p = pal.primaryType;
                bool dual = pal.hasSecondaryType;

                Add(1, basic[p]);
                Add(1, p == PalType.Neutral ? QuickStrike : basic[PalType.Neutral]);
                if (dual) Add(8, basic[pal.secondaryType]);
                Add(10, strong[p]);
                if (dual) Add(20, strong[pal.secondaryType]);
                else if (p != PalType.Neutral) Add(20, strong[PalType.Neutral]);

                pal.learnset = list;
                EditorUtility.SetDirty(pal);
                filled++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PalRogue] 학습 스킬 연결 {filled}마리 / 건너뜀 {skipped}마리");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif