using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>화염 속성 팰월드 기술 연출 (그림: PwFx/f_* — 불꽃 색이 그림에 들어 있다)</summary>
    public partial class SkillFx
    {
        static readonly Color FOrange = C(0xff8a2a), FYellow = C(0xffd45a), FRed = C(0xff4a24), FHot = C(0xfff0c0), FBlue = C(0x7fd8ff);

        static partial void RegFire()
        {
            PwFx["Fire_Shot"] = (fx, u, t, a) => fx.FireShot(u, a);
            PwFx["Spirit_Fire"] = (fx, u, t, a) => fx.SpiritFire(u, a);
            PwFx["Fire_Arrow"] = (fx, u, t, a) => fx.FireArrow(u, a);
            PwFx["Fire_Breath"] = (fx, u, t, a) => fx.FireBreath(u, a);
            PwFx["Flame_Storm"] = (fx, u, t, a) => fx.FlameStorm(u, a);
            PwFx["Flame_Wall"] = (fx, u, t, a) => fx.FlameWall(u, a);
            PwFx["Flame_Panel"] = (fx, u, t, a) => fx.FlamePanel(u, a);
            PwFx["Eruption"] = (fx, u, t, a) => fx.Eruption(u, a);
            PwFx["Inferno"] = (fx, u, t, a) => fx.Inferno(u, a);
            PwFx["Fireball"] = (fx, u, t, a) => fx.Fireball(u, a);
            PwFx["Burning_Horn"] = (fx, u, t, a) => fx.BurningHorn(u, a);
            PwFx["Flame_Charge"] = (fx, u, t, a) => fx.FlameCharge(u, a);
            PwFx["Volcanic_Strike"] = (fx, u, t, a) => fx.VolcanicStrike(u, a);
        }

        // ───── 화염 공용 조각 ─────

        /// <summary>연기가 뭉게뭉게 피어오른다</summary>
        void Smoke(Vector3 at, float u, int n = 4)
        {
            for (int i = 0; i < n; i++)
            {
                var p = at + new Vector3(Rnd(-0.4f, 0.4f) * u, Rnd(-0.2f, 0.2f) * u, 0f);
                var end = p + new Vector3(Rnd(-0.3f, 0.3f) * u, Rnd(0.9f, 1.5f) * u, 0f);
                var sr = Ast(Pw + "f_smoke", p, 0.6f * u, Al(Color.white, 0f), 1, Rnd(0, 360));
                float grow = Rnd(1.8f, 2.6f), spin = Rnd(-60f, 60f);
                Anim(sr, Rnd(0.7f, 1f), (s, k) => { s.transform.position = Vector3.Lerp(p, end, Out(k)); Size(s, 0.6f * u * Mathf.Lerp(1f, grow, k)); s.transform.Rotate(0, 0, spin * BattleTime.FxDt); s.color = Al(Color.white, 0.8f * Am * Bell(k)); }, i * 0.05f);
            }
        }

        /// <summary>불씨가 위로 흩날린다</summary>
        void Embers(Vector3 at, float u, int n, float spread = 1f, float rise = 1.4f)
        {
            for (int i = 0; i < n; i++)
            {
                var p = at + new Vector3(Rnd(-0.5f, 0.5f) * spread * u, Rnd(-0.3f, 0.3f) * u, 0f);
                Mote(p, p + new Vector3(Rnd(-0.6f, 0.6f) * u, Rnd(0.6f, 1f) * rise * u, 0f), Rnd(0.12f, 0.22f) * u, Color.white, Rnd(0.4f, 0.7f), Rnd(0f, 0.15f), Pw + "f_ember", 6);
            }
        }

        /// <summary>폭발: 불꽃 구가 부풀며 터지고, 충격 별·불씨·연기가 이어진다</summary>
        void FireBlast(Vector3 at, float size, float u, bool big = false)
        {
            var b = Ast(Pw + "f_blast", at, 0f, Al(Color.white, Am), 6, Rnd(0, 360));
            Anim(b, big ? 0.5f : 0.35f, (s, k) => { Size(s, size * (0.3f + 0.9f * Out(k * 1.6f))); s.transform.Rotate(0, 0, 90f * BattleTime.FxDt); s.color = Al(Color.white, Am * (k < 0.5f ? 1f : (1f - k) * 2f)); });
            Impact(at, size * 0.8f, FYellow, 0.2f);
            Embers(at, u, big ? 12 : 6, size / u * 0.4f);
            Smoke(at, u, big ? 5 : 2);
        }

        /// <summary>상대 몸에 불꽃이 잠깐 날름거린다</summary>
        IEnumerator Burn(PalView v, float u, float seconds = 0.6f, int n = 4)
        {
            if (astMissed || astNoHit) yield break;
            for (int i = 0; i < n; i++)
            {
                var p = v.Center + new Vector3(Rnd(-0.5f, 0.5f) * u, Rnd(-0.6f, 0.2f) * u, 0f);
                var f = Ast(Pw + "f_flame", p, 0.6f * u, Al(Color.white, 0f), 5, 0f, new Vector2(0.5f, 0f));
                float life = seconds * Rnd(0.7f, 1f);
                Anim(f, life, (s, k) => { SizeWH(s, 0.8f * u * (1f + 0.15f * Mathf.Sin(k * 40f)), 1.3f * u * Bell(k)); s.color = Al(Color.white, Am * Bell(k)); }, i * 0.07f);
            }
        }

        // ───── 기술 ─────

        /// <summary>파이어 샷: 입가에 불이 붙었다가, 불씨와 연기를 끌며 도는 불덩이가 날아가 펑 터진다</summary>
        IEnumerator FireShot(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.5f * u;
            var ign = Ast(Pw + "f_flame", mouth, 0f, Color.white, 4, Ang(dir) - 90f, new Vector2(0.5f, 0f));
            for (float t = 0f; t < 0.22f; t += BattleTime.FxDt) { SizeWH(ign, 0.5f * u, 0.8f * u * Out(t / 0.22f) * (1f + 0.2f * Mathf.Sin(t * 60f))); yield return null; }
            Destroy(ign.gameObject);
            GameAudio.Se("hit", 0.6f);
            Run(Recoil(user, dir, 0.2f, 0.15f));
            var ball = Ast(Pw + "f_fireball", mouth, 0.7f * u, Al(Color.white, Am), 5);
            float trail = 0f;
            for (float t = 0f; t < 0.26f; t += BattleTime.FxDt)
            {
                var p = Vector3.Lerp(mouth, aim, t / 0.26f);
                ball.transform.position = p; ball.transform.Rotate(0, 0, -800f * BattleTime.FxDt);
                trail += BattleTime.FxDt;
                if (trail > 0.02f)
                {
                    trail = 0f;
                    Mote(p, p - dir * 0.6f * u + Vector3.up * 0.2f * u, 0.2f * u, Color.white, 0.3f, 0f, Pw + "f_ember", 4);
                    var sm = Ast(Pw + "f_smoke", p, 0.35f * u, Al(Color.white, 0.5f * Am), 2);
                    Anim(sm, 0.4f, (s, k) => { Size(s, 0.35f * u * (1f + k)); s.color = Al(Color.white, 0.5f * Am * (1f - k)); });
                }
                yield return null;
            }
            Destroy(ball.gameObject);
            GameAudio.Se("hit_strong", 0.8f);
            FireBlast(aim, 1.8f * u, u);
            Run(Squash(astTarget, 0.16f));
            Run(Burn(astTarget, u, 0.5f, 3));
            AstHit(0.1f);
            yield return BattleTime.FxWait(0.4f);
        }

        /// <summary>스피릿 파이어: 흔들리며 날아가던 도깨비불이 한가운데서 다섯으로 갈라져, 휘어 날아가 잇달아 터진다</summary>
        IEnumerator SpiritFire(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center;
            var mid = Vector3.Lerp(from, aim, 0.45f) + Vector3.up * 0.8f * u;
            var wisp = Ast(Pw + "f_wisp", from, 0f, Al(Color.white, Am), 5);
            GameAudio.Se("shine", 0.6f);
            for (float t = 0f; t < 0.2f; t += BattleTime.FxDt) { Size(wisp, 1.5f * u * BackOut(t / 0.2f)); yield return null; }
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt)
            {
                float k = t / 0.35f;
                wisp.transform.position = Vector3.Lerp(from, mid, Out(k)) + Vector3.up * Mathf.Sin(k * 12f) * 0.15f * u;
                wisp.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(k * 18f) * 20f);
                if (UnityEngine.Random.value < 0.5f) Embers(wisp.transform.position, u * 0.5f, 1, 0.5f, 0.8f);
                yield return null;
            }
            Destroy(wisp.gameObject);
            GameAudio.Se("select", 0.7f);
            Impact(mid, 1.3f * u, FOrange, 0.18f);
            for (int i = 0; i < 5; i++)
            {
                var w = Ast(Pw + "f_wisp", mid, 0.8f * u, Al(Color.white, Am), 5);
                var ctrl = mid + Dir(-90f + (i - 2) * 45f + 180f) * 1.6f * u + Vector3.up * 1.2f * u;
                var end = aim + new Vector3(Rnd(-0.5f, 0.5f) * u, Rnd(-0.4f, 0.4f) * u, 0f);
                Run(WispFly(w, mid, ctrl, end, 0.32f + i * 0.06f, u, i == 4));
            }
            yield return BattleTime.FxWait(0.7f);
        }

        IEnumerator WispFly(SpriteRenderer w, Vector3 a, Vector3 c, Vector3 b, float time, float u, bool last)
        {
            Vector3 prev = a;
            for (float t = 0f; t < time && w != null; t += BattleTime.FxDt)
            {
                float k = t / time;
                var p = Vector3.Lerp(Vector3.Lerp(a, c, k), Vector3.Lerp(c, b, k), k);
                w.transform.position = p;
                w.transform.rotation = Quaternion.Euler(0, 0, Ang(p - prev) + 90f);
                prev = p;
                if (UnityEngine.Random.value < 0.4f) Mote(p, p + Vector3.up * 0.3f * u, 0.14f * u, Color.white, 0.25f, 0f, Pw + "f_ember", 4);
                yield return null;
            }
            if (w != null) Destroy(w.gameObject);
            GameAudio.Se("hit", 0.6f);
            FireBlast(b, 1.1f * u, u);
            Run(Squash(astTarget, 0.1f, 0.1f));
            if (last) { Run(Burn(astTarget, u)); AstHit(0.1f); }
        }

        /// <summary>불화살: 하늘로 쏜 불화살 셋이 휘어 내려와 잇달아 꽂히고, 꽂힌 자리에서 불꽃이 솟는다</summary>
        IEnumerator FireArrow(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center;
            yield return user.WindUp(aim, 0.14f);
            for (int i = 0; i < 3; i++)
            {
                GameAudio.Se("select", 0.6f);
                var arrow = Ast(Pw + "f_arrow", from, 1.6f * u, Al(Color.white, Am), 5);
                var end = aim + new Vector3((i - 1) * 0.45f * u, Rnd(-0.2f, 0.3f) * u, 0f);
                var ctrl = Vector3.Lerp(from, end, 0.4f) + Vector3.up * (2.2f + i * 0.4f) * u;
                Run(ArrowFly(arrow, from, ctrl, end, 0.4f, u, i == 2));
                yield return BattleTime.FxWait(0.12f);
            }
            yield return BattleTime.FxWait(0.6f);
        }

        IEnumerator ArrowFly(SpriteRenderer arrow, Vector3 a, Vector3 c, Vector3 b, float time, float u, bool last)
        {
            Vector3 prev = a; float trail = 0f;
            for (float t = 0f; t < time && arrow != null; t += BattleTime.FxDt)
            {
                float k = t / time;
                var p = Vector3.Lerp(Vector3.Lerp(a, c, k), Vector3.Lerp(c, b, k), k);
                arrow.transform.position = p;
                arrow.transform.rotation = Quaternion.Euler(0, 0, Ang(p - prev));
                trail += BattleTime.FxDt;
                if (trail > 0.02f) { trail = 0f; Mote(p, p + Vector3.up * 0.2f * u, 0.16f * u, Color.white, 0.25f, 0f, Pw + "f_ember", 4); }
                prev = p;
                yield return null;
            }
            if (arrow != null) Destroy(arrow.gameObject);
            GameAudio.Se(last ? "hit_strong" : "hit", 0.8f);
            Impact(b, 1.3f * u, FOrange, 0.18f);
            var pillar = Ast(Pw + "f_flame", b, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0.1f));
            Anim(pillar, 0.45f, (s, k) => SizeWH(s, 0.9f * u * (1f + 0.15f * Mathf.Sin(k * 50f)), 2f * u * Bell(k)));
            Embers(b, u, 5);
            Run(Squash(astTarget, last ? 0.2f : 0.1f));
            if (last) { Run(Burn(astTarget, u)); AstHit(0.12f); }
        }

        /// <summary>파이어 브레스: 불씨를 빨아들여 숨을 모았다가, 일렁이는 화염을 길게 내뿜어 상대를 불길로 감싼다</summary>
        IEnumerator FireBreath(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.55f * u;
            Run(DimTo(0.35f, 0.25f));
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt)
            {
                if (UnityEngine.Random.value < 0.6f) Mote(mouth + Dir(Rnd(0, 360)) * 1.3f * u, mouth, 0.16f * u, Color.white, 0.22f, 0f, Pw + "f_ember", 4);
                yield return null;
            }
            GameAudio.Se("hit", 0.7f);
            float len = Vector3.Distance(mouth, aim) + 1.2f * u;
            var cone = Ast(Pw + "f_breath", mouth, 0f, Al(Color.white, Am), 5, Ang(dir), new Vector2(0f, 0.5f));
            var cone2 = Ast(Pw + "f_breath", mouth, 0f, Al(FYellow, 0.6f * Am), 4, Ang(dir), new Vector2(0f, 0.5f));
            const float hold = 0.7f;
            float emb = 0f;
            for (float t = 0f; t < hold + 0.2f; t += BattleTime.FxDt)
            {
                float f = t > hold ? 1f - (t - hold) / 0.2f : 1f, grow = Out(t / 0.12f);
                SizeWH(cone, len * grow, 2.2f * u * (1f + 0.12f * Mathf.Sin(t * 45f)) * f);
                SizeWH(cone2, len * 0.9f * grow, 2.8f * u * (1f + 0.15f * Mathf.Sin(t * 37f + 1f)) * f);
                cone.flipY = Mathf.Repeat(t, 0.1f) < 0.05f;
                emb += BattleTime.FxDt;
                if (emb > 0.04f && t < hold) { emb = 0f; Embers(aim, u, 1, 1f); Mote(mouth, aim + new Vector3(Rnd(-0.5f, 0.5f), Rnd(-0.5f, 0.5f), 0f) * u, 0.2f * u, Color.white, 0.3f, 0f, Pw + "f_ember", 6); }
                if (t > 0.12f && !astHit) { Run(Burn(astTarget, u, 0.9f, 6)); Run(Squash(astTarget, 0.12f, 0.6f)); AstHit(0.1f); }
                yield return null;
            }
            Destroy(cone.gameObject); Destroy(cone2.gameObject);
            Smoke(aim, u, 5);
            yield return BattleTime.FxWait(0.3f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>화염 폭풍: 양옆에서 불 회오리 둘이 솟아 돌며 다가가, 상대 자리에서 하나로 합쳐져 터진다</summary>
        IEnumerator FlameStorm(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, d = aim - from;
            var side = new Vector3(-d.y, d.x, 0f).normalized * 1.2f * u;
            float feet = user.Feet.y;
            GameAudio.Se("shine", 0.6f);
            var t1 = Ast(Pw + "f_tornado", new Vector3(from.x, feet, 0f) + side, 0f, Al(Color.white, Am), 5, 0f, new Vector2(0.5f, 0f));
            var t2 = Ast(Pw + "f_tornado", new Vector3(from.x, feet, 0f) - side, 0f, Al(Color.white, Am), 5, 0f, new Vector2(0.5f, 0f));
            var a1 = t1.transform.position; var a2 = t2.transform.position;
            var target = new Vector3(aim.x, astTarget.Feet.y, 0f);
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt) { float k = BackOut(t / 0.3f); SizeWH(t1, 1.3f * u, 2.6f * u * k); SizeWH(t2, 1.3f * u, 2.6f * u * k); yield return null; }
            for (float t = 0f; t < 0.55f; t += BattleTime.FxDt)
            {
                float k = In(t / 0.55f);
                t1.transform.position = Vector3.Lerp(a1, target, k) + side * 0.3f * Mathf.Sin(k * 9f);
                t2.transform.position = Vector3.Lerp(a2, target, k) - side * 0.3f * Mathf.Sin(k * 9f);
                t1.flipX = t2.flipX = Mathf.Repeat(t, 0.08f) < 0.04f;
                if (UnityEngine.Random.value < 0.5f) { Embers(t1.transform.position + Vector3.up * u, u * 0.5f, 1); Embers(t2.transform.position + Vector3.up * u, u * 0.5f, 1); }
                Dust(t1.transform.position, u * 0.4f, 1, 0.5f);
                yield return null;
            }
            Destroy(t1.gameObject); Destroy(t2.gameObject);
            GameAudio.Se("hit_strong");
            var merged = Ast(Pw + "f_tornado", target, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0f));
            Anim(merged, 0.5f, (s, k) => { SizeWH(s, 1.8f * u * (1f + 0.5f * k), 3.2f * u * Bell(k)); s.flipX = Mathf.Repeat(k, 0.1f) < 0.05f; });
            FireBlast(aim, 2.4f * u, u, true);
            Run(Squash(astTarget, 0.22f));
            Run(Burn(astTarget, u));
            AstHit(0.16f);
            yield return BattleTime.FxWait(0.5f);
        }

        /// <summary>화염 장벽: 상대 발밑 땅이 붉게 갈라지더니, 불길의 벽이 솟아올라 한동안 이글거린다</summary>
        IEnumerator FlameWall(PalView user, Vector3 aim)
        {
            float u = U;
            yield return user.WindUp(aim, 0.15f);
            var feet = new Vector3(aim.x, astMissed ? aim.y - 0.8f * u : astTarget.Feet.y, 0f);
            GameAudio.Se("stat_down", 0.6f);
            var crack = Ast(Pw + "f_crack", feet, 0f, Al(Color.white, Am), 2);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { SizeWH(crack, 3.2f * u * Out(t / 0.35f), 0.5f * u); if (!astMissed) Run(CameraShake(0.02f, 0.03f)); yield return null; }
            GameAudio.Se("hit_strong");
            var back = Ast(Pw + "f_wall", feet, 0f, Al(Color.white, 0.8f * Am), Behind(astTarget), 0f, new Vector2(0.5f, 0f));
            var front = Ast(Pw + "f_wall", feet + Vector3.down * 0.1f * u, 0f, Al(Color.white, 0.9f * Am), 4, 0f, new Vector2(0.5f, 0f));
            front.flipX = true;
            Impact(feet + Vector3.up * 0.6f * u, 2f * u, FOrange, 0.25f);
            const float burn = 0.8f;
            for (float t = 0f; t < burn + 0.25f; t += BattleTime.FxDt)
            {
                float rise = Out(t / 0.15f), fade = t > burn ? 1f - (t - burn) / 0.25f : 1f;
                SizeWH(back, 3.6f * u, 2.6f * u * rise * fade * (1f + 0.08f * Mathf.Sin(t * 30f)));
                SizeWH(front, 3.2f * u, 1.6f * u * rise * fade * (1f + 0.1f * Mathf.Sin(t * 41f)));
                back.flipX = Mathf.Repeat(t, 0.12f) < 0.06f;
                if (UnityEngine.Random.value < 0.6f) Embers(feet + Vector3.up * u, u, 1, 1.6f, 2f);
                if (t > 0.1f && !astHit) { Run(Burn(astTarget, u, 0.8f, 5)); Run(Squash(astTarget, 0.15f)); AstHit(0.12f); }
                yield return null;
            }
            Destroy(back.gameObject); Destroy(front.gameObject);
            Anim(crack, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            Smoke(feet + Vector3.up * 0.8f * u, u, 5);
            yield return BattleTime.FxWait(0.3f);
        }

        /// <summary>플레임 판넬: 몸 둘레에 불덩이들이 줄줄이 생겨나, 기관총처럼 쉴 새 없이 쏟아져 상대를 두들긴다</summary>
        IEnumerator FlamePanel(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var perp = new Vector3(-dir.y, dir.x, 0f);
            const int n = 16;
            var balls = new SpriteRenderer[n];
            var spots = new Vector3[n];
            GameAudio.Se("stat_up", 0.6f);
            for (int i = 0; i < n; i++)
            {
                float lane = (i % 4 - 1.5f) * 0.45f, back = (i / 4) * 0.35f;
                spots[i] = from - dir * (0.3f + back) * u + perp * lane * u + Vector3.up * 0.3f * u;
                balls[i] = Ast(Pw + "f_fireball", spots[i], 0f, Al(Color.white, Am), 5);
                Run(PopIn(balls[i], 0.42f * u, i * 0.02f));
            }
            yield return BattleTime.FxWait(0.45f);
            for (int i = 0; i < n; i++)
            {
                var end = aim + new Vector3(Rnd(-0.6f, 0.6f) * u, Rnd(-0.5f, 0.5f) * u, 0f);
                var b = balls[i];
                GameAudio.Se("select", 0.4f);
                Run(Throw(b, spots[i], end, 0.16f, 0f, -900f));
                Run(DelayThen(0.16f, PanelHit(end, u, i == n - 1)));
                Run(Recoil(user, dir, 0.06f, 0.05f));
                yield return BattleTime.FxWait(0.045f);
            }
            yield return BattleTime.FxWait(0.5f);
        }

        IEnumerator PopIn(SpriteRenderer sr, float size, float delay)
        {
            yield return BattleTime.FxWait(delay);
            for (float t = 0f; t < 0.15f && sr != null; t += BattleTime.FxDt) { Size(sr, size * BackOut(t / 0.15f)); yield return null; }
        }

        IEnumerator PanelHit(Vector3 at, float u, bool last)
        {
            GameAudio.Se("hit", 0.45f);
            var b = Ast(Pw + "f_blast", at, 0f, Al(Color.white, Am), 6, Rnd(0, 360));
            Anim(b, 0.22f, (s, k) => { Size(s, 1f * u * Out(k * 2f)); s.color = Al(Color.white, Am * (1f - k)); });
            Embers(at, u * 0.6f, 2);
            Run(Squash(astTarget, 0.06f, 0.06f));
            if (!astMissed) Run(CameraShake(0.03f, 0.04f));
            if (last) { Impact(at, 2f * u, FOrange); Smoke(at, u, 4); Run(Burn(astTarget, u)); AstHit(0.12f); }
            yield break;
        }

        /// <summary>분화: 땅을 울려 하늘로 화산탄을 쏘아 올리면, 불꼬리를 단 용암 덩어리들이 상대 둘레에 쏟아져 터진다</summary>
        IEnumerator Eruption(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.45f, 0.3f));
            var cam = CamRect();
            var top = user.Center + Vector3.up * user.Height * 0.4f;
            GameAudio.Se("stat_down", 0.7f);
            Run(Squash(user, 0.2f, 0.25f));
            Run(CameraShake(0.08f, 0.35f));
            Dust(user.Feet, u, 6);
            yield return BattleTime.FxWait(0.2f);
            for (int i = 0; i < 6; i++)                                                    // 하늘로 쏘아 올린다
            {
                var rock = Ast(Pw + "f_magma", top, 0.45f * u, Al(Color.white, Am), 5);
                var up = top + new Vector3(Rnd(-1.2f, 1.2f) * u, cam.yMax + 1f - top.y, 0f);
                Run(Throw(rock, top, up, 0.3f, 0f, 500f, false, (s, k) => { if (UnityEngine.Random.value < 0.5f) Mote(s.transform.position, s.transform.position + Vector3.down * 0.3f * u, 0.18f * u, Color.white, 0.3f, 0f, Pw + "f_ember", 4); }));
                GameAudio.Se("select", 0.5f);
                yield return BattleTime.FxWait(0.05f);
            }
            yield return BattleTime.FxWait(0.35f);
            for (int i = 0; i < 9; i++)                                                    // 쏟아진다
            {
                var end = aim + new Vector3(Rnd(-1.4f, 1.4f) * u, Rnd(-0.5f, 0.4f) * u, 0f);
                if (i == 8) end = aim;
                var start = new Vector3(end.x + Rnd(-1f, 1f) * u, cam.yMax + 1f, 0f);
                var rock = Ast(Pw + "f_magma", start, (i == 8 ? 0.9f : 0.5f) * u, Al(Color.white, Am), 5);
                var tail = Ast(Pw + "f_flame", start, 0.5f * u, Al(Color.white, Am), 4, Ang(end - start) + 90f, new Vector2(0.5f, 0f));
                Run(Throw(tail, start, end, 0.22f, 0f, 0f, false, (s, k) => SizeWH(s, 0.5f * u, 1.4f * u)));
                Run(Throw(rock, start, end, 0.22f, 0f, 700f));
                Run(DelayThen(0.22f, EruptHit(end, u, i == 8)));
                yield return BattleTime.FxWait(0.08f);
            }
            yield return BattleTime.FxWait(0.6f);
            yield return DimTo(0f, 0.25f);
        }

        IEnumerator EruptHit(Vector3 at, float u, bool last)
        {
            GameAudio.Se(last ? "hit_strong" : "hit", 0.7f);
            FireBlast(at, (last ? 2.6f : 1.3f) * u, u, last);
            var crack = Ast(Pw + "f_crack", at + Vector3.down * 0.3f * u, 1.2f * u, Al(Color.white, Am), 2, Rnd(-20f, 20f));
            Anim(crack, 0.6f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            if (!astMissed) Run(CameraShake(last ? 0.16f : 0.06f, 0.12f));
            Run(Squash(astTarget, last ? 0.24f : 0.08f));
            if (last) { Run(Burn(astTarget, u)); AstHit(0.18f); }
            yield break;
        }

        /// <summary>인페르노: 상대 둘레의 땅에 불 균열이 잇달아 달아오르고, 시간차로 불기둥이 연쇄 폭발한 뒤 한가운데가 크게 터진다</summary>
        IEnumerator Inferno(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.55f, 0.3f));
            yield return user.WindUp(aim, 0.18f);
            var feet = new Vector3(aim.x, astMissed ? aim.y - 0.8f * u : astTarget.Feet.y, 0f);
            const int n = 6;
            var spots = new Vector3[n];
            var cracks = new SpriteRenderer[n];
            GameAudio.Se("stat_down", 0.6f);
            for (int i = 0; i < n; i++)
            {
                float an = i * 60f * Mathf.Deg2Rad;
                spots[i] = feet + new Vector3(Mathf.Cos(an) * 1.5f * u, Mathf.Sin(an) * 0.45f * u, 0f);
                cracks[i] = Ast(Pw + "f_crack", spots[i], 0f, Al(Color.white, Am), 2, Rnd(-30f, 30f));
                Run(PopIn(cracks[i], 1.1f * u, i * 0.08f));
                yield return BattleTime.FxWait(0.08f);
            }
            var ring = Ast(Pw + "f_ring", feet, 0f, Al(Color.white, 0.8f * Am), 1);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { Size(ring, 3.4f * u * Out(t / 0.35f), 0.32f); ring.transform.Rotate(0, 0, 60f * BattleTime.FxDt); yield return null; }
            for (int i = 0; i < n; i++)
            {
                GameAudio.Se("hit", 0.6f);
                var p = spots[i];
                var pillar = Ast(Pw + "f_flame", p, 0f, Al(Color.white, Am), 5, 0f, new Vector2(0.5f, 0f));
                Anim(pillar, 0.5f, (s, k) => SizeWH(s, 0.9f * u * (1f + 0.15f * Mathf.Sin(k * 40f)), 2.4f * u * Bell(k)));
                FireBlast(p + Vector3.up * 0.4f * u, 1.1f * u, u);
                Destroy(cracks[i].gameObject);
                if (!astMissed) Run(CameraShake(0.05f, 0.08f));
                Run(Squash(astTarget, 0.06f, 0.08f));
                yield return BattleTime.FxWait(0.08f);
            }
            GameAudio.Se("hit_strong");
            ScreenFlash(FOrange, 0.45f, 0.02f, 0.04f, 0.35f);
            FireBlast(aim, 3.2f * u, u, true);
            var big = Ast(Pw + "f_flame", feet, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0f));
            Anim(big, 0.6f, (s, k) => SizeWH(s, 2f * u * (1f + 0.1f * Mathf.Sin(k * 30f)), 4f * u * Bell(k)));
            Anim(ring, 0.4f, (s, k) => { Size(s, 3.4f * u * (1f + 0.4f * k), 0.32f); s.color = Al(Color.white, 0.8f * Am * (1f - k)); });
            Run(Squash(astTarget, 0.26f));
            Run(Burn(astTarget, u, 0.8f, 6));
            AstHit(0.22f);
            yield return BattleTime.FxWait(0.55f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>화염구: 머리 위에서 불씨를 빨아들이며 거대한 불덩이가 부풀고, 아지랑이를 일으키며 내던져져 큰 폭발을 일으킨다</summary>
        IEnumerator Fireball(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.6f, 0.3f));
            var cam = CamRect();
            var top = user.Center + Vector3.up * user.Height * 0.65f;
            top.y = Mathf.Min(top.y, cam.yMax - 1.3f * u);
            var ball = Ast(Pw + "f_fireball", top, 0f, Al(Color.white, Am), 5);
            var halo = Ast(Pw + "f_ring", top, 0f, Al(Color.white, 0.6f * Am), 4);
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.8f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.8f);
                Size(ball, 2.2f * u * k * (1f + 0.06f * Mathf.Sin(t * 30f)));
                Size(halo, 3f * u * k); halo.transform.Rotate(0, 0, -120f * BattleTime.FxDt);
                ball.transform.Rotate(0, 0, 200f * BattleTime.FxDt);
                if (UnityEngine.Random.value < 0.7f) Mote(top + Dir(Rnd(0, 360)) * 2.4f * u, top, 0.2f * u, Color.white, 0.3f, 0f, Pw + "f_ember", 4);
                if (UnityEngine.Random.value < 0.15f) Run(CameraShake(0.02f, 0.05f));
                yield return null;
            }
            Destroy(halo.gameObject);
            GameAudio.Se("hit", 0.8f);
            Run(Recoil(user, aim - user.Center, 0.3f, 0.25f));
            float trail = 0f;
            for (float t = 0f; t < 0.45f; t += BattleTime.FxDt)
            {
                float k = t / 0.45f;
                var p = Vector3.Lerp(top, aim, k * k) + Vector3.up * 1.2f * u * 4f * k * (1f - k);
                ball.transform.position = p; ball.transform.Rotate(0, 0, 500f * BattleTime.FxDt);
                trail += BattleTime.FxDt;
                if (trail > 0.03f) { trail = 0f; Smoke(p, u * 0.7f, 1); Embers(p, u, 2); }
                yield return null;
            }
            Destroy(ball.gameObject);
            GameAudio.Se("hit_strong");
            ScreenFlash(FHot, 0.7f, 0.02f, 0.06f, 0.45f);
            FireBlast(aim, 4f * u, u, true);
            Ring(aim, 0.5f * u, 4.5f * u, Al(FOrange, Am), 0.5f, Pw + "f_ring");
            Ring(aim, 0.5f * u, 3.5f * u, Al(FYellow, Am), 0.45f, Pw + "f_ring", 0.35f, 0.05f);
            Dust(astTarget.Feet, u, 8, 2.2f);
            Run(Squash(astTarget, 0.3f));
            Run(Burn(astTarget, u, 0.9f, 6));
            Run(CameraShake(0.3f, 0.45f));
            AstHit(0.3f);
            yield return BattleTime.FxWait(0.6f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>불타는 뿔 (불페르노): 머리를 숙여 뿔에 불을 붙이고, 불꽃 자취를 남기며 돌진해 불의 초승달로 올려친다</summary>
        IEnumerator BurningHorn(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 dir = (aim - user.Center).normalized;
            yield return user.WindUp(aim, 0.2f);
            var horn = Ast(Pw + "f_flame", user.Center + dir * 0.6f * u, 0f, Al(Color.white, Am), 5, Ang(dir) - 90f, new Vector2(0.5f, 0f));
            for (float t = 0f; t < 0.2f; t += BattleTime.FxDt) { SizeWH(horn, 0.6f * u, 1.1f * u * Out(t / 0.2f)); yield return null; }
            bool going = true;
            Run(FollowShield(horn, user, dir, u * 0.7f, () => going));
            Run(FireTrail(user, () => going));
            SpeedLines(aim, 5f * u, 0.45f, FOrange);
            yield return user.Lunge(aim, 99f, 0.4f, () =>
            {
                GameAudio.Se("hit_strong");
                var cres = Ast(Pw + "f_horn", aim, 0f, Al(Color.white, Am), 7, Ang(dir) + 60f);
                Anim(cres, 0.35f, (s, k) => { Size(s, 2.8f * u * Out(k * 2f)); s.transform.Rotate(0, 0, 200f * BattleTime.FxDt); s.color = Al(Color.white, Am * (1f - k)); });
                FireBlast(aim, 2f * u, u);
                Run(Squash(astTarget, 0.22f));
                Run(Burn(astTarget, u));
                AstHit(0.14f);
            });
            going = false;
            Destroy(horn.gameObject);
            yield return BattleTime.FxWait(0.4f);
        }

        IEnumerator FireTrail(PalView user, System.Func<bool> keep)
        {
            while (keep())
            {
                var p = new Vector3(user.Center.x, user.Feet.y + 0.2f * U, 0f);
                var f = Ast(Pw + "f_flame", p, 0.5f * U, Al(Color.white, 0.9f), 1, 0f, new Vector2(0.5f, 0f));
                Anim(f, 0.4f, (s, k) => { SizeWH(s, 0.5f * U, 0.9f * U * Bell(k)); s.color = Al(Color.white, 0.9f * (1f - k)); });
                Afterimage(user, Al(FOrange, 0.5f), 0.25f);
                yield return BattleTime.FxWait(0.03f);
            }
        }

        /// <summary>화염 돌격 (파이린): 몸에 불의 고리를 두르고 활활 타오르며 돌진해, 지나간 자리에 잔불을 남기고 들이받아 터뜨린다</summary>
        IEnumerator FlameCharge(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.4f, 0.25f));
            GameAudio.Se("stat_up", 0.7f);
            var aura = Ast(Pw + "f_ring", user.Center, 0f, Al(Color.white, Am), 6);
            for (float t = 0f; t < 0.4f; t += BattleTime.FxDt)
            {
                Size(aura, 2.4f * u * BackOut(t / 0.4f)); aura.transform.Rotate(0, 0, 400f * BattleTime.FxDt);
                Embers(user.Center, u, 1, 1.2f);
                yield return null;
            }
            bool going = true;
            Run(FollowShield(aura, user, Vector3.zero, u, () => going));
            Run(FireTrail(user, () => going));
            Run(GroundFire(user, () => going));
            SpeedLines(aim, 6f * u, 0.5f, FOrange);
            yield return user.Lunge(aim, 99f, 0.48f, () =>
            {
                GameAudio.Se("hit_strong");
                ScreenFlash(FOrange, 0.3f, 0.02f, 0.03f, 0.25f);
                FireBlast(aim, 2.8f * u, u, true);
                Ring(aim, 0.5f * u, 3.4f * u, Al(Color.white, Am), 0.4f, Pw + "f_ring");
                Run(Squash(astTarget, 0.26f));
                Run(Burn(astTarget, u, 0.8f, 5));
                AstHit(0.18f);
            });
            going = false;
            Anim(aura, 0.25f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            yield return BattleTime.FxWait(0.6f);
            yield return DimTo(0f, 0.25f);
        }

        IEnumerator GroundFire(PalView user, System.Func<bool> keep)
        {
            while (keep())
            {
                var p = new Vector3(user.Center.x + Rnd(-0.2f, 0.2f) * U, user.Feet.y, 0f);
                var f = Ast(Pw + "f_flame", p, 0.45f * U, Al(Color.white, 0.9f), 0, 0f, new Vector2(0.5f, 0f));
                float life = Rnd(0.6f, 0.9f);
                Anim(f, life, (s, k) => { SizeWH(s, 0.45f * U * (1f + 0.15f * Mathf.Sin(k * 50f)), 0.8f * U * Bell(k)); s.color = Al(Color.white, 0.9f * Bell(k)); });
                yield return BattleTime.FxWait(0.04f);
            }
        }

        /// <summary>화산의 일격 (만티파이어): 주황 잔상을 끌며 파고든 뒤, 멈춘 자리의 땅이 갈라지며 불기둥과 용암 덩어리가 솟구친다</summary>
        IEnumerator VolcanicStrike(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.5f, 0.25f));
            bool going = true;
            Run(AfterimageTrail(user, () => going));
            Run(FireTrail(user, () => going));
            SpeedLines(aim, 6f * u, 0.5f, FRed);
            yield return user.Lunge(aim, 99f, 0.42f, () => Run(VolcanoAt(aim, u)));
            going = false;
            yield return BattleTime.FxWait(0.9f);
            yield return DimTo(0f, 0.25f);
        }

        IEnumerator VolcanoAt(Vector3 aim, float u)
        {
            GameAudio.Se("hit", 0.8f);
            Impact(aim, 1.6f * u, FOrange, 0.18f);
            var feet = new Vector3(aim.x, astMissed ? aim.y - 0.8f * u : astTarget.Feet.y, 0f);
            var crack = Ast(Pw + "f_crack", feet, 0f, Al(Color.white, Am), 2);
            for (float t = 0f; t < 0.2f; t += BattleTime.FxDt) { SizeWH(crack, 3f * u * Out(t / 0.2f), 0.6f * u); yield return null; }
            GameAudio.Se("hit_strong");
            ScreenFlash(FRed, 0.35f, 0.02f, 0.04f, 0.3f);
            var pillar = Ast(Pw + "f_flame", feet, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0f));
            Anim(pillar, 0.65f, (s, k) => SizeWH(s, 1.8f * u * (1f + 0.12f * Mathf.Sin(k * 40f)), 4.2f * u * Bell(k)));
            Ring(feet, 0.4f * u, 3.6f * u, Al(Color.white, Am), 0.45f, Pw + "f_ring", 0.35f);
            for (int i = 0; i < 7; i++)
            {
                var rock = Ast(Pw + "f_magma", feet, Rnd(0.3f, 0.5f) * u, Al(Color.white, Am), 6);
                var land = feet + new Vector3(Rnd(-2f, 2f) * u, Rnd(-0.2f, 0.2f) * u, 0f);
                Run(Throw(rock, feet, land, Rnd(0.45f, 0.6f), Rnd(1.6f, 2.6f) * u, Rnd(-600f, 600f)));
            }
            FireBlast(aim, 2.6f * u, u, true);
            Dust(feet, u, 8, 2f);
            Run(CameraShake(0.22f, 0.4f));
            Run(Squash(astTarget, 0.28f));
            Run(Burn(astTarget, u, 0.8f, 6));
            AstHit(0.22f);
            Anim(crack, 0.6f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
        }
    }
}
