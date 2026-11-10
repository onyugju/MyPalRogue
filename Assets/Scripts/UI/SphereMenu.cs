using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 포켓로그식 스피어(볼) 메뉴. 스피어 5종을 항상 "그림  이름   x개수"로 보여주고, 맨 아래에 "취소".
    ///  - 고른 줄의 스피어 그림이 살짝 흔들린다 (포켓로그와 같은 느낌)
    ///  - 마지막으로 고른 스피어에 다음에도 커서가 간다
    ///  - 개수가 0인 스피어는 골라도 반응하지 않는다
    /// 사용법:  yield return sphereMenu.Choose(보유개수배열);   int t = sphereMenu.Result;  // -1이면 취소
    /// 창·줄 배치는 메뉴 PalRogue/36 으로 만든 오브젝트를 코드가 처음 열 때 작게 다시 맞춘다.
    /// </summary>
    public class SphereMenu : MonoBehaviour
    {
        public RectTransform window;       // 창 (오른쪽 아래 기준)
        public Button[] rowButtons;        // 6줄 (스피어 5종 + 취소)
        public TMP_Text[] countTexts;
        public TMP_Text[] nameTexts;
        public Image cursor;

        /// <summary>이 칸(명령 창) 바로 위, 오른쪽 끝에 맞춰 창을 띄운다 (포켓로그처럼 명령 창은 그대로 보인다). null이면 화면 오른쪽 아래</summary>
        public RectTransform placeAbove;

        // 320x180 화면 픽셀 기준 크기 (예전보다 작게)
        public const float RowTop = 6f, RowStep = 12f, BottomPad = 6f;
        const float WindowWidth = 110f, RowX = 4f, RowWidth = 102f, RowHeight = 12f;
        const float CursorX = 4f, IconX = 11f, IconSize = 10f;
        const float NameX = 23f, NameWidth = 58f, CountX = 82f, CountWidth = 22f;
        const float FontSize = 9f;
        const float WobbleSeconds = 0.35f, WobbleAngle = 18f;
        const string CancelText = "취소";

        /// <summary>고른 스피어 종류(SphereType 번호). 취소했으면 -1</summary>
        public int Result { get; private set; } = -1;

        readonly int[] counts = new int[PalSpheres.Count];
        int lastRow;                                     // 마지막으로 고른 줄 (다음에 열면 여기에 커서)
        int clicked = -2;
        GameObject lastSelected;
        bool bound;
        Image[] icons;
        Coroutine wobble;

        int Rows => Mathf.Min(PalSpheres.Count + 1, rowButtons.Length);
        int CancelRow => Rows - 1;

        void Bind()
        {
            if (bound) return;
            bound = true;
            Layout();
            for (int i = 0; i < rowButtons.Length; i++)
            {
                int row = i;
                rowButtons[i].onClick.AddListener(() => OnClick(row));
                var entry = rowButtons[i].GetComponent<MenuEntry>();
                if (entry == null) continue;
                entry.Selected += e => OnHighlight(row, e);
                entry.Cancelled += () => clicked = -1;
            }
        }

        /// <summary>창·줄·글자를 작게 다시 배치하고 줄마다 스피어 그림을 단다</summary>
        void Layout()
        {
            window.sizeDelta = new Vector2(WindowWidth, RowTop + RowStep * Rows + BottomPad);
            icons = new Image[rowButtons.Length];
            for (int i = 0; i < rowButtons.Length; i++)
            {
                var rt = (RectTransform)rowButtons[i].transform;
                Place(rt, RowX, RowTop + RowStep * i, RowWidth, RowHeight);
                bool cancel = i == CancelRow;
                // 이름 (왼쪽), 개수 (오른쪽)
                Place(nameTexts[i].rectTransform, cancel ? IconX - RowX : NameX - RowX, 0f, NameWidth, RowHeight);
                nameTexts[i].alignment = TextAlignmentOptions.MidlineLeft;
                Place(countTexts[i].rectTransform, CountX - RowX, 0f, CountWidth, RowHeight);
                countTexts[i].alignment = TextAlignmentOptions.MidlineRight;
                foreach (var t in new[] { nameTexts[i], countTexts[i] })
                {
                    t.fontSize = FontSize;
                    t.enableAutoSizing = true;          // 이름이 길면 글자만 조금 줄인다
                    t.fontSizeMax = FontSize;
                    t.fontSizeMin = 6f;
                    t.textWrappingMode = TextWrappingModes.NoWrap;
                }
                if (cancel || i >= PalSpheres.Count) continue;

                var go = new GameObject("Icon", typeof(RectTransform));
                go.transform.SetParent(rt, false);
                var irt = (RectTransform)go.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 1f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = new Vector2(IconX - RowX + IconSize / 2f, -RowHeight / 2f);
                irt.sizeDelta = new Vector2(IconSize, IconSize);
                var img = go.AddComponent<Image>();
                img.sprite = PalSpheres.LoadSprite((SphereType)i);
                img.preserveAspect = true;
                img.raycastTarget = false;
                icons[i] = img;
            }
            for (int i = Rows; i < rowButtons.Length; i++) rowButtons[i].gameObject.SetActive(false);
            if (cursor != null)
            {
                cursor.rectTransform.sizeDelta = new Vector2(5f, 8f);
                cursor.rectTransform.anchoredPosition = new Vector2(CursorX, -(RowTop + 2f));
            }
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public IEnumerator Choose(int[] have)
        {
            Bind();
            for (int t = 0; t < PalSpheres.Count; t++) counts[t] = have != null && t < have.Length ? have[t] : 0;

            for (int i = 0; i < Rows; i++)
            {
                rowButtons[i].gameObject.SetActive(true);
                bool cancel = i == CancelRow;
                nameTexts[i].text = cancel ? CancelText : PalSpheres.Names[i];
                countTexts[i].text = cancel ? "" : $"x{counts[i]}";
                var c = !cancel && counts[i] <= 0 ? new Color(0.6f, 0.6f, 0.6f) : Color.white;   // 없는 스피어는 흐리게
                nameTexts[i].color = countTexts[i].color = c;
                if (icons[i] != null)
                {
                    icons[i].color = counts[i] > 0 ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                    icons[i].rectTransform.localRotation = Quaternion.identity;
                }
            }

            // 방향키 위/아래로 줄 안에서 돈다
            for (int i = 0; i < Rows; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = rowButtons[(i + Rows - 1) % Rows];
                nav.selectOnDown = rowButtons[(i + 1) % Rows];
                rowButtons[i].navigation = nav;
            }

            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            PlaceAboveBox();
            clicked = -2;
            // 마지막으로 고른 스피어 (다 떨어졌으면 가진 것 중 첫 번째)
            int start = lastRow >= 0 && lastRow < CancelRow && counts[lastRow] > 0 ? lastRow : FirstOwned();
            Select(rowButtons[start]);
            while (clicked == -2) yield return null;

            Result = clicked >= 0 && clicked < CancelRow ? clicked : -1;
            if (Result >= 0) lastRow = Result;
            gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>창의 오른쪽 아래 모서리를 placeAbove 칸의 오른쪽 위 모서리에 붙인다</summary>
        void PlaceAboveBox()
        {
            var parent = window.parent as RectTransform;
            if (placeAbove == null || parent == null || !placeAbove.gameObject.activeInHierarchy) { window.anchoredPosition = Vector2.zero; return; }
            var corners = new Vector3[4];
            placeAbove.GetWorldCorners(corners);                       // 0 왼쪽 아래, 1 왼쪽 위, 2 오른쪽 위, 3 오른쪽 아래
            Vector2 topRight = parent.InverseTransformPoint(corners[2]);
            var r = parent.rect;
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(1f, 0f);
            window.anchoredPosition = new Vector2(Mathf.Min(0f, topRight.x - r.xMax), topRight.y - r.yMin);
        }

        int FirstOwned()
        {
            for (int i = 0; i < CancelRow; i++) if (counts[i] > 0) return i;
            return CancelRow;
        }

        void OnClick(int row)
        {
            if (row < CancelRow && counts[row] <= 0) { GameAudio.Se("error"); return; }   // 없는 스피어
            clicked = row;
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
                cursor.rectTransform.anchoredPosition = new Vector2(CursorX, -(RowTop + RowStep * row + 2f));
            if (wobble != null) StopCoroutine(wobble);
            foreach (var ic in icons) if (ic != null) ic.rectTransform.localRotation = Quaternion.identity;
            if (row < icons.Length && icons[row] != null && isActiveAndEnabled) wobble = StartCoroutine(Wobble(icons[row].rectTransform));
        }

        /// <summary>고른 스피어 그림을 좌우로 한 번 흔든다</summary>
        IEnumerator Wobble(RectTransform rt)
        {
            for (float t = 0f; t < WobbleSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / WobbleSeconds;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 3f) * WobbleAngle * (1f - k));
                yield return null;
            }
            rt.localRotation = Quaternion.identity;
            wobble = null;
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
