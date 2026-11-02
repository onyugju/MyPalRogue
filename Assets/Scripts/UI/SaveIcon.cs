using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 자동 저장 표시: 화면 맨 오른쪽 아래에 저장 아이콘(Resources/UI/saving_icon)이 3초 동안 깜빡이다가 사라진다.
    /// 아이콘 왼쪽에 "저장 완료" (실패하면 빨간 "저장 실패!") 글자도 같이 보여 준다.
    /// 사용법: SaveIcon.Show(성공?, 글꼴);
    /// </summary>
    public class SaveIcon : MonoBehaviour
    {
        const int SortingOrder = 450;
        const float ShowSeconds = 3f, FadeSeconds = 0.3f;
        const float Size = 16f, Margin = 3f;

        static SaveIcon instance;
        RawImage icon;
        TMP_Text label;
        Coroutine running;
        bool ok = true;

        static readonly Color OkColor = new Color(0.97f, 0.97f, 0.97f), FailColor = new Color(1f, 0.35f, 0.35f);

        public static void Show(bool ok = true, TMP_FontAsset font = null)
        {
            if (instance == null) instance = Create();
            if (instance == null) return;
            instance.ok = ok;
            if (font != null && instance.label.font != font) instance.label.font = font;
            instance.label.text = ok ? "저장 완료" : "저장 실패!";
            if (instance.running != null) instance.StopCoroutine(instance.running);
            instance.running = instance.StartCoroutine(instance.Play());
        }

        static SaveIcon Create()
        {
            var tex = Resources.Load<Texture2D>("UI/saving_icon");
            var go = new GameObject("SaveIcon", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320f, 180f);
            scaler.matchWidthOrHeight = 0.5f;

            var s = go.AddComponent<SaveIcon>();
            s.icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<RawImage>();
            var rt = s.icon.rectTransform;
            rt.SetParent(go.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);       // 화면 오른쪽 아래 구석
            rt.anchoredPosition = new Vector2(-Margin, Margin);
            rt.sizeDelta = new Vector2(Size, Size);
            if (tex != null) { tex.filterMode = FilterMode.Point; s.icon.texture = tex; }
            else s.icon.color = new Color(0.3f, 0.6f, 1f);                         // 그림이 없으면 파란 네모
            s.icon.raycastTarget = false;
            s.icon.enabled = false;

            s.label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var lrt = s.label.rectTransform;
            lrt.SetParent(go.transform, false);
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(1f, 0f);
            lrt.anchoredPosition = new Vector2(-Margin - Size - 2f, Margin);
            lrt.sizeDelta = new Vector2(60f, Size);
            s.label.fontSize = 8f;
            s.label.alignment = TextAlignmentOptions.MidlineRight;
            s.label.textWrappingMode = TextWrappingModes.NoWrap;
            s.label.outlineWidth = 0.25f;
            s.label.outlineColor = new Color32(30, 30, 30, 255);
            s.label.raycastTarget = false;
            s.label.enabled = false;
            return s;
        }

        IEnumerator Play()
        {
            icon.enabled = true;
            label.enabled = true;
            var c = icon.color;
            var lc = ok ? OkColor : FailColor;
            for (float t = 0; t < ShowSeconds; t += Time.unscaledDeltaTime)
            {
                // 처음과 끝은 부드럽게 나타나고 사라지며, 그 사이에는 천천히 깜빡인다
                float a = Mathf.Min(1f, t / FadeSeconds, (ShowSeconds - t) / FadeSeconds);
                a *= 0.75f + 0.25f * Mathf.Cos(t * Mathf.PI * 2f);
                icon.color = new Color(c.r, c.g, c.b, a);
                label.color = new Color(lc.r, lc.g, lc.b, Mathf.Min(1f, t / FadeSeconds, (ShowSeconds - t) / FadeSeconds));
                yield return null;
            }
            icon.enabled = false;
            label.enabled = false;
            running = null;
        }
    }
}
