using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 포켓로그식 파티 화면. 왼쪽 큰 칸 = 파티 1번, 오른쪽 5칸 = 2~6번.
    /// 사용법:  yield return partyScreen.Choose(파티, 취소가능?, "안내 문구", 검사함수);
    ///          int i = partyScreen.Result;   // -1이면 취소
    /// 검사함수는 고를 수 없는 칸이면 이유 문장을, 괜찮으면 null을 돌려준다.
    /// 입력: 마우스 클릭, 방향키 + Enter/Space, Esc(취소). 참조 칸들은 메뉴 PalRogue/14 가 연결한다.
    /// </summary>
    public class PartyScreen : MonoBehaviour
    {
        public PartySlotView[] slots;      // 6개
        public Button[] slotButtons;       // 6개 (각 칸)
        public Button cancelButton;
        public RawImage cancelFrame;       // party_cancel 시트 (위: 기본, 아래: 선택)
        public TMP_Text messageText;

        [Header("칸을 골랐을 때 뜨는 메뉴 창")]
        public GameObject optionsBox;
        public Button[] optionButtons;     // OptionLabels와 같은 순서
        public Image optionCursor;

        /// <summary>메뉴 창 항목 (순서 = 번호). 메뉴 14가 이 글자로 창을 만든다</summary>
        public static readonly string[] OptionLabels = { "교체한다", "능력치를 본다", "그만둔다" };
        public const int OptionSwitch = 0, OptionSummary = 1;
        int OptionCancel => optionButtons != null && optionButtons.Length > 0 ? optionButtons.Length - 1 : 2;   // 마지막 줄 = 그만둔다

        /// <summary>"능력치를 본다"를 고르면 부른다 (파티, 칸 번호). BattleScreen이 능력치 화면을 연결한다</summary>
        public Func<IReadOnlyList<Pal>, int, IEnumerator> OpenSummary;

        const float ErrorHoldSeconds = 1.1f;
        const int SortingOrder = 80;               // RewardScreen(50), 웨이브·돈 글자(60)보다 위
        const string NotReadyText = "아직 준비 중인 기능이에요!";

        /// <summary>마지막으로 고른 파티 번호. 취소했으면 -1, 모드 버튼(건넨다/버리기)을 눌렀으면 ModeToggled</summary>
        public int Result { get; private set; } = -1;
        public const int ModeToggled = -2;
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>true면 파티 화면이 보이기만 하고 입력은 받지 않는다 (위에 다른 선택 창이 떠 있을 때)</summary>
        public bool Suspended { get; set; }

        /// <summary>파티 화면 아래 메시지창 글자를 바꾼다</summary>
        public void SetMessage(string text) { if (messageText != null) messageText.text = text; }

        // 왼쪽 큰 칸 아래 모드 버튼 (지닌 도구 관리: 건넨다 ↔ 버리기). 그만둔다 버튼을 복사해서 만든다
        Button modeButton;
        RawImage modeFrame;
        TMP_Text modeText;
        string modeHint, currentPrompt;
        static readonly Vector2 ModeButtonPos = new Vector2(34f, 86f);   // 화면 왼쪽 위 기준 (큰 칸 아래 가운데)

        void EnsureModeButton()
        {
            if (modeButton != null || cancelButton == null) return;
            var go = Instantiate(cancelButton.gameObject, cancelButton.transform.parent, false);
            go.name = "ModeButton";
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(ModeButtonPos.x, -ModeButtonPos.y);
            modeButton = go.GetComponent<Button>();
            modeButton.onClick.RemoveAllListeners();
            modeButton.onClick.AddListener(() => { if (!showingError && !inOptions && !Suspended && modeLabelOn) clicked = -3; });
            modeButton.navigation = new Navigation { mode = Navigation.Mode.None };
            modeFrame = cancelFrame == null ? null : FindSame(cancelButton.transform, cancelFrame.transform, go.transform)?.GetComponent<RawImage>();
            modeText = go.GetComponentInChildren<TMP_Text>(true);
            var entry = go.GetComponent<MenuEntry>();
            if (entry != null)
            {
                entry.Selected += _ => OnHighlight(-2);
                entry.Cancelled += OnCancel;
            }
        }
        bool modeLabelOn;

        /// <summary>원본(from) 안에서 target이 있는 자리와 같은 자리의 오브젝트를 복사본(to)에서 찾는다</summary>
        static Transform FindSame(Transform from, Transform target, Transform to)
        {
            var path = new List<int>();
            for (var t = target; t != null && t != from; t = t.parent) path.Insert(0, t.GetSiblingIndex());
            var cur = to;
            foreach (int i in path) { if (i >= cur.childCount) return null; cur = cur.GetChild(i); }
            return cur;
        }

        IReadOnlyList<Pal> party;
        int selected = -1;
        int clicked = -2;                  // -2: 아직 안 고름, -1: 취소, 0~5: 칸 번호
        bool canCancel;
        bool showingError;
        bool inOptions;
        int optionClicked = -2;            // -2: 아직 안 고름
        GameObject lastSelected;
        bool bound;

        void Bind()
        {
            if (bound) return;
            bound = true;
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int index = i;
                slotButtons[i].onClick.AddListener(() => OnClick(index));
                var entry = slotButtons[i].GetComponent<MenuEntry>();
                if (entry != null)
                {
                    entry.Selected += _ => OnHighlight(index);
                    entry.Cancelled += OnCancel;
                }
            }
            RemoveRetiredOptions();
            for (int i = 0; optionButtons != null && i < optionButtons.Length; i++)
            {
                int index = i;
                optionButtons[i].onClick.AddListener(() => { if (inOptions && !showingError) optionClicked = index; });
                var entry = optionButtons[i].GetComponent<MenuEntry>();
                if (entry != null)
                {
                    entry.Selected += e => OnOptionHighlight(index, e);
                    entry.Cancelled += OnCancel;
                }
            }
            // 방향키 이동은 아래 Update에서 직접 한다 (유니티 자동 이동은 큰 칸·작은 칸 배치에서 가끔 멈추거나 엉뚱한 칸으로 간다)
            foreach (var b in slotButtons) b.navigation = new Navigation { mode = Navigation.Mode.None };
            cancelButton.navigation = new Navigation { mode = Navigation.Mode.None };
            cancelButton.onClick.AddListener(OnCancel);
            var cancelEntry = cancelButton.GetComponent<MenuEntry>();
            if (cancelEntry != null)
            {
                cancelEntry.Selected += _ => OnHighlight(-1);
                cancelEntry.Cancelled += OnCancel;
            }
        }

        /// <summary>
        /// 없앤 메뉴 항목(닉네임 바꾸기, 아직 없는 팰 도감)이 씬에 남아 있으면 지우고, 아래 항목을 한 줄씩 올리고 창을 줄인다
        /// (씬을 다시 만들지 않아도 되게). optionButtons 순서 = OptionLabels 순서가 된다.
        /// </summary>
        void RemoveRetiredOptions()
        {
            if (optionButtons == null) return;
            var keep = new List<Button>();
            float step = 0f;
            int removed = 0;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                var b = optionButtons[i];
                var label = b.GetComponentInChildren<TMP_Text>(true);
                if (i > 0 && step == 0f)
                    step = ((RectTransform)optionButtons[i - 1].transform).anchoredPosition.y - ((RectTransform)b.transform).anchoredPosition.y;
                if (label != null && (label.text.Contains("닉네임") || label.text.Contains("도감")))
                {
                    b.gameObject.SetActive(false);
                    removed++;
                    continue;
                }
                if (removed > 0) ((RectTransform)b.transform).anchoredPosition += new Vector2(0f, step * removed);
                keep.Add(b);
            }
            if (removed == 0) return;
            optionButtons = keep.ToArray();
            for (int i = 0; i < optionButtons.Length; i++)                 // 위아래 이동도 남은 항목끼리 (끝에서 처음으로 돈다)
                optionButtons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = optionButtons[(i + optionButtons.Length - 1) % optionButtons.Length],
                    selectOnDown = optionButtons[(i + 1) % optionButtons.Length],
                };
            if (optionsBox != null)
            {
                var box = (RectTransform)optionsBox.transform;
                box.sizeDelta -= new Vector2(0f, step * removed);
                foreach (RectTransform child in box)                       // 창 안쪽 칸(테두리 안)도 같이 줄인다
                    if (child.GetComponent<Button>() == null && child.sizeDelta.y > step * 2) child.sizeDelta -= new Vector2(0f, step * removed);
            }
        }

        /// <summary>파티 화면을 열고 한 칸을 고를 때까지 기다린다. 결과는 Result.</summary>
        /// <param name="actionLabel">메뉴 창 첫 항목 글자. 비우면 "교체한다" (놓아줄 때는 "놓아준다")</param>
        /// <param name="direct">true면 칸을 고르면 메뉴 창 없이 바로 그 칸이 결과가 된다</param>
        /// <param name="keepOpen">true면 고른 뒤에도 파티 화면을 닫지 않는다 (이어서 다른 창을 띄울 때)</param>
        /// <param name="modeLabel">왼쪽 큰 칸 아래 모드 버튼 글자 (null이면 버튼 없음). 누르면 Result = ModeToggled</param>
        /// <param name="modeHint">모드 버튼에 커서를 올렸을 때 메시지창 설명</param>
        public IEnumerator Choose(IReadOnlyList<Pal> party, bool canCancel, string prompt, Func<int, string> validate,
                                  string actionLabel = null, bool direct = false, bool keepOpen = false,
                                  string modeLabel = null, string modeHint = null, int startSlot = -1)
        {
            Bind();
            Suspended = false;
            currentPrompt = prompt;
            this.modeHint = modeHint;
            modeLabelOn = modeLabel != null;
            if (modeLabelOn) EnsureModeButton();
            if (modeButton != null)
            {
                modeButton.gameObject.SetActive(modeLabelOn);
                modeButton.interactable = true;
                if (modeText != null && modeLabelOn) modeText.text = modeLabel;
            }
            SetActionLabel(string.IsNullOrEmpty(actionLabel) ? OptionLabels[OptionSwitch] : actionLabel);
            this.party = party;
            this.canCancel = canCancel;
            transform.SetAsLastSibling();          // 다른 전투 UI보다 항상 위에 그려지게
            // 자기 캔버스로 맨 위에 그린다 (보상 화면·웨이브/돈 글자보다도 위 → 파티 화면 위에 다른 글자가 안 비친다)
            gameObject.SetActive(true);
            RewardScreen.EnsureOwnCanvas(gameObject, SortingOrder, true);   // 켠 뒤에 맞춘다 (꺼진 채로 정하면 풀릴 수 있음)
            cancelButton.gameObject.SetActive(canCancel);
            if (optionsBox != null) optionsBox.SetActive(false);
            inOptions = false;
            Result = -1;

            // 처음 커서: 고를 수 있는 첫 칸 (쓰러져서 교체할 때 쓰러진 팰에 커서가 가 있지 않게)
            int start = 0;
            if (validate != null)
                for (int i = 0; i < party.Count && i < slotButtons.Length; i++)
                    if (string.IsNullOrEmpty(validate(i))) { start = i; break; }
            if (startSlot >= 0 && startSlot < party.Count) start = startSlot;
            else if (startSlot == ModeToggled && modeLabelOn) start = -2;
            lastRight = start >= 1 ? start : 1;
            Select(start == -2 ? modeButton : slotButtons[start]);
            while (true)
            {
                messageText.text = prompt;
                clicked = -2;
                Refresh();
                while (clicked == -2) yield return null;

                if (clicked == -1) { Result = -1; break; }
                if (clicked == -3) { Result = ModeToggled; break; }
                int slot = clicked;

                // 칸을 고르면 바로 교체하지 않고 메뉴 창을 띄운다
                if (optionsBox != null && !direct)
                {
                    yield return ChooseOption(slot);
                    if (optionClicked != OptionSwitch) { Select(slotButtons[slot]); continue; }   // 그만둔다 → 다시 칸 고르기
                }

                string error = validate?.Invoke(slot);
                if (string.IsNullOrEmpty(error)) { Result = slot; break; }

                // 고를 수 없는 칸: 이유를 잠깐 보여주고 다시 고르게 한다
                showingError = true;
                messageText.text = error;
                yield return new WaitForSecondsRealtime(ErrorHoldSeconds);
                showingError = false;
                Select(slotButtons[slot]);
            }

            if (!keepOpen) Close();
            else Suspended = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>keepOpen으로 열어 둔 파티 화면을 닫는다</summary>
        public void Close()
        {
            Suspended = false;
            if (modeButton != null) modeButton.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        int lastRight = 1;   // 오른쪽 줄에서 마지막으로 있던 칸 (왼쪽 큰 칸에서 → 를 누르면 돌아간다)

        void Update()
        {
            MoveCursor();
            // 빈 곳을 클릭해서 선택이 사라져도 키보드 조작이 끊기지 않게
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null && lastSelected != null && lastSelected.activeInHierarchy)
                es.SetSelectedGameObject(lastSelected);
        }

        /// <summary>
        /// 방향키: 왼쪽 큰 칸(0) ↔ 오른쪽 줄(1~5), 오른쪽 줄 안에서 위아래, 맨 아래에서 ↓ = 그만둔다 버튼
        /// </summary>
        void MoveCursor()
        {
            if (party == null || inOptions || showingError || Suspended || !gameObject.activeInHierarchy) return;
            int h = MenuKeys.Horizontal, v = MenuKeys.Vertical;
            if (h == 0 && v == 0) return;
            int count = Mathf.Min(party.Count, slotButtons.Length);
            int cur = selected, next = cur;
            if (h > 0 && (cur == 0 || cur == -2) && count > 1) next = Mathf.Clamp(lastRight, 1, count - 1);
            else if (h < 0 && cur != 0 && cur != -2) next = 0;
            else if (v != 0 && cur == -2) next = v < 0 ? 0 : (canCancel ? -1 : -2);   // 모드 버튼: 위 = 큰 칸, 아래 = 그만둔다
            else if (v != 0 && cur == -1 && v < 0 && modeLabelOn && count <= 1) next = -2;
            else if (v != 0)
            {
                if (cur == 0) { if (v > 0) next = modeLabelOn ? -2 : canCancel ? -1 : 0; }
                else if (cur >= 1)
                {
                    next = cur + v;
                    if (next < 1) next = 1;
                    if (next >= count) next = canCancel ? -1 : count - 1;
                }
                else if (v < 0) next = count > 1 ? count - 1 : 0;      // 그만둔다 → 위
            }
            if (next == cur) return;
            if (next >= 1) lastRight = next;
            Select(next == -2 ? modeButton : next < 0 ? cancelButton : slotButtons[next]);
        }

        void OnClick(int index)
        {
            if (showingError || Suspended || party == null || index >= party.Count) return;
            clicked = index;
        }

        void OnCancel()
        {
            if (showingError || Suspended) return;
            if (inOptions) { optionClicked = OptionCancel; return; }   // 메뉴 창만 닫는다
            if (!canCancel) return;
            clicked = -1;
        }

        /// <summary>메뉴 창을 띄우고 항목을 고를 때까지 기다린다. 결과는 optionClicked (교체한다/그만둔다)</summary>
        IEnumerator ChooseOption(int slot)
        {
            inOptions = true;
            SetSlotsInteractable(false);
            optionsBox.transform.SetAsLastSibling();
            optionsBox.SetActive(true);
            string name = party[slot].Data.palName;
            string ask = $"{Josa.Eul(name)} 어떻게 할까?";
            Select(optionButtons[0]);

            while (true)
            {
                messageText.text = ask;
                optionClicked = -2;
                while (optionClicked == -2) yield return null;
                if (optionClicked == OptionSwitch || optionClicked == OptionCancel) break;

                if (optionClicked == OptionSummary && OpenSummary != null)
                {
                    // 능력치 화면을 보는 동안 파티 화면은 잠깐 숨긴다 (뒤에서 방향키를 먹지 않게)
                    optionsBox.SetActive(false);
                    gameObject.SetActive(false);
                    yield return OpenSummary(party, slot);
                    gameObject.SetActive(true);
                    optionsBox.SetActive(true);
                    Refresh();
                    Select(optionButtons[OptionSummary]);
                    continue;
                }

                // 아직 만들지 않은 기능
                showingError = true;
                messageText.text = NotReadyText;
                yield return new WaitForSecondsRealtime(ErrorHoldSeconds);
                showingError = false;
            }

            optionsBox.SetActive(false);
            SetSlotsInteractable(true);
            inOptions = false;
        }

        void SetActionLabel(string text)
        {
            if (optionButtons == null || optionButtons.Length == 0) return;
            var label = optionButtons[OptionSwitch].GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
        }

        void SetSlotsInteractable(bool on)
        {
            foreach (var b in slotButtons) b.interactable = on;
            cancelButton.interactable = on;
        }

        void OnOptionHighlight(int index, MenuEntry entry)
        {
            lastSelected = entry.gameObject;
            if (optionCursor == null) return;
            var label = (RectTransform)entry.transform;
            optionCursor.rectTransform.anchoredPosition = label.anchoredPosition + new Vector2(-10f, -1f);
        }

        void OnHighlight(int index)
        {
            if (inOptions || Suspended) return;
            selected = index;
            lastSelected = index >= 0 ? slotButtons[index].gameObject : index == -2 && modeButton != null ? modeButton.gameObject : cancelButton.gameObject;
            // 모드 버튼에 커서가 있으면 그 설명, 아니면 원래 안내 문구
            if (modeLabelOn && !showingError && messageText != null)
                messageText.text = index == -2 && modeHint != null ? modeHint : currentPrompt;
            Refresh();
        }

        void Refresh()
        {
            if (party == null) return;
            for (int i = 0; i < slots.Length; i++)
                slots[i].Set(i < party.Count ? party[i] : null, i == selected);

            if (cancelFrame != null)
                cancelFrame.uvRect = new Rect(0f, selected == -1 ? 0f : 0.5f, 1f, 0.5f);   // 위 칸=기본, 아래 칸=선택
            if (modeFrame != null && modeLabelOn)
                modeFrame.uvRect = new Rect(0f, selected == -2 ? 0f : 0.5f, 1f, 0.5f);
        }

        void Select(Button button)
        {
            var es = EventSystem.current;
            if (es == null) return;
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(button.gameObject);
        }
    }
}
