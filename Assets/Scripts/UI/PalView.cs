using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 전투 필드에 서 있는 팰 한 마리의 모습(스프라이트/애니메이션). 다른 팰로 바꿔 끼우거나 쓰러지는 연출을 한다.
    /// 씬에서 직접 조절해 둔 크기와 발 위치를 기억해서, 팰이 바뀌어도 같은 비율로 맞춰 준다.
    /// (메뉴 PalRogue/10 이 자동으로 붙이고 기준값을 계산한다)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PalView : MonoBehaviour
    {
        [Tooltip("PalData의 기본 배율에 곱해지는 값 (씬에서 직접 키우거나 줄인 만큼)")]
        public float scaleTune = 1f;
        [Tooltip("발 끝의 월드 Y 좌표. 팰이 바뀌어도 이 높이에 발이 오도록 맞춘다")]
        public float feetWorldY;

        SpriteRenderer sr;
        Animator animator;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
        }

        /// <summary>이 팰의 모습으로 바꾼다. back=true면 뒷모습(내 팰), false면 앞모습(적)</summary>
        public void Show(PalData data, bool back)
        {
            if (sr == null) Awake();

            var sprite = back ? data.BackOrFront : data.frontSprite;
            float scale = (back ? data.BackOrFrontScale : data.frontScale) * scaleTune;
            var controller = back ? data.BackOrFrontAnimator : data.frontAnimator;

            sr.sprite = sprite;
            sr.enabled = true;
            sr.color = Color.white;
            transform.localScale = Vector3.one * scale;

            if (controller != null)
            {
                if (animator == null) animator = gameObject.AddComponent<Animator>();
                animator.enabled = true;
                animator.runtimeAnimatorController = controller;
                animator.Rebind();
            }
            else if (animator != null)
            {
                animator.runtimeAnimatorController = null;
                animator.enabled = false;
            }

            // 스프라이트 중심이 기준이라, 발 끝 높이가 같도록 위치를 보정한다
            float height = sprite != null ? sprite.bounds.size.y * scale : 0f;
            var pos = transform.position;
            pos.y = feetWorldY + height / 2f;
            transform.position = pos;
        }

        /// <summary>쓰러지는 연출: 아래로 가라앉으며 투명해진다</summary>
        public IEnumerator PlayFaint()
        {
            if (sr == null) Awake();
            Vector3 start = transform.position;
            const float duration = 0.5f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                transform.position = start + Vector3.down * (k * 0.6f);
                var c = sr.color; c.a = 1f - k; sr.color = c;
                yield return null;
            }
            sr.enabled = false;
            transform.position = start;
        }
    }
}
