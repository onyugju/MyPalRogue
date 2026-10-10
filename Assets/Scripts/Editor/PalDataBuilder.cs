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

        // HP, 공격, 방어, 특공, 특방, 속도 (합계 6.0)
        static readonly float[][] Weights =
        {
            new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f },  // Balanced
            new[] { 0.9f, 1.4f, 0.8f, 1.3f, 0.8f, 0.8f },  // Attacker
            new[] { 1.4f, 0.8f, 1.4f, 0.8f, 1.2f, 0.4f },  // Tank
            new[] { 0.8f, 1.0f, 0.7f, 1.0f, 0.7f, 1.8f },  // Fast
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
                pal.backSprite = back;
                pal.frontAnimator = frontAnim;
                pal.backAnimator = backAnim;
                pal.frontScale = FitScale(front, FrontBox);
                pal.backScale = FitScale(back, BackBox);
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
        static void CreatePalAssets()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(OutDir);

            int created = 0, skipped = 0, missing = 0;
            foreach (var r in Rows)
            {
                string assetPath = $"{OutDir}/{r.id}_{r.eng}.asset";
                if (AssetDatabase.LoadAssetAtPath<PalData>(assetPath) != null) { skipped++; continue; }

                string dir = $"{PalRoot}/{r.id} {r.kor}";
                LoadVisual(dir, r.front, "Frames", out var front, out var frontAnim);
                Sprite back = null; RuntimeAnimatorController backAnim = null;
                if (r.back != null) LoadVisual(dir, r.back, "Frames_back", out back, out backAnim);
                if (front == null) { Debug.LogWarning($"[PalRogue] 앞모습 스프라이트 없음: {dir}/{r.front}.png"); missing++; }

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
                pal.baseHP = S(0); pal.baseAttack = S(1); pal.baseDefense = S(2);
                pal.baseSpAttack = S(3); pal.baseSpDefense = S(4); pal.baseSpeed = S(5);

                AssetDatabase.CreateAsset(pal, assetPath);
                created++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PalRogue] PalData 생성 {created}개 / 건너뜀 {skipped}개 / 스프라이트 누락 {missing}개");
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