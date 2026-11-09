using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>무속성 팰월드 기술 연출 (그림: PwFx/n_*)</summary>
    public partial class SkillFx
    {
        static readonly Color NWhite = C(0xffffff), NCream = C(0xfff3d6), NSky = C(0xcfe6ff), NHoly = C(0xffe08a), NPink = C(0xffc2dc), NSteel = C(0xdfe7f2);

        static partial void RegNeutral()
        {
            PwFx["Air_Cannon"] = (fx, u, t, a) => fx.AirCannon(u, a);
            PwFx["Double_Fang"] = (fx, u, t, a) => fx.DoubleFang(u, a);
            PwFx["Power_Shot"] = (fx, u, t, a) => fx.PowerShot(u, a);
            PwFx["Power_Bomb"] = (fx, u, t, a) => fx.PowerBomb(u, a);
            PwFx["Air_Blade"] = (fx, u, t, a) => fx.AirBlade(u, a);
            PwFx["Star_Mine"] = (fx, u, t, a) => fx.StarMine(u, a);
            PwFx["Pal_Blast"] = (fx, u, t, a) => fx.PalBlast(u, a);
            PwFx["Radiant_Barrage"] = (fx, u, t, a) => fx.RadiantBarrage(u, a);
            PwFx["Holy_Burst"] = (fx, u, t, a) => fx.HolyBurst(u, a);
            PwFx["Rolling_Fluff"] = (fx, u, t, a) => fx.RollingFluff(u, a);
            PwFx["Meow_Punch"] = (fx, u, t, a) => fx.MeowPunch(u, a);
            PwFx["Lancer_Charge"] = (fx, u, t, a) => fx.LancerCharge(u, a);
            PwFx["Wild_Fang"] = (fx, u, t, a) => fx.WildFang(u, a);
        }

        /// <summary>공기 대포: 바람을 빨아들여 공기를 압축하고, 반동과 함께 쏘면 공기 고리를 남기며 날아가 상대를 날려 버린다</summary>
        IEnumerator AirCannon(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, d = aim - from, dir = d.normalized;
            var mouth = from + dir * 0.5f * u;
            var ball = Ast(Pw + "n_airball", mouth, 0f, Al(NWhite, 0.95f * Am), 4);
            for (float t = 0f; t < 0.4f; t += BattleTime.FxDt)
            {
                Size(ball, 0.85f * u * Out(t / 0.4f));
                ball.transform.Rotate(0, 0, -720f * BattleTime.FxDt);
                if (UnityEngine.Random.value < 0.5f)
                {
                    float ang = Rnd(0f, 360f);
                    var w = Ast(Pw + "n_wind", mouth + Dir(ang) * 1.6f * u, 1.2f * u, Al(NSky, 0f), 3, ang + 180f, new Vector2(1f, 0.5f));
                    var st = w.transform.position;
                    Anim(w, 0.25f, (s, k) => { s.transform.position = Vector3.Lerp(st, mouth, In(k)); s.color = Al(NSky, 0.8f * Bell(k)); });
                }
                yield return null;
            }
            GameAudio.Se("hit", 0.7f);
            Run(Recoil(user, dir, 0.35f));
            Impact(mouth, 1.2f * u, NSky, 0.18f);
            float ring = 0f;
            for (float t = 0f; t < 0.22f; t += BattleTime.FxDt)
            {
                var p = Vector3.Lerp(mouth, aim, t / 0.22f);
                ball.transform.position = p;
                ball.transform.Rotate(0, 0, -1200f * BattleTime.FxDt);
                ring += BattleTime.FxDt;
                if (ring > 0.05f)
                {
                    ring = 0f;
                    var r = Ast(Pw + "n_airball", p, 0.9f * u, Al(NWhite, 0.5f * Am), 2, Ang(d));
                    Anim(r, 0.3f, (s, k) => { SizeWH(s, 0.35f * u, Mathf.Lerp(0.9f, 1.8f, Out(k)) * u); s.color = Al(NWhite, 0.5f * Am * (1f - k)); });
                }
                yield return null;
            }
            Destroy(ball.gameObject);
            GameAudio.Se("hit_strong", 0.8f);
            Impact(aim, 2.0f * u, NSky);
            for (int i = 0; i < 8; i++)
            {
                float ang = Ang(d) + Rnd(-70f, 70f);
                var w = Ast(Pw + "n_wind", aim, 0f, Al(NWhite, Am), 4, ang, new Vector2(0f, 0.5f));
                Anim(w, 0.35f, (s, k) => { Size(s, 2.2f * u * Out(k)); s.transform.position = aim + Dir(ang) * 0.8f * u * k; s.color = Al(NWhite, Am * (1f - k)); });
            }
            Dust(astTarget.Feet, u, 6, 1.4f);
            Run(Squash(astTarget, 0.22f));
            AstHit(0.12f);
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>더블 팽: 집중선 속에 잔상을 남기며 달려들어, 엇갈린 각도로 두 번 콱 문다. 이빨 자국이 남는다</summary>
        IEnumerator DoubleFang(PalView user, Vector3 aim)
        {
            float u = U;
            bool going = true;
            Run(AfterimageTrail(user, () => going));
            SpeedLines(aim, 5f * u, 0.6f, NWhite);
            yield return user.Lunge(aim, 99f, 0.42f, () => Run(Bite(aim, u, 2, 1.1f, NWhite)));
            going = false;
            yield return BattleTime.FxWait(0.45f);
        }

        IEnumerator Bite(Vector3 c, float u, int times, float scale, Color col, float angle = 0f)
        {
            for (int i = 0; i < times; i++)
            {
                bool last = i == times - 1;
                float ang = angle + (times > 1 ? (i == 0 ? -18f : 18f) : 0f);
                float sc = scale * (last ? 1.25f : 1f);
                var rot = Quaternion.Euler(0, 0, ang);
                float w = 1.6f * u * sc, gap = 1.0f * u * sc;
                var top = Ast(Pw + "n_fang", c, w, Al(col, Am), 5, ang);
                var bot = Ast(Pw + "n_fang", c, w, Al(col, Am), 5, ang);
                bot.flipY = true;
                for (float t = 0f; t < 0.08f; t += BattleTime.FxDt)
                {
                    float off = gap * (1f + 0.25f * Out(t / 0.08f));
                    top.transform.position = c + rot * Vector3.up * off; bot.transform.position = c - rot * Vector3.up * off;
                    yield return null;
                }
                for (float t = 0f; t < 0.06f; t += BattleTime.FxDt)
                {
                    float off = Mathf.Lerp(gap * 1.25f, 0.1f * u * sc, In(t / 0.06f));
                    top.transform.position = c + rot * Vector3.up * off; bot.transform.position = c - rot * Vector3.up * off;
                    yield return null;
                }
                GameAudio.Se(last ? "hit_strong" : "hit", 0.9f);
                Impact(c, (last ? 2.0f : 1.4f) * u, NCream);
                Spray(Pw + "n_mote", c, 8, 1.4f * u, 0.2f * u, NWhite, NCream, 0.35f);
                Run(Squash(astTarget, last ? 0.2f : 0.12f));
                var mark = Ast(Pw + "n_scratch", c, 1.4f * u * sc, Al(NWhite, 0.8f * Am), 4, ang + 45f);
                Anim(mark, 0.5f, (s, k) => s.color = Al(NWhite, 0.8f * Am * (1f - k)));
                if (last) AstHit(0.14f);
                else if (!astMissed) Run(CameraShake(0.08f, 0.1f));
                yield return BattleTime.FxWait(0.05f);
                Anim(top, 0.2f, (s, k) => { s.transform.position += rot * Vector3.up * BattleTime.FxDt * 2f; s.color = Al(col, Am * (1f - k)); });
                Anim(bot, 0.2f, (s, k) => { s.transform.position -= rot * Vector3.up * BattleTime.FxDt * 2f; s.color = Al(col, Am * (1f - k)); });
                yield return BattleTime.FxWait(0.1f);
            }
        }

        /// <summary>파워 샷: 세 불씨가 돌며 힘을 모으고, 총구 섬광과 반동과 함께 쏜 탄이 상대를 관통해 뒤에서 한 번 더 터진다</summary>
        IEnumerator PowerShot(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var muzzle = from + dir * 0.55f * u;
            var charge = Ast(Pw + "n_bullet", muzzle, 0f, NCream, 5);
            var orbit = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++) orbit[i] = Ast(Pw + "n_mote", muzzle, 0.25f * u, Al(NHoly, Am), 6);
            GameAudio.Se("stat_up", 0.5f);
            for (float t = 0f; t < 0.45f; t += BattleTime.FxDt)
            {
                float k = t / 0.45f;
                Size(charge, 0.6f * u * Out(k) * (1f + 0.25f * Mathf.Sin(t * 70f)));
                for (int i = 0; i < 3; i++)
                {
                    float an = (i * 120f + 900f * t) * Mathf.Deg2Rad, rr = Mathf.Lerp(0.9f, 0.2f, k) * u;
                    orbit[i].transform.position = muzzle + new Vector3(Mathf.Cos(an) * rr, Mathf.Sin(an) * rr, 0f);
                }
                if (UnityEngine.Random.value < 0.5f) Mote(muzzle + Dir(Rnd(0, 360)) * 1.1f * u, muzzle, 0.15f * u, NCream, 0.2f, 0f, Pw + "n_mote");
                yield return null;
            }
            foreach (var o in orbit) Destroy(o.gameObject);
            GameAudio.Se("hit", 0.8f);
            Impact(muzzle, 1.3f * u, NHoly, 0.16f);
            Run(Recoil(user, dir, 0.3f, 0.2f));
            var end = aim + dir * 2.2f * u;
            float trail = 0f; bool hit = false;
            for (float t = 0f; t < 0.26f; t += BattleTime.FxDt)
            {
                var p = Vector3.Lerp(muzzle, end, t / 0.26f);
                charge.transform.position = p;
                trail += BattleTime.FxDt;
                if (trail > 0.012f)
                {
                    trail = 0f;
                    var ghost = Ast(Pw + "n_bullet", p, 0.5f * u, Al(NHoly, 0.6f * Am), 4);
                    Anim(ghost, 0.22f, (s, kk) => { Size(s, 0.5f * u * (1f - kk)); s.color = Al(NHoly, 0.6f * Am * (1f - kk)); });
                }
                if (!hit && Vector3.Dot(p - aim, dir) > 0f)
                {
                    hit = true;
                    GameAudio.Se("hit_strong", 0.8f);
                    Impact(aim, 1.8f * u, NHoly);
                    Run(Squash(astTarget, 0.16f));
                    AstHit(0.1f);
                }
                yield return null;
            }
            Destroy(charge.gameObject);
            var exit = aim + dir * 1.2f * u;
            var flat = Ast(Pw + "n_bullet", exit, 0.6f * u, Al(NWhite, Am), 5, Ang(dir));
            Anim(flat, 0.25f, (s, k) => { SizeWH(s, Mathf.Lerp(0.6f, 0.2f, k) * u, Mathf.Lerp(0.6f, 2.4f, Out(k)) * u); s.color = Al(NWhite, Am * (1f - k)); });
            Spray(Pw + "n_mote", exit, 10, 1.4f * u, 0.2f * u, NHoly, NWhite);
            yield return BattleTime.FxWait(0.3f);
        }

        /// <summary>파워 폭탄: 머리 위에서 핏줄이 맥동하는 에너지 구가 부풀어, 포물선으로 날아가 돔처럼 터진다</summary>
        IEnumerator PowerBomb(PalView user, Vector3 aim)
        {
            float u = U;
            var top = user.Center + Vector3.up * user.Height * 0.6f;
            top.y = Mathf.Min(top.y, CamRect().yMax - 0.8f * u);                   // 큰 팰이 써도 화면 밖으로 나가지 않게
            var bomb = Ast(Pw + "n_bomb", top, 0f, NSky, 4);
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                Size(bomb, 1.3f * u * BackOut(t / 0.5f) * (1f + 0.08f * Mathf.Sin(t * 30f)));
                bomb.transform.Rotate(0, 0, 120f * BattleTime.FxDt);
                yield return null;
            }
            yield return Throw(bomb, top, aim, 0.38f, 1.2f * u, 240f);
            GameAudio.Se("hit_strong");
            Impact(aim, 2.6f * u, NSky, 0.3f);
            Dust(astTarget.Feet, u, 8, 2f);
            Run(Squash(astTarget, 0.25f));
            var dome = Ast(Pw + "n_bomb", aim, 1.3f * u, Al(NWhite, Am), 4);
            Anim(dome, 0.45f, (s, k) => { Size(s, Mathf.Lerp(1.3f, 3.6f, Out(k)) * u); s.color = Al(NWhite, Am * (1f - k)); s.transform.Rotate(0, 0, 200f * BattleTime.FxDt); });
            Ring(aim, 0.5f * u, 3.4f * u, Al(NSky, Am), 0.4f, "ast_shock", 0.4f);
            Spray(Pw + "n_mote", aim, 14, 2.2f * u, 0.22f * u, NSky, NWhite, 0.5f);
            if (!astMissed) ScreenFlash(NWhite, 0.25f, 0.02f, 0f, 0.2f);
            AstHit(0.14f);
            yield return BattleTime.FxWait(0.4f);
        }

        /// <summary>에어 블레이드: 몸을 크게 휘둘러 공기 칼날 셋을 만들고, 바람 꼬리를 끌며 휘어 날아가 X자로 엇갈려 벤다. 잠깐 뒤 베인 자리가 터진다</summary>
        IEnumerator AirBlade(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center;
            float baseAng = Ang(aim - from);
            yield return user.WindUp(aim, 0.16f);
            var sweep = Ast(Pw + "n_crescent", from, 0f, Al(NWhite, Am), 5, baseAng - 90f);
            Anim(sweep, 0.2f, (s, k) => { Size(s, 2.6f * u * Out(k)); s.transform.rotation = Quaternion.Euler(0, 0, baseAng - 150f + 120f * k); s.color = Al(NWhite, Am * (1f - k)); });
            GameAudio.Se("select", 0.7f);
            for (int i = 0; i < 3; i++)
            {
                float off = (i - 1) * 28f;
                var blade = Ast(Pw + "n_crescent", from, 1.4f * u, Al(NSky, Am), 4, baseAng + off - 90f);
                var mid = from + Dir(baseAng + off) * Vector3.Distance(from, aim) * 0.55f;
                Run(BladeFly(blade, from, mid, aim, baseAng + off - 90f, u, i));
            }
            yield return BattleTime.FxWait(0.4f);
            GameAudio.Se("hit_strong", 0.8f);
            Impact(aim, 2f * u, NSky);
            Run(Squash(astTarget, 0.15f));
            AstHit(0.12f);
            yield return BattleTime.FxWait(0.14f);
            for (int i = 0; i < 3; i++)
            {
                var p = aim + new Vector3(Rnd(-0.4f, 0.4f) * u, Rnd(-0.4f, 0.4f) * u, 0f);
                GameAudio.Se("hit", 0.5f);
                Impact(p, 1.1f * u, NWhite, 0.18f);
                yield return BattleTime.FxWait(0.06f);
            }
            yield return BattleTime.FxWait(0.2f);
        }

        IEnumerator BladeFly(SpriteRenderer blade, Vector3 from, Vector3 mid, Vector3 to, float ang, float u, int i)
        {
            float trail = 0f;
            for (float t = 0f; t < 0.34f; t += BattleTime.FxDt)
            {
                float k = t / 0.34f;
                var p = Vector3.Lerp(Vector3.Lerp(from, mid, k), Vector3.Lerp(mid, to, k), k);
                blade.transform.position = p;
                blade.transform.rotation = Quaternion.Euler(0, 0, ang + 540f * k * (i % 2 == 0 ? 1f : -1f));
                trail += BattleTime.FxDt;
                if (trail > 0.025f)
                {
                    trail = 0f;
                    var g = Ast(Pw + "n_crescent", p, 1.2f * u, Al(NSky, 0.4f * Am), 3, blade.transform.eulerAngles.z);
                    Anim(g, 0.18f, (s, kk) => s.color = Al(NSky, 0.4f * Am * (1f - kk)));
                }
                yield return null;
            }
            if (blade != null) Destroy(blade.gameObject);
            var cut = Ast("ast_line", to, 0f, Al(NWhite, Am), 6, 35f + i * 55f);
            Anim(cut, 0.3f, (s, k) => SizeWH(s, 3.2f * u * Out(k * 3f), 0.18f * u * (1f - k)));
        }

        /// <summary>스타 마인: 별 여섯을 흩뿌려 상대 둘레에 띄우고, 점점 빨라지는 째깍 소리와 함께 금빛→주황→빨강으로 달아오르다가 연쇄로 터진 뒤 한가운데서 별가루가 폭죽처럼 터진다</summary>
        IEnumerator StarMine(PalView user, Vector3 aim)
        {
            float u = U;
            yield return user.WindUp(aim, 0.15f);
            const int n = 6;
            var stars = new SpriteRenderer[n];
            var halos = new SpriteRenderer[n];
            var pos = new Vector3[n];
            GameAudio.Se("shine", 0.7f);
            for (int i = 0; i < n; i++)
            {
                pos[i] = aim + new Vector3(Mathf.Cos(i * 60f * Mathf.Deg2Rad) * 1.25f * u, Mathf.Sin(i * 60f * Mathf.Deg2Rad) * 0.85f * u, 0f);
                var thrown = Ast(Pw + "n_star5", user.Center, 0.45f * u, Al(NHoly, Am), 5);
                Run(Throw(thrown, user.Center, pos[i], 0.3f, 1.2f * u, 720f, false, (s, k) =>
                {
                    if (UnityEngine.Random.value < 0.4f) Mote(s.transform.position, s.transform.position + Vector3.down * 0.3f * u, 0.14f * u, NHoly, 0.3f, 0f, Pw + "n_mote");
                }));
                yield return BattleTime.FxWait(0.04f);
            }
            yield return BattleTime.FxWait(0.3f);
            for (int i = 0; i < n; i++)
            {
                halos[i] = Ast("ast_dot", pos[i], 1.2f * u, Al(NHoly, 0.5f * Am), 3);
                stars[i] = Ast(Pw + "n_star5", pos[i], 0.6f * u, Al(NHoly, Am), 5);
                Twinkle(pos[i], 1.2f * u, Al(NWhite, Am), 0.2f);
            }
            // 째깍째깍 — 점점 빨라지며 달아오른다
            Color[] heat = { NHoly, C(0xffa040), C(0xff4a4a) };
            float beat = 0.26f, rot = 0f;
            for (int b = 0; b < 7; b++)
            {
                GameAudio.Se("select", 0.4f + 0.05f * b);
                float h = b / 6f;
                var col = h < 0.5f ? Color.Lerp(heat[0], heat[1], h * 2f) : Color.Lerp(heat[1], heat[2], (h - 0.5f) * 2f);
                for (float t = 0f; t < beat; t += BattleTime.FxDt)
                {
                    float k = Bell(t / beat);
                    rot += 180f * BattleTime.FxDt * (1f + h * 3f);
                    for (int i = 0; i < n; i++)
                    {
                        var jitter = new Vector3(Rnd(-1f, 1f), Rnd(-1f, 1f), 0f) * 0.04f * u * h;
                        stars[i].transform.position = pos[i] + jitter;
                        stars[i].transform.rotation = Quaternion.Euler(0, 0, rot);
                        Size(stars[i], 0.6f * u * (1f + 0.4f * k));
                        stars[i].color = Color.Lerp(Al(col, Am), Al(NWhite, Am), k * 0.6f);
                        Size(halos[i], 1.2f * u * (1f + 0.6f * k)); halos[i].color = Al(col, 0.5f * Am * (0.5f + k));
                    }
                    yield return null;
                }
                beat = Mathf.Max(0.07f, beat * 0.72f);
            }
            // 연쇄 폭발
            Color[] blast = { NHoly, NPink, NSky };
            for (int i = 0; i < n; i++)
            {
                Destroy(stars[i].gameObject); Destroy(halos[i].gameObject);
                var c = blast[i % 3];
                GameAudio.Se("hit", 0.7f);
                Impact(pos[i], 1.9f * u, c, 0.3f);
                Ring(pos[i], 0.3f * u, 2.0f * u, Al(c, Am), 0.35f, Pw + "n_airball");
                Spray(Pw + "n_star5", pos[i], 6, 1.2f * u, 0.22f * u, c, NWhite, 0.45f);
                Run(Squash(astTarget, 0.1f, 0.08f));
                if (!astMissed) Run(CameraShake(0.07f, 0.1f));
                yield return BattleTime.FxWait(0.06f);
            }
            // 한가운데 별가루 폭죽
            yield return BattleTime.FxWait(0.05f);
            GameAudio.Se("hit_strong");
            if (!astMissed) ScreenFlash(NHoly, 0.35f, 0.02f, 0.03f, 0.3f);
            Impact(aim, 3.2f * u, NHoly, 0.35f);
            Twinkle(aim, 4f * u, Al(NWhite, Am), 0.45f);
            for (int i = 0; i < 20; i++)
            {
                var c = blast[i % 3];
                var st = Ast(Pw + "n_star5", aim, 0.3f * u, Al(c, Am), 6, Rnd(0, 360));
                var vel = Dir(Rnd(0f, 360f)) * Rnd(2f, 3.5f) * u;
                float spin = Rnd(-600f, 600f);
                Anim(st, 0.8f, (s, k) => { s.transform.position = aim + vel * Out(k) + Vector3.down * 1.4f * u * k * k; s.transform.Rotate(0, 0, spin * BattleTime.FxDt); s.color = Al(c, Am * (1f - k * k)); });
            }
            Run(Squash(astTarget, 0.25f));
            AstHit(0.2f);
            yield return BattleTime.FxWait(0.5f);
        }

        /// <summary>팰 블래스트: 주위가 어두워지고 빛 알갱이가 모인 뒤, 가장자리가 일렁이는 굵은 광선을 쏜다</summary>
        IEnumerator PalBlast(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var muzzle = from + dir * 0.6f * u;
            Run(DimTo(0.5f, 0.3f));
            GameAudio.Se("stat_up", 0.7f);
            var core = Ast(Pw + "n_bullet", muzzle, 0f, NWhite, 5);
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                Size(core, 0.9f * u * Out(t / 0.5f));
                if (UnityEngine.Random.value < 0.7f) Mote(muzzle + Dir(Rnd(0, 360)) * Rnd(1.2f, 2f) * u, muzzle, 0.16f * u, UnityEngine.Random.value < 0.5f ? NSky : NCream, 0.25f, 0f, Pw + "n_mote");
                yield return null;
            }
            GameAudio.Se("hit_strong");
            float len = Vector3.Distance(muzzle, aim) + 2.5f * u;
            var beam = Ast(Pw + "n_beam", muzzle, 0f, Al(NSky, Am), 4, Ang(dir), new Vector2(0f, 0.5f));
            const float hold = 0.5f;
            for (float t = 0f; t < hold + 0.15f; t += BattleTime.FxDt)
            {
                float f = t > hold ? 1f - (t - hold) / 0.15f : 1f;
                SizeWH(beam, len * Out(t / 0.08f), 1.3f * u * (1f + 0.1f * Mathf.Sin(t * 80f)) * f);
                beam.color = Al(Color.Lerp(NSky, NWhite, 0.5f + 0.5f * Mathf.Sin(t * 30f)), Am);
                Size(core, 0.9f * u * f * (1f + 0.2f * Mathf.Sin(t * 60f)));
                if (t > 0.08f && !astHit) { Impact(aim, 2.4f * u, NSky, 0.3f); Run(Squash(astTarget, 0.22f, 0.3f)); AstHit(0.16f); }
                if (Mathf.Repeat(t, 0.12f) < BattleTime.FxDt) Ring(aim, 0.5f * u, 2.2f * u, Al(NWhite, 0.8f * Am), 0.25f, "ast_shock");
                yield return null;
            }
            Destroy(beam.gameObject); Destroy(core.gameObject);
            Spray(Pw + "n_mote", aim, 12, 2f * u, 0.2f * u, NSky, NWhite);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>레이디언트 배리지: 빛구가 떠올라 세 번 맥동하며 빛을 모으고, 상대 위에서 여섯 갈래로 갈라져 육각으로 둘러싼 빛구들이 빛탄을 퍼붓는다. 마지막에 다시 모여 한 덩어리로 떨어져 터진다</summary>
        IEnumerator RadiantBarrage(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.55f, 0.3f));
            GameAudio.Se("shine");
            var cam = CamRect();
            var start = user.Center + Vector3.up * user.Height * 0.7f;
            start.y = Mathf.Min(start.y, cam.yMax - 1f * u);
            var cross = Ast(Pw + "n_holycross", start, 0f, Al(NHoly, 0.6f * Am), 2);
            var orb = Ast(Pw + "n_holyorb", start, 0f, Al(NHoly, Am), 4);
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt) { Size(orb, 1.5f * u * BackOut(t / 0.3f)); Size(cross, 3f * u * Out(t / 0.3f)); yield return null; }
            for (int p = 0; p < 3; p++)                                                      // 세 번 맥동하며 빛을 모은다
            {
                GameAudio.Se("select", 0.5f + 0.1f * p);
                Ring(start, 3.2f * u, 0.6f * u, Al(NHoly, 0.8f * Am), 0.22f, Pw + "n_airball");
                for (int m = 0; m < 6; m++) Mote(start + Dir(Rnd(0, 360)) * 2.2f * u, start, 0.18f * u, NWhite, 0.22f, 0f, Pw + "n_mote");
                for (float t = 0f; t < 0.2f; t += BattleTime.FxDt)
                {
                    Size(orb, 1.5f * u * (1f + 0.25f * Bell(t / 0.2f) * (p + 1) / 3f));
                    cross.transform.Rotate(0, 0, 120f * BattleTime.FxDt);
                    orb.transform.Rotate(0, 0, -90f * BattleTime.FxDt);
                    yield return null;
                }
            }
            var over = aim + Vector3.up * 2.4f * u;
            over.y = Mathf.Min(over.y, cam.yMax - 1f * u);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt)
            {
                var p = Vector3.Lerp(start, over, Out(t / 0.35f));
                orb.transform.position = cross.transform.position = p;
                cross.transform.Rotate(0, 0, 120f * BattleTime.FxDt);
                yield return null;
            }
            // 여섯 갈래로 갈라진다
            GameAudio.Se("hit", 0.7f);
            Impact(over, 2f * u, NHoly, 0.25f);
            Destroy(orb.gameObject);
            Anim(cross, 0.4f, (s, k) => { Size(s, 3f * u * (1f + k)); s.color = Al(NHoly, 0.6f * Am * (1f - k)); });
            const int n = 6;
            var minis = new SpriteRenderer[n];
            var spots = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float an = (90f + i * 60f) * Mathf.Deg2Rad;
                spots[i] = aim + new Vector3(Mathf.Cos(an) * 1.6f * u, Mathf.Sin(an) * 1.2f * u + 0.6f * u, 0f);
                spots[i].y = Mathf.Min(spots[i].y, cam.yMax - 0.5f * u);
                minis[i] = Ast(Pw + "n_holyorb", over, 0.7f * u, Al(NHoly, Am), 5);
            }
            for (float t = 0f; t < 0.25f; t += BattleTime.FxDt)
            {
                for (int i = 0; i < n; i++) { minis[i].transform.position = Vector3.Lerp(over, spots[i], Out(t / 0.25f)); minis[i].transform.Rotate(0, 0, 200f * BattleTime.FxDt); }
                yield return null;
            }
            // 빛탄을 퍼붓는다
            for (int volley = 0; volley < 3; volley++)
            {
                for (int i = 0; i < n; i++)
                {
                    var from = spots[i];
                    var end = aim + new Vector3(Rnd(-0.5f, 0.5f) * u, Rnd(-0.4f, 0.4f) * u, 0f);
                    var shot = Ast(Pw + "n_bullet", from, 0.32f * u, Al(NHoly, Am), 6);
                    var tail = Ast("ast_streak", from, 1.0f * u, Al(NHoly, 0.8f * Am), 5, Ang(end - from), new Vector2(1f, 0.5f));
                    Run(Throw(tail, from, end, 0.14f));
                    Run(Throw(shot, from, end, 0.14f));
                    Run(DelayThen(0.14f, PopAt(end, u)));
                    Size(minis[i], 0.7f * u * 1.3f);
                    yield return BattleTime.FxWait(0.035f);
                    Size(minis[i], 0.7f * u);
                }
                Run(Squash(astTarget, 0.1f, 0.1f));
            }
            yield return BattleTime.FxWait(0.15f);
            // 다시 모여 떨어진다
            for (float t = 0f; t < 0.22f; t += BattleTime.FxDt)
            {
                for (int i = 0; i < n; i++) minis[i].transform.position = Vector3.Lerp(spots[i], over, In(t / 0.22f));
                yield return null;
            }
            foreach (var m in minis) Destroy(m.gameObject);
            var big = Ast(Pw + "n_holyorb", over, 1.8f * u, Al(NWhite, Am), 6);
            for (float t = 0f; t < 0.14f; t += BattleTime.FxDt) { big.transform.position = Vector3.Lerp(over, aim, In(t / 0.14f)); big.transform.Rotate(0, 0, 600f * BattleTime.FxDt); yield return null; }
            Destroy(big.gameObject);
            GameAudio.Se("hit_strong");
            ScreenFlash(NHoly, 0.5f, 0.02f, 0.05f, 0.35f);
            Impact(aim, 3f * u, NHoly, 0.35f);
            var burst = Ast(Pw + "n_holycross", aim, 0f, Al(NWhite, Am), 6);
            Anim(burst, 0.5f, (s, k) => { Size(s, 5f * u * Out(k)); s.transform.rotation = Quaternion.Euler(0, 0, 45f * k); s.color = Al(NWhite, Am * (1f - k)); });
            Ring(aim, 0.5f * u, 4f * u, Al(NHoly, Am), 0.45f, Pw + "n_airball");
            Spray(Pw + "n_mote", aim, 16, 2.6f * u, 0.22f * u, NHoly, NWhite, 0.5f);
            Run(Squash(astTarget, 0.25f));
            AstHit(0.2f);
            yield return BattleTime.FxWait(0.4f);
            yield return DimTo(0f, 0.25f);
        }

        IEnumerator PopAt(Vector3 at, float u)
        {
            GameAudio.Se("hit", 0.45f);
            Impact(at, 0.9f * u, NHoly, 0.15f);
            Twinkle(at, 1.0f * u, Al(NWhite, Am), 0.2f);
            yield break;
        }

        /// <summary>신성 폭발: 상대 자리에 빛의 십자가 돌며 커지고, 땅의 빛고리와 함께 하얗게 터진다</summary>
        IEnumerator HolyBurst(PalView user, Vector3 aim)
        {
            float u = U;
            yield return user.WindUp(aim, 0.15f);
            Run(DimTo(0.6f, 0.3f));
            GameAudio.Se("shine");
            Ring(aim, 3.5f * u, 0.5f * u, Al(NHoly, Am), 0.45f, "ast_ring", 0.35f);          // 빛이 모여든다
            var cross = Ast(Pw + "n_holycross", aim, 0f, Al(NHoly, Am), 5);
            for (float t = 0f; t < 0.45f; t += BattleTime.FxDt)
            {
                Size(cross, 1.2f * u * Out(t / 0.45f));
                cross.transform.rotation = Quaternion.Euler(0, 0, 90f * t / 0.45f);
                yield return null;
            }
            GameAudio.Se("hit_strong");
            ScreenFlash(NWhite, 0.6f, 0.02f, 0.05f, 0.35f);
            Anim(cross, 0.5f, (s, k) => { Size(s, Mathf.Lerp(1.2f, 4.5f, Out(k)) * u); s.color = Al(NWhite, Am * (1f - k)); s.transform.rotation = Quaternion.Euler(0, 0, 90f + 30f * k); });
            Ring(aim, 0.5f * u, 4f * u, Al(NHoly, Am), 0.5f, "ast_shock");
            Ring(aim, 0.4f * u, 3f * u, Al(NWhite, Am), 0.45f, "ast_ring", 0.3f);
            Spray(Pw + "n_mote", aim, 16, 2.6f * u, 0.22f * u, NHoly, NWhite, 0.5f);
            Impact(aim, 2.8f * u, NHoly, 0.3f);
            Run(Squash(astTarget, 0.25f));
            AstHit(0.2f);
            yield return BattleTime.FxWait(0.45f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>데굴데굴 솜사탕: 통통 튀며 굴러가 솜구름을 흩날리고, 부딪치는 순간 솜뭉치가 폭죽처럼 터지며 머리 위로 별이 빙글빙글 돈다</summary>
        IEnumerator RollingFluff(PalView user, Vector3 aim)
        {
            float u = U;
            var tr = user.transform; var home = tr.position;
            var d = aim - user.Center; d.z = 0f;
            var hitPos = home + d * 0.8f;
            bool rolling = true;
            Run(FluffTrail(user, () => rolling));
            GameAudio.Se("select", 0.6f);
            for (int b = 0; b < 3; b++)                                                // 통, 통, 통 튀며 다가간다
            {
                var a = tr.position; var e = Vector3.Lerp(home, hitPos, (b + 1) / 3f);
                float h = (0.7f - 0.15f * b) * u;
                for (float t = 0f; t < 0.13f; t += BattleTime.FxDt) { float k = t / 0.13f; tr.position = Vector3.Lerp(a, e, k) + Vector3.up * h * 4f * k * (1f - k); yield return null; }
                tr.position = e;
                Dust(new Vector3(e.x, user.Feet.y, 0f), u * 0.5f, 2, 0.6f);
                Run(Squash(user, 0.15f, 0.08f));
            }
            rolling = false;
            GameAudio.Se("hit_strong", 0.8f);
            Impact(aim, 1.8f * u, NCream);
            for (int i = 0; i < 14; i++)
            {
                float ang = i * (360f / 14f) + Rnd(-10f, 10f);
                var w = Ast(Pw + "n_wool", aim, 0.45f * u, Al(NCream, Am), 5);
                var end = aim + Dir(ang) * Rnd(1.2f, 2f) * u;
                Anim(w, 0.5f, (s, k) => { s.transform.position = Vector3.Lerp(aim, end, Out(k)) + Vector3.down * 0.4f * u * k * k; Size(s, 0.45f * u * (1f + 0.6f * k)); s.color = Al(NCream, Am * (1f - k)); });
            }
            for (int i = 0; i < 3; i++)
            {
                var st = Ast(Pw + "n_star5", aim, 0.4f * u, Al(NHoly, Am), 6);
                float ph = i * 120f;
                Anim(st, 0.9f, (s, k) => { float an = (ph + 900f * k) * Mathf.Deg2Rad; s.transform.position = aim + Vector3.up * 0.9f * u + new Vector3(Mathf.Cos(an) * 0.65f * u, Mathf.Sin(an) * 0.22f * u, 0f); s.color = Al(NHoly, Am * (1f - k * k)); });
            }
            Run(Squash(astTarget, 0.22f));
            AstHit(0.1f);
            var back = tr.position;
            for (float t = 0f; t < 0.25f; t += BattleTime.FxDt) { float k = t / 0.25f; tr.position = Vector3.Lerp(back, home, Out(k)) + Vector3.up * 0.4f * u * 4f * k * (1f - k); yield return null; }
            tr.position = home;
            yield return BattleTime.FxWait(0.4f);
        }

        IEnumerator FluffTrail(PalView user, System.Func<bool> keep)
        {
            while (keep())
            {
                var p = user.Center + Vector3.down * 0.3f * U;
                var w = Ast(Pw + "n_wool", p, 0.45f * U, Al(NCream, 0.8f), -1);
                Anim(w, 0.35f, (s, k) => { Size(s, 0.45f * U * (1f - 0.5f * k)); s.transform.position = p + Vector3.up * 0.2f * U * k; s.color = Al(NCream, 0.8f * (1f - k)); });
                yield return BattleTime.FxWait(0.03f);
            }
        }

        /// <summary>냥냥 펀치: 집중선 속에서 양팔로 눈에 안 보이게 두드려 발바닥 자국이 콩콩 찍히고, 마지막에 커다란 발바닥으로 쾅 내리친다</summary>
        IEnumerator MeowPunch(PalView user, Vector3 aim)
        {
            float u = U;
            var tr = user.transform; var home = tr.position;
            var d = aim - user.Center; d.z = 0f;
            var close = home + d * 0.7f;
            for (float t = 0f; t < 0.14f; t += BattleTime.FxDt) { tr.position = Vector3.Lerp(home, close, In(t / 0.14f)); yield return null; }
            SpeedLines(aim, 4.5f * u, 0.9f, NPink, 0.6f);
            for (int i = 0; i < 9; i++)
            {
                GameAudio.Se("hit", 0.55f);
                tr.position = close + new Vector3(Rnd(-0.1f, 0.1f), Rnd(-0.1f, 0.1f), 0f) * u;
                var p = aim + new Vector3(Rnd(-0.65f, 0.65f) * u, Rnd(-0.55f, 0.55f) * u, 0f);
                var col = i % 2 == 0 ? NPink : NWhite;
                var paw = Ast(Pw + "n_paw", p, 0f, Al(col, Am), 5, Rnd(-40f, 40f));
                Anim(paw, 0.4f, (s, k) => { Size(s, 0.65f * u * (k < 0.2f ? BackOut(k / 0.2f) : 1f)); s.color = Al(col, Am * (k < 0.6f ? 1f : (1f - k) / 0.4f)); });
                Impact(p, 0.8f * u, NPink, 0.12f);
                Run(Squash(astTarget, 0.08f, 0.07f));
                if (!astMissed) Run(CameraShake(0.035f, 0.05f));
                yield return BattleTime.FxWait(0.05f);
            }
            yield return BattleTime.FxWait(0.08f);
            GameAudio.Se("hit_strong");
            var startBig = aim + Vector3.up * 1.2f * u;
            var big = Ast(Pw + "n_paw", startBig, 1.8f * u, Al(NPink, Am), 6);
            for (float t = 0f; t < 0.1f; t += BattleTime.FxDt) { big.transform.position = Vector3.Lerp(startBig, aim, In(t / 0.1f)); yield return null; }
            Impact(aim, 2.4f * u, NPink);
            Dust(astTarget.Feet, u, 6);
            Run(Squash(astTarget, 0.25f));
            AstHit(0.16f);
            Anim(big, 0.35f, (s, k) => { Size(s, 1.8f * u * (1f + 0.15f * k)); s.color = Al(NPink, Am * (1f - k)); });
            for (float t = 0f; t < 0.2f; t += BattleTime.FxDt) { tr.position = Vector3.Lerp(close, home, Out(t / 0.2f)); yield return null; }
            tr.position = home;
            yield return BattleTime.FxWait(0.25f);
        }

        /// <summary>창기병 돌격: 창끝에 성광을 모으고 육각 방패를 세우면 발밑에 빛의 길이 깔린다. 금빛 잔상을 끌며 돌진해 거대한 빛의 창으로 꿰뚫고, 방패로 들이받아 상대 둘레에 빛기둥 셋이 솟는다</summary>
        IEnumerator LancerCharge(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            float ang = Ang(dir);
            Run(DimTo(0.6f, 0.3f));
            // ① 창끝에 성광이 모인다
            GameAudio.Se("shine");
            var tip = from + dir * 0.9f * u;
            var glint = Ast(Pw + "n_holycross", tip, 0f, Al(NHoly, Am), 6);
            for (float t = 0f; t < 0.4f; t += BattleTime.FxDt)
            {
                Size(glint, 1.6f * u * Out(t / 0.4f));
                glint.transform.Rotate(0, 0, 360f * BattleTime.FxDt);
                if (UnityEngine.Random.value < 0.6f) Mote(tip + Dir(Rnd(0, 360)) * 1.4f * u, tip, 0.15f * u, NHoly, 0.22f, 0f, Pw + "n_mote");
                yield return null;
            }
            // ② 방패를 세우고 빛의 길이 깔린다
            GameAudio.Se("stat_up", 0.7f);
            var shield = Ast(Pw + "n_shield", tip, 0f, Al(NSky, Am), 5);
            for (int i = 0; i < 2; i++) Ring(tip, 0.6f * u, 2.6f * u, Al(NSky, 0.8f * Am), 0.35f, Pw + "n_airball", 1.15f, i * 0.1f);
            float feetY = Mathf.Min(user.Feet.y, astTarget.Feet.y) + 0.15f * u;
            var roadFrom = new Vector3(from.x, feetY, 0f);
            float roadLen = Mathf.Abs(aim.x - from.x) + 1.5f * u;
            var road = Ast(Pw + "n_beam", roadFrom, 0f, Al(NHoly, 0.7f * Am), -3, aim.x > from.x ? 0f : 180f, new Vector2(0f, 0.5f));
            for (float t = 0f; t < 0.25f; t += BattleTime.FxDt)
            {
                Size(shield, 1.7f * u * BackOut(t / 0.25f), 1.15f);
                SizeWH(road, roadLen * Out(t / 0.25f), 0.35f * u);
                yield return null;
            }
            // ③ 돌진
            bool going = true;
            Run(FollowShield(shield, user, dir, u, () => going));
            Run(FollowShield(glint, user, dir, u * 1.3f, () => going));
            Run(GoldTrail(user, () => going));
            SpeedLines(aim, 6f * u, 0.5f, NHoly);
            GameAudio.Se("select", 0.8f);
            yield return user.Lunge(aim, 99f, 0.45f, () =>
            {
                GameAudio.Se("hit_strong");
                ScreenFlash(NHoly, 0.45f, 0.02f, 0.04f, 0.3f);
                var lance = Ast(Pw + "n_lance", aim - dir * 1.6f * u, 0f, Al(NWhite, Am), 7, ang, new Vector2(0f, 0.5f));
                Anim(lance, 0.4f, (s, k) => { SizeWH(s, 5f * u * Out(k * 2.5f), 0.9f * u * (1f - 0.6f * k)); s.color = Al(NWhite, Am * (1f - k * k)); });
                var lanceGlow = Ast(Pw + "n_lance", aim - dir * 1.6f * u, 0f, Al(NHoly, 0.7f * Am), 6, ang, new Vector2(0f, 0.5f));
                Anim(lanceGlow, 0.45f, (s, k) => { SizeWH(s, 5.6f * u * Out(k * 2.5f), 1.8f * u * (1f - 0.5f * k)); s.color = Al(NHoly, 0.7f * Am * (1f - k)); });
                var bash = Ast(Pw + "n_shield", aim - dir * 0.4f * u, 1.7f * u, Al(NWhite, Am), 6);
                Anim(bash, 0.35f, (s, k) => { Size(s, 1.7f * u * (1f + 1.2f * Out(k)), 1.15f); s.color = Al(NWhite, Am * (1f - k)); });
                Impact(aim, 3f * u, NHoly, 0.3f);
                var flare = Ast(Pw + "n_holycross", aim, 0f, Al(NHoly, Am), 7, 45f);
                Anim(flare, 0.45f, (s, k) => Size(s, 4f * u * Bell(k)));
                Spray(Pw + "n_mote", aim, 14, 2.2f * u, 0.22f * u, NSky, NHoly);
                Dust(astTarget.Feet, u, 8, 2f);
                Run(Squash(astTarget, 0.28f));
                Run(HolyPillars(aim, u));
                AstHit(0.24f);
            });
            going = false;
            Anim(shield, 0.25f, (s, k) => s.color = Al(NSky, Am * (1f - k)));
            Anim(glint, 0.25f, (s, k) => s.color = Al(NHoly, Am * (1f - k)));
            Anim(road, 0.4f, (s, k) => s.color = Al(NHoly, 0.7f * Am * (1f - k)));
            yield return BattleTime.FxWait(0.6f);
            yield return DimTo(0f, 0.25f);
        }

        IEnumerator FollowShield(SpriteRenderer shield, PalView user, Vector3 dir, float u, System.Func<bool> keep)
        {
            while (keep() && shield != null) { shield.transform.position = user.Center + dir * 0.9f * u; yield return null; }
        }

        IEnumerator GoldTrail(PalView user, System.Func<bool> keep)
        {
            while (keep())
            {
                Afterimage(user, Al(NHoly, 0.55f), 0.3f);
                var p = new Vector3(user.Center.x, user.Feet.y, 0f);
                Dust(p, U * 0.5f, 1, 0.6f);
                yield return BattleTime.FxWait(0.035f);
            }
        }

        /// <summary>창기병 돌격 끝: 상대 둘레에 빛기둥 셋이 차례로 솟는다</summary>
        IEnumerator HolyPillars(Vector3 aim, float u)
        {
            float top = CamRect().yMax + 0.5f;
            float[] xs = { -1.1f, 1.1f, 0f };
            foreach (float x in xs)
            {
                var foot = new Vector3(aim.x + x * u, astTarget.Feet.y, 0f);
                var pillar = Ast(Pw + "n_beam", foot, 0f, Al(NHoly, 0.85f * Am), 5, 90f, new Vector2(0f, 0.5f));
                float h = top - foot.y;
                Anim(pillar, 0.45f, (s, k) => { SizeWH(s, h * Out(k * 4f), (x == 0f ? 1.4f : 0.8f) * u * (1f - k)); });
                Ring(foot, 0.3f * u, 1.8f * u, Al(NHoly, Am), 0.3f, Pw + "n_airball", 0.35f);
                GameAudio.Se("hit", 0.5f);
                yield return BattleTime.FxWait(0.08f);
            }
        }

        /// <summary>와일드 팽: 울부짖어 음파를 퍼뜨린 뒤, 지그재그로 잔상을 남기며 파고들어 커다란 턱으로 비스듬히 문다. 흙먼지가 인다</summary>
        IEnumerator WildFang(PalView user, Vector3 aim)
        {
            float u = U;
            var tr = user.transform;
            Vector3 home = tr.position, d = aim - user.Center; d.z = 0f;
            GameAudio.Se("stat_up", 0.6f);
            for (int i = 0; i < 3; i++) Ring(user.Center, 0.6f * u, 2.6f * u, Al(NSteel, 0.8f), 0.35f, "ast_ring", 1f, i * 0.08f);
            Run(Squash(user, -0.12f, 0.25f));
            yield return BattleTime.FxWait(0.3f);
            var side = new Vector3(-d.y, d.x, 0f).normalized * 0.6f * u;
            Vector3[] path = { home + d * 0.25f + side, home + d * 0.5f - side, home + d * 0.72f + side * 0.4f, home + d * 0.8f };
            SpeedLines(aim, 5f * u, 0.5f, NSteel);
            foreach (var p in path)
            {
                var a = tr.position;
                Dust(new Vector3(a.x, user.Feet.y, 0f), u * 0.6f, 2, 0.8f);
                for (float t = 0f; t < 0.06f; t += BattleTime.FxDt) { tr.position = Vector3.Lerp(a, p, t / 0.06f); yield return null; }
                Afterimage(user, Al(NSteel, 0.65f), 0.3f);
            }
            float tilt = d.x < 0f ? -28f : 28f;
            yield return Bite(aim, u, 1, 1.6f, NWhite, tilt);
            Dust(astTarget.Feet, u, 6);
            Spray(Pw + "n_mote", aim, 10, 1.6f * u, 0.22f * u, NSteel, NWhite);
            var back = tr.position;
            for (float t = 0f; t < 0.22f; t += BattleTime.FxDt) { tr.position = Vector3.Lerp(back, home, Out(t / 0.22f)); yield return null; }
            tr.position = home;
            yield return BattleTime.FxWait(0.2f);
        }
    }
}
