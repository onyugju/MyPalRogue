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

            // ① 애니메이터를 먼저 정리한다.
            //    애니메이터를 끄면 유니티가 애니메이터가 바꿔 놓은 그림을 "처음 값"(이전 팰)으로 되돌리기 때문에,
            //    그림을 먼저 넣고 나중에 끄면 이전 팰 그림이 다시 나타난다.
            if (controller != null)
            {
                if (animator == null) animator = gameObject.AddComponent<Animator>();
                animator.enabled = true;
                animator.runtimeAnimatorController = controller;
                animator.Rebind();
                animator.Update(0f);                  // 첫 프레임을 바로 적용
            }
            else if (animator != null)
            {
                animator.runtimeAnimatorController = null;
                animator.enabled = false;
            }

            // ② 그 다음에 그림·크기를 넣는다 (애니메이션 팰이면 애니메이터가 이어서 그림을 바꾼다)
            if (controller == null) sr.sprite = sprite;
            pendingSprite = controller == null ? sprite : null;
            sr.enabled = true;
            sr.color = Color.white;
            transform.localScale = Vector3.one * scale;

            // 스프라이트 중심이 기준이라, 발 끝 높이가 같도록 위치를 보정한다
            float height = sprite != null ? sprite.bounds.size.y * scale : 0f;
            var pos = transform.position;
            pos.y = feetWorldY + height / 2f;
            transform.position = pos;
        }

        Sprite pendingSprite;

        // 같은 프레임 안에서 애니메이터가 그림을 되돌리는 경우까지 막기 위해, 정지 그림 팰은 한 번 더 확실히 넣는다
        void LateUpdate()
        {
            if (pendingSprite == null) return;
            if (sr.sprite != pendingSprite) sr.sprite = pendingSprite;
            pendingSprite = null;
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
