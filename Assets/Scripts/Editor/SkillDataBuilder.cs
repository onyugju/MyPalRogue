#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/6 → 기존 스킬 에셋 삭제 + 모든 팰 학습 목록 비우기 (재생성용)
    /// 메뉴 PalRogue/4 → Assets/Data/Skills 에 스킬 생성
    /// 메뉴 PalRogue/5 → 학습 목록이 빈 팰에 스킬 자동 연결
    ///
    /// 스킬 이름/속성은 팰월드 원작 참고, 수치(위력·명중·PP·습득 레벨)는
    /// 아래 GetBand 표로 우리가 새로 정한 값이다. (원작 쿨타임 → 포켓몬식 PP)
    /// </summary>
    public static class SkillDataBuilder
    {
        const string SkillDir = "Assets/Data/Skills";
        const string PalDir = "Assets/Data/Pals";
        const string AllPals = "*";   // 모든 팰이 배우는 스킬

        const SkillCategory Phys = SkillCategory.Physical;
        const SkillCategory Spec = SkillCategory.Special;

        // ───── 원작 쿨타임 구간 → 위력 / 명중 / PP / 습득 레벨 ─────
        struct Band { public int power, acc, pp, level; }
        static Band GetBand(int cd) => cd switch
        {
            <= 2 => new Band { power = 40, acc = 100, pp = 25, level = 1 },
            <= 4 => new Band { power = 55, acc = 100, pp = 20, level = 6 },
            <= 8 => new Band { power = 70, acc = 100, pp = 15, level = 12 },
            <= 12 => new Band { power = 80, acc = 95, pp = 15, level = 16 },
            <= 16 => new Band { power = 90, acc = 90, pp = 10, level = 20 },
            <= 20 => new Band { power = 100, acc = 90, pp = 10, level = 28 },
            <= 24 => new Band { power = 110, acc = 85, pp = 5, level = 34 },
            _ => new Band { power = 120, acc = 80, pp = 5, level = 40 },
        };

        struct Row
        {
            public PalType type; public int cd; public string kor, eng;
            public SkillCategory cat; public string owner; public int power, priority;
            // owner: null=속성 공용 / AllPals=전원 / 에셋 이름(예 "139_Anubis")=전용
            // power: 0이면 쿨타임 구간 값 사용, 아니면 이 값으로 덮어씀
            public Row(PalType type, int cd, string kor, string eng, SkillCategory cat,
                       string owner = null, int power = 0, int priority = 0)
            {
                this.type = type; this.cd = cd; this.kor = kor; this.eng = eng; this.cat = cat;
                this.owner = owner; this.power = power; this.priority = priority;
            }
        }

        // 속성별 공용 스킬은 "쿨타임 낮은 순" 5개 (Void만 3개). 순서가 학습 규칙에 쓰이므로 바꾸지 말 것.
        static readonly Row[] Rows =
        {
            // ── 무 ──
            new(PalType.Neutral, 2,  "공기 대포",   "Air_Cannon",  Spec),
            new(PalType.Neutral, 4,  "파워 샷",     "Power_Shot",  Spec),
            new(PalType.Neutral, 8,  "파워 폭탄",   "Power_Bomb",  Spec),
            new(PalType.Neutral, 20, "팰 블래스트", "Pal_Blast",   Spec),
            new(PalType.Neutral, 30, "신성 폭발",   "Holy_Burst",  Spec),
            // ── 화염 ──
            new(PalType.Fire, 2,  "파이어 샷",   "Fire_Shot",    Spec),
            new(PalType.Fire, 4,  "스피릿 파이어", "Spirit_Fire", Spec),
            new(PalType.Fire, 8,  "파이어 브레스", "Fire_Breath", Spec),
            new(PalType.Fire, 16, "플레임 판넬", "Flame_Panel",   Spec),
            new(PalType.Fire, 30, "화염구",      "Fireball",      Spec),
            // ── 물 ──
            new(PalType.Water, 2,  "워터 제트",     "Water_Jet",     Spec),
            new(PalType.Water, 4,  "버블 샷",       "Bubble_Shot",   Spec),
            new(PalType.Water, 8,  "라인 스플래시", "Line_Splash",   Spec),
            new(PalType.Water, 20, "하이드로 스트림", "Hydro_Stream", Spec),
            new(PalType.Water, 30, "물기둥 분출",   "Water_Geyser",  Spec),
            // ── 풀 ──
            new(PalType.Grass, 2,  "바람의 칼날",   "Wind_Blade",       Spec),
            new(PalType.Grass, 4,  "씨앗 기관총",   "Seed_Machine_Gun", Spec),
            new(PalType.Grass, 8,  "멀티 커터",     "Multi_Cutter",     Spec),
            new(PalType.Grass, 20, "리플렉트 리프", "Reflect_Leaf",     Spec),
            new(PalType.Grass, 30, "윈드 블래스트", "Wind_Blast",       Spec),
            // ── 번개 ──
            new(PalType.Electric, 2,  "번개 창",   "Lightning_Spear", Spec),
            new(PalType.Electric, 4,  "전기 파장", "Electric_Wave",   Spec),
            new(PalType.Electric, 8,  "라인 썬더", "Line_Thunder",    Spec),
            new(PalType.Electric, 16, "번개 일격", "Thunder_Strike",  Spec),
            new(PalType.Electric, 30, "번개 폭풍", "Thunderstorm",    Spec),
            // ── 땅 ──
            new(PalType.Ground, 2,  "머드 슛",    "Mud_Shot",    Spec),
            new(PalType.Ground, 4,  "바위 폭발",  "Rock_Burst",  Phys),
            new(PalType.Ground, 8,  "바위 대포",  "Rock_Cannon", Phys),
            new(PalType.Ground, 20, "바위 창",    "Rock_Spear",  Phys),
            new(PalType.Ground, 30, "스톤 비트",  "Stone_Beat",  Phys),
            // ── 얼음 ──
            new(PalType.Ice, 2,  "얼음 미사일",   "Ice_Missile",  Spec),
            new(PalType.Ice, 4,  "얼음 칼날",     "Ice_Blade",    Spec),
            new(PalType.Ice, 8,  "서리 낀 입김",  "Frost_Breath", Spec),
            new(PalType.Ice, 16, "아이시클 라인", "Icicle_Line",  Phys),
            new(PalType.Ice, 30, "다이아몬드 폴", "Diamond_Fall", Spec),
            // ── 용 ──
            new(PalType.Dragon, 2,  "용 대포",    "Dragon_Cannon", Spec),
            new(PalType.Dragon, 4,  "용의 파장",  "Dragon_Wave",   Spec),
            new(PalType.Dragon, 8,  "용의 숨결",  "Dragon_Breath", Spec),
            new(PalType.Dragon, 16, "빔 슬라이서", "Beam_Slicer",  Spec),
            new(PalType.Dragon, 30, "용의 운석",  "Dragon_Meteor", Spec),
            // ── 어둠 ──
            new(PalType.Dark, 2,  "다크 샷",      "Dark_Shot",    Spec),
            new(PalType.Dark, 4,  "그림자 폭발",  "Shadow_Burst", Spec),
            new(PalType.Dark, 8,  "어둠 화살",    "Dark_Arrow",   Spec),
            new(PalType.Dark, 20, "어둠의 레이저", "Dark_Laser",  Spec),
            new(PalType.Dark, 30, "다크 위스프",  "Dark_Wisp",    Spec),
            // ── Void (제로버스 전용 고유속성, 원작에 없는 오리지널) ──
            new(PalType.Void, 2,  "공허의 탄환", "Void_Shot",  Spec, power: 50),
            new(PalType.Void, 8,  "공허의 파동", "Void_Pulse", Spec, power: 85),
            new(PalType.Void, 30, "소멸",        "Annihilate", Spec, power: 130),

            // ── 팰 전용 스킬 (owner = PalData 에셋 이름) ──
            new(PalType.Neutral,  2,  "데굴데굴 솜사탕", "Rolling_Fluff",     Phys, "001_Lamball"),
            new(PalType.Neutral,  2,  "냥냥 펀치",       "Meow_Punch",        Phys, "002_Cattiva"),
            new(PalType.Ground,   4,  "파이팅 슬래시",   "Fighting_Slash",    Phys, "013_Pupperai"),
            new(PalType.Fire,     4,  "불타는 뿔",       "Burning_Horn",      Phys, "058_Arsox"),
            new(PalType.Dragon,   8,  "신비의 허리케인", "Mystic_Hurricane",  Spec, "063_Elphidran"),
            new(PalType.Dragon,   4,  "로켓 태클",       "Rocket_Tackle",     Phys, "103_Chillet"),
            new(PalType.Ground,   16, "스핀 레그 슬래시", "Spin_Leg_Slash",   Phys, "139_Anubis"),
            new(PalType.Ground,   20, "포스 드라이브",   "Force_Drive",       Phys, "139_Anubis"),
            new(PalType.Ground,   24, "그라운드 스매셔", "Ground_Smasher",    Phys, "139_Anubis"),
            new(PalType.Ground,   16, "서머솔트 스크래치", "Somersault_Scratch", Phys, "140_Sekhmet"),
            new(PalType.Ground,   20, "롤링 스크래치",   "Rolling_Scratch",   Phys, "140_Sekhmet"),
            new(PalType.Ground,   30, "작열 미사일",     "Blazing_Missile",   Spec, "184_Aegidron"),
            new(PalType.Electric, 8,  "전기 할퀴기",     "Electric_Claw",     Phys, "185_Grizzbolt"),
            new(PalType.Electric, 24, "뇌격의 중전차",   "Thunder_Tank",      Spec, "185_Grizzbolt"),
            new(PalType.Neutral,  30, "창기병 돌격",     "Lancer_Charge",     Phys, "198_Paladius"),
            new(PalType.Ice,      30, "크리스탈 윙",     "Crystal_Wing",      Phys, "200_Frostallion"),
            new(PalType.Water,    30, "타라소닉 레이저", "Tarasonic_Laser",   Spec, "201_Neptilius"),
            new(PalType.Dragon,   30, "유성 광선",       "Meteor_Beam",       Spec, "202_Jetragon"),
            new(PalType.Dark,     30, "악몽의 빛줄기",   "Nightmare_Ray",     Spec, "000_Bellanoir_Libero"),
            new(PalType.Dark,     20, "화염 왈츠",       "Flame_Waltz",       Spec, "000_Bellanoir_Libero"),

            // ── 오리지널: 선제기 (전원 Lv1) ──
            new(PalType.Neutral,  2,  "기습",            "Quick_Strike",      Phys, AllPals, power: 35, priority: 1),
        };

        // ───────── 6. 초기화 ─────────
        [MenuItem("PalRogue/6. 스킬·학습목록 초기화 (재생성용)")]
        static void ResetAll()
        {
            if (!EditorUtility.DisplayDialog("스킬 초기화",
                "Assets/Data/Skills 의 스킬 에셋을 전부 삭제하고,\n모든 팰의 학습 스킬 목록을 비웁니다.\n\n계속할까요?",
                "초기화", "취소")) return;

            foreach (var guid in AssetDatabase.FindAssets("t:PalData", new[] { PalDir }))
            {
                var pal = AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(guid));
                if (pal == null) continue;
                pal.learnset = new List<LearnsetEntry>();
                EditorUtility.SetDirty(pal);
            }
            AssetDatabase.SaveAssets();
            if (AssetDatabase.IsValidFolder(SkillDir)) AssetDatabase.DeleteAsset(SkillDir);
            AssetDatabase.Refresh();
            Debug.Log("[PalRogue] 스킬 에셋 삭제 + 학습 목록 초기화 완료. 이제 메뉴 4번 → 5번을 실행하세요.");
        }

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

                var band = GetBand(r.cd);
                var sk = ScriptableObject.CreateInstance<SkillData>();
                sk.skillName = r.kor;
                sk.description = "";
                sk.type = r.type;
                sk.category = r.cat;
                sk.power = r.power > 0 ? r.power : band.power;
                sk.accuracy = band.acc;
                sk.maxPP = band.pp;
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
            // 속성별 공용 스킬 (Rows 순서 유지)
            var generic = new Dictionary<PalType, List<Row>>();
            foreach (var r in Rows)
            {
                if (r.owner != null) continue;
                if (!generic.TryGetValue(r.type, out var l)) generic[r.type] = l = new List<Row>();
                l.Add(r);
            }

            SkillData Get(string eng) => AssetDatabase.LoadAssetAtPath<SkillData>($"{SkillDir}/{eng}.asset");

            int filled = 0, skipped = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:PalData", new[] { PalDir }))
            {
                var pal = AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(guid));
                if (pal == null) continue;
                if (pal.learnset != null && pal.learnset.Count > 0) { skipped++; continue; }

                var learn = new Dictionary<string, int>();   // 스킬 ID → 습득 레벨 (중복 시 낮은 레벨)
                void Put(string eng, int level)
                {
                    if (!learn.TryGetValue(eng, out var old) || level < old) learn[eng] = level;
                }
                void PutRow(Row row) => Put(row.eng, GetBand(row.cd).level);

                // 1) 전원 스킬 / 이 팰 전용 스킬
                foreach (var r in Rows)
                {
                    if (r.owner == AllPals) Put(r.eng, 1);
                    else if (r.owner == pal.name) PutRow(r);
                }

                // 2) 주 속성 공용 스킬 5개(Void는 3개) 전부
                var p = pal.primaryType;
                if (generic.TryGetValue(p, out var main)) foreach (var r in main) PutRow(r);

                // 3) 무속성 보조 (공기 대포, 파워 폭탄). 무/Void 속성은 제외
                if (p != PalType.Neutral && p != PalType.Void && generic.TryGetValue(PalType.Neutral, out var neutral))
                {
                    PutRow(neutral[0]);
                    PutRow(neutral[2]);
                }

                // 4) 보조 속성이 있으면 그 속성의 2번째/4번째 스킬
                if (pal.hasSecondaryType && generic.TryGetValue(pal.secondaryType, out var sub) && sub.Count > 3)
                {
                    PutRow(sub[1]);
                    PutRow(sub[3]);
                }

                var list = new List<LearnsetEntry>();
                foreach (var kv in learn.OrderBy(k => k.Value).ThenBy(k => k.Key))
                {
                    var sk = Get(kv.Key);
                    if (sk == null) { Debug.LogWarning($"[PalRogue] 스킬 없음: {kv.Key} (메뉴 4번을 먼저 실행하세요)"); continue; }
                    list.Add(new LearnsetEntry { level = kv.Value, skill = sk });
                }
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