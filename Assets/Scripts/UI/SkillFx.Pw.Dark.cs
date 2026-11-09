using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>어둠 속성 팰월드 기술 연출 (그림: PwFx/d_*)</summary>
    public partial class SkillFx
    {
        static readonly Color DPurple = C(0xa04dff), DViolet = C(0xd9a8ff), DPoison = C(0x9be04a), DCrimson = C(0xff4a7a);

        static partial void RegDark()
        {
            PwFx["Dark_Shot"] = (fx, u, t, a) => fx.DarkShotFx(u, a);
            PwFx["Poison_Shot"] = (fx, u, t, a) => fx.PoisonShotFx(u, a);
            PwFx["Dark_Orb"] = (fx, u, t, a) => fx.DarkOrbFx(u, a);
            PwFx["Dark_Wave"] = (fx, u, t, a) => fx.DarkWaveFx(u, a);
            PwFx["Dark_Cannon"] = (fx, u, t, a) => fx.DarkCannonFx(u, a);
            PwFx["Shadow_Burst"] = (fx, u, t, a) => fx.ShadowBurstFx(u, a);
            PwFx["Dark_Arrow"] = (fx, u, t, a) => fx.DarkArrowFx(u, a);
            PwFx["Poison_Shower"] = (fx, u, t, a) => fx.PoisonShowerFx(u, a);
            PwFx["Spirit_Flame"] = (fx, u, t, a) => fx.SpiritFlameFx(u, a);
            PwFx["Nightmare_Ball"] = (fx, u, t, a) => fx.NightmareBallFx(u, a);
            PwFx["Apocalypse"] = (fx, u, t, a) => fx.ApocalypseFx(u, a);
            PwFx["Dark_Laser"] = (fx, u, t, a) => fx.DarkLaserFx(u, a);
            PwFx["Dark_Wisp"] = (fx, u, t, a) => fx.DarkWispFx(u, a);
            PwFx["Psycho_Gravity"] = (fx, u, t, a) => fx.PsychoGravityFx(u, a);
            PwFx["Poison_Fog"] = (fx, u, t, a) => fx.PoisonFogFx(u, a);
            PwFx["Nightmare_Ray"] = (fx, u, t, a) => fx.NightmareRayFx(u, a);
            PwFx["Flame_Waltz"] = (fx, u, t, a) => fx.FlameWaltzFx(u, a);
            PwFx["Phantom_Gallop"] = (fx, u, t, a) => fx.PhantomGallopFx(u, a);
        }

        // ───── 공용 조각 ─────

        /// <summary>어둠이 터진다: 검보라 별 + 보라 고리 + 그림자 알갱이</summary>
        void DarkBurst(Vector3 at, float size, float u)
        {
            var n = Ast(Pw + "d_nova", at, 0f, Al(Color.white, Am), 7, Rnd(0, 360));
            Anim(n, 0.35f, (s, k) => { Size(s, size * Out(k * 2f)); s.transform.Rotate(0, 0, 200f * BattleTime.FxDt); s.color = Al(Color.white, Am * (1f - k)); });
            Ring(at, 0.2f * size, size * 1.1f, Al(DPurple, Am), 0.35f, Pw + "d_orb", 1f);
            for (int i = 0; i < 6; i++) Mote(at, at + Dir(Rnd(0, 360)) * size * 0.6f, 0.2f * u, Color.white, 0.35f, 0f, Pw + "d_wisp", 6);
        }

        /// <summary>독이 퍼진다: 거품이 보글보글 + 초록 번쩍임</summary>
        void PoisonSplash(Vector3 at, float size, float u)
        {
            for (int i = 0; i < 8; i++)
            {
                var b = Ast(Pw + "d_poison", at, Rnd(0.15f, 0.3f) * u, Al(Color.white, Am), 7);
                Run(Throw(b, at, at + new Vector3(Rnd(-1f, 1f), Rnd(-0.6f, 0.4f), 0f) * size, Rnd(0.35f, 0.5f), Rnd(0.3f, 0.7f) * size));
            }
            var f = Ast(Pw + "d_fog", at, 0f, Al(Color.white, 0.8f * Am), 6, Rnd(0, 360));
            Anim(f, 0.6f, (s, k) => { Size(s, size * 1.6f * Out(k)); s.color = Al(Color.white, 0.8f * Am * (1f - k)); });
            if (!astMissed && astTarget != null) Run(astTarget.Flash(DPoison, 2, 0.08f));
        }

        // ───── 기술 ─────

        /// <summary>다크 샷: 느릿하게 출발한 암흑탄이 점점 빨라지며 꼬리를 늘이고, 꽂히는 순간 검게 터진다</summary>
        IEnumerator DarkShotFx(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center + (aim - user.Center).normalized * 0.5f * u;
            var o = Ast(Pw + "d_orb", from, 0.6f * u, Al(Color.white, Am), 6);
            GameAudio.Se("select", 0.6f);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt)
            {
                float k = t / 0.35f, e = k * k * k;
                o.transform.position = Vector3.Lerp(from, aim, e);
                if (Random.value < 0.6f) { var tr = Ast(Pw + "d_orb", o.transform.position, 0.5f * u * (0.5f + k), Al(DPurple, 0.5f), 5); Anim(tr, 0.2f, (s, kk) => s.color = Al(DPurple, 0.5f * (1f - kk))); }
                yield return null;
            }
            Destroy(o.gameObject);
            GameAudio.Se("hit", 0.8f);
            DarkBurst(aim, 1.6f * u, u);
            Run(Squash(astTarget, 0.14f));
            AstHit(0.06f);
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>독 사격: 보글거리는 독 진흙 덩어리를 철퍽 던져, 독 거품과 독안개가 퍼진다</summary>
        IEnumerator PoisonShotFx(PalView user, Vector3 aim)
        {
            float u = U;
            var b = Ast(Pw + "d_poison", user.Center, 0.7f * u, Al(Color.white, Am), 6);
            GameAudio.Se("select", 0.6f);
            yield return Throw(b, user.Center, aim, 0.3f, 0.8f * u, 0f, false, (s, k) => { if (Random.value < 0.5f) Mote(s.transform.position, s.transform.position + Vector3.down * 0.3f * u, 0.15f * u, Color.white, 0.3f, 0f, Pw + "d_poison", 5); });
            GameAudio.Se("hit", 0.7f);
            PoisonSplash(aim, 1.2f * u, u);
            Run(Squash(astTarget, 0.12f));
            AstHit(0.05f);
            yield return BattleTime.FxWait(0.45f);
        }

        /// <summary>암흑구: 일렁이는 어둠 구체가 둥실둥실 떠가 상대에 닿으면, 안으로 빨아들였다가 터뜨린다</summary>
        IEnumerator DarkOrbFx(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center;
            var o = Ast(Pw + "d_orb", from, 0f, Al(Color.white, Am), 6);
            GameAudio.Se("shine", 0.5f);
            for (float t = 0f; t < 0.2f; t += BattleTime.FxDt) { Size(o, 1f * u * BackOut(t / 0.2f)); yield return null; }
            for (float t = 0f; t < 0.55f; t += BattleTime.FxDt)
            {
                float k = t / 0.55f;
                o.transform.position = Vector3.Lerp(from, aim, k * k * (3f - 2f * k)) + Vector3.up * Mathf.Sin(k * 10f) * 0.18f * u;
                o.transform.Rotate(0, 0, -200f * BattleTime.FxDt);
                yield return null;
            }
            for (float t = 0f; t < 0.15f; t += BattleTime.FxDt) { Size(o, 1f * u * (1f - 0.6f * t / 0.15f)); yield return null; }
            Destroy(o.gameObject);
            GameAudio.Se("hit", 0.8f);
            DarkBurst(aim, 2f * u, u);
            Run(Squash(astTarget, 0.16f));
            AstHit(0.08f);
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>어둠 파장: 부채꼴로 어둠 구체 다섯을 퍼뜨려, 퍼져 나가는 물결처럼 상대를 덮친다</summary>
        IEnumerator DarkWaveFx(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = aim - from, perp = new Vector3(-dir.y, dir.x, 0f).normalized;
            GameAudio.Se("select", 0.7f);
            for (int i = -2; i <= 2; i++)
            {
                var to = aim + perp * i * 0.5f * u;
                var o = Ast(Pw + "d_orb", from, 0.55f * u, Al(Color.white, Am), 6);
                Run(Throw(o, from, to, 0.28f, 0f, 300f, false, (s, k) => Size(s, 0.55f * u * (1f + k * 0.6f))));
                bool hit = Mathf.Abs(i) <= 1, last = i == 1;
                if (hit) Run(DelayThen(0.28f, Do(() => { GameAudio.Se("hit", 0.5f); DarkBurst(to, 1.1f * u, u); Run(Squash(astTarget, 0.08f, 0.08f)); if (last) AstHit(0.08f); })));
            }
            yield return BattleTime.FxWait(0.6f);
        }

        /// <summary>어둠 대포: 어둠을 한 점에 꾹 눌러 담아, 대포처럼 쾅 쏘아 보낸다</summary>
        IEnumerator DarkCannonFx(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var muzzle = from + dir * 0.6f * u;
            var o = Ast(Pw + "d_orb", muzzle, 0f, Al(Color.white, Am), 6);
            for (float t = 0f; t < 0.25f; t += BattleTime.FxDt)
            {
                Size(o, 0.8f * u * (1f - 0.5f * t / 0.25f) + 0.3f * u);
                if (Random.value < 0.6f) Mote(muzzle + Dir(Rnd(0, 360)) * 1.2f * u, muzzle, 0.18f * u, Color.white, 0.18f, 0f, Pw + "d_wisp", 5);
                yield return null;
            }
            GameAudio.Se("hit", 0.9f);
            Run(Recoil(user, dir, 0.25f, 0.15f));
            var bl = Ast(Pw + "d_nova", muzzle, 1f * u, Al(Color.white, Am), 7);
            Anim(bl, 0.15f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            yield return Throw(o, muzzle, aim, 0.12f, 0f, 0f, false, (s, k) => { var tr = Ast(Pw + "d_beam", s.transform.position, 0.6f * u, Al(Color.white, 0.6f), 5, Ang(dir)); Anim(tr, 0.15f, (r, kk) => r.color = Al(Color.white, 0.6f * (1f - kk))); });
            DarkBurst(aim, 1.8f * u, u);
            Run(Squash(astTarget, 0.18f));
            AstHit(0.1f);
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>그림자 폭발: 발밑 그림자가 부풀어 오르더니, 어둠의 힘이 몸 둘레로 확 터져 나가 상대를 덮친다</summary>
        IEnumerator ShadowBurstFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.4f, 0.2f));
            var c = user.Center;
            var sh = Ast(Pw + "d_orb", new Vector3(c.x, user.Feet.y, 0f), 0f, Al(Color.black, 0.8f), 1);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { Size(sh, 2.4f * u * Out(t / 0.35f), 0.3f); yield return null; }
            GameAudio.Se("hit_strong", 0.8f);
            float dist = Vector3.Distance(c, aim);
            var n = Ast(Pw + "d_nova", c, 0f, Al(Color.white, Am), 7);
            Anim(n, 0.45f, (s, k) => { Size(s, (dist * 2f + u) * Out(k)); s.transform.Rotate(0, 0, 120f * BattleTime.FxDt); s.color = Al(Color.white, Am * (1f - k)); });
            Ring(c, 0.5f * u, dist * 2.2f, Al(DPurple, Am), 0.45f, Pw + "d_orb");
            Anim(sh, 0.4f, (s, k) => s.color = Al(Color.black, 0.8f * (1f - k)));
            yield return BattleTime.FxWait(0.2f);
            DarkBurst(aim, 1.6f * u, u);
            Run(Squash(astTarget, 0.18f));
            AstHit(0.1f);
            yield return BattleTime.FxWait(0.35f);
            yield return DimTo(0f, 0.2f);
        }

        /// <summary>어둠 화살: 그림자 화살 넷을 잇달아 쏘면, 저마다 휘어 돌며 상대를 쫓아가 꽂힌다</summary>
        IEnumerator DarkArrowFx(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center;
            for (int i = 0; i < 4; i++)
            {
                GameAudio.Se("select", 0.5f);
                var a = Ast(Pw + "d_arrow", from, 1.3f * u, Al(Color.white, Am), 6);
                var ctrl = Vector3.Lerp(from, aim, 0.5f) + Vector3.up * (i % 2 == 0 ? 1.8f : -1.4f) * u + Vector3.right * Rnd(-0.5f, 0.5f) * u;
                Run(ArrowCurve(a, from, ctrl, aim, 0.35f, u, i == 3));
                yield return BattleTime.FxWait(0.09f);
            }
            yield return BattleTime.FxWait(0.6f);
        }

        IEnumerator ArrowCurve(SpriteRenderer a, Vector3 p0, Vector3 c, Vector3 p1, float time, float u, bool last)
        {
            Vector3 prev = p0;
            for (float t = 0f; t < time && a != null; t += BattleTime.FxDt)
            {
                float k = t / time;
                var p = Vector3.Lerp(Vector3.Lerp(p0, c, k), Vector3.Lerp(c, p1, k), k);
                a.transform.position = p; a.transform.rotation = Quaternion.Euler(0, 0, Ang(p - prev));
                prev = p;
                yield return null;
            }
            if (a != null) Destroy(a.gameObject);
            GameAudio.Se(last ? "hit_strong" : "hit", 0.6f);
            DarkBurst(p1, (last ? 1.6f : 1f) * u, u);
            Run(Squash(astTarget, last ? 0.18f : 0.08f));
            if (last) AstHit(0.1f);
        }

        /// <summary>포이즌 샤워: 독 구슬을 하늘로 한 움큼 흩뿌리면, 사방으로 비처럼 떨어져 독 웅덩이를 남긴다</summary>
        IEnumerator PoisonShowerFx(PalView user, Vector3 aim)
        {
            float u = U;
            var c = user.Center;
            var cam = CamRect();
            GameAudio.Se("select", 0.7f);
            for (int i = 0; i < 16; i++)
            {
                var b = Ast(Pw + "d_poison", c, 0.35f * u, Al(Color.white, Am), 6);
                Run(Throw(b, c, new Vector3(c.x + Rnd(-1.5f, 1.5f) * u, cam.yMax + 0.5f, 0f), 0.3f));
            }
            yield return BattleTime.FxWait(0.35f);
            var g = GroundAt(aim, u);
            for (int i = 0; i < 18; i++)
            {
                bool onTarget = i % 3 == 0;
                var land = onTarget ? aim + (Vector3)(Random.insideUnitCircle * 0.5f * u) : new Vector3(aim.x + Rnd(-3f, 3f) * u, g.y + Rnd(-0.3f, 0.3f) * u, 0f);
                var st = new Vector3(land.x + Rnd(-0.3f, 0.3f) * u, cam.yMax + 0.5f, 0f);
                var b = Ast(Pw + "d_poison", st, Rnd(0.3f, 0.45f) * u, Al(Color.white, Am), 6);
                Run(Throw(b, st, land, 0.25f));
                Run(DelayThen(0.25f, Do(() =>
                {
                    var pud = Ast(Pw + "d_fog", land, 0f, Al(Color.white, 0.8f * Am), 2);
                    Anim(pud, 0.7f, (s, k) => { Size(s, 0.9f * u * Out(k * 3f), 0.35f); s.color = Al(Color.white, 0.8f * Am * (1f - k)); });
                    if (onTarget) { GameAudio.Se("hit", 0.4f); Run(Squash(astTarget, 0.06f, 0.06f)); if (!astHit) { PoisonSplash(aim, 1f * u, u); AstHit(0.08f); } }
                })));
                yield return BattleTime.FxWait(0.03f);
            }
            yield return BattleTime.FxWait(0.6f);
        }

        /// <summary>유령의 불꽃: 원념 서린 도깨비불 셋을 풀어놓으면, 흐느적거리며 상대를 쫓아가 빙글 돈 뒤 덮친다</summary>
        IEnumerator SpiritFlameFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.4f, 0.25f));
            GameAudio.Se("shine", 0.6f);
            for (int i = 0; i < 3; i++)
            {
                var w = Ast(Pw + "d_wisp", user.Center, 0.9f * u, Al(Color.white, Am), 6);
                Run(WispChase(w, user.Center + Dir(90f + (i - 1) * 50f) * 1.2f * u, aim, i * 2.1f, u, i == 2));
                yield return BattleTime.FxWait(0.08f);
            }
            yield return BattleTime.FxWait(1.3f);
            yield return DimTo(0f, 0.25f);
        }

        IEnumerator WispChase(SpriteRenderer w, Vector3 start, Vector3 aim, float ph, float u, bool last)
        {
            var p0 = w.transform.position;
            for (float t = 0f; t < 0.25f && w != null; t += BattleTime.FxDt) { w.transform.position = Vector3.Lerp(p0, start, Out(t / 0.25f)); yield return null; }
            for (float t = 0f; t < 0.9f && w != null; t += BattleTime.FxDt)
            {
                float k = t / 0.9f;
                float an = ph + t * 9f, rad = Mathf.Lerp(1.2f, 0.2f, k);
                var orbit = aim + new Vector3(Mathf.Cos(an) * rad, Mathf.Sin(an) * rad * 0.6f, 0f) * u;
                w.transform.position = Vector3.Lerp(start, orbit, Out(Mathf.Min(1f, k * 2f)));
                w.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 12f + ph) * 20f);
                if (Random.value < 0.3f) Mote(w.transform.position, w.transform.position + Vector3.up * 0.3f * u, 0.2f * u, Al(DViolet, 0.8f), 0.3f, 0f, Pw + "d_wisp", 4);
                yield return null;
            }
            if (w != null) Destroy(w.gameObject);
            GameAudio.Se(last ? "hit_strong" : "hit", 0.6f);
            DarkBurst(aim, (last ? 2f : 1.2f) * u, u);
            Run(Squash(astTarget, last ? 0.2f : 0.1f));
            if (last) AstHit(0.12f);
        }

        /// <summary>악몽의 구체: 등 뒤에 어둠의 마법진을 그리며 거대한 어둠 구슬을 불러내고, 빠르게 쏘아 크게 터뜨린다</summary>
        IEnumerator NightmareBallFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.55f, 0.3f));
            var cam = CamRect();
            var top = user.Center + Vector3.up * user.Height * 0.55f; top.y = Mathf.Min(top.y, cam.yMax - 1.2f * u);
            var rune = Ast(Pw + "d_rune", top, 0f, Al(Color.white, Am), 4);
            var orb = Ast(Pw + "d_orb", top, 0f, Al(Color.white, Am), 6);
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.65f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.65f);
                Size(rune, 3f * u * k); rune.transform.Rotate(0, 0, 90f * BattleTime.FxDt);
                Size(orb, 2f * u * k * (1f + 0.05f * Mathf.Sin(t * 30f))); orb.transform.Rotate(0, 0, -150f * BattleTime.FxDt);
                if (Random.value < 0.6f) Mote(top + Dir(Rnd(0, 360)) * 2.4f * u, top, 0.2f * u, Color.white, 0.3f, 0f, Pw + "d_wisp", 5);
                yield return null;
            }
            GameAudio.Se("hit", 0.9f);
            Anim(rune, 0.3f, (s, k) => { Size(s, 3f * u * (1f + k)); s.color = Al(Color.white, Am * (1f - k)); });
            yield return Throw(orb, top, aim, 0.18f, 0f, -600f);
            GameAudio.Se("hit_strong");
            ScreenFlash(DPurple, 0.4f, 0.02f, 0.04f, 0.3f);
            DarkBurst(aim, 3.4f * u, u);
            Ring(aim, 0.4f * u, 4f * u, Al(DViolet, Am), 0.5f, Pw + "d_orb");
            Run(Squash(astTarget, 0.26f));
            Run(CameraShake(0.2f, 0.35f));
            AstHit(0.2f);
            yield return BattleTime.FxWait(0.5f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>아포칼립스: 상대 발밑에 거대한 어둠의 마법진이 새겨지고, 둘레에서 어둠의 회오리 여럿이 일어나 휘몰아친다</summary>
        IEnumerator ApocalypseFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.7f, 0.35f));
            var g = GroundAt(aim, u);
            var rune = Ast(Pw + "d_rune", g, 0f, Al(Color.white, Am), 1);
            GameAudio.Se("stat_down", 0.7f);
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt) { Size(rune, 4.6f * u * Out(t / 0.5f), 0.32f); rune.transform.Rotate(0, 0, 60f * BattleTime.FxDt); yield return null; }
            const int n = 5;
            var vs = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                float an = i * Mathf.PI * 2f / n;
                var p = g + new Vector3(Mathf.Cos(an) * 1.8f, Mathf.Sin(an) * 0.55f, 0f) * u;
                vs[i] = Ast(Pw + "d_vortex", p, 0f, Al(Color.white, Am), Mathf.Sin(an) > 0 ? Behind(astTarget) : 6, 0f, new Vector2(0.5f, 0.03f));
                GameAudio.Se("hit", 0.4f);
                yield return BattleTime.FxWait(0.06f);
            }
            for (float t = 0f; t < 1f; t += BattleTime.FxDt)
            {
                for (int i = 0; i < n; i++) { SizeWH(vs[i], 1.1f * u, 3.4f * u * Out(Mathf.Min(1f, t / 0.25f)) * (t > 0.8f ? (1f - t) / 0.2f : 1f)); vs[i].flipX = Mathf.Repeat(t + i * 0.03f, 0.08f) < 0.04f; }
                rune.transform.Rotate(0, 0, 120f * BattleTime.FxDt);
                if (Random.value < 0.5f) Mote(g + new Vector3(Rnd(-2f, 2f), 0f, 0f) * u, aim + (Vector3)(Random.insideUnitCircle * 0.5f * u), 0.25f * u, Color.white, 0.3f, 0f, Pw + "d_wisp", 7);
                if (t > 0.3f && !astHit) { AstHit(0.12f); }
                if (t > 0.3f && Random.value < 0.12f) { Run(Squash(astTarget, 0.08f, 0.08f)); DarkBurst(aim + (Vector3)(Random.insideUnitCircle * 0.6f * u), 0.9f * u, u); }
                yield return null;
            }
            foreach (var v in vs) Destroy(v.gameObject);
            GameAudio.Se("hit_strong");
            ScreenFlash(DPurple, 0.45f, 0.02f, 0.04f, 0.3f);
            DarkBurst(aim, 3.6f * u, u);
            Anim(rune, 0.4f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            Run(Squash(astTarget, 0.28f));
            Run(CameraShake(0.22f, 0.4f));
            yield return BattleTime.FxWait(0.5f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>어둠의 레이저: 앞에 어둠의 마법진을 세우고, 그 한가운데로 어둠을 모아 굵은 광선으로 꿰뚫는다</summary>
        IEnumerator DarkLaserFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.6f, 0.3f));
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var front = from + dir * 0.8f * u;
            var rune = Ast(Pw + "d_rune", front, 0f, Al(Color.white, Am), 7);
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.55f; t += BattleTime.FxDt)
            {
                SizeWH(rune, 0.8f * u * Out(t / 0.3f), 2.4f * u * Out(t / 0.3f)); rune.transform.rotation = Quaternion.Euler(0, 0, Ang(dir));
                if (Random.value < 0.7f) Mote(front + Dir(Rnd(0, 360)) * 1.8f * u, front, 0.2f * u, Color.white, 0.22f, 0f, Pw + "d_wisp", 6);
                yield return null;
            }
            GameAudio.Se("hit_strong");
            float len = Vector3.Distance(front, aim) + 1.2f * u;
            var beam = Ast(Pw + "d_beam", front, 0f, Al(Color.white, Am), 8, Ang(dir), new Vector2(0f, 0.5f));
            ScreenFlash(DViolet, 0.35f, 0.02f, 0.04f, 0.3f);
            for (float t = 0f; t < 0.6f; t += BattleTime.FxDt)
            {
                float f = t > 0.45f ? 1f - (t - 0.45f) / 0.15f : 1f;
                SizeWH(beam, len * Out(t / 0.08f), 1.3f * u * (1f + 0.12f * Mathf.Sin(t * 50f)) * f);
                if (Random.value < 0.5f) DarkBurst(aim + (Vector3)(Random.insideUnitCircle * 0.4f * u), 0.8f * u, u);
                if (t > 0.08f && !astHit) { AstHit(0.16f); Run(Squash(astTarget, 0.2f, 0.4f)); }
                yield return null;
            }
            Destroy(beam.gameObject);
            Anim(rune, 0.25f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            yield return BattleTime.FxWait(0.3f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>다크 위스프: 몸에서 어둠 구체 여덟 개가 소용돌이치며 풀려나, 저마다 다른 길로 상대를 쫓아가 연달아 터진다</summary>
        IEnumerator DarkWispFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.6f, 0.3f));
            var c = user.Center;
            GameAudio.Se("stat_up", 0.6f);
            const int n = 8;
            var os = new SpriteRenderer[n];
            for (int i = 0; i < n; i++) os[i] = Ast(Pw + "d_orb", c, 0.55f * u, Al(Color.white, Am), 6);
            for (float t = 0f; t < 0.6f; t += BattleTime.FxDt)
            {
                for (int i = 0; i < n; i++) { float an = t * 8f + i * Mathf.PI * 2f / n; os[i].transform.position = c + new Vector3(Mathf.Cos(an), Mathf.Sin(an) * 0.6f, 0f) * 1.3f * u * Out(t / 0.3f); }
                yield return null;
            }
            for (int i = 0; i < n; i++)
            {
                var p = os[i].transform.position;
                var ctrl = Vector3.Lerp(p, aim, 0.5f) + (Vector3)(Random.insideUnitCircle * 2f * u);
                Run(OrbCurve(os[i], p, ctrl, aim + (Vector3)(Random.insideUnitCircle * 0.4f * u), 0.4f, u, i == n - 1));
                yield return BattleTime.FxWait(0.05f);
            }
            yield return BattleTime.FxWait(0.65f);
            yield return DimTo(0f, 0.3f);
        }

        IEnumerator OrbCurve(SpriteRenderer o, Vector3 a, Vector3 c, Vector3 b, float time, float u, bool last)
        {
            for (float t = 0f; t < time && o != null; t += BattleTime.FxDt)
            {
                float k = t / time;
                o.transform.position = Vector3.Lerp(Vector3.Lerp(a, c, k), Vector3.Lerp(c, b, k), k);
                if (Random.value < 0.4f) { var tr = Ast(Pw + "d_orb", o.transform.position, 0.35f * u, Al(DPurple, 0.5f), 5); Anim(tr, 0.2f, (s, kk) => s.color = Al(DPurple, 0.5f * (1f - kk))); }
                yield return null;
            }
            if (o != null) Destroy(o.gameObject);
            GameAudio.Se(last ? "hit_strong" : "hit", 0.5f);
            DarkBurst(b, (last ? 2.6f : 1.1f) * u, u);
            Run(Squash(astTarget, last ? 0.26f : 0.07f));
            if (last) { ScreenFlash(DPurple, 0.35f, 0.02f, 0.04f, 0.3f); AstHit(0.2f); }
        }

        /// <summary>사이코 그래비티: 상대를 보랏빛 염동력으로 감싸 허공에 둥실 띄웠다가, 그대로 땅바닥에 내리꽂는다</summary>
        IEnumerator PsychoGravityFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.65f, 0.3f));
            GameAudio.Se("stat_up", 0.7f);
            var aura = Ast(Pw + "d_orb", aim, 0f, Al(DViolet, 0.6f * Am), 7);
            var rune = Ast(Pw + "d_rune", GroundAt(aim, u), 0f, Al(Color.white, Am), 1);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { Size(aura, 2.6f * u * Out(t / 0.35f)); Size(rune, 3f * u * Out(t / 0.35f), 0.3f); yield return null; }
            Transform tr = astMissed ? null : astTarget.transform;
            var home = tr != null ? tr.position : Vector3.zero;
            float lift = 1.6f * u;
            for (float t = 0f; t < 0.55f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.55f);
                if (tr != null) tr.position = home + Vector3.up * lift * k + Vector3.right * Mathf.Sin(t * 30f) * 0.03f * u;
                aura.transform.position = aim + Vector3.up * lift * k;
                aura.transform.Rotate(0, 0, 200f * BattleTime.FxDt);
                rune.transform.Rotate(0, 0, 150f * BattleTime.FxDt);
                if (Random.value < 0.5f) Mote(aura.transform.position + Dir(Rnd(0, 360)) * 1.4f * u, aura.transform.position, 0.2f * u, Color.white, 0.25f, 0f, Pw + "d_wisp", 8);
                yield return null;
            }
            yield return BattleTime.FxWait(0.12f);
            GameAudio.Se("select", 0.9f);
            for (float t = 0f; t < 0.12f; t += BattleTime.FxDt) { float k = In(t / 0.12f); if (tr != null) tr.position = home + Vector3.up * lift * (1f - k); aura.transform.position = aim + Vector3.up * lift * (1f - k); yield return null; }
            if (tr != null) tr.position = home;
            GameAudio.Se("hit_strong");
            ScreenFlash(DPurple, 0.45f, 0.02f, 0.04f, 0.3f);
            DarkBurst(GroundAt(aim, u) + Vector3.up * 0.3f * u, 3f * u, u);
            Ring(GroundAt(aim, u), 0.4f * u, 4.4f * u, Al(DViolet, Am), 0.45f, Pw + "d_orb", 0.3f);
            Dust(GroundAt(aim, u), u, 10, 2.2f);
            Destroy(aura.gameObject);
            Anim(rune, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            Run(Squash(astTarget, 0.34f));
            Run(CameraShake(0.3f, 0.45f));
            AstHit(0.28f);
            yield return BattleTime.FxWait(0.55f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>독 안개 (변화기): 앞쪽으로 짙은 독안개를 자욱이 퍼뜨려, 상대를 감싸 서서히 독에 물들인다</summary>
        IEnumerator PoisonFogFx(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center;
            GameAudio.Se("stat_down", 0.6f);
            for (int i = 0; i < 9; i++)
            {
                var p = Vector3.Lerp(from, aim, (i + 1) / 9f) + (Vector3)(Random.insideUnitCircle * 0.6f * u);
                var f = Ast(Pw + "d_fog", from, 0f, Al(Color.white, 0f), 6 + i % 3, Rnd(0, 360));
                float sz = Rnd(1.6f, 2.4f) * u, spin = Rnd(-40f, 40f);
                Anim(f, 1.4f, (s, k) => { s.transform.position = Vector3.Lerp(from, p, Out(k * 2f)); Size(s, sz * Out(k * 2f)); s.transform.Rotate(0, 0, spin * BattleTime.FxDt); s.color = Al(Color.white, 0.75f * Am * Bell(k)); });
                yield return BattleTime.FxWait(0.05f);
            }
            for (float t = 0f; t < 0.8f; t += BattleTime.FxDt)
            {
                if (Random.value < 0.4f) { var p = aim + (Vector3)(Random.insideUnitCircle * 0.8f * u); Mote(p, p + Vector3.up * 0.6f * u, 0.2f * u, Color.white, 0.4f, 0f, Pw + "d_poison", 8); }
                if (t > 0.3f && !astHit) { GameAudio.Se("stat_down", 0.6f); if (!astMissed) Run(astTarget.Flash(DPoison, 3, 0.1f)); AstHit(0f); }
                yield return null;
            }
            yield return BattleTime.FxWait(0.2f);
        }

        /// <summary>악몽의 빛줄기 (벨라누아르 리베로): 모습을 스르르 감췄다가 상대 머리 위에 나타나, 꽃처럼 퍼지는 진홍빛 광선을 쏟아낸다</summary>
        IEnumerator NightmareRayFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.7f, 0.3f));
            var r = user.Renderer;
            var col = r != null ? r.color : Color.white;
            GameAudio.Se("shine", 0.6f);
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt) { if (r != null) r.color = Al(col, 1f - t / 0.3f); if (Random.value < 0.6f) Mote(user.Center + (Vector3)(Random.insideUnitCircle * user.Height * 0.4f), user.Center + Vector3.up * user.Height * 0.6f, 0.25f * u, Color.white, 0.4f, 0f, Pw + "d_wisp", 7); yield return null; }
            var cam = CamRect();
            var above = new Vector3(aim.x, Mathf.Min(aim.y + 2.4f * u, cam.yMax - 1f * u), 0f);
            var ghost = Ast(Pw + "d_bloomray", above, 0f, Al(Color.white, Am), 8);
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.45f; t += BattleTime.FxDt) { Size(ghost, 2.2f * u * BackOut(t / 0.45f)); ghost.transform.Rotate(0, 0, 160f * BattleTime.FxDt); yield return null; }
            GameAudio.Se("hit_strong");
            ScreenFlash(DCrimson, 0.45f, 0.02f, 0.04f, 0.3f);
            for (int i = 0; i < 8; i++)
            {
                float ang = -90f + (i - 3.5f) * 14f;
                var beam = Ast(Pw + "d_beam", above, 0f, Al(DCrimson, Am), 7, ang, new Vector2(0f, 0.5f));
                Anim(beam, 0.6f, (s, k) => { SizeWH(s, 4f * u * Out(k * 4f), 0.6f * u * (1f - k)); }, i * 0.02f);
            }
            var flower = Ast(Pw + "d_bloomray", aim, 0f, Al(Color.white, Am), 9);
            Anim(flower, 0.6f, (s, k) => { Size(s, 4f * u * Out(k * 2f)); s.transform.Rotate(0, 0, 100f * BattleTime.FxDt); s.color = Al(Color.white, Am * (1f - k)); });
            Run(Squash(astTarget, 0.3f));
            Run(CameraShake(0.22f, 0.4f));
            AstHit(0.24f);
            yield return BattleTime.FxWait(0.5f);
            Anim(ghost, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt) { if (r != null) r.color = Al(col, t / 0.3f); yield return null; }
            if (r != null) r.color = col;
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>화염 왈츠 (벨라누아르 리베로): 춤추듯 맴돌며 진홍빛 화염 고리를 겹겹이 퍼뜨리고, 고리에서 떨어진 불꽃들이 상대를 쫓아간다</summary>
        IEnumerator FlameWaltzFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.55f, 0.3f));
            var c = user.Center;
            for (int i = 0; i < 3; i++)
            {
                GameAudio.Se("shine", 0.5f);
                var ring = Ast(Pw + "d_flamering", c, 0f, Al(Color.white, Am), 6, Rnd(0, 360));
                float rr = (1.2f + i * 0.6f) * 2f * u;
                Anim(ring, 0.55f, (s, k) => { Size(s, rr * Out(k)); s.transform.Rotate(0, 0, 150f * BattleTime.FxDt); s.color = Al(Color.white, Am * (1f - k * k)); });
                Run(Squash(user, 0.08f, 0.15f));
                yield return BattleTime.FxWait(0.15f);
            }
            for (int i = 0; i < 5; i++)
            {
                var start = c + Dir(i * 72f) * 1.6f * u;
                var f = Ast(Pw + "d_ghostfire", start, 0.55f * u, Al(DCrimson, Am), 7);
                var ctrl = Vector3.Lerp(start, aim, 0.5f) + Dir(i * 72f) * 1.2f * u;
                Run(FlameHome(f, start, ctrl, aim + (Vector3)(Random.insideUnitCircle * 0.4f * u), 0.45f, u, i == 4));
                yield return BattleTime.FxWait(0.06f);
            }
            yield return BattleTime.FxWait(0.7f);
            yield return DimTo(0f, 0.3f);
        }

        IEnumerator FlameHome(SpriteRenderer f, Vector3 a, Vector3 c, Vector3 b, float time, float u, bool last)
        {
            for (float t = 0f; t < time && f != null; t += BattleTime.FxDt)
            {
                float k = t / time;
                f.transform.position = Vector3.Lerp(Vector3.Lerp(a, c, k), Vector3.Lerp(c, b, k), k);
                f.transform.Rotate(0, 0, 500f * BattleTime.FxDt);
                if (Random.value < 0.5f) Mote(f.transform.position, f.transform.position + Vector3.up * 0.3f * u, 0.2f * u, DCrimson, 0.25f, 0f, Pw + "d_ghostfire", 5);
                yield return null;
            }
            if (f != null) Destroy(f.gameObject);
            GameAudio.Se(last ? "hit_strong" : "hit", 0.5f);
            var b2 = Ast(Pw + "d_flamering", b, 0f, Al(Color.white, Am), 8);
            Anim(b2, 0.3f, (s, k) => { Size(s, (last ? 2.6f : 1.2f) * u * Out(k * 2f)); s.color = Al(Color.white, Am * (1f - k)); });
            Impact(b, (last ? 2f : 1f) * u, DCrimson, 0.16f);
            Run(Squash(astTarget, last ? 0.24f : 0.08f));
            if (last) { if (!astMissed) Run(astTarget.Flash(DCrimson, 2, 0.08f)); AstHit(0.18f); }
        }

        /// <summary>유령의 질주 (마레이스): 유령 말처럼 느릿하게 앞으로 달려 나가며, 흔들리는 푸른 화염구를 잇달아 날린다</summary>
        IEnumerator PhantomGallopFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.5f, 0.3f));
            var tr = user.transform; var home = tr.position;
            var fwd = (aim - user.Center).normalized;
            for (int i = 0; i < 6; i++)
            {
                for (float t = 0f; t < 0.13f; t += BattleTime.FxDt)
                {
                    tr.position = home + fwd * (i * 0.13f + t) / 0.78f * 1.2f * u + Vector3.up * Mathf.Abs(Mathf.Sin((i * 0.13f + t) * 24f)) * 0.12f * u;
                    if (Random.value < 0.3f) Afterimage(user, Al(DViolet, 0.4f), 0.3f);
                    yield return null;
                }
                var from = user.Center + fwd * 0.5f * u;
                var f = Ast(Pw + "d_ghostfire", from, 0.75f * u, Al(Color.white, Am), 7);
                var end = aim + (Vector3)(Random.insideUnitCircle * 0.5f * u);
                bool last = i == 5, first = i == 0;
                Run(Throw(f, from, end, 0.3f, Rnd(-0.4f, 0.4f) * u, 0f, false, (s, k) => { s.transform.position += Vector3.up * Mathf.Sin(k * 20f) * 0.03f * u; if (Random.value < 0.4f) Mote(s.transform.position, s.transform.position + Vector3.up * 0.3f * u, 0.18f * u, Color.white, 0.25f, 0f, Pw + "d_ghostfire", 5); }));
                GameAudio.Se("select", 0.5f);
                Run(DelayThen(0.3f, Do(() => { GameAudio.Se(last ? "hit_strong" : "hit", 0.5f); Impact(end, (last ? 2f : 1f) * u, C(0x7a7aff), 0.16f); Run(Squash(astTarget, last ? 0.22f : 0.07f)); if (last) AstHit(0.16f); else if (first) AstHit(0f); })));
            }
            yield return BattleTime.FxWait(0.4f);
            var at = tr.position;
            for (float t = 0f; t < 0.25f; t += BattleTime.FxDt) { tr.position = Vector3.Lerp(at, home, Out(t / 0.25f)); yield return null; }
            tr.position = home;
            yield return DimTo(0f, 0.3f);
        }
    }
}
