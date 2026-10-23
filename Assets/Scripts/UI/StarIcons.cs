using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 응축 ★을 이름 바로 오른쪽에 작은 별 그림으로 보여준다 (글꼴에 ★ 글자가 없어도 보이게 그림을 쓴다).
    /// 이름 글자(왼쪽 정렬)의 자식으로 별 줄을 만들어 두고, 이름 길이에 맞춰 위치를 옮긴다.
    /// 그림: Resources/UI/star.png (7x7)
    /// </summary>
    public static class StarIcons
    {
        const string RowName = "Stars";
        const float Size = 7f, Gap = 0f, LeftPad = 2f;
        static Texture2D star;

        public static void Show(TMP_Text nameText, int stars)
        {
            if (nameText == null) return;
            var row = nameText.transform.Find(RowName) as RectTransform;
            if (stars <= 0)
            {
                if (row != null) row.gameObject.SetActive(false);
                return;
            }
            if (star == null)
            {
                star = Resources.Load<Texture2D>("UI/star");
                if (star == null) { Debug.LogWarning("[StarIcons] Resources/UI/star.png 가 없습니다."); return; }
                star.filterMode = FilterMode.Point;
            }
            if (row == null)
            {
                var go = new GameObject(RowName, typeof(RectTransform));
                go.transform.SetParent(nameText.transform, false);
                row = (RectTransform)go.transform;
                row.anchorMin = row.anchorMax = new Vector2(0f, 0.5f);
                row.pivot = new Vector2(0f, 0.5f);
            }
            row.gameObject.SetActive(true);

            // 이름 글자 폭만큼 오른쪽에 붙인다
            float width = nameText.GetPreferredValues(nameText.text).x;
            row.anchoredPosition = new Vector2(Mathf.Round(width + LeftPad), 0f);
            row.sizeDelta = new Vector2(stars * (Size + Gap), Size);

            for (int i = 0; i < Mathf.Max(stars, row.childCount); i++)
            {
                RawImage img;
                if (i < row.childCount) img = row.GetChild(i).GetComponent<RawImage>();
                else
                {
                    var go = new GameObject("Star", typeof(RectTransform));
                    go.transform.SetParent(row, false);
                    img = go.AddComponent<RawImage>();
                    img.raycastTarget = false;
                    var rt = img.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.sizeDelta = new Vector2(Size, Size);
                }
                img.texture = star;
                img.rectTransform.anchoredPosition = new Vector2(i * (Size + Gap), 0f);
                img.gameObject.SetActive(i < stars);
            }
        }
    }
}
