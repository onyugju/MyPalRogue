using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 멈춘 화면(스크린샷)이 유리처럼 깨지는 연출. 화면을 맨 위에 덮고 있다가,
    /// 하얗게 번쩍 → 중심에서 금이 퍼짐 → 잠깐 멈춤 → 조각이 바깥으로 튀며 떨어진다. 다 끝나면 스스로 사라진다.
    /// 조각 = 다각형 모양 마스크(Image + Mask) 안에 스크린샷 전체(RawImage)를 맞춰 놓은 것. 금 = 회전한 가는 Image.
    /// </summary>
    public class ScreenGlass : MonoBehaviour
    {
        const int Rays = 12;
        const int MaskSize = 128;                 // 조각 모양 마스크 그림 크기 (긴 쪽)
        Texture tex;
        Vector2 center;
        RawImage whole;
        Image flash;
        RectTransform lineRoot;
        readonly List<(GameObject go, int ring)> lineParts = new List<(GameObject, int)>();
        readonly List<Shard> shards = new List<Shard>();
        readonly List<Object> made = new List<Object>();

        class Shard
        {
            public RectTransform rt;
            public RawImage pic;
            public Vector2 pos, vel;
            public float spin, delay;
        }

        public static ScreenGlass Create(Texture shot, Vector2 crackCenter)
        {
            var go = new GameObject("ScreenGlass", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 320;                                    // 보스 컷인(300)보다 위
            var glass = go.AddComponent<ScreenGlass>();
            glass.tex = shot;
            glass.center = crackCenter;
            glass.Build();
            return glass;
        }

        RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        void Build()
        {
            float w = Screen.width, h = Screen.height;

            // 조각: 중심에서 뻗는 금(Rays개)과 동심원 고리로 나눈 칸
            float diag = Mathf.Sqrt(w * w + h * h);
            float[] rings = { 0f, h * 0.08f, h * 0.22f, h * 0.45f, h * 0.8f, diag * 1.2f };
            var angles = new float[Rays];
            for (int i = 0; i < Rays; i++) angles[i] = (i + Random.Range(-0.3f, 0.3f)) / Rays * Mathf.PI * 2f;
            var v = new Vector2[rings.Length, Rays];
            for (int j = 0; j < rings.Length; j++)
                for (int i = 0; i < Rays; i++)
                {
                    float a = angles[i] + (j == 0 ? 0f : Random.Range(-0.1f, 0.1f));
                    float rad = rings[j] * (j == 0 ? 0f : Random.Range(0.88f, 1.12f));
                    v[j, i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                }

            var screenRect = new Rect(-8f, -8f, w + 16f, h + 16f);
            var segs = new List<(Vector2 a, Vector2 b, int ring)>();
            for (int j = 0; j < rings.Length - 1; j++)
                for (int i = 0; i < Rays; i++)
                {
                    int n = (i + 1) % Rays;
                    segs.Add((v[j, i], v[j + 1, i], j));
                    if (j > 0) segs.Add((v[j, i], v[j, n], j));
                }
            float lw = Mathf.Max(3f, h / 150f);

            // 조각에 쓸 그림: 금이 그려진 화면 (조각이 떨어질 때 가장자리에 금이 남아 있게)
            var cracked = BakeCracks(tex as Texture2D, segs, lw);
            for (int j = 0; j < rings.Length - 1; j++)
                for (int i = 0; i < Rays; i++)
                {
                    int n = (i + 1) % Rays;
                    var poly = new[] { v[j, i], v[j + 1, i], v[j + 1, n], v[j, n] };
                    var bb = Bounds(poly);
                    if (!bb.Overlaps(screenRect)) continue;                  // 화면 밖 칸은 만들지 않는다
                    // 화면 밖으로 크게 삐져나간 칸은 화면 크기까지만 (마스크 해상도를 아끼려고)
                    var clip = Rect.MinMaxRect(Mathf.Max(bb.xMin, screenRect.xMin), Mathf.Max(bb.yMin, screenRect.yMin),
                                               Mathf.Min(bb.xMax, screenRect.xMax), Mathf.Min(bb.yMax, screenRect.yMax));
                    MakeShard(poly, clip, j, w, h, cracked != null ? cracked : tex);
                }

            // 처음엔 한 장으로 (조각 사이 틈이 보이지 않게) — 조각보다 위에
            var wr = Child("Whole", transform);
            wr.anchorMax = Vector2.one;
            wr.sizeDelta = Vector2.zero;
            whole = wr.gameObject.AddComponent<RawImage>();
            whole.texture = tex;
            whole.raycastTarget = false;

            // 금
            lineRoot = Child("Cracks", transform);
            lineRoot.anchorMax = Vector2.one;
            lineRoot.sizeDelta = Vector2.zero;
            foreach (var (a, b, ring) in segs)
            {
                lineParts.Add((Line(a, b, lw * 3.5f, new Color(1f, 0.8f, 1f, 0.22f)), ring));     // 빛 번짐
                lineParts.Add((Line(a, b, lw * 1.8f, new Color(0.05f, 0.02f, 0.1f, 0.8f)), ring)); // 어두운 틈
                lineParts.Add((Line(a, b, lw * 0.7f, Color.white), ring));                         // 빛나는 가운데
            }
            ShowLines(-1);

            var fr = Child("Flash", transform);
            fr.anchorMax = Vector2.one;
            fr.sizeDelta = Vector2.zero;
            flash = fr.gameObject.AddComponent<Image>();
            flash.raycastTarget = false;
            flash.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>화면 그림을 복사해 금(어두운 틈 + 밝은 가운데)을 직접 그려 넣는다. 읽을 수 없는 그림이면 null</summary>
        Texture2D BakeCracks(Texture2D src, List<(Vector2 a, Vector2 b, int ring)> segs, float lw)
        {
            if (src == null || !src.isReadable) return null;
            int w = src.width, h = src.height;
            var px = src.GetPixels32();
            int outer = Mathf.Max(2, Mathf.RoundToInt(lw * 0.9f)), core = Mathf.Max(1, Mathf.RoundToInt(lw * 0.35f));
            var dark = new Color32(14, 6, 26, 255);
            var light = new Color32(255, 250, 255, 255);
            var screen = new Rect(-outer, -outer, w + outer * 2, h + outer * 2);
            foreach (var (a0, b0, _) in segs)
            {
                if (!ClipToRect(a0, b0, screen, out var a, out var b)) continue;
                float len = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(1f, core)));
                for (int k = 0; k <= steps; k++)
                {
                    var p = Vector2.Lerp(a, b, k / (float)steps);
                    int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
                    for (int dy = -outer; dy <= outer; dy++)
                    {
                        int y = cy + dy;
                        if (y < 0 || y >= h) continue;
                        for (int dx = -outer; dx <= outer; dx++)
                        {
                            int x = cx + dx;
                            if (x < 0 || x >= w) continue;
                            int d2 = dx * dx + dy * dy;
                            if (d2 > outer * outer) continue;
                            int i = y * w + x;
                            if (d2 <= core * core) px[i] = light;
                            else if (px[i].r != 255 || px[i].g != 250) px[i] = dark;
                        }
                    }
                }
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px);
            t.Apply();
            made.Add(t);
            return t;
        }

        /// <summary>선분을 사각형 안쪽으로 자른다 (Liang–Barsky). 남는 부분이 없으면 false</summary>
        static bool ClipToRect(Vector2 a0, Vector2 b0, Rect r, out Vector2 a, out Vector2 b)
        {
            a = a0; b = b0;
            float t0 = 0f, t1 = 1f, dx = b0.x - a0.x, dy = b0.y - a0.y;
            float[] p = { -dx, dx, -dy, dy };
            float[] q = { a0.x - r.xMin, r.xMax - a0.x, a0.y - r.yMin, r.yMax - a0.y };
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Approximately(p[i], 0f)) { if (q[i] < 0f) return false; continue; }
                float t = q[i] / p[i];
                if (p[i] < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            a = a0 + new Vector2(dx, dy) * t0;
            b = a0 + new Vector2(dx, dy) * t1;
            return true;
        }

        static Rect Bounds(Vector2[] poly)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in poly) { x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        /// <summary>다각형 모양 마스크 + 그 안에 화면 전체 그림을 맞춰 놓은 조각 하나</summary>
        void MakeShard(Vector2[] poly, Rect box, int ring, float w, float h, Texture picture)
        {
            if (box.width < 2f || box.height < 2f) return;
            // 마스크 그림 (다각형 안쪽만 불투명)
            float scale = MaskSize / Mathf.Max(box.width, box.height);
            int tw = Mathf.Max(4, Mathf.CeilToInt(box.width * scale)), th = Mathf.Max(4, Mathf.CeilToInt(box.height * scale));
            var mask = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[tw * th];
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    var p = new Vector2(box.xMin + (x + 0.5f) / scale, box.yMin + (y + 0.5f) / scale);
                    px[y * tw + x] = new Color32(255, 255, 255, (byte)(Inside(poly, p) ? 255 : 0));
                }
            mask.SetPixels32(px);
            mask.Apply();
            var sprite = Sprite.Create(mask, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 100f);
            made.Add(mask);
            made.Add(sprite);

            var rt = Child("Shard", transform);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = box.size;
            rt.anchoredPosition = box.center;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            var m = rt.gameObject.AddComponent<Mask>();
            m.showMaskGraphic = false;

            // 마스크 안의 화면 그림: 화면 왼쪽 아래가 원점이 되도록 맞춘다
            var pr = Child("Pic", rt);
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = Vector2.zero;
            pr.sizeDelta = new Vector2(w, h);
            pr.anchoredPosition = -box.center;
            var pic = pr.gameObject.AddComponent<RawImage>();
            pic.texture = picture;
            pic.raycastTarget = false;

            rt.gameObject.SetActive(false);
            var c = box.center;
            var outDir = (c - center).sqrMagnitude > 1f ? (c - center).normalized : Random.insideUnitCircle.normalized;
            float speed = Random.Range(0.2f, 0.55f) * h * (1.2f - 0.15f * ring);
            shards.Add(new Shard
            {
                rt = rt, pic = pic, pos = c,
                vel = outDir * speed + new Vector2(0f, Random.Range(0.15f, 0.4f) * h),
                spin = Random.Range(-160f, 160f),
                delay = ring * 0.05f + Random.Range(0f, 0.06f),
            });
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            // 볼록 다각형: 모든 변에 대해 같은 쪽이면 안쪽 (도는 방향과 상관없이)
            int sign = 0;
            for (int i = 0; i < poly.Length; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Length];
                float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (Mathf.Abs(cross) < 1e-3f) continue;
                int s = cross > 0 ? 1 : -1;
                if (sign == 0) sign = s;
                else if (s != sign) return false;
            }
            return true;
        }

        GameObject Line(Vector2 a, Vector2 b, float width, Color color)
        {
            var d = b - a;
            var rt = Child("Crack", lineRoot);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(d.magnitude, width);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt.gameObject;
        }

        void ShowLines(int reach)
        {
            foreach (var (go, ring) in lineParts) go.SetActive(ring <= reach);
        }

        public IEnumerator Shatter()
        {
            float h = Screen.height;
            // 하얗게 번쩍 (구가 깨지는 빛) — 짧게
            for (float t = 0f; t < 0.22f; t += BattleTime.FxDt)
            {
                flash.color = new Color(1f, 1f, 1f, t < 0.04f ? t / 0.04f : 1f - (t - 0.04f) / 0.18f);
                yield return null;
            }
            flash.color = new Color(1f, 1f, 1f, 0f);

            // "쩍" — 화면에 금이 퍼진다 (고리 하나씩, 칠 때마다 화면이 살짝 흔들린다)
            for (int ring = 0; ring < 5; ring++)
            {
                ShowLines(ring);
                if (ring == 0 || ring == 2) GameAudio.Se("hit");
                for (float t = 0f; t < 0.07f; t += BattleTime.FxDt)
                {
                    float a = h * 0.006f * (1f - t / 0.07f);
                    SetOffset(new Vector2(Random.Range(-a, a), Random.Range(-a, a)));
                    yield return null;
                }
            }
            SetOffset(Vector2.zero);
            yield return BattleTime.FxWait(0.55f);                         // 숨을 고르는 정적 (금 간 화면이 멈춰 있다)

            // 산산조각: 가운데부터 바깥으로 튀며 떨어진다
            GameAudio.Se("hit_strong");
            foreach (var s in shards) s.rt.gameObject.SetActive(true);
            whole.enabled = false;
            lineRoot.gameObject.SetActive(false);
            float gravity = -2.2f * h;
            const float Duration = 1.6f;
            for (float t = 0f; t < Duration; t += BattleTime.FxDt)
            {
                float dt = BattleTime.FxDt;
                foreach (var s in shards)
                {
                    if (t < s.delay) continue;
                    s.vel.y += gravity * dt;
                    s.pos += s.vel * dt;
                    s.rt.anchoredPosition = s.pos;
                    s.rt.localRotation = Quaternion.Euler(0, 0, s.rt.localEulerAngles.z + s.spin * dt);
                    float k = Mathf.Clamp01((t - s.delay) / (Duration - s.delay));
                    float sc = 1f - 0.3f * k;
                    s.rt.localScale = new Vector3(sc, sc, 1f);
                    float shine = 1f + 0.25f * Mathf.Max(0f, 1f - k * 4f);       // 깨진 직후 살짝 반짝
                    s.pic.color = new Color(shine, shine, shine, k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f);
                }
                yield return null;
            }
            Destroy(gameObject);
        }

        void SetOffset(Vector2 o)
        {
            ((RectTransform)whole.transform).anchoredPosition = o;
            lineRoot.anchoredPosition = o;
        }

        void OnDestroy()
        {
            foreach (var o in made) if (o != null) Destroy(o);
        }
    }
}
