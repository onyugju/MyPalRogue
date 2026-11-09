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

        // 기술 표는 PalworldSkillTable (팰월드 기술: 공용기 + 우리 팰 전용기). 공허 셋은 제로버스 데이터용 (실전에서는 AstralymPhase 기술을 쓴다)
        static readonly Row[] VoidRows =
        {
            new(PalType.Void, 2,  "공허의 탄환", "Void_Shot",  Atk, power: 50),
            new(PalType.Void, 8,  "공허의 파동", "Void_Pulse", Atk, power: 85),
            new(PalType.Void, 30, "소멸",        "Annihilate", Atk, power: 130),
        };

        static readonly Row[] Rows = PalworldSkillTable.All
            .Select(e => new Row(e.type, e.cd, e.kor, e.eng, e.cat, e.owner, e.power, e.priority))
            .Concat(VoidRows).ToArray();

        // ───────── 34. 팰 전용 기술 추가 ─────────
        /// <summary>전용 기술의 설명과 추가 효과 (상태이상 / 능력치 변화). 기술 에셋 이름 → 내용</summary>
        static readonly Dictionary<string, (string desc, StatusCondition status, int statusChance, StatChange? change, int changeChance)> SignatureEffects =
            new Dictionary<string, (string, StatusCondition, int, StatChange?, int)>();   // 팰월드 표(PalworldSkillTable)가 설명·추가 효과를 갖고 있다

        /// <summary>
        /// 메뉴 PalRogue/34. 팰 전용 기술 추가
        ///  ① 표(Rows)에 있는데 아직 없는 기술 에셋을 만든다 (메뉴 4와 같음)
        ///  ② 전용 기술에 설명·추가 효과를 넣는다
        ///  ③ 이미 만든 팰의 학습 목록에 그 팰의 전용 기술이 없으면 더한다 (원래 습득 레벨로)
        /// 여러 번 실행해도 안전하다. 그림이 아직 없는 팰은 나중에 추가될 때 메뉴 5 규칙으로 전용 기술을 같이 배운다.
        /// </summary>
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
                      (names.Count > 0 ? $" ({string.Join(", ", names)})" : "") + ". (메뉴 12로 연출 확인)");
        }

        // ───────── 6. 초기화 ─────────
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

                // 2) 주 속성 공용 기술: 습득 단계(레벨)마다 하나씩 (팰마다 다르게 고른다)
                var p = pal.primaryType;
                if (generic.TryGetValue(p, out var main))
                {
                    if (p == PalType.Void) foreach (var r in main) PutRow(r);
                    else foreach (var tier in main.GroupBy(r => GetBand(r.cd).level)) PutRow(Pick(tier.ToList(), pal.name + tier.Key));
                }

                // 3) 무속성 공용 기술 둘 (무/공허 속성은 제외): 1단계 하나 + 12레벨 단계 하나
                if (p != PalType.Neutral && p != PalType.Void && generic.TryGetValue(PalType.Neutral, out var neutral))
                {
                    foreach (int lvTier in new[] { 1, 12 })
                    {
                        var pool = neutral.Where(r => GetBand(r.cd).level == lvTier).ToList();
                        if (pool.Count > 0) PutRow(Pick(pool, pal.name + "N" + lvTier));
                    }
                }

                // 4) 보조 속성 공용 기술 둘: 12레벨 단계 하나 + 28레벨 단계 하나
                if (pal.hasSecondaryType && pal.secondaryType != p && generic.TryGetValue(pal.secondaryType, out var sub))
                {
                    foreach (int lvTier in new[] { 12, 28 })
                    {
                        var pool = sub.Where(r => GetBand(r.cd).level == lvTier).ToList();
                        if (pool.Count > 0) PutRow(Pick(pool, pal.name + "S" + lvTier));
                    }
                }

                var list = new List<LearnsetEntry>();
                foreach (var kv in learn.OrderBy(k => k.Value).ThenBy(k => k.Key))
                {
                    var sk = Get(kv.Key);
                    if (sk == null) { Debug.LogWarning($"[PalRogue] 스킬 없음: {kv.Key} (메뉴 01을 먼저 실행하세요)"); continue; }
                    list.Add(new LearnsetEntry { level = kv.Value, skill = sk });
                }
                pal.learnset = list;
                EditorUtility.SetDirty(pal);
                filled++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PalRogue] 학습 스킬 연결 {filled}마리 / 건너뜀 {skipped}마리");
        }

        /// <summary>팰 이름으로 정해지는 고르기 (같은 팰은 늘 같은 기술, 팰마다는 다르게)</summary>
        static Row Pick(List<Row> pool, string seed)
        {
            uint h = 2166136261;
            foreach (char c in seed) { h ^= c; h *= 16777619; }
            return pool[(int)(h % (uint)pool.Count)];
        }

        // ───────── 43. 팰월드 기술로 전면 교체 ─────────
        /// <summary>
        /// 메뉴 PalRogue/43. 기술 표(PalworldSkillTable)대로 기술 에셋을 만들거나 고치고, 표에 없는 옛 기술 에셋은 지운 뒤
        /// 모든 팰의 학습 목록을 새 규칙으로 다시 만든다 (변화 기술 SupportSkillBuilder 포함). 여러 번 실행해도 같은 결과.
        /// 저장된 모험에 지운 기술이 있으면 불러올 때 자동으로 다시 배운다.
        /// </summary>
        [MenuItem("PalRogue/43. 팰월드 기술로 전면 교체", false, 230)]
        public static void RebuildPalworld()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(SkillDir);
            int created = 0, updated = 0, removed = 0;
            foreach (var e in PalworldSkillTable.All)
            {
                string path = $"{SkillDir}/{e.eng}.asset";
                var sk = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                bool isNew = sk == null;
                if (isNew) sk = ScriptableObject.CreateInstance<SkillData>();
                sk.skillName = e.kor;
                sk.description = e.desc;
                sk.type = e.type;
                sk.category = e.cat;
                sk.power = e.power;
                sk.accuracy = e.acc;
                sk.maxPP = e.pp;
                sk.priority = e.priority;
                sk.statusEffect = e.status;
                sk.statusChance = e.statusChance;
                sk.statChanges = e.changes.ToList();
                sk.statChangeChance = e.changeChance;
                sk.healPercent = e.heal;
                ApplyDefaultStatus(sk);
                if (isNew) { AssetDatabase.CreateAsset(sk, path); created++; }
                else { EditorUtility.SetDirty(sk); updated++; }
            }
            CreateSkills();                                                    // 공허 기술 (제로버스 데이터용)

            var keep = new HashSet<string>(PalworldSkillTable.All.Select(e => e.eng));
            foreach (var r in VoidRows) keep.Add(r.eng);
            foreach (var sp in SupportSkillBuilder.Supports) keep.Add(sp.eng);
            foreach (var guid in AssetDatabase.FindAssets("t:SkillData", new[] { SkillDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (keep.Contains(Path.GetFileNameWithoutExtension(path))) continue;
                AssetDatabase.DeleteAsset(path);
                removed++;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:PalData", new[] { PalDir }))
            {
                var pal = AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(guid));
                if (pal == null) continue;
                pal.learnset = new List<LearnsetEntry>();
                EditorUtility.SetDirty(pal);
            }
            AssetDatabase.SaveAssets();
            FillLearnsets();
            SupportSkillBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log($"[팰월드 기술] 새로 {created}개 / 고침 {updated}개 / 옛 기술 지움 {removed}개, 모든 팰 학습 목록을 다시 만들었습니다.");
        }

        // ───────── 12. 스킬 종류 통합 (이미 만든 스킬 에셋용) ─────────
        /// <summary>예전 '물리/특수' 값을 가진 스킬 에셋을 전부 '공격'으로 바꾼다 (변화 스킬은 그대로)</summary>
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
