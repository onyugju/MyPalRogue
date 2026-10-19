#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/10: 메뉴 9로 만든 전투 화면에 "움직이는 부분"을 붙인다.
    ///  - 커맨드 4개에 클릭/선택 영역 추가
    ///  - 스킬 선택 창(SkillPanel) + 스킬 정보 창(SkillInfoPanel) + 다시 시작 버튼 생성
    ///  - 팰 오브젝트에 PalView, BattleCanvas에 BattleScreen을 붙이고 참조를 전부 연결
    /// 이미 손으로 옮겨 둔 위치·크기는 건드리지 않는다(새로 만드는 것은 스킬 창류뿐). 다시 실행해도 안전하다.
    /// </summary>
    public static class BattleScreenWiring
    {
        const string UiDir = "Assets/palrogue_assets_core/images/ui";
        const string PalDir = "Assets/Data/Pals";
        const string DefaultPlayerPal = "029_Foxparks";
        const string DefaultEnemyPal = "004_Lifmunk";
        const float InfoFont = 8f;     // 스킬 정보창의 작은 글자 크기

        // ───── 스킬 정보창 배치 (정보창 왼쪽 위 기준 픽셀, 포켓로그 스크린샷에서 잰 값) ─────
        static readonly Vector2 TypeBadgePos = new Vector2(13, 6), TypeBadgeSize = new Vector2(18, 11);
        static readonly Vector2 CategoryPos = new Vector2(41, 6), CategorySize = new Vector2(26, 11);
        static readonly float[] InfoRowY = { 17.5f, 25.5f, 33.5f };   // PP / 위력 / 명중률 줄의 위쪽
        const float InfoLabelX = 9f, InfoValueRight = 67f;             // 라벨 왼쪽 끝, 값 오른쪽 끝

        // ───── 상태이상 뱃지: 포켓로그처럼 이름 바로 아래 (이름 위치 + 11.5px) ─────
        const float StatusBelowName = 11.5f;
        static readonly Vector2 StatusBadgeSize = new Vector2(19, 8);   // 뱃지 그림 비율 565:242

        [MenuItem("PalRogue/10. 전투 화면 연결 (스크립트 붙이기)", false, 1010)]
        static void Wire()
        {
            var canvasGo = GameObject.Find("BattleCanvas");
            var field = GameObject.Find("BattleField");
            if (canvasGo == null || field == null)
            {
                EditorUtility.DisplayDialog("먼저 실행하세요", "메뉴 'PalRogue > 9. 전투 화면 레이아웃 생성'을 먼저 실행해 주세요.", "확인");
                return;
            }

            // ───── 필요한 기존 오브젝트 찾기 ─────
            var root = canvasGo.transform;
            var missing = new List<string>();
            Transform Need(Transform parent, string path)
            {
                var t = parent.Find(path);
                if (t == null) missing.Add($"{parent.name}/{path}");
                return t;
            }
            var enemyInfoT = Need(root, "EnemyInfo");
            var playerInfoT = Need(root, "PlayerInfo");
            var msgTextT = Need(root, "MessagePanel/Text");
            var cmdT = Need(root, "CommandBox");
            var fightT = Need(root, "CommandBox/Fight");
            var sphereT = Need(root, "CommandBox/Sphere");
            var palsT = Need(root, "CommandBox/Pals");
            var runT = Need(root, "CommandBox/Run");
            var cursorT = Need(root, "CommandBox/Cursor");
            var playerPalT = Need(field.transform, "PlayerPal");
            var enemyPalT = Need(field.transform, "EnemyPal");
            if (missing.Count > 0)
            {
                Debug.LogError("[PalRogue] 다음 오브젝트를 찾을 수 없습니다 (이름을 바꾸거나 지우지 않았는지 확인, 또는 9번을 다시 실행): " + string.Join(", ", missing));
                return;
            }

            var fightLabel = fightT.GetComponent<TextMeshProUGUI>();
            var font = fightLabel != null ? fightLabel.font : null;
            float menuSize = fightLabel != null ? fightLabel.fontSize : 11f;
            var windowSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/windows/window_1.png");
            var cursorSprite = cursorT.GetComponent<Image>().sprite;

            // ───── 커맨드 4개에 클릭 영역 ─────
            var fightBtn = AddHit(fightT);
            var sphereBtn = AddHit(sphereT);
            var palsBtn = AddHit(palsT);
            var runBtn = AddHit(runT);

            // ───── 스킬 창 / 정보 창 / 다시 시작 버튼 (이미 있으면 지우고 새로) ─────
            DestroyChild(root, "SkillPanel");
            DestroyChild(root, "SkillInfoPanel");
            DestroyChild(root, "RestartButton");

            // 스킬 선택 창 (포켓로그 스크린샷 기준): 1열 x17 / 2열 x130, 1행 y10 / 2행 y26, 커서는 글자 왼쪽 7px
            var skillPanel = Make(root, "SkillPanel", new Vector2(0, 0), new Vector2(0, 0), new Vector2(242, 48));
            AddWindow(skillPanel, windowSprite);
            var skillButtons = new Button[4];
            var skillTexts = new TMP_Text[4];
            Vector2[] slots = { new Vector2(17, 10), new Vector2(130, 10), new Vector2(17, 26), new Vector2(130, 26) };
            for (int i = 0; i < 4; i++)
            {
                var label = Label(skillPanel, $"Skill{i + 1}", font, menuSize, TextAlignmentOptions.MidlineLeft, slots[i], new Vector2(100, 12));
                skillTexts[i] = label;
                skillButtons[i] = AddHit(label.transform);
            }
            var skillCursorRt = MakeTopLeft(skillPanel, "Cursor", new Vector2(10, 11), new Vector2(6, 10));
            var skillCursor = skillCursorRt.gameObject.AddComponent<Image>();
            skillCursor.sprite = cursorSprite;
            skillCursor.raycastTarget = false;

            // 스킬 정보 창: 위쪽에 속성 뱃지 + 종류 뱃지, 아래에 PP / 위력 / 명중률
            var infoPanel = Make(root, "SkillInfoPanel", new Vector2(1, 0), new Vector2(0, 0), new Vector2(78, 48));
            AddWindow(infoPanel, windowSprite);

            // 위쪽 줄 (포켓로그 스킬 정보창 기준): 속성 뱃지와 종류 아이콘을 위에서 6px, 높이 11px로.
            // 두 그림은 같은 높이로 그려져 있어서 같은 배율로 줄이면 속성 18x11, 종류 26x11이 된다.
            // 속성 뱃지는 포켓로그 뱃지 자리(x10~35)의 가운데, 종류 아이콘은 x41에 둔다.
            var typeBadgeRt = MakeTopLeft(infoPanel, "TypeBadge", TypeBadgePos, TypeBadgeSize);
            var typeBadge = typeBadgeRt.gameObject.AddComponent<RawImage>();
            typeBadge.raycastTarget = false;
            typeBadge.enabled = false;
            var typeFallback = Label(infoPanel, "TypeFallback", font, InfoFont, TextAlignmentOptions.Midline, TypeBadgePos, TypeBadgeSize);

            var catRt = MakeTopLeft(infoPanel, "CategoryBadge", CategoryPos, CategorySize);
            var catBadge = catRt.gameObject.AddComponent<RawImage>();
            catBadge.raycastTarget = false;
            var catText = Label(infoPanel, "CategoryText", font, InfoFont, TextAlignmentOptions.Midline, CategoryPos, CategorySize);

            string[] rowIds = { "Pp", "Power", "Accuracy" };
            string[] rowTitles = { "PP", "위력", "명중률" };
            float[] rowY = InfoRowY;
            var values = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                var title = Label(infoPanel, rowIds[i] + "Label", font, InfoFont, TextAlignmentOptions.MidlineLeft, new Vector2(InfoLabelX, rowY[i]), new Vector2(30, 8));
                title.text = rowTitles[i];
                values[i] = Label(infoPanel, rowIds[i] + "Value", font, InfoFont, TextAlignmentOptions.MidlineRight, new Vector2(InfoValueRight - 32, rowY[i]), new Vector2(32, 8));
            }

            var restart = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuEntry));
            restart.transform.SetParent(root, false);
            var rrt = (RectTransform)restart.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            var rimg = restart.GetComponent<Image>();
            rimg.color = new Color(1, 1, 1, 0);
            rimg.canvasRenderer.cullTransparentMesh = false;
            var restartBtn = restart.GetComponent<Button>();
            restartBtn.targetGraphic = rimg;
            restartBtn.transition = Selectable.Transition.None;

            skillPanel.gameObject.SetActive(false);
            infoPanel.gameObject.SetActive(false);
            restart.SetActive(false);

            // ───── 팰 오브젝트에 PalView ─────
            var playerView = SetupPalView(playerPalT, true);
            var enemyView = SetupPalView(enemyPalT, false);

            // ───── BattleScreen 붙이고 연결 ─────
            var screen = canvasGo.GetComponent<BattleScreen>();
            if (screen == null) screen = canvasGo.AddComponent<BattleScreen>();
            screen.playerView = playerView;
            screen.enemyView = enemyView;
            screen.playerInfo = EnsureInfoBox(playerInfoT);
            screen.enemyInfo = EnsureInfoBox(enemyInfoT);
            screen.messageText = msgTextT.GetComponent<TMP_Text>();
            screen.commandBox = cmdT.gameObject;
            screen.commandCursor = cursorT.GetComponent<Image>();
            screen.fightButton = fightBtn; screen.sphereButton = sphereBtn;
            screen.palsButton = palsBtn; screen.runButton = runBtn;
            screen.skillPanel = skillPanel.gameObject;
            screen.skillInfoPanel = infoPanel.gameObject;
            screen.skillCursor = skillCursor;
            screen.skillButtons = skillButtons;
            screen.skillTexts = skillTexts;
            screen.typeBadge = typeBadge;
            screen.typeFallbackText = typeFallback;
            screen.categoryBadge = catBadge;
            screen.categoryText = catText;
            screen.ppValueText = values[0];
            screen.powerValueText = values[1];
            screen.accuracyValueText = values[2];
            screen.restartButton = restartBtn;

            // 전투에 쓸 팰: 비어 있을 때만 기본값을 넣는다 (직접 바꿔 둔 값은 유지)
            if (screen.playerPal == null) screen.playerPal = AssetDatabase.LoadAssetAtPath<PalData>($"{PalDir}/{DefaultPlayerPal}.asset");
            if (screen.enemyPal == null) screen.enemyPal = AssetDatabase.LoadAssetAtPath<PalData>($"{PalDir}/{DefaultEnemyPal}.asset");
            EditorUtility.SetDirty(screen);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                Debug.LogWarning("[PalRogue] 씬에 EventSystem이 없습니다. 상단 메뉴 GameObject > UI > Event System 으로 만들어 주세요. (버튼 입력에 필요)");

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[PalRogue] 전투 화면 연결 완료. Play를 누르면 전투가 시작됩니다. (BattleCanvas의 BattleScreen에서 팰/레벨을 바꿀 수 있어요)");
        }

        // ═════════════════ 도우미 ═════════════════
        /// <summary>
        /// 정보창에 PalInfoBox가 제대로 붙어 있게 만든다.
        /// 스크립트 파일을 지웠다가 다시 넣으면 파일 고유 번호(GUID)가 바뀌어서 기존 컴포넌트가
        /// 'Missing(Mono Script)' 상태가 된다. 그럴 때 깨진 컴포넌트를 지우고 새로 붙인 뒤,
        /// 하위 오브젝트 이름(Name, Level, HpFill ...)으로 참조 칸을 다시 연결한다.
        /// </summary>
        static PalInfoBox EnsureInfoBox(Transform box)
        {
            var info = box.GetComponent<PalInfoBox>();
            if (info == null)
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(box.gameObject);
                info = box.gameObject.AddComponent<PalInfoBox>();
                Debug.Log($"[PalRogue] {box.name}: 깨진 컴포넌트 {removed}개를 지우고 PalInfoBox를 새로 붙였습니다.");
            }
            if (info.nameText == null) info.nameText = FindComp<TMP_Text>(box, "Name");
            if (info.levelText == null) info.levelText = FindComp<TMP_Text>(box, "Level");
            if (info.hpText == null) info.hpText = FindComp<TMP_Text>(box, "HpText");      // 적 정보창엔 없음
            if (info.hpFill == null) info.hpFill = FindComp<RawImage>(box, "HpFill");
            if (info.expFill == null) info.expFill = FindComp<RawImage>(box, "ExpFill");   // 적 정보창엔 없음
            if (info.typeIcon1 == null) info.typeIcon1 = FindComp<Image>(box, "Type1");
            if (info.typeIcon2 == null) info.typeIcon2 = FindComp<Image>(box, "Type2");
            info.statusBadge = EnsureStatusBadge(box);                                   // 위치는 매번 이름 기준으로 다시 맞춤
            EditorUtility.SetDirty(info);
            return info;
        }

        /// <summary>
        /// 정보창의 상태이상 뱃지를 포켓로그와 같은 자리, 즉 이름 바로 아래(이름 위쪽 + 11.5px, 왼쪽 끝 맞춤)에 둔다.
        /// 이름(Name)을 씬에서 옮겨 두었으면 그 위치를 따라간다. 메뉴 10을 실행할 때마다 다시 맞춘다.
        /// </summary>
        static RawImage EnsureStatusBadge(Transform box)
        {
            var raw = FindComp<RawImage>(box, "StatusBadge");
            if (raw == null)
            {
                var created = MakeTopLeft(box, "StatusBadge", Vector2.zero, StatusBadgeSize);
                raw = created.gameObject.AddComponent<RawImage>();
                raw.raycastTarget = false;
                raw.enabled = false;
            }

            var rt = raw.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = StatusBadgeSize;

            var name = box.Find("Name") as RectTransform;
            if (name != null)
            {
                // Name도 왼쪽 위 기준이라 anchoredPosition.y가 "위에서부터의 거리"의 음수다
                float left = Mathf.Round(name.anchoredPosition.x);
                float top = Mathf.Floor(-name.anchoredPosition.y + StatusBelowName);
                rt.anchoredPosition = new Vector2(left, -top);
            }
            else
            {
                rt.anchoredPosition = box.name == "PlayerInfo" ? new Vector2(20, -16) : new Vector2(6, -15);
            }
            rt.SetAsLastSibling();      // 흰 HP 띠보다 위에 그려지게
            EditorUtility.SetDirty(rt);
            return raw;
        }

        static T FindComp<T>(Transform parent, string childName) where T : Component
        {
            var t = parent.Find(childName);
            return t != null ? t.GetComponent<T>() : null;
        }

        static PalView SetupPalView(Transform pal, bool back)
        {
            var sr = pal.GetComponent<SpriteRenderer>();
            var view = pal.GetComponent<PalView>();
            if (view == null) view = pal.gameObject.AddComponent<PalView>();

            float tune = 1f;
            if (sr != null && sr.sprite != null)
            {
                // 지금 씬에 놓인 스프라이트가 어느 PalData의 것인지 찾아서, 직접 조절해 둔 크기 배율을 계산한다
                foreach (var guid in AssetDatabase.FindAssets("t:PalData", new[] { PalDir }))
                {
                    var d = AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (d == null) continue;
                    bool match = back ? d.BackOrFront == sr.sprite : d.frontSprite == sr.sprite;
                    if (!match) continue;
                    float dataScale = back ? d.BackOrFrontScale : d.frontScale;
                    if (dataScale > 0f) tune = pal.localScale.x / dataScale;
                    break;
                }
                view.feetWorldY = pal.position.y - sr.sprite.bounds.size.y * pal.localScale.y / 2f;
            }
            view.scaleTune = tune;
            EditorUtility.SetDirty(view);
            return view;
        }

        static Button AddHit(Transform label)
        {
            DestroyChild(label, "Hit");
            var go = new GameObject("Hit", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuEntry));
            go.transform.SetParent(label, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-9f, -2f); rt.offsetMax = new Vector2(2f, 2f);    // 라벨보다 살짝 넓게(커서 자리까지)

            var img = go.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0);                  // 안 보이지만 클릭은 받는다
            img.canvasRenderer.cullTransparentMesh = false;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;        // 선택 표시는 ▶ 커서가 대신한다
            return btn;
        }

        static void DestroyChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        static RectTransform Make(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform MakeTopLeft(Transform parent, string name, Vector2 pos, Vector2 size) =>
            Make(parent, name, new Vector2(0, 1), new Vector2(pos.x, -pos.y), size);

        static void AddWindow(RectTransform rt, Sprite window)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = window;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
        }

        static TextMeshProUGUI Label(Transform parent, string name, TMP_FontAsset font, float size,
                                     TextAlignmentOptions align, Vector2 pos, Vector2 sz)
        {
            var rt = MakeTopLeft(parent, name, pos, sz);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.text = "";
            return t;
        }
    }
}
#endif
