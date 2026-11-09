using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 제로버스 전용 기술 연출. 다른 팰의 연출 시트를 쓰지 않고, Resources/UI/AstralymFx의 텍스처
    /// (은하·웜홀·성운·빛살·충격파·포격·꽃잎·유성…)를 코드로 움직여 그린다.
    /// 최종보스답게 큰 기술은 주위를 어둡게 가라앉히고(배경만, 팰은 밝게 남는다), 화면 전체를 번쩍이고, 크게 흔든다.
    ///  1페이즈: 별의 비 / 아스트랄 글라이딩 / 웜홀 / 팔디움 캐논
    ///  2페이즈: 디바인 레이 / 천상의 꽃 / 창조의 종언 / 멸성
    /// </summary>
    public partial class SkillFx
    {
        const string AstDir = "UI/AstralymFx/";
        static readonly Dictionary<string, Sprite> astSprites = new Dictionary<string, Sprite>();

        static readonly Color VoidTeal = C(0x5ef2d6), VoidViolet = C(0xa57bff),
                              StarWhite = C(0xfffaf0), StarGold = C(0xffdf96), StarPink = C(0xff9ad8), StarCyan = C(0x9ff3ff),
                              DyingRed = C(0xff5a7a);

        // 예전 공허 기술 에셋(제로버스만 배움)도 새 연출로 보낸다
        static readonly Dictionary<string, string> AstralymAlias = new Dictionary<string, string>
        {
            ["Void_Shot"] = AstralymPhase.GlideId,
            ["Void_Pulse"] = AstralymPhase.WormHoleId,
            ["Annihilate"] = AstralymPhase.BurstId,
            ["Void_Erosion"] = AstralymPhase.WormHoleId,
        };

        static readonly HashSet<string> AstralymSkills = new HashSet<string>
        {
            AstralymPhase.StarfallId, AstralymPhase.GlideId, AstralymPhase.WormHoleId, AstralymPhase.BurstId,
            AstralymPhase.DivineRayId, AstralymPhase.BloomId, AstralymPhase.CreationEndId, AstralymPhase.ExtinctionId,
        };

        public static bool IsAstralymSkill(SkillData s) => s != null && (AstralymSkills.Contains(s.name) || AstralymAlias.ContainsKey(s.name));

        /// <summary>코드로 직접 그리는 전용 연출이 있는 기술 (제로버스 기술 + 팰월드 기술)</summary>
        public static bool IsCustomSkill(SkillData s) => IsAstralymSkill(s) || (s != null && PwFx.ContainsKey(s.name));

        // 이번 기술 하나를 그리는 동안의 정보
        PalView astTarget;
        bool astMissed, astStrong, astCrit, astHit, astNoHit;
        float U;                                           // 기준 크기 (상대 키에 맞춘 월드 단위)

        // ═════════════════ 도구 ═════════════════

        static Sprite AstSprite(string name, Vector2 pivot)
        {
            string key = name + pivot;
            if (astSprites.TryGetValue(key, out var s) && s != null) return s;
            string path = name.Contains("/") ? "UI/" + name : AstDir + name;      // "PwFx/n_paw" 처럼 폴더를 붙이면 다른 연출 그림 폴더
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) { Debug.LogWarning($"[SkillFx] Resources/{path}.png 가 없습니다."); return null; }
            tex.filterMode = FilterMode.Bilinear;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, 100f, 0, SpriteMeshType.FullRect);
            astSprites[key] = s;
            return s;
        }

        /// <summary>텍스처 하나를 놓는다. width = 월드에서의 가로 길이, sort = 연출 기본 순서에서 더할 값</summary>
        SpriteRenderer Ast(string tex, Vector3 pos, float width, Color color, int sort = 0, float angle = 0f, Vector2? pivot = null)
        {
            var go = new GameObject(tex);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AstSprite(tex, pivot ?? new Vector2(0.5f, 0.5f));
            sr.color = color;
            sr.sortingLayerID = sortingLayer;
            sr.sortingOrder = sortingOrder + sort;
            Size(sr, width);
            return sr;
        }

        /// <summary>어떤 팰보다 뒤에 그릴 때 쓰는 순서 값</summary>
        int Behind(PalView v, int extra = 1) => v.SortingOrder - sortingOrder - extra;

        static float TexW(SpriteRenderer sr) => sr.sprite != null ? sr.sprite.rect.width / 100f : 1f;
        static float TexH(SpriteRenderer sr) => sr.sprite != null ? sr.sprite.rect.height / 100f : 1f;
        static void Size(SpriteRenderer sr, float width, float squash = 1f)
        {
            float s = Mathf.Max(0f, width) / TexW(sr);
            sr.transform.localScale = new Vector3(s, s * squash, 1f);
        }
        /// <summary>가로 길이와 세로 두께(월드)를 따로 정한다</summary>
        static void SizeWH(SpriteRenderer sr, float width, float height)
        {
            sr.transform.localScale = new Vector3(Mathf.Max(0f, width) / TexW(sr), Mathf.Max(0f, height) / TexH(sr), 1f);
        }
        static Color Al(Color c, float a) => new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
        static float Out(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        static float In(float t) { t = Mathf.Clamp01(t); return t * t; }
        static float BackOut(float t) { t = Mathf.Clamp01(t) - 1f; return 1f + t * t * (2.4f * t + 1.4f); }
        static float Bell(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        static Vector3 Dir(float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad), 0f);
        static float Rnd(float a, float b) => UnityEngine.Random.Range(a, b);
        static float Ang(Vector3 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

        /// <summary>sr을 seconds 동안 step(sr, 0~1)으로 움직이고 지운다</summary>
        void Anim(SpriteRenderer sr, float seconds, Action<SpriteRenderer, float> step, float delay = 0f)
        {
            if (sr == null) return;
            if (delay > 0f) sr.enabled = false;
            Run(AnimRoutine(sr, seconds, step, delay));
        }

        IEnumerator AnimRoutine(SpriteRenderer sr, float seconds, Action<SpriteRenderer, float> step, float delay)
        {
            if (delay > 0f) { yield return BattleTime.FxWait(delay); if (sr != null) sr.enabled = true; }
            for (float t = 0f; t < seconds && sr != null; t += BattleTime.FxDt)
            {
                step(sr, t / seconds);
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        /// <summary>빛 알갱이 하나가 from에서 to로 움직이며 사라진다</summary>
        void Mote(Vector3 from, Vector3 to, float size, Color color, float life, float delay = 0f, string tex = "ast_dot", int sort = 2)
        {
            var sr = Ast(tex, from, size, Al(color, 0f), sort, Rnd(0f, 360f));
            float spin = Rnd(-300f, 300f);
            Anim(sr, life, (s, k) =>
            {
                s.transform.position = Vector3.Lerp(from, to, Out(k));
                s.transform.rotation = Quaternion.Euler(0, 0, spin * k * life);
                s.color = Al(color, color.a * Bell(k) * 1.3f);
            }, delay);
        }

        /// <summary>점점 커지며 옅어지는 고리</summary>
        void Ring(Vector3 at, float from, float to, Color color, float life, string tex = "ast_ring", float squash = 1f, float delay = 0f, int sort = 1)
        {
            var sr = Ast(tex, at, from, color, sort);
            Anim(sr, life, (s, k) => { Size(s, Mathf.Lerp(from, to, Out(k)), squash); s.color = Al(color, (1f - k) * color.a); }, delay);
        }

        /// <summary>네 갈래 별빛이 반짝 피었다 진다</summary>
        void Twinkle(Vector3 at, float size, Color color, float life, float delay = 0f, int sort = 6)
        {
            var sr = Ast("ast_star", at, 0f, color, sort);
            float turn = Rnd(-60f, 60f);
            Anim(sr, life, (s, k) => { Size(s, size * Bell(k)); s.transform.rotation = Quaternion.Euler(0, 0, turn * k); }, delay);
        }

        /// <summary>파편들이 사방으로 튄다</summary>
        void Shards(Vector3 at, int n, float dist, float size, Color a, Color b, float life = 0.45f)
        {
            for (int i = 0; i < n; i++)
                Mote(at, at + Dir(i * 360f / n + Rnd(-12f, 12f)) * dist * Rnd(0.6f, 1.2f), size * Rnd(0.7f, 1.3f), i % 2 == 0 ? a : b, life, 0f, "ast_shard", 5);
        }

        /// <summary>바깥으로 뻗는 빛줄기 다발</summary>
        void RayBurst(Vector3 at, int n, float len, Color a, Color b, float life = 0.4f, float alpha = 1f)
        {
            for (int i = 0; i < n; i++)
            {
                float ang = i * 360f / n + Rnd(-8f, 8f);
                var col = Al(i % 2 == 0 ? a : b, alpha);
                var ray = Ast("ast_streak", at, 0f, col, 5, ang, new Vector2(0f, 0.5f));
                float l = len * Rnd(0.7f, 1.15f);
                Anim(ray, life, (s, k) => { Size(s, l * Out(k * 1.4f)); s.color = Al(col, col.a * (1f - k)); });
            }
        }

        /// <summary>맞는 순간: 상대가 움찔하고, 세면 화면이 흔들린다 (한 기술에 한 번)</summary>
        void AstHit(float shake = 0f)
        {
            if (astMissed || astHit || astNoHit) return;
            astHit = true;
            Run(astTarget.HitReact(astStrong));
            float s = Mathf.Max(shake, astStrong ? (astCrit ? 0.18f : 0.12f) : 0f);
            if (s > 0f) Run(CameraShake(s, 0.3f + s));
            if (astCrit) Twinkle(astTarget.Center + Vector3.up * 0.3f * U, 2.4f * U, StarWhite, 0.35f);
        }

        // ───── 화면 전체 번쩍임 (UI까지 덮는 오버레이) ─────
        static Image overlay;

        static Image Overlay()
        {
            if (overlay != null) return overlay;
            var go = new GameObject("AstralymFlash");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var img = new GameObject("Flash").AddComponent<Image>();
            img.transform.SetParent(go.transform, false);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            img.raycastTarget = false;
            img.color = Color.clear;
            overlay = img;
            return img;
        }

        void ScreenFlash(Color c, float peak, float up, float hold, float down) => Run(ScreenFlashRoutine(c, peak, up, hold, down));

        static IEnumerator ScreenFlashRoutine(Color c, float peak, float up, float hold, float down)
        {
            var img = Overlay();
            for (float t = 0f; t < up; t += BattleTime.FxDt) { img.color = Al(c, peak * t / up); yield return null; }
            img.color = Al(c, peak);
            if (hold > 0f) yield return BattleTime.FxWait(hold);
            for (float t = 0f; t < down; t += BattleTime.FxDt) { img.color = Al(c, peak * (1f - Out(t / down))); yield return null; }
            img.color = Color.clear;
        }

        // ───── 주위를 어둡게 (배경·발판만. 팰과 연출은 밝게 남아 도드라진다) ─────
        readonly List<SpriteRenderer> dimList = new List<SpriteRenderer>();
        readonly List<Color> dimOrig = new List<Color>();
        float dimNow;

        void DimCollect(PalView a, PalView b)
        {
            DimRestore();
            var roots = new HashSet<Transform>();
            if (a != null && a.transform.parent != null) roots.Add(a.transform.parent);
            if (b != null && b.transform.parent != null) roots.Add(b.transform.parent);
            foreach (var root in roots)
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null || dimList.Contains(sr)) continue;
                    if (sr.GetComponentInParent<PalView>() != null) continue;       // 팰(과 팰에 붙은 실루엣·기운)은 그대로
                    if (sr.transform.IsChildOf(transform)) continue;                // 연출 자신은 빼고
                    dimList.Add(sr);
                    dimOrig.Add(sr.color);
                }
        }

        void DimApply(float k)
        {
            dimNow = k;
            for (int i = 0; i < dimList.Count; i++)
            {
                if (dimList[i] == null) continue;
                var c = dimOrig[i];
                dimList[i].color = new Color(Mathf.Lerp(c.r, c.r * 0.12f, k), Mathf.Lerp(c.g, c.g * 0.08f, k), Mathf.Lerp(c.b, c.b * 0.22f, k), c.a);
            }
        }

        IEnumerator DimTo(float k, float seconds)
        {
            float from = dimNow;
            for (float t = 0f; t < seconds; t += BattleTime.FxDt) { DimApply(Mathf.Lerp(from, k, Out(t / seconds))); yield return null; }
            DimApply(k);
        }

        void DimRestore()
        {
            for (int i = 0; i < dimList.Count; i++) if (dimList[i] != null) dimList[i].color = dimOrig[i];
            dimList.Clear();
            dimOrig.Clear();
            dimNow = 0f;
        }

        // ───── 잔상 ─────
        static Material solidMat;

        void Afterimage(PalView v, Color color, float life)
        {
            var src = v.Renderer;
            if (src == null || src.sprite == null) return;
            if (solidMat == null) { var sh = Shader.Find("GUI/Text Shader"); if (sh != null) solidMat = new Material(sh); }
            var go = new GameObject("Afterimage");
            go.transform.SetParent(transform, true);
            go.transform.position = src.transform.position;
            go.transform.rotation = src.transform.rotation;
            go.transform.localScale = src.transform.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            if (solidMat != null) sr.sharedMaterial = solidMat;
            sr.sprite = src.sprite;
            sr.flipX = src.flipX;
            sr.sortingLayerID = src.sortingLayerID;
            sr.sortingOrder = src.sortingOrder - 1;
            sr.color = color;
            Anim(sr, life, (s, k) => s.color = Al(color, color.a * (1f - k)));
        }

        static Rect CamRect()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return new Rect(-9f, -5f, 18f, 10f);
            float h = cam.orthographicSize, w = h * cam.aspect;
            var p = cam.transform.position;
            return new Rect(p.x - w, p.y - h, w * 2f, h * 2f);
        }

        // ═════════════════ 재생 ═════════════════

        IEnumerator PlayAstralym(SkillData skill, PalView user, PalView target, bool missed, bool critical, float effectiveness)
        {
            sortingLayer = target.SortingLayerId;
            sortingOrder = Mathf.Max(user.SortingOrder, target.SortingOrder) + SortingAbovePals;
            astTarget = target;
            astMissed = missed;
            astCrit = critical && !missed;
            astStrong = !missed && (critical || effectiveness > 1f || skill.power >= 100);
            astHit = false;
            astNoHit = skill.category == SkillCategory.Status;
            U = Mathf.Clamp(target.Height * 0.55f, 1.4f, 2.6f) * (critical ? 1.12f : !missed && effectiveness < 1f ? 0.9f : 1f);

            Vector3 from = user.Center, to = target.Center;
            Vector3 dir = to - from; dir.z = 0f;
            Vector3 aim = missed ? to + dir.normalized * 1.6f + Vector3.up * 0.8f : to;
            string id = AstralymAlias.TryGetValue(skill.name, out var alias) ? alias : skill.name;

            DimCollect(user, target);
            try
            {
                switch (id)
                {
                    case AstralymPhase.StarfallId:     yield return StarRain(user, aim); break;
                    case AstralymPhase.GlideId:        yield return Glide(user, aim); break;
                    case AstralymPhase.WormHoleId:     yield return WormHole(user, target, aim); break;
                    case AstralymPhase.BurstId:        yield return PaldiumCannon(user, aim); break;
                    case AstralymPhase.DivineRayId:    yield return DivineRay(user, aim); break;
                    case AstralymPhase.BloomId:        yield return CelestialBloom(user); break;
                    case AstralymPhase.CreationEndId:  yield return CreationEnd(user, aim); break;
                    case AstralymPhase.ExtinctionId:   yield return Extinction(user, aim); break;
                    default:
                        if (PwFx.TryGetValue(id, out var pw)) yield return pw(this, user, target, aim);
                        break;
                }
                while (running > 0) yield return null;
            }
            finally
            {
                RestoreSquash();
                DimRestore();
                if (overlay != null) overlay.color = Color.clear;
            }
        }

        // ═════════════════ 1페이즈 ═════════════════

        /// <summary>별의 비: 상대 머리 위에 성운이 소용돌이치고, 그 속에서 유성이 비처럼 쏟아진다</summary>
        IEnumerator StarRain(PalView user, Vector3 aim)
        {
            float u = U;
            GameAudio.Se("shine");
            yield return user.WindUp(aim, 0.15f);
            Run(DimTo(0.55f, 0.3f));
            var cam = CamRect();
            var sky = new Vector3(aim.x, Mathf.Min(aim.y + 3.0f * u, cam.yMax - 0.6f * u), 0f);
            var nebA = Ast("ast_nebula", sky, 0f, Al(StarPink, 0f), -2);
            var nebB = Ast("ast_nebula", sky, 0f, Al(VoidViolet, 0f), -1, 90f);
            const float life = 1.9f;
            Anim(nebA, life, (s, k) => { Size(s, Mathf.Lerp(2f, 5.2f, Out(k * 3f)) * u, 0.55f); s.transform.rotation = Quaternion.Euler(0, 0, 40f * k); s.color = Al(StarPink, 0.75f * Mathf.Min(1f, k * 5f) * Mathf.Min(1f, (1f - k) * 4f)); });
            Anim(nebB, life, (s, k) => { Size(s, Mathf.Lerp(1.6f, 4.4f, Out(k * 3f)) * u, 0.5f); s.transform.rotation = Quaternion.Euler(0, 0, 90f - 55f * k); s.color = Al(VoidViolet, 0.7f * Mathf.Min(1f, k * 5f) * Mathf.Min(1f, (1f - k) * 4f)); });
            for (int i = 0; i < 6; i++) Twinkle(sky + new Vector3(Rnd(-1.8f, 1.8f) * u, Rnd(-0.5f, 0.5f) * u, 0f), Rnd(0.5f, 0.9f) * u, StarWhite, 0.5f, i * 0.06f);
            yield return BattleTime.FxWait(0.35f);

            const int n = 9;
            for (int i = 0; i < n; i++)
            {
                var end = aim + new Vector3(Rnd(-0.8f, 0.8f) * u, Rnd(-0.45f, 0.35f) * u, 0f);
                var start = sky + new Vector3(Rnd(-1.6f, 1.6f) * u, Rnd(-0.2f, 0.2f) * u, 0f);
                Run(Meteor(start, end, i % 3 == 0 ? StarCyan : i % 3 == 1 ? StarPink : StarGold, u, 0.17f, i % 2 == 0));
                yield return BattleTime.FxWait(0.065f);
            }
            yield return BattleTime.FxWait(0.2f);
            // 마지막 큰 한 방
            GameAudio.Se("hit_strong");
            float a = astMissed ? 0.45f : 1f;
            Ring(aim, 0.4f * u, 3.4f * u, Al(StarPink, a), 0.45f, "ast_shock");
            Ring(aim, 0.3f * u, 2.4f * u, Al(StarWhite, a), 0.35f);
            Twinkle(aim, 3.2f * u, Al(StarWhite, a), 0.4f);
            Shards(aim, 12, 2.0f * u, 0.22f * u, StarGold, StarCyan);
            if (!astMissed) ScreenFlash(StarWhite, 0.35f, 0.03f, 0f, 0.25f);
            AstHit(0.18f);
            yield return BattleTime.FxWait(0.35f);
            yield return DimTo(0f, 0.3f);
        }

        IEnumerator Meteor(Vector3 start, Vector3 end, Color tail, float u, float fall, bool sound)
        {
            var d = end - start;
            float ang = Ang(d);
            var streak = Ast("ast_streak", start, 2.4f * u, tail, 3, ang, new Vector2(1f, 0.5f));
            var core = Ast("ast_streak", start, 1.3f * u, StarWhite, 4, ang, new Vector2(1f, 0.5f));
            var head = Ast("ast_star", start, 0.9f * u, StarWhite, 5);
            for (float t = 0f; t < fall; t += BattleTime.FxDt)
            {
                var p = Vector3.Lerp(start, end, In(t / fall));
                streak.transform.position = core.transform.position = head.transform.position = p;
                head.transform.Rotate(0, 0, 900f * BattleTime.FxDt);
                yield return null;
            }
            Destroy(streak.gameObject); Destroy(core.gameObject); Destroy(head.gameObject);
            float a = astMissed ? 0.45f : 1f;
            if (sound) GameAudio.Se("hit", 0.7f);
            Twinkle(end, 1.6f * u, Al(StarWhite, a), 0.28f);
            Ring(end, 0.3f * u, 1.5f * u, Al(tail, a), 0.3f, "ast_shock", 0.45f);
            Shards(end, 4, 0.9f * u, 0.16f * u, StarGold, tail, 0.35f);
            if (!astMissed) Run(CameraShake(0.06f, 0.12f));
        }

        /// <summary>아스트랄 글라이딩: 별빛 잔상을 남기며 미끄러져 파고들어, X자로 두 번 베고 가로로 한 번 더 벤다</summary>
        IEnumerator Glide(PalView user, Vector3 aim)
        {
            float u = U;
            GameAudio.Se("shine");
            Ring(user.Center, 0.4f * u, 2.2f * u, VoidTeal, 0.3f, "ast_shock", 0.5f);
            yield return user.WindUp(aim, 0.16f);
            bool trailing = true;
            Run(AfterimageTrail(user, () => trailing));
            yield return user.Lunge(aim, 99f, 0.44f, () => Run(GlideSlashes(aim, u)));
            trailing = false;
            yield return BattleTime.FxWait(0.3f);
        }

        IEnumerator AfterimageTrail(PalView user, Func<bool> keep)
        {
            int i = 0;
            while (keep())
            {
                Afterimage(user, Al(i++ % 2 == 0 ? VoidTeal : VoidViolet, 0.55f), 0.3f);
                var p = user.Center;
                Mote(p, p + Vector3.down * 0.2f * U + Dir(Rnd(0, 360)) * 0.3f * U, 0.18f * U, StarWhite, 0.3f);
                yield return BattleTime.FxWait(0.03f);
            }
        }

        IEnumerator GlideSlashes(Vector3 c, float u)
        {
            float a = astMissed ? 0.45f : 1f;
            float[] angles = { 38f, -38f, 4f };
            for (int i = 0; i < angles.Length; i++)
            {
                bool last = i == angles.Length - 1;
                GameAudio.Se(last ? "hit_strong" : "hit");
                float ang = angles[i];
                var glow = Ast("ast_line", c, 0f, Al(VoidTeal, 0.9f * a), 4, ang);
                var core = Ast("ast_line", c, 0f, Al(StarWhite, a), 5, ang);
                float len = (last ? 4.2f : 3.4f) * u;
                Anim(glow, 0.3f, (s, k) => { SizeWH(s, len * Out(k * 4f), 0.5f * u * (1f - k)); s.color = Al(VoidTeal, 0.9f * a * (1f - k)); });
                Anim(core, 0.24f, (s, k) => SizeWH(s, len * Out(k * 4f), 0.16f * u * (1f - k)));
                Shards(c, 5, 1.4f * u, 0.18f * u, VoidTeal, StarWhite, 0.35f);
                if (last)
                {
                    var flare = Ast("ast_flare", c, 0f, Al(StarWhite, a), 6, ang);
                    Anim(flare, 0.35f, (s, k) => SizeWH(s, Mathf.Lerp(2f, 6f, Out(k)) * u, 0.6f * u * (1f - k)));
                    Ring(c, 0.4f * u, 2.6f * u, Al(VoidViolet, a), 0.35f, "ast_shock");
                    AstHit(0.16f);
                }
                else if (!astMissed) Run(CameraShake(0.07f, 0.1f));
                yield return BattleTime.FxWait(0.08f);
            }
        }

        /// <summary>웜홀: 상대 뒤에 웜홀이 열려 상대를 삼키고, 머리 위의 다른 웜홀에서 내던져 땅에 꽂는다</summary>
        IEnumerator WormHole(PalView user, PalView target, Vector3 aim)
        {
            float u = U;
            GameAudio.Se("shine");
            yield return user.WindUp(aim, 0.16f);
            Run(DimTo(0.35f, 0.25f));
            int back = Behind(target);
            var c = aim;
            var hole = Ast("ast_wormhole", c, 0f, VoidViolet, back);
            var glow = Ast("ast_ring_soft", c, 0f, Al(VoidTeal, 0.8f), back - 1);
            float spin = 0f;
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt)
            {
                float k = BackOut(t / 0.3f);
                Size(hole, 2.8f * u * k); Size(glow, 3.6f * u * k);
                spin -= 500f * BattleTime.FxDt; hole.transform.rotation = Quaternion.Euler(0, 0, spin);
                yield return null;
            }
            // 빨아들인다
            bool pull = !astMissed && !astNoHit;
            var tr = target.transform;
            Vector3 home = tr.position, scale = tr.localScale;
            float suck = 0f;
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                float k = t / 0.5f;
                spin -= Mathf.Lerp(500f, 1400f, k) * BattleTime.FxDt; hole.transform.rotation = Quaternion.Euler(0, 0, spin);
                suck += BattleTime.FxDt;
                while (suck > 0.025f) { suck -= 0.025f; var p = c + Dir(Rnd(0, 360)) * Rnd(1.6f, 2.4f) * u; Mote(p, c, 0.18f * u, UnityEngine.Random.value < 0.5f ? VoidTeal : StarWhite, 0.3f); }
                if (pull && k > 0.25f)
                {
                    float q = In((k - 0.25f) / 0.75f);
                    tr.position = Vector3.Lerp(home, c, q);
                    tr.localScale = Vector3.Lerp(scale, scale * 0.02f, q);
                    tr.rotation = Quaternion.Euler(0, 0, 540f * q);
                }
                yield return null;
            }
            if (pull) tr.localScale = Vector3.zero;
            GameAudio.Se("stat_down");
            Anim(hole, 0.15f, (s, k) => Size(s, 2.8f * u * (1f - k)));
            Anim(glow, 0.15f, (s, k) => Size(s, 3.6f * u * (1f - k)));
            yield return BattleTime.FxWait(0.22f);

            // 머리 위에 두 번째 웜홀 → 내던진다
            var top = c + Vector3.up * 2.6f * u;
            var hole2 = Ast("ast_wormhole", top, 0f, StarPink, 2);
            for (float t = 0f; t < 0.18f; t += BattleTime.FxDt) { Size(hole2, 2.2f * u * BackOut(t / 0.18f)); hole2.transform.Rotate(0, 0, 700f * BattleTime.FxDt); yield return null; }
            if (pull)
            {
                for (float t = 0f; t < 0.16f; t += BattleTime.FxDt)
                {
                    float k = In(t / 0.16f);
                    tr.position = Vector3.Lerp(top, home, k);
                    tr.localScale = Vector3.Lerp(scale * 0.3f, scale, Out(t / 0.16f));
                    tr.rotation = Quaternion.Euler(0, 0, 360f * (1f - k));
                    hole2.transform.Rotate(0, 0, 700f * BattleTime.FxDt);
                    yield return null;
                }
            }
            tr.position = home; tr.localScale = scale; tr.rotation = Quaternion.identity;
            Anim(hole2, 0.2f, (s, k) => { Size(s, 2.2f * u * (1f - k)); s.transform.Rotate(0, 0, 700f * BattleTime.FxDt); });
            // 땅에 꽂힌다
            GameAudio.Se("hit_strong");
            float a = astMissed ? 0.45f : 1f;
            var ground = astMissed ? aim : Vector3.Lerp(target.Center, target.Feet, 0.6f);
            Ring(ground, 0.5f * u, 3.6f * u, Al(VoidTeal, a), 0.45f, "ast_shock", 0.35f);
            Ring(ground, 0.4f * u, 2.6f * u, Al(StarPink, a), 0.4f, "ast_ring", 0.35f);
            Shards(ground, 10, 1.8f * u, 0.2f * u, VoidViolet, StarWhite);
            AstHit(0.22f);
            yield return BattleTime.FxWait(0.3f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>팔디움 캐논: 마법진과 함께 에너지를 한껏 모은 뒤, 화면을 가로지르는 거대한 포격을 쏜다</summary>
        IEnumerator PaldiumCannon(PalView user, Vector3 aim)
        {
            float u = U, uu = Mathf.Clamp(user.Height * 0.5f, 1.4f, 2.8f);
            Vector3 from = user.Center, dir = aim - from; dir.z = 0f; dir.Normalize();
            var muzzle = from + dir * 0.7f * uu;
            GameAudio.Se("stat_up");
            Run(DimTo(0.7f, 0.35f));
            var sigil = Ast("ast_sigil", muzzle, 0f, Al(VoidTeal, 0f), 3);
            var sigil2 = Ast("ast_sigil", muzzle, 0f, Al(StarPink, 0f), 2);
            var orb = Ast("ast_dot", muzzle, 0f, StarWhite, 5);
            var orbGlow = Ast("ast_dot", muzzle, 0f, Al(VoidTeal, 0.8f), 4);
            const float charge = 0.85f;
            float spawn = 0f;
            for (float t = 0f; t < charge; t += BattleTime.FxDt)
            {
                float k = t / charge;
                Size(sigil, 2.6f * uu * Out(k * 2f)); sigil.color = Al(VoidTeal, Mathf.Min(1f, k * 3f));
                sigil.transform.rotation = Quaternion.Euler(0, 0, 300f * k * k);
                Size(sigil2, 1.7f * uu * Out(k * 2f)); sigil2.color = Al(StarPink, 0.8f * Mathf.Min(1f, k * 3f));
                sigil2.transform.rotation = Quaternion.Euler(0, 0, -420f * k * k);
                float pulse = 1f + 0.12f * Mathf.Sin(t * 40f);
                Size(orb, Mathf.Lerp(0.2f, 1.1f, k) * uu * pulse); Size(orbGlow, Mathf.Lerp(0.5f, 2.4f, k) * uu * pulse);
                spawn += BattleTime.FxDt;
                while (spawn > 0.018f) { spawn -= 0.018f; var p = muzzle + Dir(Rnd(0, 360)) * Rnd(1.8f, 3f) * uu; Mote(p, muzzle, 0.16f * uu, UnityEngine.Random.value < 0.5f ? VoidTeal : StarPink, 0.25f); }
                yield return null;
            }
            // 발사
            GameAudio.Se("hit_strong");
            ScreenFlash(StarWhite, 0.55f, 0.03f, 0.03f, 0.3f);
            Destroy(orb.gameObject);
            Anim(orbGlow, 0.25f, (s, k) => Size(s, 2.4f * uu * (1f - k)));
            Anim(sigil, 0.5f, (s, k) => { s.color = Al(VoidTeal, 1f - k); s.transform.Rotate(0, 0, 600f * BattleTime.FxDt); });
            Anim(sigil2, 0.5f, (s, k) => { s.color = Al(StarPink, 0.8f * (1f - k)); s.transform.Rotate(0, 0, -800f * BattleTime.FxDt); });
            float ang = Ang(dir);
            float len = (aim - muzzle).magnitude + 4f * u;
            var outer = Ast("ast_cannon", muzzle, 0f, Al(VoidTeal, 0.75f), 3, ang, new Vector2(0f, 0.5f));
            var beam = Ast("ast_cannon", muzzle, 0f, StarWhite, 4, ang, new Vector2(0f, 0.5f));
            var muzzleFlare = Ast("ast_flare", muzzle, 0f, StarWhite, 6, ang + 90f);
            const float hold = 0.55f, fadeOut = 0.18f;
            float ringT = 0f;
            for (float t = 0f; t < hold + fadeOut; t += BattleTime.FxDt)
            {
                float grow = Out(t / 0.07f), fade = t > hold ? 1f - (t - hold) / fadeOut : 1f;
                float wob = 1f + 0.12f * Mathf.Sin(t * 70f);
                SizeWH(beam, len * grow, 1.1f * u * wob * fade);
                SizeWH(outer, len * grow, 2.3f * u * wob * fade);
                SizeWH(muzzleFlare, 5f * uu * fade, 0.6f * uu * wob * fade);
                ringT += BattleTime.FxDt;
                if (t > 0.06f && t < hold && ringT > 0.1f)
                {
                    ringT = 0f;
                    Ring(aim, 0.5f * u, 2.8f * u, Al(StarWhite, astMissed ? 0.4f : 0.9f), 0.3f, "ast_shock");
                    Shards(aim, 4, 1.6f * u, 0.2f * u, VoidTeal, StarPink, 0.35f);
                }
                if (t > 0.06f) AstHit(0.28f);
                yield return null;
            }
            Destroy(beam.gameObject); Destroy(outer.gameObject); Destroy(muzzleFlare.gameObject);
            Ring(aim, 0.6f * u, 4.2f * u, Al(VoidViolet, astMissed ? 0.45f : 1f), 0.5f, "ast_shock");
            RayBurst(aim, 10, 2.6f * u, StarWhite, VoidTeal, 0.4f, astMissed ? 0.45f : 1f);
            yield return BattleTime.FxWait(0.3f);
            yield return DimTo(0f, 0.3f);
        }

        // ═════════════════ 2페이즈 ═════════════════

        /// <summary>디바인 레이: 하늘에 빛살이 펼쳐지고, 빛기둥이 상대 주위로 연달아 내리꽂힌 뒤 마지막 거대한 기둥이 정통으로 떨어진다</summary>
        IEnumerator DivineRay(PalView user, Vector3 aim)
        {
            float u = U;
            GameAudio.Se("shine");
            yield return user.WindUp(aim, 0.15f);
            Run(DimTo(0.6f, 0.3f));
            var cam = CamRect();
            float top = cam.yMax + 0.5f;
            var halo = Ast("ast_rays", new Vector3(aim.x, cam.yMax - 0.3f * u, 0f), 0f, Al(StarGold, 0f), -3);
            Anim(halo, 1.6f, (s, k) => { Size(s, Mathf.Lerp(3f, 8f, Out(k * 2f)) * u, 0.6f); s.transform.rotation = Quaternion.Euler(0, 0, 25f * k); s.color = Al(StarGold, 0.7f * Mathf.Min(1f, k * 5f) * Mathf.Min(1f, (1f - k) * 3f)); });
            yield return BattleTime.FxWait(0.25f);
            float[] xs = { -1.5f, 1.3f, -0.7f, 0.9f, 0f };
            for (int i = 0; i < xs.Length; i++)
            {
                bool last = i == xs.Length - 1;
                var p = aim + new Vector3(xs[i] * u, last ? 0f : Rnd(-0.3f, 0.2f) * u, 0f);
                Run(HolyPillar(p, top, last ? 1.9f * u : 0.75f * u, last, u));
                yield return BattleTime.FxWait(last ? 0.5f : 0.13f);
            }
            yield return DimTo(0f, 0.3f);
        }

        IEnumerator HolyPillar(Vector3 p, float top, float width, bool last, float u)
        {
            float a = astMissed ? 0.5f : 1f;
            float h = top - p.y + 0.5f * u;
            var glow = Ast("ast_beam", new Vector3(p.x, top, 0f), 0f, Al(StarGold, 0.7f * a), 3, 0f, new Vector2(0.5f, 1f));
            var beam = Ast("ast_beam", new Vector3(p.x, top, 0f), 0f, Al(StarWhite, a), 4, 0f, new Vector2(0.5f, 1f));
            for (float t = 0f; t < 0.08f; t += BattleTime.FxDt)
            {
                float k = t / 0.08f;
                SizeWH(beam, width, h * k); SizeWH(glow, width * 2.6f, h * k);
                yield return null;
            }
            GameAudio.Se(last ? "hit_strong" : "hit", last ? 1f : 0.7f);
            Ring(p, 0.3f * u, (last ? 4.2f : 2f) * u, Al(StarGold, a), last ? 0.5f : 0.35f, "ast_shock", 0.4f);
            Twinkle(p, (last ? 3.2f : 1.6f) * u, Al(StarWhite, a), 0.35f);
            Shards(p, last ? 12 : 5, (last ? 2.2f : 1.2f) * u, 0.18f * u, StarGold, StarWhite, 0.4f);
            if (last)
            {
                ScreenFlash(StarWhite, 0.6f, 0.02f, 0.05f, 0.4f);
                RayBurst(p, 14, 3.2f * u, StarWhite, StarGold, 0.45f, a);
                AstHit(0.28f);
            }
            else if (!astMissed) Run(CameraShake(0.08f, 0.12f));
            float hold = last ? 0.35f : 0.15f, total = hold + 0.25f, r = hold / total;
            Anim(beam, total, (s, k) => { float f = k < r ? 1f : 1f - (k - r) / (1f - r); SizeWH(s, width * (1f + 0.15f * Mathf.Sin(k * 60f)) * f, h); s.color = Al(StarWhite, a * f); });
            Anim(glow, total + 0.05f, (s, k) => { SizeWH(s, width * 2.6f * (1f + 0.4f * k), h); s.color = Al(StarGold, 0.7f * a * (1f - k)); });
        }

        /// <summary>천상의 꽃: 제로버스 뒤로 별빛의 꽃이 두 겹으로 피어나고, 금빛이 쏟아지며 힘이 차오른다 (자신 강화)</summary>
        IEnumerator CelestialBloom(PalView user)
        {
            float uu = Mathf.Clamp(user.Height * 0.5f, 1.4f, 2.8f);
            Vector3 c = user.Center;
            int back = Behind(user);
            GameAudio.Se("shine");
            yield return user.WindUp(c + Vector3.down, 0.18f);
            var halo = Ast("ast_dot", c, 0f, Al(StarGold, 0f), back - 4);
            var rays = Ast("ast_rays", c, 0f, Al(StarWhite, 0f), back - 5);
            const float life = 1.9f;
            Anim(halo, life, (s, k) => { Size(s, 6f * uu * Out(k * 3f)); s.color = Al(StarGold, 0.55f * Mathf.Min(1f, k * 4f) * Mathf.Min(1f, (1f - k) * 3f)); });
            Anim(rays, life, (s, k) => { Size(s, 7f * uu * Out(k * 2f)); s.transform.rotation = Quaternion.Euler(0, 0, 30f * k); s.color = Al(StarWhite, 0.5f * Mathf.Min(1f, k * 3f) * Mathf.Min(1f, (1f - k) * 3f)); });

            // 꽃잎: 바깥 10장(분홍) + 안쪽 8장(흰빛). 두 겹이 서로 반대로 천천히 돈다
            var petals = new List<(SpriteRenderer sr, float baseAng, float len, Color col, float turn, int idx)>();
            for (int layer = 0; layer < 2; layer++)
            {
                int n = layer == 0 ? 10 : 8;
                float len = (layer == 0 ? 3.0f : 2.0f) * uu;
                var col = layer == 0 ? StarPink : StarWhite;
                for (int i = 0; i < n; i++)
                {
                    float baseAng = i * 360f / n + (layer == 0 ? 0f : 22f);
                    var sr = Ast("ast_petal", c, 0f, Al(col, 0f), back - 3 + layer, baseAng - 90f, new Vector2(0.5f, 0f));
                    petals.Add((sr, baseAng, len, col, layer == 0 ? 1f : -1f, i));
                }
            }
            GameAudio.Se("stat_up");
            const float bloom = 0.6f, stay = 0.7f, scatter = 0.5f;
            float rot = 0f, spawn = 0f;
            bool burst = false;
            for (float t = 0f; t < bloom + stay; t += BattleTime.FxDt)
            {
                rot += BattleTime.FxDt * 18f;
                foreach (var (sr, baseAng, len, col, turn, idx) in petals)
                {
                    float k = Mathf.Max(0f, BackOut((t - idx * 0.025f) / bloom));
                    if (t < idx * 0.025f) k = 0f;
                    SizeWH(sr, len * 0.5f * k, len * k);
                    sr.transform.rotation = Quaternion.Euler(0, 0, baseAng - 90f + rot * turn);
                    sr.color = Al(col, 0.85f * Mathf.Clamp01(t * 4f));
                }
                spawn += BattleTime.FxDt;
                while (spawn > 0.03f)
                {
                    spawn -= 0.03f;
                    var p = c + new Vector3(Rnd(-0.9f, 0.9f) * uu, Rnd(-0.9f, 0.2f) * uu, 0f);
                    Mote(p, p + Vector3.up * Rnd(1.2f, 2f) * uu, Rnd(0.12f, 0.22f) * uu, UnityEngine.Random.value < 0.6f ? StarGold : StarWhite, 0.6f, 0f, UnityEngine.Random.value < 0.3f ? "ast_star" : "ast_dot");
                }
                if (!burst && t > bloom * 0.6f)
                {
                    burst = true;
                    Twinkle(c, 3.2f * uu, StarWhite, 0.5f);
                    Ring(c, 0.5f * uu, 4.4f * uu, StarGold, 0.6f, "ast_shock");
                    Run(user.Flash(StarGold, 2));
                }
                yield return null;
            }
            // 꽃잎이 흩날린다
            foreach (var (sr, baseAng, len, col, turn, idx) in petals)
            {
                var start = sr.transform.position;
                var drift = Dir(baseAng) * Rnd(1.2f, 2.2f) * uu + Vector3.up * Rnd(0.2f, 0.8f) * uu;
                float spin = Rnd(-200f, 200f);
                Anim(sr, scatter, (s, k) => { s.transform.position = start + drift * Out(k); s.transform.Rotate(0, 0, spin * BattleTime.FxDt); s.color = Al(col, 0.85f * (1f - k)); });
            }
            yield return BattleTime.FxWait(scatter);
        }

        /// <summary>창조의 종언: 어둠 속에 은하가 빚어지고, 별들을 삼키며 돌다가 상대 위로 무너져 터진다</summary>
        IEnumerator CreationEnd(PalView user, Vector3 aim)
        {
            float u = U;
            GameAudio.Se("shine");
            yield return user.WindUp(aim, 0.15f);
            Run(DimTo(0.85f, 0.4f));
            var g = aim + Vector3.up * 1.4f * u;
            var galA = Ast("ast_galaxy", g, 0f, Al(StarPink, 0f), 3);
            var galB = Ast("ast_galaxy", g, 0f, Al(StarCyan, 0f), 4, 60f);
            var core = Ast("ast_dot", g, 0f, Al(StarWhite, 0f), 5);
            const float form = 1.0f;
            float spin = 0f, spawn = 0f;
            var cam = CamRect();
            for (float t = 0f; t < form; t += BattleTime.FxDt)
            {
                float k = t / form;
                spin += Mathf.Lerp(60f, 260f, k) * BattleTime.FxDt;
                Size(galA, 5.8f * u * Out(k * 1.5f), 0.62f); galA.transform.rotation = Quaternion.Euler(0, 0, spin);
                Size(galB, 5.0f * u * Out(k * 1.5f), 0.62f); galB.transform.rotation = Quaternion.Euler(0, 0, 60f + spin * 1.2f);
                galA.color = Al(StarPink, 0.9f * Mathf.Min(1f, k * 3f)); galB.color = Al(StarCyan, 0.8f * Mathf.Min(1f, k * 3f));
                Size(core, 1.4f * u * (1f + 0.1f * Mathf.Sin(t * 30f))); core.color = Al(StarWhite, Mathf.Min(1f, k * 3f));
                spawn += BattleTime.FxDt;
                while (spawn > 0.02f)
                {
                    spawn -= 0.02f;
                    var p = new Vector3(Rnd(cam.xMin, cam.xMax), Rnd(cam.yMin, cam.yMax), 0f);
                    Mote(p, g, Rnd(0.1f, 0.2f) * u, UnityEngine.Random.value < 0.5f ? StarWhite : StarGold, 0.45f, 0f, UnityEngine.Random.value < 0.4f ? "ast_star" : "ast_dot");
                }
                yield return null;
            }
            // 무너진다
            GameAudio.Se("stat_down");
            for (float t = 0f; t < 0.32f; t += BattleTime.FxDt)
            {
                float k = In(t / 0.32f);
                spin += 1400f * BattleTime.FxDt;
                var p = Vector3.Lerp(g, aim, k);
                galA.transform.position = galB.transform.position = core.transform.position = p;
                Size(galA, 5.8f * u * (1f - k), 0.62f); galA.transform.rotation = Quaternion.Euler(0, 0, spin);
                Size(galB, 5.0f * u * (1f - k), 0.62f); galB.transform.rotation = Quaternion.Euler(0, 0, spin * 1.2f);
                Size(core, 1.4f * u * (1f - 0.7f * k));
                yield return null;
            }
            Destroy(galA.gameObject); Destroy(galB.gameObject); Destroy(core.gameObject);
            var dot = Ast("ast_void", aim, 0.5f * u, StarPink, 5);
            for (float t = 0f; t < 0.14f; t += BattleTime.FxDt) { Size(dot, 0.5f * u * (1f - 0.5f * t / 0.14f) * (1f + 0.2f * Mathf.Sin(t * 120f))); yield return null; }
            Destroy(dot.gameObject);

            // 폭발
            GameAudio.Se("hit_strong");
            float a = astMissed ? 0.5f : 1f;
            ScreenFlash(StarWhite, 0.85f, 0.02f, 0.06f, 0.55f);
            Ring(aim, 0.4f * u, 6.5f * u, Al(StarPink, a), 0.6f, "ast_shock");
            Ring(aim, 0.3f * u, 5f * u, Al(StarCyan, a), 0.55f, "ast_shock", 1f, 0.08f);
            Ring(aim, 0.3f * u, 3.6f * u, Al(StarGold, a), 0.5f, "ast_ring", 1f, 0.16f);
            var burstRays = Ast("ast_rays", aim, 0f, Al(StarWhite, a), 4);
            Anim(burstRays, 0.7f, (s, k) => { Size(s, 8f * u * Out(k * 2f)); s.transform.rotation = Quaternion.Euler(0, 0, 50f * k); s.color = Al(StarWhite, a * (1f - k)); });
            RayBurst(aim, 18, 4f * u, StarWhite, StarPink, 0.5f, a);
            Shards(aim, 16, 3f * u, 0.24f * u, StarCyan, StarGold, 0.6f);
            AstHit(0.32f);
            yield return BattleTime.FxWait(0.55f);
            yield return DimTo(0f, 0.4f);
        }

        /// <summary>
        /// 멸성: 세상이 캄캄해지고, 전장 위에 별 하나가 태어난다. 별은 세 번 고동치며 붉게 죽어 가다가
        /// 한 점으로 무너지고 — 화면을 하얗게 지우는 초신성이 된다.
        /// </summary>
        IEnumerator Extinction(PalView user, Vector3 aim)
        {
            float u = U;
            var cam = CamRect();
            GameAudio.Se("stat_down");
            yield return DimTo(0.95f, 0.5f);
            Run(user.Flash(StarWhite, 3));
            var sc = new Vector3(Mathf.Lerp(user.Center.x, aim.x, 0.5f), Mathf.Min(cam.yMax - 1.8f * u, Mathf.Max(user.Center.y, aim.y) + 1.2f * u), 0f);

            // 별이 태어난다
            GameAudio.Se("shine");
            var rays = Ast("ast_rays", sc, 0f, Al(StarGold, 0f), 2);
            var glow = Ast("ast_dot", sc, 0f, Al(StarGold, 0f), 3);
            var star = Ast("ast_dot", sc, 0f, StarWhite, 5);
            var flare = Ast("ast_star", sc, 0f, StarWhite, 6);
            Color tint = StarGold;
            float rot = 0f, ringT = 0f;
            for (float t = 0f; t < 0.8f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.8f);
                rot += 30f * BattleTime.FxDt;
                Size(star, 1.6f * u * k); Size(glow, 4.5f * u * k); glow.color = Al(tint, 0.7f * k);
                Size(rays, 7f * u * k); rays.color = Al(tint, 0.6f * k); rays.transform.rotation = Quaternion.Euler(0, 0, rot);
                Size(flare, 3.5f * u * k);
                ringT += BattleTime.FxDt;
                if (ringT > 0.2f) { ringT = 0f; Ring(sc, 1f * u, 4f * u, Al(StarGold, 0.7f), 0.5f); }
                yield return null;
            }
            // 세 번 고동치며 붉게 죽어 간다 (금빛 → 분홍 → 핏빛)
            Color[] stages = { StarGold, StarPink, DyingRed, DyingRed };
            for (int beat = 0; beat < 3; beat++)
            {
                GameAudio.Se("hit", 0.8f + beat * 0.1f);
                var fromC = stages[beat]; var toC = stages[beat + 1];
                Ring(sc, 1.2f * u, (4f + beat) * u, Al(toC, 0.9f), 0.45f, "ast_shock");
                if (beat > 0) Run(CameraShake(0.05f + 0.03f * beat, 0.15f));
                float grow = 1f + 0.15f * beat;
                for (float t = 0f; t < 0.32f; t += BattleTime.FxDt)
                {
                    float k = t / 0.32f, pulse = grow * (1f + 0.3f * Bell(k));
                    tint = Color.Lerp(fromC, toC, k);
                    rot += 60f * BattleTime.FxDt;
                    Size(star, 1.6f * u * pulse); Size(glow, 4.5f * u * pulse); glow.color = Al(tint, 0.75f);
                    star.color = Color.Lerp(StarWhite, tint, 0.15f * (beat + k));
                    Size(rays, 7f * u * pulse); rays.color = Al(tint, 0.6f); rays.transform.rotation = Quaternion.Euler(0, 0, rot);
                    Size(flare, 3.5f * u * pulse);
                    yield return null;
                }
            }
            // 한 점으로 무너진다
            GameAudio.Se("stat_down");
            float last = 1.3f;
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt)
            {
                float k = last * (1f - In(t / 0.3f));
                rot += 900f * BattleTime.FxDt;
                Size(star, 1.6f * u * Mathf.Max(0.06f, k)); Size(glow, 4.5f * u * k);
                Size(rays, 7f * u * k); rays.transform.rotation = Quaternion.Euler(0, 0, rot);
                Size(flare, 3.5f * u * Mathf.Max(0.1f, k));
                if (UnityEngine.Random.value < 0.6f) { var p = sc + Dir(Rnd(0, 360)) * Rnd(2f, 4f) * u; Mote(p, sc, 0.18f * u, DyingRed, 0.25f); }
                yield return null;
            }
            Destroy(glow.gameObject); Destroy(rays.gameObject);
            yield return BattleTime.FxWait(0.18f);                                   // 정적
            Destroy(star.gameObject); Destroy(flare.gameObject);

            // 초신성
            GameAudio.Se("hit_strong");
            ScreenFlash(StarWhite, 1f, 0.02f, 0.18f, 0.9f);
            float a = astMissed ? 0.6f : 1f;
            float screen = Mathf.Max(cam.width, cam.height) * 1.2f;
            Ring(sc, 0.5f * u, screen, Al(StarWhite, a), 0.9f, "ast_shock");
            Ring(sc, 0.5f * u, screen * 0.8f, Al(StarPink, a), 0.85f, "ast_shock", 1f, 0.1f);
            Ring(sc, 0.5f * u, screen * 0.6f, Al(DyingRed, a), 0.8f, "ast_ring", 1f, 0.2f);
            var nova = Ast("ast_rays", sc, 0f, Al(StarWhite, a), 4);
            Anim(nova, 1.0f, (s, k) => { Size(s, screen * Out(k * 1.5f)); s.transform.rotation = Quaternion.Euler(0, 0, 40f * k); s.color = Al(StarWhite, a * (1f - k)); });
            RayBurst(sc, 26, screen * 0.55f, StarWhite, StarPink, 0.7f, a);
            Shards(sc, 22, screen * 0.4f, 0.28f * u, StarGold, DyingRed, 0.8f);
            // 별의 잔해가 상대를 덮친다
            Ring(aim, 0.5f * u, 4f * u, Al(DyingRed, a), 0.5f, "ast_shock", 1f, 0.12f);
            Shards(aim, 12, 2.4f * u, 0.22f * u, StarWhite, StarPink, 0.5f);
            Run(CameraShake(0.38f, 0.7f));
            astStrong = !astMissed;
            AstHit(0.38f);
            // 남은 성운
            var rem1 = Ast("ast_nebula", sc, 0f, Al(StarPink, 0f), -2);
            var rem2 = Ast("ast_nebula", sc, 0f, Al(StarCyan, 0f), -1, 120f);
            Anim(rem1, 1.3f, (s, k) => { Size(s, Mathf.Lerp(3f, 7f, Out(k)) * u, 0.7f); s.color = Al(StarPink, 0.6f * Bell(k)); s.transform.rotation = Quaternion.Euler(0, 0, 20f * k); });
            Anim(rem2, 1.3f, (s, k) => { Size(s, Mathf.Lerp(2.5f, 6f, Out(k)) * u, 0.7f); s.color = Al(StarCyan, 0.5f * Bell(k)); s.transform.rotation = Quaternion.Euler(0, 0, 120f - 25f * k); });
            yield return BattleTime.FxWait(0.8f);
            yield return DimTo(0f, 0.6f);
        }
    }
}
