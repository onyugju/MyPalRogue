using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>물 속성 팰월드 기술 연출 (그림: PwFx/w_* — 물빛이 그림에 들어 있다)</summary>
    public partial class SkillFx
    {
        static readonly Color WBlue = C(0x3d9bff), WCyan = C(0x8fe3ff), WFoam = C(0xeaffff), WAcid = C(0xb8f05a);

        static partial void RegWater()
        {
            PwFx["Water_Jet"] = (fx, u, t, a) => fx.WaterJet(u, a);
            PwFx["Aqua_Shot"] = (fx, u, t, a) => fx.AquaShot(u, a);
            PwFx["Bubble_Shot"] = (fx, u, t, a) => fx.BubbleShot(u, a);
            PwFx["Acid_Rain"] = (fx, u, t, a) => fx.AcidRain(u, a);
            PwFx["Line_Splash"] = (fx, u, t, a) => fx.LineSplash(u, a);
            PwFx["Water_Bomb"] = (fx, u, t, a) => fx.WaterBomb(u, a);
            PwFx["High_Pressure_Jet"] = (fx, u, t, a) => fx.HighPressureJet(u, a);
            PwFx["Wall_Splash"] = (fx, u, t, a) => fx.WallSplash(u, a);
            PwFx["Hydro_Slicer"] = (fx, u, t, a) => fx.HydroSlicer(u, a);
            PwFx["Hydro_Stream"] = (fx, u, t, a) => fx.HydroStream(u, a);
            PwFx["Aqua_Surge"] = (fx, u, t, a) => fx.AquaSurge(u, a);
            PwFx["Water_Geyser"] = (fx, u, t, a) => fx.WaterGeyser(u, a);
            PwFx["Tarasonic_Laser"] = (fx, u, t, a) => fx.TarasonicLaser(u, a);
            PwFx["Sea_Serpent"] = (fx, u, t, a) => fx.SeaSerpent(u, a);
        }

        // ───── 공용 조각 ─────

        /// <summary>상대 발밑 (빗나가면 겨눈 곳 아래)</summary>
        Vector3 GroundAt(Vector3 aim, float u) => new Vector3(aim.x, astMissed || astTarget == null ? aim.y - 0.8f * u : astTarget.Feet.y, 0f);

        /// <summary>물이 철퍽 튄다: 물보라 왕관 + 물방울 + 물결 고리</summary>
        void WSplash(Vector3 at, float size, float u, int drops = 8)
        {
            var cr = Ast(Pw + "w_splash", at + Vector3.down * size * 0.25f, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0.05f));
            Anim(cr, 0.4f, (s, k) => { SizeWH(s, size * (0.6f + 0.6f * Out(k * 2f)), size * 0.8f * Bell(Mathf.Min(1f, k * 1.3f))); s.color = Al(Color.white, Am * (1f - k * k)); });
            for (int i = 0; i < drops; i++)
            {
                var end = at + new Vector3(Rnd(-0.9f, 0.9f) * size, Rnd(0.1f, 0.7f) * size, 0f);
                var d = Ast(Pw + "w_droplet", at, Rnd(0.1f, 0.18f) * u, Al(Color.white, Am), 6);
                Run(Throw(d, at, end + Vector3.down * 0.6f * size, Rnd(0.35f, 0.5f), Rnd(0.4f, 0.8f) * size));
            }
            Ring(at, 0.2f * size, 1.1f * size, Al(WCyan, 0.8f * Am), 0.45f, Pw + "w_ripple", 0.35f);
        }

        /// <summary>땅에서 물기둥이 솟았다 무너진다</summary>
        void WColumn(Vector3 feet, float w, float h, float life, int sort = 5)
        {
            var c = Ast(Pw + "w_column", feet, 0f, Al(Color.white, Am), sort, 0f, new Vector2(0.5f, 0.02f));
            Anim(c, life, (s, k) => { float up = k < 0.25f ? Out(k / 0.25f) : 1f - In((k - 0.25f) / 0.75f) * 0.9f; SizeWH(s, w * (1f + 0.08f * Mathf.Sin(k * 40f)), h * up); s.color = Al(Color.white, Am * (k < 0.7f ? 1f : (1f - k) / 0.3f)); s.flipX = Mathf.Repeat(k, 0.12f) < 0.06f; });
            WSplash(feet, w * 1.3f, w, 5);
        }

        // ───── 기술 ─────

        /// <summary>워터 제트: 입에서 작은 물탄 셋이 연달아 튀어나가 탁탁 부딪친다</summary>
        IEnumerator WaterJet(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            for (int i = 0; i < 3; i++)
            {
                GameAudio.Se("select", 0.5f);
                var d = Ast(Pw + "w_drop", from + dir * 0.4f * u, 0.7f * u, Al(Color.white, Am), 5, Ang(dir));
                var end = aim + new Vector3(Rnd(-0.2f, 0.2f), Rnd(-0.25f, 0.25f), 0f) * u;
                Run(Throw(d, d.transform.position, end, 0.16f, 0f, 0f, true));
                bool last = i == 2;
                Run(DelayThen(0.16f, Do(() => { GameAudio.Se("hit", 0.5f); WSplash(end, 0.8f * u, u, 4); Run(Squash(astTarget, 0.08f, 0.08f)); if (last) AstHit(0.06f); })));
                Run(Recoil(user, dir, 0.08f, 0.06f));
                yield return BattleTime.FxWait(0.09f);
            }
            yield return BattleTime.FxWait(0.4f);
        }

        IEnumerator Do(System.Action a) { a(); yield break; }

        /// <summary>아쿠아 샷: 물방울을 빚어 쏘면, 물보라 꼬리를 남기고 날아가 철퍽 터진다</summary>
        IEnumerator AquaShot(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.5f * u;
            var orb = Ast(Pw + "w_orb", mouth, 0f, Al(Color.white, Am), 5);
            for (float t = 0f; t < 0.2f; t += BattleTime.FxDt) { Size(orb, 0.5f * u * BackOut(t / 0.2f)); yield return null; }
            Destroy(orb.gameObject);
            GameAudio.Se("select", 0.7f);
            Run(Recoil(user, dir, 0.18f, 0.14f));
            var d = Ast(Pw + "w_drop", mouth, 1.2f * u, Al(Color.white, Am), 5, Ang(dir));
            yield return Throw(d, mouth, aim, 0.24f, 0.2f * u, 0f, true, (s, k) => { if (Random.value < 0.5f) Mote(s.transform.position, s.transform.position + Vector3.down * 0.4f * u, 0.12f * u, Color.white, 0.3f, 0f, Pw + "w_droplet", 4); });
            GameAudio.Se("hit", 0.8f);
            WSplash(aim, 1.8f * u, u, 10);
            Run(Squash(astTarget, 0.16f));
            AstHit(0.08f);
            yield return BattleTime.FxWait(0.45f);
        }

        /// <summary>버블 샷: 거품 여섯 개가 흔들흔들 떠가 상대를 감싸고, 하나씩 퐁퐁 터진다</summary>
        IEnumerator BubbleShot(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center;
            GameAudio.Se("shine", 0.5f);
            const int n = 6;
            var bs = new SpriteRenderer[n]; var ends = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                bs[i] = Ast(Pw + "w_bubble", from, Rnd(0.45f, 0.7f) * u, Al(Color.white, Am), 6);
                ends[i] = aim + (Vector3)(Random.insideUnitCircle * 0.7f * u);
                Run(BubbleFloat(bs[i], from, ends[i], 0.6f + i * 0.05f, Rnd(0f, 6f), u));
                yield return BattleTime.FxWait(0.06f);
            }
            yield return BattleTime.FxWait(0.65f);
            for (int i = 0; i < n; i++)
            {
                if (bs[i] == null) continue;
                GameAudio.Se("select", 0.4f);
                var p = bs[i].transform.position;
                Destroy(bs[i].gameObject);
                Ring(p, 0.3f * u, 0.9f * u, Al(WFoam, Am), 0.25f, Pw + "w_ripple");
                Spray(Pw + "w_droplet", p, 6, 0.6f * u, 0.12f * u, Color.white, Color.white, 0.3f);
                Run(Squash(astTarget, 0.07f, 0.08f));
                if (i == n - 1) AstHit(0.06f);
                yield return BattleTime.FxWait(0.07f);
            }
            yield return BattleTime.FxWait(0.35f);
        }

        IEnumerator BubbleFloat(SpriteRenderer b, Vector3 a, Vector3 c, float time, float ph, float u)
        {
            var bs = b.transform.localScale;
            for (float t = 0f; t < time + 0.6f && b != null; t += BattleTime.FxDt)
            {
                float k = Mathf.Clamp01(t / time);
                b.transform.position = Vector3.Lerp(a, c, Out(k)) + new Vector3(Mathf.Sin(t * 7f + ph) * 0.12f, Mathf.Sin(t * 5f + ph) * 0.18f, 0f) * u;
                b.transform.localScale = new Vector3(bs.x * (1f + 0.06f * Mathf.Sin(t * 11f + ph)), bs.y * (1f - 0.06f * Mathf.Sin(t * 11f + ph)), bs.z);
                yield return null;
            }
        }

        /// <summary>산성비: 상대 머리 위에 탁한 먹구름이 몰려와, 연둣빛 산성 빗줄기를 퍼붓고 쉬익 녹아내린다</summary>
        static readonly Color AcidCloud = new Color(0.5f, 0.56f, 0.5f);

        IEnumerator AcidRain(PalView user, Vector3 aim)
        {
            float u = U;
            var cam = CamRect();
            var top = aim + Vector3.up * 2.2f * u; top.y = Mathf.Min(top.y, cam.yMax - 0.8f * u);
            Run(DimTo(0.35f, 0.3f));
            GameAudio.Se("stat_down", 0.6f);
            var cloud = Ast(Pw + "w_cloud", top, 0f, Al(Color.white, 0f), 7);
            var cloud2 = Ast(Pw + "w_cloud", top + new Vector3(0.6f, 0.15f, 0f) * u, 0f, Al(Color.white, 0f), 6);
            cloud2.flipX = true;
            for (float t = 0f; t < 0.4f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.4f);
                SizeWH(cloud, 2.5f * u * k, 1.15f * u * k); SizeWH(cloud2, 2f * u * k, 0.95f * u * k);
                cloud.color = Al(AcidCloud, 0.9f * Am * k); cloud2.color = Al(AcidCloud * 0.85f, 0.8f * Am * k);
                yield return null;
            }
            var ground = GroundAt(aim, u);
            float drip = 0f;
            for (float t = 0f; t < 0.9f; t += BattleTime.FxDt)
            {
                cloud.transform.position = top + Vector3.right * Mathf.Sin(t * 6f) * 0.08f * u;
                drip += BattleTime.FxDt;
                while (drip > 0.018f)
                {
                    drip -= 0.018f;
                    var s0 = top + new Vector3(Rnd(-1.4f, 1.4f) * u, Rnd(-0.3f, 0f) * u, 0f);
                    s0.x = top.x + Mathf.Clamp(s0.x - top.x, -1.1f * u, 1.1f * u);
                    var r = Ast(Pw + "w_rain", s0, 0.12f * u, Al(Color.white, 0.9f * Am), 6, 100f);
                    SizeWH(r, 0.12f * u, 0.7f * u);
                    var land = new Vector3(s0.x - 0.25f * u, ground.y + Rnd(-0.2f, 0.4f) * u, 0f);
                    Run(Throw(r, s0, land, 0.22f));
                    if (Random.value < 0.35f) Mote(land, land + Vector3.up * 0.25f * u, 0.14f * u, WAcid, 0.25f, 0.22f, Pw + "w_droplet", 5);
                }
                if (t > 0.25f && !astHit) { GameAudio.Se("hit", 0.6f); AstHit(0.05f); }
                if (t > 0.25f && Random.value < 0.25f && !astMissed)
                {
                    var p = astTarget.Center + new Vector3(Rnd(-0.5f, 0.5f), Rnd(-0.5f, 0.4f), 0f) * u;
                    var sm = Ast(Pw + "f_smoke", p, 0.4f * u, Al(WAcid, 0.6f), 7);
                    Anim(sm, 0.5f, (s, k) => { s.transform.position = p + Vector3.up * k * 0.6f * u; Size(s, 0.4f * u * (1f + k)); s.color = Al(WAcid, 0.6f * (1f - k)); });
                    Run(Squash(astTarget, 0.05f, 0.08f));
                }
                yield return null;
            }
            Anim(cloud, 0.35f, (s, k) => s.color = Al(Color.white, 0.95f * Am * (1f - k)));
            Anim(cloud2, 0.35f, (s, k) => s.color = Al(Color.white, 0.8f * Am * (1f - k)));
            yield return BattleTime.FxWait(0.4f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>라인 스플래시: 발밑에서 상대까지 한 줄로 물기둥이 차례차례 솟구친다</summary>
        IEnumerator LineSplash(PalView user, Vector3 aim)
        {
            float u = U;
            yield return user.WindUp(aim, 0.14f);
            var a = new Vector3(user.Center.x, user.Feet.y, 0f);
            var b = GroundAt(aim, u);
            const int n = 6;
            for (int i = 1; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                bool last = i == n;
                GameAudio.Se(last ? "hit_strong" : "hit", 0.5f);
                WColumn(p, (last ? 1.2f : 0.7f) * u, (last ? 3f : 1.6f + i * 0.15f) * u, last ? 0.7f : 0.5f);
                Run(CameraShake(0.03f, 0.06f));
                if (last) { Run(Squash(astTarget, 0.2f)); AstHit(0.1f); }
                yield return BattleTime.FxWait(0.07f);
            }
            yield return BattleTime.FxWait(0.6f);
        }

        /// <summary>물폭탄: 머리 위에 커다란 물구슬을 빚어 출렁출렁 키운 뒤, 내던져 큰 물보라로 터뜨린다</summary>
        IEnumerator WaterBomb(PalView user, Vector3 aim)
        {
            float u = U;
            var cam = CamRect();
            var top = user.Center + Vector3.up * user.Height * 0.6f; top.y = Mathf.Min(top.y, cam.yMax - 1.1f * u);
            var orb = Ast(Pw + "w_orb", top, 0f, Al(Color.white, Am), 5);
            GameAudio.Se("shine", 0.6f);
            for (float t = 0f; t < 0.55f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.55f), wob = 0.08f * Mathf.Sin(t * 25f);
                SizeWH(orb, 1.8f * u * k * (1f + wob), 1.8f * u * k * (1f - wob));
                orb.transform.Rotate(0, 0, 120f * BattleTime.FxDt);
                if (Random.value < 0.5f) Mote(top + Dir(Rnd(0, 360)) * 1.8f * u, top, 0.14f * u, Color.white, 0.25f, 0f, Pw + "w_droplet", 4);
                yield return null;
            }
            GameAudio.Se("select", 0.8f);
            Run(Recoil(user, aim - user.Center, 0.25f, 0.2f));
            yield return Throw(orb, top, aim, 0.42f, 1.2f * u, 200f, false, (s, k) => { if (Random.value < 0.6f) Mote(s.transform.position, s.transform.position + Vector3.down * 0.5f * u, 0.15f * u, Color.white, 0.35f, 0f, Pw + "w_droplet", 4); });
            GameAudio.Se("hit_strong");
            ScreenFlash(WCyan, 0.3f, 0.02f, 0.03f, 0.3f);
            WSplash(aim, 3.2f * u, u, 16);
            WColumn(GroundAt(aim, u), 1.6f * u, 2.6f * u, 0.6f);
            Ring(aim, 0.4f * u, 3.6f * u, Al(WFoam, Am), 0.5f, Pw + "w_ripple", 0.5f, 0.06f);
            Run(Squash(astTarget, 0.26f));
            AstHit(0.2f);
            yield return BattleTime.FxWait(0.6f);
        }

        /// <summary>고압수 발사: 가는 고압 물줄기가 꽂힌 뒤, 맞은 자리에서 좌우 두 갈래로 갈라져 뻗어 나간다</summary>
        IEnumerator HighPressureJet(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.5f * u;
            float len = Vector3.Distance(mouth, aim);
            GameAudio.Se("hit", 0.7f);
            var beam = Ast(Pw + "w_stream", mouth, 0f, Al(Color.white, Am), 5, Ang(dir), new Vector2(0f, 0.5f));
            for (float t = 0f; t < 0.12f; t += BattleTime.FxDt) { SizeWH(beam, len * Out(t / 0.12f), 0.35f * u); yield return null; }
            GameAudio.Se("hit_strong", 0.8f);
            WSplash(aim, 1.4f * u, u, 8);
            Run(Squash(astTarget, 0.15f, 0.3f));
            AstHit(0.1f);
            var f1 = Ast(Pw + "w_stream", aim, 0f, Al(Color.white, Am), 6, Ang(dir) + 35f, new Vector2(0f, 0.5f));
            var f2 = Ast(Pw + "w_stream", aim, 0f, Al(Color.white, Am), 6, Ang(dir) - 35f, new Vector2(0f, 0.5f));
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.18f), f = t > 0.35f ? 1f - (t - 0.35f) / 0.15f : 1f;
                SizeWH(beam, len, 0.35f * u * (1f + 0.15f * Mathf.Sin(t * 50f)) * f);
                SizeWH(f1, 2.4f * u * k, 0.28f * u * f); SizeWH(f2, 2.4f * u * k, 0.28f * u * f);
                beam.flipY = f1.flipY = f2.flipY = Mathf.Repeat(t, 0.06f) < 0.03f;
                if (Random.value < 0.5f) Spray(Pw + "w_droplet", aim, 2, 0.8f * u, 0.12f * u, Color.white, Color.white, 0.3f);
                yield return null;
            }
            Destroy(beam.gameObject); Destroy(f1.gameObject); Destroy(f2.gameObject);
            yield return BattleTime.FxWait(0.3f);
        }

        /// <summary>월 스플래시: 상대 앞뒤로 물의 벽이 세 겹 잇달아 솟아올라 와르르 덮친다</summary>
        IEnumerator WallSplash(PalView user, Vector3 aim)
        {
            float u = U;
            yield return user.WindUp(aim, 0.16f);
            var g = GroundAt(aim, u);
            Vector3 side = (aim - user.Center).x > 0 ? Vector3.right : Vector3.left;
            for (int i = 0; i < 3; i++)
            {
                var p = g + side * (i - 1) * 0.9f * u;
                GameAudio.Se("hit", 0.7f);
                for (int j = -1; j <= 1; j++) WColumn(p + Vector3.right * j * 0.45f * u, 0.8f * u, (2.4f + i * 0.4f) * u * (j == 0 ? 1f : 0.8f), 0.65f, 5 + i);
                Run(CameraShake(0.06f, 0.1f));
                Run(Squash(astTarget, 0.12f));
                if (i == 2) AstHit(0.14f);
                yield return BattleTime.FxWait(0.16f);
            }
            var wave = Ast(Pw + "w_wave", g + Vector3.up * 1.4f * u - side * 1.5f * u, 0f, Al(Color.white, Am), 7);
            wave.flipX = side.x < 0;
            Anim(wave, 0.45f, (s, k) => { s.transform.position = g + Vector3.up * 1.2f * u + side * Mathf.Lerp(-1.5f, 0.8f, Out(k)) * u; SizeWH(s, 3f * u, 2.4f * u * Bell(k)); });
            yield return BattleTime.FxWait(0.25f);
            WSplash(aim, 2.6f * u, u, 12);
            yield return BattleTime.FxWait(0.5f);
        }

        /// <summary>하이드로 슬라이서: 칼날 같은 물줄기로 위에서 아래로 베어 내리고, 벤 자리에서 물이 뿜어져 오른다</summary>
        IEnumerator HydroSlicer(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.5f * u;
            float len = Vector3.Distance(mouth, aim) + 1.2f * u, baseAng = Ang(dir);
            yield return user.WindUp(aim, 0.12f);
            GameAudio.Se("select", 0.8f);
            var blade = Ast(Pw + "w_stream", mouth, 0f, Al(Color.white, Am), 6, baseAng + 30f, new Vector2(0f, 0.5f));
            float sgn = dir.x >= 0 ? 1f : -1f;
            for (float t = 0f; t < 0.32f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.32f);
                blade.transform.rotation = Quaternion.Euler(0, 0, baseAng + sgn * Mathf.Lerp(30f, -30f, k));
                SizeWH(blade, len, 0.22f * u * (1f + 0.3f * Bell(k)));
                var tip = mouth + (Vector3)(Vector2)(Quaternion.Euler(0, 0, blade.transform.eulerAngles.z) * Vector3.right) * len;
                if (Random.value < 0.6f) Mote(tip, tip + Vector3.down * 0.4f * u, 0.13f * u, Color.white, 0.3f, 0f, Pw + "w_droplet", 6);
                if (k > 0.45f && !astHit) { GameAudio.Se("hit", 0.8f); AstHit(0.08f); Run(Squash(astTarget, 0.16f)); }
                yield return null;
            }
            Anim(blade, 0.15f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            yield return BattleTime.FxWait(0.15f);
            GameAudio.Se("hit_strong", 0.8f);
            WColumn(GroundAt(aim, u), 1.3f * u, 3.4f * u, 0.8f);
            Run(Squash(astTarget, 0.2f));
            Run(CameraShake(0.1f, 0.2f));
            yield return BattleTime.FxWait(0.7f);
        }

        /// <summary>하이드로 스트림: 굵은 물줄기를 내뿜어 파도째로 상대를 쓸어 버린다</summary>
        IEnumerator HydroStream(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.5f * u;
            float len = Vector3.Distance(mouth, aim) + 2f * u;
            Run(DimTo(0.4f, 0.25f));
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt) { if (Random.value < 0.6f) Mote(mouth + Dir(Rnd(0, 360)) * 1.2f * u, mouth, 0.14f * u, Color.white, 0.2f, 0f, Pw + "w_droplet", 5); yield return null; }
            GameAudio.Se("hit_strong", 0.8f);
            var s1 = Ast(Pw + "w_stream", mouth, 0f, Al(Color.white, Am), 6, Ang(dir), new Vector2(0f, 0.5f));
            var s2 = Ast(Pw + "w_stream", mouth, 0f, Al(WBlue, 0.7f * Am), 5, Ang(dir), new Vector2(0f, 0.5f));
            for (float t = 0f; t < 0.85f; t += BattleTime.FxDt)
            {
                float g = Out(t / 0.12f), f = t > 0.7f ? 1f - (t - 0.7f) / 0.15f : 1f;
                SizeWH(s1, len * g, 1.1f * u * (1f + 0.1f * Mathf.Sin(t * 40f)) * f);
                SizeWH(s2, len * g, 1.7f * u * (1f + 0.12f * Mathf.Sin(t * 33f + 1f)) * f);
                s1.flipY = Mathf.Repeat(t, 0.07f) < 0.035f;
                if (Random.value < 0.7f) Spray(Pw + "w_droplet", aim, 2, 1.1f * u, 0.15f * u, Color.white, WCyan, 0.35f);
                if (t > 0.1f && !astHit) { AstHit(0.12f); Run(Squash(astTarget, 0.14f, 0.7f)); }
                yield return null;
            }
            Destroy(s1.gameObject); Destroy(s2.gameObject);
            WSplash(aim, 2.4f * u, u, 12);
            yield return BattleTime.FxWait(0.35f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>아쿠아 서지: 양옆에서 큰 파도가 밀려와 상대 자리에서 부딪치고, 한가운데서 거대한 물기둥으로 터진다</summary>
        IEnumerator AquaSurge(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.45f, 0.3f));
            var g = GroundAt(aim, u);
            GameAudio.Se("stat_down", 0.6f);
            var L = Ast(Pw + "w_wave", g, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0f));
            var R = Ast(Pw + "w_wave", g, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0f));
            R.flipX = true;
            for (float t = 0f; t < 0.55f; t += BattleTime.FxDt)
            {
                float k = In(t / 0.55f);
                L.transform.position = g + Vector3.left * Mathf.Lerp(4f, 0.9f, k) * u;
                R.transform.position = g + Vector3.right * Mathf.Lerp(4f, 0.9f, k) * u;
                SizeWH(L, 2.6f * u, 2.4f * u * (0.6f + 0.4f * k)); SizeWH(R, 2.6f * u, 2.4f * u * (0.6f + 0.4f * k));
                if (Random.value < 0.4f) { Dust(L.transform.position, u * 0.4f, 1, 0.5f); }
                yield return null;
            }
            GameAudio.Se("hit_strong");
            Anim(L, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            Anim(R, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            ScreenFlash(WFoam, 0.4f, 0.02f, 0.04f, 0.35f);
            WSplash(aim, 3f * u, u, 16);
            WColumn(g, 2f * u, 4.5f * u, 0.9f, 7);
            Run(Squash(astTarget, 0.28f));
            Run(CameraShake(0.22f, 0.4f));
            AstHit(0.22f);
            yield return BattleTime.FxWait(0.8f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>물기둥 분출: 상대 둘레 땅에 물이 보글보글 고이더니, 물기둥이 사방에서 잇달아 치솟고 가운데가 가장 높이 솟는다</summary>
        IEnumerator WaterGeyser(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.5f, 0.3f));
            yield return user.WindUp(aim, 0.18f);
            var g = GroundAt(aim, u);
            var spots = new Vector3[8];
            for (int i = 0; i < spots.Length; i++)
            {
                spots[i] = g + new Vector3(Rnd(-2.2f, 2.2f) * u, Rnd(-0.3f, 0.3f) * u, 0f);
                Ring(spots[i], 0.1f * u, 0.8f * u, Al(WCyan, Am), 0.4f, Pw + "w_ripple", 0.3f, i * 0.03f);
            }
            GameAudio.Se("stat_down", 0.5f);
            yield return BattleTime.FxWait(0.35f);
            for (int i = 0; i < spots.Length; i++)
            {
                GameAudio.Se("hit", 0.5f);
                WColumn(spots[i], 0.75f * u, Rnd(2f, 3f) * u, 0.6f, 4 + (i % 3));
                Run(CameraShake(0.04f, 0.06f));
                Run(Squash(astTarget, 0.07f, 0.08f));
                yield return BattleTime.FxWait(0.06f);
            }
            GameAudio.Se("hit_strong");
            ScreenFlash(WCyan, 0.35f, 0.02f, 0.04f, 0.3f);
            WColumn(g, 2.2f * u, 5f * u, 1f, 7);
            WSplash(aim, 3.4f * u, u, 18);
            Run(Squash(astTarget, 0.3f));
            Run(CameraShake(0.25f, 0.45f));
            AstHit(0.25f);
            yield return BattleTime.FxWait(0.9f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>타라소닉 레이저 (넵튤리우스): 바다의 힘을 모아 고압 광선으로 세 번 휩쓸고, 휩쓴 자리마다 물기둥이 솟는다</summary>
        IEnumerator TarasonicLaser(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.6f * u;
            float len = Vector3.Distance(mouth, aim) + 1.5f * u, baseAng = Ang(dir), sgn = dir.x >= 0 ? 1f : -1f;
            Run(DimTo(0.6f, 0.3f));
            var orb = Ast(Pw + "w_orb", mouth, 0f, Al(Color.white, Am), 6);
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                Size(orb, 1.2f * u * Out(t / 0.5f) * (1f + 0.08f * Mathf.Sin(t * 40f))); orb.transform.Rotate(0, 0, 300f * BattleTime.FxDt);
                if (Random.value < 0.7f) Mote(mouth + Dir(Rnd(0, 360)) * 2f * u, mouth, 0.15f * u, Color.white, 0.25f, 0f, Pw + "w_droplet", 6);
                yield return null;
            }
            var g = GroundAt(aim, u);
            float[] from3 = { 25f, -25f, 15f }, to3 = { -25f, 25f, -15f };
            for (int i = 0; i < 3; i++)
            {
                GameAudio.Se("hit_strong", 0.7f);
                var beam = Ast(Pw + "w_stream", mouth, 0f, Al(Color.white, Am), 7, baseAng, new Vector2(0f, 0.5f));
                var glow = Ast(Pw + "w_stream", mouth, 0f, Al(WBlue, 0.6f * Am), 6, baseAng, new Vector2(0f, 0.5f));
                for (float t = 0f; t < 0.28f; t += BattleTime.FxDt)
                {
                    float k = t / 0.28f, ang = baseAng + sgn * Mathf.Lerp(from3[i], to3[i], Out(k));
                    beam.transform.rotation = glow.transform.rotation = Quaternion.Euler(0, 0, ang);
                    SizeWH(beam, len, 0.55f * u * Bell(Mathf.Min(1f, k * 1.3f) * 0.5f + 0.25f)); SizeWH(glow, len, 1.1f * u);
                    beam.flipY = Mathf.Repeat(t, 0.06f) < 0.03f;
                    yield return null;
                }
                Destroy(beam.gameObject); Destroy(glow.gameObject);
                WColumn(g + Vector3.right * (i - 1) * 1f * u, 1.2f * u, 3.6f * u, 0.7f, 5 + i);
                Run(Squash(astTarget, 0.18f));
                Run(CameraShake(0.12f, 0.15f));
                if (i == 0) AstHit(0.12f);
            }
            Destroy(orb.gameObject);
            ScreenFlash(WFoam, 0.45f, 0.02f, 0.05f, 0.35f);
            WColumn(g, 2.4f * u, 5.2f * u, 1f, 8);
            WSplash(aim, 3.4f * u, u, 18);
            Run(Squash(astTarget, 0.3f));
            yield return BattleTime.FxWait(0.9f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>바다뱀 (요르문타이드): 포효하며 몸에 물줄기를 휘감고, 소용돌이 물결을 끌며 돌진해 거대한 파도로 덮친다</summary>
        IEnumerator SeaSerpent(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.5f, 0.25f));
            GameAudio.Se("stat_up", 0.8f);
            for (int i = 0; i < 3; i++) Ring(user.Center, 0.4f * u, 3f * u, Al(WCyan, Am), 0.5f, Pw + "w_ripple", 1f, i * 0.12f);
            Run(CameraShake(0.08f, 0.35f));
            var coil = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++) coil[i] = Ast(Pw + "w_stream", user.Center, 2.2f * u, Al(Color.white, 0.85f * Am), 6 - i, i * 120f, new Vector2(0f, 0.5f));
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                for (int i = 0; i < 3; i++) { coil[i].transform.position = user.Center; coil[i].transform.rotation = Quaternion.Euler(0, 0, i * 120f + t * 720f); SizeWH(coil[i], 1.5f * u * Out(t / 0.2f), 0.35f * u); }
                yield return null;
            }
            bool going = true;
            Run(AfterimageTrail(user, () => going));
            Run(WaterTrail(user, () => going));
            SpeedLines(aim, 6f * u, 0.5f, WCyan);
            yield return user.Lunge(aim, 99f, 0.42f, () =>
            {
                GameAudio.Se("hit_strong");
                ScreenFlash(WFoam, 0.4f, 0.02f, 0.04f, 0.3f);
                var wave = Ast(Pw + "w_wave", aim + Vector3.up * 0.4f * u, 0f, Al(Color.white, Am), 8);
                wave.flipX = (aim - user.Center).x < 0;
                Anim(wave, 0.5f, (s, k) => { SizeWH(s, 3.6f * u * (0.7f + 0.3f * k), 3f * u * Bell(k)); });
                WSplash(aim, 3f * u, u, 16);
                Run(Squash(astTarget, 0.28f));
                AstHit(0.22f);
            });
            going = false;
            foreach (var c in coil) Anim(c, 0.2f, (s, k) => s.color = Al(Color.white, 0.85f * Am * (1f - k)));
            yield return BattleTime.FxWait(0.7f);
            yield return DimTo(0f, 0.25f);
        }

        IEnumerator WaterTrail(PalView user, System.Func<bool> keep)
        {
            while (keep())
            {
                var p = user.Center;
                var s = Ast(Pw + "w_bubble", p + (Vector3)(Random.insideUnitCircle * 0.5f * U), Rnd(0.25f, 0.45f) * U, Al(Color.white, 0.9f), 3);
                Anim(s, 0.45f, (sr, k) => { sr.transform.position += Vector3.up * 0.01f; sr.color = Al(Color.white, 0.9f * (1f - k)); });
                Mote(p, p + Vector3.down * 0.5f * U + (Vector3)(Random.insideUnitCircle * 0.4f * U), 0.15f * U, Color.white, 0.35f, 0f, Pw + "w_droplet", 3);
                yield return BattleTime.FxWait(0.03f);
            }
        }
    }
}
