using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 타이틀 화면 (TitleScene). 포켓로그 타이틀처럼:
    ///  ① 검은 화면에 "로딩 중..." 상자 (로고 그림을 불러오는 동안)
    ///  ② 세로 줄무늬가 벌어지며 초원 배경이 드러난다
    ///  ③ 오른쪽 아래 메뉴(새 게임 / 불러오기)가 나오고, 로고가 위에서 내려오며 나타난다
    /// 새 게임 → StarterScene (팰 선택 → 저장 슬롯), 불러오기 → StarterScene (저장 슬롯만).
    /// 화면은 코드로 만든다. 씬에는 이 컴포넌트와 폰트만 있으면 된다 (메뉴 PalRogue/35가 만든다).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Tooltip("글꼴 (전투 화면과 같은 한글 픽셀 글꼴)")]
        public TMP_FontAsset font;
        [Tooltip("Resources 안의 로고 그림 경로")]
        public string logoPath = "UI/Logo";
        [Tooltip("플레이한 적이 없을 때의 타이틀 배경 (Resources/Arenas 의 배경 이름). 플레이한 적이 있으면 마지막으로 플레이한 지역 배경이 나온다")]
        public string backgroundBiome = "plains";

        const int Stripes = 16;
        const float FieldH = 132f;          // 위쪽 그림 영역 (아래 48픽셀은 메시지 판)
        const float LogoW = 236f;
        const float MenuX = 244f, MenuY = 128f, MenuW = 72f, RowH = 17f;

        enum Item { Continue, NewGame, Load }
        readonly System.Collections.Generic.List<Item> items = new System.Collections.Generic.List<Item>();
        static string Label(Item i) => i switch { Item.Continue => "계속하기", Item.NewGame => "새 게임", _ => "불러오기" };
        int continueSlot = -1;

        RectTransform root;
        Image background;
        Image[] stripes;
        RectTransform loadingBox;
        TMP_Text loadingText, messageText;
        RawImage logo;
        RectTransform logoRt, menu;
        TMP_Text[] itemTexts;
        Image cursor;
        int index;
        bool ready, leaving;
        float messageUntil;

        void Start()
        {
            Time.timeScale = 1f;
            GameSession.Clear();
            Build();
            StartCoroutine(Intro());
        }

        void Build()
        {
            root = UiKit.Canvas(gameObject, 100, Color.black);

            background = UiKit.Box(root, "Field", 0, 0, 320, 180).gameObject.AddComponent<Image>();
            // 마지막으로 플레이한 지역의 배경 (예: 설원에서 끄면 설원)
            string biome = PlayerSave.LastBiome;
            if (string.IsNullOrEmpty(biome) || AreaScenery.Load(biome + "_bg") == null) biome = backgroundBiome;
            background.sprite = AreaScenery.Load(biome + "_bg");
            background.color = background.sprite != null ? Color.white : UiKit.Hex(0x78c850);
            background.raycastTarget = false;

            // 배경을 가리는 검은 세로 줄 (점점 가늘어지며 사라진다)
            stripes = new Image[Stripes];
            float w = 320f / Stripes;
            for (int i = 0; i < Stripes; i++)
            {
                var s = UiKit.Box(root, "Stripe" + i, i * w, 0, w, FieldH, Color.black);
                stripes[i] = s.GetComponent<Image>();
            }

            // 아래 메시지 판 (전투 화면과 같은 자리)
            UiKit.Box(root, "PanelLine", 0, FieldH, 320, 2, UiKit.FrameRed);
            UiKit.Box(root, "Panel", 0, FieldH + 2, 320, 180 - FieldH - 2, UiKit.PanelFill);
            messageText = UiKit.Label(root, font, "Message", 10, FieldH + 8, 220, 30, 10, TextAlignmentOptions.TopLeft, UiKit.White);

            // 로딩 상자
            loadingBox = UiKit.Frame(root, "Loading", 124, 54, 72, 22, UiKit.FrameRed, UiKit.FrameFill, out var loadingInner);
            loadingText = UiKit.Label(loadingInner, font, "Text", 0, 0, 68, 18, 10, TextAlignmentOptions.Center, UiKit.White);
            loadingText.text = "로딩 중...";

            // 로고
            logoRt = UiKit.Box(root, "Logo", (320 - LogoW) / 2f, 4, LogoW, LogoW * 745f / 2112f);
            logo = logoRt.gameObject.AddComponent<RawImage>();
            logo.raycastTarget = false;
            logo.color = new Color(1, 1, 1, 0);

            // 메뉴: 마지막으로 플레이한 모험이 남아 있으면 맨 위에 "계속하기"
            int last = PlayerSave.LastSlot;
            if (last >= 0 && SaveSlots.Exists(last)) { continueSlot = last; items.Add(Item.Continue); }
            items.Add(Item.NewGame);
            items.Add(Item.Load);
            float menuH = 10 + RowH * items.Count;
            menu = UiKit.Frame(root, "Menu", MenuX, 176 - menuH, MenuW, menuH, UiKit.FrameRed, UiKit.FrameFill, out var menuInner);
            itemTexts = new TMP_Text[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                int row = i;
                var t = UiKit.Label(menuInner, font, "Item" + i, 12, 3 + i * RowH, MenuW - 16, RowH, 11, TextAlignmentOptions.MidlineLeft, UiKit.White);
                t.text = Label(items[i]);
                UiKit.Clickable(t.gameObject, () => { if (ready) { index = row; Refresh(); } }, () => { if (ready) { index = row; Choose(); } });
                itemTexts[i] = t;
            }
            cursor = UiKit.Cursor(menuInner);
            menu.gameObject.SetActive(false);
        }

        IEnumerator Intro()
        {
            // ① 로딩: 로고 그림을 불러온다 (최소 0.8초는 보여 준다)
            var request = Resources.LoadAsync<Texture2D>(logoPath);
            float t0 = Time.unscaledTime;
            while (!request.isDone || Time.unscaledTime - t0 < 0.8f)
            {
                int dots = 1 + (int)((Time.unscaledTime - t0) * 3f) % 3;
                loadingText.text = "로딩 중" + new string('.', dots);
                yield return null;
            }
            var tex = request.asset as Texture2D;
            if (tex != null)
            {
                tex.filterMode = FilterMode.Bilinear;     // 큰 그림을 작게 줄여 그리므로 부드럽게
                logo.texture = tex;
            }

            // ② 세로 줄무늬가 벌어지며 배경이 드러난다
            const float reveal = 0.8f;
            for (float t = 0; t < reveal; t += Time.unscaledDeltaTime)
            {
                float k = Smooth(t / reveal);
                SetStripes(1f - k);
                if (k > 0.55f) loadingBox.gameObject.SetActive(false);
                yield return null;
            }
            SetStripes(0f);
            loadingBox.gameObject.SetActive(false);

            // ③ 메뉴 → 로고
            yield return new WaitForSecondsRealtime(0.15f);
            menu.gameObject.SetActive(true);
            ready = true;
            index = 0;
            Refresh();
            const float logoIn = 0.5f;
            Vector2 home = logoRt.anchoredPosition;
            for (float t = 0; t < logoIn; t += Time.unscaledDeltaTime)
            {
                float k = Smooth(t / logoIn);
                logo.color = new Color(1, 1, 1, k);
                logoRt.anchoredPosition = home + new Vector2(0, 10f * (1f - k));
                yield return null;
            }
            logo.color = Color.white;
            logoRt.anchoredPosition = home;
        }

        void SetStripes(float cover)
        {
            float w = 320f / Stripes;
            for (int i = 0; i < stripes.Length; i++)
            {
                var rt = stripes[i].rectTransform;
                float sw = Mathf.Round(w * cover);
                rt.sizeDelta = new Vector2(sw, FieldH);
                rt.anchoredPosition = new Vector2(i * w + (w - sw) / 2f, 0);   // 줄 가운데를 기준으로 가늘어진다
                stripes[i].enabled = sw > 0f;
            }
        }

        void Update()
        {
            if (messageUntil > 0 && Time.unscaledTime > messageUntil) { messageUntil = 0; messageText.text = ""; }
            if (!ready || leaving) return;
            int v = MenuKeys.Vertical;
            if (v != 0) { index = (index + v + items.Count) % items.Count; Refresh(); }
            if (MenuKeys.Confirm) Choose();
        }

        void Refresh()
        {
            bool hasSave = SaveSlots.AnyExists;
            for (int i = 0; i < itemTexts.Length; i++)
                itemTexts[i].color = items[i] == Item.Load && !hasSave ? UiKit.Soft : UiKit.White;
            cursor.rectTransform.anchoredPosition = new Vector2(4, -(3 + index * RowH + (RowH - 9) / 2f));
        }

        void Choose()
        {
            if (leaving) return;
            var item = items[index];
            if (item == Item.Continue)
            {
                var save = SaveSlots.Load(continueSlot);
                if (save == null || !GameSession.CanLoad(GameSession.GameScene))
                {
                    messageText.text = "마지막 모험을 불러오지 못했어요.";
                    messageUntil = Time.unscaledTime + 2.5f;
                    return;
                }
                leaving = true;
                GameSession.Loaded = save;            // 마지막으로 플레이한 곳으로 바로
                GameSession.NewParty = null;
                GameSession.Slot = continueSlot;
                GameSession.Go(GameSession.GameScene, font);
                return;
            }
            if (item == Item.Load && !SaveSlots.AnyExists)
            {
                messageText.text = "불러올 모험이 없어요.\n새 게임으로 시작해 주세요!";
                messageUntil = Time.unscaledTime + 2.5f;
                return;
            }
            if (!GameSession.CanLoad(GameSession.StarterScene))
            {
                messageText.text = "StarterScene이 빌드 설정에 없어요.\n(메뉴 PalRogue/35를 실행해 주세요)";
                messageUntil = Time.unscaledTime + 4f;
                return;
            }
            leaving = true;
            GameSession.OpenForLoad = item == Item.Load;
            GameSession.Go(GameSession.StarterScene, font);
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }
}
