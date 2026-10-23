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
        public static readonly string[] OptionLabels = { "교체한다", "능력치를 본다", "팰 도감", "닉네임 바꾸기", "그만둔다" };
        public const int OptionSwitch = 0, OptionSummary = 1, OptionCancel = 4;

        /// <summary>"능력치를 본다"를 고르면 부른다 (파티, 칸 번호). BattleScreen이 능력치 화면을 연결한다</summary>
        public Func<IReadOnlyList<Pal>, int, IEnumerator> OpenSummary;

        const float ErrorHoldSeconds = 1.1f;
        const int SortingOrder = 80;               // RewardScreen(50), 웨이브·돈 글자(60)보다 위
        const string NotReadyText = "아직 준비 중인 기능이에요!";

        /// <summary>마지막으로 고른 파티 번호. 취소했으면 -1</summary>
        public int Result { get; private set; } = -1;
        public bool IsOpen => gameObject.activeSelf;

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
            cancelButton.onClick.AddListener(OnCancel);
            var cancelEntry = cancelButton.GetComponent<MenuEntry>();
            if (cancelEntry != null)
            {
                cancelEntry.Selected += _ => OnHighlight(-1);
                cancelEntry.Cancelled += OnCancel;
            }
        }

        /// <summary>파티 화면을 열고 한 칸을 고를 때까지 기다린다. 결과는 Result.</summary>
        /// <param name="actionLabel">메뉴 창 첫 항목 글자. 비우면 "교체한다" (놓아줄 때는 "놓아준다")</param>
        public IEnumerator Choose(IReadOnlyList<Pal> party, bool canCancel, string prompt, Func<int, string> validate,
                                  string actionLabel = null)
        {
            Bind();
            SetActionLabel(string.IsNullOrEmpty(actionLabel) ? OptionLabels[OptionSwitch] : actionLabel);
            this.party = party;
            this.canCancel = canCancel;
            transform.SetAsLastSibling();          // 다른 전투 UI보다 항상 위에 그려지게
            // 자기 캔버스로 맨 위에 그린다 (보상 화면·웨이브/돈 글자보다도 위 → 파티 화면 위에 다른 글자가 안 비친다)
            RewardScreen.EnsureOwnCanvas(gameObject, SortingOrder, true);
            gameObject.SetActive(true);
            cancelButton.gameObject.SetActive(canCancel);
            if (optionsBox != null) optionsBox.SetActive(false);
            inOptions = false;
            Result = -1;

            Select(slotButtons[0]);
            while (true)
            {
                messageText.text = prompt;
                clicked = -2;
                Refresh();
                while (clicked == -2) yield return null;

                if (clicked == -1) { Result = -1; break; }
                int slot = clicked;

                // 칸을 고르면 바로 교체하지 않고 메뉴 창을 띄운다
                if (optionsBox != null)
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

            gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void Update()
        {
            // 빈 곳을 클릭해서 선택이 사라져도 키보드 조작이 끊기지 않게
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null && lastSelected != null && lastSelected.activeInHierarchy)
                es.SetSelectedGameObject(lastSelected);
        }

        void OnClick(int index)
        {
            if (showingError || party == null || index >= party.Count) return;
            clicked = index;
        }

        void OnCancel()
        {
            if (showingError) return;
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
            if (inOptions) return;
            selected = index;
            lastSelected = index >= 0 ? slotButtons[index].gameObject : cancelButton.gameObject;
            Refresh();
        }

        void Refresh()
        {
            if (party == null) return;
            for (int i = 0; i < slots.Length; i++)
                slots[i].Set(i < party.Count ? party[i] : null, i == selected);

            if (cancelFrame != null)
                cancelFrame.uvRect = new Rect(0f, selected == -1 ? 0f : 0.5f, 1f, 0.5f);   // 위 칸=기본, 아래 칸=선택
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
