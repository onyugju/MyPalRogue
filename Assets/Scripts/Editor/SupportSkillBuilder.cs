#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/26. 버프·디버프 기술 추가
    ///  ① 속성마다 변화 기술 1개씩(10종)을 Assets/Data/Skills 에 만든다 (이미 있으면 내용만 맞춘다)
    ///  ② 이미 있는 팰의 학습 목록에 더한다: 주 속성 기술은 Lv.10, 보조 속성 기술은 Lv.22 (이미 있으면 건너뜀)
    ///  ③ 몇몇 공격 기술에 능력치 변화 추가 효과를 붙인다 (머드 슛: 30% 상대 스피드 -1 등)
    /// 여러 번 실행해도 안전하다. 메뉴 6(초기화) → 4 → 5 로 스킬을 다시 만들었다면 이 메뉴도 다시 실행할 것.
    /// 이름은 원작 팰월드에 없는 팰로그 오리지널이다 (원작은 쿨타임 방식이라 변화 기술이 거의 없음).
    /// </summary>
    public static class SupportSkillBuilder
    {
        const string SkillDir = "Assets/Data/Skills";
        const string PalDir = "Assets/Data/Pals";
        public const int PrimaryLevel = 10, SecondaryLevel = 22;

        public struct Support
        {
            public PalType type;
            public string eng, kor, desc;
            public int pp, heal;
            public StatChange[] changes;
        }

        static StatChange Self(StatType s, int n) => new StatChange(s, n, true);
        static StatChange Foe(StatType s, int n) => new StatChange(s, n, false);

        public static readonly Support[] Supports =
        {
            new() { type = PalType.Neutral,  eng = "Focus_Up",       kor = "기합 모으기", pp = 15, desc = "기합을 모아 자신의 공격을 크게 올린다.",
                    changes = new[] { Self(StatType.Attack, 2) } },
            new() { type = PalType.Fire,     eng = "Burning_Spirit", kor = "투지 연소",   pp = 10, desc = "투지를 불태워 공격을 크게 올리지만 방어가 떨어진다.",
                    changes = new[] { Self(StatType.Attack, 2), Self(StatType.Defense, -1) } },
            new() { type = PalType.Water,    eng = "Aqua_Veil",      kor = "물의 베일",   pp = 10, desc = "물의 막을 둘러 방어를 올리고 최대 HP의 1/4을 회복한다.",
                    changes = new[] { Self(StatType.Defense, 1) }, heal = 25 },
            new() { type = PalType.Grass,    eng = "Photosynthesis", kor = "광합성",      pp = 5,  desc = "햇빛을 받아 최대 HP의 절반을 회복한다.",
                    changes = new StatChange[0], heal = 50 },
            new() { type = PalType.Electric, eng = "Charge_Up",      kor = "전기 충전",   pp = 15, desc = "몸에 전기를 모아 스피드를 크게 올린다.",
                    changes = new[] { Self(StatType.Speed, 2) } },
            new() { type = PalType.Ground,   eng = "Rock_Armor",     kor = "바위 갑옷",   pp = 15, desc = "바위로 몸을 감싸 방어를 크게 올린다.",
                    changes = new[] { Self(StatType.Defense, 2) } },
            new() { type = PalType.Ice,      eng = "Frost_Mist",     kor = "서리 안개",   pp = 15, desc = "차가운 안개로 상대의 스피드를 크게 떨어뜨린다.",
                    changes = new[] { Foe(StatType.Speed, -2) } },
            new() { type = PalType.Dragon,   eng = "Dragon_Dance",   kor = "용의 춤",     pp = 10, desc = "신비한 춤을 추어 공격과 스피드를 올린다.",
                    changes = new[] { Self(StatType.Attack, 1), Self(StatType.Speed, 1) } },
            new() { type = PalType.Dark,     eng = "Fear_Gaze",      kor = "공포의 눈빛", pp = 15, desc = "무서운 눈빛으로 노려보아 상대의 공격을 크게 떨어뜨린다.",
                    changes = new[] { Foe(StatType.Attack, -2) } },
            new() { type = PalType.Void,     eng = "Void_Erosion",   kor = "공허의 침식", pp = 10, desc = "공허가 스며들어 상대의 모든 능력치를 떨어뜨린다.",
                    changes = new[] { Foe(StatType.Attack, -1), Foe(StatType.Defense, -1), Foe(StatType.Speed, -1) } },
        };

        /// <summary>공격 기술에 붙이는 능력치 변화 추가 효과 (기술 에셋 이름, 효과, 확률%)</summary>
        public static readonly (string eng, StatChange change, int chance)[] Secondary =
        {
            ("Mud_Shot",     Foe(StatType.Speed, -1),   30),   // 머드 슛: 진흙이 묻어 느려진다
            ("Bubble_Shot",  Foe(StatType.Speed, -1),   20),   // 버블 샷
            ("Shadow_Burst", Foe(StatType.Defense, -1), 20),   // 그림자 폭발
            ("Power_Shot",   Self(StatType.Attack, 1),  20),   // 파워 샷: 쏠수록 힘이 붙는다
            ("Meteor_Beam",  Self(StatType.Attack, 1),  100),  // 유성 광선 (제트래곤 전용)
        };

        [MenuItem("PalRogue/26. 버프·디버프 기술 추가")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(SkillDir)) { Debug.LogError($"[버프 기술] {SkillDir} 폴더가 없습니다. 메뉴 4를 먼저 실행하세요."); return; }

            int created = 0, updated = 0;
            var made = new Dictionary<PalType, SkillData>();
            foreach (var sp in Supports)
            {
                string path = $"{SkillDir}/{sp.eng}.asset";
                var sk = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                bool isNew = sk == null;
                if (isNew) sk = ScriptableObject.CreateInstance<SkillData>();
                sk.skillName = sp.kor;
                sk.description = sp.desc;
                sk.type = sp.type;
                sk.category = SkillCategory.Status;
                sk.power = 0;
                sk.accuracy = 100;
                sk.maxPP = sp.pp;
                sk.priority = 0;
                sk.statusEffect = StatusCondition.None;
                sk.statusChance = 0;
                sk.statChanges = sp.changes.ToList();
                sk.statChangeChance = 100;
                sk.healPercent = sp.heal;
                if (isNew) { AssetDatabase.CreateAsset(sk, path); created++; }
                else { EditorUtility.SetDirty(sk); updated++; }
                made[sp.type] = sk;
            }

            // 학습 목록에 더하기
            int palsChanged = 0, entries = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:PalData", new[] { PalDir }))
            {
                var pal = AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(guid));
                if (pal == null) continue;
                pal.learnset ??= new List<LearnsetEntry>();
                int before = entries;
                void Add(PalType t, int level)
                {
                    if (!made.TryGetValue(t, out var sk)) return;
                    if (pal.learnset.Any(e => e.skill == sk)) return;
                    pal.learnset.Add(new LearnsetEntry { level = level, skill = sk });
                    entries++;
                }
                Add(pal.primaryType, pal.primaryType == PalType.Void ? 1 : PrimaryLevel);
                if (pal.hasSecondaryType && pal.secondaryType != pal.primaryType) Add(pal.secondaryType, SecondaryLevel);
                if (entries == before) continue;
                pal.learnset = pal.learnset.OrderBy(e => e.level).ThenBy(e => e.skill != null ? e.skill.name : "").ToList();
                EditorUtility.SetDirty(pal);
                palsChanged++;
            }

            // 공격 기술 추가 효과
            int secondary = 0;
            foreach (var (eng, change, chance) in Secondary)
            {
                var sk = AssetDatabase.LoadAssetAtPath<SkillData>($"{SkillDir}/{eng}.asset");
                if (sk == null) continue;
                sk.statChanges = new List<StatChange> { change };
                sk.statChangeChance = chance;
                EditorUtility.SetDirty(sk);
                secondary++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[버프 기술] 변화 기술 새로 {created}개 / 갱신 {updated}개, 학습 목록 {palsChanged}마리에 {entries}개 추가, " +
                      $"공격 기술 {secondary}개에 능력치 변화 추가 효과. (메뉴 27로 테스트)");
        }
    }
}
#endif
