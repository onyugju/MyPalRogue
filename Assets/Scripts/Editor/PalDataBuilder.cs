#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/1, 2번을 순서대로 실행하면
    /// Assets/Data/Pals 에 팰 25종(PalData)이 만들어진다.
    /// 이미 있는 에셋은 건드리지 않는다(직접 수정한 값 보호).
    /// </summary>
    public static class PalDataBuilder
    {
        const string PalRoot = "Assets/Resources/1.팰/1.Pal";
        const string OutDir = "Assets/Data/Pals";
        const float FrontBox = 3f;  // 앞모습을 이 크기(유닛)의 정사각형에 맞춤
        const float BackBox = 4f;

        enum Tier { Early = 300, Mid = 420, Late = 520, Boss = 620 }   // 능력치 총합
        enum Role { Balanced, Attacker, Tank, Fast }

        // HP, 공격, 방어, 속도 (합계 4.0)
        static readonly float[][] Weights =
        {
            new[] { 1.0f, 1.0f, 1.0f, 1.0f },  // Balanced
            new[] { 0.9f, 1.5f, 0.8f, 0.8f },  // Attacker
            new[] { 1.4f, 0.7f, 1.5f, 0.4f },  // Tank
            new[] { 0.7f, 1.1f, 0.6f, 1.6f },  // Fast
        };

        struct Row
        {
            public string id, kor, eng, front, back;
            public PalType t1; public PalType? t2;
            public Tier tier; public Role role; public int catchRate;
            public Row(string id, string kor, string eng, string front, string back,
                       PalType t1, PalType? t2, Tier tier, Role role, int catchRate)
            {
                this.id = id; this.kor = kor; this.eng = eng; this.front = front; this.back = back;
                this.t1 = t1; this.t2 = t2; this.tier = tier; this.role = role; this.catchRate = catchRate;
            }
        }

        // ⚠ = 속성이 확실하지 않은 팰. 팰월드 위키에서 확인 후 수정할 것.
        static readonly Row[] Rows =
        {
            new("001","도로롱","Lamball","001_Lamball","001_Lamball_back",PalType.Neutral,null,Tier.Early,Role.Tank,200),
            new("002","까부냥","Cattiva","002_Cattiva","002_Cattiva_back",PalType.Neutral,null,Tier.Early,Role.Attacker,200),
            new("004","큐룰리스","Lifmunk","004_Lifmunk","004_LIfmunk_back",PalType.Grass,null,Tier.Early,Role.Balanced,190),
            new("005","청부리","Fuack","005_Fuack","005_Fuack_back",PalType.Water,null,Tier.Early,Role.Fast,190),
            new("013","솔바둑","Pupperai","013_Pupperai","013_Pupperai_back",PalType.Ground,null,Tier.Early,Role.Balanced,170),  
            new("015","찌릿도치","Jolthog","015_Jolthog","015_Jolthog_back",PalType.Electric,null,Tier.Early,Role.Fast,190),
            new("029","파이호","Foxparks","029_Foxparks","029_Foxparks_back",PalType.Fire,null,Tier.Early,Role.Attacker,190),
            new("040","밀카우","Mozzarina","040_Mozzarina","040_Mozzarina_back",PalType.Neutral,null,Tier.Mid,Role.Tank,150),
            new("058","불페르노","Arsox","058_Arsox",null,PalType.Fire,null,Tier.Mid,Role.Attacker,120),
            new("063","실피아","Elphidran","063_Elphidran","063_Elphidran_back",PalType.Dragon,null,Tier.Mid,Role.Fast,100),
            new("079","캐티메이지","Katress","079_Katress","079_Katress_back",PalType.Dark,null,Tier.Mid,Role.Attacker,120),
            new("103","베비뇽","Chillet","103_Chillet","103_Chillet_back",PalType.Ice,PalType.Dragon,Tier.Mid,Role.Balanced,100),
            new("139","아누비스","Anubis","139_Anubis","139_Anubis_back",PalType.Ground,null,Tier.Late,Role.Attacker,70),
            new("140","세크메트","Sekhmet","140_Sekhmet","140_Sekhmet_back",PalType.Ground,null,Tier.Mid,Role.Balanced,100),  
            new("172","뇌운조","Dynamoff","172_Dynamoff","172_Dynamoff_back",PalType.Electric,null,Tier.Mid,Role.Tank,100),
            new("174","센코","Flaracle","174_Flaracle","174_Flaracle_back",PalType.Fire,null,Tier.Mid,Role.Attacker,100),
            new("184","셀가드라","Aegidron","184_Aegidron",null,PalType.Ground,null,Tier.Late,Role.Tank,60),   
            new("185","일렉판다","Grizzbolt","185_Grizzbolt","185_Grizzbolt_back",PalType.Electric,null,Tier.Late,Role.Attacker,70),
            new("186","릴린","Lyleen","186_Lyleen",null,PalType.Grass,null,Tier.Late,Role.Balanced,60),
            new("198","팔라디우스","Paladius","198_Paladius","198_Paladius_back",PalType.Neutral,null,Tier.Late,Role.Attacker,50),
            new("200","빙천마","Frostallion","200_Frostallion","200_Frostallion_back",PalType.Ice,null,Tier.Late,Role.Fast,50),
            new("201","넵티오스","Neptilius","201_Neptilius",null,PalType.Water,null,Tier.Late,Role.Fast,50),
            new("202","제트래곤","Jetragon","202_Jetragon","202_Jetragon_back",PalType.Dragon,null,Tier.Boss,Role.Fast,10),
            new("000","벨라루주","Bellanoir_Libero","000_Bellanoir Libero",null,PalType.Dark,null,Tier.Boss,Role.Attacker,5),
            new("000","제로버스","Zerobus","boss_Zerobus",null,PalType.Void,null,Tier.Boss,Role.Tank,0),   // catchRate 0 = 포획 불가

            // ── 추가 20종 (속성별 4~5종이 되도록. 원종만, 이벤트·콜라보·아종 제외) ──
            // 그림(앞모습 png 또는 애니메이션)을 폴더에 넣은 팰만 만들어진다. 나머지는 그림을 넣으면 자동으로 추가된다 (메뉴 2-1)
            new("016","뚱코알라","Depresso","016_Depresso","016_Depresso_back",PalType.Dark,null,Tier.Early,Role.Tank,190),
            new("017","펭키","Pengullet","017_Pengullet","017_Pengullet_back",PalType.Water,PalType.Ice,Tier.Early,Role.Balanced,190),
            new("023","몽지","Tanzee","023_Tanzee","023_Tanzee_back",PalType.Grass,null,Tier.Early,Role.Balanced,190),
            new("026","불꽃밤비","Rooby","026_Rooby","026_Rooby_back",PalType.Fire,null,Tier.Early,Role.Fast,190),
            new("028","돌진돼지","Rushoar","028_Rushoar","028_Rushoar_back",PalType.Ground,null,Tier.Early,Role.Tank,180),
            new("033","다크울프","Direhowl","033_Direhowl","033_Direhowl_back",PalType.Neutral,null,Tier.Mid,Role.Fast,150),
            new("042","번개냥","Sparkit","042_Sparkit","042_Sparkit_back",PalType.Electric,null,Tier.Early,Role.Attacker,190),
            new("075","씨무기","Surfent","075_Surfent","075_Surfent_back",PalType.Water,null,Tier.Mid,Role.Fast,120),
            new("076","로빈몽","Robinquill","076_Robinquill","076_Robinquill_back",PalType.Grass,null,Tier.Mid,Role.Attacker,120),
            new("080","헬가루다","Helzephyr","080_Helzephyr","080_Helzephyr_back",PalType.Dark,null,Tier.Late,Role.Fast,80),
            new("084","플로라디노","Dinossom","084_Dinossom","084_Dinossom_back",PalType.Grass,PalType.Dragon,Tier.Mid,Role.Tank,110),
            new("093","파이린","Pyrin","093_Pyrin","093_Pyrin_back",PalType.Fire,null,Tier.Mid,Role.Fast,110),
            new("095","빙호","Foxcicle","095_Foxcicle","095_Foxcicle_back",PalType.Ice,null,Tier.Mid,Role.Attacker,110),
            new("098","썬도그","Rayhound","098_Rayhound","098_Rayhound_back",PalType.Electric,null,Tier.Mid,Role.Fast,110),
            new("102","풀바오","Mossanda","102_Mossanda","102_Mossanda_back",PalType.Grass,null,Tier.Late,Role.Tank,80),
            new("112","만티파이어","Blazehowl","112_Blazehowl","112_Blazehowl_back",PalType.Fire,null,Tier.Late,Role.Attacker,70),
            new("117","고스호스","Maraith","117_Maraith","117_Maraith_back",PalType.Dark,null,Tier.Mid,Role.Attacker,110),
            new("121","레비드라","Jormuntide","121_Jormuntide","121_Jormuntide_back",PalType.Dragon,PalType.Water,Tier.Late,Role.Attacker,60),
            new("124","페스키","Quivern","124_Quivern","124_Quivern_back",PalType.Dragon,null,Tier.Late,Role.Balanced,70),
            new("134","움포","Wumpo","134_Wumpo","134_Wumpo_back",PalType.Ice,null,Tier.Late,Role.Tank,70),   
        };

        // ───────── 1. 도트 이미지 필터 보정 ─────────
        [MenuItem("PalRogue/1. 팰 스프라이트 Point 필터 적용")]
        static void ApplyPointFilter()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/1.팰" });
            int n = 0;
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
                n++;
            }
            Debug.Log($"[PalRogue] {n}개 텍스처를 Point / 무압축으로 변경했습니다.");
        }

        // ───────── 7. 이미 만든 PalData의 스프라이트 재연결 ─────────
        [MenuItem("PalRogue/7. 팰 스프라이트·배율 재연결")]
        static void RelinkSprites()
        {
            int ok = 0, fail = 0;
            foreach (var r in Rows)
            {
                var pal = AssetDatabase.LoadAssetAtPath<PalData>($"{OutDir}/{r.id}_{r.eng}.asset");
                if (pal == null) continue;

                string dir = $"{PalRoot}/{r.id} {r.kor}";
                LoadVisual(dir, r.front, "Frames", out var front, out var frontAnim);
                Sprite back = null; RuntimeAnimatorController backAnim = null;
                if (r.back != null) LoadVisual(dir, r.back, "Frames_back", out back, out backAnim);
                if (front == null) { Debug.LogWarning($"[PalRogue] 앞모습 없음: {dir}/{r.front}.png"); fail++; continue; }

                pal.frontSprite = front;
                pal.backSprite  = back;
                pal.frontAnimator = frontAnim;
                pal.backAnimator  = backAnim;
                pal.frontScale  = FitScale(front, FrontBox);
                pal.backScale   = FitScale(back, BackBox);
                PalSizing.Apply(pal);
                EditorUtility.SetDirty(pal);
                ok++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PalRogue] 스프라이트 재연결 {ok}개 / 실패 {fail}개");
        }

        // ───────── 3. 제로버스 설정 보정 (이미 만든 에셋용) ─────────
        [MenuItem("PalRogue/3. 제로버스 설정 적용 (고유속성·포획불가)")]
        static void ApplyZerobusFix()
        {
            const string path = OutDir + "/000_Zerobus.asset";
            var pal = AssetDatabase.LoadAssetAtPath<PalData>(path);
            if (pal == null) { Debug.LogWarning($"[PalRogue] 에셋을 찾을 수 없음: {path}"); return; }
            pal.primaryType = PalType.Void;
            pal.hasSecondaryType = false;
            pal.canBeCaught = false;
            EditorUtility.SetDirty(pal);
            AssetDatabase.SaveAssets();
            Debug.Log("[PalRogue] 제로버스: 속성 Void, 포획 불가로 변경했습니다.");
        }

        // ───────── 2. PalData 생성 ─────────
        [MenuItem("PalRogue/2. PalData 에셋 생성")]
        static void CreatePalAssetsMenu() => CreatePalAssets(true);

        /// <summary>그림이 있는데 아직 PalData가 없는 팰만 만든다. 만든 수를 돌려준다</summary>
        public static int CreatePalAssets(bool report)
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(OutDir);

            int created = 0, skipped = 0, missing = 0;
            var waiting = new System.Collections.Generic.List<string>();
            foreach (var r in Rows)
            {
                string assetPath = $"{OutDir}/{r.id}_{r.eng}.asset";
                if (AssetDatabase.LoadAssetAtPath<PalData>(assetPath) != null) { skipped++; continue; }

                string dir = $"{PalRoot}/{r.id} {r.kor}";
                LoadVisual(dir, r.front, "Frames", out var front, out var frontAnim);
                Sprite back = null; RuntimeAnimatorController backAnim = null;
                if (r.back != null) LoadVisual(dir, r.back, "Frames_back", out back, out backAnim);
                if (front == null) { waiting.Add(r.kor); missing++; continue; }   // 그림이 아직 없으면 만들지 않는다 (야생에 투명한 팰이 나오지 않게)

                var pal = ScriptableObject.CreateInstance<PalData>();
                pal.palId = r.id;
                pal.palName = r.kor;
                pal.englishName = r.eng.Replace('_', ' ');
                pal.frontSprite = front;
                pal.backSprite = back;
                pal.frontAnimator = frontAnim;
                pal.backAnimator = backAnim;
                pal.frontScale = FitScale(front, FrontBox);
                pal.backScale = FitScale(back, BackBox);
                pal.primaryType = r.t1;
                pal.hasSecondaryType = r.t2.HasValue;
                if (r.t2.HasValue) pal.secondaryType = r.t2.Value;
                pal.canBeCaught = r.catchRate > 0;           // 0이면 포획 불가
                pal.catchRate = Mathf.Max(1, r.catchRate);

                float total = (int)r.tier;
                var w = Weights[(int)r.role];
                int S(int i) => Mathf.Max(1, Mathf.RoundToInt(total / 6f * w[i]));
                pal.baseHP = S(0); pal.baseAttack = S(1); pal.baseDefense = S(2); pal.baseSpeed = S(3);
                PalSizing.Apply(pal);                         // 넓이·몸집 기준으로 크기 맞춤

                AssetDatabase.CreateAsset(pal, assetPath);
                created++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (report || created > 0)
                Debug.Log($"[PalRogue] PalData 생성 {created}개 / 이미 있음 {skipped}개 / 그림 기다리는 중 {missing}개" +
                          (waiting.Count > 0 ? $" ({string.Join(", ", waiting)})" : ""));
            return created;
        }

        // ───────── 2-1. 새 팰 한 번에 추가 ─────────
        // 그림을 넣은 새 팰을: PalData 만들기(2) → 학습 기술(5) → 변화 기술(26) → 애니메이션 연결(7-1) → 야생 목록에 넣기
        // 에디터가 켜지거나 스크립트가 바뀔 때도 새로 만들 팰이 있으면 자동으로 실행된다.
        [MenuItem("PalRogue/2-1. 새 팰 한 번에 추가 (그림 넣은 팰)")]
        static void AddNewPalsMenu() => AddNewPals(true);

        [InitializeOnLoadMethod]
        static void AutoAddNewPals() => EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) AddNewPals(false);
        };

        static void AddNewPals(bool report)
        {
            if (!AssetDatabase.IsValidFolder(OutDir)) return;
            PalAnimationLinker.Run(false);                     // 프레임만 넣은 팰은 애니메이션부터 만든다
            int created = CreatePalAssets(report);
            if (created == 0 && !report) return;
            SkillDataBuilder.FillLearnsets();                  // 학습 목록이 빈(새) 팰만 채운다
            if (AssetDatabase.IsValidFolder("Assets/Data/Skills")) SupportSkillBuilder.Build();
            PalAnimationLinker.Run(false);
            bool sceneChanged = RefillWildPals(out int wild);
            Debug.Log($"[PalRogue] 새 팰 {created}종 추가 완료 (학습 기술·변화 기술·애니메이션 연결). 야생 팰 목록 {wild}종" +
                      (sceneChanged ? " — 씬의 야생 팰 목록이 바뀌었으니 Ctrl+S로 저장하세요." : ""));
        }

        /// <summary>열린 씬의 전투 화면(BattleScreen) 야생 팰 목록을 모든 PalData로 다시 채운다</summary>
        static bool RefillWildPals(out int count)
        {
            count = 0;
            var screen = Object.FindAnyObjectByType<BattleScreen>();
            if (screen == null) return false;
            var all = AssetDatabase.FindAssets("t:PalData", new[] { OutDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).OrderBy(p => p.name).ToList();
            count = all.Count;
            if (screen.wildPals != null && screen.wildPals.Count == all.Count && screen.wildPals.All(all.Contains)) return false;
            Undo.RecordObject(screen, "야생 팰 목록");
            screen.wildPals = all;
            EditorUtility.SetDirty(screen);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
            return true;
        }

        // 이미지가 Multiple(자동 슬라이스)이라 서브 스프라이트를 꺼내야 한다
        // (자동 슬라이스가 본체 말고 작은 조각을 먼저 만드는 경우가 있어서 "가장 큰 스프라이트"를 고른다)
        static Sprite LoadSprite(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();

        /// <summary>
        /// 정지 이미지({dir}/{name}.png) 또는 애니메이션 폴더({dir}/{name}/{name}.controller + Frames 폴더) 중 있는 쪽을 연결한다.
        /// 애니메이션이면 첫 프레임을 대표 스프라이트(정지 화면·크기 배율 계산용)로 쓴다.
        /// </summary>
        static void LoadVisual(string dir, string name, string framesFolder,
                               out Sprite sprite, out RuntimeAnimatorController animator)
        {
            sprite = null;
            animator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{dir}/{name}/{name}.controller");
            string frames = $"{dir}/{name}/{framesFolder}";
            if (animator != null && AssetDatabase.IsValidFolder(frames))
            {
                var first = AssetDatabase.FindAssets("t:Texture2D", new[] { frames })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.EndsWith(".png"))
                    .OrderBy(p => Path.GetFileName(p), System.StringComparer.Ordinal)
                    .FirstOrDefault();
                if (first != null) sprite = LoadSprite(first);
            }
            if (sprite == null) sprite = LoadSprite($"{dir}/{name}.png");
            if (sprite != null || !AssetDatabase.IsValidFolder(dir)) return;

            // 이름이 조금 달라도 찾는다: 폴더 안의 애니메이션 컨트롤러(앞/뒤 구분) → 폴더 바로 아래 PNG
            bool back = framesFolder.EndsWith("_back");
            bool IsBack(string p) => Path.GetFileNameWithoutExtension(p).EndsWith("_back", System.StringComparison.OrdinalIgnoreCase);
            var ctrlPath = AssetDatabase.FindAssets("t:AnimatorController", new[] { dir })
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => IsBack(p) == back);
            if (ctrlPath != null)
            {
                animator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
                sprite = PalAnimationLinker.FirstSprite(animator);
                if (sprite != null) return;
            }
            var png = Directory.GetFiles(dir, "*.png").Select(p => p.Replace('\\', '/'))
                .FirstOrDefault(p => IsBack(p) == back);
            if (png != null) sprite = LoadSprite(png);
        }

        static float FitScale(Sprite s, float box)
        {
            if (s == null) return 1f;
            var size = s.bounds.size;                 // 유닛 단위
            return box / Mathf.Max(size.x, size.y);
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