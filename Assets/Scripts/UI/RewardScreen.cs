using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 포켓로그식 웨이브 보상 화면 (화면 크기 320×180 기준 좌표).
    ///  ┌ 위 띠: 상점 (돈으로 사는 회복 아이템, 이름 + 가격)
    ///  ├ 가운데: 보상 아이템 3~8개 (이름 색 = 등급 색), 5개부터는 두 줄
    ///  ├ 아래: "갱신 ○○G" (돈을 내고 보상 다시 뽑기) / "파티 확인"
    ///  └ 메시지창 위 이름표 + 메시지창에 설명
    /// 씬에 미리 만들 필요 없이 BattleScreen이 처음 쓸 때 코드로 만든다 (Create).
    /// 입력: 마우스 / 방향키 + Enter·Space·Z / Esc(보상 건너뛰기)
    /// </summary>
    public class RewardScreen : MonoBehaviour
    {
        public enum Pick { None, Reward, Shop, Reroll, Party, Skip }

        /// <summary>마지막으로 고른 것의 종류와 번호 (보상·상점 칸 번호)</summary>
        public Pick ResultKind { get; private set; }
        public int ResultIndex { get; private set; }

        // ───── 배치 (320×180 기준) ─────
        const float FieldBottom = 134f;            // 이 위쪽(전투 화면)을 어둡게 덮는다. 아래는 메시지창
        const float BandHeight = 49f;              // 위쪽 상점 띠
        const float ShopIconY = 12f, ShopNameY = 23f, ShopPriceY = 30f, ShopIconSize = 16f;
        const float RewardIconSize = 32f;          // 아이템 그림 칸 (포켓로그 그림은 32×32 안에 16~20px 정도로 그려져 있다)
        const float SphereIconSize = 18f;          // 스피어 보상은 다른 아이템 그림과 비슷한 크기로 줄인다
        const float BallSize = 14f;                // 떨어지는 팰 스피어 크기
        const float OneRowY = 68f;                 // 보상이 한 줄일 때 아이콘 중심
        const float TwoRowY1 = 62f, TwoRowY2 = 96f;// 두 줄일 때
        const float RewardNameOffset = 16f;        // 아이콘 중심 → 이름 중심
        const float BottomRowY = 113f;             // 갱신 / 파티 확인
        float CenterX => root.rect.width > 0f ? root.rect.width / 2f : 160f;   // 화면 가운데 (기준 320 → 160)
        const float FontSmall = 7f, FontName = 9f, FontMain = 11f;

        static readonly Color DimColor = new Color(0f, 0f, 0f, 0.9f);   // 뒤 전투 화면이 거의 안 보이게
        const int SortingOrder = 50;               // 다른 전투 UI·팰 그림보다 위에 그린다
        const int HudSortingOrder = 60;            // 웨이브·돈 글자는 보상 화면보다 위
        static readonly Color BandColor = new Color(0.07f, 0.08f, 0.19f, 0.95f);
        static readonly Color White = new Color(0.97f, 0.97f, 0.97f);
        static readonly Color PriceColor = new Color(0.97f, 0.85f, 0.42f);
        static readonly Color TooExpensive = new Color(0.97f, 0.36f, 0.36f);
        static readonly Color TagColor = new Color(0.22f, 0.18f, 0.27f, 1f);
        static readonly Color TagOutline = new Color(0.85f, 0.25f, 0.22f, 1f);

        TMP_FontAsset font;
        TMP_Text description;                       // 전투 화면 메시지창 글자 (설명을 여기에 쓴다)
        RectTransform root, shopRoot, rewardRoot;
        Image cursor;
        Button rerollButton, partyButton;
        TMP_Text rerollText;
        RectTransform nameTag;
        Image dim;
        readonly List<RectTransform> rewardIcons = new List<RectTransform>();
        readonly List<GameObject> rewardLabels = new List<GameObject>();
        bool revealing;
        RawImage tagIcon;
        TMP_Text tagName;

        readonly List<Button> shopButtons = new List<Button>();
        readonly List<Button> rewardButtons = new List<Button>();
        IReadOnlyList<ItemDef> rewards;
        IReadOnlyList<(ItemDef item, int price)> shop;
        bool waiting;
        GameObject lastSelected;

        // ═════════════════ 만들기 ═════════════════

        public static RewardScreen Create(Transform parent, TMP_FontAsset font, Sprite cursorSprite, TMP_Text description)
        {
            var go = new GameObject("RewardScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var screen = go.AddComponent<RewardScreen>();
            screen.font = font;
            screen.description = description;
            screen.Build(cursorSprite);
            go.SetActive(false);
            return screen;
        }

        void Build(Sprite cursorSprite)
        {
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            // 전투 화면을 어둡게, 위쪽은 상점 띠
            dim = TopStretch("Dim", root, 0f, FieldBottom).gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0f);
            var band = TopStretch("ShopBand", root, 0f, BandHeight).gameObject.AddComponent<Image>();
            band.color = BandColor;

            shopRoot = Box("Shop", root, 0, 0, 320, BandHeight);
            rewardRoot = Box("Rewards", root, 0, 0, 320, FieldBottom);

            rerollButton = TextButton("Reroll", 10, BottomRowY, 90, TextAlignmentOptions.MidlineLeft, out rerollText);
            partyButton = TextButton("Party", 320 - 10 - 60, BottomRowY, 60, TextAlignmentOptions.MidlineRight, out var partyText);
            partyText.text = "파티 확인";
            Hook(rerollButton, () => Choose(Pick.Reroll, 0), () => Highlight(Pick.Reroll, 0));
            Hook(partyButton, () => Choose(Pick.Party, 0), () => Highlight(Pick.Party, 0));

            // 메시지창 위에 걸치는 이름표 (아이콘 + 이름)
            nameTag = Box("NameTag", root, 14, 124, 118, 13);
            var tagBg = nameTag.gameObject.AddComponent<Image>();
            tagBg.color = TagColor;
            var outline = nameTag.gameObject.AddComponent<Outline>();
            outline.effectColor = TagOutline;
            outline.effectDistance = new Vector2(1f, -1f);
            tagIcon = Box("Icon", nameTag, 3, 0.5f, 12, 12).gameObject.AddComponent<RawImage>();
            tagIcon.raycastTarget = false;
            tagName = Label(Box("Name", nameTag, 18, 0, 98, 13), FontName, TextAlignmentOptions.MidlineLeft, White);

            cursor = Box("Cursor", root, 0, 0, 8, 8).gameObject.AddComponent<Image>();
            cursor.raycastTarget = false;
            if (cursorSprite != null)
            {
                cursor.sprite = cursorSprite;
                cursor.SetNativeSize();
            }
        }

        // ═════════════════ 열기 / 고르기 ═════════════════

        /// <summary>보상·상점 목록과 갱신 비용, 가진 돈으로 화면을 채우고 보여준다</summary>
        /// <param name="forReveal">true면 아이템·상점·버튼을 숨겨 두고, 이어서 PlayReveal로 등장 연출을 한다</param>
        public void Open(IReadOnlyList<ItemDef> rewardItems, IReadOnlyList<(ItemDef item, int price)> shopItems, int rerollCost, int money,
                         bool forReveal = false)
        {
            rewards = rewardItems;
            shop = shopItems;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            EnsureOwnCanvas(gameObject, SortingOrder, true);

            BuildShop(money);
            BuildRewards();
            rerollText.text = $"갱신  <color=#{ColorUtility.ToHtmlStringRGB(money >= rerollCost ? PriceColor : TooExpensive)}>{rerollCost:N0}G</color>";
            SetupNavigation();

            // "파티 확인"은 화면 오른쪽 끝에 붙인다 (화면 비율이 달라도)
            var partyRt = (RectTransform)partyButton.transform;
            partyRt.anchoredPosition = new Vector2(root.rect.width - 10f - partyRt.sizeDelta.x, partyRt.anchoredPosition.y);

            if (forReveal) SetContentVisible(false);
            else
            {
                SetContentVisible(true);
                dim.color = DimColor;
            }
        }

        /// <summary>보상 화면을 완전히 닫는다 (다음에 열 때 다시 어두워지는 연출부터)</summary>
        public void Close()
        {
            dim.color = new Color(0f, 0f, 0f, 0f);
            Hide();
        }

        /// <summary>
        /// 이 오브젝트에 자기 Canvas를 붙여 정해진 순서로 그린다 (부모 캔버스의 다른 UI·팰 그림보다 위).
        /// 버튼 클릭을 받으려면 GraphicRaycaster도 필요하다.
        /// </summary>
        public static void EnsureOwnCanvas(GameObject go, int order, bool clickable)
        {
            var canvas = go.GetComponent<Canvas>();
            if (canvas == null) canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
            if (clickable && go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        }

        TMP_Text hudArea, hudMoney;

        /// <summary>
        /// 웨이브·돈 글자를 보상 화면 위에도 보이게 한다. 원래 글자를 건드리지 않고 같은 모양의 복사본을 보상 화면 안에 둔다.
        /// (원래 글자에 Canvas를 붙이는 방식은 글자 위치가 어긋나서 쓰지 않는다)
        /// </summary>
        public void MirrorHud(TMP_Text area, TMP_Text money)
        {
            hudArea = Mirror(hudArea, area);
            hudMoney = Mirror(hudMoney, money);
        }

        TMP_Text Mirror(TMP_Text copy, TMP_Text source)
        {
            if (source == null) return copy;
            if (copy == null)
            {
                var go = Instantiate(source.gameObject, transform, false);
                var extraCanvas = go.GetComponent<Canvas>();
                if (extraCanvas != null) Destroy(extraCanvas);
                copy = go.GetComponent<TMP_Text>();
                var src = source.rectTransform;
                var dst = copy.rectTransform;
                dst.anchorMin = src.anchorMin; dst.anchorMax = src.anchorMax; dst.pivot = src.pivot;
                dst.anchoredPosition = src.anchoredPosition; dst.sizeDelta = src.sizeDelta;
            }
            copy.text = source.text;
            copy.transform.SetAsLastSibling();
            return copy;
        }

        /// <summary>(예전 방식) 웨이브·돈 글자를 보상 화면 위에 보이게 한다 — 쓰지 않음</summary>
        public static void RaiseHud(Component text)
        {
            if (text != null && text.gameObject.activeInHierarchy) EnsureOwnCanvas(text.gameObject, HudSortingOrder, false);
        }

        void SetContentVisible(bool on)
        {
            shopRoot.gameObject.SetActive(on);
            foreach (var icon in rewardIcons) icon.gameObject.SetActive(on);
            foreach (var label in rewardLabels) label.SetActive(on);
            rerollButton.gameObject.SetActive(on);
            partyButton.gameObject.SetActive(on);
            nameTag.gameObject.SetActive(on);
            cursor.gameObject.SetActive(on);
            if (!on) SetMessage("");
        }

        // ═════════════════ 등장 연출 ═════════════════
        const float DimFadeSeconds = 0.25f;
        const float BallDropSeconds = 0.32f, BallStagger = 0.1f, BallDropFrom = -14f;
        const float UpgradeStepSeconds = 0.32f, FlashSeconds = 0.2f;
        const float OpenPopSeconds = 0.18f;

        static Texture2D flashTexture;

        /// <summary>포켓로그처럼: 어두워짐 → 스피어가 떨어짐 → 등급만큼 번쩍이며 업그레이드 → 열리며 아이템 등장</summary>
        public IEnumerator PlayReveal()
        {
            revealing = true;

            // ① 화면 어둡게 (이미 어두우면 건너뜀)
            float startAlpha = dim.color.a;
            for (float t = 0f; t < DimFadeSeconds && startAlpha < DimColor.a; t += Time.unscaledDeltaTime)
            {
                dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(startAlpha, DimColor.a, t / DimFadeSeconds));
                yield return null;
            }
            dim.color = DimColor;

            // ② 팰 스피어(일반)가 위에서 하나씩 떨어진다
            var balls = new List<Image>();
            var flashes = new List<RawImage>();
            for (int i = 0; i < rewards.Count; i++)
            {
                var center = IconCenter(i);
                var ball = Box($"Ball{i}", rewardRoot, center.x - BallSize / 2f, BallDropFrom, BallSize, BallSize).gameObject.AddComponent<Image>();
                ball.sprite = PalSpheres.LoadSprite(SphereType.Pal);
                ball.preserveAspect = true;
                ball.raycastTarget = false;
                ball.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                balls.Add(ball);

                var flashRt = Box("Flash", ball.transform, 0f, 0f, BallSize + 6f, BallSize + 6f);
                flashRt.anchorMin = flashRt.anchorMax = flashRt.pivot = new Vector2(0.5f, 0.5f);
                flashRt.anchoredPosition = Vector2.zero;
                var flash = flashRt.gameObject.AddComponent<RawImage>();
                flash.texture = FlashTexture();
                flash.color = new Color(1f, 1f, 1f, 0f);
                flash.raycastTarget = false;
                flashes.Add(flash);
            }

            float dropTotal = BallDropSeconds + BallStagger * Mathf.Max(0, rewards.Count - 1) + 0.12f;
            for (float t = 0f; t < dropTotal; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < balls.Count; i++) PlaceBall(balls[i], i, t - i * BallStagger);
                yield return null;
            }
            for (int i = 0; i < balls.Count; i++) PlaceBall(balls[i], i, 99f);
            yield return Wait(0.15f);

            // ③ 높은 등급은 한 단계씩 번쩍이며 상위 스피어로 바뀐다 (일반 → 기가 → 하이퍼 → 얼티밋 → 엑조틱)
            int maxTier = 0;
            foreach (var r in rewards) maxTier = Mathf.Max(maxTier, (int)r.Tier);
            for (int step = 1; step <= maxTier; step++)
            {
                for (int i = 0; i < balls.Count; i++)
                {
                    if ((int)rewards[i].Tier < step) continue;
                    balls[i].sprite = PalSpheres.LoadSprite((SphereType)step);
                    StartCoroutine(Flash(flashes[i], balls[i].rectTransform));
                }
                yield return Wait(UpgradeStepSeconds);
            }
            yield return Wait(0.12f);

            // ④ 스피어가 열리면서(번쩍) 아이템이 튀어나온다 + 이름·상점·버튼 표시
            for (int i = 0; i < balls.Count; i++)
            {
                StartCoroutine(Flash(flashes[i], null));
                balls[i].enabled = false;
                rewardIcons[i].gameObject.SetActive(true);
                rewardIcons[i].localScale = Vector3.one * 0.3f;
            }
            for (float t = 0f; t < OpenPopSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / OpenPopSeconds;
                float scale = k < 0.6f ? Mathf.Lerp(0.3f, 1.2f, k / 0.6f) : Mathf.Lerp(1.2f, 1f, (k - 0.6f) / 0.4f);
                foreach (var icon in rewardIcons) icon.localScale = Vector3.one * scale;
                yield return null;
            }
            foreach (var icon in rewardIcons) icon.localScale = Vector3.one;
            yield return Wait(0.12f);
            foreach (var b in balls) Destroy(b.gameObject);

            SetContentVisible(true);
            revealing = false;
        }

        /// <summary>떨어지는 스피어 위치 (t: 이 스피어가 떨어지기 시작한 뒤 지난 시간). 바닥에 닿으면 살짝 튄다</summary>
        void PlaceBall(Image ball, int index, float t)
        {
            var center = IconCenter(index);
            float y;                                                    // 공 중심 (위에서부터)
            if (t <= 0f) y = BallDropFrom;
            else if (t < BallDropSeconds)
            {
                float k = t / BallDropSeconds;
                y = Mathf.Lerp(BallDropFrom, center.y, k * k);          // 점점 빨라지며 떨어진다
            }
            else
            {
                float b = (t - BallDropSeconds) / 0.12f;                // 작게 한 번 튄다
                y = b < 1f ? center.y - Mathf.Sin(b * Mathf.PI) * 3f : center.y;
            }
            // 공의 pivot이 가운데라서, 왼쪽 위 앵커 기준 위치 = 중심
            ball.rectTransform.anchoredPosition = new Vector2(center.x, -y);
        }

        IEnumerator Flash(RawImage flash, RectTransform pulse)
        {
            for (float t = 0f; t < FlashSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / FlashSeconds;
                if (flash != null) flash.color = new Color(1f, 1f, 1f, 1f - k);
                if (pulse != null) pulse.localScale = Vector3.one * (1f + 0.35f * (1f - k));
                yield return null;
            }
            if (flash != null) flash.color = new Color(1f, 1f, 1f, 0f);
            if (pulse != null) pulse.localScale = Vector3.one;
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        /// <summary>번쩍임에 쓰는 하얀 원 (코드로 만든다)</summary>
        static Texture2D FlashTexture()
        {
            if (flashTexture != null) return flashTexture;
            const int size = 16;
            flashTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    flashTexture.SetPixel(x, y, new Color(1f, 1f, 1f, d <= c ? 1f : 0f));
                }
            flashTexture.Apply();
            return flashTexture;
        }

        /// <summary>보상 i번 아이콘 중심 (rewardRoot 왼쪽 위 기준, y는 아래로)</summary>
        Vector2 IconCenter(int i)
        {
            int n = rewards.Count;
            int firstRow = n <= 4 ? n : Mathf.CeilToInt(n / 2f);
            bool secondRow = i >= firstRow;
            int rowCount = secondRow ? n - firstRow : firstRow;
            int col = secondRow ? i - firstRow : i;
            float step = RewardStep(rowCount);
            float cx = CenterX + (col - (rowCount - 1) / 2f) * step;
            float cy = n <= 4 ? OneRowY : (secondRow ? TwoRowY2 : TwoRowY1);
            return new Vector2(cx, cy);
        }

        static float RewardStep(int rowCount) => rowCount <= 3 ? 64f : 300f / Mathf.Max(4, rowCount);

        public void Hide()
        {
            gameObject.SetActive(false);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>무언가 고를 때까지 기다린다. 결과는 ResultKind / ResultIndex</summary>
        /// <param name="select">처음 커서를 둘 곳 (보상 칸 번호)</param>
        public IEnumerator Choose(int select = 0)
        {
            while (revealing) yield return null;
            ResultKind = Pick.None;
            waiting = true;
            var first = rewardButtons.Count > 0 ? rewardButtons[Mathf.Clamp(select, 0, rewardButtons.Count - 1)] : rerollButton;
            Select(first);
            while (ResultKind == Pick.None) yield return null;
            waiting = false;
        }

        /// <summary>설명 칸(메시지창)에 잠깐 다른 글을 띄운다 (돈이 부족하다! 등)</summary>
        public void SetMessage(string text)
        {
            if (description == null) return;
            description.text = text;
            description.maxVisibleCharacters = 9999;
        }

        void Update()
        {
            if (!waiting) return;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null && lastSelected != null && lastSelected.activeInHierarchy)
                es.SetSelectedGameObject(lastSelected);
        }

        // ═════════════════ 칸 만들기 ═════════════════

        void BuildShop(int money)
        {
            ClearChildren(shopRoot);
            shopButtons.Clear();
            int n = shop.Count;
            float step = n <= 3 ? 64f : Mathf.Min(64f, 300f / n);
            for (int i = 0; i < n; i++)
            {
                var (item, price) = shop[i];
                float cx = CenterX + (i - (n - 1) / 2f) * step;
                var cell = Box($"Shop{i}", shopRoot, cx - step / 2f, 2f, step, BandHeight - 4f);
                var button = MakeButton(cell);

                var icon = Box("Icon", cell, step / 2f - ShopIconSize / 2f, ShopIconY - 2f - ShopIconSize / 2f, ShopIconSize, ShopIconSize)
                    .gameObject.AddComponent<RawImage>();
                SetIcon(icon, item, ShopIconSize, ShopIconSize);
                Label(Box("Name", cell, 0, ShopNameY - 2f - 4f, step, 8), FontSmall, TextAlignmentOptions.Center, White).text = item.Name;
                Label(Box("Price", cell, 0, ShopPriceY - 2f - 4f, step, 8), FontSmall, TextAlignmentOptions.Center,
                      money >= price ? PriceColor : TooExpensive).text = $"{price:N0}G";

                int index = i;
                Hook(button, () => Choose(Pick.Shop, index), () => Highlight(Pick.Shop, index));
                shopButtons.Add(button);
            }
        }

        void BuildRewards()
        {
            ClearChildren(rewardRoot);
            rewardButtons.Clear();
            rewardIcons.Clear();
            rewardLabels.Clear();
            int n = rewards.Count;
            int firstRow = n <= 4 ? n : Mathf.CeilToInt(n / 2f);
            for (int i = 0; i < n; i++)
            {
                int rowCount = i >= firstRow ? n - firstRow : firstRow;
                float step = RewardStep(rowCount);
                var center = IconCenter(i);

                var item = rewards[i];
                var cell = Box($"Reward{i}", rewardRoot, center.x - step / 2f, center.y - RewardIconSize / 2f, step, RewardIconSize + 12f);
                var button = MakeButton(cell);
                // 그림 칸은 가운데 기준 (등장할 때 커졌다 작아지는 연출, 스피어 그림은 작게)
                var iconRt = Box("Icon", cell, step / 2f, RewardIconSize / 2f, RewardIconSize, RewardIconSize);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                var icon = iconRt.gameObject.AddComponent<RawImage>();
                SetIcon(icon, item, RewardIconSize, SphereIconSize);
                rewardIcons.Add(iconRt);
                var label = Label(Box("Name", cell, -6f, RewardIconSize / 2f + RewardNameOffset - 5f, step + 12f, 10), FontName,
                                  TextAlignmentOptions.Center, ItemTiers.Color(item.Tier));
                label.text = item.Name;
                label.enableAutoSizing = true;                 // "기술머신: 다이아몬드 폴"처럼 긴 이름은 글자를 줄인다
                label.fontSizeMax = FontName;
                label.fontSizeMin = 6f;
                rewardLabels.Add(label.gameObject);

                int index = i;
                Hook(button, () => Choose(Pick.Reward, index), () => Highlight(Pick.Reward, index));
                rewardButtons.Add(button);
            }
        }

        /// <summary>아이콘 그림을 넣는다. 스피어(팰 스피어 그림)는 그림 전체가 공이라 sphereSize로 줄여 다른 아이템과 크기를 맞춘다</summary>
        static void SetIcon(RawImage image, ItemDef item, float itemSize, float sphereSize)
        {
            float size = item.Sphere.HasValue ? sphereSize : itemSize;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            var (texture, uv) = item.LoadIcon();
            image.texture = texture;
            image.uvRect = uv;
            image.enabled = texture != null;
            image.raycastTarget = false;
            if (texture != null) texture.filterMode = FilterMode.Point;
        }

        /// <summary>방향키 이동: 상점 줄 ↔ 보상 줄 ↔ 갱신·파티 확인 줄</summary>
        void SetupNavigation()
        {
            var rows = new List<List<Button>>();
            if (shopButtons.Count > 0) rows.Add(shopButtons);
            int n = rewardButtons.Count;
            int firstRow = n <= 4 ? n : Mathf.CeilToInt(n / 2f);
            if (n > 0) rows.Add(rewardButtons.GetRange(0, firstRow));
            if (n > firstRow) rows.Add(rewardButtons.GetRange(firstRow, n - firstRow));
            rows.Add(new List<Button> { rerollButton, partyButton });

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                for (int c = 0; c < row.Count; c++)
                {
                    var nav = new Navigation { mode = Navigation.Mode.Explicit };
                    nav.selectOnLeft = row[(c - 1 + row.Count) % row.Count];
                    nav.selectOnRight = row[(c + 1) % row.Count];
                    if (r > 0) nav.selectOnUp = Nearest(rows[r - 1], c, row.Count);
                    if (r < rows.Count - 1) nav.selectOnDown = Nearest(rows[r + 1], c, row.Count);
                    row[c].navigation = nav;
                }
            }
        }

        /// <summary>다른 줄로 옮길 때 비슷한 위치(비율)의 칸</summary>
        static Button Nearest(List<Button> row, int col, int fromCount)
        {
            if (row.Count == 0) return null;
            float t = fromCount <= 1 ? 0.5f : col / (float)(fromCount - 1);
            return row[Mathf.Clamp(Mathf.RoundToInt(t * (row.Count - 1)), 0, row.Count - 1)];
        }

        // ═════════════════ 선택 처리 ═════════════════

        void Choose(Pick kind, int index)
        {
            if (!waiting) return;
            ResultIndex = index;
            ResultKind = kind;
        }

        void Highlight(Pick kind, int index)
        {
            Button button = kind switch
            {
                Pick.Shop => shopButtons[index],
                Pick.Reward => rewardButtons[index],
                Pick.Reroll => rerollButton,
                _ => partyButton,
            };
            lastSelected = button.gameObject;

            // ▶ 커서: 아이콘 / 글자 왼쪽
            var rt = (RectTransform)button.transform;
            Vector2 pos;
            switch (kind)
            {
                case Pick.Shop:
                    pos = TopLeftOf(rt) + new Vector2(rt.rect.width / 2f - ShopIconSize / 2f - 7f, -(ShopIconY - 2f));
                    break;
                case Pick.Reward:
                    pos = TopLeftOf(rt) + new Vector2(rt.rect.width / 2f - RewardIconSize / 2f - 6f, -RewardIconSize / 2f);
                    break;
                case Pick.Reroll:
                    pos = TopLeftOf(rt) + new Vector2(-6f, -rt.rect.height / 2f);
                    break;
                default:
                    var label = button.GetComponentInChildren<TMP_Text>();
                    float textWidth = label != null ? label.GetPreferredValues(label.text).x : 40f;
                    pos = TopLeftOf(rt) + new Vector2(rt.rect.width - textWidth - 6f, -rt.rect.height / 2f);
                    break;
            }
            cursor.rectTransform.anchoredPosition = pos + new Vector2(-cursor.rectTransform.rect.width / 2f, cursor.rectTransform.rect.height / 2f);
            cursor.transform.SetAsLastSibling();

            // 이름표 + 설명
            ItemDef item = kind == Pick.Shop ? shop[index].item : kind == Pick.Reward ? rewards[index] : null;
            nameTag.gameObject.SetActive(item != null);
            if (item != null)
            {
                SetIcon(tagIcon, item, 12f, 9f);
                float s = tagIcon.rectTransform.sizeDelta.x;            // 작은 그림도 12×12 칸 가운데에
                tagIcon.rectTransform.anchoredPosition = new Vector2(3f + (12f - s) / 2f, -0.5f - (12f - s) / 2f);
                tagName.text = item.Name;
                tagName.color = ItemTiers.Color(item.Tier);
                SetMessage(item.Description);
            }
            else SetMessage(kind == Pick.Reroll ? "돈을 내고 보상 아이템을 다시 뽑는다." : "파티의 상태를 확인한다.");
        }

        /// <summary>root 기준 왼쪽 위 좌표 (anchoredPosition 계산용)</summary>
        Vector2 TopLeftOf(RectTransform rt)
        {
            var world = new Vector3[4];
            rt.GetWorldCorners(world);                                  // 0:왼아래 1:왼위 2:오른위 3:오른아래
            Vector2 local = root.InverseTransformPoint(world[1]);
            // root의 왼쪽 위를 (0,0)으로 바꾼다 (cursor의 앵커가 왼쪽 위)
            return local - new Vector2(root.rect.xMin, root.rect.yMax);
        }

        // ═════════════════ UI 도우미 ═════════════════

        void Hook(Button button, Action onClick, Action onHighlight)
        {
            button.onClick.AddListener(() => onClick());
            var entry = button.gameObject.AddComponent<MenuEntry>();
            entry.Selected += _ => onHighlight();
            entry.Cancelled += () => Choose(Pick.Skip, 0);
        }

        static Button MakeButton(RectTransform cell)
        {
            var hit = cell.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);                     // 투명하지만 클릭은 받는다
            var button = cell.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            return button;
        }

        Button TextButton(string name, float x, float y, float w, TextAlignmentOptions align, out TMP_Text text)
        {
            var cell = Box(name, root, x, y - 6f, w, 12f);
            var button = MakeButton(cell);
            text = Label(Box("Text", cell, 0, 0, w, 12f), FontMain, align, White);
            return button;
        }

        TMP_Text Label(RectTransform rt, float size, TextAlignmentOptions align, Color color)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        /// <summary>부모의 왼쪽 위 기준 (x, y)에 w×h 크기 칸 (y는 아래로 증가)</summary>
        static RectTransform Box(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>가로로 꽉 차는 띠 (위에서 y만큼 내려와 높이 h)</summary>
        static RectTransform TopStretch(string name, Transform parent, float y, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta = new Vector2(0f, h);
            return rt;
        }

        static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }

        static void Select(Button button)
        {
            var es = EventSystem.current;
            if (es == null || button == null) return;
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(button.gameObject);
        }
    }
}
