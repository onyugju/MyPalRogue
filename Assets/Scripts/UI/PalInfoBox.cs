using System.Collections.Generic;
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

            SetType(typeIcon1, pal.Data.primaryType);
            if (typeIcon2 != null)
            {
                bool dual = pal.Data.hasSecondaryType;
                typeIcon2.gameObject.SetActive(dual);
                if (dual) SetType(typeIcon2, pal.Data.secondaryType);
            }
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
            if (typeIcon1 != null) typeIcon1.rectTransform.anchoredPosition = normalType1Pos + new Vector2(extra, 0f);
            if (typeIcon2 != null) typeIcon2.rectTransform.anchoredPosition = normalType2Pos + new Vector2(extra, 0f);
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
