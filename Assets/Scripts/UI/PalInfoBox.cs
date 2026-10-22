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
            nameText.text = string.IsNullOrEmpty(pal.Data.palName) ? pal.Data.name : pal.Data.palName;
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
            int px = Mathf.RoundToInt(HpBarWidth * ratio);
            if (ratio > 0f && px < 1) px = 1;                         // 살아있으면 최소 1픽셀
            // overlay_hp.png(48x6)는 위에서부터 2줄씩 초록/노랑/빨강. uv는 아래가 0이라 순서가 뒤집힌다.
            float uvY = ratio > 0.5f ? 4f / 6f : ratio > 0.25f ? 2f / 6f : 0f;
            SetBar(hpFill, px, HpBarWidth, uvY, 2f / 6f);
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
