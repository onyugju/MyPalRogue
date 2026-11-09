using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>땅 속성 팰월드 기술 연출 (그림: PwFx/r_*)</summary>
    public partial class SkillFx
    {
        static readonly Color RSand = C(0xe8c98a), RBrown = C(0x8a5a32), RFlame = C(0xff9a3a);

        static partial void RegGround()
        {
            PwFx["Mud_Shot"] = (fx, u, t, a) => fx.MudShot(u, a);
            PwFx["Rock_Burst"] = (fx, u, t, a) => fx.RockBurstFx(u, a);
            PwFx["Rock_Cannon"] = (fx, u, t, a) => fx.RockCannon(u, a);
            PwFx["Sand_Tornado"] = (fx, u, t, a) => fx.SandTornado(u, a);
            PwFx["Rock_Blast"] = (fx, u, t, a) => fx.RockBlast(u, a);
            PwFx["Rock_Spear"] = (fx, u, t, a) => fx.RockSpear(u, a);
            PwFx["Sand_Twister"] = (fx, u, t, a) => fx.SandTwister(u, a);
            PwFx["Stone_Beat"] = (fx, u, t, a) => fx.StoneBeat(u, a);
            PwFx["Fighting_Slash"] = (fx, u, t, a) => fx.FightingSlash(u, a);
            PwFx["Spin_Leg_Slash"] = (fx, u, t, a) => fx.SpinLegSlash(u, a);
            PwFx["Force_Drive"] = (fx, u, t, a) => fx.ForceDrive(u, a);
            PwFx["Ground_Smasher"] = (fx, u, t, a) => fx.GroundSmasher(u, a);
            PwFx["Somersault_Scratch"] = (fx, u, t, a) => fx.SomersaultScratch(u, a);
            PwFx["Rolling_Scratch"] = (fx, u, t, a) => fx.RollingScratch(u, a);
            PwFx["Blazing_Missile"] = (fx, u, t, a) => fx.BlazingMissile(u, a);
            PwFx["Boar_Rush"] = (fx, u, t, a) => fx.BoarRush(u, a);
        }

        // ───── 공용 조각 ─────

        /// <summary>돌이 부서져 자갈과 모래 먼지가 튄다</summary>
        void RockBreak(Vector3 at, float size, float u, int n = 7)
        {
            for (int i = 0; i < n; i++)
            {
                var p = Ast(Pw + "r_pebble", at, Rnd(0.18f, 0.35f) * u, Al(Color.white, Am), 7, Rnd(0, 360));
                Run(Throw(p, at, at + new Vector3(Rnd(-1f, 1f) * size, Rnd(-0.6f, 0.2f) * size, 0f), Rnd(0.35f, 0.5f), Rnd(0.3f, 0.8f) * size, Rnd(-600f, 600f)));
            }
            var c = Ast(Pw + "r_sand", at, 0f, Al(Color.white, 0.9f * Am), 6, Rnd(0, 360));
            Anim(c, 0.6f, (s, k) => { Size(s, size * 1.5f * Out(k)); s.color = Al(Color.white, 0.9f * Am * (1f - k)); });
            Impact(at, size * 0.8f, RSand, 0.18f);
        }

        /// <summary>땅이 울리며 흙먼지 고리가 퍼진다</summary>
        void Quake(Vector3 ground, float size, float u)
        {
            Ring(ground, 0.3f * size, size, Al(Color.white, Am), 0.45f, Pw + "r_quake", 0.32f);
            var cr = Ast(Pw + "r_crack", ground, 0f, Al(Color.white, Am), 1, Rnd(-10f, 10f));
            Anim(cr, 0.8f, (s, k) => { SizeWH(s, size * 0.9f * Out(k * 4f), 0.35f * u); s.color = Al(Color.white, Am * (k < 0.6f ? 1f : (1f - k) / 0.4f)); });
            Dust(ground, u, 8, size / u * 0.6f);
            if (!astMissed) Run(CameraShake(0.12f * size / (2f * u), 0.25f));
        }

        /// <summary>땅에서 바위 창이 솟구쳤다 무너진다</summary>
        void StoneSpike(Vector3 ground, float w, float h, float life, float ang = 0f, int sort = 5)
        {
            var s = Ast(Pw + "r_spear", ground, 0f, Al(Color.white, Am), sort, ang, new Vector2(0.5f, 0.02f));
            Anim(s, life, (sr, k) => { float up = k < 0.15f ? BackOut(k / 0.15f) : 1f - In((k - 0.75f) / 0.25f) * (k > 0.75f ? 1f : 0f); SizeWH(sr, w, h * up); });
            Dust(ground, w, 3, 0.8f);
        }

        // ───── 기술 ─────

        /// <summary>머드 슛: 끈적한 진흙 덩어리를 철퍽 던져 상대 몸에 들러붙게 하고, 진흙 방울이 뚝뚝 흘러내린다</summary>
        IEnumerator MudShot(PalView user, Vector3 aim)
        {
            float u = U;
            var mud = Ast(Pw + "r_mud", user.Center, 0.8f * u, Al(Color.white, Am), 6);
            GameAudio.Se("select", 0.6f);
            yield return Throw(mud, user.Center, aim, 0.3f, 0.7f * u, 500f);
            GameAudio.Se("hit", 0.8f);
            var splat = Ast(Pw + "r_mud", aim, 0f, Al(Color.white, Am), 8, Rnd(0, 360));
            Anim(splat, 0.8f, (s, k) => { SizeWH(s, 1.6f * u * Out(k * 4f), 1.2f * u * Out(k * 4f) * (1f - 0.2f * k)); s.color = Al(Color.white, Am * (k < 0.6f ? 1f : (1f - k) / 0.4f)); });
            for (int i = 0; i < 6; i++) { var d = Ast(Pw + "r_mud", aim, 0.2f * u, Al(Color.white, Am), 8); Run(Throw(d, aim, aim + new Vector3(Rnd(-0.8f, 0.8f), Rnd(-1.2f, -0.6f), 0f) * u, Rnd(0.4f, 0.6f), Rnd(0.1f, 0.5f) * u)); }
            if (!astMissed) Run(astTarget.Flash(RBrown, 2, 0.08f));
            Run(Squash(astTarget, 0.14f));
            AstHit(0.06f);
            yield return BattleTime.FxWait(0.5f);
        }

        /// <summary>바위 폭발: 발치의 돌멩이들을 끌어모아 앞에 띄웠다가, 산탄처럼 흩뿌려 쏟아붓는다</summary>
        IEnumerator RockBurstFx(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var front = from + dir * 0.8f * u;
            const int n = 9;
            var ps = new SpriteRenderer[n];
            GameAudio.Se("stat_down", 0.5f);
            for (int i = 0; i < n; i++)
            {
                var g = new Vector3(front.x + Rnd(-0.8f, 0.8f) * u, user.Feet.y, 0f);
                ps[i] = Ast(Pw + "r_pebble", g, Rnd(0.25f, 0.4f) * u, Al(Color.white, Am), 6, Rnd(0, 360));
                Run(Throw(ps[i], g, front + (Vector3)(Random.insideUnitCircle * 0.5f * u), 0.3f, 0.3f * u, 300f, false));
            }
            yield return BattleTime.FxWait(0.32f);
            GameAudio.Se("hit", 0.8f);
            Run(Recoil(user, dir, 0.15f, 0.12f));
            for (int i = 0; i < n; i++)
            {
                var p = Ast(Pw + "r_pebble", front + (Vector3)(Random.insideUnitCircle * 0.4f * u), Rnd(0.25f, 0.4f) * u, Al(Color.white, Am), 7, Rnd(0, 360));
                var end = aim + (Vector3)(Random.insideUnitCircle * 0.8f * u);
                Run(Throw(p, p.transform.position, end, Rnd(0.13f, 0.2f), 0f, 800f));
            }
            yield return BattleTime.FxWait(0.15f);
            RockBreak(aim, 1.4f * u, u, 6);
            Run(Squash(astTarget, 0.16f));
            AstHit(0.1f);
            yield return BattleTime.FxWait(0.45f);
        }

        /// <summary>바위 대포: 땅을 쩍 갈라 큼직한 바위를 뽑아 올리고, 대포알처럼 곧게 쏘아 박살 낸다</summary>
        IEnumerator RockCannon(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 dir = (aim - user.Center).normalized;
            var g = new Vector3(user.Center.x + dir.x * 0.8f * u, user.Feet.y, 0f);
            GameAudio.Se("stat_down", 0.6f);
            var cr = Ast(Pw + "r_crack", g, 1.8f * u, Al(Color.white, Am), 1);
            Dust(g, u, 5);
            var rk = Ast(Pw + "r_rock", g, 1.1f * u, Al(Color.white, Am), 6);
            var hover = g + Vector3.up * user.Height * 0.5f;
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { rk.transform.position = Vector3.Lerp(g, hover, BackOut(t / 0.35f)); rk.transform.Rotate(0, 0, 90f * BattleTime.FxDt); yield return null; }
            GameAudio.Se("hit", 0.8f);
            Run(Recoil(user, dir, 0.2f, 0.15f));
            yield return Throw(rk, hover, aim, 0.2f, 0f, 700f, false, (s, k) => { if (Random.value < 0.5f) Mote(s.transform.position, s.transform.position + Vector3.down * 0.3f * u, 0.3f * u, Color.white, 0.3f, 0f, Pw + "r_sand", 4); });
            GameAudio.Se("hit_strong");
            RockBreak(aim, 1.8f * u, u, 9);
            Anim(cr, 0.4f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            Run(Squash(astTarget, 0.22f));
            AstHit(0.14f);
            yield return BattleTime.FxWait(0.45f);
        }

        /// <summary>모래 폭풍: 몸 양옆에 모래 회오리가 일어나 상대를 감싸 빙빙 돌며 모래로 할퀸다</summary>
        IEnumerator SandTornado(PalView user, Vector3 aim)
        {
            float u = U;
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            var g = GroundAt(aim, u);
            var tw = new[] { Ast(Pw + "r_twister", ug + Vector3.left * 1.1f * u, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0.03f)), Ast(Pw + "r_twister", ug + Vector3.right * 1.1f * u, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0.03f)) };
            GameAudio.Se("shine", 0.5f);
            for (float t = 0f; t < 0.25f; t += BattleTime.FxDt) { foreach (var x in tw) SizeWH(x, 1f * u, 2.2f * u * Out(t / 0.25f)); yield return null; }
            var s0 = new[] { tw[0].transform.position, tw[1].transform.position };
            for (float t = 0f; t < 0.9f; t += BattleTime.FxDt)
            {
                float k = Mathf.Clamp01(t / 0.4f);
                for (int i = 0; i < 2; i++)
                {
                    float an = t * 9f + i * Mathf.PI;
                    var orbit = g + new Vector3(Mathf.Cos(an) * 1.1f, Mathf.Sin(an) * 0.3f, 0f) * u;
                    tw[i].transform.position = Vector3.Lerp(s0[i], orbit, Out(k));
                    tw[i].sortingOrder = (Mathf.Sin(an) < 0 || astTarget == null) ? sortingOrder + 6 : astTarget.SortingOrder - 1;
                    tw[i].flipX = Mathf.Repeat(t, 0.08f) < 0.04f;
                    if (Random.value < 0.3f) Dust(tw[i].transform.position, u * 0.4f, 1, 0.6f);
                }
                if (k >= 1f && !astHit) { GameAudio.Se("hit", 0.7f); AstHit(0.1f); }
                if (k >= 1f && Random.value < 0.15f) { Run(Squash(astTarget, 0.06f, 0.06f)); Impact(aim + (Vector3)(Random.insideUnitCircle * 0.5f * u), 0.6f * u, RSand, 0.12f); }
                yield return null;
            }
            foreach (var x in tw) Anim(x, 0.25f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            var c = Ast(Pw + "r_sand", aim, 0f, Al(Color.white, Am), 7);
            Anim(c, 0.5f, (s, k) => { Size(s, 3f * u * Out(k)); s.color = Al(Color.white, Am * (1f - k)); });
            yield return BattleTime.FxWait(0.4f);
        }

        /// <summary>암석 폭발: 발을 쿵 굴러 땅을 뒤흔들면, 둘레 땅에서 암석 덩어리가 튀어 올라 사방으로 흩어지며 상대를 때린다</summary>
        IEnumerator RockBlast(PalView user, Vector3 aim)
        {
            float u = U;
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            Run(Squash(user, 0.2f, 0.2f));
            GameAudio.Se("hit_strong", 0.8f);
            Quake(ug, 3f * u, u);
            for (int i = 0; i < 10; i++)
            {
                var start = ug + new Vector3(Rnd(-1.4f, 1.4f), Rnd(-0.2f, 0.2f), 0f) * u;
                bool toTarget = i % 3 == 0;
                var end = toTarget ? aim + (Vector3)(Random.insideUnitCircle * 0.5f * u) : start + new Vector3(Rnd(-2.5f, 2.5f), Rnd(-0.5f, 0.3f), 0f) * u;
                var rk = Ast(Pw + "r_rock", start, Rnd(0.4f, 0.7f) * u, Al(Color.white, Am), 6, Rnd(0, 360));
                Run(Throw(rk, start, end, Rnd(0.35f, 0.5f), Rnd(1.2f, 2f) * u, Rnd(-500f, 500f)));
                if (toTarget) Run(DelayThen(0.45f, Do(() => { GameAudio.Se("hit", 0.6f); RockBreak(end, 1f * u, u, 4); Run(Squash(astTarget, 0.12f)); AstHit(0.12f); })));
                yield return BattleTime.FxWait(0.03f);
            }
            yield return BattleTime.FxWait(0.7f);
        }

        /// <summary>바위 창: 땅 밑으로 균열이 상대까지 쩍쩍 뻗어 가더니, 발밑에서 거대한 바위 창이 솟구친다</summary>
        IEnumerator RockSpear(PalView user, Vector3 aim)
        {
            float u = U;
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            var g = GroundAt(aim, u);
            GameAudio.Se("stat_down", 0.6f);
            for (int i = 1; i <= 6; i++)
            {
                var p = Vector3.Lerp(ug, g, i / 6f);
                var cr = Ast(Pw + "r_crack", p, 1f * u, Al(Color.white, Am), 1, Rnd(-15f, 15f));
                Anim(cr, 1f, (s, k) => s.color = Al(Color.white, Am * (1f - k * k)));
                Dust(p, u * 0.5f, 2, 0.5f);
                yield return BattleTime.FxWait(0.05f);
            }
            GameAudio.Se("hit_strong");
            StoneSpike(g, 1.4f * u, 3.8f * u, 0.9f, 0f, 7);
            StoneSpike(g + Vector3.left * 0.7f * u, 0.7f * u, 1.8f * u, 0.8f, 22f, 5);
            StoneSpike(g + Vector3.right * 0.7f * u, 0.7f * u, 2f * u, 0.8f, -20f, 5);
            RockBreak(g + Vector3.up * 0.5f * u, 1.6f * u, u, 9);
            Run(CameraShake(0.2f, 0.3f));
            Run(Squash(astTarget, 0.26f));
            AstHit(0.18f);
            yield return BattleTime.FxWait(0.8f);
        }

        /// <summary>샌드 트위스터: 모래 회오리 셋이 상대 둘레를 휘몰아 돌며 모래 폭풍을 일으키고, 한가운데로 모여 한꺼번에 터진다</summary>
        IEnumerator SandTwister(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.45f, 0.3f));
            var g = GroundAt(aim, u);
            var tw = new SpriteRenderer[3];
            GameAudio.Se("shine", 0.6f);
            for (int i = 0; i < 3; i++) tw[i] = Ast(Pw + "r_twister", g, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0.03f));
            for (float t = 0f; t < 1.1f; t += BattleTime.FxDt)
            {
                float rad = t < 0.8f ? 2.2f : Mathf.Lerp(2.2f, 0f, (t - 0.8f) / 0.3f);
                for (int i = 0; i < 3; i++)
                {
                    float an = t * 6f + i * Mathf.PI * 2f / 3f;
                    tw[i].transform.position = g + new Vector3(Mathf.Cos(an) * rad, Mathf.Sin(an) * rad * 0.3f, 0f) * u;
                    SizeWH(tw[i], 1.1f * u, 2.8f * u * Out(t / 0.3f));
                    tw[i].sortingOrder = (Mathf.Sin(an) < 0 || astTarget == null) ? sortingOrder + 6 : astTarget.SortingOrder - 1;
                    tw[i].flipX = Mathf.Repeat(t, 0.08f) < 0.04f;
                    if (Random.value < 0.3f) Dust(tw[i].transform.position, u * 0.4f, 1, 0.6f);
                }
                if (Random.value < 0.3f) { var c = Ast(Pw + "r_sand", g + new Vector3(Rnd(-2f, 2f), Rnd(0.2f, 2f), 0f) * u, 1.2f * u, Al(Color.white, 0.5f * Am), 4); Anim(c, 0.5f, (s, k) => s.color = Al(Color.white, 0.5f * Am * Bell(k))); }
                if (t > 0.4f && !astHit) { AstHit(0.1f); }
                if (t > 0.4f && Random.value < 0.1f) Run(Squash(astTarget, 0.06f, 0.06f));
                yield return null;
            }
            GameAudio.Se("hit_strong");
            foreach (var x in tw) Destroy(x.gameObject);
            var big = Ast(Pw + "r_twister", g, 0f, Al(Color.white, Am), 8, 0f, new Vector2(0.5f, 0.03f));
            Anim(big, 0.5f, (s, k) => { SizeWH(s, 2.6f * u * (1f + k), 4f * u * Bell(k)); s.flipX = Mathf.Repeat(k, 0.1f) < 0.05f; });
            RockBreak(aim, 2.4f * u, u, 10);
            Run(Squash(astTarget, 0.26f));
            Run(CameraShake(0.18f, 0.3f));
            yield return BattleTime.FxWait(0.55f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>스톤 비트: 땅을 뒤흔들어 상대 앞의 거대한 바위를 공중 높이 띄워 올리고, 그대로 머리 위로 내리찍는다</summary>
        IEnumerator StoneBeat(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.55f, 0.3f));
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            Run(Squash(user, 0.2f, 0.25f));
            GameAudio.Se("stat_down", 0.7f);
            Quake(ug, 2.4f * u, u);
            var g = GroundAt(aim, u);
            var src = g + (ug - g).normalized * 1.6f * u;
            var rk = Ast(Pw + "r_rock", src, 2.6f * u, Al(Color.white, Am), 7);
            Dust(src, u, 8);
            var cam = CamRect();
            var high = new Vector3(aim.x, Mathf.Min(aim.y + 3f * u, cam.yMax - 1.3f * u), 0f);
            for (float t = 0f; t < 0.55f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.55f);
                rk.transform.position = Vector3.Lerp(src, high, k) + Vector3.right * Mathf.Sin(t * 50f) * 0.04f * u;
                rk.transform.Rotate(0, 0, 40f * BattleTime.FxDt);
                if (Random.value < 0.4f) Mote(rk.transform.position, rk.transform.position + Vector3.down * 0.8f * u, 0.25f * u, Color.white, 0.3f, 0f, Pw + "r_pebble", 5);
                yield return null;
            }
            yield return BattleTime.FxWait(0.15f);
            GameAudio.Se("select", 0.8f);
            for (float t = 0f; t < 0.16f; t += BattleTime.FxDt) { rk.transform.position = Vector3.Lerp(high, aim, In(t / 0.16f)); yield return null; }
            Destroy(rk.gameObject);
            GameAudio.Se("hit_strong");
            ScreenFlash(RSand, 0.35f, 0.02f, 0.04f, 0.3f);
            RockBreak(aim, 3f * u, u, 16);
            Quake(g, 4.4f * u, u);
            Run(Squash(astTarget, 0.32f));
            Run(CameraShake(0.3f, 0.45f));
            AstHit(0.28f);
            yield return BattleTime.FxWait(0.65f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>파이팅 슬래시 (펌푸린): 입에 문 봉을 휘둘러 위아래로 두 번 연달아 후려친다</summary>
        IEnumerator FightingSlash(PalView user, Vector3 aim)
        {
            float u = U;
            bool flip = (aim - user.Center).x < 0;
            yield return user.Lunge(aim, 99f, 0.35f, () => Run(TwoSwings(aim, u, flip)));
            yield return BattleTime.FxWait(0.4f);
        }

        IEnumerator TwoSwings(Vector3 aim, float u, bool flip)
        {
            for (int i = 0; i < 2; i++)
            {
                GameAudio.Se(i == 1 ? "hit_strong" : "hit", 0.8f);
                float dirS = i == 0 ? -1f : 1f;
                var sw = Ast(Pw + "r_swing", aim, 0f, Al(Color.white, Am), 8, (i == 0 ? -30f : 150f) + (flip ? 180f : 0f));
                sw.flipX = flip;
                Anim(sw, 0.25f, (s, k) => { Size(s, 2.4f * u * Out(k * 3f)); s.transform.Rotate(0, 0, dirS * 300f * BattleTime.FxDt); s.color = Al(Color.white, Am * (1f - k)); });
                Impact(aim, 1.2f * u, RSand, 0.16f);
                Run(Squash(astTarget, i == 1 ? 0.2f : 0.1f, 0.1f));
                if (i == 1) AstHit(0.12f);
                yield return BattleTime.FxWait(0.14f);
            }
        }

        /// <summary>스핀 레그 슬래시 (아누비스): 몸을 팽이처럼 회전시키며 다가가, 튼튼한 다리로 크게 돌려차 넘어뜨린다</summary>
        IEnumerator SpinLegSlash(PalView user, Vector3 aim)
        {
            float u = U;
            bool flip = (aim - user.Center).x < 0;
            bool going = true;
            Run(SpinRings(user, () => going));
            yield return user.Lunge(aim, 99f, 0.4f, () =>
            {
                GameAudio.Se("hit_strong");
                var sw = Ast(Pw + "r_swing", aim + Vector3.down * 0.3f * u, 0f, Al(Color.white, Am), 8, flip ? 180f : 0f);
                sw.flipX = flip;
                Anim(sw, 0.35f, (s, k) => { SizeWH(s, 3.6f * u * Out(k * 3f), 2.2f * u * Out(k * 3f)); s.transform.Rotate(0, 0, (flip ? 1 : -1) * 260f * BattleTime.FxDt); s.color = Al(Color.white, Am * (1f - k)); });
                Impact(aim, 2f * u, RSand, 0.2f);
                Dust(GroundAt(aim, u), u, 8, 2f);
                Run(Squash(astTarget, 0.26f));
                AstHit(0.18f);
            });
            going = false;
            yield return BattleTime.FxWait(0.5f);
        }

        IEnumerator SpinRings(PalView user, System.Func<bool> keep)
        {
            while (keep())
            {
                var p = user.Center;
                var r = Ast(Pw + "r_swing", p, 1.6f * U, Al(Color.white, 0.6f), 4, Rnd(0, 360));
                Anim(r, 0.2f, (s, k) => { s.transform.Rotate(0, 0, 900f * BattleTime.FxDt); s.color = Al(Color.white, 0.6f * (1f - k)); });
                Dust(new Vector3(p.x, user.Feet.y, 0f), U * 0.4f, 1, 0.6f);
                yield return BattleTime.FxWait(0.04f);
            }
        }

        /// <summary>포스 드라이브 (아누비스): 순간이동하듯 파고들어 주먹을 눈에 안 보이게 쏟아붓고, 마지막 한 방으로 날려 버린다</summary>
        IEnumerator ForceDrive(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.5f, 0.15f));
            bool going = true;
            Run(AfterimageTrail(user, () => going));
            yield return user.Lunge(aim, 99f, 0.18f, () => { });
            going = false;
            for (int i = 0; i < 10; i++)
            {
                var p = aim + (Vector3)(Random.insideUnitCircle * 0.6f * u);
                GameAudio.Se("hit", 0.35f);
                Impact(p, Rnd(0.7f, 1.1f) * u, RSand, 0.1f);
                SpeedLines(p, 1.6f * u, 0.12f, RSand, 0.5f);
                Run(Squash(astTarget, 0.06f, 0.05f));
                if (i == 3) AstHit(0.05f);
                yield return BattleTime.FxWait(0.04f);
            }
            GameAudio.Se("hit_strong");
            ScreenFlash(RSand, 0.4f, 0.02f, 0.04f, 0.3f);
            Impact(aim, 2.8f * u, RFlame, 0.25f);
            Ring(aim, 0.4f * u, 3.2f * u, Al(Color.white, Am), 0.4f, Pw + "r_quake");
            SpeedLines(aim, 6f * u, 0.4f, RSand);
            Run(Squash(astTarget, 0.3f));
            Run(CameraShake(0.22f, 0.35f));
            yield return BattleTime.FxWait(0.55f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>그라운드 스매셔 (아누비스): 화면 밖으로 높이 뛰어올랐다가 상대 앞에 떨어지며 주먹으로 땅을 내리쳐, 바위가 겹겹이 솟는다</summary>
        IEnumerator GroundSmasher(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.55f, 0.3f));
            var tr = user.transform; var home = tr.position;
            var cam = CamRect();
            GameAudio.Se("select", 0.8f);
            Dust(new Vector3(user.Center.x, user.Feet.y, 0f), u, 6);
            float up = cam.yMax + user.Height - home.y;
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt) { tr.position = home + Vector3.up * up * In(t / 0.3f); yield return null; }
            var g = GroundAt(aim, u);
            var land = new Vector3(Mathf.Lerp(home.x, aim.x, 0.75f), home.y, home.z);
            var shadow = Ast(Pw + "r_sand", new Vector3(land.x, g.y, 0f), 0f, Al(Color.black, 0.5f), 1);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { Size(shadow, 2f * u * Out(t / 0.35f), 0.3f); yield return null; }
            var topP = land + Vector3.up * up;
            for (float t = 0f; t < 0.14f; t += BattleTime.FxDt) { tr.position = Vector3.Lerp(topP, land, In(t / 0.14f)); yield return null; }
            tr.position = land;
            Destroy(shadow.gameObject);
            GameAudio.Se("hit_strong");
            ScreenFlash(RSand, 0.45f, 0.02f, 0.04f, 0.3f);
            Quake(g, 5f * u, u);
            for (int r = 1; r <= 2; r++)
                for (int i = 0; i < 4 + r * 2; i++)
                {
                    float an = i * Mathf.PI * 2f / (4 + r * 2);
                    var p = g + new Vector3(Mathf.Cos(an) * r * 1f, Mathf.Sin(an) * r * 0.3f, 0f) * u;
                    StoneSpike(p, 0.6f * u, (2.4f - r * 0.5f) * u, 0.8f, -Mathf.Cos(an) * 25f, Mathf.Sin(an) > 0 && astTarget != null ? Behind(astTarget) : 6);
                }
            RockBreak(aim, 2.6f * u, u, 12);
            Run(Squash(astTarget, 0.32f));
            Run(CameraShake(0.3f, 0.45f));
            AstHit(0.28f);
            yield return BattleTime.FxWait(0.5f);
            for (float t = 0f; t < 0.25f; t += BattleTime.FxDt) { tr.position = Vector3.Lerp(land, home, Out(t / 0.25f)) + Vector3.up * Bell(t / 0.25f) * 0.6f * u; yield return null; }
            tr.position = home;
            yield return BattleTime.FxWait(0.2f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>서머솔트 스크래치 (세크메트): 발톱으로 날카롭게 할퀸 뒤, 공중제비를 돌며 뒤로 휙 물러난다</summary>
        IEnumerator SomersaultScratch(PalView user, Vector3 aim)
        {
            float u = U;
            bool flip = (aim - user.Center).x < 0;
            var tr = user.transform; var home = tr.position;
            yield return user.Lunge(aim, 99f, 0.3f, () =>
            {
                GameAudio.Se("hit_strong");
                var c = Ast(Pw + "r_claw", aim, 0f, Al(Color.white, Am), 8);
                c.flipX = flip;
                Anim(c, 0.35f, (s, k) => { Size(s, 2.2f * u * Out(k * 4f)); s.color = Al(Color.white, Am * (1f - k * k)); });
                Impact(aim, 1.4f * u, RSand, 0.18f);
                Run(Squash(astTarget, 0.2f));
                AstHit(0.14f);
            });
            var back = home - (aim - home).normalized * 0.8f * u;
            float spin = flip ? 360f : -360f;
            for (float t = 0f; t < 0.4f; t += BattleTime.FxDt)
            {
                float k = t / 0.4f;
                tr.position = Vector3.Lerp(home, back, k) + Vector3.up * Bell(k) * 1.4f * u;
                if (Random.value < 0.6f) Afterimage(user, Al(RSand, 0.5f), 0.2f);
                var sw = Ast(Pw + "r_swing", user.Center, 1.4f * u, Al(Color.white, 0.4f), 3, spin * k);
                Anim(sw, 0.12f, (s, kk) => s.color = Al(Color.white, 0.4f * (1f - kk)));
                yield return null;
            }
            for (float t = 0f; t < 0.2f; t += BattleTime.FxDt) { tr.position = Vector3.Lerp(back, home, Out(t / 0.2f)); yield return null; }
            tr.position = home;
            Dust(new Vector3(user.Center.x, user.Feet.y, 0f), u, 4);
            yield return BattleTime.FxWait(0.25f);
        }

        /// <summary>롤링 스크래치 (세크메트): 몸을 공처럼 말아 데굴데굴 굴러 파고든 뒤, 튀어 오르며 발톱으로 X자로 할퀸다</summary>
        IEnumerator RollingScratch(PalView user, Vector3 aim)
        {
            float u = U;
            bool flip = (aim - user.Center).x < 0;
            bool going = true;
            Run(RollTrail(user, () => going, flip));
            yield return user.Lunge(aim, 99f, 0.4f, () => Run(XClaw(aim, u, flip)));
            going = false;
            yield return BattleTime.FxWait(0.5f);
        }

        IEnumerator RollTrail(PalView user, System.Func<bool> keep, bool flip)
        {
            float rot = 0f;
            while (keep())
            {
                rot += (flip ? 1 : -1) * 40f;
                var r = Ast(Pw + "r_quake", user.Center, user.Height * 0.9f, Al(Color.white, 0.7f), 5, rot);
                Anim(r, 0.12f, (s, k) => s.color = Al(Color.white, 0.7f * (1f - k)));
                Dust(new Vector3(user.Center.x, user.Feet.y, 0f), U * 0.4f, 1, 0.5f);
                yield return BattleTime.FxWait(0.03f);
            }
        }

        IEnumerator XClaw(Vector3 aim, float u, bool flip)
        {
            for (int i = 0; i < 2; i++)
            {
                GameAudio.Se(i == 1 ? "hit_strong" : "hit", 0.8f);
                var c = Ast(Pw + "r_claw", aim, 0f, Al(Color.white, Am), 8, i == 0 ? 0f : 70f);
                c.flipX = flip ^ (i == 1);
                Anim(c, 0.35f, (s, k) => { Size(s, 2.4f * u * Out(k * 4f)); s.color = Al(Color.white, Am * (1f - k * k)); });
                Impact(aim, 1.3f * u, RSand, 0.16f);
                Run(Squash(astTarget, i == 1 ? 0.22f : 0.1f));
                if (i == 1) AstHit(0.16f);
                yield return BattleTime.FxWait(0.12f);
            }
        }

        /// <summary>작열 미사일 (이지드론): 날개의 발사구에서 미사일을 줄줄이 쏘아 올리고, 포물선을 그리며 상대 둘레에 쏟아져 연쇄 폭발한다</summary>
        IEnumerator BlazingMissile(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.55f, 0.3f));
            var c = user.Center;
            const int n = 14;
            for (int i = 0; i < n; i++)
            {
                bool last = i == n - 1;
                var from = c + new Vector3((i % 2 == 0 ? -1f : 1f) * 0.7f * u, 0.4f * u, 0f);
                var end = last ? aim : aim + new Vector3(Rnd(-1.4f, 1.4f), Rnd(-0.5f, 0.5f), 0f) * u;
                var m = Ast(Pw + "r_missile", from, 0.8f * u, Al(Color.white, Am), 7);
                Run(Throw(m, from, end, 0.42f, Rnd(1.8f, 2.8f) * u, 0f, true, (s, k) => { if (Random.value < 0.6f) { var sm = Ast(Pw + "r_sand", s.transform.position, 0.3f * u, Al(Color.white, 0.6f), 4); Anim(sm, 0.4f, (r, kk) => { Size(r, 0.3f * u * (1f + kk)); r.color = Al(Color.white, 0.6f * (1f - kk)); }); } }));
                GameAudio.Se("select", 0.35f);
                Run(DelayThen(0.42f, Do(() =>
                {
                    GameAudio.Se(last ? "hit_strong" : "hit", 0.5f);
                    Impact(end, (last ? 2.6f : 1.2f) * u, RFlame, 0.2f);
                    var b = Ast(Pw + "r_sand", end, 0f, Al(RFlame, Am), 6);
                    Anim(b, 0.4f, (s, k) => { Size(s, (last ? 3f : 1.4f) * u * Out(k)); s.color = Al(Color.Lerp(RFlame, Color.white, k), Am * (1f - k)); });
                    Run(CameraShake(last ? 0.25f : 0.05f, 0.1f));
                    Run(Squash(astTarget, last ? 0.28f : 0.05f));
                    if (last) { ScreenFlash(RFlame, 0.45f, 0.02f, 0.04f, 0.3f); RockBreak(end, 2f * u, u, 8); AstHit(0.24f); }
                })));
                yield return BattleTime.FxWait(0.05f);
            }
            yield return BattleTime.FxWait(0.9f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>멧돼지 돌진 (돌진돼지): 앞발로 땅을 긁어 흙먼지를 차올리며 겨누고, 흙바람을 일으키며 맹렬히 들이받는다</summary>
        IEnumerator BoarRush(PalView user, Vector3 aim)
        {
            float u = U;
            var feet = new Vector3(user.Center.x, user.Feet.y, 0f);
            Vector3 back = -(aim - user.Center).normalized;
            for (int i = 0; i < 3; i++)
            {
                GameAudio.Se("select", 0.4f);
                Run(Recoil(user, -back, 0.08f, 0.1f));
                for (int j = 0; j < 3; j++) { var p = feet + Vector3.right * Rnd(-0.2f, 0.2f) * u; Mote(p, p + new Vector3(back.x * Rnd(0.6f, 1.2f), Rnd(0.2f, 0.6f), 0f) * u, 0.3f * u, Color.white, 0.4f, 0f, Pw + "r_sand", 3); }
                yield return BattleTime.FxWait(0.12f);
            }
            bool going = true;
            Run(DustTrail(user, () => going));
            SpeedLines(aim, 5f * u, 0.4f, RSand);
            yield return user.Lunge(aim, 99f, 0.32f, () =>
            {
                GameAudio.Se("hit_strong");
                Impact(aim, 2.2f * u, RSand, 0.22f);
                Ring(aim, 0.4f * u, 3f * u, Al(Color.white, Am), 0.4f, Pw + "r_quake");
                RockBreak(aim, 1.4f * u, u, 6);
                Run(Squash(astTarget, 0.26f));
                AstHit(0.16f);
            });
            going = false;
            yield return BattleTime.FxWait(0.45f);
        }

        IEnumerator DustTrail(PalView user, System.Func<bool> keep)
        {
            while (keep())
            {
                var p = new Vector3(user.Center.x, user.Feet.y, 0f);
                var c = Ast(Pw + "r_sand", p, 0.6f * U, Al(Color.white, 0.8f), 3, Rnd(0, 360));
                Anim(c, 0.45f, (s, k) => { Size(s, 0.6f * U * (1f + 1.5f * k)); s.color = Al(Color.white, 0.8f * (1f - k)); });
                yield return BattleTime.FxWait(0.03f);
            }
        }
    }
}
