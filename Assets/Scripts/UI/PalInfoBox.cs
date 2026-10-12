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

        /// <summary>팰의 현재 상태를 화면에 반영한다</summary>
        public void Refresh(Pal pal)
        {
            nameText.text = string.IsNullOrEmpty(pal.Data.palName) ? pal.Data.name : pal.Data.palName;
            levelText.text = $"Lv.{pal.Level}";
            if (hpText != null) hpText.text = $"{pal.CurrentHP}/{pal.MaxHP}";
            SetHp(pal.MaxHP > 0 ? (float)pal.CurrentHP / pal.MaxHP : 0f);

            SetType(typeIcon1, pal.Data.primaryType);
            if (typeIcon2 != null)
            {
                bool dual = pal.Data.hasSecondaryType;
                typeIcon2.gameObject.SetActive(dual);
                if (dual) SetType(typeIcon2, pal.Data.secondaryType);
            }
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

        /// <summary>경험치 비율(0~1). 내 정보창만 사용</summary>
        public void SetExp(float ratio)
        {
            if (expFill == null) return;
            int px = Mathf.RoundToInt(ExpBarWidth * Mathf.Clamp01(ratio));
            SetBar(expFill, px, ExpBarWidth, 0f, 1f);
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
