using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 보스 등장 컷인. 화면이 어두워지고 가운데에 색 띠가 펼쳐지며
    /// 큰 제목("보스 출현!" 등)이 오른쪽에서 미끄러져 들어와 번쩍인 뒤, 띠가 접히며 사라진다.
    /// 전투 화면 위(정렬 300, 화면 전환 연출 500 아래)에 따로 캔버스를 만든다.
    /// 시간은 BattleTime.FxDt (전투 속도를 조금 따르고, 넘기기 키를 누르고 있으면 빨리 끝난다).
    /// </summary>
    public class BossIntro : MonoBehaviour
    {
        const int SortingOrder = 300;
        const float BandHeight = 48f;          // 320x180 기준 띠 높이
        const float DimAlpha = 0.55f;
        const float OpenSeconds = 0.14f, SlideSeconds = 0.22f, HoldSeconds = 0.95f, CloseSeconds = 0.16f;
        const float HeldHold = 0.25f;          // 넘기기 키를 누르고 있으면 이만큼만 보여 준다

        Image dim, band, edgeTop, edgeBottom;
        const float HeaderY = 14f, TitleY = 1f, SubY = -14f;   // 이명(작게) / 이름·제목(크게) / 부제
        TMP_Text header, title, subtitle;

        public static BossIntro Create(TMP_FontAsset font)
        {
            var go = new GameObject("BossIntro", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320f, 180f);
            scaler.matchWidthOrHeight = 0.5f;

            var bi = go.AddComponent<BossIntro>();
            bi.dim = MakeImage(go.transform, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Color.clear);
            bi.band = MakeImage(go.transform, "Band", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, BandHeight), Color.clear);
            bi.edgeTop = MakeImage(bi.band.transform, "EdgeTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 2f), Color.clear);
            bi.edgeBottom = MakeImage(bi.band.transform, "EdgeBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), Color.clear);
            bi.header = MakeText(bi.band.transform, font, "Header", 9f, HeaderY);
            bi.title = MakeText(bi.band.transform, font, "Title", 18f, TitleY);
            bi.subtitle = MakeText(bi.band.transform, font, "Subtitle", 8f, SubY);
            go.SetActive(false);
            return bi;
        }

        static Image MakeImage(Transform parent, string name, Vector2 min, Vector2 max, Vector2 size, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            var rt = img.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = min; rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            if (min == Vector2.zero && max == Vector2.one) { rt.offsetMin = new Vector2(-4f, -4f); rt.offsetMax = new Vector2(4f, 4f); }
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TMP_Text MakeText(Transform parent, TMP_FontAsset font, string name, float size, float y)
        {
            var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var rt = t.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300f, 20f);
            rt.anchoredPosition = new Vector2(0f, y);
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Midline;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(20, 10, 10, 255);
            t.raycastTarget = false;
            return t;
        }

        /// <summary>컷인 한 번 재생 (끝나면 다시 숨는다). headerText = 이름 위 작은 글자 (전설의 이명, 없으면 null)</summary>
        public IEnumerator Play(string headerText, string titleText, string subtitleText, Color color)
        {
            gameObject.SetActive(true);
            header.text = headerText ?? "";
            title.text = titleText;
            subtitle.text = subtitleText;
            float titleY = string.IsNullOrEmpty(headerText) ? 5f : TitleY;    // 이명이 없으면 제목을 조금 위로
            var bandColor = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.92f);
            var edge = new Color(Mathf.Min(1f, color.r * 1.2f), Mathf.Min(1f, color.g * 1.2f), Mathf.Min(1f, color.b * 1.2f), 1f);
            band.color = bandColor;
            edgeTop.color = edgeBottom.color = edge;
            var bandRt = band.rectTransform;
            var titleRt = title.rectTransform;
            var subRt = subtitle.rectTransform;
            var headRt = header.rectTransform;
            Vector2 titleHome = new Vector2(0f, titleY), subHome = new Vector2(0f, string.IsNullOrEmpty(headerText) ? -11f : SubY);
            Vector2 headHome = new Vector2(0f, HeaderY);

            // ① 어두워지면서 띠가 위아래로 펼쳐진다
            SetText(0f);
            for (float t = 0f; t < OpenSeconds; t += BattleTime.FxDt)
            {
                float k = t / OpenSeconds;
                dim.color = new Color(0f, 0f, 0f, DimAlpha * k);
                bandRt.localScale = new Vector3(1f, Ease(k), 1f);
                yield return null;
            }
            dim.color = new Color(0f, 0f, 0f, DimAlpha);
            bandRt.localScale = Vector3.one;

            // ② 제목은 오른쪽에서, 부제는 왼쪽에서 미끄러져 들어온다
            for (float t = 0f; t < SlideSeconds; t += BattleTime.FxDt)
            {
                float k = Ease(t / SlideSeconds);
                titleRt.anchoredPosition = titleHome + Vector2.right * (1f - k) * 220f;
                subRt.anchoredPosition = subHome + Vector2.left * (1f - k) * 220f;
                headRt.anchoredPosition = headHome + Vector2.left * (1f - k) * 220f;
                SetText(k);
                yield return null;
            }
            titleRt.anchoredPosition = titleHome;
            subRt.anchoredPosition = subHome;
            headRt.anchoredPosition = headHome;
            SetText(1f);

            // ③ 제목이 두 번 번쩍이고 잠깐 머문다 (넘기기 키를 누르고 있으면 짧게)
            for (float t = 0f; t < HoldSeconds; t += BattleTime.FxDt)
            {
                if (BattleTime.AdvanceHeld && t > HeldHold) break;
                bool flash = t < 0.3f && Mathf.FloorToInt(t / 0.075f) % 2 == 0;
                title.color = flash ? edge : Color.white;
                yield return null;
            }
            title.color = Color.white;

            // ④ 띠가 접히고 화면이 밝아진다
            for (float t = 0f; t < CloseSeconds; t += BattleTime.FxDt)
            {
                float k = 1f - t / CloseSeconds;
                dim.color = new Color(0f, 0f, 0f, DimAlpha * k);
                bandRt.localScale = new Vector3(1f, k, 1f);
                SetText(k);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        void SetText(float a)
        {
            header.color = new Color(1f, 0.85f, 0.45f, a);       // 이명은 금빛
            title.color = new Color(1f, 1f, 1f, a);
            subtitle.color = new Color(1f, 0.92f, 0.8f, a);
        }

        static float Ease(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
    }
}
