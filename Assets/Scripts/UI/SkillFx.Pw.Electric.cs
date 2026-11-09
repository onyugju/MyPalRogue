using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>번개 속성 팰월드 기술 연출 (그림: PwFx/e_*)</summary>
    public partial class SkillFx
    {
        static readonly Color EYellow = C(0xffe94a), EWhite = C(0xfffbe0), ECyan = C(0x9be8ff);

        static partial void RegElectric()
        {
            PwFx["Lightning_Spear"] = (fx, u, t, a) => fx.LightningSpear(u, a);
            PwFx["Spark_Shot"] = (fx, u, t, a) => fx.SparkShot(u, a);
            PwFx["Electric_Wave"] = (fx, u, t, a) => fx.ElectricWave(u, a);
            PwFx["Lightning_Ball"] = (fx, u, t, a) => fx.LightningBall(u, a);
            PwFx["Plasma_Panel"] = (fx, u, t, a) => fx.PlasmaPanel(u, a);
            PwFx["Lock_On_Laser"] = (fx, u, t, a) => fx.LockOnLaser(u, a);
            PwFx["Line_Thunder"] = (fx, u, t, a) => fx.LineThunder(u, a);
            PwFx["Tri_Thunder"] = (fx, u, t, a) => fx.TriThunder(u, a);
            PwFx["Tri_Spark"] = (fx, u, t, a) => fx.TriSpark(u, a);
            PwFx["Thunder_Strike"] = (fx, u, t, a) => fx.ThunderStrike(u, a);
            PwFx["Thunder_Rain"] = (fx, u, t, a) => fx.ThunderRain(u, a);
            PwFx["Electric_Bolt"] = (fx, u, t, a) => fx.ElectricBolt(u, a);
            PwFx["Omni_Thunder"] = (fx, u, t, a) => fx.OmniThunder(u, a);
            PwFx["Thunder_Rail"] = (fx, u, t, a) => fx.ThunderRail(u, a);
            PwFx["Thunderstorm"] = (fx, u, t, a) => fx.Thunderstorm(u, a);
            PwFx["Execution_Laser"] = (fx, u, t, a) => fx.ExecutionLaser(u, a);
            PwFx["Electric_Claw"] = (fx, u, t, a) => fx.ElectricClaw(u, a);
            PwFx["Thunder_Tank"] = (fx, u, t, a) => fx.ThunderTank(u, a);
            PwFx["Summon_Thunder"] = (fx, u, t, a) => fx.SummonThunder(u, a);
        }

        // ───── 공용 조각 ─────

        /// <summary>두 점 사이에 번개 줄기가 지지직 깜빡인다</summary>
        void BoltLine(Vector3 a, Vector3 b, float thick, float life, int sort = 6)
        {
            var d = b - a;
            var sr = Ast(Pw + "e_bolt1", a, 0f, Al(Color.white, Am), sort, Ang(d), new Vector2(0f, 0.5f));
            var s1 = AstSprite(Pw + "e_bolt1", new Vector2(0f, 0.5f)); var s2 = AstSprite(Pw + "e_bolt2", new Vector2(0f, 0.5f));
            float len = d.magnitude;
            Anim(sr, life, (s, k) =>
            {
                bool alt = Mathf.Repeat(k * life, 0.06f) < 0.03f;
                if (s1 != null && s2 != null) s.sprite = alt ? s1 : s2;
                s.flipY = Mathf.Repeat(k * life, 0.09f) < 0.045f;
                SizeWH(s, len, thick * (0.8f + 0.4f * Random.value));
                s.color = Al(Color.white, Am * (k < 0.7f ? 1f : (1f - k) / 0.3f));
            });
        }

        /// <summary>맞은 몸에 전기가 튀고 노랗게 번쩍인다</summary>
        void Shock(Vector3 at, float u, float size = 1f)
        {
            var sp = Ast(Pw + "e_spark", at, 0f, Al(Color.white, Am), 7, Rnd(0, 360));
            Anim(sp, 0.25f, (s, k) => { Size(s, 1.4f * size * u * Out(k * 2f)); s.transform.Rotate(0, 0, 400f * BattleTime.FxDt); s.color = Al(Color.white, Am * (1f - k)); });
            for (int i = 0; i < 4; i++)
            {
                var p = at + (Vector3)(Random.insideUnitCircle * 0.7f * size * u);
                var s2 = Ast(Pw + "e_spark", p, 0.5f * size * u, Al(Color.white, Am), 7, Rnd(0, 360));
                Anim(s2, 0.18f, (s, k) => s.color = Al(Color.white, Am * (Mathf.Repeat(k, 0.3f) < 0.15f ? 1f : 0.2f) * (1f - k)), i * 0.05f);
            }
            if (!astMissed && astTarget != null) Run(astTarget.Flash(EYellow, 2, 0.05f));
        }

        /// <summary>하늘에서 상대(또는 지점) 위로 벼락이 내리꽂힌다</summary>
        void ThunderAt(Vector3 ground, float u, float w = 1f)
        {
            var cam = CamRect();
            float h = cam.yMax + 0.5f - ground.y;
            var s = Ast(Pw + "e_strike", new Vector3(ground.x, cam.yMax + 0.5f, 0f), 0f, Al(Color.white, Am), 8, 0f, new Vector2(0.5f, 1f));
            Anim(s, 0.3f, (sr, k) => { SizeWH(sr, 1.2f * w * u * (1f + 0.3f * Random.value), h); sr.flipX = Random.value < 0.5f; sr.color = Al(Color.white, Am * (Mathf.Repeat(k, 0.2f) < 0.12f ? 1f : 0.35f) * (1f - k * k)); });
            var ring = Ast(Pw + "e_ring", ground, 0f, Al(Color.white, Am), 4);
            Anim(ring, 0.3f, (sr, k) => { Size(sr, 2.2f * w * u * Out(k * 2f), 0.35f); sr.color = Al(Color.white, Am * (1f - k)); });
            Impact(ground + Vector3.up * 0.2f * u, 1.3f * w * u, EYellow, 0.18f);
            Dust(ground, u * 0.6f, 3, 1f);
        }

        // ───── 기술 ─────

        /// <summary>번개 창: 손끝에 번개 창이 지직 생겨나고, 눈 깜짝할 새 꽂혀 몸을 감전시킨다</summary>
        IEnumerator LightningSpear(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var sp = Ast(Pw + "e_spear", from + Vector3.up * 0.4f * u, 0f, Al(Color.white, Am), 6, Ang(dir));
            GameAudio.Se("select", 0.6f);
            for (float t = 0f; t < 0.2f; t += BattleTime.FxDt) { Size(sp, 1.8f * u * Out(t / 0.2f)); sp.flipY = Random.value < 0.5f; yield return null; }
            GameAudio.Se("hit", 0.8f);
            yield return Throw(sp, sp.transform.position, aim, 0.1f, 0f, 0f, true);
            Shock(aim, u);
            Run(Squash(astTarget, 0.16f));
            AstHit(0.08f);
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>스파크 샷: 지직거리는 불꽃 다섯 개를 부채꼴로 흩뿌려, 튀며 날아가 상대 몸에서 터진다</summary>
        IEnumerator SparkShot(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = aim - from;
            GameAudio.Se("select", 0.7f);
            for (int i = -2; i <= 2; i++)
            {
                var to = i == 0 ? aim : aim + (Vector3)(Vector2)(Quaternion.Euler(0, 0, 90f) * dir.normalized) * i * 0.45f * u;
                var s = Ast(Pw + "e_spark", from, 0.6f * u, Al(Color.white, Am), 6);
                Run(Throw(s, from, to, 0.2f + Mathf.Abs(i) * 0.03f, 0.3f * u * i, 900f));
                bool last = i == 2;
                Run(DelayThen(0.2f + Mathf.Abs(i) * 0.03f, Do(() => { GameAudio.Se("hit", 0.45f); Shock(to, u, 0.6f); Run(Squash(astTarget, 0.07f, 0.07f)); if (last) AstHit(0.06f); })));
            }
            yield return BattleTime.FxWait(0.55f);
        }

        /// <summary>전기 파장: 몸에 전기를 잔뜩 모았다가 지지직 고리 모양 파동으로 사방에 퍼뜨린다</summary>
        IEnumerator ElectricWave(PalView user, Vector3 aim)
        {
            float u = U;
            var c = user.Center;
            GameAudio.Se("stat_up", 0.6f);
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt) { if (Random.value < 0.5f) BoltLine(c, c + Dir(Rnd(0, 360)) * 0.9f * u, 0.3f * u, 0.08f); yield return null; }
            GameAudio.Se("hit", 0.8f);
            float dist = Vector3.Distance(c, aim);
            for (int i = 0; i < 2; i++)
            {
                var r = Ast(Pw + "e_ring", c, 0f, Al(Color.white, Am), 6, Rnd(0, 360));
                float lag = i * 0.08f;
                Anim(r, 0.4f, (s, k) => { Size(s, (dist * 2.3f + 1f * u) * Out(k)); s.flipX = Random.value < 0.5f; s.color = Al(Color.white, Am * (1f - k * k)); }, lag);
            }
            yield return BattleTime.FxWait(0.22f);
            Shock(aim, u);
            Run(Squash(astTarget, 0.14f));
            AstHit(0.08f);
            yield return BattleTime.FxWait(0.4f);
        }

        /// <summary>번개 구체: 지직대는 번개 덩어리가 천천히 다가가 상대에 들러붙어, 여러 갈래로 방전하며 터진다</summary>
        IEnumerator LightningBall(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center;
            var ball = Ast(Pw + "e_ball", from, 0f, Al(Color.white, Am), 6);
            GameAudio.Se("shine", 0.6f);
            for (float t = 0f; t < 0.25f; t += BattleTime.FxDt) { Size(ball, 1.1f * u * BackOut(t / 0.25f)); yield return null; }
            for (float t = 0f; t < 0.6f; t += BattleTime.FxDt)
            {
                float k = t / 0.6f;
                ball.transform.position = Vector3.Lerp(from, aim, k * k * (3f - 2f * k)) + Vector3.up * Mathf.Sin(k * 9f) * 0.2f * u;
                ball.transform.Rotate(0, 0, 300f * BattleTime.FxDt); ball.flipX = Random.value < 0.5f;
                if (Random.value < 0.3f) BoltLine(ball.transform.position, ball.transform.position + Dir(Rnd(0, 360)) * 0.8f * u, 0.25f * u, 0.07f);
                yield return null;
            }
            GameAudio.Se("hit_strong", 0.8f);
            for (int i = 0; i < 6; i++) BoltLine(aim, aim + Dir(i * 60f + Rnd(-15f, 15f)) * 1.6f * u, 0.4f * u, 0.2f);
            Anim(ball, 0.25f, (s, k) => { Size(s, 1.1f * u * (1f + k)); s.color = Al(Color.white, Am * (1f - k)); });
            Shock(aim, u, 1.3f);
            Run(Squash(astTarget, 0.18f));
            AstHit(0.12f);
            yield return BattleTime.FxWait(0.4f);
        }

        /// <summary>플라즈마 판넬: 몸 둘레에 띄운 플라즈마 구슬들이 서로 전기로 이어지더니, 연발로 쏟아진다</summary>
        IEnumerator PlasmaPanel(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            const int n = 10;
            var balls = new SpriteRenderer[n]; var spots = new Vector3[n];
            GameAudio.Se("stat_up", 0.6f);
            for (int i = 0; i < n; i++)
            {
                spots[i] = from + Dir(90f + (i - (n - 1) / 2f) * 22f) * 1.1f * u - dir * 0.3f * u;
                balls[i] = Ast(Pw + "e_ball", spots[i], 0f, Al(Color.white, Am), 6);
                Run(PopIn(balls[i], 0.45f * u, i * 0.03f));
            }
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { int j = Random.Range(0, n - 1); BoltLine(spots[j], spots[j + 1], 0.2f * u, 0.06f); yield return null; }
            for (int i = 0; i < n; i++)
            {
                var end = aim + (Vector3)(Random.insideUnitCircle * 0.5f * u);
                Run(Throw(balls[i], spots[i], end, 0.14f, 0f, 600f));
                bool last = i == n - 1;
                Run(DelayThen(0.14f, Do(() => { GameAudio.Se("hit", 0.4f); Shock(end, u, 0.6f); Run(Squash(astTarget, 0.06f, 0.06f)); if (last) AstHit(0.1f); })));
                yield return BattleTime.FxWait(0.05f);
            }
            yield return BattleTime.FxWait(0.4f);
        }

        /// <summary>고정식 레이저: 붉은 조준 꺾쇠가 상대에게 달라붙어 삑삑 고정되고, 한 줄기 레이저가 번쩍 꿰뚫는다</summary>
        IEnumerator LockOnLaser(PalView user, Vector3 aim)
        {
            float u = U;
            var lk = Ast(Pw + "e_lock", aim, 0f, Al(Color.white, Am), 8);
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                Size(lk, Mathf.Lerp(3.4f, 1.6f, Out(t / 0.35f)) * u);
                lk.color = Al(Color.white, Am * (Mathf.Repeat(t, 0.12f) < 0.08f ? 1f : 0.3f));
                if (Mathf.Repeat(t, 0.12f) < BattleTime.FxDt) GameAudio.Se("select", 0.4f);
                yield return null;
            }
            GameAudio.Se("hit_strong", 0.8f);
            Vector3 from = user.Center, d = aim - from;
            var las = Ast(Pw + "e_laser", from, 0f, Al(Color.white, Am), 7, Ang(d), new Vector2(0f, 0.5f));
            float len = d.magnitude + 1.5f * u;
            ScreenFlash(EWhite, 0.25f, 0.01f, 0.03f, 0.2f);
            Anim(las, 0.3f, (s, k) => SizeWH(s, len, 0.7f * u * (1f - k) * (1f + 0.3f * Random.value)));
            Destroy(lk.gameObject);
            Shock(aim, u);
            Run(Squash(astTarget, 0.18f));
            AstHit(0.1f);
            yield return BattleTime.FxWait(0.4f);
        }

        /// <summary>라인 썬더: 발밑에서 땅을 타고 뇌격이 지그재그로 내달려, 상대 발밑에서 위로 튀어 오른다</summary>
        IEnumerator LineThunder(PalView user, Vector3 aim)
        {
            float u = U;
            var a = new Vector3(user.Center.x, user.Feet.y + 0.1f * u, 0f);
            var g = GroundAt(aim, u) + Vector3.up * 0.1f * u;
            GameAudio.Se("hit", 0.6f);
            Vector3 prev = a;
            for (int i = 1; i <= 8; i++)
            {
                var p = Vector3.Lerp(a, g, i / 8f) + Vector3.up * (i % 2 == 0 ? 0.15f : -0.1f) * u;
                BoltLine(prev, p, 0.45f * u, 0.35f);
                Spray(Pw + "e_spark", p, 2, 0.5f * u, 0.25f * u, Color.white, Color.white, 0.2f);
                prev = p;
                yield return BattleTime.FxWait(0.03f);
            }
            GameAudio.Se("hit_strong", 0.8f);
            BoltLine(g, g + Vector3.up * 2.4f * u, 0.8f * u, 0.3f);
            Shock(aim, u, 1.2f);
            Run(Squash(astTarget, 0.2f));
            AstHit(0.12f);
            yield return BattleTime.FxWait(0.45f);
        }

        /// <summary>트라이 썬더: 상대 머리 위에 먹구름이 끼고, 벼락이 왼쪽·오른쪽·한가운데로 세 번 내리꽂힌다</summary>
        IEnumerator TriThunder(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.45f, 0.25f));
            var cam = CamRect();
            var top = new Vector3(aim.x, Mathf.Min(aim.y + 2.6f * u, cam.yMax - 0.7f * u), 0f);
            var cloud = Ast(Pw + "e_cloud", top, 0f, Al(Color.white, 0f), 9);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { float k = Out(t / 0.35f); SizeWH(cloud, 3.4f * u * k, 1.6f * u * k); cloud.color = Al(Color.white, Am * k); yield return null; }
            var g = GroundAt(aim, u);
            float[] xs = { -0.9f, 0.9f, 0f };
            for (int i = 0; i < 3; i++)
            {
                GameAudio.Se(i == 2 ? "hit_strong" : "hit", 0.8f);
                ScreenFlash(EWhite, i == 2 ? 0.45f : 0.2f, 0.01f, 0.03f, 0.15f);
                ThunderAt(g + Vector3.right * xs[i] * u, u, i == 2 ? 1.5f : 0.9f);
                if (i == 2) { Shock(aim, u, 1.3f); Run(Squash(astTarget, 0.24f)); AstHit(0.16f); }
                else Run(Squash(astTarget, 0.08f));
                yield return BattleTime.FxWait(0.2f);
            }
            Anim(cloud, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            yield return BattleTime.FxWait(0.35f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>트라이 스파크: 곧게 내리꽂히는 번개 한 줄기와, 휘어 돌아 쫓아가는 번개 구슬 둘을 함께 쏜다</summary>
        IEnumerator TriSpark(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center;
            GameAudio.Se("hit", 0.7f);
            BoltLine(from, aim, 0.6f * u, 0.25f);
            Shock(aim, u, 0.8f);
            Run(Squash(astTarget, 0.1f));
            for (int i = 0; i < 2; i++)
            {
                var b = Ast(Pw + "e_ball", from, 0.6f * u, Al(Color.white, Am), 6);
                var ctrl = Vector3.Lerp(from, aim, 0.5f) + Vector3.up * (i == 0 ? 2f : -1.6f) * u;
                Run(CurveTo(b, from, ctrl, aim, 0.45f, u));
            }
            yield return BattleTime.FxWait(0.45f);
            GameAudio.Se("hit_strong", 0.8f);
            Shock(aim, u, 1.3f);
            Run(Squash(astTarget, 0.2f));
            AstHit(0.12f);
            yield return BattleTime.FxWait(0.4f);
        }

        IEnumerator CurveTo(SpriteRenderer s, Vector3 a, Vector3 c, Vector3 b, float time, float u)
        {
            for (float t = 0f; t < time && s != null; t += BattleTime.FxDt)
            {
                float k = t / time;
                s.transform.position = Vector3.Lerp(Vector3.Lerp(a, c, k), Vector3.Lerp(c, b, k), k);
                s.transform.Rotate(0, 0, 500f * BattleTime.FxDt); s.flipX = Random.value < 0.5f;
                if (Random.value < 0.4f) Mote(s.transform.position, s.transform.position + (Vector3)(Random.insideUnitCircle * 0.4f * u), 0.25f * u, Color.white, 0.15f, 0f, Pw + "e_spark", 5);
                yield return null;
            }
            if (s != null) Destroy(s.gameObject);
        }

        /// <summary>번개 일격: 스스로에게 벼락을 내리쳐 몸에 전기를 가득 담고, 그대로 사방으로 방전 폭발을 일으킨다</summary>
        IEnumerator ThunderStrike(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.5f, 0.2f));
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            GameAudio.Se("hit_strong", 0.8f);
            ScreenFlash(EWhite, 0.4f, 0.01f, 0.03f, 0.15f);
            ThunderAt(ug, u, 1.4f);
            Run(user.Flash(EYellow, 3, 0.05f));
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { if (Random.value < 0.6f) BoltLine(user.Center, user.Center + Dir(Rnd(0, 360)) * 1f * u, 0.3f * u, 0.06f); yield return null; }
            GameAudio.Se("hit_strong");
            float dist = Vector3.Distance(user.Center, aim);
            var r = Ast(Pw + "e_ring", user.Center, 0f, Al(Color.white, Am), 7);
            Anim(r, 0.4f, (s, k) => { Size(s, (dist * 2.2f + 2f * u) * Out(k)); s.color = Al(Color.white, Am * (1f - k)); s.flipY = Random.value < 0.5f; });
            for (int i = 0; i < 10; i++) BoltLine(user.Center, user.Center + Dir(i * 36f) * (dist + 0.5f * u) * Rnd(0.6f, 1f), 0.5f * u, 0.3f);
            BoltLine(user.Center, aim, 0.7f * u, 0.35f);
            yield return BattleTime.FxWait(0.15f);
            Shock(aim, u, 1.4f);
            Run(Squash(astTarget, 0.24f));
            Run(CameraShake(0.18f, 0.3f));
            AstHit(0.18f);
            yield return BattleTime.FxWait(0.45f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>천둥비: 하늘의 먹구름으로 번개를 끌어올려 모았다가, 때가 차면 벼락을 비처럼 쏟아붓는다</summary>
        IEnumerator ThunderRain(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.6f, 0.3f));
            var cam = CamRect();
            var top = new Vector3(aim.x, Mathf.Min(aim.y + 2.8f * u, cam.yMax - 0.7f * u), 0f);
            var cloud = Ast(Pw + "e_cloud", top, 0f, Al(Color.white, 0f), 9);
            var cloud2 = Ast(Pw + "e_cloud", top + Vector3.right * 1.5f * u, 0f, Al(Color.white, 0f), 9); cloud2.flipX = true;
            GameAudio.Se("stat_up", 0.6f);
            for (float t = 0f; t < 0.7f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.4f);
                SizeWH(cloud, 4f * u * k, 1.8f * u * k); SizeWH(cloud2, 3.2f * u * k, 1.5f * u * k);
                cloud.color = cloud2.color = Al(Color.white, Am * k);
                if (Random.value < 0.6f) { var p = user.Center + (Vector3)(Random.insideUnitCircle * 0.5f * u); Mote(p, top + Vector3.right * Rnd(-1f, 1f) * u, 0.3f * u, Color.white, 0.3f, 0f, Pw + "e_spark", 7); }
                if (Random.value < 0.1f) BoltLine(top + Vector3.right * Rnd(-1.5f, 1f) * u, top + new Vector3(Rnd(-1f, 1.5f), Rnd(-0.3f, 0.3f), 0f) * u, 0.3f * u, 0.08f, 10);
                yield return null;
            }
            var g = GroundAt(aim, u);
            for (int i = 0; i < 9; i++)
            {
                bool last = i == 8;
                GameAudio.Se(last ? "hit_strong" : "hit", 0.6f);
                ScreenFlash(EWhite, last ? 0.5f : 0.15f, 0.01f, 0.02f, 0.12f);
                ThunderAt(last ? g : g + Vector3.right * Rnd(-2f, 2f) * u, u, last ? 1.7f : 0.8f);
                Run(Squash(astTarget, last ? 0.26f : 0.06f));
                if (last) { Shock(aim, u, 1.5f); AstHit(0.22f); }
                yield return BattleTime.FxWait(0.07f);
            }
            Anim(cloud, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            Anim(cloud2, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            yield return BattleTime.FxWait(0.45f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>전기 볼트: 온몸의 털이 곤두서도록 전기를 끌어모은 뒤, 굵은 번개 기둥 하나를 상대에게 내다꽂는다</summary>
        IEnumerator ElectricBolt(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.6f, 0.3f));
            var c = user.Center;
            var ball = Ast(Pw + "e_ball", c, 0f, Al(Color.white, Am), 7);
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.6f; t += BattleTime.FxDt)
            {
                Size(ball, 1.6f * u * Out(t / 0.6f)); ball.transform.Rotate(0, 0, 600f * BattleTime.FxDt); ball.flipX = Random.value < 0.5f;
                if (Random.value < 0.6f) BoltLine(c + Dir(Rnd(0, 360)) * 2f * u, c, 0.3f * u, 0.07f);
                if (Random.value < 0.2f) Run(CameraShake(0.02f, 0.04f));
                yield return null;
            }
            GameAudio.Se("hit_strong");
            ScreenFlash(EWhite, 0.6f, 0.01f, 0.05f, 0.3f);
            BoltLine(c, aim, 1.6f * u, 0.5f, 8);
            BoltLine(c, aim, 0.9f * u, 0.5f, 9);
            Anim(ball, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            Shock(aim, u, 1.8f);
            Ring(aim, 0.4f * u, 3.6f * u, Al(Color.white, Am), 0.4f, Pw + "e_ring");
            Run(Squash(astTarget, 0.3f));
            Run(CameraShake(0.25f, 0.4f));
            AstHit(0.25f);
            yield return BattleTime.FxWait(0.55f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>전방위 번개: 몸에서 여덟 갈래 뇌격이 사방의 땅을 가르며 내달리고, 그중 하나가 상대를 덮친다</summary>
        IEnumerator OmniThunder(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.5f, 0.25f));
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            Run(Squash(user, 0.15f, 0.2f));
            GameAudio.Se("hit_strong", 0.8f);
            Ring(ug, 0.3f * u, 2.4f * u, Al(Color.white, Am), 0.35f, Pw + "e_ring", 0.35f);
            float dist = Vector3.Distance(ug, GroundAt(aim, u)) + 1f * u;
            var tips = new Vector3[8];
            for (int i = 0; i < 8; i++) tips[i] = ug;
            for (int s = 1; s <= 6; s++)
            {
                for (int i = 0; i < 8; i++)
                {
                    var dir = i == 0 ? (GroundAt(aim, u) - ug).normalized : (Vector3)new Vector2(Mathf.Cos(i * 45f * Mathf.Deg2Rad), Mathf.Sin(i * 45f * Mathf.Deg2Rad) * 0.35f).normalized;
                    var next = ug + dir * dist * s / 6f + (Vector3)(Random.insideUnitCircle * 0.15f * u);
                    BoltLine(tips[i], next, 0.4f * u, 0.3f);
                    tips[i] = next;
                }
                GameAudio.Se("hit", 0.35f);
                yield return BattleTime.FxWait(0.035f);
            }
            ScreenFlash(EWhite, 0.4f, 0.01f, 0.03f, 0.2f);
            for (int i = 1; i < 8; i++) Spray(Pw + "e_spark", tips[i], 3, 0.6f * u, 0.3f * u, Color.white, Color.white, 0.25f);
            BoltLine(GroundAt(aim, u), aim + Vector3.up * u, 0.9f * u, 0.3f);
            Shock(aim, u, 1.4f);
            Run(Squash(astTarget, 0.24f));
            Run(CameraShake(0.2f, 0.3f));
            AstHit(0.2f);
            yield return BattleTime.FxWait(0.45f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>썬더 레일: 몸 앞에 전기 레일을 여러 줄 깔고, 그 위로 전격 다섯 발을 한꺼번에 쏘아 보낸다</summary>
        IEnumerator ThunderRail(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.55f, 0.25f));
            Vector3 from = user.Center, dir = (aim - from).normalized, perp = new Vector3(-dir.y, dir.x, 0f);
            GameAudio.Se("stat_up", 0.6f);
            for (float t = 0f; t < 0.4f; t += BattleTime.FxDt)
            {
                for (int i = -2; i <= 2; i++) if (Random.value < 0.25f) { var o = from + perp * i * 0.35f * u + dir * 0.5f * u; BoltLine(o, o + dir * 0.8f * u, 0.15f * u, 0.06f); }
                yield return null;
            }
            for (int i = -2; i <= 2; i++)
            {
                var o = from + perp * i * 0.35f * u + dir * 0.5f * u;
                var end = aim + perp * i * 0.25f * u;
                var sp = Ast(Pw + "e_spear", o, 1.5f * u, Al(Color.white, Am), 7, Ang(end - o));
                Run(Throw(sp, o, end, 0.13f, 0f, 0f, true));
                BoltLine(o, end, 0.35f * u, 0.25f);
                GameAudio.Se("hit", 0.6f);
                yield return BattleTime.FxWait(0.03f);
            }
            yield return BattleTime.FxWait(0.1f);
            GameAudio.Se("hit_strong");
            ScreenFlash(EWhite, 0.45f, 0.01f, 0.04f, 0.25f);
            Shock(aim, u, 1.8f);
            Ring(aim, 0.4f * u, 3.4f * u, Al(Color.white, Am), 0.4f, Pw + "e_ring");
            Run(Squash(astTarget, 0.28f));
            Run(CameraShake(0.25f, 0.4f));
            AstHit(0.24f);
            yield return BattleTime.FxWait(0.5f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>번개 폭풍: 앞에 거대한 번개 회오리를 일으켜 상대 쪽으로 몰아가고, 휘말린 자리에 벼락이 연달아 떨어진다</summary>
        IEnumerator Thunderstorm(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.6f, 0.3f));
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            var g = GroundAt(aim, u);
            var start = Vector3.Lerp(ug, g, 0.25f);
            var v = Ast(Pw + "e_vortex", start, 0f, Al(Color.white, Am), 7, 0f, new Vector2(0.5f, 0.03f));
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.45f; t += BattleTime.FxDt) { SizeWH(v, 2.2f * u * Out(t / 0.45f), 4.4f * u * Out(t / 0.45f)); v.flipX = Mathf.Repeat(t, 0.06f) < 0.03f; yield return null; }
            for (float t = 0f; t < 0.9f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.6f);
                v.transform.position = Vector3.Lerp(start, g, k) + Vector3.right * Mathf.Sin(t * 14f) * 0.15f * u;
                v.flipX = Mathf.Repeat(t, 0.06f) < 0.03f;
                if (Random.value < 0.6f) BoltLine(v.transform.position + Vector3.up * Rnd(0.5f, 4f) * u, v.transform.position + new Vector3(Rnd(-1.5f, 1.5f), Rnd(0.5f, 4f), 0f) * u, 0.3f * u, 0.07f, 8);
                Dust(v.transform.position, u * 0.4f, 1, 0.8f);
                if (Random.value < 0.07f) { GameAudio.Se("hit", 0.5f); ThunderAt(v.transform.position + Vector3.right * Rnd(-1.4f, 1.4f) * u, u, 0.7f); }
                if (k > 0.9f && !astHit) { Shock(aim, u, 1.4f); Run(Squash(astTarget, 0.2f, 0.3f)); AstHit(0.2f); }
                yield return null;
            }
            GameAudio.Se("hit_strong");
            ScreenFlash(EWhite, 0.45f, 0.01f, 0.04f, 0.25f);
            ThunderAt(g, u, 1.6f);
            Anim(v, 0.35f, (s, k) => { SizeWH(s, 2.2f * u * (1f + k), 4.4f * u); s.color = Al(Color.white, Am * (1f - k)); });
            Run(Squash(astTarget, 0.24f));
            yield return BattleTime.FxWait(0.5f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>익스큐션 레이저: 상대에게 조준을 고정하고, 하늘에서 번개 레이저를 쾅쾅 연달아 내리꽂는다</summary>
        IEnumerator ExecutionLaser(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.6f, 0.3f));
            var lk = Ast(Pw + "e_lock", aim, 0f, Al(Color.white, Am), 9);
            for (float t = 0f; t < 0.45f; t += BattleTime.FxDt) { Size(lk, Mathf.Lerp(4f, 2f, Out(t / 0.3f)) * u); lk.color = Al(Color.white, Am * (Mathf.Repeat(t, 0.1f) < 0.06f ? 1f : 0.3f)); yield return null; }
            var cam = CamRect();
            for (int i = 0; i < 5; i++)
            {
                bool last = i == 4;
                var x = aim.x + (last ? 0f : Rnd(-0.6f, 0.6f) * u);
                var topP = new Vector3(x, cam.yMax + 0.5f, 0f);
                var bot = new Vector3(x, GroundAt(aim, u).y, 0f);
                GameAudio.Se(last ? "hit_strong" : "hit", 0.7f);
                var las = Ast(Pw + "e_laser", topP, 0f, Al(Color.white, Am), 8, -90f, new Vector2(0f, 0.5f));
                float w = (last ? 1.6f : 0.8f) * u;
                Anim(las, 0.28f, (s, k) => SizeWH(s, topP.y - bot.y, w * (1f - k) * (1f + 0.3f * Random.value)));
                ScreenFlash(EWhite, last ? 0.55f : 0.2f, 0.01f, 0.03f, 0.15f);
                Impact(bot + Vector3.up * 0.3f * u, (last ? 2.4f : 1.2f) * u, EYellow, 0.2f);
                Ring(bot, 0.3f * u, (last ? 3.6f : 1.8f) * u, Al(Color.white, Am), 0.35f, Pw + "e_ring", 0.35f);
                Run(Squash(astTarget, last ? 0.3f : 0.1f));
                Run(CameraShake(last ? 0.25f : 0.08f, 0.15f));
                if (last) { Shock(aim, u, 1.6f); AstHit(0.25f); }
                yield return BattleTime.FxWait(last ? 0.1f : 0.13f);
            }
            Destroy(lk.gameObject);
            yield return BattleTime.FxWait(0.5f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>전기 할퀴기 (그리즈볼트): 전기를 두른 발톱으로 두 번 덮쳐 할퀴고, 앞발로 땅을 내리쳐 충격을 퍼뜨린다</summary>
        IEnumerator ElectricClaw(PalView user, Vector3 aim)
        {
            float u = U;
            bool flip = (aim - user.Center).x < 0;
            yield return user.Lunge(aim, 99f, 0.35f, () =>
            {
                Run(ClawPair(aim, u, flip));
            });
            yield return BattleTime.FxWait(0.5f);
        }

        IEnumerator ClawPair(Vector3 aim, float u, bool flip)
        {
            for (int i = 0; i < 2; i++)
            {
                GameAudio.Se("hit", 0.8f);
                var c = Ast(Pw + "e_claw", aim, 0f, Al(Color.white, Am), 8, i == 0 ? 0f : 90f);
                c.flipX = flip ^ (i == 1);
                Anim(c, 0.3f, (s, k) => { Size(s, 2.2f * u * Out(k * 3f)); s.color = Al(Color.white, Am * (1f - k * k)); });
                Shock(aim, u, 0.8f);
                Run(Squash(astTarget, 0.12f, 0.1f));
                yield return BattleTime.FxWait(0.15f);
            }
            GameAudio.Se("hit_strong");
            var g = GroundAt(aim, u);
            Ring(g, 0.3f * u, 3.2f * u, Al(Color.white, Am), 0.4f, Pw + "e_ring", 0.35f);
            for (int i = 0; i < 6; i++) BoltLine(g, g + new Vector3(Mathf.Cos(i * 60f * Mathf.Deg2Rad) * 1.8f, Mathf.Sin(i * 60f * Mathf.Deg2Rad) * 0.5f, 0f) * u, 0.4f * u, 0.25f);
            Dust(g, u, 6);
            Run(CameraShake(0.15f, 0.25f));
            Run(Squash(astTarget, 0.22f));
            AstHit(0.16f);
        }

        /// <summary>뇌격의 중전차 (그리즈볼트): 미니건을 드르륵 난사해 몰아붙인 뒤, 총구에 모은 전기를 한 발의 거대 전격으로 쏜다</summary>
        IEnumerator ThunderTank(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.5f, 0.25f));
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var muzzle = from + dir * 0.7f * u;
            for (int i = 0; i < 18; i++)
            {
                var end = aim + (Vector3)(Random.insideUnitCircle * 0.6f * u);
                var b = Ast(Pw + "e_bullet", muzzle, 0.6f * u, Al(Color.white, Am), 7, Ang(end - muzzle));
                Run(Throw(b, muzzle, end, 0.08f, 0f, 0f, true));
                var fl = Ast(Pw + "e_spark", muzzle, 0.6f * u, Al(Color.white, Am), 8, Rnd(0, 360));
                Anim(fl, 0.05f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
                if (i % 2 == 0) { GameAudio.Se("hit", 0.3f); Run(DelayThen(0.08f, Do(() => { Impact(end, 0.5f * u, EYellow, 0.08f); Run(Squash(astTarget, 0.04f, 0.04f)); }))); }
                Run(Recoil(user, dir, 0.04f, 0.03f));
                if (i == 6) AstHit(0.05f);
                yield return BattleTime.FxWait(0.035f);
            }
            var ball = Ast(Pw + "e_ball", muzzle, 0f, Al(Color.white, Am), 8);
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.45f; t += BattleTime.FxDt)
            {
                Size(ball, 1.4f * u * Out(t / 0.45f)); ball.transform.Rotate(0, 0, 700f * BattleTime.FxDt);
                if (Random.value < 0.6f) BoltLine(muzzle + Dir(Rnd(0, 360)) * 1.6f * u, muzzle, 0.25f * u, 0.06f);
                yield return null;
            }
            GameAudio.Se("hit_strong");
            ScreenFlash(EWhite, 0.55f, 0.01f, 0.05f, 0.3f);
            Run(Recoil(user, dir, 0.4f, 0.3f));
            BoltLine(muzzle, aim, 1.4f * u, 0.45f, 9);
            var las = Ast(Pw + "e_laser", muzzle, 0f, Al(Color.white, Am), 8, Ang(aim - muzzle), new Vector2(0f, 0.5f));
            float len = Vector3.Distance(muzzle, aim);
            Anim(las, 0.4f, (s, k) => SizeWH(s, len, 1.4f * u * (1f - k)));
            Destroy(ball.gameObject);
            Shock(aim, u, 1.8f);
            Ring(aim, 0.4f * u, 3.6f * u, Al(Color.white, Am), 0.45f, Pw + "e_ring");
            Run(Squash(astTarget, 0.3f));
            Run(CameraShake(0.25f, 0.4f));
            yield return BattleTime.FxWait(0.55f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>천둥 소환 (라이붕): 제 앞과 상대 자리에 번개를 불러 떨어뜨리고, 그 번개 길을 따라 섬광처럼 내달려 들이받는다</summary>
        IEnumerator SummonThunder(PalView user, Vector3 aim)
        {
            float u = U;
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            var g = GroundAt(aim, u);
            var mid = Vector3.Lerp(ug, g, 0.3f);
            GameAudio.Se("hit", 0.8f);
            ScreenFlash(EWhite, 0.2f, 0.01f, 0.02f, 0.12f);
            ThunderAt(mid, u, 0.9f);
            yield return BattleTime.FxWait(0.12f);
            GameAudio.Se("hit", 0.8f);
            ThunderAt(g, u, 1f);
            BoltLine(mid + Vector3.up * 0.2f * u, g + Vector3.up * 0.2f * u, 0.4f * u, 0.6f);
            yield return BattleTime.FxWait(0.12f);
            bool going = true;
            Run(AfterimageTrail(user, () => going));
            Run(ElecTrail(user, () => going));
            SpeedLines(aim, 6f * u, 0.4f, EYellow);
            yield return user.Lunge(aim, 99f, 0.25f, () =>
            {
                GameAudio.Se("hit_strong");
                Shock(aim, u, 1.4f);
                Impact(aim, 2f * u, EYellow, 0.2f);
                Run(Squash(astTarget, 0.22f));
                AstHit(0.14f);
            });
            going = false;
            yield return BattleTime.FxWait(0.4f);
        }

        IEnumerator ElecTrail(PalView user, System.Func<bool> keep)
        {
            Vector3 prev = user.Center;
            while (keep())
            {
                var p = user.Center;
                if ((p - prev).sqrMagnitude > 0.01f) BoltLine(prev, p, 0.4f * U, 0.2f, 4);
                Afterimage(user, Al(EYellow, 0.5f), 0.2f);
                prev = p;
                yield return BattleTime.FxWait(0.03f);
            }
        }
    }
}
