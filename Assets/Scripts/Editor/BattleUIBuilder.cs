#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/9: 포켓로그식 전투 화면을 현재 씬에 한 번에 만든다. (먼저 메뉴 8을 실행할 것)
    ///  - BattleField : 배경·발판·팰 2마리 (월드 공간, SpriteRenderer + Animator)
    ///  - BattleCanvas: 정보창 2개, 메시지창, 커맨드창, 지역/재화 텍스트 (UI, 320x180 기준)
    /// 다시 실행하면 기존 BattleField / BattleCanvas 를 지우고 새로 만든다.
    ///
    /// 좌표는 모두 "320x180 화면의 픽셀"이다. 위치가 마음에 안 들면 아래 상수만 고쳐서 다시 실행하면 된다.
    /// </summary>
    public static class BattleUIBuilder
    {
        // ───────── 에셋 경로 ─────────
        const string Core      = "Assets/palrogue_assets_core";
        const string UiDir     = Core + "/images/ui";
        const string ArenaDir  = Core + "/images/arenas";
        const string FontPath  = Core + "/fonts/pokemon-emerald-pro KR SDF.asset";   // 메뉴 11이 만드는 한글 폰트
        const string PalDir    = "Assets/Data/Pals";

        // ───────── 미리보기에 쓸 팰 / 배경 ─────────
        const string PlayerPalAsset = "029_Foxparks";   // 내 팰 (뒷모습)
        const string EnemyPalAsset  = "004_Lifmunk";    // 적 팰 (앞모습)
        const int PlayerLevel = 10, EnemyLevel = 8;
        const string Biome = "plains";

        // ───────── 화면 / 월드 기준 ─────────
        const float ScreenW = 320f, ScreenH = 180f;
        const float PxPerUnit = 18f;   // 월드 1유닛 = 화면 18픽셀 (카메라 orthographicSize = 5)

        // 팰이 서는 위치 = 발 끝 좌표 (화면 픽셀, 왼쪽 위가 원점)
        static readonly Vector2 PlayerFeet = new Vector2(104f, 140f);
        static readonly Vector2 EnemyFeet  = new Vector2(236f, 82f);
        const float EnemyBaseOffsetX = 20f;   // 적 발판을 오른쪽으로 민 정도 (스크린샷 기준)

        // ───────── 글자 ─────────
        const float FontMain = 11f, FontSmall = 7f, FontMessage = 12f, FontMenu = 11f;
        static readonly Color White = Color.white;
        static readonly Color HpGreen = new Color(0.25f, 0.95f, 0.45f);
        static readonly Color ExpBlue = new Color(0.2f, 0.75f, 1f);
        static readonly Color Gold = new Color(0.95f, 0.86f, 0.5f);

        static TMP_FontAsset font;

        [MenuItem("PalRogue/9. 전투 화면 레이아웃 생성")]
        static void Build()
        {
            // 사전 점검: 메뉴 8을 먼저 실행했는지
            var probe = AssetImporter.GetAtPath($"{UiDir}/pbinfo_player.png") as TextureImporter;
            if (probe == null) { Debug.LogError($"[PalRogue] {UiDir}/pbinfo_player.png 를 찾을 수 없습니다."); return; }
            if (probe.spriteImportMode != SpriteImportMode.Single)
            {
                EditorUtility.DisplayDialog("먼저 실행하세요", "메뉴 'PalRogue > 8. UI 이미지 임포트 설정 적용'을 먼저 실행해 주세요.", "확인");
                return;
            }

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) Debug.LogWarning($"[PalRogue] 폰트 에셋을 찾을 수 없음: {FontPath} (기본 폰트로 표시됩니다)");

            var playerData = LoadPal(PlayerPalAsset);
            var enemyData = LoadPal(EnemyPalAsset);
            if (playerData == null || enemyData == null)
            {
                Debug.LogError("[PalRogue] 미리보기용 PalData를 찾을 수 없습니다. 상단의 PlayerPalAsset / EnemyPalAsset 이름을 확인하세요.");
                return;
            }

            DestroyExisting("BattleField");
            DestroyExisting("BattleCanvas");

            SetupCamera();
            BuildField(playerData, enemyData);
            BuildCanvas(new Pal(playerData, PlayerLevel), new Pal(enemyData, EnemyLevel));

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[PalRogue] 전투 화면 레이아웃을 만들었습니다. Game 뷰를 16:9(예: Full HD)로 두고 확인하세요.");
        }

        // ═════════════════ 월드(배경 + 팰) ═════════════════
        static void SetupCamera()
        {
            var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null) { Debug.LogWarning("[PalRogue] 카메라를 찾을 수 없습니다."); return; }
            cam.orthographic = true;
            cam.orthographicSize = (ScreenH / 2f) / PxPerUnit;      // 180px = 10유닛 → 5
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            EditorUtility.SetDirty(cam);
        }

        static void BuildField(PalData playerData, PalData enemyData)
        {
            var root = new GameObject("BattleField").transform;

            // 배경 이미지는 모두 PPU 18이라 화면 픽셀과 1:1. 중심점이 가운데라서 "중심 픽셀 좌표"로 놓는다.
            AddSprite("Background", root, Arena("bg"), 0, PxToWorld(ScreenW / 2f, ScreenH / 2f));
            AddSprite("PlayerBase", root, Arena("a"), 1, PxToWorld(ScreenW / 2f, 132f / 2f));
            AddSprite("EnemyBase", root, Arena("b"), 1, PxToWorld(ScreenW / 2f + EnemyBaseOffsetX, 132f / 2f));

            AddPal("PlayerPal", root, playerData, true, PlayerFeet);
            AddPal("EnemyPal", root, enemyData, false, EnemyFeet);
        }

        static Sprite Arena(string suffix) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArenaDir}/{Biome}_{suffix}.png");

        static void AddSprite(string name, Transform parent, Sprite sprite, int order, Vector3 worldPos)
        {
            if (sprite == null) { Debug.LogWarning($"[PalRogue] 배경 스프라이트 없음: {name}"); return; }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
        }

        static void AddPal(string name, Transform parent, PalData pal, bool back, Vector2 feetPx)
        {
            var sprite = back ? pal.BackOrFront : pal.frontSprite;
            float scale = back ? pal.BackOrFrontScale : pal.frontScale;
            var anim = back ? pal.BackOrFrontAnimator : pal.frontAnimator;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 10;

            if (anim != null)
            {
                var animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = anim;
            }

            // 스프라이트 중심이 기준점이라, "발 끝"이 목표 좌표에 오도록 높이의 절반만큼 올린다
            float heightPx = sprite != null ? sprite.bounds.size.y * scale * PxPerUnit : 0f;
            go.transform.position = PxToWorld(feetPx.x, feetPx.y - heightPx / 2f);
        }

        /// <summary>화면 픽셀 좌표(왼쪽 위 원점) → 월드 좌표</summary>
        static Vector3 PxToWorld(float x, float y) =>
            new Vector3((x - ScreenW / 2f) / PxPerUnit, (ScreenH / 2f - y) / PxPerUnit, 0f);

        // ═════════════════ 캔버스(UI) ═════════════════
        static void BuildCanvas(Pal player, Pal enemy)
        {
            var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ScreenW, ScreenH);     // 320x180 → 1080p에서 정확히 6배
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            var root = canvasGo.transform;

            BuildHeader(root);
            BuildEnemyInfo(root, enemy);
            BuildPlayerInfo(root, player);
            BuildBottomPanel(root, player);
        }

        // 우상단: 지역(스테이지)과 재화
        static void BuildHeader(Transform root)
        {
            Label(TopRight(root, "AreaText", 3, 2, 120, 13), "마을 - 1", FontMain, TextAlignmentOptions.MidlineRight, White);
            Label(TopRight(root, "MoneyText", 3, 14, 120, 13), "1,000 G", FontMain, TextAlignmentOptions.MidlineRight, Gold);
        }

        // 적 정보창 (좌상단) : pbinfo_enemy_mini 130x31, HP 슬롯 x59 y20 (48x2)
        static void BuildEnemyInfo(Transform root, Pal enemy)
        {
            var box = TopLeft(root, "EnemyInfo", 10, 24, 130, 31);
            AddImage(box, UiSprite("pbinfo_enemy_mini"));

            var name = Label(TopLeft(box, "Name", 6, 4, 62, 12), "", FontMain, TextAlignmentOptions.MidlineLeft, White);
            var level = Label(TopLeft(box, "Level", 66, 4, 42, 12), "", FontMain, TextAlignmentOptions.MidlineRight, White);
            Label(TopLeft(box, "HpLabel", 46, 15, 14, 9), "HP", FontSmall, TextAlignmentOptions.Midline, HpGreen);
            var hp = Bar(TopLeft(box, "HpFill", 59, 20, 48, 2), "overlay_hp");
            var type1 = Icon(TopLeft(box, "Type1", 118, 4, 12, 12));
            var type2 = Icon(TopLeft(box, "Type2", 118, 16, 12, 12));

            Wire(box.gameObject, name, level, null, hp, null, type1, type2).Refresh(enemy);
        }

        // 내 정보창 (우중단) : pbinfo_player 130x42, HP 슬롯 x69 y20 (48x2), EXP 슬롯 x32 y39 (85x2)
        static void BuildPlayerInfo(Transform root, Pal player)
        {
            var box = TopRight(root, "PlayerInfo", 9, 88, 130, 42);
            AddImage(box, UiSprite("pbinfo_player"));

            var name = Label(TopLeft(box, "Name", 13, 5, 56, 12), "", FontMain, TextAlignmentOptions.MidlineLeft, White);
            var level = Label(TopLeft(box, "Level", 64, 5, 42, 12), "", FontMain, TextAlignmentOptions.MidlineRight, White);
            Label(TopLeft(box, "HpLabel", 56, 15, 14, 9), "HP", FontSmall, TextAlignmentOptions.Midline, HpGreen);
            var hp = Bar(TopLeft(box, "HpFill", 69, 20, 48, 2), "overlay_hp");
            var hpText = Label(TopLeft(box, "HpText", 67, 26, 50, 10), "", FontMain, TextAlignmentOptions.MidlineRight, White);
            Label(TopLeft(box, "ExpLabel", 12, 35, 18, 8), "EXP", FontSmall, TextAlignmentOptions.MidlineLeft, ExpBlue);
            var exp = Bar(TopLeft(box, "ExpFill", 32, 39, 85, 2), "overlay_exp");
            var type1 = Icon(TopLeft(box, "Type1", -9, 10, 12, 12));
            var type2 = Icon(TopLeft(box, "Type2", -9, 23, 12, 12));

            var info = Wire(box.gameObject, name, level, hpText, hp, exp, type1, type2);
            info.Refresh(player);
            info.SetExp(0.4f);   // 미리보기용
        }

        // 하단: 메시지창(좌) + 커맨드창(우, 2x2)
        static void BuildBottomPanel(Transform root, Pal player)
        {
            var msg = BottomLeft(root, "MessagePanel", 0, 0, ScreenW, 48);
            AddImage(msg, UiSprite("overlay_message"));      // 512x96 이미지를 320x48로 늘려서 사용
            string palName = string.IsNullOrEmpty(player.Data.palName) ? player.Data.name : player.Data.palName;
            var msgText = Label(TopLeft(msg, "Text", 12, 8, 185, 36), $"{palName}은(는)\n무엇을 할까?",
                                FontMessage, TextAlignmentOptions.MidlineLeft, White);
            msgText.lineSpacing = 10f;

            var cmd = BottomRight(root, "CommandBox", 0, 0, 116, 45);
            var win = AddImage(cmd, UiSprite("windows/window_1"));
            win.type = Image.Type.Sliced;

            Label(TopLeft(cmd, "Fight", 14, 10, 50, 12), "싸운다", FontMenu, TextAlignmentOptions.MidlineLeft, White);
            Label(TopLeft(cmd, "Sphere", 70, 10, 44, 12), "스피어", FontMenu, TextAlignmentOptions.MidlineLeft, White);
            Label(TopLeft(cmd, "Pals", 14, 27, 50, 12), "팰", FontMenu, TextAlignmentOptions.MidlineLeft, White);
            Label(TopLeft(cmd, "Run", 70, 27, 44, 12), "도망간다", FontMenu, TextAlignmentOptions.MidlineLeft, White);
            AddImage(TopLeft(cmd, "Cursor", 7, 11, 6, 10), UiSprite("cursor"));
        }

        // ═════════════════ 도우미 ═════════════════
        static PalInfoBox Wire(GameObject box, TMP_Text name, TMP_Text level, TMP_Text hpText,
                               RawImage hp, RawImage exp, Image t1, Image t2)
        {
            var info = box.AddComponent<PalInfoBox>();
            info.nameText = name; info.levelText = level; info.hpText = hpText;
            info.hpFill = hp; info.expFill = exp; info.typeIcon1 = t1; info.typeIcon2 = t2;
            return info;
        }

        static PalData LoadPal(string assetName) => AssetDatabase.LoadAssetAtPath<PalData>($"{PalDir}/{assetName}.asset");
        static Sprite UiSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/{name}.png");

        static void DestroyExisting(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        static RectTransform Make(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;                       // 모서리를 기준점으로 쓴다 (왼쪽위 / 오른쪽위 / 왼쪽아래 / 오른쪽아래)
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        // 부모의 각 모서리에서 (x, y)만큼 안쪽으로 떨어진 위치. 단위는 화면 픽셀.
        static RectTransform TopLeft(Transform p, string n, float x, float y, float w, float h) => Make(p, n, new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));
        static RectTransform TopRight(Transform p, string n, float x, float y, float w, float h) => Make(p, n, new Vector2(1, 1), new Vector2(-x, -y), new Vector2(w, h));
        static RectTransform BottomLeft(Transform p, string n, float x, float y, float w, float h) => Make(p, n, new Vector2(0, 0), new Vector2(x, y), new Vector2(w, h));
        static RectTransform BottomRight(Transform p, string n, float x, float y, float w, float h) => Make(p, n, new Vector2(1, 0), new Vector2(-x, y), new Vector2(w, h));

        static Image AddImage(RectTransform rt, Sprite sprite)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static Image Icon(RectTransform rt)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        static RawImage Bar(RectTransform rt, string textureName)
        {
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{UiDir}/{textureName}.png");
            raw.raycastTarget = false;
            return raw;
        }

        static TextMeshProUGUI Label(RectTransform rt, string text, float size, TextAlignmentOptions align, Color color)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
#endif
