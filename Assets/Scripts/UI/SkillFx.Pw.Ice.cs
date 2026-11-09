using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>얼음 속성 팰월드 기술 연출 (그림: PwFx/i_*)</summary>
    public partial class SkillFx
    {
        static readonly Color IBlue = C(0x7cc8ff), IWhite = C(0xeef9ff), IDeep = C(0x3a7fe0);

        static partial void RegIce()
        {
            PwFx["Ice_Missile"] = (fx, u, t, a) => fx.IceMissile(u, a);
            PwFx["Ice_Blade"] = (fx, u, t, a) => fx.IceBlade(u, a);
            PwFx["Iceberg"] = (fx, u, t, a) => fx.Iceberg(u, a);
            PwFx["Frost_Breath"] = (fx, u, t, a) => fx.FrostBreath(u, a);
            PwFx["Freeze_Wall"] = (fx, u, t, a) => fx.FreezeWall(u, a);
            PwFx["Icicle_Bullet"] = (fx, u, t, a) => fx.IcicleBullet(u, a);
            PwFx["Icicle_Line"] = (fx, u, t, a) => fx.IcicleLine(u, a);
            PwFx["Blizzard_Spike"] = (fx, u, t, a) => fx.BlizzardSpike(u, a);
            PwFx["Diamond_Fall"] = (fx, u, t, a) => fx.DiamondFall(u, a);
            PwFx["Double_Blizzard_Spike"] = (fx, u, t, a) => fx.DoubleBlizzardSpike(u, a);
            PwFx["Frost_Out"] = (fx, u, t, a) => fx.FrostOut(u, a);
            PwFx["Crystal_Wing"] = (fx, u, t, a) => fx.CrystalWing(u, a);
            PwFx["Snowball_Roll"] = (fx, u, t, a) => fx.SnowballRoll(u, a);
        }

        // ───── 공용 조각 ─────

        /// <summary>얼음이 와장창 깨진다: 얼음 조각 + 눈송이 + 냉기</summary>
        void IceBreak(Vector3 at, float size, float u, int n = 8)
        {
            for (int i = 0; i < n; i++)
            {
                var sh = Ast(Pw + "i_shard", at, Rnd(0.25f, 0.45f) * u, Al(Color.white, Am), 7, Rnd(0, 360));
                var end = at + Dir(Rnd(0, 360)) * size * Rnd(0.5f, 1f);
                Run(Throw(sh, at, end + Vector3.down * 0.4f * u, Rnd(0.3f, 0.45f), Rnd(0.2f, 0.6f) * u, Rnd(-700f, 700f)));
            }
            for (int i = 0; i < n / 2; i++) Mote(at, at + Dir(Rnd(0, 360)) * size * 0.8f + Vector3.up * 0.3f * u, 0.22f * u, Color.white, Rnd(0.4f, 0.7f), 0f, Pw + "i_snow", 6);
            var m = Ast(Pw + "i_mist", at, 0f, Al(Color.white, 0.8f * Am), 5);
            Anim(m, 0.5f, (s, k) => { Size(s, size * 1.4f * Out(k)); s.color = Al(Color.white, 0.8f * Am * (1f - k)); });
            Impact(at, size * 0.7f, IWhite, 0.18f);
        }

        /// <summary>맞은 팰이 하얗게 얼어붙듯 번쩍인다</summary>
        void Frosted(float u)
        {
            if (astMissed || astTarget == null) return;
            Run(astTarget.Flash(IBlue, 2, 0.07f));
            var c = astTarget.Center;
            for (int i = 0; i < 5; i++) Mote(c + (Vector3)(Random.insideUnitCircle * 0.6f * u), c + (Vector3)(Random.insideUnitCircle * 0.9f * u) + Vector3.up * 0.4f * u, 0.18f * u, Color.white, 0.5f, i * 0.04f, Pw + "i_snow", 7);
        }

        /// <summary>땅에서 얼음 가시가 솟았다가 부서진다</summary>
        void IceSpike(Vector3 ground, float w, float h, float life, float ang = 0f, int sort = 5)
        {
            var s = Ast(Pw + "i_spike", ground, 0f, Al(Color.white, Am), sort, ang, new Vector2(0.5f, 0.02f));
            Anim(s, life, (sr, k) => { float up = k < 0.15f ? BackOut(k / 0.15f) : 1f; SizeWH(sr, w, h * up); sr.color = Al(Color.white, Am * (k < 0.75f ? 1f : (1f - k) / 0.25f)); });
        }

        // ───── 기술 ─────

        /// <summary>얼음 미사일: 머리 위 허공에 고드름 셋이 얼어붙어 생기고, 차례로 날아가 꽂히며 깨진다</summary>
        IEnumerator IceMissile(PalView user, Vector3 aim)
        {
            float u = U;
            var top = user.Center + Vector3.up * user.Height * 0.5f;
            var ics = new SpriteRenderer[3];
            GameAudio.Se("shine", 0.5f);
            for (int i = 0; i < 3; i++)
            {
                var p = top + Vector3.right * (i - 1) * 0.5f * u;
                ics[i] = Ast(Pw + "i_icicle", p, 0f, Al(Color.white, Am), 6, Ang(aim - p) + 90f);
                Run(PopIn(ics[i], 0.35f * u, i * 0.06f));
            }
            yield return BattleTime.FxWait(0.3f);
            for (int i = 0; i < 3; i++)
            {
                var p = ics[i].transform.position;
                var end = aim + new Vector3(Rnd(-0.3f, 0.3f), Rnd(-0.3f, 0.3f), 0f) * u;
                GameAudio.Se("select", 0.5f);
                Run(Throw(ics[i], p, end, 0.16f));
                bool last = i == 2;
                Run(DelayThen(0.16f, Do(() => { GameAudio.Se("hit", 0.5f); IceBreak(end, 0.9f * u, u, 5); Run(Squash(astTarget, 0.08f, 0.08f)); if (last) { Frosted(u); AstHit(0.06f); } })));
                yield return BattleTime.FxWait(0.08f);
            }
            yield return BattleTime.FxWait(0.45f);
        }

        /// <summary>얼음 칼날: 초승달 모양 얼음 날을 휙 던져, 서리 자국을 남기며 베고 지나간다</summary>
        IEnumerator IceBlade(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = aim - from;
            yield return user.WindUp(aim, 0.12f);
            GameAudio.Se("select", 0.7f);
            var b = Ast(Pw + "i_crescent", from, 1.4f * u, Al(Color.white, Am), 6, Ang(dir) - 90f);
            yield return Throw(b, from, aim, 0.2f, 0f, 0f, false, (s, k) => { if (Random.value < 0.6f) Mote(s.transform.position, s.transform.position + Vector3.down * 0.3f * u, 0.15f * u, Color.white, 0.4f, 0f, Pw + "i_snow", 5); });
            GameAudio.Se("hit", 0.8f);
            var sl = Ast(Pw + "i_crescent", aim, 0f, Al(Color.white, Am), 8, Ang(dir) - 60f);
            Anim(sl, 0.35f, (s, k) => { Size(s, 2.4f * u * Out(k * 3f)); s.color = Al(Color.white, Am * (1f - k)); });
            IceBreak(aim, 1.2f * u, u, 6);
            Frosted(u);
            Run(Squash(astTarget, 0.16f));
            AstHit(0.08f);
            yield return BattleTime.FxWait(0.4f);
        }

        /// <summary>빙산: 상대 발밑이 하얗게 얼어붙더니, 날카로운 얼음칼이 아래에서 솟구쳐 찌른다</summary>
        IEnumerator Iceberg(PalView user, Vector3 aim)
        {
            float u = U;
            yield return user.WindUp(aim, 0.14f);
            var g = GroundAt(aim, u);
            var frost = Ast(Pw + "i_ring", g, 0f, Al(Color.white, Am), 2);
            GameAudio.Se("stat_down", 0.5f);
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt) { Size(frost, 2.6f * u * Out(t / 0.3f), 0.3f); yield return null; }
            GameAudio.Se("hit_strong", 0.8f);
            IceSpike(g, 1.1f * u, 3f * u, 0.8f, 0f, 6);
            IceSpike(g + Vector3.left * 0.5f * u, 0.6f * u, 1.6f * u, 0.7f, 18f, 5);
            IceSpike(g + Vector3.right * 0.5f * u, 0.6f * u, 1.8f * u, 0.7f, -15f, 5);
            IceBreak(g + Vector3.up * 0.4f * u, 1.4f * u, u, 8);
            Frosted(u);
            Run(Squash(astTarget, 0.22f));
            Run(CameraShake(0.1f, 0.2f));
            AstHit(0.12f);
            yield return BattleTime.FxWait(0.7f);
            Anim(frost, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            IceBreak(g + Vector3.up * 1.2f * u, 1.2f * u, u, 6);
            yield return BattleTime.FxWait(0.3f);
        }

        /// <summary>서리 낀 입김: 하얀 숨을 길게 내뿜어, 눈송이 섞인 냉기가 상대를 감싸 얼어붙게 한다</summary>
        IEnumerator FrostBreath(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.55f * u;
            Run(DimTo(0.3f, 0.25f));
            GameAudio.Se("shine", 0.6f);
            float len = Vector3.Distance(mouth, aim) + 1.2f * u;
            var cone = Ast(Pw + "i_breath", mouth, 0f, Al(Color.white, Am), 5, Ang(dir), new Vector2(0f, 0.5f));
            for (float t = 0f; t < 0.9f; t += BattleTime.FxDt)
            {
                float f = t > 0.7f ? 1f - (t - 0.7f) / 0.2f : 1f;
                SizeWH(cone, len * Out(t / 0.15f), 2.2f * u * (1f + 0.1f * Mathf.Sin(t * 30f)) * f);
                cone.flipY = Mathf.Repeat(t, 0.12f) < 0.06f;
                if (Random.value < 0.7f) Mote(mouth, aim + (Vector3)(Random.insideUnitCircle * 0.9f * u), 0.2f * u, Color.white, 0.35f, 0f, Pw + "i_snow", 6);
                if (t > 0.15f && !astHit) { GameAudio.Se("hit", 0.6f); Frosted(u); Run(Squash(astTarget, 0.1f, 0.6f)); AstHit(0.08f); }
                yield return null;
            }
            Destroy(cone.gameObject);
            var crust = Ast(Pw + "i_ring", aim, 0f, Al(Color.white, Am), 7);
            Anim(crust, 0.5f, (s, k) => { Size(s, 2f * u * Out(k * 3f)); s.color = Al(Color.white, Am * (1f - k)); });
            IceBreak(aim, 1.3f * u, u, 6);
            yield return BattleTime.FxWait(0.3f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>프리즈 월: 땅에서 두꺼운 얼음 벽이 쿵 솟아올라 그대로 미끄러져 상대를 들이받고 산산이 부서진다</summary>
        IEnumerator FreezeWall(PalView user, Vector3 aim)
        {
            float u = U;
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            var g = GroundAt(aim, u);
            var start = Vector3.Lerp(ug, g, 0.3f);
            var wall = Ast(Pw + "i_wall", start, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0f));
            GameAudio.Se("hit", 0.8f);
            for (float t = 0f; t < 0.2f; t += BattleTime.FxDt) { SizeWH(wall, 1.5f * u, 2.2f * u * BackOut(t / 0.2f)); yield return null; }
            Dust(start, u, 6);
            Run(CameraShake(0.06f, 0.12f));
            var end = g + (ug - g).normalized * 0.4f * u;
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt)
            {
                wall.transform.position = Vector3.Lerp(start, end, In(t / 0.3f));
                if (Random.value < 0.6f) Mote(wall.transform.position, wall.transform.position + new Vector3(Rnd(-0.5f, 0.5f), 0.3f, 0f) * u, 0.2f * u, Color.white, 0.3f, 0f, Pw + "i_snow", 5);
                yield return null;
            }
            GameAudio.Se("hit_strong");
            Destroy(wall.gameObject);
            for (int i = 0; i < 6; i++)
            {
                var ch = Ast(Pw + "i_chunk", end + Vector3.up * Rnd(0.3f, 1.8f) * u, Rnd(0.4f, 0.7f) * u, Al(Color.white, Am), 7, Rnd(0, 360));
                Run(Throw(ch, ch.transform.position, ch.transform.position + new Vector3(Rnd(-1.6f, 1.6f), -0.6f, 0f) * u, 0.45f, Rnd(0.6f, 1.2f) * u, Rnd(-500f, 500f)));
            }
            IceBreak(aim, 1.8f * u, u, 10);
            Frosted(u);
            Run(Squash(astTarget, 0.24f));
            AstHit(0.14f);
            yield return BattleTime.FxWait(0.5f);
        }

        /// <summary>아이시클 불릿: 하늘에 고드름 여섯 개가 부채꼴로 얼어붙고, 끝을 상대에게 겨눈 뒤 한 발씩 쏘아 보낸다</summary>
        IEnumerator IcicleBullet(PalView user, Vector3 aim)
        {
            float u = U;
            var c = user.Center + Vector3.up * user.Height * 0.45f;
            const int n = 6;
            var ics = new SpriteRenderer[n];
            GameAudio.Se("shine", 0.6f);
            for (int i = 0; i < n; i++)
            {
                var p = c + Dir(30f + i * 24f) * 1.2f * u;
                ics[i] = Ast(Pw + "i_icicle", p, 0f, Al(Color.white, Am), 6, Ang(aim - p) + 90f);
                Run(PopIn(ics[i], 0.42f * u, i * 0.04f));
                Mote(p, p + Vector3.up * 0.2f * u, 0.18f * u, Color.white, 0.3f, i * 0.04f, Pw + "i_snow", 7);
            }
            yield return BattleTime.FxWait(0.45f);
            for (int i = 0; i < n; i++)
            {
                var p = ics[i].transform.position;
                var end = aim + (Vector3)(Random.insideUnitCircle * 0.45f * u);
                ics[i].transform.rotation = Quaternion.Euler(0, 0, Ang(end - p) + 90f);
                GameAudio.Se("select", 0.4f);
                Run(Throw(ics[i], p, end, 0.12f));
                bool last = i == n - 1;
                Run(DelayThen(0.12f, Do(() => { GameAudio.Se("hit", 0.45f); IceBreak(end, 0.7f * u, u, 4); Run(Squash(astTarget, 0.07f, 0.06f)); if (last) { Frosted(u); AstHit(0.1f); } })));
                yield return BattleTime.FxWait(0.07f);
            }
            yield return BattleTime.FxWait(0.45f);
        }

        /// <summary>아이시클 라인: 발밑에서 세 갈래로 얼음 기둥이 줄지어 솟구치며 뻗어 나가 상대를 꿰뚫는다</summary>
        IEnumerator IcicleLine(PalView user, Vector3 aim)
        {
            float u = U;
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            var g = GroundAt(aim, u);
            GameAudio.Se("stat_down", 0.5f);
            for (int s = 1; s <= 6; s++)
            {
                for (int lane = -1; lane <= 1; lane++)
                {
                    var end = g + Vector3.up * lane * 0.5f * u + Vector3.right * lane * 0.3f * u;
                    var p = Vector3.Lerp(ug, end, s / 6f);
                    IceSpike(p, 0.45f * u, (0.8f + s * 0.18f) * u * (lane == 0 ? 1.2f : 1f), 0.6f, Rnd(-10f, 10f), lane == 1 ? 3 : 6);
                }
                GameAudio.Se("hit", 0.35f);
                yield return BattleTime.FxWait(0.05f);
            }
            GameAudio.Se("hit_strong", 0.8f);
            IceBreak(aim, 1.4f * u, u, 8);
            Frosted(u);
            Run(Squash(astTarget, 0.22f));
            Run(CameraShake(0.12f, 0.2f));
            AstHit(0.14f);
            yield return BattleTime.FxWait(0.6f);
        }

        /// <summary>눈보라 스파이크: 눈보라를 휘감아 거대한 얼음덩어리를 빚고, 내던져 부딪친 자리에 서리 충격파를 퍼뜨린다</summary>
        IEnumerator BlizzardSpike(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.45f, 0.25f));
            var top = user.Center + Vector3.up * user.Height * 0.55f;
            var cam = CamRect(); top.y = Mathf.Min(top.y, cam.yMax - 1f * u);
            var ch = Ast(Pw + "i_chunk", top, 0f, Al(Color.white, Am), 6);
            GameAudio.Se("shine", 0.7f);
            for (float t = 0f; t < 0.55f; t += BattleTime.FxDt)
            {
                Size(ch, 1.8f * u * Out(t / 0.55f)); ch.transform.Rotate(0, 0, 90f * BattleTime.FxDt);
                if (Random.value < 0.8f) Mote(top + Dir(Rnd(0, 360)) * 2.2f * u, top, 0.22f * u, Color.white, 0.3f, 0f, Pw + "i_snow", 6);
                yield return null;
            }
            GameAudio.Se("select", 0.8f);
            yield return Throw(ch, top, aim, 0.32f, 0.8f * u, 400f, false, (s, k) => { if (Random.value < 0.6f) Mote(s.transform.position, s.transform.position + (Vector3)(Random.insideUnitCircle * 0.5f * u), 0.2f * u, Color.white, 0.35f, 0f, Pw + "i_snow", 5); });
            GameAudio.Se("hit_strong");
            ScreenFlash(IWhite, 0.35f, 0.02f, 0.04f, 0.3f);
            IceBreak(aim, 2.4f * u, u, 14);
            Ring(aim, 0.4f * u, 4f * u, Al(Color.white, Am), 0.5f, Pw + "i_ring", 0.45f);
            Frosted(u);
            Run(Squash(astTarget, 0.26f));
            Run(CameraShake(0.2f, 0.35f));
            AstHit(0.2f);
            yield return BattleTime.FxWait(0.55f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>다이아몬드 폴: 상대 머리 위 하늘에서 반짝이는 얼음덩어리가 잇달아 떨어지고, 마지막엔 가장 큰 덩어리가 내리찍는다</summary>
        IEnumerator DiamondFall(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.55f, 0.3f));
            var cam = CamRect();
            for (int i = 0; i < 6; i++)
            {
                bool last = i == 5;
                var end = aim + (last ? Vector3.zero : new Vector3(Rnd(-0.8f, 0.8f), Rnd(-0.4f, 0.4f), 0f) * u);
                var start = new Vector3(end.x + Rnd(-0.4f, 0.4f) * u, cam.yMax + 1.5f, 0f);
                var ch = Ast(Pw + "i_chunk", start, (last ? 2.2f : 0.9f) * u, Al(Color.white, Am), 7, Rnd(0, 360));
                Mote(start, end, 0.3f * u, Color.white, 0.25f, 0f, Pw + "i_snow", 6);
                Run(Throw(ch, start, end, last ? 0.32f : 0.22f, 0f, 300f));
                Run(DelayThen(last ? 0.32f : 0.22f, Do(() =>
                {
                    GameAudio.Se(last ? "hit_strong" : "hit", 0.7f);
                    IceBreak(end, (last ? 2.8f : 1.1f) * u, u, last ? 14 : 5);
                    Run(Squash(astTarget, last ? 0.3f : 0.1f));
                    Run(CameraShake(last ? 0.28f : 0.07f, 0.15f));
                    if (last) { ScreenFlash(IWhite, 0.5f, 0.02f, 0.05f, 0.35f); Ring(end, 0.5f * u, 4.2f * u, Al(Color.white, Am), 0.5f, Pw + "i_ring", 0.45f); Frosted(u); AstHit(0.25f); }
                })));
                yield return BattleTime.FxWait(last ? 0.5f : 0.16f);
            }
            yield return BattleTime.FxWait(0.4f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>더블 눈보라 스파이크: 양손에 거대한 얼음덩어리 둘을 빚어, 하나를 던지고 곧이어 더 큰 하나를 내리꽂는다</summary>
        IEnumerator DoubleBlizzardSpike(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.6f, 0.3f));
            var c = user.Center;
            var cam = CamRect();
            var p1 = c + new Vector3(-0.9f, user.Height * 0.5f, 0f); var p2 = c + new Vector3(0.9f, user.Height * 0.55f, 0f);
            p1.y = Mathf.Min(p1.y, cam.yMax - u); p2.y = Mathf.Min(p2.y, cam.yMax - u);
            var a = Ast(Pw + "i_chunk", p1, 0f, Al(Color.white, Am), 6);
            var b = Ast(Pw + "i_chunk", p2, 0f, Al(Color.white, Am), 6); b.flipX = true;
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.6f; t += BattleTime.FxDt)
            {
                Size(a, 1.5f * u * Out(t / 0.6f)); Size(b, 2f * u * Out(t / 0.6f));
                a.transform.Rotate(0, 0, 80f * BattleTime.FxDt); b.transform.Rotate(0, 0, -80f * BattleTime.FxDt);
                if (Random.value < 0.8f) Mote(c + Dir(Rnd(0, 360)) * 2.5f * u, Random.value < 0.5f ? p1 : p2, 0.22f * u, Color.white, 0.3f, 0f, Pw + "i_snow", 6);
                yield return null;
            }
            for (int i = 0; i < 2; i++)
            {
                var s = i == 0 ? a : b; var from = i == 0 ? p1 : p2;
                GameAudio.Se("select", 0.8f);
                yield return Throw(s, from, aim, 0.28f, 0.8f * u, 500f);
                GameAudio.Se("hit_strong");
                ScreenFlash(IWhite, i == 0 ? 0.3f : 0.55f, 0.02f, 0.04f, 0.3f);
                IceBreak(aim, (i == 0 ? 2f : 3f) * u, u, 12);
                Ring(aim, 0.4f * u, (i == 0 ? 3f : 4.6f) * u, Al(Color.white, Am), 0.5f, Pw + "i_ring", 0.45f);
                Run(Squash(astTarget, i == 0 ? 0.18f : 0.3f));
                Run(CameraShake(i == 0 ? 0.12f : 0.3f, 0.3f));
                if (i == 0) { Frosted(u); AstHit(0.12f); }
                yield return BattleTime.FxWait(0.1f);
            }
            for (int i = 0; i < 4; i++) IceSpike(GroundAt(aim, u) + Vector3.right * (i - 1.5f) * 0.6f * u, 0.5f * u, Rnd(1.2f, 2f) * u, 0.6f, (i - 1.5f) * -12f);
            yield return BattleTime.FxWait(0.6f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>프로스트 아웃: 상대 발밑을 중심으로 서리가 번지고, 얼음 기둥이 겹겹의 동심원으로 차례차례 터져 나온다</summary>
        IEnumerator FrostOut(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.6f, 0.3f));
            yield return user.WindUp(aim, 0.16f);
            var g = GroundAt(aim, u);
            GameAudio.Se("stat_down", 0.6f);
            var fr = Ast(Pw + "i_mist", g, 0f, Al(Color.white, 0.8f * Am), 1);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { Size(fr, 4.6f * u * Out(t / 0.35f), 0.3f); yield return null; }
            GameAudio.Se("hit_strong", 0.8f);
            IceSpike(g, 1.3f * u, 3.4f * u, 1f, 0f, 7);
            Frosted(u);
            Run(Squash(astTarget, 0.2f));
            AstHit(0.16f);
            for (int ring = 1; ring <= 3; ring++)
            {
                int n = 4 + ring * 3;
                for (int i = 0; i < n; i++)
                {
                    float an = (i + ring * 0.5f) * Mathf.PI * 2f / n;
                    var p = g + new Vector3(Mathf.Cos(an) * ring * 0.9f * u, Mathf.Sin(an) * ring * 0.28f * u, 0f);
                    IceSpike(p, 0.55f * u, (2.4f - ring * 0.4f) * u, 0.8f, -Mathf.Cos(an) * 20f, Mathf.Sin(an) > 0 ? 3 : 6);
                }
                GameAudio.Se("hit", 0.5f);
                Ring(g, ring * 0.6f * u, ring * 1.4f * u, Al(Color.white, Am), 0.3f, Pw + "i_ring", 0.3f);
                Run(CameraShake(0.1f, 0.12f));
                Run(Squash(astTarget, 0.1f));
                yield return BattleTime.FxWait(0.12f);
            }
            ScreenFlash(IWhite, 0.4f, 0.02f, 0.04f, 0.3f);
            yield return BattleTime.FxWait(0.6f);
            IceBreak(g + Vector3.up * u, 3f * u, u, 16);
            Anim(fr, 0.3f, (s, k) => s.color = Al(Color.white, 0.8f * Am * (1f - k)));
            yield return BattleTime.FxWait(0.3f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>크리스탈 윙 (프로스탈리온): 등에 얼음 결정 날개를 활짝 펼치고, 결정 가루를 흩날리며 질주해 날개로 베어 가른다</summary>
        IEnumerator CrystalWing(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.55f, 0.25f));
            bool flip = (aim - user.Center).x < 0;
            var w1 = Ast(Pw + "i_wing", user.Center, 0f, Al(Color.white, Am), Behind(user), 0f, new Vector2(0.15f, 0.2f));
            var w2 = Ast(Pw + "i_wing", user.Center, 0f, Al(IBlue, 0.7f * Am), Behind(user, 2), 20f, new Vector2(0.15f, 0.2f));
            w1.flipX = w2.flipX = flip;
            GameAudio.Se("shine", 0.8f);
            for (float t = 0f; t < 0.4f; t += BattleTime.FxDt)
            {
                float k = BackOut(t / 0.4f);
                Size(w1, user.Height * 1.4f * k); Size(w2, user.Height * 1.3f * k);
                w1.transform.position = w2.transform.position = user.Center;
                if (Random.value < 0.5f) Mote(user.Center + (Vector3)(Random.insideUnitCircle * user.Height * 0.6f), user.Center + Vector3.up * 0.4f * u, 0.18f * u, Color.white, 0.4f, 0f, Pw + "i_snow", 8);
                yield return null;
            }
            bool going = true;
            Run(FollowShield(w1, user, Vector3.zero, 0f, () => going));
            Run(FollowShield(w2, user, Vector3.zero, 0f, () => going));
            Run(AfterimageTrail(user, () => going));
            Run(SnowTrail(user, () => going));
            SpeedLines(aim, 6f * u, 0.5f, IBlue);
            yield return user.Lunge(aim, 99f, 0.4f, () =>
            {
                GameAudio.Se("hit_strong");
                ScreenFlash(IWhite, 0.45f, 0.02f, 0.04f, 0.3f);
                for (int i = 0; i < 2; i++)
                {
                    var sl = Ast(Pw + "i_crescent", aim, 0f, Al(Color.white, Am), 9, (flip ? 180f : 0f) + (i == 0 ? 35f : -35f));
                    Anim(sl, 0.4f, (s, k) => { Size(s, 3.6f * u * Out(k * 3f)); s.color = Al(Color.white, Am * (1f - k)); }, i * 0.08f);
                }
                IceBreak(aim, 2.6f * u, u, 14);
                Frosted(u);
                Run(Squash(astTarget, 0.28f));
                AstHit(0.22f);
            });
            going = false;
            Anim(w1, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            Anim(w2, 0.3f, (s, k) => s.color = Al(IBlue, 0.7f * Am * (1f - k)));
            yield return BattleTime.FxWait(0.6f);
            yield return DimTo(0f, 0.25f);
        }

        IEnumerator SnowTrail(PalView user, System.Func<bool> keep)
        {
            while (keep())
            {
                var p = user.Center + (Vector3)(Random.insideUnitCircle * 0.5f * U);
                Mote(p, p + Vector3.down * 0.4f * U, 0.22f * U, Color.white, 0.5f, 0f, Pw + "i_snow", 3);
                var sh = Ast(Pw + "i_shard", p, 0.3f * U, Al(Color.white, 0.9f), 3, Rnd(0, 360));
                Anim(sh, 0.35f, (s, k) => s.color = Al(Color.white, 0.9f * (1f - k)));
                yield return BattleTime.FxWait(0.025f);
            }
        }

        /// <summary>눈덩이 굴리기 (덤보): 앞에 눈덩이를 만들어 데굴데굴 굴리며 키우고, 가장 커졌을 때 번쩍 들어 던져 터뜨린다</summary>
        IEnumerator SnowballRoll(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 dir = (aim - user.Center).normalized;
            var g0 = new Vector3(user.Center.x, user.Feet.y, 0f) + new Vector3(dir.x, 0f, 0f) * 0.8f * u;
            var ball = Ast(Pw + "i_snowball", g0, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0f));
            GameAudio.Se("select", 0.6f);
            float roll = 0f;
            for (float t = 0f; t < 0.8f; t += BattleTime.FxDt)
            {
                float k = t / 0.8f, size = Mathf.Lerp(0.3f, 1.6f, k) * u;
                var p = g0 + new Vector3(dir.x, 0f, 0f) * Mathf.Sin(k * Mathf.PI) * 0.8f * u;
                ball.transform.position = p;
                roll -= Mathf.Sign(dir.x) * 600f * BattleTime.FxDt;
                ball.transform.rotation = Quaternion.Euler(0, 0, roll);
                Size(ball, size);
                if (Random.value < 0.5f) Mote(p, p + new Vector3(-dir.x * Rnd(0.3f, 0.7f), Rnd(0.1f, 0.4f), 0f) * u, 0.18f * u, Color.white, 0.35f, 0f, Pw + "i_snow", 5);
                yield return null;
            }
            ball.transform.rotation = Quaternion.identity;
            var top = user.Center + Vector3.up * user.Height * 0.5f;
            yield return Throw(ball, ball.transform.position, top, 0.18f);
            ball = Ast(Pw + "i_snowball", top, 1.6f * u, Al(Color.white, Am), 7);
            GameAudio.Se("select", 0.8f);
            yield return Throw(ball, top, aim, 0.35f, 1.2f * u, 300f);
            GameAudio.Se("hit_strong");
            for (int i = 0; i < 14; i++)
            {
                var b = Ast(Pw + "i_snowball", aim, Rnd(0.2f, 0.45f) * u, Al(Color.white, Am), 7);
                Run(Throw(b, aim, aim + new Vector3(Rnd(-2f, 2f), Rnd(-1.2f, 0.3f), 0f) * u, Rnd(0.35f, 0.5f), Rnd(0.5f, 1.2f) * u, Rnd(-400f, 400f)));
            }
            var puff = Ast(Pw + "i_mist", aim, 0f, Al(Color.white, Am), 8);
            Anim(puff, 0.5f, (s, k) => { Size(s, 3.6f * u * Out(k)); s.color = Al(Color.white, Am * (1f - k)); });
            Frosted(u);
            Run(Squash(astTarget, 0.26f));
            Run(CameraShake(0.15f, 0.25f));
            AstHit(0.18f);
            yield return BattleTime.FxWait(0.55f);
        }
    }
}
