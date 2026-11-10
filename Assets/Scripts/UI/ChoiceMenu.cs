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
    /// 창은 아래쪽 기준이라 줄 수에 맞춰 위로 늘어난다. 참조 칸들은 메뉴 PalRogue/37 이 연결한다.
    /// </summary>
    public class ChoiceMenu : MonoBehaviour
    {
        public RectTransform window;
        public Button[] rowButtons;        // 최대 6줄
        public TMP_Text[] labels;
        public Image cursor;

        public const float RowTop = 9f, RowStep = 16f, BottomPad = 7f;

        public int Result { get; private set; } = -1;

        /// <summary>커서가 다른 줄로 옮겨갈 때 (줄 번호). 설명 문구를 바꾸는 데 쓴다</summary>
        public event System.Action<int> Highlighted;

        /// <summary>창이 떠 있을 때 ◀ ▶ 를 누르면 부른다 (커서 줄, -1/+1). 수량 바꾸기 등에 쓴다. 쓰지 않으면 null</summary>
        public System.Action<int, int> HorizontalPressed;

        /// <summary>떠 있는 창의 한 줄 글자를 바꾼다</summary>
        public void SetLabel(int row, string text) { if (row >= 0 && row < labels.Length && labels[row] != null) labels[row].text = text; }

        int currentRow;

        /// <summary>
        /// 이 창을 다른 화면(파티 화면 등) 위에 그린다. 0 이상이면 창이 열릴 때마다 자기 캔버스 순서를 이 값으로 맞춘다
        /// (꺼져 있는 오브젝트에 캔버스 순서를 정하면 켜질 때 풀리는 경우가 있어서, 켠 뒤에 맞춘다)
        /// </summary>
        public int DrawOrder { get; set; } = -1;

        const float DefaultWidth = 92f, RowX = 8f, TextX = 12f;

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
        /// <param name="width">창 폭. 0이면 기본(92). 긴 글자가 들어갈 때 넓힌다</param>
        public IEnumerator Choose(string[] items, int startIndex = 0, float width = 0f)
        {
            Bind();
            SetWidth(width > 0f ? width : DefaultWidth);
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
            if (DrawOrder >= 0) RewardScreen.EnsureOwnCanvas(gameObject, DrawOrder, true);
            clicked = -2;
            Select(rowButtons[Mathf.Clamp(startIndex, 0, rows - 1)]);
            while (clicked == -2) yield return null;

            Result = clicked;
            gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void Update()
        {
            if (HorizontalPressed != null && clicked == -2)
            {
                int h = MenuKeys.Horizontal;
                if (h != 0) HorizontalPressed(currentRow, h);
            }
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null && lastSelected != null && lastSelected.activeInHierarchy)
                es.SetSelectedGameObject(lastSelected);
        }

        void SetWidth(float w)
        {
            window.sizeDelta = new Vector2(w, window.sizeDelta.y);
            for (int i = 0; i < rowButtons.Length; i++)
            {
                var row = (RectTransform)rowButtons[i].transform;
                row.sizeDelta = new Vector2(w - RowX * 2f, row.sizeDelta.y);
                if (labels[i] != null) labels[i].rectTransform.sizeDelta = new Vector2(w - RowX * 2f - TextX, labels[i].rectTransform.sizeDelta.y);
            }
        }

        void OnHighlight(int row, MenuEntry entry)
        {
            currentRow = row;
            Highlighted?.Invoke(row);
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
