#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/14: 포켓로그식 파티 화면(PartyPanel)을 BattleCanvas 안에 만들고 BattleScreen에 연결한다.
    /// (메뉴 8, 10을 먼저 실행해 둘 것) 다시 실행하면 PartyPanel을 지우고 새로 만든다.
    /// 좌표는 모두 320x180 화면의 픽셀(왼쪽 위 원점)이고, party_bg 그림의 자리에 맞춰 잰 값이다.
    /// </summary>
    public static class PartyScreenBuilder
    {
        const string UiDir = "Assets/palrogue_assets_core/images/ui";
        const string PalDir = "Assets/Data/Pals";

        // ───── 칸 위치 (party_bg의 보라색 자리에 맞춤) ─────
        static readonly Vector2 MainSlotPos = new Vector2(9, 31), MainSlotSize = new Vector2(110, 49);
        static readonly Vector2 SmallSlotSize = new Vector2(175, 24);
        const float SmallSlotX = 143, SmallSlotTop = 13, SmallSlotStep = 28;

        // ───── 칸 안쪽 배치 (칸 왼쪽 위 기준) ─────
        struct SlotLayout
        {
            public Vector2 iconPos, iconSize, namePos, levelPos, hpBarPos, statusPos;
            public float hpTextRight, hpTextY, nameWidth;
        }
        static readonly SlotLayout MainLayout = new SlotLayout
        {
            iconPos = new Vector2(4, 4), iconSize = new Vector2(22, 22),
            namePos = new Vector2(28, 8), nameWidth = 78, levelPos = new Vector2(28, 19),
            hpBarPos = new Vector2(8, 31), hpTextRight = 105, hpTextY = 38, statusPos = new Vector2(80, 20),
        };
        static readonly SlotLayout SmallLayout = new SlotLayout
        {
            iconPos = new Vector2(4, 2), iconSize = new Vector2(20, 20),
            namePos = new Vector2(27, 1), nameWidth = 44, levelPos = new Vector2(27, 13),
            hpBarPos = new Vector2(72, 4), hpTextRight = 169, hpTextY = 12, statusPos = new Vector2(48, 14),
        };
        static readonly Vector2 HpFillOffset = new Vector2(16, 2);   // party_slot_hp_bar 안에서 HP가 채워지는 자리
        static readonly Vector2 StatusSize = new Vector2(19, 8);

        // ───── 아래쪽: 안내 메시지 창 + 취소 버튼 ─────
        static readonly Vector2 MessagePos = new Vector2(1, 149), MessageSize = new Vector2(262, 30);
        static readonly Vector2 CancelPos = new Vector2(265, 155), CancelSize = new Vector2(52, 16);

        const float NameFont = 11f, SmallFont = 8f, MessageFont = 11f, CancelFont = 10f;

        // 파티가 비어 있을 때 넣어 줄 테스트 파티
        static readonly (string asset, int level)[] DefaultParty =
        {
            ("004_Lifmunk", 10), ("001_Lamball", 8), ("002_Cattiva", 9), ("005_Fuack", 7),
        };

        static TMP_FontAsset font;

        [MenuItem("PalRogue/14. 파티 화면 만들기", false, 1014)]
        static void Build()
        {
            var canvasGo = GameObject.Find("BattleCanvas");
            var screen = canvasGo != null ? canvasGo.GetComponent<BattleScreen>() : null;
            if (screen == null)
            {
                EditorUtility.DisplayDialog("먼저 실행하세요", "메뉴 9, 10을 먼저 실행해서 전투 화면(BattleCanvas)을 만들어 주세요.", "확인");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/party_bg.png") == null)
            {
                EditorUtility.DisplayDialog("먼저 실행하세요", "메뉴 'PalRogue > 8. UI 이미지 임포트 설정 적용'을 먼저 실행해 주세요.", "확인");
                return;
            }

            var anyText = canvasGo.GetComponentInChildren<TextMeshProUGUI>(true);
            font = anyText != null ? anyText.font : null;

            var old = canvasGo.transform.Find("PartyPanel");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // ── 전체 화면 패널 + 배경 ──
            var panelGo = new GameObject("PartyPanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panel = (RectTransform)panelGo.transform;
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            var bg = panelGo.AddComponent<Image>();
            bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/party_bg.png");
            bg.raycastTarget = true;                          // 뒤쪽 전투 화면이 눌리지 않게 막는다

            var mainTex = Tex("party_slot_main");
            var smallTex = Tex("party_slot");
            var hpBarSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/party_slot_hp_bar.png");
            var hpFillTex = Tex("party_slot_hp_overlay");

            // ── 칸 6개 ──
            var slots = new PartySlotView[6];
            var buttons = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                bool main = i == 0;
                var pos = main ? MainSlotPos : new Vector2(SmallSlotX, SmallSlotTop + SmallSlotStep * (i - 1));
                var size = main ? MainSlotSize : SmallSlotSize;
                var lay = main ? MainLayout : SmallLayout;
                (slots[i], buttons[i]) = BuildSlot(panel, $"Slot{i + 1}", pos, size, lay, main ? mainTex : smallTex, hpBarSprite, hpFillTex);
            }

            // ── 안내 메시지 창 ──
            var msg = TopLeft(panel, "MessageBox", MessagePos, MessageSize);
            var win = msg.gameObject.AddComponent<Image>();
            win.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/windows/window_1.png");
            win.type = Image.Type.Sliced;
            win.raycastTarget = false;
            var msgText = Label(msg, "Text", new Vector2(9, 7), new Vector2(MessageSize.x - 18, 16), MessageFont, TextAlignmentOptions.MidlineLeft);

            // ── 취소 버튼 ──
            var cancel = TopLeft(panel, "CancelButton", CancelPos, CancelSize);
            var cancelFrame = cancel.gameObject.AddComponent<RawImage>();
            cancelFrame.texture = Tex("party_cancel");
            cancelFrame.uvRect = new Rect(0, 0.5f, 1, 0.5f);
            var cancelBtn = cancel.gameObject.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelFrame;
            cancelBtn.transition = Selectable.Transition.None;
            cancel.gameObject.AddComponent<MenuEntry>();
            Label(cancel, "Label", new Vector2(18, 2), new Vector2(32, 12), CancelFont, TextAlignmentOptions.MidlineLeft).text = "취소";

            // ── PartyScreen 연결 ──
            var party = panelGo.AddComponent<PartyScreen>();
            party.slots = slots;
            party.slotButtons = buttons;
            party.cancelButton = cancelBtn;
            party.cancelFrame = cancelFrame;
            party.messageText = msgText;
            panel.SetAsLastSibling();                         // 전투 UI보다 위에
            panelGo.SetActive(false);                         // 평소에는 숨김

            screen.partyScreen = party;
            if (screen.playerParty == null || !screen.playerParty.Any(m => m != null && m.pal != null))
                screen.playerParty = BuildDefaultParty(screen);
            EditorUtility.SetDirty(screen);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[PalRogue] 파티 화면을 만들었습니다. 테스트 파티 {screen.playerParty.Count}마리 (BattleCanvas > BattleScreen > Player Party 에서 바꿀 수 있어요)");
        }

        // ═════════════════ 칸 하나 ═════════════════
        static (PartySlotView, Button) BuildSlot(Transform panel, string name, Vector2 pos, Vector2 size, SlotLayout lay,
                                                 Texture2D frameTex, Sprite hpBarSprite, Texture2D hpFillTex)
        {
            var root = TopLeft(panel, name, pos, size);
            var frame = root.gameObject.AddComponent<RawImage>();
            frame.texture = frameTex;
            frame.uvRect = new Rect(0, 1f - 1f / PartySlotView.FrameCount, 1, 1f / PartySlotView.FrameCount);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;   // 선택 표시는 칸 그림이 바뀌는 것으로 대신한다
            root.gameObject.AddComponent<MenuEntry>();

            var icon = TopLeft(root, "Icon", lay.iconPos, lay.iconSize).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var nameText = Label(root, "Name", lay.namePos, new Vector2(lay.nameWidth, 12), NameFont, TextAlignmentOptions.MidlineLeft);
            var levelText = Label(root, "Level", lay.levelPos, new Vector2(30, 9), SmallFont, TextAlignmentOptions.MidlineLeft);

            var bar = TopLeft(root, "HpBar", lay.hpBarPos, new Vector2(100, 7)).gameObject.AddComponent<Image>();
            bar.sprite = hpBarSprite;
            bar.raycastTarget = false;
            var fill = TopLeft(root, "HpFill", lay.hpBarPos + HpFillOffset, new Vector2(PartySlotView.HpFillWidth, 2)).gameObject.AddComponent<RawImage>();
            fill.texture = hpFillTex;
            fill.raycastTarget = false;

            var hpText = Label(root, "HpText", new Vector2(lay.hpTextRight - 40, lay.hpTextY), new Vector2(40, 9), SmallFont, TextAlignmentOptions.MidlineRight);

            var status = TopLeft(root, "StatusBadge", lay.statusPos, StatusSize).gameObject.AddComponent<RawImage>();
            status.raycastTarget = false;
            status.enabled = false;

            var view = root.gameObject.AddComponent<PartySlotView>();
            view.frame = frame; view.icon = icon; view.nameText = nameText; view.levelText = levelText;
            view.hpFill = fill; view.hpText = hpText; view.statusBadge = status;
            return (view, button);
        }

        static List<PartyMember> BuildDefaultParty(BattleScreen screen)
        {
            var list = new List<PartyMember>();
            foreach (var (asset, level) in DefaultParty)
            {
                var pal = AssetDatabase.LoadAssetAtPath<PalData>($"{PalDir}/{asset}.asset");
                if (pal != null) list.Add(new PartyMember { pal = pal, level = level });
            }
            // 지금 쓰던 내 팰이 목록에 없으면 맨 앞에 넣는다
            if (screen.playerPal != null && list.All(m => m.pal != screen.playerPal))
                list.Insert(0, new PartyMember { pal = screen.playerPal, level = screen.playerLevel });
            return list.Take(6).ToList();
        }

        // ═════════════════ 도우미 ═════════════════
        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{UiDir}/{name}.png");

        static RectTransform TopLeft(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = size;
            return rt;
        }

        static TextMeshProUGUI Label(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align)
        {
            var t = TopLeft(parent, name, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = fontSize;
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
