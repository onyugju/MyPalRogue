using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 전투 화면의 팰 정보창 하나(이름·레벨·HP바·속성 아이콘). 적용/내 팰 정보창 둘 다 이 컴포넌트를 쓴다.
    /// 참조 칸들은 메뉴 PalRogue/9 가 자동으로 연결해 준다.
    /// </summary>
    public class PalInfoBox : MonoBehaviour
    {
        public const float HpBarWidth = 48f;    // overlay_hp.png 가로
        public const float ExpBarWidth = 85f;   // overlay_exp.png 가로

        public TMP_Text nameText;
        public TMP_Text levelText;
        public TMP_Text hpText;        // 적 정보창은 비워둔다
        public RawImage hpFill;
        public RawImage expFill;       // 적 정보창은 비워둔다
        public Image typeIcon1;
        public Image typeIcon2;
        [Tooltip("상태이상 / 기절 뱃지 (Resources/statuses 이미지)")]
        public RawImage statusBadge;

        // statuses 이미지(1024x572)에서 뱃지가 그려진 부분만 (투명 여백 제외)
        static readonly Rect StatusUv = new Rect(221f / 1024f, 163f / 572f, 565f / 1024f, 242f / 572f);

        /// <summary>팰의 현재 상태를 화면에 반영한다</summary>
        public void Refresh(Pal pal)
        {
            ApplyBossLayout(pal);
            nameText.text = string.IsNullOrEmpty(pal.Data.palName) ? pal.Data.name : pal.Data.palName;
            StarIcons.Show(nameText, pal.Stars);                       // 응축 ★
            levelText.text = $"Lv.{pal.Level}";
            if (hpText != null) hpText.text = $"{pal.CurrentHP}/{pal.MaxHP}";
            SetHp(pal.MaxHP > 0 ? (float)pal.CurrentHP / pal.MaxHP : 0f);
            SetExp(pal.ExpRatio);

            if (pal.IsFainted) ShowFainted();
            else SetStatus(pal.Status);

            ShowStages(Battle.StageSnapshot(pal), Battle.IsCharging(pal));

            ShowTypes(pal.Data);
        }

        // ───────── 속성 표시 (화살표 모양 뱃지) ─────────
        // Resources/1.팰/2.Elements/{enemy_type | player_type}/
        //   single_type/Single_{속성}.png          : 속성 1개 — 상대는 오른쪽(>), 나는 왼쪽(<)을 가리키는 화살표
        //   double_type/{속성}/Double_{속성}_Type1 : "/" 모양 반쪽,  Type2 : "\\" 모양 반쪽
        // 위치·크기는 씬에서 Type1 칸을 놓아 둔 자리를 그대로 쓴다 (속성 1개일 때의 화살표 자리).
        // 속성 2개면 같은 너비로 반쪽 둘을 위아래로 붙여 같은 화살표 모양을 만든다 (위 = 주 속성, 아래 = 보조 속성).
        //   상대(>) : 위 Type2(\\) + 아래 Type1(/)      나(<) : 위 Type1(/) + 아래 Type2(\\)
        // 그림이 없는 속성은 예전 ui_type 아이콘을 쓴다.
        const string ElementDir = "1.팰/2.Elements";
        // 반쪽 그림(1659x948)은 위아래에 투명 여백이 있다. 그림이 실제로 그려진 줄(원본 픽셀)을 기준으로
        // 위 반쪽의 아랫변과 아래 반쪽의 윗변을 딱 맞붙여 틈 없는 화살표 하나로 만든다.
        //   Type1("/") : 그림 있는 줄 192 ~ 804      Type2("\\") : 145 ~ 812
        const float SrcW = 1659f, SrcH = 948f;
        const float T1Top = 192f, T1Bottom = 804f, T2Top = 145f, T2Bottom = 812f;
        const float JoinOverlap = 6f;              // 맞닿는 테두리를 살짝 겹쳐 이음매를 한 줄로 (원본 픽셀)

        bool badgeFrameSaved;
        Vector2 badgePos, badgeSize, type2Pos, type2Size;

        void SaveBadgeFrame()
        {
            if (badgeFrameSaved || typeIcon1 == null) return;
            badgeFrameSaved = true;
            badgePos = typeIcon1.rectTransform.anchoredPosition;
            badgeSize = typeIcon1.rectTransform.sizeDelta;
            if (typeIcon2 != null) { type2Pos = typeIcon2.rectTransform.anchoredPosition; type2Size = typeIcon2.rectTransform.sizeDelta; }
        }

        void ShowTypes(PalData data)
        {
            if (typeIcon1 == null) return;
            SaveBadgeFrame();
            bool player = expFill != null;
            string side = player ? "player_type" : "enemy_type";
            bool dual = data.hasSecondaryType && data.secondaryType != data.primaryType;
            var shift = new Vector2(bossMode ? BossExtraWidth : 0f, 0f);     // 보스 정보창은 가로로 길어진다

            Sprite top, bottom = null;
            if (!dual) top = Resources.Load<Sprite>($"{ElementDir}/{side}/single_type/Single_{data.primaryType}");
            else
            {
                string topPart = player ? "Type1" : "Type2", bottomPart = player ? "Type2" : "Type1";
                top = Resources.Load<Sprite>($"{ElementDir}/{side}/double_type/{data.primaryType}/Double_{data.primaryType}_{topPart}");
                bottom = Resources.Load<Sprite>($"{ElementDir}/{side}/double_type/{data.secondaryType}/Double_{data.secondaryType}_{bottomPart}");
            }

            if (top == null || (dual && bottom == null))
            {
                // 새 그림이 없으면 예전 동그란 아이콘 (원래 자리)
                Place(typeIcon1, Resources.Load<Sprite>($"{ElementDir}/ui_type/{data.primaryType}"), badgePos + shift, new Vector2(12f, 12f));
                if (typeIcon2 != null)
                {
                    typeIcon2.gameObject.SetActive(dual);
                    if (dual) Place(typeIcon2, Resources.Load<Sprite>($"{ElementDir}/ui_type/{data.secondaryType}"), type2Pos + shift, type2Size);
                }
                return;
            }

            if (!dual)
            {
                Place(typeIcon1, top, badgePos + shift, badgeSize);
                if (typeIcon2 != null) typeIcon2.gameObject.SetActive(false);
                return;
            }

            // 반쪽 둘: 화살표 자리와 같은 너비. 위 반쪽 아랫변에 아래 반쪽 윗변을 붙이고, 합친 모양의 세로 가운데를 화살표 자리 가운데에 맞춘다
            float w = badgeSize.x, k = w / SrcW, h = SrcH * k;
            float topVisTop = player ? T1Top : T2Top, topVisBottom = player ? T1Bottom : T2Bottom;
            float bottomVisTop = player ? T2Top : T1Top, bottomVisBottom = player ? T2Bottom : T1Bottom;
            float offset = topVisBottom - bottomVisTop - JoinOverlap;          // 아래 반쪽 그림이 위 반쪽 그림보다 내려가는 양 (원본 픽셀)
            float visCenter = (topVisTop + offset + bottomVisBottom) / 2f;      // 합친 모양의 가운데 (위 반쪽 그림 맨 위 기준)
            float centerY = -badgePos.y + badgeSize.y / 2f;                    // 화살표 자리 가운데 (정보창 위에서부터)
            float topY = centerY - visCenter * k;
            Place(typeIcon1, top, new Vector2(badgePos.x, -topY) + shift, new Vector2(w, h));
            if (typeIcon2 != null)
            {
                typeIcon2.gameObject.SetActive(true);
                Place(typeIcon2, bottom, new Vector2(badgePos.x, -(topY + offset * k)) + shift, new Vector2(w, h));
                typeIcon2.transform.SetSiblingIndex(typeIcon1.transform.GetSiblingIndex() + 1);
            }
        }

        static void Place(Image icon, Sprite sprite, Vector2 pos, Vector2 size)
        {
            if (icon == null) return;
            icon.gameObject.SetActive(true);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
            var rt = icon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        // ───────── C 키 능력치 보기 (포켓로그처럼: 기술을 고르는 중 C를 누르고 있으면 정보창 대신 능력치 단계) ─────────
        // 포켓로그의 능력치 창 그림(pbinfo_*_stats)을 정보창과 같은 자리·크기로 덮는다 (모양이 정보창과 똑같다).
        // 내 정보창은 아래 경험치 줄이 그대로 보인다. 단계 숫자는 포켓로그 그림(pbinfo_stat_numbers: +1~+6 빨강, -1~-6 파랑, 0은 -)
        static Texture2D statNumbers;
        static readonly Dictionary<string, Sprite> statBacks = new Dictionary<string, Sprite>();
        RectTransform peek;
        CanvasGroup peekGroup;
        Image peekBack;
        RawImage[] peekValues;
        float peekAlpha, peekTarget;
        // 상성 배율 칸 (상대 정보창 바로 오른쪽, 기술을 고르는 동안)
        static Sprite effWindow;
        RectTransform effBox;
        TMP_Text effText;

        const float PeekFont = 11f, NumW = 9f, NumH = 8f, ValueGap = 3f, ColumnGap = 14f;

        static Sprite StatBack(string name)
        {
            if (statBacks.TryGetValue(name, out var sp) && sp != null) return sp;
            var tex = Resources.Load<Texture2D>("UI/" + name);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Point;
            sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            statBacks[name] = sp;
            return sp;
        }

        /// <summary>
        /// on이면 정보창 위에 능력치 창을 덮어 보여 준다 (서서히 나타나고 사라진다). foe = 맞은편 팰 (행동 순서 비교용)
        ///  [공격 ±n / 방어 ±n]  [스피드 ±n / 급소 n%]  [선공·후공 / 실드·힘 모으기·각성]
        /// </summary>
        public void SetPeek(bool on, Pal pal, Pal foe = null)
        {
            if (on && pal != null && EnsurePeek())
            {
                SetStageNumber(peekValues[0], pal.GetStage(StatType.Attack));
                SetStageNumber(peekValues[1], pal.GetStage(StatType.Defense));
                SetStageNumber(peekValues[2], pal.GetStage(StatType.Speed));

                int crit = Mathf.RoundToInt(CritChance(pal) * 100f);
                SetText(peekCrit, crit + "%", crit > Mathf.RoundToInt(BattleCalc.CritChance * 100f) ? PeekGold : UiKit.White);

                // 행동 순서 (같은 우선도 기술끼리): 스피드가 높으면 먼저
                if (foe != null && !foe.IsFainted)
                {
                    int me = pal.GetStat(StatType.Speed), them = foe.GetStat(StatType.Speed);
                    if (me > them) SetText(peekTags[0], "선공", PeekGreen);
                    else if (me < them) SetText(peekTags[0], "후공", PeekGrey);
                    else SetText(peekTags[0], "동속", UiKit.White);
                }
                else SetText(peekTags[0], "", UiKit.White);

                // 특별한 상태 하나: 보스 실드 > 힘 모으기 > 각성
                var awaken = pal.Effects.OfType<Awakening>().FirstOrDefault();
                if (pal.IsBoss && pal.ShieldsLeft > 0) SetText(peekTags[1], $"실드 {pal.ShieldsLeft}", PeekGold);
                else if (Battle.IsCharging(pal)) SetText(peekTags[1], "힘 모으기", PeekRed);
                else if (awaken != null) SetText(peekTags[1], awaken.Active ? (awaken.IsTrue ? "진각성" : "각성 중") : "각성 대기", awaken.Active ? PeekGold : PeekGrey);
                else SetText(peekTags[1], "", UiKit.White);

                LayoutPeek();
                // 속성 화살표·상성 칸은 능력치 창 위에
                peek.SetAsLastSibling();
                if (typeIcon1 != null && typeIcon1.gameObject.activeSelf) typeIcon1.transform.SetAsLastSibling();
                if (typeIcon2 != null && typeIcon2.gameObject.activeSelf) typeIcon2.transform.SetAsLastSibling();
                if (effBox != null) effBox.SetAsLastSibling();
            }
            peekTarget = on && peek != null ? 1f : 0f;
        }

        static readonly Color PeekGold = new Color32(0xf8, 0xd0, 0x30, 0xff), PeekGreen = new Color32(0x78, 0xe0, 0x70, 0xff),
                              PeekGrey = new Color32(0x9a, 0x9a, 0xae, 0xff), PeekRed = new Color32(0xff, 0x5a, 0x4a, 0xff);

        /// <summary>급소율: 기본 1/16에 급소 아이템·강화(크리티컬커터 등)가 겹친다</summary>
        static float CritChance(Pal p)
        {
            float miss = 1f - BattleCalc.CritChance;
            foreach (var c in p.Effects.OfType<CritPerk>()) miss *= 1f - c.ExtraChance;
            return 1f - miss;
        }

        static void SetText((TMP_Text main, TMP_Text shadow) t, string text, Color color)
        {
            t.main.text = t.shadow.text = text;
            t.main.color = color;
        }

        // 이름 4개(공격·방어·스피드·급소) + 단계 숫자 3개 + 급소율 + 표시 2개
        (TMP_Text main, TMP_Text shadow)[] peekLabels;
        (TMP_Text main, TMP_Text shadow) peekCrit;
        (TMP_Text main, TMP_Text shadow)[] peekTags;

        bool EnsurePeek()
        {
            bool player = expFill != null;
            string backName = player ? "pbinfo_player_stats" : bossMode ? "pbinfo_enemy_boss_stats" : "pbinfo_enemy_mini_stats";
            var back = StatBack(backName);
            if (back == null) { Debug.LogWarning("[PalInfoBox] Resources/UI 에 능력치 창 그림이 없습니다. (메뉴 PalRogue/18 실행)"); return false; }
            if (statNumbers == null) { statNumbers = Resources.Load<Texture2D>("UI/pbinfo_stat_numbers"); if (statNumbers != null) statNumbers.filterMode = FilterMode.Point; }

            if (peek == null)
            {
                var font = nameText.font;
                peek = new GameObject("StatPeek", typeof(RectTransform)).GetComponent<RectTransform>();
                peek.SetParent(transform, false);
                peek.anchorMin = Vector2.zero; peek.anchorMax = Vector2.one;      // 정보창 그림과 같은 자리·크기
                peek.offsetMin = peek.offsetMax = Vector2.zero;
                peekBack = peek.gameObject.AddComponent<Image>();
                peekBack.raycastTarget = false;
                peekGroup = peek.gameObject.AddComponent<CanvasGroup>();
                peekGroup.blocksRaycasts = false;
                peekGroup.interactable = false;

                string[] names = { "공격", "방어", "스피드", "급소" };
                peekLabels = new (TMP_Text, TMP_Text)[names.Length];
                for (int i = 0; i < names.Length; i++) { peekLabels[i] = ShadowLabel(font, "Name" + i); SetText(peekLabels[i], names[i], Color.white); }
                peekValues = new RawImage[3];
                for (int i = 0; i < peekValues.Length; i++)
                {
                    var v = new GameObject("Value" + i, typeof(RectTransform)).AddComponent<RawImage>();
                    v.rectTransform.SetParent(peek, false);
                    v.rectTransform.anchorMin = v.rectTransform.anchorMax = new Vector2(0f, 1f);
                    v.rectTransform.pivot = new Vector2(0f, 0.5f);
                    v.rectTransform.sizeDelta = new Vector2(NumW, NumH);
                    v.texture = statNumbers;
                    v.raycastTarget = false;
                    peekValues[i] = v;
                }
                peekCrit = ShadowLabel(font, "Crit");
                peekTags = new[] { ShadowLabel(font, "Tag0"), ShadowLabel(font, "Tag1") };
                peekGroup.alpha = 0f;
                peek.gameObject.SetActive(false);
            }
            peekBack.sprite = back;
            return true;
        }

        (TMP_Text, TMP_Text) ShadowLabel(TMP_FontAsset font, string name)
        {
            var shadow = PeekLabel(font, name + "Shadow", new Color32(0x5a, 0x5a, 0x6a, 0xff));
            var main = PeekLabel(font, name, Color.white);
            return (main, shadow);
        }

        TMP_Text PeekLabel(TMP_FontAsset font, string name, Color color)
        {
            var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var rt = t.rectTransform;
            rt.SetParent(peek, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(40f, 12f);
            t.font = font;
            t.fontSize = expFill != null ? PeekFont : PeekFont - 1.5f;   // 상대 정보창은 키가 작아서 작게
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.color = color;
            return t;
        }

        static float W(TMP_Text t) => string.IsNullOrEmpty(t.text) ? 0f : t.GetPreferredValues(t.text).x;

        /// <summary>
        /// 세 칸 × 두 줄을 정보창 몸통(속성 화살표 쪽 제외) 가운데에 맞춘다.
        /// 몸통 범위는 그림 기준 픽셀 (상대 0~118 · 위아래 2~24 / 보스 0~166 / 나 6~130 · 위아래 4~37)
        /// </summary>
        void LayoutPeek()
        {
            bool player = expFill != null;
            float left = player ? 8f : 2f, right = player ? 126f : (bossMode ? 166f : 116f);
            float top = player ? 4f : 2f, bottom = player ? 37f : 24f;
            float mid = (top + bottom) / 2f, row = (bottom - top) / 4f;
            float y0 = mid - row, y1 = mid + row;

            float n1 = Mathf.Max(W(peekLabels[0].main), W(peekLabels[1].main));
            float n2 = Mathf.Max(W(peekLabels[2].main), W(peekLabels[3].main));
            float col1 = n1 + ValueGap + NumW;
            float col2 = n2 + ValueGap + Mathf.Max(NumW, W(peekCrit.main));
            float col3 = Mathf.Max(W(peekTags[0].main), W(peekTags[1].main));
            float avail = right - left, sum = col1 + col2 + col3;
            float gap = Mathf.Clamp((avail - sum) / 4f, 3f, ColumnGap);
            float x = Mathf.Round(left + (avail - sum - gap * (col3 > 0 ? 2f : 1f)) / 2f);

            PlaceLabel(peekLabels[0], x, y0); PlaceValue(peekValues[0], x + n1 + ValueGap, y0);
            PlaceLabel(peekLabels[1], x, y1); PlaceValue(peekValues[1], x + n1 + ValueGap, y1);
            x += col1 + gap;
            PlaceLabel(peekLabels[2], x, y0); PlaceValue(peekValues[2], x + n2 + ValueGap, y0);
            PlaceLabel(peekLabels[3], x, y1); PlaceLabel(peekCrit, x + n2 + ValueGap, y1);
            x += col2 + gap;
            PlaceLabel(peekTags[0], x, y0);
            PlaceLabel(peekTags[1], x, y1);
        }

        static void PlaceLabel((TMP_Text main, TMP_Text shadow) t, float x, float y)
        {
            t.main.rectTransform.anchoredPosition = new Vector2(x, -y);
            t.shadow.rectTransform.anchoredPosition = new Vector2(x + 0.7f, -y - 0.7f);
        }

        static void PlaceValue(RawImage v, float x, float y) => v.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));

        /// <summary>pbinfo_stat_numbers(118x8): 9칸씩 +1~+6, -1~-6, 마지막 10칸이 0(-)</summary>
        static void SetStageNumber(RawImage img, int stage)
        {
            if (statNumbers == null) { img.enabled = false; return; }
            img.enabled = true;
            int idx = stage > 0 ? Mathf.Min(stage, 6) - 1 : stage < 0 ? 5 + Mathf.Min(-stage, 6) : 12;
            float x = idx * 9f, w = idx == 12 ? 10f : 9f, tw = statNumbers.width, th = statNumbers.height;
            img.uvRect = new Rect(x / tw, 0f, w / tw, 1f);
            img.rectTransform.sizeDelta = new Vector2(w, th);
        }

        /// <summary>상대 정보창 바로 오른쪽(속성 화살표 다음) 가운데에 상성 배율 창. mult가 null이면 숨긴다 (변화 기술 등)</summary>
        public void SetEffectiveness(float? mult)
        {
            if (mult == null) { if (effBox != null) effBox.gameObject.SetActive(false); return; }
            if (effBox == null)
            {
                if (effWindow == null)
                {
                    var tex = Resources.Load<Texture2D>("UI/window_1_xthin");
                    if (tex != null)
                    {
                        tex.filterMode = FilterMode.Point;
                        effWindow = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(5, 5, 5, 5));
                    }
                }
                effBox = new GameObject("Effectiveness", typeof(RectTransform)).GetComponent<RectTransform>();
                effBox.SetParent(transform, false);
                effBox.anchorMin = effBox.anchorMax = new Vector2(0f, 1f);
                effBox.pivot = new Vector2(0f, 0.5f);
                effBox.sizeDelta = new Vector2(EffW, EffH);
                var b = effBox.gameObject.AddComponent<Image>();
                b.raycastTarget = false;
                if (effWindow != null) { b.sprite = effWindow; b.type = Image.Type.Sliced; }
                else b.color = UiKit.FrameRed;
                effText = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                effText.rectTransform.SetParent(effBox, false);
                effText.rectTransform.anchorMin = Vector2.zero; effText.rectTransform.anchorMax = Vector2.one;
                effText.rectTransform.offsetMin = new Vector2(0.5f, 0f); effText.rectTransform.offsetMax = new Vector2(0.5f, 0.5f);
                effText.font = nameText.font;
                effText.fontSize = 11f;
                effText.alignment = TextAlignmentOptions.Center;
                effText.textWrappingMode = TextWrappingModes.NoWrap;
                effText.raycastTarget = false;
                effText.color = UiKit.White;
            }
            float m = mult.Value;
            // 정보창 몸통의 세로 가운데 (그림 기준 위에서 13), 속성 화살표 오른쪽 끝 + 2
            var box = (RectTransform)transform;
            float right = box.rect.width - (bossMode ? 12f : 12f);
            if (typeIcon1 != null && typeIcon1.gameObject.activeSelf)
            {
                var ic = typeIcon1.rectTransform;
                right = ic.anchoredPosition.x + ic.sizeDelta.x;
                if (typeIcon2 != null && typeIcon2.gameObject.activeSelf) right = Mathf.Max(right, typeIcon2.rectTransform.anchoredPosition.x + typeIcon2.rectTransform.sizeDelta.x);
            }
            effBox.anchoredPosition = new Vector2(Mathf.Round(right + EffGap), -EffCenterY);
            effText.text = (Mathf.Approximately(m, Mathf.Round(m)) ? m.ToString("0") : m.ToString("0.##")) + "x";
            effBox.gameObject.SetActive(true);
        }

        const float EffW = 26f, EffH = 18f, EffGap = 2f, EffCenterY = 13f;

        void LateUpdate()
        {
            if (peekGroup == null) return;
            if (peekAlpha == peekTarget) return;
            peekAlpha = Mathf.MoveTowards(peekAlpha, peekTarget, Time.unscaledDeltaTime * 8f);   // 0.12초쯤에 바뀐다
            peekGroup.alpha = peekAlpha;
            peek.gameObject.SetActive(peekAlpha > 0f);
        }

        // ───────── 능력치 단계 표시 (공격+2  스피드-1 …) ─────────
        // 상대 정보창은 아래에, 내 정보창은 위에 작은 글씨로. 오르면 주황, 떨어지면 하늘색. 보스가 힘을 모으면 빨간 "힘 모으는 중!"
        const string UpColor = "#ffa070", DownColor = "#7cc0ff", ChargeColor = "#ff4d4d";
        TMP_Text stageText;
        Image stageBack;

        /// <summary>stages = 공격·방어·스피드 단계 (StatType 순서)</summary>
        public void ShowStages(int[] stages, bool charging)
        {
            var parts = new List<string>();
            if (stages != null)
            {
                var types = (StatType[])System.Enum.GetValues(typeof(StatType));
                for (int i = 0; i < stages.Length && i < types.Length; i++)
                {
                    int st = stages[i];
                    if (st == 0) continue;
                    parts.Add($"<color={(st > 0 ? UpColor : DownColor)}>{StatStages.Short(types[i])}{(st > 0 ? "+" : "")}{st}</color>");
                }
            }
            if (charging) parts.Add($"<color={ChargeColor}>힘 모으는 중!</color>");

            if (parts.Count == 0)
            {
                if (stageText != null) stageBack.gameObject.SetActive(false);
                return;
            }
            EnsureStageText();
            stageBack.gameObject.SetActive(true);
            stageText.text = string.Join("  ", parts);
            float w = stageText.GetPreferredValues(stageText.text).x;
            stageBack.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(w) + 6f, 10f);
        }

        void EnsureStageText()
        {
            if (stageText != null) return;
            bool player = expFill != null;                      // 내 정보창은 위쪽, 상대 정보창은 아래쪽에 붙인다
            var back = new GameObject("StageBack", typeof(RectTransform)).AddComponent<Image>();
            var rt = back.rectTransform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = player ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            rt.pivot = player ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
            rt.anchoredPosition = player ? new Vector2(10f, 1f) : new Vector2(6f, -1f);
            back.color = new Color(0f, 0f, 0f, 0.55f);
            back.raycastTarget = false;
            stageBack = back;

            stageText = new GameObject("StageText", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var trt = stageText.rectTransform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(3f, 0f); trt.offsetMax = Vector2.zero;
            stageText.font = nameText.font;
            stageText.fontSize = 7f;
            stageText.alignment = TextAlignmentOptions.Left;
            stageText.textWrappingMode = TextWrappingModes.NoWrap;
            stageText.richText = true;
            stageText.raycastTarget = false;
        }

        /// <summary>상태이상 뱃지를 보여주거나(None이면) 숨긴다</summary>
        public void SetStatus(StatusCondition status) => SetBadge(StatusRules.BadgeName(status));

        /// <summary>쓰러졌을 때 기절 뱃지</summary>
        public void ShowFainted() => SetBadge(StatusRules.FaintedBadgeName);

        void SetBadge(string imageName)
        {
            if (statusBadge == null) return;
            var tex = imageName != null ? Resources.Load<Texture2D>($"statuses/{imageName}") : null;
            statusBadge.texture = tex;
            statusBadge.uvRect = StatusUv;
            statusBadge.enabled = tex != null;
        }

        /// <summary>HP 비율(0~1). 절반 초과 초록 / 25% 초과 노랑 / 그 이하 빨강 (포켓로그와 동일)</summary>
        public void SetHp(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            float width = bossMode ? BossBarWidth : HpBarWidth;
            int px = Mathf.RoundToInt(width * ratio);
            if (ratio > 0f && px < 1) px = 1;                         // 살아있으면 최소 1픽셀
            float uvY, uvH;
            if (bossMode)
            {
                // overlay_hp_boss.png(86x12): 위에서부터 4줄씩(빈 2줄 + 색 2줄) 초록/노랑/빨강
                uvY = ratio > 0.5f ? 8f / 12f : ratio > 0.25f ? 4f / 12f : 0f;
                uvH = 2f / 12f;
            }
            else
            {
                // overlay_hp.png(48x6)는 위에서부터 2줄씩 초록/노랑/빨강. uv는 아래가 0이라 순서가 뒤집힌다.
                uvY = ratio > 0.5f ? 4f / 6f : ratio > 0.25f ? 2f / 6f : 0f;
                uvH = 2f / 6f;
            }
            SetBar(hpFill, px, width, uvY, uvH);
        }

        // ───────── 보스 정보창 (포켓로그 pbinfo_enemy_boss) ─────────
        // 보스는 정보창이 가로로 길어지고(130 → 178), HP바도 길어지며(48 → 86) 칸(실드) 경계가 보인다.
        // HP 글자 대신 빨간 "보스". 그림은 Resources/UI 에 있다 (ItemIconSetup이 자동 복사).
        public const float BossBarWidth = 86f;
        const float BossBarX = 68f;
        const float BossExtraWidth = 48f;
        static readonly Color BossLabelColor = new Color(0.97f, 0.33f, 0.2f);
        static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.95f);

        static Sprite bossBackground;
        static Texture2D bossHpTexture;

        bool bossMode;
        bool layoutSaved;
        Image background;
        Sprite normalBackground;
        Vector2 normalSize, normalLevelPos, normalType1Pos, normalType2Pos, normalHpPos, normalLabelPos;
        Texture normalHpTexture;
        TMP_Text hpLabel;
        string normalLabelText;
        Color normalLabelColor;
        readonly List<Image> dividers = new List<Image>();

        void SaveNormalLayout()
        {
            if (layoutSaved) return;
            layoutSaved = true;
            var rt = (RectTransform)transform;
            normalSize = rt.sizeDelta;
            background = GetComponent<Image>();
            if (background != null) normalBackground = background.sprite;
            if (levelText != null) normalLevelPos = levelText.rectTransform.anchoredPosition;
            if (typeIcon1 != null) normalType1Pos = typeIcon1.rectTransform.anchoredPosition;
            if (typeIcon2 != null) normalType2Pos = typeIcon2.rectTransform.anchoredPosition;
            if (hpFill != null) { normalHpPos = hpFill.rectTransform.anchoredPosition; normalHpTexture = hpFill.texture; }
            var labelTf = transform.Find("HpLabel");
            hpLabel = labelTf != null ? labelTf.GetComponent<TMP_Text>() : null;
            if (hpLabel != null)
            {
                normalLabelText = hpLabel.text;
                normalLabelColor = hpLabel.color;
                normalLabelPos = hpLabel.rectTransform.anchoredPosition;
            }
        }

        /// <summary>보스면 보스 모양 정보창으로, 아니면 원래대로 (상대 정보창만)</summary>
        void ApplyBossLayout(Pal pal)
        {
            if (expFill != null) return;                               // 내 정보창은 보스 모양을 쓰지 않는다
            SaveNormalLayout();
            bool boss = pal.IsBoss && LoadBossArt();
            bossMode = boss;
            float extra = boss ? BossExtraWidth : 0f;
            var rt = (RectTransform)transform;
            rt.sizeDelta = normalSize + new Vector2(extra, 0f);
            if (background != null) background.sprite = boss ? bossBackground : normalBackground;
            if (levelText != null) levelText.rectTransform.anchoredPosition = normalLevelPos + new Vector2(extra, 0f);
            if (hpFill != null)
            {
                hpFill.texture = boss ? bossHpTexture : normalHpTexture;
                hpFill.rectTransform.anchoredPosition = boss ? new Vector2(BossBarX, normalHpPos.y) : normalHpPos;
            }
            if (hpLabel != null)
            {
                hpLabel.text = boss ? "보스" : normalLabelText;
                hpLabel.color = boss ? BossLabelColor : normalLabelColor;
                hpLabel.rectTransform.anchoredPosition = boss ? new Vector2(BossBarX - 18f, normalLabelPos.y) : normalLabelPos;
            }
            SetShields(pal);
        }

        /// <summary>남은 실드 경계선을 HP바 위에 그린다 (실드가 깨지면 그 선이 사라진다)</summary>
        public void SetShields(Pal pal)
        {
            int show = bossMode ? pal.ShieldsLeft : 0;
            while (dividers.Count < show) dividers.Add(MakeDivider());
            for (int i = 0; i < dividers.Count; i++)
            {
                bool on = i < show;
                dividers[i].gameObject.SetActive(on);
                if (!on) continue;
                int shield = i + 1;                                     // 아래에서 shield번째 실드
                float x = BossBarX + Mathf.Round(BossBarWidth * shield / pal.BossSegments);
                dividers[i].rectTransform.anchoredPosition = new Vector2(x, normalHpPos.y + 1f);
                dividers[i].transform.SetAsLastSibling();
            }
        }

        Image MakeDivider()
        {
            var go = new GameObject("ShieldLine", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(1f, 4f);                         // HP바(2줄)보다 위아래로 1줄씩 길게
            var img = go.AddComponent<Image>();
            img.color = DividerColor;
            img.raycastTarget = false;
            return img;
        }

        static bool LoadBossArt()
        {
            if (bossBackground != null && bossHpTexture != null) return true;
            var bg = Resources.Load<Texture2D>("UI/pbinfo_enemy_boss");
            bossHpTexture = Resources.Load<Texture2D>("UI/overlay_hp_boss");
            if (bg == null || bossHpTexture == null)
            {
                Debug.LogWarning("[PalInfoBox] Resources/UI 에 보스 정보창 그림이 없습니다. (메뉴 PalRogue/18 실행)");
                return false;
            }
            bg.filterMode = FilterMode.Point;
            bossHpTexture.filterMode = FilterMode.Point;
            bossBackground = Sprite.Create(bg, new Rect(0, 0, bg.width, bg.height), new Vector2(0.5f, 0.5f), 100f);
            return true;
        }

        /// <summary>HP바를 from → to 로 부드럽게 줄이거나 늘린다 (코루틴으로 실행)</summary>
        public System.Collections.IEnumerator AnimateHp(Pal pal, int from, int to)
        {
            int max = Mathf.Max(1, pal.MaxHP);
            float duration = Mathf.Clamp(Mathf.Abs(to - from) / (float)max * 1.2f, 0.25f, 1.0f);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                ShowHp(Mathf.RoundToInt(Mathf.Lerp(from, to, t / duration)), max);
                yield return null;
            }
            ShowHp(to, max);
        }

        void ShowHp(int hp, int max)
        {
            if (hpText != null) hpText.text = $"{hp}/{max}";
            SetHp((float)hp / max);
        }

        /// <summary>경험치 비율(0~1). 내 정보창만 사용</summary>
        public void SetExp(float ratio)
        {
            if (expFill == null) return;
            int px = Mathf.RoundToInt(ExpBarWidth * Mathf.Clamp01(ratio));
            SetBar(expFill, px, ExpBarWidth, 0f, 1f);
        }

        /// <summary>EXP바를 from → to (0~1)로 채운다 (코루틴)</summary>
        public System.Collections.IEnumerator AnimateExp(float from, float to)
        {
            if (expFill == null) yield break;
            float duration = Mathf.Clamp(Mathf.Abs(to - from) * 1.0f, 0.2f, 0.9f);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetExp(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetExp(to);
        }

        /// <summary>레벨 글자만 바꾼다 (레벨업 연출 중간)</summary>
        public void SetLevel(int level) => levelText.text = $"Lv.{level}";

        /// <summary>HP 숫자와 바를 지금 값으로 (레벨업으로 최대 HP가 바뀌었을 때)</summary>
        public void RefreshHp(Pal pal)
        {
            if (hpText != null) hpText.text = $"{pal.CurrentHP}/{pal.MaxHP}";
            SetHp(pal.MaxHP > 0 ? (float)pal.CurrentHP / pal.MaxHP : 0f);
        }

        static void SetBar(RawImage bar, int px, float fullWidth, float uvY, float uvH)
        {
            if (bar == null) return;
            bar.gameObject.SetActive(px > 0);
            var rt = bar.rectTransform;
            rt.sizeDelta = new Vector2(px, rt.sizeDelta.y);
            bar.uvRect = new Rect(0f, uvY, px / fullWidth, uvH);      // 잘린 만큼만 보여줘서 늘어나지 않게
        }

        static void SetType(Image icon, PalType type)
        {
            if (icon == null) return;
            icon.sprite = Resources.Load<Sprite>($"1.팰/2.Elements/ui_type/{type}");
            icon.enabled = icon.sprite != null;
        }
    }
}
