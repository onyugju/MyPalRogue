using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 저장 슬롯 고르기 (포켓로그의 세션 선택 화면).
    /// 슬롯마다: 모험 번호, 지역 - 웨이브, 저장한 날짜·시각, 플레이 시간, 파티 팰 아이콘과 레벨.
    ///  - 저장 모드(새 게임): 고른 슬롯에 새 모험을 저장한다. 이미 모험이 있으면 지울지 묻는다
    ///  - 불러오기 모드: 모험이 있는 슬롯만 고를 수 있다
    /// 조작: ↑↓ 이동, Enter 선택, Esc 뒤로. 결과: Result (슬롯 번호, 뒤로 가면 -1)
    /// </summary>
    public class SaveSlotScreen : MonoBehaviour
    {
        const int SortingOrder = 410;
        const int Visible = 3;
        const float SlotX = 14f, SlotW = 284f, SlotH = 50f, SlotTop = 17f, SlotGap = 4f;
        static readonly Color Teal = UiKit.Hex(0x006a62), Corner = UiKit.Hex(0x2a8ae0);

        TMP_FontAsset font;
        RectTransform root;
        TMP_Text header;
        readonly List<SlotView> views = new List<SlotView>();
        RectTransform arrow;
        RectTransform dialog;
        TMP_Text dialogText;
        TMP_Text[] dialogItems;
        Image dialogCursor;

        class SlotView
        {
            public RectTransform rt;
            public TMP_Text title, line2, line3, line4, empty;
            public Image[] icons;
            public TMP_Text[] levels;
            public RectTransform corners;
        }

        RunSave[] saves = new RunSave[SaveSlots.Count];
        bool forSave, done;
        int cursor, top;
        float openedAt, headerUntil;
        string headerDefault;

        public int Result { get; private set; } = -1;

        public static SaveSlotScreen Create(TMP_FontAsset font)
        {
            var go = new GameObject("SaveSlotScreen", typeof(RectTransform));
            var screen = go.AddComponent<SaveSlotScreen>();
            screen.font = font;
            screen.Build();
            go.SetActive(false);
            return screen;
        }

        // 팰 아이콘을 찾을 때 쓰는 목록 (에셋 이름 → 데이터)
        Dictionary<string, PalData> palLookup = new Dictionary<string, PalData>();

        void Build()
        {
            root = UiKit.Canvas(gameObject, SortingOrder, Teal);
            header = UiKit.Label(root, font, "Header", 10, 2, 300, 14, 10, TextAlignmentOptions.MidlineLeft, UiKit.White);

            for (int i = 0; i < Visible; i++)
            {
                int row = i;
                var outer = UiKit.Frame(root, "Slot" + i, SlotX, SlotTop + i * (SlotH + SlotGap), SlotW, SlotH, UiKit.FrameRed, UiKit.FrameFill, out var inner);
                var v = new SlotView { rt = outer };
                v.title = UiKit.Label(inner, font, "Title", 10, 1, 120, 11, 9, TextAlignmentOptions.MidlineLeft, UiKit.White);
                v.line2 = UiKit.Label(inner, font, "Wave", 10, 12, 120, 11, 9, TextAlignmentOptions.MidlineLeft, UiKit.White);
                v.line3 = UiKit.Label(inner, font, "Date", 10, 23, 130, 11, 9, TextAlignmentOptions.MidlineLeft, UiKit.White);
                v.line4 = UiKit.Label(inner, font, "Time", 10, 34, 120, 11, 9, TextAlignmentOptions.MidlineLeft, UiKit.White);
                v.empty = UiKit.Label(inner, font, "Empty", 10, 0, 200, SlotH - 4, 11, TextAlignmentOptions.MidlineLeft, UiKit.Soft);
                v.icons = new Image[GameConfig.MaxPartySize];
                v.levels = new TMP_Text[GameConfig.MaxPartySize];
                for (int j = 0; j < v.icons.Length; j++)
                {
                    var icon = UiKit.Box(inner, "Icon" + j, 136 + j * 23, 7, 20, 17).gameObject.AddComponent<Image>();
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    v.icons[j] = icon;
                    v.levels[j] = UiKit.Label(inner, font, "Lv" + j, 132 + j * 23, 25, 28, 9, 7, TextAlignmentOptions.Center, UiKit.White);
                }
                v.corners = BuildCorners(outer);
                UiKit.Clickable(outer.gameObject, () => { if (CanInput) { cursor = top + row; Refresh(); } },
                                                   () => { if (CanInput) { cursor = top + row; StartCoroutine(Pick()); } });
                views.Add(v);
            }

            arrow = UiKit.Cursor(root).rectTransform;
            arrow.sizeDelta = new Vector2(5, 9);

            // 확인 창 (예/아니오)
            dialog = UiKit.Frame(root, "Dialog", 50, 62, 220, 56, UiKit.FrameRed, UiKit.FrameFill, out var dInner);
            dialogText = UiKit.Label(dInner, font, "Text", 8, 4, 200, 24, 9, TextAlignmentOptions.TopLeft, UiKit.White);
            dialogText.textWrappingMode = TextWrappingModes.Normal;
            dialogItems = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                int row = i;
                dialogItems[i] = UiKit.Label(dInner, font, "Choice" + i, 24 + i * 70, 32, 60, 14, 10, TextAlignmentOptions.MidlineLeft, UiKit.White);
                dialogItems[i].text = i == 0 ? "예" : "아니오";
                UiKit.Clickable(dialogItems[i].gameObject, () => { dialogIndex = row; RefreshDialog(); }, () => { dialogIndex = row; dialogAnswer = row; });
            }
            dialogCursor = UiKit.Cursor(dInner);
            dialog.gameObject.SetActive(false);
        }

        /// <summary>파란 귀퉁이 표시 (고른 슬롯)</summary>
        static RectTransform BuildCorners(RectTransform parent)
        {
            var rt = UiKit.Box(parent, "Corners", 3, 3, parent.sizeDelta.x - 6, parent.sizeDelta.y - 6);
            for (int c = 0; c < 4; c++)
            {
                var a = new Vector2(c % 2, c / 2 == 0 ? 1 : 0);
                for (int part = 0; part < 2; part++)
                {
                    var img = new GameObject("Corner", typeof(RectTransform)).AddComponent<Image>();
                    img.color = Corner;
                    img.raycastTarget = false;
                    var b = img.rectTransform;
                    b.SetParent(rt, false);
                    b.anchorMin = b.anchorMax = b.pivot = a;
                    b.anchoredPosition = Vector2.zero;
                    b.sizeDelta = part == 0 ? new Vector2(5, 2) : new Vector2(2, 5);
                }
            }
            return rt;
        }

        bool CanInput => !done && !dialog.gameObject.activeSelf && Time.unscaledTime - openedAt > 0.2f;

        /// <summary>화면을 열고 슬롯을 고를 때까지 기다린다. forSave = 새 모험을 저장할 슬롯 고르기</summary>
        public IEnumerator Choose(bool forSave, IEnumerable<PalData> pals)
        {
            this.forSave = forSave;
            palLookup = pals.Where(p => p != null).GroupBy(p => p.name).ToDictionary(g => g.Key, g => g.First());
            for (int i = 0; i < saves.Length; i++) saves[i] = SaveSlots.Load(i);
            headerDefault = forSave ? "새 모험을 저장할 슬롯을 고르세요.   Esc: 뒤로" : "불러올 모험을 고르세요.   Esc: 뒤로";
            header.text = headerDefault;
            header.color = UiKit.White;
            headerUntil = 0;
            Result = -1;
            done = false;
            cursor = forSave ? FirstEmptyOr0() : FirstFilledOr0();
            top = 0;
            dialog.gameObject.SetActive(false);
            gameObject.SetActive(true);
            openedAt = Time.unscaledTime;
            Refresh();
            while (!done) yield return null;
        }

        public void Hide() => gameObject.SetActive(false);

        int FirstEmptyOr0() { for (int i = 0; i < saves.Length; i++) if (saves[i] == null) return i; return 0; }
        int FirstFilledOr0() { for (int i = 0; i < saves.Length; i++) if (saves[i] != null) return i; return 0; }

        void Update()
        {
            if (headerUntil > 0 && Time.unscaledTime > headerUntil) { headerUntil = 0; header.text = headerDefault; header.color = UiKit.White; }
            if (dialog.gameObject.activeSelf)
            {
                int h = MenuKeys.Horizontal + MenuKeys.Vertical;
                if (h != 0) { dialogIndex = (dialogIndex + h + 2) % 2; GameAudio.Se("select"); RefreshDialog(); }
                if (MenuKeys.Confirm) dialogAnswer = dialogIndex;
                else if (MenuKeys.Cancel) dialogAnswer = 1;
                return;
            }
            if (!CanInput) return;
            int v = MenuKeys.Vertical;
            if (v != 0) { cursor = (cursor + v + saves.Length) % saves.Length; GameAudio.Se("select"); Refresh(); }
            if (MenuKeys.Confirm) StartCoroutine(Pick());
            else if (MenuKeys.Cancel) { Result = -1; done = true; }
        }

        IEnumerator Pick()
        {
            if (done) yield break;
            GameAudio.Se("select");
            var save = saves[cursor];
            if (!forSave)
            {
                if (save == null) { Flash("이 슬롯에는 모험이 없어요."); yield break; }
                Result = cursor;
                done = true;
                yield break;
            }
            if (save != null)
            {
                yield return Ask($"슬롯 {cursor + 1}에 있는 모험(웨이브 {save.wave})을 지우고\n새 모험을 시작할까요?");
                if (dialogAnswer != 0) yield break;
            }
            Result = cursor;
            done = true;
        }

        void Flash(string text)
        {
            GameAudio.Se("error");
            header.text = text;
            header.color = UiKit.Gold;
            headerUntil = Time.unscaledTime + 2f;
        }

        // ── 확인 창 ──
        int dialogIndex, dialogAnswer = -1;

        IEnumerator Ask(string question)
        {
            dialogText.text = question;
            dialogIndex = 1;                // 실수로 지우지 않게 "아니오"부터
            dialogAnswer = -1;
            dialog.SetAsLastSibling();
            dialog.gameObject.SetActive(true);
            RefreshDialog();
            yield return null;              // 이번 프레임의 Enter가 바로 "예"가 되지 않게
            while (dialogAnswer < 0) yield return null;
            dialog.gameObject.SetActive(false);
        }

        void RefreshDialog()
        {
            dialogCursor.rectTransform.anchoredPosition = new Vector2(16 + dialogIndex * 70, -34.5f);
        }

        // ── 화면 갱신 ──
        void Refresh()
        {
            if (cursor < top) top = cursor;
            if (cursor >= top + Visible) top = cursor - Visible + 1;
            top = Mathf.Clamp(top, 0, Mathf.Max(0, saves.Length - Visible));

            for (int i = 0; i < views.Count; i++)
            {
                int slot = top + i;
                var v = views[i];
                bool selected = slot == cursor;
                v.rt.anchoredPosition = new Vector2(selected ? SlotX - 6 : SlotX, -(SlotTop + i * (SlotH + SlotGap)));
                v.corners.gameObject.SetActive(selected);
                Fill(v, slot, saves[slot]);
                if (selected) arrow.anchoredPosition = new Vector2(SlotX + SlotW + 1, -(SlotTop + i * (SlotH + SlotGap) + SlotH / 2f - 4.5f));
            }
        }

        void Fill(SlotView v, int slot, RunSave s)
        {
            bool has = s != null;
            v.title.text = has ? $"슬롯 {slot + 1}  (모험 {s.runNumber})" : "";
            v.line2.text = has ? $"{s.AreaName} - 웨이브 {s.wave}" : "";
            v.line3.text = has ? s.SavedAtText : "";
            v.line4.text = has ? s.PlayTimeText : "";
            v.empty.text = has ? "" : $"슬롯 {slot + 1}  -  빈 슬롯";
            for (int j = 0; j < v.icons.Length; j++)
            {
                PalData data = null;
                PalSave ps = has && j < s.party.Count ? s.party[j] : null;
                if (ps != null) palLookup.TryGetValue(ps.pal, out data);
                v.icons[j].enabled = data != null && data.frontSprite != null;
                if (v.icons[j].enabled)
                {
                    v.icons[j].sprite = data.frontSprite;
                    v.icons[j].color = ps.hp > 0 ? Color.white : new Color(0.45f, 0.45f, 0.45f);   // 쓰러진 팰은 어둡게
                }
                v.levels[j].text = ps != null ? $"Lv{ps.level}" : "";
            }
        }
    }
}
