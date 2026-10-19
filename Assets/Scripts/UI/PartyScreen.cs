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

        const float ErrorHoldSeconds = 1.1f;

        /// <summary>마지막으로 고른 파티 번호. 취소했으면 -1</summary>
        public int Result { get; private set; } = -1;
        public bool IsOpen => gameObject.activeSelf;

        IReadOnlyList<Pal> party;
        int selected = -1;
        int clicked = -2;                  // -2: 아직 안 고름, -1: 취소, 0~5: 칸 번호
        bool canCancel;
        bool showingError;
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
            cancelButton.onClick.AddListener(OnCancel);
            var cancelEntry = cancelButton.GetComponent<MenuEntry>();
            if (cancelEntry != null)
            {
                cancelEntry.Selected += _ => OnHighlight(-1);
                cancelEntry.Cancelled += OnCancel;
            }
        }

        /// <summary>파티 화면을 열고 한 칸을 고를 때까지 기다린다. 결과는 Result.</summary>
        public IEnumerator Choose(IReadOnlyList<Pal> party, bool canCancel, string prompt, Func<int, string> validate)
        {
            Bind();
            this.party = party;
            this.canCancel = canCancel;
            transform.SetAsLastSibling();          // 다른 전투 UI보다 항상 위에 그려지게
            gameObject.SetActive(true);
            cancelButton.gameObject.SetActive(canCancel);
            Result = -1;

            Select(slotButtons[0]);
            while (true)
            {
                messageText.text = prompt;
                clicked = -2;
                Refresh();
                while (clicked == -2) yield return null;

                if (clicked == -1) { Result = -1; break; }

                string error = validate?.Invoke(clicked);
                if (string.IsNullOrEmpty(error)) { Result = clicked; break; }

                // 고를 수 없는 칸: 이유를 잠깐 보여주고 다시 고르게 한다
                showingError = true;
                messageText.text = error;
                yield return new WaitForSecondsRealtime(ErrorHoldSeconds);
                showingError = false;
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
            if (showingError || !canCancel) return;
            clicked = -1;
        }

        void OnHighlight(int index)
        {
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
