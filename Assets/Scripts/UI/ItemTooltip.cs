using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 아이템 설명 말풍선 (빨간 테두리 칸: 이름(등급 색) + 효과 설명).
    /// 아이템 줄·능력치 화면이 같이 쓴다. 다른 모든 화면 위(470)에 그리고, 마우스를 막지 않는다(레이캐스터 없음).
    /// </summary>
    public class ItemTooltip : MonoBehaviour
    {
        const int Order = 470;
        static ItemTooltip inst;
        RectTransform root, box, inner;
        TMP_Text title, desc;

        static ItemTooltip Get(TMP_FontAsset font)
        {
            if (inst != null) return inst;
            var go = new GameObject("ItemTooltip", typeof(RectTransform));
            inst = go.AddComponent<ItemTooltip>();
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320f, 180f);
            scaler.matchWidthOrHeight = 0.5f;
            inst.root = new GameObject("Layout", typeof(RectTransform)).GetComponent<RectTransform>();
            inst.root.SetParent(go.transform, false);
            inst.root.anchorMin = inst.root.anchorMax = inst.root.pivot = new Vector2(0.5f, 0.5f);
            inst.root.sizeDelta = new Vector2(320f, 180f);
            inst.box = UiKit.Frame(inst.root, "Tooltip", 0, 0, 120, 30, UiKit.FrameRed, UiKit.FrameFill, out inst.inner);
            inst.title = UiKit.Label(inst.inner, font, "Name", 5, 2, 200, 12, 10, TextAlignmentOptions.TopLeft, UiKit.White);
            inst.desc = UiKit.Label(inst.inner, font, "Desc", 5, 14, 200, 12, 8, TextAlignmentOptions.TopLeft, UiKit.White);
            inst.box.gameObject.SetActive(false);
            return inst;
        }

        /// <summary>anchor 칸의 오른쪽 아래에 띄운다 (화면 밖으로 나가지 않게)</summary>
        public static void Show(TMP_FontAsset font, string name, Color nameColor, string text, RectTransform anchor)
        {
            var t = Get(font);
            t.title.text = name;
            t.title.color = nameColor;
            t.desc.text = text ?? "";
            var nameSize = t.title.GetPreferredValues(name);
            var descSize = t.desc.GetPreferredValues(t.desc.text);
            float w = Mathf.Clamp(Mathf.Max(nameSize.x, descSize.x) + 14, 60, 300);
            float h = 18 + descSize.y + 4;
            t.box.sizeDelta = new Vector2(w, h);
            t.inner.sizeDelta = new Vector2(w - 4, h - 4);
            t.desc.rectTransform.sizeDelta = new Vector2(w - 10, descSize.y + 2);

            // anchor의 오른쪽 아래 모서리 (320x180 판 기준, 아래로 +)
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            var br = t.root.InverseTransformPoint(corners[3]);
            float x = Mathf.Clamp(br.x + 160f + 2, 2, 318 - w);
            float y = Mathf.Clamp(90f - br.y + 2, 2, 178 - h);
            t.box.anchoredPosition = new Vector2(x, -y);
            t.box.gameObject.SetActive(true);
        }

        public static void Hide()
        {
            if (inst != null) inst.box.gameObject.SetActive(false);
        }

        /// <summary>go에 마우스를 올리면 설명이 뜨고, 벗어나면 사라지게 한다</summary>
        public static void Attach(GameObject go, TMP_FontAsset font, Func<(string name, Color color, string desc)> info, Action enter = null, Action exit = null)
        {
            var g = go.GetComponent<Graphic>();
            if (g == null) { var img = go.AddComponent<Image>(); img.color = new Color(1, 1, 1, 0); g = img; }
            g.raycastTarget = true;
            var h = go.AddComponent<Hover>();
            h.enter = () =>
            {
                enter?.Invoke();
                var (n, c, d) = info();
                Show(font, n, c, d, (RectTransform)go.transform);
            };
            h.exit = () => { Hide(); exit?.Invoke(); };
        }

        class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action enter, exit;
            public void OnPointerEnter(PointerEventData e) => enter?.Invoke();
            public void OnPointerExit(PointerEventData e) => exit?.Invoke();
            void OnDisable() { if (inst != null && inst.box.gameObject.activeSelf) exit?.Invoke(); }   // 칸이 사라지면 말풍선도
        }
    }
}
