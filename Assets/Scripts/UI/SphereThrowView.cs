using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 필드 위를 날아가는 스피어 하나 (던지기 → 상대를 흡수 → 땅에 떨어짐 → 흔들림 → 성공/실패).
    /// 씬에 따로 만들 필요 없이 BattleScreen이 처음 쓸 때 만든다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SphereThrowView : MonoBehaviour
    {
        const float PxPerUnit = 18f;          // 월드 1유닛 = 화면 18픽셀 (전투 화면 기준)
        const float SizePx = 12f;             // 스피어가 화면에서 보이는 크기(픽셀)
        static readonly Vector2 ThrowFromPx = new Vector2(70f, 150f);   // 던지기 시작 위치 (화면 픽셀, 왼쪽 위 원점)

        SpriteRenderer sr;

        public static SphereThrowView Create(Transform parent)
        {
            var go = new GameObject("SphereThrow", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<SphereThrowView>();
            view.sr = go.GetComponent<SpriteRenderer>();
            view.sr.sortingOrder = 20;
            go.SetActive(false);
            return view;
        }

        public float Radius => SizePx / PxPerUnit / 2f;

        public void Hide() => gameObject.SetActive(false);

        /// <summary>start(지금 화면 왼쪽 아래)에서 target까지 포물선으로 날아간다</summary>
        /// <param name="from">던지기 시작 위치 (비우면 기본 위치). 내 팰을 내보낼 때는 화면 왼쪽 아래 밖</param>
        public IEnumerator Throw(SphereType type, Vector3 target, float duration = 0.55f, Vector3? from = null, float arc = 2.2f)
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            sr.sprite = PalSpheres.LoadSprite(type);
            sr.color = Color.white;
            float w = sr.sprite != null ? Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y) : 1f;
            transform.localScale = Vector3.one * (SizePx / PxPerUnit / w);
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);

            Vector3 start = from ?? new Vector3((ThrowFromPx.x - 160f) / PxPerUnit, (90f - ThrowFromPx.y) / PxPerUnit, 0f);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                var p = Vector3.Lerp(start, target, k);
                p.y += Mathf.Sin(k * Mathf.PI) * arc;
                transform.position = p;
                transform.rotation = Quaternion.Euler(0, 0, -720f * k);
                yield return null;
            }
            transform.position = target;
            transform.rotation = Quaternion.identity;
        }

        /// <summary>땅으로 떨어지며 한 번 통 튄다</summary>
        public IEnumerator Drop(Vector3 ground, float duration = 0.35f)
        {
            Vector3 from = transform.position;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                var p = Vector3.Lerp(from, ground, k * k);
                if (k > 0.7f) p.y += Mathf.Sin((k - 0.7f) / 0.3f * Mathf.PI) * 0.15f;   // 작게 튐
                transform.position = p;
                yield return null;
            }
            transform.position = ground;
        }

        /// <summary>좌우로 한 번 흔들린다</summary>
        public IEnumerator Shake(float duration = 0.45f)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI * 2f) * 25f);
                yield return null;
            }
            transform.rotation = Quaternion.identity;
            yield return new WaitForSecondsRealtime(0.35f);
        }

        /// <summary>포획 성공: 살짝 어두워지며 멈춘다</summary>
        public void ShowCaught() => sr.color = new Color(0.55f, 0.55f, 0.55f);
    }
}
