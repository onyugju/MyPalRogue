using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 화면 전체를 검게 덮었다가 걷어내는 연출 (지역 이동 때 사용).
    /// 모든 화면 위(정렬 500)에 따로 캔버스를 만들고, 검은 동안에는 가운데에 글자(지역 이름)를 띄울 수 있다.
    /// 시간은 unscaled 로 잰다.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        const int SortingOrder = 500;

        Image black;
        TMP_Text title, subtitle;

        /// <summary>지금 검은 정도 (0 = 안 보임, 1 = 완전히 검음)</summary>
        public float Alpha => black != null ? black.color.a : 0f;

        public static ScreenFade Create(TMP_FontAsset font)
        {
            var go = new GameObject("ScreenFade", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320f, 180f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var fade = go.AddComponent<ScreenFade>();
            fade.black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
            Stretch(fade.black.rectTransform, go.transform);
            fade.black.color = new Color(0f, 0f, 0f, 0f);
            fade.black.raycastTarget = false;

            fade.title = MakeText(go.transform, font, "Title", 16f, 6f);
            fade.subtitle = MakeText(go.transform, font, "Subtitle", 8f, -10f);
            fade.subtitle.color = new Color(0.75f, 0.75f, 0.8f, 0f);
            go.SetActive(false);
            return fade;
        }

        static void Stretch(RectTransform rt, Transform parent)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-4f, -4f); rt.offsetMax = new Vector2(4f, 4f);   // 가장자리 틈 없게 조금 크게
        }

        static TMP_Text MakeText(Transform parent, TMP_FontAsset font, string name, float size, float y)
        {
            var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var rt = t.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300f, 24f);
            rt.anchoredPosition = new Vector2(0f, y);
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = new Color(1f, 1f, 1f, 0f);
            t.raycastTarget = false;
            return t;
        }

        /// <summary>검은 화면을 alpha 까지 (클릭은 검은 동안 막는다)</summary>
        public IEnumerator FadeTo(float alpha, float seconds)
        {
            gameObject.SetActive(true);
            float from = Alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, alpha, Smooth(t / seconds)));
                yield return null;
            }
            SetAlpha(alpha);
            if (alpha <= 0f) gameObject.SetActive(false);
        }

        void SetAlpha(float a)
        {
            black.color = new Color(0f, 0f, 0f, a);
            black.raycastTarget = a > 0.01f;
        }

        /// <summary>검은 화면 가운데에 글자를 잠깐 보여 준다 (나타남 → 유지 → 사라짐)</summary>
        public IEnumerator ShowTitle(string text, string sub, float hold)
        {
            title.text = text;
            subtitle.text = sub ?? "";
            const float fade = 0.3f;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime) { SetTextAlpha(t / fade); yield return null; }
            SetTextAlpha(1f);
            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime) { SetTextAlpha(1f - t / fade); yield return null; }
            SetTextAlpha(0f);
        }

        void SetTextAlpha(float a)
        {
            var c = title.color; c.a = a; title.color = c;
            var s = subtitle.color; s.a = a; subtitle.color = s;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
