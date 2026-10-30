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
    /// 포켓로그식 "능력치를 본다" 화면 (스테이터스). 320×180 기준 좌표, 포켓로그와 같은 배치.
    ///  ① 상태   : 프로필(어버이·ID·타입·응축 별) / 강화 칸(엔터로 강화 효과 → 패시브 → 각성 → 시너지 → 사용한 아이템) / 트레이너 메모
    ///  ② 능력   : 지닌 물건 / 능력치 (엔터 → 개체값) / 경험치
    ///  ③ 싸울기술: 기술 4개 + 효과·설명. 엔터 → "기술 순서 교체" → 바꿀 기술을 골라 엔터
    /// 입력: ←→ 페이지, ↑↓ 다른 팰(①②) / 기술 고르기(③), Enter·Space·Z 결정, Esc·X·Backspace·우클릭 닫기.
    /// 그림은 Resources/UI/Summary 에 있는 포켓로그 그림을 쓴다. 씬에 미리 만들 필요 없이 처음 쓸 때 코드로 만든다.
    /// </summary>
    public class SummaryScreen : MonoBehaviour
    {
        public const int SortingOrder = 90;        // 파티 화면(80)보다 위
        const string Dir = "UI/Summary/";

        enum Page { Profile, Stats, Moves }
        const int PageCount = 3;

        // ───── 글자 (포켓로그 색) ─────
        const float FontHeader = 15f, FontText = 12f, FontDesc = 10.5f, FontTiny = 7f;
        static readonly Color White = Hex(0xf8f8f8), WhiteShadow = Hex(0x636363);
        static readonly Color Dark = Hex(0x484848), DarkShadow = Hex(0xd0d0c8);
        static readonly Color PinkText = Hex(0xf89890), PinkShadow = Hex(0x984038);
        static readonly Color BlueText = Hex(0x40c8f8), BlueShadow = Hex(0x006090);
        static readonly Color GoldText = Hex(0xe8e8a8), GoldShadow = Hex(0xa0a060);
        static readonly Color PpHalf = Hex(0xccbe00), PpLow = Hex(0xd64b00), PpEmpty = Hex(0xe13d3d), PpShadow = Hex(0x6b5a73);
        // 메모·이름의 강조 표시: [r]빨강[/r] [b]파랑[/b] [g]금색[/g]
        static readonly (string tag, string main, string shadow)[] Marks =
        {
            ("r", "e70808", "ffbd73"), ("b", "40c8f8", "006090"), ("g", "e8e8a8", "a0a060"), ("p", "f89890", "984038"),
        };

        static readonly Rect BadgeUv = new Rect(255f / 1024f, 129f / 572f, 514f / 1024f, 310f / 572f);     // Skill_type 그림의 뱃지 부분
        static readonly Rect CategoryUv = new Rect(152f / 1024f, 129f / 572f, 737f / 1024f, 318f / 572f);  // Attack/Status Icon
        static readonly Rect StatusUv = new Rect(221f / 1024f, 163f / 572f, 565f / 1024f, 242f / 572f);    // statuses 뱃지

        TMP_FontAsset font;
        ChoiceMenu choiceMenu;
        RectTransform root;

        // 왼쪽 (모든 페이지 공통)
        RawImage tab;
        RectTransform nextArea;
        ShadowText number, nameText, levelText, statusLabel;
        Image portrait, sphereIcon;
        RawImage statusBar, statusBadge;
        RectTransform starRow;

        // 페이지
        RawImage pageBg;
        RectTransform[] pages = new RectTransform[PageCount];

        // ① 상태
        ShadowText trainerText, idText, typeLabel, starLabel, abilityLabel, abilityName, abilityDesc, memoText;
        RawImage[] typeBadges = new RawImage[2];
        ShadowText[] typeFallback = new ShadowText[2];
        RectTransform profileStars;

        // ② 능력
        RectTransform itemRoot;
        readonly ShadowText[] statLabels = new ShadowText[6], statValues = new ShadowText[6];
        ShadowText expValue, nextValue, noItemText;
        RawImage expFill;

        // ③ 싸울기술
        readonly List<MoveRow> moveRows = new List<MoveRow>();
        RawImage moveCursor, selectCursor;
        ShadowText moveDescription;
        RectTransform effectPanel;
        ShadowText powerValue, accuracyValue;
        RawImage categoryIcon;

        // 상태
        IReadOnlyList<Pal> party;
        int index;
        Pal Pal => party[index];
        Page page;
        int abilityPage;                // ① 강화 칸이 보여주는 내용 (엔터로 넘긴다)
        bool showIvs;                   // ② 개체값 보기
        int moveCursorIndex;
        int swapFrom = -1;              // 순서를 바꿀 기술 (-1 = 고르는 중 아님)
        int trainerId;
        string trainerName;

        // 마우스 입력 (버튼 → 다음 프레임에 처리)
        int clickedRow = -1;
        int clickedPageDelta;

        class MoveRow
        {
            public RectTransform root;
            public RawImage badge;
            public ShadowText badgeText, name, pp;
            public RawImage ppLabel;
        }

        // ═════════════════ 만들기 ═════════════════

        public static SummaryScreen Create(Transform parent, TMP_FontAsset font, ChoiceMenu choiceMenu)
        {
            var go = new GameObject("SummaryScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var screen = go.AddComponent<SummaryScreen>();
            screen.font = font;
            screen.choiceMenu = choiceMenu;
            screen.Build();
            go.SetActive(false);
            return screen;
        }

        void Build()
        {
            var full = (RectTransform)transform;
            full.anchorMin = Vector2.zero;
            full.anchorMax = Vector2.one;
            full.offsetMin = full.offsetMax = Vector2.zero;
            var back = gameObject.AddComponent<Image>();                  // 화면 밖 여백까지 덮는다 (뒤 전투 화면 클릭 막기)
            back.color = Color.black;

            // 320×180 화면 (가운데)
            var go = new GameObject("Screen", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            root = (RectTransform)go.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(320f, 180f);

            Img(root, "summary_bg", 0, 0);
            tab = Img(root, "summary_tabs_1_ko", 81, 0);
            ClickArea(root, 81, 0, 19, 16, () => clickedPageDelta = -1);   // ≪
            nextArea = ClickArea(root, 100, 0, 19, 16, () => clickedPageDelta = 1);   // ≫ (탭 폭에 따라 Refresh에서 옮긴다)
            Text(root, 4, 0, 90, 16, FontHeader, TextAlignmentOptions.Left, true).Set("스테이터스");

            // 왼쪽: 도감 번호 / 팰 그림 / 이름·별 / 스피어·레벨 / 상태이상
            Img(root, "summary_dexnb_label_ko", 6, 21);
            number = Text(root, 11, 18, 31, 14, FontHeader, TextAlignmentOptions.Right, true);
            portrait = Box("Portrait", root, 9, 36, 88, 88).gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            nameText = Text(root, 6, 127, 92, 13, FontText, TextAlignmentOptions.Left, true);
            starRow = Box("Stars", root, 0, 130, 30, 7);
            sphereIcon = Box("Sphere", root, 6, 148, 13, 13).gameObject.AddComponent<Image>();
            sphereIcon.preserveAspect = true;
            sphereIcon.raycastTarget = false;
            levelText = Text(root, 22, 149, 70, 13, FontText, TextAlignmentOptions.Left, false);
            statusBar = Img(root, "summary_status", 0, 164);
            statusLabel = Text(statusBar.rectTransform, 3, 1, 60, 13, FontText, TextAlignmentOptions.Left, true);
            statusLabel.Set("상태");
            statusBadge = Box("Badge", statusBar.rectTransform, 81, 4, 20, 8).gameObject.AddComponent<RawImage>();
            statusBadge.raycastTarget = false;

            // 오른쪽 페이지
            pageBg = Img(root, "summary_profile", 106, 21);
            for (int i = 0; i < PageCount; i++) pages[i] = Box("Page" + i, root, 106, 21, 214, 159);
            BuildProfile(pages[0]);
            BuildStats(pages[1]);
            BuildMoves(pages[2]);

            // 기술 효과 칸 (③에서 왼쪽 아래에 나온다)
            effectPanel = Img(root, "summary_moves_effect", 6, 118).rectTransform;
            Img(effectPanel, "summary_moves_effect_title_ko", 7, 2);
            Text(effectPanel, 8, 13, 50, 14, FontText, TextAlignmentOptions.Left, true).Set("위력");
            Text(effectPanel, 8, 29, 50, 14, FontText, TextAlignmentOptions.Left, true).Set("명중");
            Text(effectPanel, 8, 45, 50, 14, FontText, TextAlignmentOptions.Left, true).Set("분류");
            powerValue = Text(effectPanel, 50, 13, 48, 14, FontText, TextAlignmentOptions.Right, false);
            accuracyValue = Text(effectPanel, 50, 29, 48, 14, FontText, TextAlignmentOptions.Right, false);
            categoryIcon = Box("Category", effectPanel, 70, 46, 28, 12).gameObject.AddComponent<RawImage>();
            categoryIcon.raycastTarget = false;

            BuildPopup();
        }

        void BuildProfile(RectTransform p)
        {
            Img(p, "summary_profile_profile_title_ko", 7, -1);
            trainerText = Text(p, 7, 9, 132, 14, FontText, TextAlignmentOptions.Left, false);
            idText = Text(p, 141, 9, 72, 14, FontText, TextAlignmentOptions.Left, false);
            typeLabel = Text(p, 7, 26, 30, 14, FontText, TextAlignmentOptions.Left, false);
            typeLabel.Set("타입/");
            for (int i = 0; i < 2; i++)
            {
                typeBadges[i] = Box("Type" + i, p, 0, 27, 24, 14).gameObject.AddComponent<RawImage>();
                typeBadges[i].raycastTarget = false;
                typeFallback[i] = Text(typeBadges[i].rectTransform, 0, 0, 24, 14, FontDesc, TextAlignmentOptions.Center, true);
            }
            starLabel = Text(p, 141, 26, 40, 14, FontText, TextAlignmentOptions.Left, false);
            profileStars = Box("Stars", p, 172, 30, 30, 7);

            Img(p, "summary_profile_prompt_a", 8, 43);
            abilityLabel = Text(p, 18, 40, 120, 13, FontText, TextAlignmentOptions.Left, true);
            abilityName = Text(p, 7, 54, 202, 14, FontText, TextAlignmentOptions.Left, false);
            abilityDesc = Text(p, 7, 69, 202, 31, FontDesc, TextAlignmentOptions.TopLeft, false, true);

            Img(p, "summary_profile_memo_title_ko", 7, 102);
            memoText = Text(p, 7, 112, 204, 46, FontText, TextAlignmentOptions.TopLeft, false, true);
        }

        void BuildStats(RectTransform p)
        {
            Img(p, "summary_stats_item_title_ko", 7, -1);
            itemRoot = Box("Items", p, 0, 0, 214, 48);
            noItemText = Text(p, 10, 16, 190, 14, FontDesc, TextAlignmentOptions.Left, false);
            noItemText.Set("지닌 물건이 없다.");
            Img(p, "summary_profile_prompt_a", 8, 55);
            Img(p, "summary_stats_stats_title_ko", 16, 54);
            for (int i = 0; i < 6; i++)
            {
                int row = i % 3, col = i / 3;
                float labelCenter = 27 + 116 * col + (col == 1 ? 5 : 0);
                statLabels[i] = Text(p, labelCenter - 30, 64 + 16 * row, 60, 14, FontText, TextAlignmentOptions.Center, true);
                statValues[i] = Text(p, 27 + 93 + 93 * col - 70, 64 + 16 * row, 70, 14, FontText, TextAlignmentOptions.Right, false);
            }
            Img(p, "summary_stats_exp_title_ko", 7, 110);
            Text(p, 6, 119, 110, 15, FontText, TextAlignmentOptions.Left, true).Set("현재 경험치");
            Text(p, 6, 135, 110, 15, FontText, TextAlignmentOptions.Left, true).Set("다음 레벨까지");
            expValue = Text(p, 113, 119, 100, 15, FontText, TextAlignmentOptions.Right, false);
            nextValue = Text(p, 113, 135, 100, 15, FontText, TextAlignmentOptions.Right, false);
            Img(p, "summary_stats_expbar_title_ko", 126, 152);
            expFill = Img(p, "summary_stats_overlay_exp", 140, 153);
        }

        void BuildMoves(RectTransform p)
        {
            Img(p, "summary_moves_moves_title_ko", 7, -1);
            moveCursor = Img(p, "summary_moves_cursor", 3, 11);
            moveCursor.uvRect = new Rect(0f, 0.5f, 1f, 0.5f);               // 위 칸 = 커서
            moveCursor.rectTransform.sizeDelta = new Vector2(211f, 16f);
            selectCursor = Img(p, "summary_moves_cursor", 3, 11);
            selectCursor.uvRect = new Rect(0f, 0f, 1f, 0.5f);                // 아래 칸 = 순서 바꿀 기술 표시
            selectCursor.rectTransform.sizeDelta = new Vector2(211f, 16f);
            for (int m = 0; m <= Pal.MaxSkills; m++)
            {
                float bottom = 26 + 16 * m;
                var row = new MoveRow { root = Box("Move" + m, p, 0, bottom - 16, 214, 16) };
                int rowIndex = m;
                ClickArea(row.root, 3, 1, 211, 16, () => clickedRow = rowIndex);
                row.badge = Box("Type", row.root, 7, 2, 24, 14).gameObject.AddComponent<RawImage>();
                row.badge.raycastTarget = false;
                row.badgeText = Text(row.badge.rectTransform, 0, 0, 24, 14, FontDesc, TextAlignmentOptions.Center, true);
                row.name = Text(row.root, 40, 2, 130, 14, FontText, TextAlignmentOptions.Left, true);
                row.ppLabel = Img(row.root, "summary_moves_overlay_pp_ko", 172, 7);
                row.pp = Text(row.root, 183, 2, 30, 14, FontText, TextAlignmentOptions.Right, true);
                moveRows.Add(row);
            }
            moveCursor.transform.SetAsLastSibling();
            Img(p, "summary_moves_descriptions_title_ko", 7, 90);
            moveDescription = Text(p, 6, 100, 205, 58, FontDesc, TextAlignmentOptions.TopLeft, false, true);
        }

        // ═════════════════ 보여주기 ═════════════════

        /// <summary>
        /// 능력치 화면을 열고 닫을 때까지 기다린다. party[index]부터 보여주고 ↑↓로 다른 팰을 볼 수 있다.
        /// </summary>
        public IEnumerator Show(IReadOnlyList<Pal> party, int index, Run run)
        {
            if (party == null || party.Count == 0) yield break;
            this.party = party;
            this.index = Mathf.Clamp(index, 0, party.Count - 1);
            trainerId = run != null ? run.TrainerId : 0;
            trainerName = GameConfig.PlayerName;
            page = Page.Profile;
            abilityPage = 0;
            showIvs = false;
            swapFrom = -1;
            moveCursorIndex = 0;

            var es = EventSystem.current;
            var previous = es != null ? es.currentSelectedGameObject : null;
            if (es != null) es.SetSelectedGameObject(null);               // 뒤 화면 버튼이 방향키를 먹지 않게

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RewardScreen.EnsureOwnCanvas(gameObject, SortingOrder, true);
            Refresh();

            yield return null;                                             // 이 화면을 연 엔터가 바로 먹히지 않게
            bool closed = false;
            while (!closed)
            {
                yield return null;

                int pageDelta = Keys.Left ? -1 : Keys.Right ? 1 : clickedPageDelta;
                clickedPageDelta = 0;
                if (pageDelta != 0 && swapFrom < 0)
                {
                    page = (Page)(((int)page + pageDelta + PageCount) % PageCount);
                    Refresh();
                    continue;
                }

                if (Keys.Cancel)
                {
                    if (swapFrom >= 0) { swapFrom = -1; Refresh(); continue; }
                    closed = true;
                    continue;
                }

                if (page == Page.Moves)
                {
                    int rows = Pal.Skills.Count + 1;                       // 기술 + 그만둔다
                    if (Keys.Up) { moveCursorIndex = (moveCursorIndex + rows - 1) % rows; Refresh(); continue; }
                    if (Keys.Down) { moveCursorIndex = (moveCursorIndex + 1) % rows; Refresh(); continue; }
                    bool confirm = Keys.Confirm;
                    if (clickedRow >= 0 && clickedRow < rows)
                    {
                        moveCursorIndex = clickedRow;
                        confirm = true;
                    }
                    clickedRow = -1;
                    if (!confirm) continue;

                    if (moveCursorIndex >= Pal.Skills.Count)                // 그만둔다
                    {
                        if (swapFrom >= 0) { swapFrom = -1; Refresh(); continue; }
                        closed = true;
                        continue;
                    }
                    if (swapFrom >= 0)
                    {
                        Pal.SwapSkills(swapFrom, moveCursorIndex);           // 고른 두 기술의 자리를 바꾼다
                        swapFrom = -1;
                        Refresh();
                        continue;
                    }
                    yield return AskSwap();
                    continue;
                }

                // ①② : ↑↓ = 다른 팰, 엔터 = 강화 칸 넘기기 / 개체값 보기
                if (party.Count > 1 && (Keys.Up || Keys.Down))
                {
                    this.index = (this.index + (Keys.Up ? party.Count - 1 : 1)) % party.Count;   // (매개변수 index가 아니라 필드)
                    Refresh();
                    continue;
                }
                if (Keys.Confirm)
                {
                    if (page == Page.Profile) abilityPage = (abilityPage + 1) % AbilityPageCount;
                    else showIvs = !showIvs;
                    Refresh();
                }
            }

            gameObject.SetActive(false);
            if (es != null && previous != null && previous.activeInHierarchy) es.SetSelectedGameObject(previous);
        }

        /// <summary>기술에서 엔터: "기술 순서 교체 / 그만둔다"</summary>
        IEnumerator AskSwap()
        {
            if (Pal.Skills.Count <= 1) yield break;
            popup.gameObject.SetActive(true);
            popupIndex = 0;
            popupClicked = -1;
            int chosen = -1;
            while (chosen < 0)
            {
                popupCursor.rectTransform.anchoredPosition = new Vector2(PopupCursorX, -(PopupRowTop + PopupRowStep * popupIndex + 1f));
                yield return null;
                if (Keys.Up || Keys.Down) popupIndex = 1 - popupIndex;
                else if (popupClicked >= 0) chosen = popupClicked;
                else if (Keys.Confirm) chosen = popupIndex;
                else if (Keys.Cancel) chosen = 1;
            }
            popup.gameObject.SetActive(false);
            if (chosen == 0) swapFrom = moveCursorIndex;
            Refresh();
            yield return null;                                             // 고른 엔터가 다음 입력으로 넘어가지 않게
        }

        // ───── 기술 순서 교체 선택 창 (전투의 예/아니오 창과 같은 모양, 화면 오른쪽 아래) ─────
        const float PopupWidth = 112f, PopupRowTop = 9f, PopupRowStep = 16f, PopupCursorX = 10f;
        RectTransform popup;
        Image popupCursor;
        int popupIndex, popupClicked = -1;

        void BuildPopup()
        {
            string[] labels = { "기술 순서 교체", "그만둔다" };
            float h = PopupRowTop + PopupRowStep * labels.Length + 7f;
            popup = Box("SwapMenu", root, 320f - PopupWidth - 1f, 180f - h - 1f, PopupWidth, h);
            var bg = popup.gameObject.AddComponent<Image>();
            var srcBg = choiceMenu != null ? choiceMenu.window.GetComponent<Image>() : null;
            if (srcBg != null) { bg.sprite = srcBg.sprite; bg.type = srcBg.type; bg.pixelsPerUnitMultiplier = srcBg.pixelsPerUnitMultiplier; }
            else bg.color = new Color(0.2f, 0.2f, 0.3f);
            float labelSize = choiceMenu != null && choiceMenu.labels.Length > 0 && choiceMenu.labels[0] != null ? choiceMenu.labels[0].fontSize : 11f;
            for (int i = 0; i < labels.Length; i++)
            {
                int row = i;
                ClickArea(popup, 8, PopupRowTop + PopupRowStep * i, PopupWidth - 16, 14, () => popupClicked = row);
                var t = MakeTmp(Box("Label", popup, 20, PopupRowTop + PopupRowStep * i, PopupWidth - 26, 14), labelSize, TextAlignmentOptions.Left, false);
                t.text = labels[i];
                t.color = Color.white;
            }
            popupCursor = Box("Cursor", popup, PopupCursorX, PopupRowTop + 1, 6, 10).gameObject.AddComponent<Image>();
            popupCursor.raycastTarget = false;
            if (choiceMenu != null && choiceMenu.cursor != null) popupCursor.sprite = choiceMenu.cursor.sprite;
            popup.gameObject.SetActive(false);
        }

        // ═════════════════ 화면 채우기 ═════════════════

        void Refresh()
        {
            var pal = Pal;
            tab.texture = Tex(page switch { Page.Profile => "summary_tabs_1_ko", Page.Stats => "summary_tabs_2_ko", _ => "summary_tabs_3_ko" });
            tab.SetNativeSize();
            nextArea.anchoredPosition = new Vector2(81f + tab.rectTransform.sizeDelta.x - 19f, 0f);
            pageBg.texture = Tex(page switch { Page.Profile => "summary_profile", Page.Stats => "summary_stats", _ => "summary_moves" });
            for (int i = 0; i < PageCount; i++) pages[i].gameObject.SetActive(i == (int)page);
            effectPanel.gameObject.SetActive(page == Page.Moves && moveCursorIndex < pal.Skills.Count);

            // 왼쪽 공통
            bool maxStars = pal.Stars >= GameConfig.MaxStars;
            number.Set(string.IsNullOrEmpty(pal.Data.palId) ? "???" : pal.Data.palId, maxStars ? GoldText : White, maxStars ? GoldShadow : WhiteShadow);
            portrait.sprite = pal.Data.frontSprite;
            portrait.enabled = portrait.sprite != null;
            portrait.color = pal.IsFainted ? new Color(0.45f, 0.45f, 0.45f) : Color.white;
            nameText.Set(PalName(pal));
            PlaceStars(starRow, pal.Stars, nameText.main.rectTransform.anchoredPosition.x + nameText.PreferredWidth + 2f, 130f);
            sphereIcon.sprite = PalSpheres.LoadSprite(pal.CaughtSphere);
            sphereIcon.enabled = sphereIcon.sprite != null;
            levelText.Set($"Lv.{pal.Level}");
            string badge = pal.IsFainted ? StatusRules.FaintedBadgeName : StatusRules.BadgeName(pal.Status);
            var badgeTex = badge != null ? Resources.Load<Texture2D>($"statuses/{badge}") : null;
            statusBar.gameObject.SetActive(badgeTex != null);
            statusBadge.texture = badgeTex;
            statusBadge.uvRect = StatusUv;

            switch (page)
            {
                case Page.Profile: RefreshProfile(pal); break;
                case Page.Stats: RefreshStats(pal); break;
                default: RefreshMoves(pal); break;
            }
        }

        void RefreshProfile(Pal pal)
        {
            trainerText.Set($"어버이/[b]{trainerName}[/b]");
            idText.Set($"IDNo.{trainerId:00000}");

            float x = 7 + typeLabel.PreferredWidth + 3;
            var types = new List<PalType> { pal.Data.primaryType };
            if (pal.Data.hasSecondaryType) types.Add(pal.Data.secondaryType);
            for (int i = 0; i < 2; i++)
            {
                bool on = i < types.Count;
                typeBadges[i].gameObject.SetActive(on);
                if (!on) continue;
                typeBadges[i].rectTransform.anchoredPosition = new Vector2(x + 28 * i, -27f);
                SetTypeBadge(typeBadges[i], typeFallback[i], types[i]);
            }

            starLabel.Set("응축/");
            PlaceStars(profileStars, pal.Stars, 141 + starLabel.PreferredWidth + 2f, 30f);
            if (pal.Stars == 0) starLabel.Set("응축/ 없음");

            var (label, name, desc) = AbilityInfo(pal, abilityPage);
            abilityLabel.Set($"{label}  [g]{abilityPage + 1}/{AbilityPageCount}[/g]");
            abilityName.Set(name);
            abilityDesc.Set(desc);
            memoText.Set(Memo(pal));
        }

        void RefreshStats(Pal pal)
        {
            // 지닌 물건: 같은 물건은 하나로 묶고 개수를 쓴다
            foreach (Transform c in itemRoot) Destroy(c.gameObject);
            var held = pal.Effects.Where(e => e.HeldItemId != null).GroupBy(e => e.HeldItemId).ToList();
            noItemText.main.gameObject.SetActive(held.Count == 0);
            noItemText.shadow.gameObject.SetActive(held.Count == 0);
            for (int i = 0; i < held.Count; i++)
            {
                const int perRow = 13;
                float ix = 4 + 16 * (i % perRow), iy = 9 + 17 * (i / perRow);
                var def = ItemCatalog.All.FirstOrDefault(d => d.Name == held[i].Key);
                var icon = Box("Item", itemRoot, ix, iy, 16, 16).gameObject.AddComponent<RawImage>();
                icon.raycastTarget = false;
                icon.texture = def != null ? Resources.Load<Texture2D>(def.IconPath) : null;
                icon.enabled = icon.texture != null;
                // 마우스를 올리면 아이템 설명 (아이템 줄과 같은 말풍선)
                string itemName = held[i].Key, itemDesc = def?.Description ?? held[i].First().Description;
                var tipColor = def != null ? ItemTiers.Color(def.Tier) : Color.white;
                var hover = Box("Hover", itemRoot, ix, iy, 16, 16).gameObject;
                ItemTooltip.Attach(hover, font, () => (itemName, tipColor, itemDesc));
                var count = Text(itemRoot, ix - 1, iy + 8, 10, 9, FontTiny, TextAlignmentOptions.BottomLeft, true);
                count.Set(held[i].Count().ToString());
            }

            // 능력치 (왼쪽: HP·공격·방어 / 오른쪽: 스피드 + 응축·강화) — 엔터로 개체값
            var perks = Perks(pal).Count;
            if (!showIvs)
            {
                StatRow(0, "HP", $"{pal.CurrentHP}/{pal.MaxHP}", Compare(pal.MaxHP, pal.BaseMaxHP));
                StatRow(1, "공격", pal.GetStat(StatType.Attack).ToString(), Compare(pal.GetStat(StatType.Attack), pal.RawStat(StatType.Attack)));
                StatRow(2, "방어", pal.GetStat(StatType.Defense).ToString(), Compare(pal.GetStat(StatType.Defense), pal.RawStat(StatType.Defense)));
                StatRow(3, "스피드", pal.GetStat(StatType.Speed).ToString(), Compare(pal.GetStat(StatType.Speed), pal.RawStat(StatType.Speed)));
                StatRow(4, "응축", $"{pal.Stars}/{GameConfig.MaxStars}", pal.Stars > 0 ? 1 : 0);
                StatRow(5, "강화", $"{perks}개", perks > 0 ? 1 : 0);
            }
            else
            {
                IvRow(0, "HP", pal.HpIV);
                IvRow(1, "공격", pal.IV(StatType.Attack));
                IvRow(2, "방어", pal.IV(StatType.Defense));
                IvRow(3, "스피드", pal.IV(StatType.Speed));
                int sum = pal.IVs.Sum(), max = Pal.MaxIV * pal.IVs.Length;
                SetStat(4, "합계", $"{sum}/{max}", sum == max);
                SetStat(5, "평가", IvGrade(sum, max), sum >= max * 0.9f);
            }

            expValue.Set(pal.TotalExp.ToString());
            nextValue.Set(pal.IsMaxLevel ? "-" : (pal.ExpToNext - pal.Exp).ToString());
            float ratio = pal.ExpRatio;
            expFill.rectTransform.sizeDelta = new Vector2(Mathf.Round(64f * ratio), 3f);
            expFill.uvRect = new Rect(0f, 0f, ratio, 1f);
            expFill.enabled = ratio > 0f;
        }

        void StatRow(int i, string label, string value, int boosted)
        {
            var (c, s) = boosted > 0 ? (PinkText, PinkShadow) : boosted < 0 ? (BlueText, BlueShadow) : (White, WhiteShadow);
            statLabels[i].Set(label, c, s);
            statValues[i].Set(value);
        }

        void IvRow(int i, string label, int iv) => SetStat(i, label, $"{iv}/{Pal.MaxIV}", iv >= Pal.MaxIV);

        void SetStat(int i, string label, string value, bool gold)
        {
            statLabels[i].Set(label, gold ? GoldText : White, gold ? GoldShadow : WhiteShadow);
            statValues[i].Set(value);
        }

        static int Compare(int now, int baseValue) => now > baseValue ? 1 : now < baseValue ? -1 : 0;

        static string IvGrade(int sum, int max)
        {
            float r = (float)sum / max;
            return r >= 0.95f ? "최고" : r >= 0.75f ? "아주 좋음" : r >= 0.5f ? "좋음" : r >= 0.3f ? "보통" : "아쉬움";
        }

        void RefreshMoves(Pal pal)
        {
            for (int m = 0; m < moveRows.Count; m++)
            {
                var row = moveRows[m];
                bool isSkill = m < pal.Skills.Count, isCancel = m == pal.Skills.Count;
                row.root.gameObject.SetActive(isSkill || isCancel);
                if (!isSkill && !isCancel) continue;
                row.badge.gameObject.SetActive(isSkill);
                row.ppLabel.gameObject.SetActive(isSkill);
                row.pp.main.gameObject.SetActive(isSkill);
                row.pp.shadow.gameObject.SetActive(isSkill);
                if (isCancel) { row.name.Set("그만둔다"); continue; }

                var s = pal.Skills[m];
                SetTypeBadge(row.badge, row.badgeText, s.Data.type);
                row.name.Set(s.Data.skillName);
                float left = s.MaxPP > 0 ? (float)s.PP / s.MaxPP : 0f;
                var ppColor = s.PP <= 0 ? PpEmpty : left <= 0.25f ? PpLow : left <= 0.5f ? PpHalf : White;
                row.pp.Set($"{s.PP}/{s.MaxPP}", ppColor, PpShadow);
            }

            moveCursorIndex = Mathf.Clamp(moveCursorIndex, 0, pal.Skills.Count);
            moveCursor.rectTransform.anchoredPosition = new Vector2(3f, -(11f + 16f * moveCursorIndex));
            selectCursor.gameObject.SetActive(swapFrom >= 0);
            if (swapFrom >= 0) selectCursor.rectTransform.anchoredPosition = new Vector2(3f, -(11f + 16f * swapFrom));

            if (moveCursorIndex < pal.Skills.Count)
            {
                var d = pal.Skills[moveCursorIndex].Data;
                bool attack = d.category != SkillCategory.Status;
                powerValue.Set(d.power > 0 ? d.power.ToString() : "---");
                accuracyValue.Set(d.accuracy > 0 ? d.accuracy.ToString() : "---");
                categoryIcon.texture = Resources.Load<Texture2D>(attack ? "Attack Icon" : "Status Icon");
                categoryIcon.uvRect = CategoryUv;
                categoryIcon.enabled = categoryIcon.texture != null;
                string extra = d.statusEffect != StatusCondition.None && d.statusChance > 0
                    ? $"\n{d.statusChance}% 확률로 상대를 {StatusRules.Korean(d.statusEffect)} 상태로 만든다."
                    : "";
                string swapHint = swapFrom >= 0 ? $"[r]{pal.Skills[swapFrom].Data.skillName}[/r]과(와) 자리를 바꿀 기술을 골라 주세요.\n" : "";
                string desc = !string.IsNullOrEmpty(d.description) ? d.description
                            : attack ? $"{PalTypeNames.Korean(d.type)} 속성의 공격 기술." : $"{PalTypeNames.Korean(d.type)} 속성의 변화 기술.";
                moveDescription.Set(swapHint + desc + extra);
            }
            else moveDescription.Set(swapFrom >= 0 ? "자리 바꾸기를 그만둔다." : "능력치 화면을 닫는다.");
        }

        // ═════════════════ ① 강화 칸 내용 ═════════════════

        const int AbilityPageCount = 5;

        /// <summary>엔터로 넘기는 강화 칸: 강화 효과 → 패시브 → 각성 → 시너지 → 사용한 아이템</summary>
        (string label, string name, string desc) AbilityInfo(Pal pal, int which)
        {
            switch (which)
            {
                case 0:
                {
                    var perks = Perks(pal);
                    if (perks.Count == 0) return ("강화 효과", "아직 없음", $"{GameConfig.PerkLevelStep}레벨마다 강화 효과를 하나씩 고를 수 있다.");
                    return ("강화 효과", perks.Count == 1 ? perks[0].PerkName : $"강화 효과 {perks.Count}개", Describe(perks));
                }
                case 1:
                {
                    var passives = Passives(pal);
                    if (passives.Count == 0) return ("패시브", "아직 없음", "임플란트 아이템으로 팰월드 패시브 스킬을 심을 수 있다.");
                    return ("패시브", string.Join(", ", passives.Select(e => e.PerkName)), Describe(passives));
                }
                case 2:
                {
                    var a = AwakeningCatalog.Of(pal);
                    if (a == null)
                        return ("각성", "아직 깨어나지 않음", $"Lv.{GameConfig.AwakenLevel}이 되면 각성을 하나 고를 수 있다.\n전투 중 HP가 절반 이하면 깨어난다.");
                    float m = a.IsTrue ? GameConfig.TrueAwakenStatMultiplier : GameConfig.AwakenStatMultiplier;
                    string title = a.IsTrue ? $"{a.Def.Name} [g](진각성)[/g]" : a.Def.Name;
                    return ("각성", title, $"HP가 절반 이하면 깨어나 모든 능력치 ×{m:0.##}\n{a.Def.Description}");
                }
                case 3:
                {
                    var syn = pal.Effects.OfType<SynergyEffect>().ToList();
                    if (syn.Count == 0) return ("속성 시너지", "없음", "파티에 같은 속성 팰이 2마리 이상이면\n그 속성 팰들에게 시너지가 켜진다.");
                    return ("속성 시너지", string.Join(", ", syn.Select(s => s.PerkName)), string.Join("\n", syn.Select(s => s.PerkDescription)));
                }
                default:
                {
                    var used = UsedItems(pal);
                    if (used.Count == 0) return ("사용한 아이템", "없음", "팰 영혼·로터스·열매 같은 아이템을 쓰면\n여기에 기록된다.");
                    return ("사용한 아이템", used.Count == 1 ? used[0].PerkName : $"아이템 {used.Count}개", Describe(used));
                }
            }
        }

        /// <summary>하나면 그 설명, 여러 개면 이름을 이어 붙인다</summary>
        static string Describe(List<BattleEffect> list) =>
            list.Count == 1 ? list[0].PerkDescription ?? "" : string.Join(" · ", list.Select(e => e.PerkName));

        static readonly HashSet<string> PassiveNames = new HashSet<string>(
            ItemCatalog.All.Where(d => d.Id.StartsWith("implant_")).Select(d => d.Name.Replace("임플란트: ", "")));

        /// <summary>레벨업으로 고른 강화 효과 (각성 제외)</summary>
        static List<BattleEffect> Perks(Pal pal) =>
            pal.Effects.Where(e => e.PerkId != null && !(e is Awakening) && e.HeldItemId == null).ToList();

        /// <summary>임플란트로 심은 팰월드 패시브 스킬</summary>
        static List<BattleEffect> Passives(Pal pal) =>
            pal.Effects.Where(e => e.PerkId == null && e.HeldItemId == null && e.PerkName != null && PassiveNames.Contains(e.PerkName)).ToList();

        /// <summary>그 밖에 영구 효과를 남긴 아이템 (팰 영혼, 비타민, 로터스, 열매 …)</summary>
        static List<BattleEffect> UsedItems(Pal pal) =>
            pal.Effects.Where(e => e.PerkId == null && e.HeldItemId == null && e.PerkName != null && !PassiveNames.Contains(e.PerkName)
                                   && !(e is SynergyEffect) && !(e is BossAura) && !(e is ITimedEffect)).ToList();

        string Memo(Pal pal)
        {
            string first = pal.MetWave <= 0
                ? "[r]모험의 시작[/r]부터 함께한\n든든한 팰이다."
                : $"[r]{pal.MetArea}[/r]의 [r]웨이브 {pal.MetWave}[/r]에서\n[r]레벨 {pal.MetLevel}[/r]일 때 만났다.";
            string second = pal.MetAsBoss ? "\n[r]보스[/r]로 만난 강한 팰이다."
                          : pal.Stars > 0 ? $"\n[r]{pal.Stars}번[/r] 응축된 팰이다."
                          : "";
            return first + second;
        }

        // ═════════════════ 도우미 ═════════════════

        static string PalName(Pal pal) => string.IsNullOrEmpty(pal.Data.palName) ? pal.Data.name : pal.Data.palName;

        static void SetTypeBadge(RawImage badge, ShadowText fallback, PalType type)
        {
            var tex = Resources.Load<Texture2D>($"1.팰/2.Elements/Skill_type/{type}_sk");
            badge.texture = tex;
            badge.uvRect = BadgeUv;
            badge.color = tex != null ? Color.white : new Color(0.35f, 0.25f, 0.5f);   // 그림이 없는 속성(공허)은 색 칸 + 글자
            fallback.Set(tex != null ? "" : PalTypeNames.Korean(type));
        }

        static Texture2D star;

        static void PlaceStars(RectTransform row, int stars, float x, float y)
        {
            row.anchoredPosition = new Vector2(Mathf.Round(x), -y);
            if (star == null) star = Resources.Load<Texture2D>("UI/star");
            for (int i = 0; i < Mathf.Max(stars, row.childCount); i++)
            {
                RawImage img;
                if (i < row.childCount) img = row.GetChild(i).GetComponent<RawImage>();
                else
                {
                    img = Box("Star", row, 7 * i, 0, 7, 7).gameObject.AddComponent<RawImage>();
                    img.raycastTarget = false;
                }
                img.texture = star;
                img.gameObject.SetActive(i < stars && star != null);
            }
        }

        static Texture2D Tex(string name)
        {
            var t = Resources.Load<Texture2D>(Dir + name);
            if (t == null) Debug.LogWarning($"[SummaryScreen] Resources/{Dir}{name}.png 가 없습니다.");
            else t.filterMode = FilterMode.Point;
            return t;
        }

        /// <summary>부모의 왼쪽 위 기준 (x, y)에 그림을 원래 크기로</summary>
        static RawImage Img(RectTransform parent, string name, float x, float y)
        {
            var tex = Tex(name);
            var img = Box(name, parent, x, y, tex != null ? tex.width : 8, tex != null ? tex.height : 8).gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        static RectTransform Box(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static RectTransform ClickArea(RectTransform parent, float x, float y, float w, float h, Action onClick)
        {
            var rt = Box("Click", parent, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() =>
            {
                onClick();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            return rt;
        }

        ShadowText Text(RectTransform parent, float x, float y, float w, float h, float size, TextAlignmentOptions align, bool light, bool wrap = false)
        {
            var st = new ShadowText { light = light };
            st.shadow = MakeTmp(Box("Shadow", parent, x + 0.6f, y + 0.8f, w, h), size, align, wrap);
            st.main = MakeTmp(Box("Text", parent, x, y, w, h), size, align, wrap);
            st.Set("");
            return st;
        }

        TMP_Text MakeTmp(RectTransform rt, float size, TextAlignmentOptions align, bool wrap)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.lineSpacing = wrap ? -8f : 0f;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        /// <summary>그림자가 있는 글자 (포켓로그 글자처럼 오른쪽 아래에 그림자). light=흰 글자, 아니면 짙은 회색 글자</summary>
        class ShadowText
        {
            public TMP_Text main, shadow;
            public bool light;

            public float PreferredWidth => main.GetPreferredValues(main.text).x;

            public void Set(string text) => Set(text, light ? White : Dark, light ? WhiteShadow : DarkShadow);

            public void Set(string text, Color color, Color shadowColor)
            {
                text ??= "";
                string m = text, s = text;
                foreach (var (tag, mainHex, shadowHex) in Marks)
                {
                    m = m.Replace($"[{tag}]", $"<color=#{mainHex}>").Replace($"[/{tag}]", "</color>");
                    s = s.Replace($"[{tag}]", $"<color=#{shadowHex}>").Replace($"[/{tag}]", "</color>");
                }
                main.text = m;
                main.color = color;
                shadow.text = s;
                shadow.color = shadowColor;
            }
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        /// <summary>키 입력 (새 입력 시스템 / 예전 입력 둘 다)</summary>
        static class Keys
        {
#if ENABLE_INPUT_SYSTEM
            static UnityEngine.InputSystem.Keyboard Kb => UnityEngine.InputSystem.Keyboard.current;
            static bool P(UnityEngine.InputSystem.Controls.KeyControl k) => k != null && k.wasPressedThisFrame;
            public static bool Left => Kb != null && P(Kb.leftArrowKey);
            public static bool Right => Kb != null && P(Kb.rightArrowKey);
            public static bool Up => Kb != null && P(Kb.upArrowKey);
            public static bool Down => Kb != null && P(Kb.downArrowKey);
            public static bool Confirm => Kb != null && (P(Kb.enterKey) || P(Kb.numpadEnterKey) || P(Kb.spaceKey) || P(Kb.zKey));
            public static bool Cancel =>
                (Kb != null && (P(Kb.escapeKey) || P(Kb.xKey) || P(Kb.backspaceKey))) ||
                (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame);
#else
            public static bool Left => Input.GetKeyDown(KeyCode.LeftArrow);
            public static bool Right => Input.GetKeyDown(KeyCode.RightArrow);
            public static bool Up => Input.GetKeyDown(KeyCode.UpArrow);
            public static bool Down => Input.GetKeyDown(KeyCode.DownArrow);
            public static bool Confirm => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                                          Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z);
            public static bool Cancel => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X) ||
                                         Input.GetKeyDown(KeyCode.Backspace) || Input.GetMouseButtonDown(1);
#endif
        }
    }
}
