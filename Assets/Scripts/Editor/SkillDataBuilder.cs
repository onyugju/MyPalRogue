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
        const string PalDir   = "Assets/Data/Pals";
        const string AllPals  = "*";   // 모든 팰이 배우는 스킬

        const SkillCategory Atk = SkillCategory.Attack;

        // ───── 원작 쿨타임 구간 → 위력 / 명중 / PP / 습득 레벨 ─────
        struct Band { public int power, acc, pp, level; }
        static Band GetBand(int cd) => cd switch
        {
            <= 2  => new Band { power = 40,  acc = 100, pp = 25, level = 1  },
            <= 4  => new Band { power = 55,  acc = 100, pp = 20, level = 6  },
            <= 8  => new Band { power = 70,  acc = 100, pp = 15, level = 12 },
            <= 12 => new Band { power = 80,  acc = 95,  pp = 15, level = 16 },
            <= 16 => new Band { power = 90,  acc = 90,  pp = 10, level = 20 },
            <= 20 => new Band { power = 100, acc = 90,  pp = 10, level = 28 },
            <= 24 => new Band { power = 110, acc = 85,  pp = 5,  level = 34 },
            _     => new Band { power = 120, acc = 80,  pp = 5,  level = 40 },
        };

        struct Row
        {
            public PalType type; public int cd; public string kor, eng;
            public SkillCategory cat; public string owner; public int power, priority;
            // owner: null=속성 공용 / AllPals=전원 / 에셋 이름(예 "139_Anubis")=전용
            // power: 0이면 쿨타임 구간 값 사용, 아니면 이 값으로 덮어씀
            public Row(PalType type, int cd, string kor, string eng, SkillCategory cat,
                       string owner = null, int power = 0, int priority = 0)
            { this.type=type; this.cd=cd; this.kor=kor; this.eng=eng; this.cat=cat;
              this.owner=owner; this.power=power; this.priority=priority; }
        }

        // 속성별 공용 스킬은 "쿨타임 낮은 순" 5개 (Void만 3개). 순서가 학습 규칙에 쓰이므로 바꾸지 말 것.
        static readonly Row[] Rows =
        {
            // ── 무 ──
            new(PalType.Neutral, 2,  "공기 대포",   "Air_Cannon",  Atk),
            new(PalType.Neutral, 4,  "파워 샷",     "Power_Shot",  Atk),
            new(PalType.Neutral, 8,  "파워 폭탄",   "Power_Bomb",  Atk),
            new(PalType.Neutral, 20, "팰 블래스트", "Pal_Blast",   Atk),
            new(PalType.Neutral, 30, "신성 폭발",   "Holy_Burst",  Atk),
            // ── 화염 ──
            new(PalType.Fire, 2,  "파이어 샷",   "Fire_Shot",    Atk),
            new(PalType.Fire, 4,  "스피릿 파이어", "Spirit_Fire", Atk),
            new(PalType.Fire, 8,  "파이어 브레스", "Fire_Breath", Atk),
            new(PalType.Fire, 16, "플레임 판넬", "Flame_Panel",   Atk),
            new(PalType.Fire, 30, "화염구",      "Fireball",      Atk),
            // ── 물 ──
            new(PalType.Water, 2,  "워터 제트",     "Water_Jet",     Atk),
            new(PalType.Water, 4,  "버블 샷",       "Bubble_Shot",   Atk),
            new(PalType.Water, 8,  "라인 스플래시", "Line_Splash",   Atk),
            new(PalType.Water, 20, "하이드로 스트림", "Hydro_Stream", Atk),
            new(PalType.Water, 30, "물기둥 분출",   "Water_Geyser",  Atk),
            // ── 풀 ──
            new(PalType.Grass, 2,  "바람의 칼날",   "Wind_Blade",       Atk),
            new(PalType.Grass, 4,  "씨앗 기관총",   "Seed_Machine_Gun", Atk),
            new(PalType.Grass, 8,  "멀티 커터",     "Multi_Cutter",     Atk),
            new(PalType.Grass, 20, "리플렉트 리프", "Reflect_Leaf",     Atk),
            new(PalType.Grass, 30, "윈드 블래스트", "Wind_Blast",       Atk),
            // ── 번개 ──
            new(PalType.Electric, 2,  "번개 창",   "Lightning_Spear", Atk),
            new(PalType.Electric, 4,  "전기 파장", "Electric_Wave",   Atk),
            new(PalType.Electric, 8,  "라인 썬더", "Line_Thunder",    Atk),
            new(PalType.Electric, 16, "번개 일격", "Thunder_Strike",  Atk),
            new(PalType.Electric, 30, "번개 폭풍", "Thunderstorm",    Atk),
            // ── 땅 ──
            new(PalType.Ground, 2,  "머드 슛",    "Mud_Shot",    Atk),
            new(PalType.Ground, 4,  "바위 폭발",  "Rock_Burst",  Atk),
            new(PalType.Ground, 8,  "바위 대포",  "Rock_Cannon", Atk),
            new(PalType.Ground, 20, "바위 창",    "Rock_Spear",  Atk),
            new(PalType.Ground, 30, "스톤 비트",  "Stone_Beat",  Atk),
            // ── 얼음 ──
            new(PalType.Ice, 2,  "얼음 미사일",   "Ice_Missile",  Atk),
            new(PalType.Ice, 4,  "얼음 칼날",     "Ice_Blade",    Atk),
            new(PalType.Ice, 8,  "서리 낀 입김",  "Frost_Breath", Atk),
            new(PalType.Ice, 16, "아이시클 라인", "Icicle_Line",  Atk),
            new(PalType.Ice, 30, "다이아몬드 폴", "Diamond_Fall", Atk),
            // ── 용 ──
            new(PalType.Dragon, 2,  "용 대포",    "Dragon_Cannon", Atk),
            new(PalType.Dragon, 4,  "용의 파장",  "Dragon_Wave",   Atk),
            new(PalType.Dragon, 8,  "용의 숨결",  "Dragon_Breath", Atk),
            new(PalType.Dragon, 16, "빔 슬라이서", "Beam_Slicer",  Atk),
            new(PalType.Dragon, 30, "용의 운석",  "Dragon_Meteor", Atk),
            // ── 어둠 ──
            new(PalType.Dark, 2,  "다크 샷",      "Dark_Shot",    Atk),
            new(PalType.Dark, 4,  "그림자 폭발",  "Shadow_Burst", Atk),
            new(PalType.Dark, 8,  "어둠 화살",    "Dark_Arrow",   Atk),
            new(PalType.Dark, 20, "어둠의 레이저", "Dark_Laser",  Atk),
            new(PalType.Dark, 30, "다크 위스프",  "Dark_Wisp",    Atk),
            // ── Void (제로버스 전용 고유속성, 원작에 없는 오리지널) ──
            new(PalType.Void, 2,  "공허의 탄환", "Void_Shot",  Atk, power: 50),
            new(PalType.Void, 8,  "공허의 파동", "Void_Pulse", Atk, power: 85),
            new(PalType.Void, 30, "소멸",        "Annihilate", Atk, power: 130),

            // ── 팰 전용 스킬 (owner = PalData 에셋 이름) ──
            new(PalType.Neutral,  2,  "데굴데굴 솜사탕", "Rolling_Fluff",     Atk, "001_Lamball"),
            new(PalType.Neutral,  2,  "냥냥 펀치",       "Meow_Punch",        Atk, "002_Cattiva"),
            new(PalType.Ground,   4,  "파이팅 슬래시",   "Fighting_Slash",    Atk, "013_Pupperai"),
            new(PalType.Fire,     4,  "불타는 뿔",       "Burning_Horn",      Atk, "058_Arsox"),
            new(PalType.Dragon,   8,  "신비의 허리케인", "Mystic_Hurricane",  Atk, "063_Elphidran"),
            new(PalType.Dragon,   4,  "로켓 태클",       "Rocket_Tackle",     Atk, "103_Chillet"),
            new(PalType.Ground,   16, "스핀 레그 슬래시", "Spin_Leg_Slash",   Atk, "139_Anubis"),
            new(PalType.Ground,   20, "포스 드라이브",   "Force_Drive",       Atk, "139_Anubis"),
            new(PalType.Ground,   24, "그라운드 스매셔", "Ground_Smasher",    Atk, "139_Anubis"),
            new(PalType.Ground,   16, "서머솔트 스크래치", "Somersault_Scratch", Atk, "140_Sekhmet"),
            new(PalType.Ground,   20, "롤링 스크래치",   "Rolling_Scratch",   Atk, "140_Sekhmet"),
            new(PalType.Ground,   30, "작열 미사일",     "Blazing_Missile",   Atk, "184_Aegidron"),
            new(PalType.Electric, 8,  "전기 할퀴기",     "Electric_Claw",     Atk, "185_Grizzbolt"),
            new(PalType.Electric, 24, "뇌격의 중전차",   "Thunder_Tank",      Atk, "185_Grizzbolt"),
            new(PalType.Neutral,  30, "창기병 돌격",     "Lancer_Charge",     Atk, "198_Paladius"),
            new(PalType.Ice,      30, "크리스탈 윙",     "Crystal_Wing",      Atk, "200_Frostallion"),
            new(PalType.Water,    30, "타라소닉 레이저", "Tarasonic_Laser",   Atk, "201_Neptilius"),
            new(PalType.Dragon,   30, "유성 광선",       "Meteor_Beam",       Atk, "202_Jetragon"),
            new(PalType.Dark,     30, "악몽의 빛줄기",   "Nightmare_Ray",     Atk, "000_Bellanoir_Libero"),
            new(PalType.Dark,     20, "화염 왈츠",       "Flame_Waltz",       Atk, "000_Bellanoir_Libero"),

            // ── 새로 추가한 팰 20종 전용 스킬 (팰의 모습·특징에서 따온 팰로그 기술) ──
            new(PalType.Dark,     4,  "우울한 독침",     "Gloomy_Sting",      Atk, "016_Depresso"),
            new(PalType.Water,    4,  "펭귄 로켓",       "Penguin_Rocket",    Atk, "017_Pengullet"),
            new(PalType.Grass,    4,  "덩굴 난타",       "Vine_Barrage",      Atk, "023_Tanzee"),
            new(PalType.Fire,     4,  "불꽃 돌진",       "Ember_Dash",        Atk, "026_Rooby"),
            new(PalType.Ground,   8,  "멧돼지 돌진",     "Boar_Rush",         Atk, "028_Rushoar"),
            new(PalType.Neutral,  4,  "송곳니 습격",     "Fang_Raid",         Atk, "033_Direhowl"),
            new(PalType.Electric, 4,  "정전기 할퀴기",   "Static_Scratch",    Atk, "042_Sparkit"),
            new(PalType.Water,    8,  "서핑 슬래시",     "Surf_Slash",        Atk, "075_Surfent"),
            new(PalType.Grass,    8,  "명궁의 화살",     "Hawkeye_Arrow",     Atk, "076_Robinquill"),
            new(PalType.Dark,     16, "지옥의 급강하",   "Hell_Dive",         Atk, "080_Helzephyr"),
            new(PalType.Grass,    16, "꽃잎 폭풍",       "Petal_Storm",       Atk, "084_Dinossom"),
            new(PalType.Fire,     16, "화염 질주",       "Blaze_Gallop",      Atk, "093_Pyrin"),
            new(PalType.Ice,      8,  "얼음 꼬리",       "Frost_Tail",        Atk, "095_Foxcicle"),
            new(PalType.Electric, 16, "점핑 클로",       "Jumping_Claw",      Atk, "098_Rayhound"),
            new(PalType.Grass,    20, "판다 라리아트",   "Panda_Lariat",      Atk, "102_Mossanda"),
            new(PalType.Fire,     20, "지옥불 할퀴기",   "Inferno_Claw",      Atk, "112_Blazehowl"),
            new(PalType.Dark,     16, "망령의 낫",       "Phantom_Scythe",    Atk, "117_Maraith"),
            new(PalType.Water,    30, "대해일",          "Tidal_Wave",        Atk, "121_Jormuntide"),
            new(PalType.Dragon,   20, "용의 급강하",     "Dragon_Dive",       Atk, "124_Quivern"),
            new(PalType.Ice,      24, "빙산 낙하",       "Iceberg_Drop",      Atk, "134_Wumpo"),

            // ── 오리지널: 선제기 (전원 Lv1) ──
            new(PalType.Neutral,  2,  "기습",            "Quick_Strike",      Atk, AllPals, power: 35, priority: 1),
        };

        // ───────── 34. 팰 전용 기술 추가 ─────────
        /// <summary>전용 기술의 설명과 추가 효과 (상태이상 / 능력치 변화). 기술 에셋 이름 → 내용</summary>
        static readonly Dictionary<string, (string desc, StatusCondition status, int statusChance, StatChange? change, int changeChance)> SignatureEffects =
            new Dictionary<string, (string, StatusCondition, int, StatChange?, int)>
        {
            ["Gloomy_Sting"]   = ("축 처진 몸으로 독침을 찌른다. 30% 확률로 독.", StatusCondition.Poison, 30, null, 0),
            ["Penguin_Rocket"] = ("몸을 로켓처럼 날려 부딪친다. 20% 확률로 상대 스피드 -1.", StatusCondition.None, 0, new StatChange(StatType.Speed, -1, false), 20),
            ["Vine_Barrage"]   = ("덩굴을 휘둘러 마구 때린다.", StatusCondition.None, 0, null, 0),
            ["Ember_Dash"]     = ("불꽃을 두르고 돌진한다. 20% 확률로 화상.", StatusCondition.Burn, 20, null, 0),
            ["Boar_Rush"]      = ("흙먼지를 일으키며 들이받는다. 30% 확률로 상대 방어 -1.", StatusCondition.None, 0, new StatChange(StatType.Defense, -1, false), 30),
            ["Fang_Raid"]      = ("날카로운 송곳니로 덮친다. 20% 확률로 자신의 스피드 +1.", StatusCondition.None, 0, new StatChange(StatType.Speed, 1, true), 20),
            ["Static_Scratch"] = ("정전기가 흐르는 발톱으로 할퀸다. 30% 확률로 마비.", StatusCondition.Paralysis, 30, null, 0),
            ["Surf_Slash"]     = ("파도를 타고 베어 낸다.", StatusCondition.None, 0, null, 0),
            ["Hawkeye_Arrow"]  = ("빈틈을 노려 화살 세 발을 쏜다. 20% 확률로 상대 방어 -1.", StatusCondition.None, 0, new StatChange(StatType.Defense, -1, false), 20),
            ["Hell_Dive"]      = ("하늘 높이 올라 지옥의 기운과 함께 내리꽂힌다. 20% 확률로 독.", StatusCondition.Poison, 20, null, 0),
            ["Petal_Storm"]    = ("꽃잎 폭풍을 일으킨다. 30% 확률로 상대 공격 -1.", StatusCondition.None, 0, new StatChange(StatType.Attack, -1, false), 30),
            ["Blaze_Gallop"]   = ("온몸을 불태우며 질주한다. 20% 확률로 화상.", StatusCondition.Burn, 20, null, 0),
            ["Frost_Tail"]     = ("얼어붙은 꼬리로 후려친다. 15% 확률로 얼음.", StatusCondition.Freeze, 15, null, 0),
            ["Jumping_Claw"]   = ("높이 뛰어올라 번개 발톱으로 내려친다. 20% 확률로 마비.", StatusCondition.Paralysis, 20, null, 0),
            ["Panda_Lariat"]   = ("굵은 팔을 휘둘러 후려친다. 30% 확률로 상대 방어 -1.", StatusCondition.None, 0, new StatChange(StatType.Defense, -1, false), 30),
            ["Inferno_Claw"]   = ("지옥불을 두른 발톱으로 할퀸다. 30% 확률로 화상.", StatusCondition.Burn, 30, null, 0),
            ["Phantom_Scythe"] = ("망령의 낫으로 영혼을 벤다. 20% 확률로 상대 공격 -1.", StatusCondition.None, 0, new StatChange(StatType.Attack, -1, false), 20),
            ["Tidal_Wave"]     = ("거대한 해일로 모든 것을 삼킨다. 30% 확률로 상대 스피드 -1.", StatusCondition.None, 0, new StatChange(StatType.Speed, -1, false), 30),
            ["Dragon_Dive"]    = ("하늘에서 용의 힘을 싣고 내리꽂힌다. 20% 확률로 자신의 공격 +1.", StatusCondition.None, 0, new StatChange(StatType.Attack, 1, true), 20),
            ["Iceberg_Drop"]   = ("거대한 빙산을 떨어뜨린다. 15% 확률로 얼음.", StatusCondition.Freeze, 15, null, 0),
        };

        /// <summary>
        /// 메뉴 PalRogue/34. 팰 전용 기술 추가
        ///  ① 표(Rows)에 있는데 아직 없는 기술 에셋을 만든다 (메뉴 4와 같음)
        ///  ② 전용 기술에 설명·추가 효과를 넣는다
        ///  ③ 이미 만든 팰의 학습 목록에 그 팰의 전용 기술이 없으면 더한다 (원래 습득 레벨로)
        /// 여러 번 실행해도 안전하다. 그림이 아직 없는 팰은 나중에 추가될 때 메뉴 5 규칙으로 전용 기술을 같이 배운다.
        /// </summary>
        [MenuItem("PalRogue/34. 팰 전용 기술 추가 (새 팰 20종)")]
        public static void AddSignatureSkills()
        {
            CreateSkills();
            int effects = 0;
            foreach (var kv in SignatureEffects)
            {
                var sk = AssetDatabase.LoadAssetAtPath<SkillData>($"{SkillDir}/{kv.Key}.asset");
                if (sk == null) continue;
                var e = kv.Value;
                sk.description = e.desc;
                sk.statusEffect = e.status;
                sk.statusChance = e.statusChance;
                sk.statChanges = e.change.HasValue ? new List<StatChange> { e.change.Value } : new List<StatChange>();
                sk.statChangeChance = e.change.HasValue ? e.changeChance : 100;
                EditorUtility.SetDirty(sk);
                effects++;
            }

            int added = 0;
            var names = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:PalData", new[] { PalDir }))
            {
                var pal = AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(guid));
                if (pal == null) continue;
                pal.learnset ??= new List<LearnsetEntry>();
                bool changed = false;
                foreach (var r in Rows)
                {
                    if (r.owner != pal.name) continue;
                    var sk = AssetDatabase.LoadAssetAtPath<SkillData>($"{SkillDir}/{r.eng}.asset");
                    if (sk == null || pal.learnset.Any(e => e.skill == sk)) continue;
                    pal.learnset.Add(new LearnsetEntry { level = GetBand(r.cd).level, skill = sk });
                    names.Add($"{pal.palName}:{r.kor}");
                    added++;
                    changed = true;
                }
                if (!changed) continue;
                pal.learnset = pal.learnset.OrderBy(e => e.level).ThenBy(e => e.skill != null ? e.skill.name : "").ToList();
                EditorUtility.SetDirty(pal);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[전용 기술] 기술 {effects}개에 설명·추가 효과, 학습 목록에 {added}개 추가" +
                      (names.Count > 0 ? $" ({string.Join(", ", names)})" : "") + ". (메뉴 22로 연출 확인)");
        }

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
        public static void CreateSkills()
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
                ApplyDefaultStatus(sk);
                AssetDatabase.CreateAsset(sk, path);
                created++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PalRogue] SkillData 생성 {created}개 / 건너뜀 {skipped}개");
        }

        // ───────── 5. 팰에 학습 스킬 연결 ─────────
        [MenuItem("PalRogue/5. 팰에 학습 스킬 자동 연결")]
        public static void FillLearnsets()
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

        // ───────── 12. 스킬 종류 통합 (이미 만든 스킬 에셋용) ─────────
        /// <summary>예전 '물리/특수' 값을 가진 스킬 에셋을 전부 '공격'으로 바꾼다 (변화 스킬은 그대로)</summary>
        [MenuItem("PalRogue/12. 스킬 종류 통합 적용 (물리·특수→공격)", false, 1012)]
        static void MergeCategories()
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:SkillData", new[] { SkillDir }))
            {
                var sk = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (sk == null) continue;
                if (sk.category == SkillCategory.Attack || sk.category == SkillCategory.Status) continue;
                sk.category = SkillCategory.Attack;      // 예전 값(물리=0은 그대로 공격, 특수=1은 여기서 정리)
                EditorUtility.SetDirty(sk);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PalRogue] 스킬 종류 통합: {changed}개를 '공격'으로 정리했습니다.");
        }

        // ───────── 13. 스킬 추가 효과(상태이상) 기본값 ─────────
        const int DefaultStatusChance = 10;   // 공격 스킬의 상태이상 확률(%)

        /// <summary>불→화상, 얼음→얼음, 번개→마비, 어둠→독 (공격 스킬, 아직 효과가 없는 것만)</summary>
        static bool ApplyDefaultStatus(SkillData sk)
        {
            if (sk.category != SkillCategory.Attack || sk.statusEffect != StatusCondition.None) return false;
            var s = StatusRules.DefaultForSkillType(sk.type);
            if (s == StatusCondition.None) return false;
            sk.statusEffect = s;
            sk.statusChance = DefaultStatusChance;
            return true;
        }

        [MenuItem("PalRogue/13. 스킬 추가 효과(상태이상) 기본값 적용", false, 1013)]
        static void ApplyDefaultStatuses()
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:SkillData", new[] { SkillDir }))
            {
                var sk = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (sk == null || !ApplyDefaultStatus(sk)) continue;
                EditorUtility.SetDirty(sk);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PalRogue] 스킬 {changed}개에 상태이상 추가 효과({DefaultStatusChance}%)를 넣었습니다. (이미 효과가 있는 스킬은 그대로)");
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
