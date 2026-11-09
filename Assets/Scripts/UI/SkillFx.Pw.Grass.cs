using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>풀 속성 팰월드 기술 연출 (그림: PwFx/g_*)</summary>
    public partial class SkillFx
    {
        static readonly Color GLeaf = C(0x6fd84a), GLight = C(0xd8ff8a), GSun = C(0xffe27a), GPink = C(0xffa8d8);

        static partial void RegGrass()
        {
            PwFx["Wind_Blade"] = (fx, u, t, a) => fx.WindBlade(u, a);
            PwFx["Seed_Machine_Gun"] = (fx, u, t, a) => fx.SeedMachineGun(u, a);
            PwFx["Seed_Mine"] = (fx, u, t, a) => fx.SeedMine(u, a);
            PwFx["Multi_Cutter"] = (fx, u, t, a) => fx.MultiCutter(u, a);
            PwFx["Grass_Tornado"] = (fx, u, t, a) => fx.GrassTornado(u, a);
            PwFx["Wind_Edge"] = (fx, u, t, a) => fx.WindEdge(u, a);
            PwFx["Thorn_Vine"] = (fx, u, t, a) => fx.ThornVine(u, a);
            PwFx["Circle_Vine"] = (fx, u, t, a) => fx.CircleVine(u, a);
            PwFx["Reflect_Leaf"] = (fx, u, t, a) => fx.ReflectLeaf(u, a);
            PwFx["Ambush_Blades"] = (fx, u, t, a) => fx.AmbushBlades(u, a);
            PwFx["Solar_Blast"] = (fx, u, t, a) => fx.SolarBlast(u, a);
            PwFx["Cross_Wind"] = (fx, u, t, a) => fx.CrossWind(u, a);
            PwFx["Wind_Blast"] = (fx, u, t, a) => fx.WindBlast(u, a);
            PwFx["Muscle_Punch"] = (fx, u, t, a) => fx.MusclePunch(u, a);
            PwFx["Sniping_Shot"] = (fx, u, t, a) => fx.SnipingShot(u, a);
            PwFx["Tail_Whip"] = (fx, u, t, a) => fx.TailWhip(u, a);
            PwFx["Wind_Barrier"] = (fx, u, t, a) => fx.WindBarrier(u, a);
            PwFx["Abundant_Blessing"] = (fx, u, t, a) => fx.AbundantBlessing(u, a);
        }

        // ───── 공용 조각 ─────

        /// <summary>잎사귀가 팔랑팔랑 흩날린다</summary>
        void LeafFly(Vector3 at, float u, int n, float spread = 1f)
        {
            for (int i = 0; i < n; i++)
            {
                var l = Ast(Pw + "g_leaf", at, Rnd(0.18f, 0.3f) * u, Al(Color.white, Am), 6, Rnd(0, 360));
                var end = at + new Vector3(Rnd(-1.2f, 1.2f) * spread, Rnd(-0.3f, 1.2f) * spread, 0f) * u;
                float spin = Rnd(-500f, 500f), ph = Rnd(0, 6);
                Anim(l, Rnd(0.5f, 0.8f), (s, k) => { s.transform.position = Vector3.Lerp(at, end, Out(k)) + Vector3.right * Mathf.Sin(k * 12f + ph) * 0.15f * u + Vector3.down * k * k * 0.5f * u; s.transform.Rotate(0, 0, spin * BattleTime.FxDt); s.color = Al(Color.white, Am * (1f - k * k)); });
            }
        }

        /// <summary>초록 칼바람이 베고 지나간 자국</summary>
        void LeafSlash(Vector3 at, float ang, float len, float u)
        {
            var sl = Ast(Pw + "g_blade", at, 0f, Al(Color.white, Am), 7, ang);
            Anim(sl, 0.22f, (s, k) => { SizeWH(s, len * Out(k * 2f), 0.35f * len * (1f - k)); s.color = Al(Color.white, Am * (1f - k)); });
            Impact(at, 0.9f * u, GLight, 0.16f);
            LeafFly(at, u, 4, 0.8f);
        }

        /// <summary>바람 칼날 하나를 날린다 (날아가는 쪽으로 누운 초승달)</summary>
        IEnumerator BladeShot(Vector3 from, Vector3 to, float w, float sec, float u, bool hit, bool last)
        {
            var dir = to - from;
            var b = Ast(Pw + "g_blade", from, w, Al(Color.white, Am), 6, Ang(dir) - 90f);
            float trail = 0f;
            for (float t = 0f; t < sec && b != null; t += BattleTime.FxDt)
            {
                b.transform.position = Vector3.Lerp(from, to, t / sec);
                trail += BattleTime.FxDt;
                if (trail > 0.03f) { trail = 0f; var g = Ast(Pw + "g_blade", b.transform.position, w, Al(GLight, 0.4f * Am), 5, Ang(dir) - 90f); Anim(g, 0.15f, (s, k) => s.color = Al(GLight, 0.4f * Am * (1f - k))); }
                yield return null;
            }
            if (b != null) Destroy(b.gameObject);
            if (!hit) yield break;
            GameAudio.Se(last ? "hit_strong" : "hit", 0.6f);
            LeafSlash(to, Ang(dir) + Rnd(-30f, 30f), 1.6f * u, u);
            Run(Squash(astTarget, last ? 0.18f : 0.08f, 0.1f));
            if (last) AstHit(0.1f);
        }

        // ───── 기술 ─────

        /// <summary>바람의 칼날: 휙 하고 초록 칼바람 하나가 눈 깜짝할 새 날아가 벤다</summary>
        IEnumerator WindBlade(PalView user, Vector3 aim)
        {
            float u = U;
            yield return user.WindUp(aim, 0.1f);
            GameAudio.Se("select", 0.7f);
            yield return BladeShot(user.Center, aim, 1.1f * u, 0.14f, u, true, true);
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>씨앗 기관총: 볼을 부풀렸다가 딱딱한 씨앗을 다다다 연사해, 맞은 자리에서 껍질이 튄다</summary>
        IEnumerator SeedMachineGun(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            Run(Squash(user, -0.12f, 0.2f));
            yield return BattleTime.FxWait(0.18f);
            for (int i = 0; i < 12; i++)
            {
                var s = Ast(Pw + "g_seed", from + dir * 0.5f * u, 0.22f * u, Al(Color.white, Am), 6, Ang(dir) + 90f);
                var end = aim + new Vector3(Rnd(-0.4f, 0.4f), Rnd(-0.4f, 0.4f), 0f) * u;
                Run(Throw(s, s.transform.position, end, 0.12f, 0f, 0f, true));
                bool last = i == 11;
                Run(DelayThen(0.12f, Do(() =>
                {
                    GameAudio.Se("hit", 0.35f);
                    Impact(end, 0.55f * u, GLight, 0.12f);
                    Spray(Pw + "g_seed", end, 3, 0.4f * u, 0.08f * u, Color.white, Color.white, 0.25f);
                    Run(Squash(astTarget, 0.05f, 0.05f));
                    if (last) { LeafFly(end, u, 5); AstHit(0.08f); }
                })));
                Run(Recoil(user, dir, 0.05f, 0.04f));
                yield return BattleTime.FxWait(0.045f);
            }
            yield return BattleTime.FxWait(0.4f);
        }

        /// <summary>씨앗 지뢰: 씨앗을 톡 던지면 상대 발밑에 박혀 싹이 트고, 꽃봉오리가 부풀어 펑 터진다</summary>
        IEnumerator SeedMine(PalView user, Vector3 aim)
        {
            float u = U;
            var g = GroundAt(aim, u) + Vector3.up * 0.1f * u;
            GameAudio.Se("select", 0.6f);
            var seed = Ast(Pw + "g_seed", user.Center, 0.4f * u, Al(Color.white, Am), 6);
            yield return Throw(seed, user.Center, g, 0.4f, 1.6f * u, 600f);
            Dust(g, u * 0.6f, 3, 0.6f);
            var sprout = Ast(Pw + "g_vine", g, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0f));
            var bud = Ast(Pw + "g_bloom", g, 0f, Al(Color.white, Am), 7);
            for (float t = 0f; t < 0.45f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.45f);
                SizeWH(sprout, 0.35f * u, 1.2f * u * k);
                bud.transform.position = g + Vector3.up * 1.15f * u * k;
                Size(bud, 0.6f * u * k * (1f + 0.15f * Mathf.Sin(t * 50f * k)));
                bud.transform.Rotate(0, 0, 200f * BattleTime.FxDt);
                yield return null;
            }
            GameAudio.Se("hit_strong", 0.8f);
            var p = bud.transform.position;
            Destroy(bud.gameObject); Destroy(sprout.gameObject);
            ScreenFlash(GLight, 0.25f, 0.02f, 0.03f, 0.25f);
            var burst = Ast(Pw + "g_sun", p, 0f, Al(GLight, Am), 7);
            Anim(burst, 0.3f, (s, k) => { Size(s, 2.4f * u * Out(k * 2f)); s.color = Al(GLight, Am * (1f - k)); });
            Spray(Pw + "g_leaf", p, 10, 1.4f * u, 0.3f * u, Color.white, Color.white, 0.5f);
            for (int i = 0; i < 6; i++) { var pt = Ast(Pw + "g_bloom", p, 0.3f * u, Al(Color.white, Am), 6); Run(Throw(pt, p, p + Dir(i * 60f + 30f) * 1.3f * u, 0.4f, 0.4f * u, 400f)); }
            Run(Squash(astTarget, 0.22f));
            AstHit(0.14f);
            yield return BattleTime.FxWait(0.55f);
        }

        /// <summary>멀티 커터: 바람 칼날 세 개가 일렬로 꼬리를 물고 날아가 같은 곳을 연달아 벤다</summary>
        IEnumerator MultiCutter(PalView user, Vector3 aim)
        {
            float u = U;
            yield return user.WindUp(aim, 0.12f);
            for (int i = 0; i < 3; i++)
            {
                GameAudio.Se("select", 0.6f);
                Run(BladeShot(user.Center, aim + Vector3.up * (1 - i) * 0.25f * u, (0.9f + i * 0.15f) * u, 0.2f, u, true, i == 2));
                yield return BattleTime.FxWait(0.08f);
            }
            yield return BattleTime.FxWait(0.5f);
        }

        /// <summary>초록 폭풍: 몸 양옆에 풀잎 회오리가 솟아 X자로 엇갈려 날아가고, 상대를 휘감아 잎을 흩뿌린다</summary>
        IEnumerator GrassTornado(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, d = aim - from;
            var side = new Vector3(-d.y, d.x, 0f).normalized * 1.3f * u;
            var tw = new[] { Ast(Pw + "g_twister", from + side, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0.1f)), Ast(Pw + "g_twister", from - side, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0.1f)) };
            GameAudio.Se("shine", 0.6f);
            for (float t = 0f; t < 0.25f; t += BattleTime.FxDt) { foreach (var x in tw) SizeWH(x, 1f * u, 2.2f * u * BackOut(t / 0.25f)); yield return null; }
            var a0 = new[] { from + side, from - side };
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.5f);
                for (int i = 0; i < 2; i++)
                {
                    var ctrl = Vector3.Lerp(a0[i], aim, 0.5f) - (i == 0 ? -1f : 1f) * side * 1.2f;
                    tw[i].transform.position = Vector3.Lerp(Vector3.Lerp(a0[i], ctrl, k), Vector3.Lerp(ctrl, aim, k), k) + Vector3.down * 0.6f * u;
                    tw[i].flipX = Mathf.Repeat(t, 0.08f) < 0.04f;
                    if (Random.value < 0.4f) LeafFly(tw[i].transform.position + Vector3.up * u, u, 1, 0.5f);
                }
                yield return null;
            }
            GameAudio.Se("hit_strong", 0.8f);
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                for (int i = 0; i < 2; i++) { tw[i].transform.position = aim + Vector3.down * 0.6f * u + Vector3.right * Mathf.Sin(t * 20f + i * 3.14f) * 0.4f * u; SizeWH(tw[i], 1.2f * u, 2.6f * u * (1f - t)); tw[i].flipX = Mathf.Repeat(t, 0.08f) < 0.04f; }
                if (Random.value < 0.6f) LeafFly(aim, u, 1, 1.2f);
                if (!astHit) { AstHit(0.12f); }
                if (Random.value < 0.15f) Run(Squash(astTarget, 0.08f, 0.08f));
                yield return null;
            }
            foreach (var x in tw) Destroy(x.gameObject);
            LeafFly(aim, u, 10, 1.3f);
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>윈드 에지: 팔을 크게 휘둘러 다섯 갈래로 퍼지는 칼바람을 부채꼴로 날린다</summary>
        IEnumerator WindEdge(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = aim - from;
            float dist = dir.magnitude;
            yield return user.WindUp(aim, 0.14f);
            var sweep = Ast(Pw + "g_blade", from + dir.normalized * 0.6f * u, 0f, Al(GLight, Am), 7, Ang(dir) - 90f);
            Anim(sweep, 0.2f, (s, k) => { Size(s, 2.2f * u * Out(k * 2f)); s.color = Al(GLight, Am * (1f - k)); });
            GameAudio.Se("select", 0.8f);
            for (int i = -2; i <= 2; i++)
            {
                var to = from + (Vector3)(Vector2)(Quaternion.Euler(0, 0, i * 12f) * dir.normalized) * (dist + 0.6f * u);
                Run(BladeShot(from, i == 0 ? aim : to, 0.9f * u, 0.22f, u, i == 0 || Mathf.Abs(i) == 1, i == 0));
            }
            yield return BattleTime.FxWait(0.6f);
        }

        /// <summary>가시덩굴: 땅속을 파고드는 뿌리가 흙을 들썩이며 상대 발밑까지 쫓아가, 가시덩굴로 솟구쳐 찌른다</summary>
        IEnumerator ThornVine(PalView user, Vector3 aim)
        {
            float u = U;
            var a = new Vector3(user.Center.x, user.Feet.y, 0f);
            var g = GroundAt(aim, u);
            GameAudio.Se("stat_down", 0.5f);
            for (float t = 0f; t < 0.5f; t += BattleTime.FxDt)
            {
                var p = Vector3.Lerp(a, g, t / 0.5f) + Vector3.right * Mathf.Sin(t * 30f) * 0.1f * u;
                if (Random.value < 0.6f) Dust(p, u * 0.35f, 1, 0.4f);
                if (Random.value < 0.3f) { var r = Ast(Pw + "g_root", p, 0.18f * u, Al(Color.white, Am), 3, 0f, new Vector2(0.5f, 0f)); Anim(r, 0.3f, (s, k) => SizeWH(s, 0.18f * u, 0.4f * u * Bell(k))); }
                yield return null;
            }
            GameAudio.Se("hit_strong", 0.8f);
            for (int i = -2; i <= 2; i++)
            {
                var v = Ast(Pw + "g_vine", g + Vector3.right * i * 0.3f * u, 0f, Al(Color.white, Am), i == 0 ? 7 : 5, i * 12f, new Vector2(0.5f, 0f));
                float h = (i == 0 ? 3f : 2f) * u;
                Anim(v, 0.7f, (s, k) => SizeWH(s, 0.7f * u, h * (k < 0.2f ? BackOut(k / 0.2f) : 1f - In((k - 0.2f) / 0.8f))), Mathf.Abs(i) * 0.04f);
            }
            Dust(g, u, 6);
            LeafFly(g + Vector3.up * u, u, 6);
            Run(Squash(astTarget, 0.24f));
            Run(CameraShake(0.1f, 0.2f));
            AstHit(0.12f);
            yield return BattleTime.FxWait(0.75f);
        }

        /// <summary>원형 덩굴: 상대 둘레 땅에 초록 원이 그려지고, 둥글게 뾰족한 뿌리가 솟은 뒤 한가운데에서 가장 큰 뿌리가 찌른다</summary>
        IEnumerator CircleVine(PalView user, Vector3 aim)
        {
            float u = U;
            yield return user.WindUp(aim, 0.15f);
            var g = GroundAt(aim, u);
            var ring = Ast(Pw + "g_barrier", g, 0f, Al(GLeaf, Am), 1);
            GameAudio.Se("stat_down", 0.5f);
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt) { Size(ring, 3.6f * u * Out(t / 0.3f), 0.3f); ring.transform.Rotate(0, 0, 90f * BattleTime.FxDt); yield return null; }
            for (int i = 0; i < 8; i++)
            {
                float an = i * 45f * Mathf.Deg2Rad;
                var p = g + new Vector3(Mathf.Cos(an) * 1.6f * u, Mathf.Sin(an) * 0.5f * u, 0f);
                var r = Ast(Pw + "g_root", p, 0f, Al(Color.white, Am), Mathf.Sin(an) > 0 ? 3 : 6, -Mathf.Cos(an) * 20f, new Vector2(0.5f, 0f));
                Anim(r, 0.7f, (s, k) => SizeWH(s, 0.5f * u, 1.8f * u * (k < 0.15f ? BackOut(k / 0.15f) : 1f - In((k - 0.15f) / 0.85f))));
                Dust(p, u * 0.4f, 1, 0.4f);
                GameAudio.Se("hit", 0.35f);
                yield return BattleTime.FxWait(0.04f);
            }
            yield return BattleTime.FxWait(0.15f);
            GameAudio.Se("hit_strong");
            var big = Ast(Pw + "g_root", g, 0f, Al(Color.white, Am), 7, 0f, new Vector2(0.5f, 0f));
            Anim(big, 0.75f, (s, k) => SizeWH(s, 1.1f * u, 3.6f * u * (k < 0.15f ? BackOut(k / 0.15f) : 1f - In((k - 0.15f) / 0.85f))));
            Dust(g, u, 8, 2f);
            Run(CameraShake(0.15f, 0.3f));
            Run(Squash(astTarget, 0.28f));
            AstHit(0.16f);
            Anim(ring, 0.6f, (s, k) => s.color = Al(GLeaf, Am * (1f - k)));
            yield return BattleTime.FxWait(0.75f);
        }

        /// <summary>리플렉트 리프: 칼바람이 보이지 않는 벽에 튕기며 지그재그로 오가 상대를 여러 번 벤다</summary>
        IEnumerator ReflectLeaf(PalView user, Vector3 aim)
        {
            float u = U;
            yield return user.WindUp(aim, 0.12f);
            var pts = new[] { user.Center, aim + new Vector3(-1.4f, 1.6f, 0f) * u, aim, aim + new Vector3(1.4f, 1.4f, 0f) * u, aim, aim + new Vector3(0f, -1.6f, 0f) * u, aim };
            for (int i = 0; i < pts.Length - 1; i++)
            {
                bool hitHere = (i + 1) % 2 == 0;
                GameAudio.Se(hitHere ? "hit" : "select", 0.5f);
                yield return BladeShot(pts[i], pts[i + 1], 0.9f * u, 0.11f, u, hitHere, i == pts.Length - 2);
                if (!hitHere) { Impact(pts[i + 1], 0.6f * u, GLight, 0.12f); }
            }
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>급습의 칼날: 상대 둘레 허공에 칼바람이 빙 둘러 나타나더니, 사방에서 한꺼번에 몰려들어 난도질한다</summary>
        IEnumerator AmbushBlades(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.45f, 0.25f));
            yield return user.WindUp(aim, 0.14f);
            const int n = 12;
            var bl = new SpriteRenderer[n]; var st = new Vector3[n];
            GameAudio.Se("shine", 0.6f);
            for (int i = 0; i < n; i++)
            {
                st[i] = aim + Dir(i * 30f + Rnd(-8f, 8f)) * Rnd(2f, 2.6f) * u;
                bl[i] = Ast(Pw + "g_blade", st[i], 0f, Al(Color.white, Am), 6, Ang(aim - st[i]) - 90f);
                Run(PopIn(bl[i], 0.8f * u, i * 0.02f));
            }
            yield return BattleTime.FxWait(0.4f);
            for (int i = 0; i < n; i++)
            {
                Destroy(bl[i].gameObject);
                bool last = i == n - 1;
                Run(BladeShot(st[i], aim + (Vector3)(Random.insideUnitCircle * 0.3f * u), 0.8f * u, 0.14f, u, i % 2 == 0 || last, last));
                yield return BattleTime.FxWait(0.035f);
            }
            yield return BattleTime.FxWait(0.3f);
            LeafFly(aim, u, 14, 1.4f);
            yield return BattleTime.FxWait(0.3f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>태양 폭발: 햇빛 알갱이를 빨아들여 머리 위에 작은 태양을 띄우고, 황금빛 광선으로 내리쏜다</summary>
        IEnumerator SolarBlast(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.5f, 0.3f));
            var cam = CamRect();
            var sunAt = user.Center + Vector3.up * user.Height * 0.55f; sunAt.y = Mathf.Min(sunAt.y, cam.yMax - 1f * u);
            var sun = Ast(Pw + "g_sun", sunAt, 0f, Al(Color.white, Am), 6);
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.7f; t += BattleTime.FxDt)
            {
                Size(sun, 1.8f * u * Out(t / 0.7f) * (1f + 0.06f * Mathf.Sin(t * 30f))); sun.transform.Rotate(0, 0, 60f * BattleTime.FxDt);
                if (Random.value < 0.7f) Mote(sunAt + Dir(Rnd(0, 360)) * 2.6f * u, sunAt, 0.18f * u, Color.white, 0.3f, 0f, Pw + "g_mote", 5);
                yield return null;
            }
            GameAudio.Se("hit_strong");
            var dir = aim - sunAt;
            var beam = Ast(Pw + "g_beam", sunAt, 0f, Al(Color.white, Am), 7, Ang(dir), new Vector2(0f, 0.5f));
            float len = dir.magnitude + 1f * u;
            ScreenFlash(GSun, 0.35f, 0.02f, 0.04f, 0.3f);
            for (float t = 0f; t < 0.6f; t += BattleTime.FxDt)
            {
                float f = t > 0.45f ? 1f - (t - 0.45f) / 0.15f : 1f;
                SizeWH(beam, len * Out(t / 0.08f), 1.2f * u * (1f + 0.1f * Mathf.Sin(t * 60f)) * f);
                if (Random.value < 0.6f) Mote(aim, aim + (Vector3)(Random.insideUnitCircle * 1.3f * u), 0.2f * u, Color.white, 0.3f, 0f, Pw + "g_mote", 7);
                if (t > 0.06f && !astHit) { AstHit(0.16f); Run(Squash(astTarget, 0.2f, 0.4f)); }
                yield return null;
            }
            Destroy(beam.gameObject);
            Anim(sun, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            var burst = Ast(Pw + "g_sun", aim, 0f, Al(Color.white, Am), 8);
            Anim(burst, 0.35f, (s, k) => { Size(s, 3f * u * Out(k * 2f)); s.color = Al(Color.white, Am * (1f - k)); });
            yield return BattleTime.FxWait(0.4f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>크로스 윈드: 상대 앞에 커다란 칼바람 둘이 떠올라 잠시 겨누다가, X자로 엇갈리며 베어 넘긴다</summary>
        IEnumerator CrossWind(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.4f, 0.25f));
            yield return user.WindUp(aim, 0.15f);
            var b1 = Ast(Pw + "g_blade", aim + new Vector3(-1.4f, 1.4f, 0f) * u, 0f, Al(Color.white, Am), 7, 45f);
            var b2 = Ast(Pw + "g_blade", aim + new Vector3(1.4f, 1.4f, 0f) * u, 0f, Al(Color.white, Am), 7, -45f);
            GameAudio.Se("shine", 0.6f);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { Size(b1, 2.4f * u * BackOut(t / 0.35f)); Size(b2, 2.4f * u * BackOut(t / 0.35f)); yield return null; }
            yield return BattleTime.FxWait(0.1f);
            GameAudio.Se("hit_strong");
            Run(Throw(b1, b1.transform.position, aim + new Vector3(1.4f, -1.4f, 0f) * u, 0.14f));
            Run(Throw(b2, b2.transform.position, aim + new Vector3(-1.4f, -1.4f, 0f) * u, 0.14f));
            yield return BattleTime.FxWait(0.07f);
            LeafSlash(aim, 45f, 3.4f * u, u);
            LeafSlash(aim, -45f, 3.4f * u, u);
            ScreenFlash(GLight, 0.3f, 0.02f, 0.03f, 0.25f);
            LeafFly(aim, u, 10, 1.5f);
            Run(Squash(astTarget, 0.26f));
            AstHit(0.18f);
            yield return BattleTime.FxWait(0.55f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>윈드 블래스트: 자기 둘레에 거대한 회오리바람을 일으켜 잎사귀째 휘몰아치고, 그대로 상대 쪽까지 휩쓴다</summary>
        IEnumerator WindBlast(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.5f, 0.3f));
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            var tw = Ast(Pw + "g_twister", ug, 0f, Al(Color.white, Am), 7, 0f, new Vector2(0.5f, 0.05f));
            var tw2 = Ast(Pw + "g_twister", ug, 0f, Al(GLight, 0.6f * Am), 4, 0f, new Vector2(0.5f, 0.05f));
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.6f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.6f);
                SizeWH(tw, 2.4f * u * k, 4.2f * u * k); SizeWH(tw2, 3.2f * u * k, 4.6f * u * k);
                tw.flipX = Mathf.Repeat(t, 0.07f) < 0.035f; tw2.flipX = !tw.flipX;
                if (Random.value < 0.8f) LeafFly(ug + Vector3.up * Rnd(0.5f, 3f) * u, u, 1, 1.4f);
                if (Random.value < 0.2f) Run(CameraShake(0.03f, 0.05f));
                yield return null;
            }
            GameAudio.Se("hit_strong");
            var g = GroundAt(aim, u);
            for (float t = 0f; t < 0.45f; t += BattleTime.FxDt)
            {
                float k = In(t / 0.45f);
                tw.transform.position = tw2.transform.position = Vector3.Lerp(ug, g, k);
                tw.flipX = Mathf.Repeat(t, 0.07f) < 0.035f; tw2.flipX = !tw.flipX;
                if (Random.value < 0.8f) LeafFly(tw.transform.position + Vector3.up * 2f * u, u, 1, 1.4f);
                Dust(tw.transform.position, u * 0.5f, 1, 0.8f);
                yield return null;
            }
            ScreenFlash(GLight, 0.35f, 0.02f, 0.04f, 0.3f);
            LeafFly(aim, u, 16, 2f);
            Run(Squash(astTarget, 0.28f));
            Run(CameraShake(0.22f, 0.4f));
            AstHit(0.22f);
            Anim(tw, 0.4f, (s, k) => { SizeWH(s, 2.4f * u * (1f + k), 4.2f * u); s.color = Al(Color.white, Am * (1f - k)); });
            Anim(tw2, 0.4f, (s, k) => s.color = Al(GLight, 0.6f * Am * (1f - k)));
            yield return BattleTime.FxWait(0.6f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>근육 펀치 (모산다): 성큼 다가가 굵은 초록 주먹으로 잽잽잽 두들기고, 마지막에 크게 휘둘러 날려 버린다</summary>
        IEnumerator MusclePunch(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 dir = (aim - user.Center).normalized;
            bool flip = dir.x < 0;
            yield return user.Lunge(aim, 99f, 0.3f, () => { });
            for (int i = 0; i < 4; i++)
            {
                bool last = i == 3;
                var p = aim - dir * (last ? 1.2f : 0.9f) * u + new Vector3(0f, Rnd(-0.35f, 0.35f) * u, 0f);
                var f = Ast(Pw + "g_fist", p, (last ? 1.6f : 0.9f) * u, Al(Color.white, Am), 8);
                f.flipX = flip;
                var hit = aim + new Vector3(Rnd(-0.2f, 0.2f), Rnd(-0.3f, 0.3f), 0f) * u;
                Anim(f, last ? 0.3f : 0.14f, (s, k) => { s.transform.position = Vector3.Lerp(p, hit, Out(k * 2.5f)); s.color = Al(Color.white, Am * (k < 0.6f ? 1f : (1f - k) / 0.4f)); });
                yield return BattleTime.FxWait(0.06f);
                GameAudio.Se(last ? "hit_strong" : "hit", 0.7f);
                Impact(hit, (last ? 2.2f : 1f) * u, last ? GSun : GLight, 0.18f);
                Run(Squash(astTarget, last ? 0.28f : 0.1f, 0.1f));
                if (last) { SpeedLines(aim, 5f * u, 0.4f, GLight); Dust(GroundAt(aim, u), u, 6); AstHit(0.2f); }
                else Run(CameraShake(0.04f, 0.05f));
                yield return BattleTime.FxWait(last ? 0.3f : 0.09f);
            }
            yield return BattleTime.FxWait(0.3f);
        }

        /// <summary>스나이핑 샷 (로빈퀼): 상대에게 조준선을 겹쳐 맞추고 숨을 고른 뒤, 화살 한 발로 정확히 꿰뚫는다</summary>
        IEnumerator SnipingShot(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.45f, 0.2f));
            var ret = Ast(Pw + "g_reticle", aim, 0f, Al(Color.white, 0f), 8);
            GameAudio.Se("select", 0.6f);
            for (float t = 0f; t < 0.55f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.55f);
                Size(ret, Mathf.Lerp(3.5f, 1.5f, k) * u); ret.transform.rotation = Quaternion.Euler(0, 0, (1f - k) * 90f);
                ret.transform.position = aim + new Vector3(Mathf.Sin(t * 13f), Mathf.Cos(t * 11f), 0f) * 0.3f * u * (1f - k);
                ret.color = Al(Color.white, Am * Mathf.Min(1f, t * 5f));
                yield return null;
            }
            GameAudio.Se("select", 0.9f);
            Run(Recoil(user, aim - user.Center, 0.2f, 0.15f));
            var from = user.Center;
            var arrow = Ast(Pw + "g_arrow", from, 1.5f * u, Al(Color.white, Am), 7, Ang(aim - from));
            yield return Throw(arrow, from, aim, 0.1f, 0f, 0f, true, (s, k) => { var g = Ast(Pw + "g_beam", s.transform.position, 0.4f * u, Al(GLight, 0.4f), 5, Ang(aim - from)); Anim(g, 0.2f, (r, kk) => r.color = Al(GLight, 0.4f * (1f - kk))); });
            GameAudio.Se("hit_strong");
            Impact(aim, 1.6f * u, GLight, 0.2f);
            var pierce = Ast(Pw + "g_beam", aim, 0f, Al(Color.white, Am), 8, Ang(aim - from), new Vector2(0f, 0.5f));
            Anim(pierce, 0.3f, (s, k) => { SizeWH(s, 2.4f * u * Out(k * 3f), 0.3f * u * (1f - k)); });
            Anim(ret, 0.3f, (s, k) => { Size(s, 1.5f * u * (1f + k)); s.color = Al(Color.white, Am * (1f - k)); });
            LeafFly(aim, u, 6);
            Run(Squash(astTarget, 0.2f));
            AstHit(0.14f);
            yield return BattleTime.FxWait(0.45f);
            yield return DimTo(0f, 0.2f);
        }

        /// <summary>꼬리 채찍 (다이노섬): 꼬리로 땅을 쾅 내리치면, 풀이 줄지어 솟구치는 충격이 상대까지 내달린다</summary>
        IEnumerator TailWhip(PalView user, Vector3 aim)
        {
            float u = U;
            var a = new Vector3(user.Center.x, user.Feet.y, 0f);
            var g = GroundAt(aim, u);
            Run(Squash(user, 0.2f, 0.2f));
            GameAudio.Se("hit", 0.8f);
            Impact(a, 1.3f * u, GLight, 0.18f);
            Dust(a, u, 6);
            Run(CameraShake(0.08f, 0.12f));
            for (int i = 1; i <= 7; i++)
            {
                var p = Vector3.Lerp(a, g, i / 7f);
                for (int j = 0; j < 3; j++)
                {
                    var l = Ast(Pw + "g_leaf", p + Vector3.right * Rnd(-0.2f, 0.2f) * u, 0f, Al(Color.white, Am), 5, Rnd(-30f, 30f), new Vector2(0.5f, 0f));
                    float h = Rnd(0.5f, 0.9f) * u * (i == 7 ? 2f : 1f);
                    Anim(l, 0.35f, (s, k) => SizeWH(s, 0.3f * u, h * Bell(k)));
                }
                Dust(p, u * 0.4f, 2, 0.6f);
                yield return BattleTime.FxWait(0.035f);
            }
            GameAudio.Se("hit_strong", 0.8f);
            Impact(aim, 1.6f * u, GLeaf, 0.2f);
            LeafFly(aim, u, 8);
            Run(Squash(astTarget, 0.2f));
            AstHit(0.12f);
            yield return BattleTime.FxWait(0.45f);
        }

        /// <summary>윈드 배리어 (릴린): 몸 둘레로 바람이 감겨 돌며 잎사귀를 실은 반투명 바람 방벽을 두른다</summary>
        IEnumerator WindBarrier(PalView user, Vector3 aim)
        {
            float u = U;
            var c = user.Center;
            var dome = Ast(Pw + "g_barrier", c, 0f, Al(Color.white, 0f), 7);
            GameAudio.Se("stat_up", 0.8f);
            var leaves = new SpriteRenderer[8];
            for (int i = 0; i < leaves.Length; i++) leaves[i] = Ast(Pw + "g_leaf", c, 0.28f * u, Al(Color.white, Am), 8);
            for (float t = 0f; t < 1.1f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.4f), f = t > 0.85f ? 1f - (t - 0.85f) / 0.25f : 1f;
                Size(dome, user.Height * 1.3f * k); dome.transform.Rotate(0, 0, 150f * BattleTime.FxDt);
                dome.color = Al(Color.white, 0.9f * Am * k * f);
                for (int i = 0; i < leaves.Length; i++)
                {
                    float an = t * 400f + i * 45f;
                    leaves[i].transform.position = c + new Vector3(Mathf.Cos(an * Mathf.Deg2Rad) * 0.65f, Mathf.Sin(an * Mathf.Deg2Rad) * 0.25f + (i % 2 == 0 ? 0.3f : -0.3f), 0f) * user.Height * k;
                    leaves[i].transform.rotation = Quaternion.Euler(0, 0, an + 90f);
                    leaves[i].color = Al(Color.white, Am * f);
                    leaves[i].sortingOrder = Mathf.Sin(an * Mathf.Deg2Rad) < 0 ? sortingOrder + 9 : user.SortingOrder - 1;
                }
                yield return null;
            }
            Destroy(dome.gameObject);
            foreach (var l in leaves) Destroy(l.gameObject);
        }

        /// <summary>풍양의 가호 (릴린): 발밑에 꽃이 잇달아 피어나고, 꽃잎과 연둣빛 생명의 빛이 몸을 감싸 올라 상처를 아문다</summary>
        IEnumerator AbundantBlessing(PalView user, Vector3 aim)
        {
            float u = U;
            var feet = new Vector3(user.Center.x, user.Feet.y, 0f);
            GameAudio.Se("shine", 0.8f);
            for (int i = 0; i < 7; i++)
            {
                var p = feet + new Vector3(Rnd(-1.3f, 1.3f) * u, Rnd(-0.2f, 0.2f) * u, 0f);
                var f = Ast(Pw + "g_bloom", p, 0f, Al(Color.white, 1f), 3, Rnd(0, 360));
                float sz = Rnd(0.4f, 0.65f) * u;
                Anim(f, 1.3f, (s, k) => { Size(s, sz * (k < 0.25f ? BackOut(k / 0.25f) : 1f), 0.5f); s.transform.Rotate(0, 0, 30f * BattleTime.FxDt); s.color = Al(Color.white, k < 0.8f ? 1f : (1f - k) * 5f); });
                yield return BattleTime.FxWait(0.06f);
            }
            var halo = Ast(Pw + "g_barrier", user.Center, 0f, Al(GLight, 0f), 6);
            for (float t = 0f; t < 0.9f; t += BattleTime.FxDt)
            {
                Size(halo, user.Height * 1.2f * Out(t / 0.3f)); halo.color = Al(GLight, 0.7f * Bell(t / 0.9f));
                if (Random.value < 0.7f) { var p = feet + Vector3.right * Rnd(-1f, 1f) * u; Mote(p, p + Vector3.up * user.Height * Rnd(0.8f, 1.3f), 0.2f * u, Color.white, 0.6f, 0f, Pw + "g_mote", 7); }
                if (Random.value < 0.25f) { var p = feet + Vector3.right * Rnd(-1f, 1f) * u; var pt = Ast(Pw + "g_bloom", p, 0.18f * u, Al(GPink, 1f), 7, Rnd(0, 360)); Run(Throw(pt, p, p + new Vector3(Rnd(-0.5f, 0.5f), user.Height / u * 1.1f, 0f) * u, 0.7f, 0f, 300f)); }
                yield return null;
            }
            Run(user.Flash(GLight, 2, 0.1f));
            Destroy(halo.gameObject);
            yield return BattleTime.FxWait(0.2f);
        }
    }
}
