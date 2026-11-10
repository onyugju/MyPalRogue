using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 제로버스 2페이즈 변신 연출 (포켓몬 메가진화처럼):
    ///  ① 주위가 어두워지고 빛줄기가 모여들며 소용돌이치는 빛의 구가 제로버스를 감싼다
    ///  ② 구가 색을 바꾸며 점점 빠르게 돈다
    ///  ③ 구가 돌처럼 굳고, 금이 세 번에 걸쳐 퍼지며 빛이 새어 나온다
    ///  ④ 그 순간 화면이 멈추고(지금 화면을 찍어 둔다) 하얗게 번쩍인 뒤 화면 자체에 금이 간다
    ///  ⑤ 화면 조각이 깨져 흩어지면, 그 뒤에서 이미 바뀐 2페이즈 맵·모습이 드러난다 (swapWorld를 멈춘 화면 뒤에서 부른다)
    /// 텍스처는 Resources/UI/MegaShift (흰색 텍스처에 색을 입혀 쓴다).
    /// </summary>
    public class MegaShift : MonoBehaviour
    {
        const string Dir = "UI/MegaShift/";
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        // 색 (구는 청록 → 분홍 → 보라로 바뀌고, 금은 금빛 주황)
        static readonly Color Cyan = new Color(0.55f, 0.95f, 1f);
        static readonly Color Pink = new Color(1f, 0.55f, 0.88f);
        static readonly Color Violet = new Color(0.72f, 0.55f, 1f);
        static readonly Color CrackGlow = new Color(1f, 0.68f, 0.32f);

        Transform world;
        int layer, order;
        readonly List<SpriteRenderer> rays = new List<SpriteRenderer>();

        public static MegaShift Create()
        {
            var go = new GameObject("MegaShift");
            DontDestroyOnLoad(go);
            return go.AddComponent<MegaShift>();
        }

        static Sprite S(string name, Vector2? pivot = null)
        {
            string key = name + (pivot.HasValue ? "@" + pivot.Value : "");
            if (sprites.TryGetValue(key, out var s) && s != null) return s;
            var tex = Resources.Load<Texture2D>(Dir + name);
            if (tex == null) { Debug.LogWarning($"[PalRogue] 메가 연출 그림이 없습니다: Resources/{Dir}{name}"); return null; }
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot ?? new Vector2(0.5f, 0.5f), 100f);
            sprites[key] = s;
            return s;
        }

        SpriteRenderer Spr(string name, Vector3 pos, float scale, Color color, int sortOffset, Vector2? pivot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(world, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = S(name, pivot);
            sr.color = color;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order + sortOffset;
            return sr;
        }

        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
        static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        static float BackOut(float t) { t = Mathf.Clamp01(t) - 1f; return 1f + t * t * (2.2f * t + 1.2f); }

        /// <param name="boss">변신하는 보스</param>
        /// <param name="player">내 팰 (같이 어두워진다)</param>
        /// <param name="ui">연출 동안 흐리게 감출 화면 UI (정보창). 2페이즈로 바뀌면 다시 보인다</param>
        /// <param name="swapWorld">멈춘 화면 뒤에서 2페이즈로 바꾸는 일 (배경·모습·정보창)</param>
        public IEnumerator Play(PalView boss, PalView player, CanvasGroup[] ui, System.Action swapWorld)
        {
            var cam = Camera.main;
            layer = boss.SortingLayerId;
            order = Mathf.Max(boss.SortingOrder, player != null ? player.SortingOrder : 0) + 10;
            world = new GameObject("MegaShiftWorld").transform;

            Vector3 center = boss.Center;
            float d = Mathf.Max(boss.Height, 2f) * 0.95f;                       // 구 지름 (월드)
            float orbScale = d / 5.12f;                                         // 512px 그림, 100px = 1유닛

            // 주위가 어두워진다: 배경·발판·팰 그림의 색을 어둡게 (연출이 끝나면 원래 색)
            var field = boss.transform.parent;
            var scene = new List<SpriteRenderer>();
            if (field != null) scene.AddRange(field.GetComponentsInChildren<SpriteRenderer>(true));
            if (player != null && !scene.Contains(player.Renderer)) scene.Add(player.Renderer);
            var original = new Color[scene.Count];
            for (int i = 0; i < scene.Count; i++) original[i] = scene[i].color;
            System.Action<float> darken = k =>
            {
                for (int i = 0; i < scene.Count; i++)
                {
                    if (scene[i] == null) continue;
                    var c = original[i];
                    scene[i].color = new Color(Mathf.Lerp(c.r, c.r * 0.18f, k), Mathf.Lerp(c.g, c.g * 0.16f, k), Mathf.Lerp(c.b, c.b * 0.3f, k), c.a);
                }
            };

            // 보스가 하얀 빛의 실루엣이 된다 (구 안에서 비쳐 보인다)
            var silhouette = Silhouette(boss, 1);

            // 구 (빛 + 소용돌이 두 겹 + 굳은 껍질 + 금 세 단계)
            var glow = Spr("orb_glow", center, 0f, A(Cyan, 0f), 2);
            var swA = Spr("orb_swirl_a", center, 0f, A(Cyan, 0f), 3);
            var swB = Spr("orb_swirl_b", center, 0f, A(Pink, 0f), 4);
            var stone = Spr("orb_stone", center, orbScale, A(Color.white, 0f), 5);
            var cracks = new[]
            {
                Spr("orb_cracks_1", center, orbScale, A(CrackGlow, 0f), 6),
                Spr("orb_cracks_2", center, orbScale, A(CrackGlow, 0f), 7),
                Spr("orb_cracks_3", center, orbScale, A(CrackGlow, 0f), 8),
            };
            var bloom = Spr("orb_glow", center, orbScale, A(Color.white, 0f), 9);

            float angA = 0f, angB = 0f, rayTimer = 0f;
            System.Action<float, float, float> spinOrb = (dt, speed, scale) =>
            {
                angA += dt * 220f * speed;
                angB -= dt * 160f * speed;
                swA.transform.rotation = Quaternion.Euler(0, 0, angA);
                swB.transform.rotation = Quaternion.Euler(0, 0, angB);
                glow.transform.localScale = swA.transform.localScale = swB.transform.localScale = Vector3.one * orbScale * scale;
            };

            // ⓪ 숨을 들이쉬듯: 정보창이 사라지고, 주위가 천천히 어두워지며, 제로버스가 하얀 빛으로 물든다
            float bodyShake = 0f;
            var bossHome = boss.transform.position;
            for (float t = 0f; t < 0.75f; t += BattleTime.FxDt)
            {
                float k = Smooth(t / 0.75f);
                darken(k);
                SetUiAlpha(ui, 1f - Smooth(t / 0.45f));
                if (silhouette != null) silhouette.color = A(Color.Lerp(Cyan, Color.white, 0.6f), k * 0.95f);
                bodyShake = 0.015f * d * k;
                boss.transform.position = bossHome + new Vector3(Random.Range(-bodyShake, bodyShake), Random.Range(-bodyShake, bodyShake), 0f);
                yield return null;
            }
            boss.transform.position = bossHome;
            darken(1f);
            SetUiAlpha(ui, 0f);

            // ① 빛이 몸을 감싸며 소용돌이치는 구가 부풀어 오른다 (빛줄기·반짝이가 모여든다)
            GameAudio.Se("shine");
            for (float t = 0f; t < 0.8f; t += BattleTime.FxDt)
            {
                float dt = BattleTime.FxDt;
                float k = Smooth(t / 0.8f);
                glow.color = A(Cyan, 0.9f * Mathf.Clamp01(t / 0.25f));
                swA.color = A(Cyan, k);
                swB.color = A(Violet, 0.8f * k);
                spinOrb(dt, 1f, Mathf.Lerp(0.55f, 1f, BackOut(t / 0.8f)) * (1f + 0.04f * Mathf.Sin(t * 20f)));
                SpawnRays(ref rayTimer, dt, center, d, 0.06f, true);
                yield return null;
            }

            // ② 색을 바꾸며 점점 빠르게 돈다
            GameAudio.Se("stat_up");
            for (float t = 0f; t < 1.4f; t += BattleTime.FxDt)
            {
                float dt = BattleTime.FxDt;
                float k = t / 1.4f;
                Color a = k < 0.5f ? Color.Lerp(Cyan, Pink, k * 2f) : Color.Lerp(Pink, Violet, (k - 0.5f) * 2f);
                Color b = k < 0.5f ? Color.Lerp(Violet, Cyan, k * 2f) : Color.Lerp(Cyan, Pink, (k - 0.5f) * 2f);
                swA.color = A(a, 1f);
                swB.color = A(b, 0.85f);
                glow.color = A(Color.Lerp(a, Color.white, 0.4f), 0.9f);
                spinOrb(dt, 1f + 2.5f * k, 1f + 0.04f * Mathf.Sin(t * (14f + 20f * k)));
                SpawnRays(ref rayTimer, dt, center, d, Mathf.Lerp(0.05f, 0.02f, k), true);
                yield return null;
            }

            // ③ 돌처럼 굳는다
            GameAudio.Se("stat_down");
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                float k = Ease(t / 0.5f);
                stone.color = A(Color.white, k);
                swA.color = A(swA.color, 1f - k);
                swB.color = A(swB.color, 1f - k);
                glow.color = A(Color.Lerp(glow.color, Violet, k), 0.9f * (1f - 0.6f * k));
                spinOrb(BattleTime.FxDt, 3.5f * (1f - k), 1f);
                yield return null;
            }
            ClearRays();
            yield return BattleTime.FxWait(0.15f);

            // 금이 세 번에 걸쳐 퍼진다 (마지막엔 빛이 새어 나온다)
            for (int i = 0; i < cracks.Length; i++)
            {
                GameAudio.Se(i < cracks.Length - 1 ? "hit" : "hit_strong");
                float hold = i < cracks.Length - 1 ? 0.38f : 0.55f;
                for (float t = 0f; t < hold; t += BattleTime.FxDt)
                {
                    float k = Mathf.Clamp01(t / 0.08f);
                    for (int j = 0; j <= i; j++)
                        cracks[j].color = A(Color.Lerp(CrackGlow, Color.white, 0.35f + 0.35f * Mathf.Sin(t * 30f)), j == i ? k : 1f);
                    float jit = (0.05f + 0.03f * i) * (1f - t / hold) * d / 5f;
                    var off = new Vector3(Random.Range(-jit, jit), Random.Range(-jit, jit), 0f);
                    stone.transform.position = center + off;
                    foreach (var c in cracks) c.transform.position = center + off;
                    if (i == cracks.Length - 1)
                    {
                        bloom.color = A(Color.white, 0.6f * t / hold);
                        bloom.transform.localScale = Vector3.one * orbScale * (1f + 0.25f * t / hold);
                        SpawnRays(ref rayTimer, BattleTime.FxDt, center, d, 0.025f, false);
                    }
                    yield return null;
                }
            }

            // ④ 화면이 멈춘다: 지금 화면을 찍고, 그 뒤에서 2페이즈로 바꾼다
            yield return new WaitForEndOfFrame();
            var shot = CaptureScreen();
            // 금이 시작될 자리 = 화면에서 구가 있던 곳 (카메라가 작은 렌더 텍스처에 그려도 맞도록 0~1 비율로 구한다)
            Vector2 crackCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (cam != null)
            {
                var vp = cam.WorldToViewportPoint(center);
                if (!float.IsNaN(vp.x) && !float.IsNaN(vp.y))
                    crackCenter = new Vector2(Mathf.Clamp01(vp.x) * Screen.width, Mathf.Clamp01(vp.y) * Screen.height);
            }
            var glass = ScreenGlass.Create(shot, crackCenter);
            darken(0f);                                                     // 원래 색으로 (2페이즈 세계는 밝게)
            Destroy(world.gameObject);
            if (silhouette != null) Destroy(silhouette.gameObject);
            rays.Clear();
            swapWorld?.Invoke();
            SetUiAlpha(ui, 1f);

            // 깨진 화면 뒤에서: 새 모습은 하얀 빛에 싸여 있다가, 유리가 다 떨어지면 서서히 드러난다
            var reveal = Silhouette(boss, 1);
            if (reveal != null) reveal.color = Color.white;

            GameAudio.Se("hit_strong");
            // 조각이 흩어지기 시작하면(번쩍 0.22 + 금 0.35 + 정적 0.55 뒤) 빛이 걷히며 새 모습이 드러난다
            StartCoroutine(RevealAfter(reveal, boss, 1.12f + 0.3f));
            yield return glass.Shatter();
            Destroy(shot);
            yield return BattleTime.FxWait(0.35f);
        }

        static void SetUiAlpha(CanvasGroup[] ui, float a)
        {
            if (ui == null) return;
            foreach (var g in ui) if (g != null) g.alpha = a;
        }

        static Material silhouetteMat;

        /// <summary>보스와 같은 그림을 단색(흰색)으로 겹쳐 그린다. 애니메이션도 따라간다 (셰이더가 없으면 null)</summary>
        SpriteRenderer Silhouette(PalView boss, int sortOffset)
        {
            if (silhouetteMat == null)
            {
                var shader = Shader.Find("GUI/Text Shader");
                if (shader == null) return null;
                silhouetteMat = new Material(shader);
            }
            var src = boss.Renderer;
            var go = new GameObject("Silhouette");
            go.transform.SetParent(src.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = silhouetteMat;
            sr.sprite = src.sprite;
            sr.flipX = src.flipX;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order + sortOffset;
            sr.color = new Color(1f, 1f, 1f, 0f);
            StartCoroutine(Follow(sr, src));
            return sr;
        }

        static IEnumerator Follow(SpriteRenderer copy, SpriteRenderer src)
        {
            while (copy != null && src != null)
            {
                copy.sprite = src.sprite;
                copy.flipX = src.flipX;
                yield return null;
            }
        }

        IEnumerator RevealAfter(SpriteRenderer reveal, PalView boss, float delay)
        {
            yield return BattleTime.FxWait(delay);
            GameAudio.Se("shine");
            world = new GameObject("MegaShiftReveal").transform;
            var revealCenter = boss.Center;
            float dd = Mathf.Max(boss.Height, 2f);
            for (int i = 0; i < 18; i++) StartCoroutine(Sparkle(revealCenter, dd, false));
            var w = world.gameObject;
            if (reveal != null) yield return FadeOut(reveal, 0f, 0.7f);
            Destroy(w, 1f);
        }

        static IEnumerator FadeOut(SpriteRenderer sr, float delay, float seconds)
        {
            yield return BattleTime.FxWait(delay);
            for (float t = 0f; t < seconds && sr != null; t += BattleTime.FxDt)
            {
                sr.color = new Color(1f, 1f, 1f, 1f - Smooth(t / seconds));
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        /// <summary>
        /// 지금 화면을 찍는다. 찍은 그림을 sRGB 텍스처로 옮겨 담아서, 리니어 색공간 프로젝트에서도
        /// UI에 다시 그렸을 때 색이 밝게 바래지 않게 한다.
        /// </summary>
        static Texture2D CaptureScreen()
        {
            var raw = ScreenCapture.CaptureScreenshotAsTexture();
            var shot = new Texture2D(raw.width, raw.height, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp };
            shot.SetPixels32(raw.GetPixels32());
            shot.Apply();
            Destroy(raw);
            return shot;
        }

        /// <summary>빛줄기: inward면 바깥에서 구로 모여들고, 아니면 구에서 바깥으로 뻗어 나간다</summary>
        void SpawnRays(ref float timer, float dt, Vector3 center, float d, float every, bool inward)
        {
            timer += dt;
            while (timer >= every)
            {
                timer -= every;
                float ang = Random.Range(0f, 360f);
                var dir = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad), 0f);
                var sr = Spr("ray", center, 1f, A(Color.Lerp(Color.white, Random.value < 0.5f ? Cyan : Pink, 0.35f), 0f), inward ? 1 : 10, new Vector2(1f, 0.5f));
                // 그림의 오른쪽 끝(가장 밝은 쪽)이 구를 향하도록
                sr.transform.rotation = Quaternion.Euler(0, 0, inward ? ang + 180f : ang);
                sr.transform.localScale = new Vector3(d * Random.Range(0.25f, 0.45f), Random.Range(0.6f, 1.4f) * d / 6f, 1f);
                rays.Add(sr);
                StartCoroutine(MoveRay(sr, center, dir, d, inward));
                if (Random.value < 0.5f) StartCoroutine(Sparkle(center, d, inward));
            }
        }

        IEnumerator MoveRay(SpriteRenderer sr, Vector3 center, Vector3 dir, float d, bool inward)
        {
            float life = Random.Range(0.28f, 0.42f);
            float from = inward ? d * 2.6f : d * 0.45f, to = inward ? d * 0.45f : d * 2.8f;
            for (float t = 0f; t < life && sr != null; t += BattleTime.FxDt)
            {
                float k = t / life;
                sr.transform.position = center + dir * Mathf.Lerp(from, to, Ease(k));
                sr.color = A(sr.color, Mathf.Sin(k * Mathf.PI) * 0.9f);
                yield return null;
            }
            if (sr != null) { rays.Remove(sr); Destroy(sr.gameObject); }
        }

        IEnumerator Sparkle(Vector3 center, float d, bool inward)
        {
            if (world == null) yield break;
            float ang = Random.Range(0f, Mathf.PI * 2f);
            var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            var sr = Spr("sparkle", center, d / 7f * Random.Range(0.6f, 1.3f), A(Color.white, 0f), 11);
            float life = Random.Range(0.4f, 0.7f);
            float from = inward ? d * Random.Range(1.2f, 2.2f) : d * 0.5f, to = inward ? d * 0.4f : d * Random.Range(1.6f, 2.6f);
            for (float t = 0f; t < life && sr != null; t += BattleTime.FxDt)
            {
                float k = t / life;
                sr.transform.position = center + dir * Mathf.Lerp(from, to, Ease(k));
                sr.transform.rotation = Quaternion.Euler(0, 0, t * 200f);
                sr.color = A(Color.white, Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        void ClearRays()
        {
            foreach (var r in rays) if (r != null) Destroy(r.gameObject);
            rays.Clear();
        }
    }

}

#if UNITY_EDITOR
namespace PalRogue
{
    /// <summary>
    /// (에디터 테스트 판 전용) 2페이즈 변신 연출을 0.1초마다 작은 JPG로 남긴다: 프로젝트/Temp/PhaseShiftFrames.
    /// 연출이 자연스러운지 한 장씩 넘겨 보며 확인하는 용도. Temp 폴더라 유니티를 닫으면 지워진다.
    /// </summary>
    public static class ShiftRecorder
    {
        /// <summary>녹화를 시작한 시각 (Time.unscaledTime) — 다른 곳에서 "몇 초에 무엇이 나왔는지" 적을 때 쓴다</summary>
        public static float StartedAt;
        public static string Folder => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Temp/PhaseShiftFrames"));

        public static IEnumerator Record(System.Func<bool> keepGoing, float interval = 0.1f, float maxSeconds = 150f)
        {
            string dir = Folder;
            if (System.IO.Directory.Exists(dir))
            {
                // 이전 녹화(수천 장)를 지우느라 몇 초 멈추지 않게: 이름만 바꿔 두고 뒤에서 지운다
                string old = dir + "_old_" + System.DateTime.Now.Ticks;
                try { System.IO.Directory.Move(dir, old); System.Threading.Tasks.Task.Run(() => { try { System.IO.Directory.Delete(old, true); } catch { } }); }
                catch { System.IO.Directory.Delete(dir, true); }
            }
            System.IO.Directory.CreateDirectory(dir);
            const int W = 480, H = 270;
            var rt = new RenderTexture(W, H, 0);
            var read = new Texture2D(W, H, TextureFormat.RGB24, false);
            // 확인용 모음 그림: 10장마다 한 칸씩, 5x5칸이 차면 sheet_NN.jpg로 저장
            const int CW = 400, CH = 225, Cols = 5, Rows = 5, Every = 10;
            var crt = new RenderTexture(CW, CH, 0);
            var sheet = new Texture2D(CW * Cols, CH * Rows, TextureFormat.RGB24, false);
            int cell = 0, sheets = 0;
            void ClearSheet() { var px = sheet.GetPixels32(); for (int p = 0; p < px.Length; p++) px[p] = new Color32(0, 0, 0, 255); sheet.SetPixels32(px); }
            void SaveSheet() { sheet.Apply(); System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"sheet_{sheets++:00}.jpg"), sheet.EncodeToJPG(80)); ClearSheet(); cell = 0; }
            ClearSheet();
            int i = 0;
            float started = Time.unscaledTime;
            StartedAt = started;
            while (keepGoing() && Time.unscaledTime - started < maxSeconds)
            {
                yield return new WaitForEndOfFrame();
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                Graphics.Blit(shot, rt);
                if (i % Every == 0) Graphics.Blit(shot, crt);
                Object.Destroy(shot);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                read.Apply();
                if (i % Every == 0)
                {
                    RenderTexture.active = crt;
                    sheet.ReadPixels(new Rect(0, 0, CW, CH), (cell % Cols) * CW, (Rows - 1 - cell / Cols) * CH);
                    if (++cell == Cols * Rows) SaveSheet();
                }
                RenderTexture.active = prev;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"f_{i++:000}_{Time.unscaledTime - started:00.00}.jpg"), read.EncodeToJPG(75));
                for (float w = interval; w > 0f; w -= Time.unscaledDeltaTime) yield return null;
            }
            if (cell > 0) SaveSheet();
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(read);
            crt.Release(); Object.Destroy(crt); Object.Destroy(sheet);
            Debug.Log($"[PalRogue] 2페이즈 연출 프레임 {i}장 저장: {dir}");
        }
    }
}
#endif
