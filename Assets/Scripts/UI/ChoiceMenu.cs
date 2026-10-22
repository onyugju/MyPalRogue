using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 메시지창 위 오른쪽에 뜨는 작은 선택 창 (예: 능력치를 본다 / 팰 도감 / 예 / 아니오).
    /// 사용법:  yield return choiceMenu.Choose(new[] { "예", "아니오" }, 0);   int i = choiceMenu.Result;  // Esc면 -1
    /// 창은 아래쪽 기준이라 줄 수에 맞춰 위로 늘어난다. 참조 칸들은 메뉴 PalRogue/17 이 연결한다.
    /// </summary>
    public class ChoiceMenu : MonoBehaviour
    {
        public RectTransform window;
        public Button[] rowButtons;        // 최대 6줄
        public TMP_Text[] labels;
        public Image cursor;

        public const float RowTop = 9f, RowStep = 16f, BottomPad = 7f;

        public int Result { get; private set; } = -1;

        int clicked = -2;
        GameObject lastSelected;
        bool bound;

        void Bind()
        {
            if (bound) return;
            bound = true;
            for (int i = 0; i < rowButtons.Length; i++)
            {
                int row = i;
                rowButtons[i].onClick.AddListener(() => clicked = row);
                var entry = rowButtons[i].GetComponent<MenuEntry>();
                if (entry == null) continue;
                entry.Selected += e => OnHighlight(row, e);
                entry.Cancelled += () => clicked = -1;
            }
        }

        /// <summary>항목을 보여주고 하나 고를 때까지 기다린다. startIndex = 처음 커서 위치</summary>
        public IEnumerator Choose(string[] items, int startIndex = 0)
        {
            Bind();
            int rows = Mathf.Min(items.Length, rowButtons.Length);
            for (int i = 0; i < rowButtons.Length; i++)
            {
                bool on = i < rows;
                rowButtons[i].gameObject.SetActive(on);
                if (on) labels[i].text = items[i];
            }
            window.sizeDelta = new Vector2(window.sizeDelta.x, RowTop + RowStep * rows + BottomPad);

            for (int i = 0; i < rows; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = rowButtons[(i + rows - 1) % rows];
                nav.selectOnDown = rowButtons[(i + 1) % rows];
                rowButtons[i].navigation = nav;
            }

            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            clicked = -2;
            Select(rowButtons[Mathf.Clamp(startIndex, 0, rows - 1)]);
            while (clicked == -2) yield return null;

            Result = clicked;
            gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void Update()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null && lastSelected != null && lastSelected.activeInHierarchy)
                es.SetSelectedGameObject(lastSelected);
        }

        void OnHighlight(int row, MenuEntry entry)
        {
            lastSelected = entry.gameObject;
            if (cursor != null)
                cursor.rectTransform.anchoredPosition = new Vector2(cursor.rectTransform.anchoredPosition.x, -(RowTop + RowStep * row + 1f));
        }

        void Select(Button b)
        {
            var es = EventSystem.current;
            if (es == null) return;
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(b.gameObject);
        }
    }
}
