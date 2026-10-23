using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 파티 화면의 칸 하나 (팰 아이콘·이름·레벨·HP바·상태 뱃지).
    /// 칸 배경은 포켓로그 party_slot(_main) 시트에서 상태에 맞는 그림을 잘라 보여준다.
    /// 참조 칸들은 메뉴 PalRogue/14 가 자동으로 연결한다.
    /// </summary>
    public class PartySlotView : MonoBehaviour
    {
        // party_slot / party_slot_main 시트는 위에서부터 6칸: 기본, 선택, 기절, 기절+선택, 교체, 교체+선택
        public const int FrameCount = 6;
        const int FrameNormal = 0, FrameFainted = 2;
        public const float HpFillWidth = 80f;          // party_slot_hp_overlay 가로

        public RawImage frame;
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text levelText;
        public RawImage hpFill;
        public TMP_Text hpText;
        public RawImage statusBadge;

        static readonly Rect StatusUv = new Rect(221f / 1024f, 163f / 572f, 565f / 1024f, 242f / 572f);

        /// <summary>팰 정보를 칸에 채운다. pal이 null이면 빈 칸(숨김)</summary>
        public void Set(Pal pal, bool selected)
        {
            gameObject.SetActive(pal != null);
            if (pal == null) return;

            int f = (pal.IsFainted ? FrameFainted : FrameNormal) + (selected ? 1 : 0);
            frame.uvRect = new Rect(0f, 1f - (f + 1f) / FrameCount, 1f, 1f / FrameCount);   // uv는 아래가 0

            icon.sprite = pal.Data.frontSprite;
            icon.enabled = icon.sprite != null;
            nameText.text = string.IsNullOrEmpty(pal.Data.palName) ? pal.Data.name : pal.Data.palName;
            StarIcons.Show(nameText, pal.Stars);                       // 응축 ★
            levelText.text = $"Lv.{pal.Level}";
            hpText.text = $"{pal.CurrentHP}/{pal.MaxHP}";
            SetHp(pal.MaxHP > 0 ? (float)pal.CurrentHP / pal.MaxHP : 0f);

            string badge = pal.IsFainted ? StatusRules.FaintedBadgeName : StatusRules.BadgeName(pal.Status);
            var tex = badge != null ? Resources.Load<Texture2D>($"statuses/{badge}") : null;
            statusBadge.texture = tex;
            statusBadge.uvRect = StatusUv;
            statusBadge.enabled = tex != null;
        }

        void SetHp(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            int px = Mathf.RoundToInt(HpFillWidth * ratio);
            if (ratio > 0f && px < 1) px = 1;
            hpFill.gameObject.SetActive(px > 0);
            var rt = hpFill.rectTransform;
            rt.sizeDelta = new Vector2(px, rt.sizeDelta.y);
            // party_slot_hp_overlay(80x9): 위에서부터 초록(0~1줄) / 노랑(3~4줄) / 빨강(6~7줄). uv는 아래가 0
            float uvY = ratio > 0.5f ? 7f / 9f : ratio > 0.25f ? 4f / 9f : 1f / 9f;
            hpFill.uvRect = new Rect(0f, uvY, px / HpFillWidth, 2f / 9f);
        }
    }
}
