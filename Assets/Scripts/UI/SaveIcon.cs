using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 자동 저장 표시: 화면 맨 오른쪽 아래에 저장 아이콘(Resources/UI/saving_icon)이 3초 동안 깜빡이다가 사라진다.
    /// 사용법: SaveIcon.Show();
    /// </summary>
    public class SaveIcon : MonoBehaviour
    {
        const int SortingOrder = 450;
        const float ShowSeconds = 3f, FadeSeconds = 0.3f;
        const float Size = 16f, Margin = 3f;

        static SaveIcon instance;
        RawImage icon;
        Coroutine running;

        public static void Show()
        {
            if (instance == null) instance = Create();
            if (instance == null) return;
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
            return s;
        }

        IEnumerator Play()
        {
            icon.enabled = true;
            var c = icon.color;
            for (float t = 0; t < ShowSeconds; t += Time.unscaledDeltaTime)
            {
                // 처음과 끝은 부드럽게 나타나고 사라지며, 그 사이에는 천천히 깜빡인다
                float a = Mathf.Min(1f, t / FadeSeconds, (ShowSeconds - t) / FadeSeconds);
                a *= 0.75f + 0.25f * Mathf.Cos(t * Mathf.PI * 2f);
                icon.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
            icon.enabled = false;
            running = null;
        }
    }
}
