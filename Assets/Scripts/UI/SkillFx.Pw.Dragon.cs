using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>용 속성 팰월드 기술 연출 (그림: PwFx/dr_*)</summary>
    public partial class SkillFx
    {
        static readonly Color GDragon = C(0xa060ff), GDragonPink = C(0xff8ae0), GDragonDeep = C(0x4a2ad0);

        static partial void RegDragon()
        {
            PwFx["Dragon_Cannon"] = (fx, u, t, a) => fx.DragonCannonFx(u, a);
            PwFx["Dragon_Wave"] = (fx, u, t, a) => fx.DragonWaveFx(u, a);
            PwFx["Dragon_Breath"] = (fx, u, t, a) => fx.DragonBreathFx(u, a);
            PwFx["Comet"] = (fx, u, t, a) => fx.CometFx(u, a);
            PwFx["Shockwave_Cannon"] = (fx, u, t, a) => fx.ShockwaveCannonFx(u, a);
            PwFx["Beam_Slicer"] = (fx, u, t, a) => fx.BeamSlicerFx(u, a);
            PwFx["Charge_Cannon"] = (fx, u, t, a) => fx.ChargeCannonFx(u, a);
            PwFx["Comet_Fall"] = (fx, u, t, a) => fx.CometFallFx(u, a);
            PwFx["Dragon_Meteor"] = (fx, u, t, a) => fx.DragonMeteorFx(u, a);
            PwFx["Meteor_Rain"] = (fx, u, t, a) => fx.MeteorRainFx(u, a);
            PwFx["Mystic_Hurricane"] = (fx, u, t, a) => fx.MysticHurricaneFx(u, a);
            PwFx["Rocket_Tackle"] = (fx, u, t, a) => fx.RocketTackleFx(u, a);
            PwFx["Meteor_Beam"] = (fx, u, t, a) => fx.MeteorBeamFx(u, a);
        }

        // ───── 공용 조각 ─────

        /// <summary>용의 힘이 터진다: 육각 별 + 비늘 조각 + 고리</summary>
        void DragonBurst(Vector3 at, float size, float u)
        {
            var b = Ast(Pw + "dr_burst", at, 0f, Al(Color.white, Am), 7, Rnd(0, 60));
            Anim(b, 0.35f, (s, k) => { Size(s, size * Out(k * 2f)); s.transform.Rotate(0, 0, 150f * BattleTime.FxDt); s.color = Al(Color.white, Am * (1f - k)); });
            Ring(at, 0.2f * size, size * 1.1f, Al(Color.white, Am), 0.35f, Pw + "dr_shock");
            Spray(Pw + "dr_scale", at, 8, size * 0.6f, 0.25f * u, Color.white, GDragonPink, 0.4f);
        }

        /// <summary>운석 하나를 떨어뜨린다 (from → to), 떨어지면 충격파</summary>
        IEnumerator MeteorDrop(Vector3 from, Vector3 to, float size, float sec, float u, bool big, bool hit)
        {
            var m = Ast(Pw + "dr_meteor", from, size, Al(Color.white, Am), 8, Ang(to - from));
            for (float t = 0f; t < sec && m != null; t += BattleTime.FxDt)
            {
                m.transform.position = Vector3.Lerp(from, to, t / sec);
                if (Random.value < 0.4f) Mote(m.transform.position, m.transform.position + (Vector3)(Random.insideUnitCircle * 0.4f * u), 0.2f * u, Color.white, 0.3f, 0f, Pw + "dr_scale", 6);
                yield return null;
            }
            if (m != null) Destroy(m.gameObject);
            GameAudio.Se(big ? "hit_strong" : "hit", big ? 1f : 0.6f);
            DragonBurst(to, (big ? 3f : 1.5f) * u, u);
            Ring(to + Vector3.down * 0.4f * u, 0.3f * u, (big ? 4.5f : 2.2f) * u, Al(Color.white, Am), 0.45f, Pw + "dr_shock", 0.32f);
            Dust(to + Vector3.down * 0.5f * u, u, big ? 8 : 3, big ? 2f : 1f);
            if (!astMissed) Run(CameraShake(big ? 0.25f : 0.07f, big ? 0.35f : 0.12f));
            if (hit) { Run(Squash(astTarget, big ? 0.28f : 0.1f)); if (big) AstHit(0.22f); }
        }

        // ───── 기술 ─────

        /// <summary>용 대포: 입 앞에 육각 무늬가 도는 용의 에너지탄을 빚어, 비늘 가루를 흩날리며 쏘아 보낸다</summary>
        IEnumerator DragonCannonFx(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.55f * u;
            var o = Ast(Pw + "dr_orb", mouth, 0f, Al(Color.white, Am), 6);
            for (float t = 0f; t < 0.18f; t += BattleTime.FxDt) { Size(o, 0.8f * u * BackOut(t / 0.18f)); o.transform.Rotate(0, 0, 300f * BattleTime.FxDt); yield return null; }
            GameAudio.Se("hit", 0.7f);
            Run(Recoil(user, dir, 0.15f, 0.12f));
            yield return Throw(o, mouth, aim, 0.18f, 0f, 500f, false, (s, k) => { if (Random.value < 0.6f) Mote(s.transform.position, s.transform.position - dir * 0.5f * u, 0.2f * u, Color.white, 0.25f, 0f, Pw + "dr_scale", 5); });
            DragonBurst(aim, 1.6f * u, u);
            Run(Squash(astTarget, 0.14f));
            AstHit(0.06f);
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>용의 파장: 용의 기운을 활활 끌어올려 포효하면, 보랏빛 충격파가 몸 둘레로 겹겹이 퍼진다</summary>
        IEnumerator DragonWaveFx(PalView user, Vector3 aim)
        {
            float u = U;
            var c = user.Center;
            var aura = Ast(Pw + "dr_aura", new Vector3(c.x, user.Feet.y, 0f), 0f, Al(Color.white, Am), Behind(user), 0f, new Vector2(0.5f, 0f));
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { SizeWH(aura, user.Height * 1.3f, user.Height * 1.5f * Out(t / 0.35f) * (1f + 0.06f * Mathf.Sin(t * 40f))); aura.flipX = Mathf.Repeat(t, 0.1f) < 0.05f; yield return null; }
            GameAudio.Se("hit_strong", 0.8f);
            float dist = Vector3.Distance(c, aim);
            for (int i = 0; i < 3; i++) Ring(c, 0.4f * u, dist * 2.3f, Al(Color.white, Am), 0.45f, Pw + "dr_shock", 1f, i * 0.07f);
            Run(CameraShake(0.08f, 0.25f));
            Anim(aura, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            yield return BattleTime.FxWait(0.22f);
            DragonBurst(aim, 1.6f * u, u);
            Run(Squash(astTarget, 0.16f));
            AstHit(0.08f);
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>용의 숨결: 비늘 같은 물결무늬가 일렁이는 보랏빛 숨결을 길게 내뿜어 상대를 휘감는다</summary>
        IEnumerator DragonBreathFx(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.55f * u;
            Run(DimTo(0.35f, 0.25f));
            for (float t = 0f; t < 0.3f; t += BattleTime.FxDt) { if (Random.value < 0.6f) Mote(mouth + Dir(Rnd(0, 360)) * 1.2f * u, mouth, 0.18f * u, Color.white, 0.2f, 0f, Pw + "dr_scale", 5); yield return null; }
            GameAudio.Se("hit", 0.8f);
            float len = Vector3.Distance(mouth, aim) + 1.3f * u;
            var cone = Ast(Pw + "dr_breath", mouth, 0f, Al(Color.white, Am), 6, Ang(dir), new Vector2(0f, 0.5f));
            for (float t = 0f; t < 0.85f; t += BattleTime.FxDt)
            {
                float f = t > 0.65f ? 1f - (t - 0.65f) / 0.2f : 1f;
                SizeWH(cone, len * Out(t / 0.12f), 2.3f * u * (1f + 0.12f * Mathf.Sin(t * 35f)) * f);
                cone.flipY = Mathf.Repeat(t, 0.1f) < 0.05f;
                if (Random.value < 0.5f) Spray(Pw + "dr_scale", aim, 1, 1f * u, 0.25f * u, Color.white, GDragonPink, 0.3f);
                if (t > 0.12f && !astHit) { AstHit(0.12f); Run(Squash(astTarget, 0.14f, 0.6f)); }
                yield return null;
            }
            Destroy(cone.gameObject);
            DragonBurst(aim, 1.8f * u, u);
            yield return BattleTime.FxWait(0.3f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>혜성: 하늘 저편에서 보랏빛 꼬리를 단 운석 하나가 비스듬히 곧장 떨어져 충격파를 일으킨다</summary>
        IEnumerator CometFx(PalView user, Vector3 aim)
        {
            float u = U;
            var cam = CamRect();
            var side = (aim - user.Center).x > 0 ? -1f : 1f;
            var from = new Vector3(aim.x + side * 3.5f * u, cam.yMax + 1f, 0f);
            GameAudio.Se("select", 0.7f);
            yield return MeteorDrop(from, aim, 1.8f * u, 0.35f, u, true, true);
            yield return BattleTime.FxWait(0.45f);
        }

        /// <summary>충격 대포: 에너지탄이 꽂힌 순간, 그 자리에서 앞쪽으로 폭발이 쾅쾅쾅 연쇄로 뻗어 나간다</summary>
        IEnumerator ShockwaveCannonFx(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var o = Ast(Pw + "dr_orb", from + dir * 0.5f * u, 0.9f * u, Al(Color.white, Am), 6);
            GameAudio.Se("hit", 0.7f);
            yield return Throw(o, o.transform.position, aim, 0.2f, 0f, 400f);
            for (int i = 0; i < 4; i++)
            {
                var p = aim + dir * i * 0.9f * u;
                GameAudio.Se(i == 0 ? "hit_strong" : "hit", 0.6f);
                DragonBurst(p, (1.8f - i * 0.2f) * u, u);
                if (i == 0) { Run(Squash(astTarget, 0.22f)); AstHit(0.14f); }
                Run(CameraShake(0.06f, 0.08f));
                yield return BattleTime.FxWait(0.08f);
            }
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>빔 슬라이서: 용의 광선으로 상대 쪽을 위에서 아래로 휩쓸고, 지나간 자국을 따라 폭발이 시간차로 터진다</summary>
        IEnumerator BeamSlicerFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.45f, 0.25f));
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.5f * u;
            float len = Vector3.Distance(mouth, aim) + 1f * u, baseAng = Ang(dir), sgn = dir.x >= 0 ? 1f : -1f;
            var beam = Ast(Pw + "dr_beam", mouth, 0f, Al(Color.white, Am), 7, baseAng + sgn * 25f, new Vector2(0f, 0.5f));
            GameAudio.Se("hit_strong", 0.7f);
            var marks = new System.Collections.Generic.List<Vector3>();
            for (float t = 0f; t < 0.4f; t += BattleTime.FxDt)
            {
                float a = baseAng + sgn * Mathf.Lerp(25f, -25f, Out(t / 0.4f));
                beam.transform.rotation = Quaternion.Euler(0, 0, a);
                SizeWH(beam, len, 0.6f * u);
                var tip = mouth + (Vector3)(Vector2)(Quaternion.Euler(0, 0, a) * Vector3.right) * len * 0.9f;
                if (marks.Count == 0 || Vector3.Distance(marks[marks.Count - 1], tip) > 0.45f * u) marks.Add(tip);
                yield return null;
            }
            Destroy(beam.gameObject);
            for (int i = 0; i < marks.Count; i++)
            {
                GameAudio.Se("hit", 0.4f);
                DragonBurst(marks[i], 1.3f * u, u);
                if (Vector3.Distance(marks[i], aim) < 0.8f * u) { Run(Squash(astTarget, 0.14f)); AstHit(0.14f); }
                yield return BattleTime.FxWait(0.06f);
            }
            if (!astHit) { DragonBurst(aim, 1.6f * u, u); Run(Squash(astTarget, 0.14f)); AstHit(0.14f); }
            yield return BattleTime.FxWait(0.35f);
            yield return DimTo(0f, 0.25f);
        }

        /// <summary>축적 대포: 몸이 떨릴 만큼 용의 에너지를 꾹꾹 눌러 담아 쏘면, 맞은 자리가 넓게 폭발한다</summary>
        IEnumerator ChargeCannonFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.55f, 0.3f));
            Vector3 from = user.Center, dir = (aim - from).normalized;
            var mouth = from + dir * 0.7f * u;
            var o = Ast(Pw + "dr_orb", mouth, 0f, Al(Color.white, Am), 7);
            GameAudio.Se("stat_up", 0.8f);
            for (float t = 0f; t < 0.7f; t += BattleTime.FxDt)
            {
                Size(o, 1.6f * u * Out(t / 0.7f)); o.transform.Rotate(0, 0, 400f * BattleTime.FxDt);
                o.transform.position = mouth + (Vector3)(Random.insideUnitCircle * 0.04f * u * t);
                if (Random.value < 0.7f) Mote(mouth + Dir(Rnd(0, 360)) * 2f * u, mouth, 0.22f * u, Color.white, 0.25f, 0f, Pw + "dr_scale", 6);
                if (Random.value < 0.25f) Ring(mouth, 2f * u, 0.4f * u, Al(Color.white, 0.6f * Am), 0.25f, Pw + "dr_shock");
                yield return null;
            }
            GameAudio.Se("hit_strong");
            Run(Recoil(user, dir, 0.35f, 0.3f));
            yield return Throw(o, mouth, aim, 0.18f, 0f, 600f);
            ScreenFlash(GDragonPink, 0.25f, 0.02f, 0.04f, 0.3f);
            DragonBurst(aim, 3.4f * u, u);
            Ring(aim, 0.5f * u, 4.6f * u, Al(Color.white, Am), 0.5f, Pw + "dr_shock");
            Ring(aim, 0.5f * u, 3.2f * u, Al(GDragonPink, Am), 0.45f, Pw + "dr_shock", 1f, 0.08f);
            Run(Squash(astTarget, 0.28f));
            Run(CameraShake(0.25f, 0.4f));
            AstHit(0.22f);
            yield return BattleTime.FxWait(0.55f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>혜성 낙하: 운석 셋을 연달아 떨어뜨려 상대 자리를 거듭 내리찍고, 충격파가 겹쳐 퍼진다</summary>
        IEnumerator CometFallFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.5f, 0.3f));
            var cam = CamRect();
            for (int i = 0; i < 3; i++)
            {
                bool last = i == 2;
                var to = last ? aim : aim + new Vector3(Rnd(-0.7f, 0.7f), Rnd(-0.3f, 0.3f), 0f) * u;
                var from = new Vector3(to.x + (i - 1) * 2.5f * u, cam.yMax + 1f, 0f);
                GameAudio.Se("select", 0.6f);
                Run(MeteorDrop(from, to, (last ? 2f : 1.4f) * u, 0.3f, u, last, true));
                yield return BattleTime.FxWait(0.22f);
            }
            yield return BattleTime.FxWait(0.65f);
            yield return DimTo(0f, 0.3f);
        }

        /// <summary>용의 운석: 등 뒤 하늘에 작은 운석 여럿을 불러내, 포물선을 그리며 상대에게 우르르 날려 보낸다</summary>
        IEnumerator DragonMeteorFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.6f, 0.3f));
            var c = user.Center;
            var cam = CamRect();
            var aura = Ast(Pw + "dr_aura", new Vector3(c.x, user.Feet.y, 0f), 0f, Al(Color.white, Am), Behind(user), 0f, new Vector2(0.5f, 0f));
            GameAudio.Se("stat_up", 0.7f);
            for (float t = 0f; t < 0.4f; t += BattleTime.FxDt) { SizeWH(aura, user.Height * 1.3f, user.Height * 1.6f * Out(t / 0.4f)); yield return null; }
            for (int i = 0; i < 8; i++)
            {
                bool last = i == 7;
                var from = new Vector3(c.x + Rnd(-1.5f, 0.5f) * u * Mathf.Sign((aim - c).x), Mathf.Min(c.y + Rnd(2f, 3f) * u, cam.yMax - 0.3f), 0f);
                var to = last ? aim : aim + (Vector3)(Random.insideUnitCircle * 0.8f * u);
                var ctrl = Vector3.Lerp(from, to, 0.5f) + Vector3.up * 1.5f * u;
                Run(MeteorArc(from, ctrl, to, (last ? 1.6f : 0.9f) * u, 0.42f, u, last));
                yield return BattleTime.FxWait(0.07f);
            }
            Anim(aura, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            yield return BattleTime.FxWait(0.8f);
            yield return DimTo(0f, 0.3f);
        }

        IEnumerator MeteorArc(Vector3 a, Vector3 c, Vector3 b, float size, float time, float u, bool last)
        {
            var m = Ast(Pw + "dr_meteor", a, size, Al(Color.white, Am), 8);
            Vector3 prev = a;
            for (float t = 0f; t < time && m != null; t += BattleTime.FxDt)
            {
                float k = t / time;
                var p = Vector3.Lerp(Vector3.Lerp(a, c, k), Vector3.Lerp(c, b, k), k);
                m.transform.position = p; m.transform.rotation = Quaternion.Euler(0, 0, Ang(p - prev)); prev = p;
                yield return null;
            }
            if (m != null) Destroy(m.gameObject);
            GameAudio.Se(last ? "hit_strong" : "hit", 0.5f);
            DragonBurst(b, (last ? 2.8f : 1.2f) * u, u);
            Run(Squash(astTarget, last ? 0.26f : 0.07f));
            if (last) { ScreenFlash(GDragonPink, 0.22f, 0.02f, 0.04f, 0.3f); Run(CameraShake(0.2f, 0.3f)); AstHit(0.2f); }
        }

        /// <summary>메테오 레인: 하늘이 어두워지고 셀 수 없는 운석이 비처럼 쏟아지며, 마지막 거대 운석이 상대를 짓누른다</summary>
        IEnumerator MeteorRainFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.75f, 0.35f));
            var cam = CamRect();
            GameAudio.Se("stat_down", 0.7f);
            yield return BattleTime.FxWait(0.25f);
            for (int i = 0; i < 16; i++)
            {
                var to = new Vector3(Rnd(cam.xMin + 1f, cam.xMax - 1f), aim.y + Rnd(-1f, 0.5f) * u, 0f);
                bool near = Mathf.Abs(to.x - aim.x) < 1.2f * u;
                var from = new Vector3(to.x - 2.5f * u, cam.yMax + 1f, 0f);
                Run(MeteorDrop(from, to, Rnd(0.8f, 1.3f) * u, 0.3f, u, false, near));
                yield return BattleTime.FxWait(0.06f);
            }
            yield return BattleTime.FxWait(0.2f);
            GameAudio.Se("select", 0.9f);
            yield return MeteorDrop(new Vector3(aim.x - 3f * u, cam.yMax + 2f, 0f), aim, 3.2f * u, 0.4f, u, true, true);
            ScreenFlash(GDragonPink, 0.33f, 0.02f, 0.05f, 0.4f);
            yield return BattleTime.FxWait(0.65f);
            yield return DimTo(0f, 0.35f);
        }

        /// <summary>신비의 허리케인 (일피드란): 분홍빛 회오리 둘을 일으켜, 서로 엇갈려 휘돌며 상대를 쫓아가 감아올린다</summary>
        IEnumerator MysticHurricaneFx(PalView user, Vector3 aim)
        {
            float u = U;
            var ug = new Vector3(user.Center.x, user.Feet.y, 0f);
            var g = GroundAt(aim, u);
            GameAudio.Se("shine", 0.7f);
            var tw = new[] { Ast(Pw + "dr_twister", ug, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0.03f)), Ast(Pw + "dr_twister", ug, 0f, Al(Color.white, Am), 6, 0f, new Vector2(0.5f, 0.03f)) };
            for (float t = 0f; t < 0.9f; t += BattleTime.FxDt)
            {
                float k = Mathf.Clamp01(t / 0.6f);
                for (int i = 0; i < 2; i++)
                {
                    var side = (i == 0 ? 1f : -1f) * Mathf.Sin(k * Mathf.PI * 1.5f) * 1.2f * u;
                    tw[i].transform.position = Vector3.Lerp(ug, g, Out(k)) + new Vector3(0f, side * 0.35f, 0f) + Vector3.right * side * 0.5f;
                    SizeWH(tw[i], 1.1f * u, 2.6f * u * Out(Mathf.Min(1f, t / 0.25f)));
                    tw[i].flipX = Mathf.Repeat(t + i * 0.04f, 0.08f) < 0.04f;
                    if (Random.value < 0.4f) Mote(tw[i].transform.position + Vector3.up * Rnd(0.3f, 2.4f) * u, tw[i].transform.position + Vector3.up * 2.6f * u, 0.2f * u, Color.white, 0.3f, 0f, Pw + "dr_scale", 7);
                }
                if (k >= 1f && !astHit) { GameAudio.Se("hit_strong", 0.8f); AstHit(0.14f); Run(Squash(astTarget, 0.2f, 0.3f)); }
                yield return null;
            }
            foreach (var x in tw) Anim(x, 0.25f, (s, kk) => { SizeWH(s, 1.1f * u * (1f + kk), 2.6f * u); s.color = Al(Color.white, Am * (1f - kk)); });
            DragonBurst(aim, 2f * u, u);
            yield return BattleTime.FxWait(0.4f);
        }

        /// <summary>로켓 태클 (칠렛): 몸을 웅크려 용의 기운을 모았다가, 뒤로 폭발을 일으키며 로켓처럼 튀어나가 들이받는다</summary>
        IEnumerator RocketTackleFx(PalView user, Vector3 aim)
        {
            float u = U;
            var c = user.Center;
            var aura = Ast(Pw + "dr_aura", new Vector3(c.x, user.Feet.y, 0f), 0f, Al(Color.white, Am), Behind(user), 0f, new Vector2(0.5f, 0f));
            GameAudio.Se("stat_up", 0.6f);
            Run(Squash(user, 0.15f, 0.35f));
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { SizeWH(aura, user.Height * 1.2f, user.Height * 1.3f * Out(t / 0.35f)); aura.flipX = Mathf.Repeat(t, 0.08f) < 0.04f; yield return null; }
            Destroy(aura.gameObject);
            var back = c - (aim - c).normalized * 0.6f * u;
            GameAudio.Se("hit", 0.8f);
            DragonBurst(back, 1.6f * u, u);
            bool going = true;
            Run(AfterimageTrail(user, () => going));
            Run(ScaleTrail(user, () => going));
            SpeedLines(aim, 5.5f * u, 0.4f, GDragon);
            yield return user.Lunge(aim, 99f, 0.26f, () =>
            {
                GameAudio.Se("hit_strong");
                DragonBurst(aim, 2.2f * u, u);
                Run(Squash(astTarget, 0.22f));
                AstHit(0.14f);
            });
            going = false;
            yield return BattleTime.FxWait(0.4f);
        }

        IEnumerator ScaleTrail(PalView user, System.Func<bool> keep)
        {
            while (keep())
            {
                var p = user.Center;
                var f = Ast(Pw + "dr_orb", p, 0.7f * U, Al(Color.white, 0.6f), 3);
                Anim(f, 0.25f, (s, k) => { Size(s, 0.7f * U * (1f - k)); s.color = Al(Color.white, 0.6f * (1f - k)); });
                Mote(p, p + (Vector3)(Random.insideUnitCircle * 0.6f * U), 0.2f * U, Color.white, 0.3f, 0f, Pw + "dr_scale", 3);
                yield return BattleTime.FxWait(0.025f);
            }
        }

        /// <summary>유성 광선 (제트래곤): 하늘 높이 기운을 끌어올리고, 양 날개에서 수많은 광선을 쏟아 맞은 곳마다 폭발시킨다</summary>
        IEnumerator MeteorBeamFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.75f, 0.35f));
            var c = user.Center;
            var aura = Ast(Pw + "dr_aura", new Vector3(c.x, user.Feet.y, 0f), 0f, Al(Color.white, Am), Behind(user), 0f, new Vector2(0.5f, 0f));
            GameAudio.Se("stat_up", 0.8f);
            for (float t = 0f; t < 0.55f; t += BattleTime.FxDt)
            {
                SizeWH(aura, user.Height * 1.5f, user.Height * 1.9f * Out(t / 0.55f)); aura.flipX = Mathf.Repeat(t, 0.08f) < 0.04f;
                if (Random.value < 0.6f) Mote(c + (Vector3)(Random.insideUnitCircle * user.Height * 0.6f), c + Vector3.up * user.Height, 0.22f * u, Color.white, 0.4f, 0f, Pw + "dr_scale", 7);
                yield return null;
            }
            var wingL = c + new Vector3(-0.9f, 0.4f, 0f) * u; var wingR = c + new Vector3(0.9f, 0.4f, 0f) * u;
            for (int i = 0; i < 14; i++)
            {
                bool last = i == 13;
                var src = i % 2 == 0 ? wingL : wingR;
                var to = last ? aim : aim + (Vector3)(Random.insideUnitCircle * 1.2f * u);
                var d = to - src;
                var beam = Ast(Pw + "dr_beam", src, 0f, Al(Color.white, Am), 8, Ang(d), new Vector2(0f, 0.5f));
                float len = d.magnitude, w = (last ? 1.2f : 0.35f) * u;
                Anim(beam, 0.25f, (s, k) => SizeWH(s, len * Out(k * 4f), w * (1f - k)));
                GameAudio.Se(last ? "hit_strong" : "hit", 0.4f);
                Run(DelayThen(0.06f, Do(() =>
                {
                    DragonBurst(to, (last ? 3.6f : 1f) * u, u);
                    Run(Squash(astTarget, last ? 0.3f : 0.05f));
                    if (last) { ScreenFlash(GDragonPink, 0.3f, 0.02f, 0.05f, 0.35f); Run(CameraShake(0.28f, 0.45f)); AstHit(0.26f); }
                    else if (!astHit) AstHit(0.04f);
                })));
                yield return BattleTime.FxWait(last ? 0.1f : 0.05f);
            }
            Anim(aura, 0.3f, (s, k) => s.color = Al(Color.white, Am * (1f - k)));
            yield return BattleTime.FxWait(0.6f);
            yield return DimTo(0f, 0.35f);
        }
    }
}
