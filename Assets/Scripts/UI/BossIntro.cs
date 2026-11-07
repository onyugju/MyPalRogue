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

        // ═════════════════ 제로버스 2페이즈: 대형 컷인 ═════════════════
        const float GrandBandHeight = 64f, StripeHeight = 5f, StripeGap = 9f;
        const float GrandHeaderY = 19f, GrandTitleY = 0f, GrandSubY = -20f;
        const float GrandTitleSize = 26f, GrandHeaderSize = 11f, GrandSubSize = 10f;
        const float GrandHold = 1.5f;
        const string GlitchChars = "#%&?!@*/\\=+<>01";
        Image stripeTop, stripeBottom, flash;
        TMP_Text ghostA, ghostB;

        void EnsureGrandParts()
        {
            if (stripeTop != null) return;
            stripeTop = MakeImage(transform, "StripeTop", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, StripeHeight), Color.clear);
            stripeBottom = MakeImage(transform, "StripeBottom", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, StripeHeight), Color.clear);
            // 이름 뒤에 겹쳐 보이는 붉은·푸른 잔상 (글리치)
            ghostA = MakeText(band.transform, title.font as TMP_FontAsset, "GhostA", GrandTitleSize, GrandTitleY);
            ghostB = MakeText(band.transform, title.font as TMP_FontAsset, "GhostB", GrandTitleSize, GrandTitleY);
            ghostA.transform.SetSiblingIndex(title.transform.GetSiblingIndex());
            ghostB.transform.SetSiblingIndex(title.transform.GetSiblingIndex());
            flash = MakeImage(transform, "Flash", Vector2.zero, Vector2.one, Vector2.zero, Color.clear);
        }

        /// <summary>화면 전체를 color로 덮거나 걷는다 (from → to 투명도). 다 걷히면 숨는다</summary>
        public IEnumerator FadeScreen(Color color, float from, float to, float seconds)
        {
            gameObject.SetActive(true);
            band.color = edgeTop.color = edgeBottom.color = Color.clear;
            SetText(0f);
            if (ghostA != null) { ghostA.text = ghostB.text = ""; stripeTop.color = stripeBottom.color = flash.color = Color.clear; }
            for (float t = 0f; t < seconds; t += BattleTime.FxDt)
            {
                dim.color = new Color(color.r, color.g, color.b, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            dim.color = new Color(color.r, color.g, color.b, to);
            if (to <= 0f) gameObject.SetActive(false);
        }

        /// <summary>
        /// 대형 컷인 (제로버스 2페이즈): 암전 → 겹줄 띠가 엇갈려 펼쳐짐 → 이명이 깨진 글자에서 한 글자씩 해독됨 →
        /// 이름이 크게 내리꽂히며 붉은·푸른 잔상이 모이고 띠가 흔들림 → 부제가 타자처럼 찍힘 → 번쩍이며 머묾 → 접힘.
        /// </summary>
        public IEnumerator PlayGrand(string headerText, string titleText, string subtitleText, Color color)
        {
            gameObject.SetActive(true);
            EnsureGrandParts();
            var bandRt = band.rectTransform;
            var titleRt = title.rectTransform;
            var oldBand = bandRt.sizeDelta;
            float oldTitleSize = title.fontSize, oldHeadSize = header.fontSize, oldSubSize = subtitle.fontSize;
            bandRt.sizeDelta = new Vector2(0f, GrandBandHeight);
            bandRt.anchoredPosition = Vector2.zero;
            title.fontSize = ghostA.fontSize = ghostB.fontSize = GrandTitleSize;
            header.fontSize = GrandHeaderSize;
            subtitle.fontSize = GrandSubSize;
            header.rectTransform.anchoredPosition = new Vector2(0f, GrandHeaderY);
            titleRt.anchoredPosition = new Vector2(0f, GrandTitleY);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, GrandSubY);
            header.text = subtitle.text = "";
            title.text = ghostA.text = ghostB.text = titleText;
            SetText(0f);
            ghostA.color = ghostB.color = Color.clear;

            var deep = new Color(color.r * 0.22f, color.g * 0.1f, color.b * 0.3f, 0.96f);
            var edge = Color.Lerp(color, Color.white, 0.25f);
            var crimson = new Color(0.95f, 0.18f, 0.3f, 0.9f);
            var stripeTopRt = stripeTop.rectTransform;
            var stripeBottomRt = stripeBottom.rectTransform;
            float stripeY = GrandBandHeight / 2f + StripeGap;
            float width = ((RectTransform)transform).rect.width + 20f;

            // ① 암전
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt)
            {
                dim.color = new Color(0f, 0f, 0f, 0.88f * t / 0.3f);
                yield return null;
            }
            dim.color = new Color(0f, 0f, 0f, 0.88f);

            // ② 띠가 위아래로 터지듯 펼쳐지고, 얇은 줄 두 개가 양쪽에서 엇갈려 들어온다
            band.color = deep;
            edgeTop.color = edgeBottom.color = edge;
            stripeTop.color = crimson;
            stripeBottom.color = new Color(color.r, color.g, color.b, 0.9f);
            for (float t = 0f; t < 0.32f; t += BattleTime.FxDt)
            {
                float k = Ease(t / 0.32f);
                bandRt.localScale = new Vector3(1f, k * (1f + 0.25f * Mathf.Sin(k * Mathf.PI)), 1f);
                stripeTopRt.anchoredPosition = new Vector2(-(1f - k) * width, stripeY);
                stripeBottomRt.anchoredPosition = new Vector2((1f - k) * width, -stripeY);
                yield return null;
            }
            bandRt.localScale = Vector3.one;
            stripeTopRt.anchoredPosition = new Vector2(0f, stripeY);
            stripeBottomRt.anchoredPosition = new Vector2(0f, -stripeY);

            // ③ 이명: 깨진 글자 → 한 글자씩 해독
            header.color = new Color(1f, 0.35f, 0.45f, 1f);
            int n = headerText?.Length ?? 0;
            float decode = 0.75f;
            for (float t = 0f; t < decode; t += BattleTime.FxDt)
            {
                int known = Mathf.FloorToInt(n * t / decode);
                header.text = Scramble(headerText, known);
                yield return null;
            }
            header.text = headerText ?? "";
            header.color = new Color(1f, 0.82f, 0.45f, 1f);                  // 해독되면 금빛

            // ④ 이름이 크게 내리꽂힌다 + 잔상이 모인다 + 띠가 흔들린다 + 하얀 번쩍임
            GameAudio.Se("hit_strong");
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                float k = Mathf.Clamp01(t / 0.18f);
                float scale = Mathf.Lerp(2.8f, 1f, Ease(k));
                titleRt.localScale = Vector3.one * scale;
                title.color = new Color(1f, 1f, 1f, k);
                float spread = (1f - Ease(t / 0.5f)) * 7f;
                ghostA.rectTransform.anchoredPosition = new Vector2(-spread, GrandTitleY + spread * 0.3f);
                ghostB.rectTransform.anchoredPosition = new Vector2(spread, GrandTitleY - spread * 0.3f);
                ghostA.rectTransform.localScale = ghostB.rectTransform.localScale = Vector3.one * scale;
                ghostA.color = new Color(1f, 0.15f, 0.3f, 0.75f * k);
                ghostB.color = new Color(0.3f, 0.6f, 1f, 0.75f * k);
                float shake = t > 0.18f ? (1f - (t - 0.18f) / 0.32f) * 4f : 0f;
                bandRt.anchoredPosition = new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));
                flash.color = new Color(1f, 1f, 1f, t > 0.15f ? Mathf.Clamp01(0.8f - (t - 0.15f) * 3f) : 0f);
                yield return null;
            }
            titleRt.localScale = Vector3.one;
            bandRt.anchoredPosition = Vector2.zero;
            flash.color = Color.clear;
            ghostA.color = ghostB.color = Color.clear;

            // ⑤ 부제가 한 글자씩 찍힌다
            subtitle.color = new Color(1f, 0.92f, 0.95f, 1f);
            int sn = subtitleText?.Length ?? 0;
            for (int i = 1; i <= sn; i++)
            {
                subtitle.text = subtitleText.Substring(0, i);
                GameAudio.Se("select", 0.4f);
                yield return BattleTime.FxWait(0.07f);
            }

            // ⑥ 머묾: 이름이 붉게 맥동하고, 가끔 글리치가 튄다 (넘기기 키를 누르고 있으면 짧게)
            float nextGlitch = 0.25f;
            for (float t = 0f; t < GrandHold; t += BattleTime.FxDt)
            {
                if (BattleTime.AdvanceHeld && t > HeldHold) break;
                float pulse = (Mathf.Sin(t * 9f) + 1f) * 0.5f;
                title.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.55f), pulse * 0.6f);
                if (t >= nextGlitch)
                {
                    nextGlitch = t + Random.Range(0.25f, 0.5f);
                    float g = Random.Range(2f, 4f) * (Random.value < 0.5f ? -1f : 1f);
                    ghostA.rectTransform.anchoredPosition = new Vector2(g, GrandTitleY);
                    ghostA.color = new Color(1f, 0.15f, 0.3f, 0.6f);
                    subtitle.text = Random.value < 0.5f ? Scramble(subtitleText, 3) : subtitleText;
                }
                else if (t > nextGlitch - 0.18f)
                {
                    ghostA.color = Color.clear;
                    subtitle.text = subtitleText ?? "";
                }
                yield return null;
            }
            ghostA.color = Color.clear;
            subtitle.text = subtitleText ?? "";

            // ⑦ 접힘: 띠가 가늘게 찌그러지며 사라지고 줄이 양쪽으로 빠진다
            for (float t = 0f; t < 0.28f; t += BattleTime.FxDt)
            {
                float k = t / 0.28f;
                bandRt.localScale = new Vector3(1f + 0.2f * k, 1f - Ease(k), 1f);
                stripeTopRt.anchoredPosition = new Vector2(Ease(k) * width, stripeY);
                stripeBottomRt.anchoredPosition = new Vector2(-Ease(k) * width, -stripeY);
                dim.color = new Color(0f, 0f, 0f, 0.88f * (1f - k));
                SetText(1f - k);
                header.color = new Color(1f, 0.82f, 0.45f, 1f - k);
                yield return null;
            }

            // 다음 보통 컷인을 위해 원래대로
            bandRt.localScale = Vector3.one;
            bandRt.sizeDelta = oldBand;
            title.fontSize = oldTitleSize;
            header.fontSize = oldHeadSize;
            subtitle.fontSize = oldSubSize;
            stripeTop.color = stripeBottom.color = Color.clear;
            ghostA.text = ghostB.text = "";
            gameObject.SetActive(false);
        }

        /// <summary>앞 known글자만 진짜, 나머지는 깨진 글자 (공백은 그대로)</summary>
        static string Scramble(string text, int known)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var chars = text.ToCharArray();
            for (int i = Mathf.Max(0, known); i < chars.Length; i++)
                if (chars[i] != ' ') chars[i] = GlitchChars[Random.Range(0, GlitchChars.Length)];
            return new string(chars);
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
