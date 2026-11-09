using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 팰월드 기술 전용 연출 (기술마다 따로 그린 연출). 그림은 Resources/UI/PwFx (속성 머리글자: n_ 무, f_ 화염 …).
    /// 기존 연출 시트는 쓰지 않는다. 속성별 파일(SkillFx.Pw.속성.cs)이 Reg속성()에서 표에 연출을 등록한다.
    /// 아직 등록되지 않은 기술은 예전 표(BySkill)·속성 기본 연출을 쓴다.
    /// 맞는 순간·어둡게·번쩍임·흔들림은 제로버스 연출과 같은 틀(SkillFx.Astralym.cs의 AstHit·DimTo·ScreenFlash)을 쓴다.
    /// </summary>
    public partial class SkillFx
    {
        delegate IEnumerator PwRoutine(SkillFx fx, PalView user, PalView target, Vector3 aim);

        static readonly Dictionary<string, PwRoutine> PwFx = new Dictionary<string, PwRoutine>();

        static SkillFx()
        {
            RegNeutral(); RegFire(); RegWater(); RegGrass(); RegElectric(); RegIce(); RegGround(); RegDark(); RegDragon(); RegSupport();
        }

        static partial void RegNeutral();
        static partial void RegFire();
        static partial void RegWater();
        static partial void RegGrass();
        static partial void RegElectric();
        static partial void RegIce();
        static partial void RegGround();
        static partial void RegDark();
        static partial void RegDragon();
        static partial void RegSupport();

        /// <summary>전용 연출이 등록된 팰월드 기술 이름들 (미리보기용)</summary>
        public static IEnumerable<string> PwSkillNames => PwFx.Keys;

        const string Pw = "PwFx/";

        /// <summary>빗나가면 옅게</summary>
        float Am => astMissed ? 0.45f : 1f;

        /// <summary>그림 하나를 from → to로 날린다 (arc: 포물선 높이, spin: 초당 회전, face: 날아가는 쪽을 바라봄). 도착하면 지운다</summary>
        IEnumerator Throw(SpriteRenderer sr, Vector3 from, Vector3 to, float seconds, float arc = 0f, float spin = 0f, bool face = false, Action<SpriteRenderer, float> each = null)
        {
            Vector3 prev = from;
            for (float t = 0f; t < seconds && sr != null; t += BattleTime.FxDt)
            {
                float k = t / seconds;
                var p = Vector3.Lerp(from, to, k) + Vector3.up * arc * 4f * k * (1f - k);
                sr.transform.position = p;
                if (face && (p - prev).sqrMagnitude > 1e-6f) sr.transform.rotation = Quaternion.Euler(0, 0, Ang(p - prev));
                else if (spin != 0f) sr.transform.Rotate(0, 0, spin * BattleTime.FxDt);
                each?.Invoke(sr, k);
                prev = p;
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        // ───── 타격감 도구 (기술마다 조합을 다르게 쓴다) ─────

        /// <summary>만화풍 충격 별이 팍 터졌다 사라진다</summary>
        void Impact(Vector3 at, float size, Color col, float life = 0.22f)
        {
            var sr = Ast(Pw + "n_impact", at, 0f, Al(col, Am), 7, Rnd(0f, 360f));
            Anim(sr, life, (s, k) => { Size(s, size * (k < 0.2f ? Out(k / 0.2f) * 1.1f : Mathf.Lerp(1.1f, 1.25f, k))); s.color = Al(col, Am * (k < 0.5f ? 1f : (1f - k) * 2f)); });
            var core = Ast("ast_dot", at, 0f, Al(Color.white, Am), 8);
            Anim(core, life * 0.8f, (s, k) => { Size(s, size * 0.9f * Bell(k)); });
        }

        /// <summary>집중선: 중심으로 모이며 빠르게 돈다 (돌진·연타의 속도감)</summary>
        void SpeedLines(Vector3 at, float size, float life, Color col, float alpha = 0.7f)
        {
            var sr = Ast(Pw + "n_speed", at, size, Al(col, 0f), -4, Rnd(0f, 360f));
            Anim(sr, life, (s, k) => { Size(s, size * Mathf.Lerp(1.3f, 0.9f, k)); s.transform.Rotate(0, 0, 40f * BattleTime.FxDt); s.color = Al(col, alpha * Am * Bell(k)); });
        }

        /// <summary>발밑에서 흙먼지가 양옆으로 퍼진다</summary>
        void Dust(Vector3 at, float u, int n = 6, float spread = 1.6f)
        {
            for (int i = 0; i < n; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                var p = at + new Vector3(Rnd(-0.2f, 0.2f) * u, Rnd(-0.1f, 0.1f) * u, 0f);
                var end = p + new Vector3(side * Rnd(0.5f, 1f) * spread * u, Rnd(0f, 0.4f) * u, 0f);
                var sr = Ast(Pw + "n_dust", p, 0.5f * u, Al(Color.white, 0.85f * Am), 3, Rnd(0f, 360f));
                float grow = Rnd(1.6f, 2.4f);
                Anim(sr, Rnd(0.45f, 0.65f), (s, k) => { s.transform.position = Vector3.Lerp(p, end, Out(k)); Size(s, 0.5f * u * Mathf.Lerp(1f, grow, Out(k))); s.color = Al(Color.white, 0.85f * Am * (1f - k)); });
            }
        }

        // 찌그러짐이 겹쳐도 원래 크기를 잃지 않도록, 팰마다 처음 크기와 진행 중인 찌그러짐 수를 기억한다
        readonly Dictionary<Transform, (Vector3 baseScale, int count)> squashing = new Dictionary<Transform, (Vector3, int)>();

        /// <summary>맞는 팰이 납작하게 찌그러졌다 돌아온다 (여러 번 겹쳐도 마지막에 꼭 처음 크기로 돌아온다)</summary>
        IEnumerator Squash(PalView v, float amount = 0.18f, float seconds = 0.16f)
        {
            if (v == null || astMissed) yield break;
            var tr = v.transform;
            if (!squashing.TryGetValue(tr, out var st)) st = (tr.localScale, 0);
            squashing[tr] = (st.baseScale, st.count + 1);
            var b = st.baseScale;
            try
            {
                for (float t = 0f; t < seconds && tr != null; t += BattleTime.FxDt)
                {
                    float k = Bell(t / seconds) * amount;
                    tr.localScale = new Vector3(b.x * (1f + k), b.y * (1f - k), b.z);
                    yield return null;
                }
            }
            finally
            {
                if (tr != null && squashing.TryGetValue(tr, out var cur))
                {
                    if (cur.count <= 1) { tr.localScale = cur.baseScale; squashing.Remove(tr); }
                    else squashing[tr] = (cur.baseScale, cur.count - 1);
                }
            }
        }

        /// <summary>기술이 끝날 때: 남은 찌그러짐을 모두 원래 크기로 되돌린다 (안전장치)</summary>
        void RestoreSquash()
        {
            foreach (var kv in squashing) if (kv.Key != null) kv.Key.localScale = kv.Value.baseScale;
            squashing.Clear();
        }

        /// <summary>쏘는 팰이 반동으로 뒤로 밀렸다 돌아온다</summary>
        IEnumerator Recoil(PalView v, Vector3 dir, float dist = 0.25f, float seconds = 0.18f)
        {
            var tr = v.transform; var home = tr.position;
            for (float t = 0f; t < seconds; t += BattleTime.FxDt) { tr.position = home - dir.normalized * dist * Bell(t / seconds); yield return null; }
            tr.position = home;
        }

        /// <summary>맞은 자리에 작은 알갱이가 사방으로 튄다 (속성마다 다른 그림)</summary>
        void Spray(string tex, Vector3 at, int n, float dist, float size, Color a, Color b, float life = 0.4f)
        {
            for (int i = 0; i < n; i++)
                Mote(at, at + Dir(i * 360f / n + Rnd(-15f, 15f)) * dist * Rnd(0.6f, 1.2f), size * Rnd(0.7f, 1.3f), i % 2 == 0 ? a : b, life, 0f, tex, 5);
        }
    }
}
