using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 모험을 시작하기 전에 데려갈 팰을 고르는 화면 (포켓로그의 스타팅 포켓몬 선택 화면).
    ///  - 가운데 칸: 모든 팰. 왼쪽 위 숫자 = 코스트. 아직 잡지 못한 팰은 검은 그림자로 보이고 고를 수 없다
    ///  - 왼쪽: 커서가 올라간 팰의 정보 (번호·코스트·잡은 수·속성·처음 기술·능력치), 애니메이션 팰은 움직인다
    ///  - 오른쪽: 랜덤 / 파티 6칸 / 코스트 합계·시작
    ///  - 위쪽: 타입 필터, 정렬
    /// 조작: 방향키 이동, Enter·Z·Space 선택, Esc·X 취소. 마우스로도 누를 수 있다.
    /// 화면은 코드로 만든다 (씬에 따로 만들 필요 없음). 사용법: yield return screen.Choose(팰목록); screen.Result
    /// </summary>
    public class StarterSelectScreen : MonoBehaviour
    {
        const int SortingOrder = 400;          // 전투 화면 위, 페이드(500) 아래
        const int Cols = 8, VisibleRows = 8;
        const float Cell = 18f;
        const float GridX = 116f, GridY = 24f, GridPad = 5f;
        const float RightX = 274f, RightW = 42f;
        const float InputGuardSeconds = 0.25f;
        const float RepeatDelay = 0.35f, RepeatRate = 0.075f;

        static readonly Color Background = Hex(0x2c2a3c);
        static readonly Color FrameRed = Hex(0xd23c3c), FrameFill = Hex(0x3b3950);
        static readonly Color GridBorder = Hex(0xb8b8b8), GridFill = Hex(0x8e8e96), CellInParty = Hex(0x9ccf7a);
        static readonly Color TabGray = Hex(0x5c5b70), InfoFill = Hex(0xdcd8c4), DetailFill = Hex(0xf8f8f8);
        static readonly Color White = Hex(0xf8f8f8), Dark = Hex(0x404040), Gold = Hex(0xf8d030), Soft = Hex(0xb0b0c0);
        static readonly Color CursorRed = Hex(0xe8402c);
        // Skill_type 이미지(1024x572)에서 뱃지가 그려진 부분 (전투 화면과 같은 값)
        static readonly Rect BadgeUv = new Rect(255f / 1024f, 129f / 572f, 514f / 1024f, 310f / 572f);

        static readonly string[] SortNames = { "번호순", "코스트순", "해금순" };

        // ───── 화면 요소 ─────
        TMP_FontAsset font;
        RectTransform root;
        TMP_Text noText, costText, caughtText, nameText, statsText, hintText, skillHeader;
        Image preview;
        RawImage[] typeBadges;
        TMP_Text[] typeFallback;
        Image[] skillBars;
        TMP_Text[] skillTexts;
        CellView[] cells;
        TMP_Text[] tabTexts;
        Image barCursor;
        Image[] partyIcons;
        Image[] partySlots;
        Image[] emptyBases, emptyMarks;      // 빈 칸: 회색 받침 + 위아래로 떠다니는 물음표
        float[] iconBaseY, markBaseY;
        TMP_Text costSumText, startText, randomText;
        RectTransform brackets;
        GameObject popup;
        RectTransform popupRt;
        TMP_Text[] popupTexts;
        Image popupCursor;

        // 애니메이션 팰 미리보기: 숨긴 스프라이트에 애니메이터를 돌리고, 바뀐 그림을 UI에 옮긴다
        SpriteRenderer rigRenderer;
        Animator rigAnimator;
        bool previewAnimated;

        class CellView
        {
            public RectTransform rt;
            public Image bg, icon;
            public TMP_Text cost;
        }

        // ───── 상태 ─────
        enum Area { Bar, Grid, Right }
        Area area = Area.Grid;
        int tab, gridIndex, rightIndex, scrollRow;
        int typeFilter = -1;      // -1 = 전체, 아니면 PalType 값
        int sortMode;
        List<PalData> all = new List<PalData>();
        List<PalData> shown = new List<PalData>();
        readonly List<PalData> party = new List<PalData>();
        bool done;
        float openedAt, messageUntil;
        string defaultHint = "방향키: 이동   Enter: 선택\nEsc: 취소";

        bool popupOpen;
        int popupIndex;
        readonly List<(string label, Action action)> popupItems = new List<(string, Action)>();

        float repeatTimer;
        Vector2Int heldDir;

        /// <summary>시작을 눌렀을 때 고른 파티</summary>
        public List<PalData> Result => party.ToList();

        // ═════════════════ 만들기 ═════════════════

        public static StarterSelectScreen Create(TMP_FontAsset font)
        {
            var go = new GameObject("StarterSelectScreen", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320f, 180f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var screen = go.AddComponent<StarterSelectScreen>();
            screen.font = font;
            screen.Build();
            go.SetActive(false);
            return screen;
        }

        void Build()
        {
            // 화면 전체 배경 (화면 비율이 달라도 빈 곳 없이)
            var bg = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
            var bgRt = bg.rectTransform;
            bgRt.SetParent(transform, false);
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(-4f, -4f); bgRt.offsetMax = new Vector2(4f, 4f);
            bg.color = Background;

            // 320x180 기준 판 (가운데)
            root = new GameObject("Layout", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(320f, 180f);

            BuildLeft();
            BuildBar();
            BuildGrid();
            BuildRight();
            BuildPopup();
            brackets = BuildBrackets(root);

            var rig = new GameObject("StarterPreviewRig");
            rig.transform.SetParent(transform, false);
            rig.transform.position = new Vector3(0f, -1000f, 0f);
            rigRenderer = rig.AddComponent<SpriteRenderer>();
            rigRenderer.enabled = false;
            rigAnimator = rig.AddComponent<Animator>();
            rigAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        void BuildLeft()
        {
            var header = Box(root, "NoTab", 4, 4, 62, 16, TabGray);
            noText = Label(header, "No", 4, -2, 58, 16, 13, TextAlignmentOptions.Left, White);

            var info = Box(root, "Info", 4, 21, 108, 86, InfoFill);
            costText = Label(info, "Cost", 3, 2, 60, 10, 8, TextAlignmentOptions.TopLeft, Dark);
            caughtText = Label(info, "Caught", 3, 11, 60, 10, 8, TextAlignmentOptions.TopLeft, Dark);

            preview = Box(info, "Preview", 3, 22, 58, 46, null).gameObject.AddComponent<Image>();
            preview.preserveAspect = true;
            preview.raycastTarget = false;

            typeBadges = new RawImage[2];
            typeFallback = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                var b = Box(info, "Type" + i, 3 + i * 27, 71, 25, 13, null);
                typeBadges[i] = b.gameObject.AddComponent<RawImage>();
                typeBadges[i].raycastTarget = false;
                typeFallback[i] = Label(b, "Text", 0, 0, 25, 13, 8, TextAlignmentOptions.Center, White);
            }

            skillHeader = Label(info, "SkillHeader", 63, 2, 44, 10, 7, TextAlignmentOptions.Center, Dark);
            skillBars = new Image[4];
            skillTexts = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                var bar = Box(info, "Skill" + i, 63, 12 + i * 12, 43, 11, Soft);
                skillBars[i] = bar.GetComponent<Image>();
                var t = Label(bar, "Text", 1, 0, 41, 11, 7, TextAlignmentOptions.Center, White);
                t.enableAutoSizing = true;
                t.fontSizeMin = 4f;
                t.fontSizeMax = 7f;
                skillTexts[i] = t;
            }

            var nameBar = Box(root, "NameBar", 4, 108, 108, 15, TabGray);
            nameText = Label(nameBar, "Name", 4, 0, 102, 15, 13, TextAlignmentOptions.Left, White);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 8f;
            nameText.fontSizeMax = 13f;

            var detail = Box(root, "Detail", 4, 124, 108, 36, DetailFill);
            statsText = Label(detail, "Stats", 4, 2, 102, 33, 8, TextAlignmentOptions.TopLeft, Dark);
            statsText.lineSpacing = -6f;

            hintText = Label(root, "Hint", 4, 162, 110, 17, 7, TextAlignmentOptions.TopLeft, White);
            hintText.lineSpacing = 0f;
        }

        void BuildBar()
        {
            var bar = Frame(root, "FilterBar", GridX, 4, 200, 18, FrameRed, FrameFill);
            tabTexts = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                var t = Label(root, "Tab" + i, GridX + 14 + i * 96, 4, 84, 18, 10, TextAlignmentOptions.Left, White);
                t.raycastTarget = true;
                Clickable.Add(t.gameObject, () => { if (!popupOpen) { area = Area.Bar; tab = index; RefreshCursor(); } },
                                             () => { if (!popupOpen) { area = Area.Bar; tab = index; Confirm(); } });
                tabTexts[i] = t;
            }
            barCursor = Box(root, "BarCursor", 0, 9, 4, 7, null).gameObject.AddComponent<Image>();
            barCursor.sprite = Triangle;
            barCursor.raycastTarget = false;
        }

        void BuildGrid()
        {
            Frame(root, "Grid", GridX, GridY, Cols * Cell + GridPad * 2, VisibleRows * Cell + GridPad * 2, GridBorder, GridFill);
            cells = new CellView[Cols * VisibleRows];
            for (int r = 0; r < VisibleRows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    int slot = r * Cols + c;
                    var rt = Box(root, $"Cell{slot}", GridX + GridPad + c * Cell, GridY + GridPad + r * Cell, Cell, Cell, new Color(1f, 1f, 1f, 0f));
                    var view = new CellView { rt = rt, bg = rt.GetComponent<Image>() };
                    view.bg.raycastTarget = true;
                    view.icon = Box(rt, "Icon", 1, 2, 16, 15, null).gameObject.AddComponent<Image>();
                    view.icon.preserveAspect = true;
                    view.icon.raycastTarget = false;
                    view.cost = Label(rt, "Cost", 0.5f, -1, 10, 8, 7, TextAlignmentOptions.TopLeft, White);
                    Clickable.Add(rt.gameObject,
                        () => { if (!popupOpen && SlotToIndex(slot) < shown.Count) { area = Area.Grid; gridIndex = SlotToIndex(slot); Refresh(); } },
                        () => { if (!popupOpen && SlotToIndex(slot) < shown.Count) { area = Area.Grid; gridIndex = SlotToIndex(slot); Confirm(); } });
                    cells[slot] = view;
                }
        }

        void BuildRight()
        {
            var random = Frame(root, "Random", RightX, GridY, RightW, 18, FrameRed, FrameFill);
            randomText = Label(random, "Text", 0, 0, RightW - 4, 14, 10, TextAlignmentOptions.Center, White);
            randomText.text = "랜덤";
            ClickRight(random.parent.gameObject, 0);

            Frame(root, "Party", RightX, 45, RightW, 98, FrameRed, FrameFill);
            partySlots = new Image[GameConfig.MaxPartySize];
            partyIcons = new Image[GameConfig.MaxPartySize];
            emptyBases = new Image[GameConfig.MaxPartySize];
            emptyMarks = new Image[GameConfig.MaxPartySize];
            iconBaseY = new float[GameConfig.MaxPartySize];
            markBaseY = new float[GameConfig.MaxPartySize];
            for (int i = 0; i < partySlots.Length; i++)
            {
                var slot = Box(root, "Slot" + i, RightX + 5, 49 + i * 15, RightW - 10, 14, new Color(1f, 1f, 1f, 0f));
                partySlots[i] = slot.GetComponent<Image>();
                partySlots[i].raycastTarget = true;
                float cx = (RightW - 10) / 2f;
                // 빈 칸 표시 (포켓로그처럼 받침 위에 물음표)
                emptyBases[i] = Box(slot, "EmptyBase", cx - 9, 7, 18, 7, null).gameObject.AddComponent<Image>();
                emptyBases[i].sprite = EmptyBaseSprite;
                emptyBases[i].raycastTarget = false;
                emptyMarks[i] = Box(slot, "EmptyMark", cx - 5, 0, 9, 11, null).gameObject.AddComponent<Image>();
                emptyMarks[i].sprite = QuestionSprite;
                emptyMarks[i].raycastTarget = false;
                markBaseY[i] = 0f;
                partyIcons[i] = Box(slot, "Icon", cx - 9, -1, 18, 15, null).gameObject.AddComponent<Image>();
                partyIcons[i].preserveAspect = true;
                partyIcons[i].raycastTarget = false;
                iconBaseY[i] = -1f;
                ClickRight(slot.gameObject, i + 1);
            }

            var start = Frame(root, "Start", RightX, 146, RightW, 32, FrameRed, FrameFill);
            costSumText = Label(start, "Cost", 0, 1, RightW - 4, 13, 10, TextAlignmentOptions.Center, White);
            startText = Label(start, "Text", 0, 14, RightW - 4, 13, 11, TextAlignmentOptions.Center, White);
            startText.text = "시작";
            ClickRight(start.parent.gameObject, GameConfig.MaxPartySize + 1);
        }

        void ClickRight(GameObject go, int index)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            Clickable.Add(go, () => { if (!popupOpen) { area = Area.Right; rightIndex = index; Refresh(); } },
                              () => { if (!popupOpen) { area = Area.Right; rightIndex = index; Confirm(); } });
        }

        void BuildPopup()
        {
            popupRt = Frame(root, "Popup", 0, 0, 74, 40, FrameRed, FrameFill).parent as RectTransform;
            popup = popupRt.gameObject;
            popupTexts = new TMP_Text[4];
            for (int i = 0; i < popupTexts.Length; i++)
            {
                int row = i;
                var t = Label(popupRt, "Item" + i, 12, 4 + i * 12, 60, 12, 9, TextAlignmentOptions.Left, White);
                t.raycastTarget = true;
                Clickable.Add(t.gameObject, () => { popupIndex = row; RefreshPopup(); }, () => { popupIndex = row; PickPopup(); });
                popupTexts[i] = t;
            }
            popupCursor = Box(popupRt, "Cursor", 5, 6, 4, 7, null).gameObject.AddComponent<Image>();
            popupCursor.sprite = Triangle;
            popupCursor.raycastTarget = false;
            popup.SetActive(false);
        }

        /// <summary>포켓로그처럼 네 귀퉁이에 붉은 꺾쇠가 있는 커서</summary>
        RectTransform BuildBrackets(RectTransform parent)
        {
            var rt = Box(parent, "Cursor", 0, 0, 20, 20, null);
            const float len = 5f, thick = 2f;
            for (int corner = 0; corner < 4; corner++)
            {
                var ax = new Vector2(corner % 2, corner / 2 == 0 ? 1 : 0);
                for (int part = 0; part < 2; part++)
                {
                    var img = new GameObject("Bracket", typeof(RectTransform)).AddComponent<Image>();
                    img.color = CursorRed;
                    img.raycastTarget = false;
                    var b = img.rectTransform;
                    b.SetParent(rt, false);
                    b.anchorMin = b.anchorMax = ax;
                    b.pivot = ax;
                    b.anchoredPosition = Vector2.zero;
                    b.sizeDelta = part == 0 ? new Vector2(len, thick) : new Vector2(thick, len);
                }
            }
            return rt;
        }

        // ═════════════════ 열기 / 닫기 ═════════════════

        /// <summary>화면을 열고 "시작"을 누를 때까지 기다린다. 결과는 Result</summary>
        public IEnumerator Choose(IEnumerable<PalData> pals)
        {
            Open(pals);
            while (!done) yield return null;
        }

        public void Open(IEnumerable<PalData> pals)
        {
            all = pals.Where(GameConfig.CanBeStarter).Distinct().ToList();
            party.RemoveAll(p => !all.Contains(p) || !PlayerSave.IsUnlocked(p));   // 지난번 파티는 그대로 남겨 둔다
            done = false;
            BackRequested = false;
            defaultHint = AllowBack ? "방향키: 이동   Enter: 선택\nEsc: 취소 · 타이틀로" : "방향키: 이동   Enter: 선택\nEsc: 취소";
            popupOpen = false;
            popup.SetActive(false);
            area = Area.Grid;
            gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);   // 전투 화면 버튼이 Enter를 받지 않게
            openedAt = Time.unscaledTime;
            messageUntil = 0f;
            hintText.text = defaultHint;
            hintText.color = White;
            ApplyFilter(null);
            if (gridIndex >= shown.Count) gridIndex = 0;
            Refresh();
        }

        public void Hide()
        {
            if (rigAnimator != null) rigAnimator.runtimeAnimatorController = null;
            gameObject.SetActive(false);
        }

        // ═════════════════ 입력 ═════════════════

        void Update()
        {
            if (done || Time.unscaledTime - openedAt < InputGuardSeconds) return;
            if (messageUntil > 0f && Time.unscaledTime > messageUntil) { messageUntil = 0f; hintText.color = White; hintText.text = defaultHint; }

            var dir = ReadDirection();
            if (popupOpen)
            {
                if (dir.y != 0) { popupIndex = (popupIndex - dir.y + popupItems.Count) % popupItems.Count; RefreshPopup(); }
                if (Keys.Confirm) PickPopup();
                else if (Keys.Cancel) ClosePopup();
                return;
            }
            if (dir != Vector2Int.zero) Move(dir);
            if (Keys.Confirm) Confirm();
            else if (Keys.Cancel) Cancel();
        }

        void LateUpdate()
        {
            if (previewAnimated && rigRenderer.sprite != null) preview.sprite = rigRenderer.sprite;

            // 파티 칸: 물음표와 들어온 팰이 위아래로 통통 (포켓로그처럼 1픽셀씩 끊어서, 칸마다 박자를 조금씩 엇갈리게)
            float t = Time.unscaledTime;
            for (int i = 0; i < partyIcons.Length; i++)
            {
                float up = Bob(t, i);
                SetY(partyIcons[i].rectTransform, iconBaseY[i], up);
                SetY(emptyMarks[i].rectTransform, markBaseY[i], up);
            }
        }

        const float BobPeriod = 0.9f;
        /// <summary>0 → 1 → 0 → -1 … 1픽셀씩 끊어서 위아래로 (위가 +)</summary>
        static float Bob(float t, int i)
        {
            float phase = Mathf.Repeat(t / BobPeriod + i * 0.17f, 1f);
            return phase < 0.25f ? 0f : phase < 0.5f ? 1f : phase < 0.75f ? 0f : -1f;
        }

        /// <summary>왼쪽 위 기준 y(아래로 +)에서 up 픽셀만큼 위로</summary>
        static void SetY(RectTransform rt, float topY, float up) =>
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -topY + up);

        /// <summary>방향키 (누르고 있으면 반복)</summary>
        Vector2Int ReadDirection()
        {
            var pressed = Keys.DirectionDown;
            if (pressed != Vector2Int.zero)
            {
                heldDir = pressed;
                repeatTimer = RepeatDelay;
                return pressed;
            }
            var held = Keys.DirectionHeld;
            if (held == Vector2Int.zero || held != heldDir) { heldDir = Vector2Int.zero; return Vector2Int.zero; }
            repeatTimer -= Time.unscaledDeltaTime;
            if (repeatTimer > 0f) return Vector2Int.zero;
            repeatTimer = RepeatRate;
            return held;
        }

        void Move(Vector2Int d)     // d.y: 위 = +1
        {
            switch (area)
            {
                case Area.Bar:
                    if (d.x != 0) tab = Mathf.Clamp(tab + d.x, 0, tabTexts.Length - 1);
                    if (d.y < 0 && shown.Count > 0) { area = Area.Grid; gridIndex = Mathf.Min(scrollRow * Cols + tab * 4, shown.Count - 1); }
                    break;

                case Area.Grid:
                {
                    if (shown.Count == 0) { if (d.y > 0) area = Area.Bar; else if (d.x > 0) area = Area.Right; break; }
                    int row = gridIndex / Cols, col = gridIndex % Cols;
                    int lastRow = (shown.Count - 1) / Cols;
                    if (d.x > 0)
                    {
                        if (col == Cols - 1 || gridIndex == shown.Count - 1) { area = Area.Right; rightIndex = Mathf.Clamp(row - scrollRow, 0, GameConfig.MaxPartySize + 1); }
                        else gridIndex++;
                    }
                    else if (d.x < 0) { if (col > 0) gridIndex--; }
                    else if (d.y > 0)
                    {
                        if (row == 0) { area = Area.Bar; tab = col < Cols / 2 ? 0 : 1; }
                        else gridIndex -= Cols;
                    }
                    else if (d.y < 0)
                    {
                        if (row < lastRow) gridIndex = Mathf.Min(gridIndex + Cols, shown.Count - 1);
                        else gridIndex = col;      // 맨 아래에서 아래 = 맨 위로
                    }
                    break;
                }

                case Area.Right:
                    int count = GameConfig.MaxPartySize + 2;   // 랜덤, 파티 6칸, 시작
                    if (d.y != 0) rightIndex = (rightIndex - d.y + count) % count;
                    else if (d.x < 0 && shown.Count > 0)
                    {
                        area = Area.Grid;
                        int row = Mathf.Min(scrollRow + rightIndex, (shown.Count - 1) / Cols);
                        gridIndex = Mathf.Min(row * Cols + Cols - 1, shown.Count - 1);
                    }
                    break;
            }
            Refresh();
        }

        void Confirm()
        {
            switch (area)
            {
                case Area.Bar:
                    if (tab == 0) CycleType();
                    else { sortMode = (sortMode + 1) % SortNames.Length; ApplyFilter(Current); }
                    Refresh();
                    break;

                case Area.Grid:
                {
                    var d = Current;
                    if (d == null) return;
                    if (!PlayerSave.IsUnlocked(d)) { ShowMessage("아직 잡지 못한 팰이에요.\n한 번 잡으면 고를 수 있어요!"); return; }
                    popupItems.Clear();
                    if (party.Contains(d)) popupItems.Add(("파티에서 빼기", () => { party.Remove(d); Refresh(); }));
                    else popupItems.Add(("파티에 추가", () => TryAdd(d)));
                    popupItems.Add(("취소", null));
                    OpenPopup(cells[IndexToSlot(gridIndex)].rt);
                    break;
                }

                case Area.Right:
                    if (rightIndex == 0) AddRandom();
                    else if (rightIndex <= GameConfig.MaxPartySize)
                    {
                        int i = rightIndex - 1;
                        if (i >= party.Count) return;
                        var d = party[i];
                        popupItems.Clear();
                        popupItems.Add(("파티에서 빼기", () => { party.Remove(d); Refresh(); }));
                        popupItems.Add(("취소", null));
                        OpenPopup((RectTransform)partySlots[i].transform, true);
                    }
                    else StartRun();
                    break;
            }
        }

        void Cancel()
        {
            // 파티 칸에서 Esc = 마지막 팰 빼기 (포켓로그처럼)
            if (party.Count > 0 && area == Area.Right)
            {
                party.RemoveAt(party.Count - 1);
                Refresh();
            }
            else if (AllowBack)          // 그 밖의 곳에서 Esc = 타이틀로 돌아가기
            {
                BackRequested = true;
                done = true;
            }
        }

        /// <summary>Esc로 화면을 나갈 수 있는지 (타이틀에서 들어온 경우)</summary>
        public bool AllowBack;
        /// <summary>Esc로 나갔는지 (시작을 누른 게 아니라)</summary>
        public bool BackRequested { get; private set; }

        // ═════════════════ 동작 ═════════════════

        int UsedCost => party.Sum(GameConfig.StarterCost);
        int LeftCost => GameConfig.StarterBudget - UsedCost;

        void TryAdd(PalData d)
        {
            if (party.Contains(d)) return;
            if (party.Count >= GameConfig.MaxPartySize) { ShowMessage("파티가 가득 찼어요!"); return; }
            int cost = GameConfig.StarterCost(d);
            if (cost > LeftCost) { ShowMessage($"코스트가 모자라요!\n(남은 코스트 {LeftCost})"); return; }
            party.Add(d);
            Refresh();
        }

        void AddRandom()
        {
            if (party.Count >= GameConfig.MaxPartySize) { ShowMessage("파티가 가득 찼어요!"); return; }
            var options = all.Where(d => PlayerSave.IsUnlocked(d) && !party.Contains(d) && GameConfig.StarterCost(d) <= LeftCost).ToList();
            if (options.Count == 0) { ShowMessage("더 넣을 수 있는 팰이 없어요!"); return; }
            var pick = options[UnityEngine.Random.Range(0, options.Count)];
            party.Add(pick);
            // 고른 팰로 커서를 옮겨서 정보를 보여 준다
            int at = shown.IndexOf(pick);
            if (at >= 0) gridIndex = at;
            Refresh(pick);
        }

        void StartRun()
        {
            if (party.Count == 0) { ShowMessage("파티에 팰을 넣어 주세요!"); return; }
            done = true;
        }

        void CycleType()
        {
            var types = ((PalType[])Enum.GetValues(typeof(PalType))).Where(t => t != PalType.Void).ToList();
            int i = typeFilter < 0 ? -1 : types.IndexOf((PalType)typeFilter);
            i++;
            typeFilter = i >= types.Count ? -1 : (int)types[i];
            ApplyFilter(Current);
        }

        void ApplyFilter(PalData keep)
        {
            IEnumerable<PalData> q = all;
            if (typeFilter >= 0) q = q.Where(d => d.HasType((PalType)typeFilter));
            q = sortMode switch
            {
                1 => q.OrderByDescending(GameConfig.StarterCost).ThenBy(Number).ThenBy(d => d.name),
                2 => q.OrderByDescending(d => PlayerSave.IsUnlocked(d)).ThenBy(Number).ThenBy(d => d.name),
                _ => q.OrderBy(Number).ThenBy(d => d.name),
            };
            shown = q.ToList();
            int at = keep != null ? shown.IndexOf(keep) : -1;
            gridIndex = at >= 0 ? at : Mathf.Clamp(gridIndex, 0, Mathf.Max(0, shown.Count - 1));
        }

        static int Number(PalData d) => int.TryParse(d.palId, out var n) ? n : 9999;

        PalData Current => area == Area.Grid && gridIndex >= 0 && gridIndex < shown.Count ? shown[gridIndex] : null;

        // ── 팝업 ──
        void OpenPopup(RectTransform near, bool leftSide = false)
        {
            popupOpen = true;
            popupIndex = 0;
            float h = 8 + popupItems.Count * 12;
            popupRt.sizeDelta = new Vector2(74, h);
            ((RectTransform)popupRt.GetChild(0)).sizeDelta = new Vector2(70, h - 4);   // 안쪽 칸
            var p = near.anchoredPosition;          // 왼쪽 위 기준 (x, -y)
            float x = leftSide ? p.x - 76 : p.x + Cell + 2;
            if (x + 74 > 318) x = p.x - 76;
            float y = Mathf.Clamp(-p.y, 2, 178 - h);
            popupRt.anchoredPosition = new Vector2(x, -y);
            popup.transform.SetAsLastSibling();
            popup.SetActive(true);
            RefreshPopup();
        }

        void RefreshPopup()
        {
            for (int i = 0; i < popupTexts.Length; i++)
            {
                bool on = i < popupItems.Count;
                popupTexts[i].gameObject.SetActive(on);
                if (on) popupTexts[i].text = popupItems[i].label;
            }
            popupCursor.rectTransform.anchoredPosition = new Vector2(5, -(6 + popupIndex * 12));
        }

        void PickPopup()
        {
            var action = popupItems[popupIndex].action;
            ClosePopup();
            action?.Invoke();
        }

        void ClosePopup()
        {
            popupOpen = false;
            popup.SetActive(false);
        }

        void ShowMessage(string text)
        {
            hintText.text = text;
            hintText.color = Gold;
            messageUntil = Time.unscaledTime + 2.5f;
        }

        // ═════════════════ 화면 갱신 ═════════════════

        void Refresh(PalData infoOverride = null)
        {
            // 커서가 보이도록 칸 스크롤
            if (area == Area.Grid && shown.Count > 0)
            {
                int row = gridIndex / Cols;
                if (row < scrollRow) scrollRow = row;
                if (row >= scrollRow + VisibleRows) scrollRow = row - VisibleRows + 1;
            }
            int maxScroll = Mathf.Max(0, (shown.Count - 1) / Cols - VisibleRows + 1);
            scrollRow = Mathf.Clamp(scrollRow, 0, maxScroll);

            RefreshGrid();
            RefreshRight();
            RefreshBar();
            PalData info = infoOverride ?? (area == Area.Grid ? Current
                         : area == Area.Right && rightIndex >= 1 && rightIndex - 1 < party.Count ? party[rightIndex - 1] : null);
            if (info != null || area == Area.Grid) ShowInfo(info);
            RefreshCursor();
        }

        void RefreshGrid()
        {
            int left = LeftCost;
            for (int slot = 0; slot < cells.Length; slot++)
            {
                var view = cells[slot];
                int index = SlotToIndex(slot);
                bool has = index < shown.Count;
                view.icon.enabled = has;
                view.cost.text = "";
                view.bg.color = new Color(1f, 1f, 1f, 0f);
                if (!has) continue;
                var d = shown[index];
                view.icon.sprite = d.frontSprite;
                bool unlocked = PlayerSave.IsUnlocked(d);
                bool inParty = party.Contains(d);
                int cost = GameConfig.StarterCost(d);
                if (!unlocked) view.icon.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);          // 아직 못 잡은 팰 = 그림자
                else if (!inParty && cost > left) view.icon.color = new Color(1f, 1f, 1f, 0.35f); // 코스트가 모자람
                else view.icon.color = Color.white;
                if (inParty) view.bg.color = CellInParty;
                view.cost.text = unlocked ? cost.ToString() : "";
                view.cost.color = cost >= 7 ? Gold : White;
            }
        }

        void RefreshRight()
        {
            for (int i = 0; i < partyIcons.Length; i++)
            {
                bool has = i < party.Count;
                partyIcons[i].enabled = has;
                if (has) partyIcons[i].sprite = party[i].frontSprite;
                emptyBases[i].enabled = !has;          // 팰이 들어오면 물음표는 사라진다
                emptyMarks[i].enabled = !has;
            }
            int used = UsedCost;
            costSumText.text = $"{used}/{GameConfig.StarterBudget}";
            costSumText.color = used >= GameConfig.StarterBudget ? Gold : White;
            startText.color = party.Count > 0 ? White : Soft;
        }

        void RefreshBar()
        {
            tabTexts[0].text = "타입: " + (typeFilter < 0 ? "전체" : PalTypeNames.Korean((PalType)typeFilter));
            tabTexts[1].text = "정렬: " + SortNames[sortMode];
            barCursor.enabled = area == Area.Bar;
            barCursor.rectTransform.anchoredPosition = new Vector2(GridX + 7 + tab * 96, -9.5f);
            for (int i = 0; i < tabTexts.Length; i++) tabTexts[i].color = area == Area.Bar && tab == i ? Gold : White;
        }

        void RefreshCursor()
        {
            RefreshBar();
            brackets.gameObject.SetActive(area != Area.Bar);
            if (area == Area.Grid)
            {
                if (shown.Count == 0) { brackets.gameObject.SetActive(false); return; }
                var rt = cells[IndexToSlot(gridIndex)].rt;
                PlaceBrackets(rt.anchoredPosition.x - 1, -rt.anchoredPosition.y - 1, Cell + 2, Cell + 2);
            }
            else if (area == Area.Right)
            {
                if (rightIndex == 0) PlaceBrackets(RightX - 1, GridY - 1, RightW + 2, 20);
                else if (rightIndex <= GameConfig.MaxPartySize)
                {
                    var rt = (RectTransform)partySlots[rightIndex - 1].transform;
                    PlaceBrackets(rt.anchoredPosition.x - 2, -rt.anchoredPosition.y - 1, rt.sizeDelta.x + 4, rt.sizeDelta.y + 2);
                }
                else PlaceBrackets(RightX - 1, 145, RightW + 2, 34);
            }
            brackets.SetAsLastSibling();
            popup.transform.SetAsLastSibling();
        }

        void PlaceBrackets(float x, float y, float w, float h)
        {
            brackets.anchoredPosition = new Vector2(x, -y);
            brackets.sizeDelta = new Vector2(w, h);
        }

        void ShowInfo(PalData d)
        {
            if (d == null)
            {
                noText.text = "No.---";
                nameText.text = "";
                costText.text = caughtText.text = statsText.text = skillHeader.text = "";
                preview.enabled = false;
                SetPreviewAnimation(null);
                for (int i = 0; i < 2; i++) { typeBadges[i].enabled = false; typeFallback[i].text = ""; }
                for (int i = 0; i < 4; i++) { skillBars[i].enabled = false; skillTexts[i].text = ""; }
                return;
            }

            bool unlocked = PlayerSave.IsUnlocked(d);
            noText.text = "No." + (string.IsNullOrEmpty(d.palId) ? "---" : d.palId);
            preview.enabled = true;
            preview.sprite = d.frontSprite;
            preview.color = unlocked ? Color.white : new Color(0.08f, 0.08f, 0.12f, 0.9f);
            SetPreviewAnimation(unlocked ? d.frontAnimator : null);

            if (!unlocked)
            {
                nameText.text = "???";
                costText.text = "코스트 ?";
                caughtText.text = "잡은 수 0";
                statsText.text = "아직 잡지 못한 팰이다.\n모험에서 잡으면\n스타팅 팰로 고를 수 있다.";
                skillHeader.text = "";
                for (int i = 0; i < 2; i++) { typeBadges[i].enabled = false; typeFallback[i].text = ""; }
                for (int i = 0; i < 4; i++) { skillBars[i].enabled = false; skillTexts[i].text = ""; }
                return;
            }

            nameText.text = string.IsNullOrEmpty(d.palName) ? d.name : d.palName;
            costText.text = $"코스트 {GameConfig.StarterCost(d)}" + (GameConfig.IsStarterPal(d) ? "  <color=#2c7a2c>스타팅</color>" : "");
            caughtText.text = $"잡은 수 {PlayerSave.CaughtCount(d)}";
            int total = d.baseHP + d.baseAttack + d.baseDefense + d.baseSpeed;
            statsText.text = $"HP {d.baseHP,-4} 공격 {d.baseAttack}\n방어 {d.baseDefense,-4} 스피드 {d.baseSpeed}\n능력치 합계 {total}";

            SetType(0, d.primaryType, true);
            SetType(1, d.secondaryType, d.hasSecondaryType && d.secondaryType != d.primaryType);

            // 레벨 5로 시작할 때 아는 기술 (Pal과 같은 규칙: 배운 레벨 순서로 마지막 4개)
            var skills = (d.learnset ?? new List<LearnsetEntry>())
                .Where(e => e.skill != null && e.level <= GameConfig.StarterLevel)
                .OrderBy(e => e.level).Select(e => e.skill).Distinct().ToList();
            if (skills.Count > 4) skills = skills.Skip(skills.Count - 4).ToList();
            skillHeader.text = "처음 기술";
            for (int i = 0; i < 4; i++)
            {
                bool has = i < skills.Count;
                skillBars[i].enabled = true;
                skillBars[i].color = has ? TypeColor(skills[i].type) : new Color(0.55f, 0.55f, 0.6f, 0.6f);
                skillTexts[i].text = has ? (string.IsNullOrEmpty(skills[i].skillName) ? skills[i].name : skills[i].skillName) : "-";
            }
        }

        void SetType(int i, PalType type, bool on)
        {
            typeBadges[i].enabled = on;
            typeFallback[i].text = "";
            if (!on) return;
            var tex = Resources.Load<Texture2D>($"1.팰/2.Elements/Skill_type/{type}_sk");
            typeBadges[i].texture = tex;
            typeBadges[i].uvRect = tex != null ? BadgeUv : new Rect(0, 0, 1, 1);
            typeBadges[i].color = tex != null ? Color.white : TypeColor(type);
            if (tex == null) typeFallback[i].text = PalTypeNames.Korean(type);
        }

        void SetPreviewAnimation(RuntimeAnimatorController ctrl)
        {
            previewAnimated = ctrl != null;
            if (rigAnimator.runtimeAnimatorController == ctrl) return;
            rigAnimator.runtimeAnimatorController = ctrl;
            if (ctrl != null) { rigAnimator.Rebind(); rigAnimator.Update(0f); }
        }

        int SlotToIndex(int slot) => scrollRow * Cols + slot;
        int IndexToSlot(int index) => Mathf.Clamp(index - scrollRow * Cols, 0, cells.Length - 1);

        // ═════════════════ 도우미 ═════════════════

        public static Color TypeColor(PalType t) => t switch
        {
            PalType.Neutral => Hex(0xa8a878),
            PalType.Fire => Hex(0xf08030),
            PalType.Water => Hex(0x6890f0),
            PalType.Grass => Hex(0x78c850),
            PalType.Electric => Hex(0xe0b020),
            PalType.Ground => Hex(0xc0a050),
            PalType.Ice => Hex(0x70c8c8),
            PalType.Dragon => Hex(0x7038f8),
            PalType.Dark => Hex(0x705848),
            PalType.Void => Hex(0x4a2c6e),
            _ => Color.gray,
        };

        /// <summary>왼쪽 위 기준 (x, y)에 w×h 칸. color가 있으면 단색 Image를 붙인다</summary>
        static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color? color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            if (color.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = color.Value;
                img.raycastTarget = false;
            }
            return rt;
        }

        /// <summary>테두리 색 칸 안에 채움 색 칸 (2픽셀 테두리). 안쪽 칸을 돌려준다 (바깥 칸 = parent)</summary>
        static RectTransform Frame(Transform parent, string name, float x, float y, float w, float h, Color border, Color fill)
        {
            var outer = Box(parent, name, x, y, w, h, border);
            return Box(outer, "Inner", 2, 2, w - 4, h - 4, fill);
        }

        TMP_Text Label(Transform parent, string name, float x, float y, float w, float h, float size, TextAlignmentOptions align, Color color)
        {
            var rt = Box(parent, name, x, y, w, h, null);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align == TextAlignmentOptions.Left ? TextAlignmentOptions.MidlineLeft : align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.color = color;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        // ── 빈 파티 칸 그림 (코드로 그린 픽셀 그림) ──
        static Sprite emptyBase, question;

        /// <summary>회색 타원 받침 (18x7)</summary>
        static Sprite EmptyBaseSprite => emptyBase != null ? emptyBase : (emptyBase = PixelSprite(new[]
        {
            "....OOOOOOOOOO....",
            "..OOGGGGGGGGGGOO..",
            ".OGGGGGGGGGGGGGGO.",
            "OGGGGGGGGGGGGGGGGO",
            ".OGGGGGGGGGGGGGGO.",
            "..OOGGGGGGGGGGOO..",
            "....OOOOOOOOOO....",
        }));

        /// <summary>입체 물음표 (9x11): 흰 앞면 + 오른쪽 위로 보이는 옆면</summary>
        static Sprite QuestionSprite
        {
            get
            {
                if (question != null) return question;
                string[] mark =
                {
                    ".#####.",
                    "#######",
                    "##...##",
                    "....###",
                    "...###.",
                    "...##..",
                    ".......",
                    "...##..",
                    "...##..",
                };
                var rows = new char[11][];
                for (int y = 0; y < 11; y++) rows[y] = new string('.', 9).ToCharArray();
                void Stamp(int ox, int oy, char c)
                {
                    for (int y = 0; y < mark.Length; y++)
                        for (int x = 0; x < mark[y].Length; x++)
                            if (mark[y][x] == '#') rows[oy + y][ox + x] = c;
                }
                Stamp(2, 0, 'S');      // 뒤쪽 옆면
                Stamp(1, 1, 'S');
                Stamp(0, 2, 'W');      // 앞면
                question = PixelSprite(rows.Select(r => new string(r)).ToArray());
                return question;
            }
        }

        /// <summary>글자 그림으로 픽셀 스프라이트 만들기 (위 줄이 위쪽). W 흰색, S 옆면, G 받침, O 받침 테두리</summary>
        static Sprite PixelSprite(string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = rows[h - 1 - y][x];
                    tex.SetPixel(x, y, c switch
                    {
                        'W' => Hex(0xf4f4ec),
                        'S' => Hex(0x8a8a74),
                        'G' => Hex(0xbcbcbc),
                        'O' => Hex(0x9a9a9a),
                        _ => Color.clear,
                    });
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
        }

        static Sprite triangle;
        /// <summary>▶ 모양 커서 (4x7 픽셀)</summary>
        static Sprite Triangle
        {
            get
            {
                if (triangle != null) return triangle;
                var tex = new Texture2D(4, 7, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 4; x++)
                        tex.SetPixel(x, y, x <= 3 - Mathf.Abs(y - 3) ? Color.white : Color.clear);
                tex.Apply();
                triangle = Sprite.Create(tex, new Rect(0, 0, 4, 7), new Vector2(0.5f, 0.5f), 1f);
                return triangle;
            }
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        /// <summary>마우스: 올리면 enter, 누르면 click</summary>
        class Clickable : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
        {
            Action enter, click;

            public static void Add(GameObject go, Action enter, Action click)
            {
                var c = go.AddComponent<Clickable>();
                c.enter = enter;
                c.click = click;
            }

            public void OnPointerEnter(PointerEventData e) => enter?.Invoke();
            public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) click?.Invoke(); }
        }

        /// <summary>키보드 (새 입력 시스템 / 예전 입력 둘 다)</summary>
        static class Keys
        {
#if ENABLE_INPUT_SYSTEM
            static UnityEngine.InputSystem.Keyboard Kb => UnityEngine.InputSystem.Keyboard.current;
            public static bool Confirm => Kb != null && (Kb.enterKey.wasPressedThisFrame || Kb.numpadEnterKey.wasPressedThisFrame || Kb.zKey.wasPressedThisFrame || Kb.spaceKey.wasPressedThisFrame);
            public static bool Cancel => Kb != null && (Kb.escapeKey.wasPressedThisFrame || Kb.xKey.wasPressedThisFrame || Kb.backspaceKey.wasPressedThisFrame);
            public static Vector2Int DirectionDown => Kb == null ? Vector2Int.zero : new Vector2Int(
                (Kb.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (Kb.leftArrowKey.wasPressedThisFrame ? 1 : 0),
                (Kb.upArrowKey.wasPressedThisFrame ? 1 : 0) - (Kb.downArrowKey.wasPressedThisFrame ? 1 : 0));
            public static Vector2Int DirectionHeld => Kb == null ? Vector2Int.zero : new Vector2Int(
                (Kb.rightArrowKey.isPressed ? 1 : 0) - (Kb.leftArrowKey.isPressed ? 1 : 0),
                (Kb.upArrowKey.isPressed ? 1 : 0) - (Kb.downArrowKey.isPressed ? 1 : 0));
#else
            public static bool Confirm => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space);
            public static bool Cancel => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Backspace);
            public static Vector2Int DirectionDown => new Vector2Int(
                (Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKeyDown(KeyCode.LeftArrow) ? 1 : 0),
                (Input.GetKeyDown(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKeyDown(KeyCode.DownArrow) ? 1 : 0));
            public static Vector2Int DirectionHeld => new Vector2Int(
                (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
#endif
        }
    }
}
