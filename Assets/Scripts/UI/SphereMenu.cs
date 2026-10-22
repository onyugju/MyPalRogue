using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 포켓로그식 스피어(볼) 메뉴. 가진 스피어만 "x개수  이름"으로 보여주고, 맨 아래에 "취소".
    /// 사용법:  yield return sphereMenu.Choose(보유개수배열);   int t = sphereMenu.Result;  // -1이면 취소
    /// 창은 오른쪽 아래에 붙어 있고, 줄 수에 맞춰 위로 늘어난다. 참조 칸들은 메뉴 PalRogue/16 이 연결한다.
    /// </summary>
    public class SphereMenu : MonoBehaviour
    {
        public RectTransform window;       // 창 (오른쪽 아래 기준)
        public Button[] rowButtons;        // 최대 6줄 (스피어 5종 + 취소)
        public TMP_Text[] countTexts;
        public TMP_Text[] nameTexts;
        public Image cursor;

        public const float RowTop = 9f, RowStep = 16f, BottomPad = 7f;
        const string CancelText = "취소";

        /// <summary>고른 스피어 종류(SphereType 번호). 취소했으면 -1</summary>
        public int Result { get; private set; } = -1;

        readonly List<int> rowTypes = new List<int>();   // 줄 번호 → 스피어 종류 (-1 = 취소)
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

        public IEnumerator Choose(int[] counts)
        {
            Bind();
            rowTypes.Clear();
            for (int t = 0; t < counts.Length && t < PalSpheres.Count; t++)
                if (counts[t] > 0) rowTypes.Add(t);
            rowTypes.Add(-1);                                    // 취소

            int rows = Mathf.Min(rowTypes.Count, rowButtons.Length);
            for (int i = 0; i < rowButtons.Length; i++)
            {
                bool on = i < rows;
                rowButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                int t = rowTypes[i];
                countTexts[i].text = t >= 0 ? $"x{counts[t]}" : "";
                nameTexts[i].text = t >= 0 ? PalSpheres.Names[t] : CancelText;
            }
            window.sizeDelta = new Vector2(window.sizeDelta.x, RowTop + RowStep * rows + BottomPad);

            // 방향키 위/아래로 보이는 줄 안에서만 돌게
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
            Select(rowButtons[0]);
            while (clicked == -2) yield return null;

            Result = clicked >= 0 && clicked < rows ? rowTypes[clicked] : -1;
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
