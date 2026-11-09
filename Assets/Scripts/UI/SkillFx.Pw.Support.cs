using System.Collections;
using UnityEngine;

namespace PalRogue
{
    /// <summary>속성별 변화기(능력치·회복) 연출 — 속성마다 그 속성 그림으로 따로 짰다</summary>
    public partial class SkillFx
    {
        static partial void RegSupport()
        {
            PwFx["Focus_Up"] = (fx, u, t, a) => fx.FocusUpFx(u, a);
            PwFx["Burning_Spirit"] = (fx, u, t, a) => fx.BurningSpiritFx(u, a);
            PwFx["Aqua_Veil"] = (fx, u, t, a) => fx.AquaVeilFx(u, a);
            PwFx["Photosynthesis"] = (fx, u, t, a) => fx.PhotosynthesisFx(u, a);
            PwFx["Charge_Up"] = (fx, u, t, a) => fx.ChargeUpFx(u, a);
            PwFx["Rock_Armor"] = (fx, u, t, a) => fx.RockArmorFx(u, a);
            PwFx["Frost_Mist"] = (fx, u, t, a) => fx.FrostMistFx(u, a);
            PwFx["Dragon_Dance"] = (fx, u, t, a) => fx.DragonDanceFx(u, a);
            PwFx["Fear_Gaze"] = (fx, u, t, a) => fx.FearGazeFx(u, a);
        }

        Vector3 FeetOf(PalView v) => new Vector3(v.Center.x, v.Feet.y, 0f);

        /// <summary>기합 모으기: 집중선이 몸으로 빨려들며 기를 모으고, 마지막에 하얀 기합이 펑 터진다</summary>
        IEnumerator FocusUpFx(PalView user, Vector3 aim)
        {
            float u = U;
            var c = user.Center;
            GameAudio.Se("stat_up", 0.7f);
            SpeedLines(c, 5f * u, 0.7f, Color.white, 0.8f);
            for (float t = 0f; t < 0.6f; t += BattleTime.FxDt)
            {
                if (Random.value < 0.7f) Mote(c + Dir(Rnd(0, 360)) * 2.2f * u, c, 0.18f * u, Color.white, 0.25f, 0f, Pw + "n_mote", 6);
                yield return null;
            }
            Run(Squash(user, -0.1f, 0.2f));
            var a = Ast(Pw + "n_aura", c, 0f, Al(Color.white, 1f), 7);
            Anim(a, 0.4f, (s, k) => { Size(s, user.Height * 1.6f * Out(k * 2f)); s.color = Al(Color.white, 1f - k); });
            Impact(c + Vector3.up * user.Height * 0.3f, 1.6f * u, Color.white, 0.25f);
            Run(user.Flash(Color.white, 2, 0.06f));
            yield return BattleTime.FxWait(0.35f);
        }

        /// <summary>투지 연소: 발밑에서 불기둥이 몸을 감싸 치솟고, 불꽃 고리가 터지며 눈빛이 이글거린다</summary>
        IEnumerator BurningSpiritFx(PalView user, Vector3 aim)
        {
            float u = U;
            var f = FeetOf(user);
            GameAudio.Se("stat_up", 0.8f);
            var back = Ast(Pw + "f_flame", f, 0f, Color.white, Behind(user), 0f, new Vector2(0.5f, 0.02f));
            var front = Ast(Pw + "f_flame", f, 0f, Al(Color.white, 0.7f), 4, 0f, new Vector2(0.5f, 0.02f));
            for (float t = 0f; t < 0.8f; t += BattleTime.FxDt)
            {
                float k = Out(t / 0.25f), fade = t > 0.6f ? 1f - (t - 0.6f) / 0.2f : 1f;
                SizeWH(back, user.Height * 1.1f * (1f + 0.1f * Mathf.Sin(t * 40f)), user.Height * 1.7f * k * fade);
                SizeWH(front, user.Height * 0.8f, user.Height * 0.9f * k * fade);
                back.flipX = Mathf.Repeat(t, 0.1f) < 0.05f;
                if (Random.value < 0.6f) Mote(f + Vector3.right * Rnd(-0.6f, 0.6f) * u, f + new Vector3(Rnd(-0.6f, 0.6f), user.Height / u * 1.3f, 0f) * u, 0.2f * u, Color.white, 0.5f, 0f, Pw + "f_ember", 6);
                yield return null;
            }
            Destroy(back.gameObject); Destroy(front.gameObject);
            Ring(user.Center, 0.4f * u, 3f * u, Color.white, 0.4f, Pw + "f_ring");
            Run(user.Flash(C(0xff7a2a), 2, 0.07f));
            yield return BattleTime.FxWait(0.3f);
        }

        /// <summary>물의 베일: 몸 둘레로 물방울이 모여 투명한 물의 막을 이루고, 출렁이며 상처를 씻어낸다</summary>
        IEnumerator AquaVeilFx(PalView user, Vector3 aim)
        {
            float u = U;
            var c = user.Center;
            GameAudio.Se("shine", 0.7f);
            for (int i = 0; i < 12; i++) Mote(c + Dir(i * 30f) * 2f * u, c + Dir(i * 30f) * user.Height * 0.55f, 0.18f * u, Color.white, 0.35f, i * 0.02f, Pw + "w_droplet", 6);
            yield return BattleTime.FxWait(0.35f);
            var veil = Ast(Pw + "w_bubble", c, 0f, Al(Color.white, 0.9f), 7);
            for (float t = 0f; t < 0.9f; t += BattleTime.FxDt)
            {
                float k = BackOut(Mathf.Min(1f, t / 0.3f)), wob = 0.05f * Mathf.Sin(t * 14f);
                SizeWH(veil, user.Height * 1.25f * k * (1f + wob), user.Height * 1.25f * k * (1f - wob));
                veil.color = Al(Color.white, 0.9f * (t > 0.7f ? (0.9f - t) / 0.2f : 1f));
                if (Random.value < 0.3f) Mote(c + (Vector3)(Random.insideUnitCircle * user.Height * 0.4f), c + Vector3.up * user.Height * 0.6f, 0.15f * u, Color.white, 0.4f, 0f, Pw + "w_bubble", 8);
                yield return null;
            }
            Destroy(veil.gameObject);
            Ring(c, 0.5f * u, 2.6f * u, Al(Color.white, 1f), 0.4f, Pw + "w_ripple");
            Run(user.Flash(C(0x8fe3ff), 2, 0.08f));
            yield return BattleTime.FxWait(0.25f);
        }

        /// <summary>광합성: 머리 위로 햇살이 쏟아져 내려 몸을 비추고, 연둣빛 생기가 몽글몽글 피어오른다</summary>
        IEnumerator PhotosynthesisFx(PalView user, Vector3 aim)
        {
            float u = U;
            var c = user.Center;
            var cam = CamRect();
            var sunP = new Vector3(c.x, Mathf.Min(c.y + user.Height * 1.1f, cam.yMax - 0.8f * u), 0f);
            var sun = Ast(Pw + "g_sun", sunP, 0f, Color.white, 6);
            GameAudio.Se("shine", 0.8f);
            for (float t = 0f; t < 0.35f; t += BattleTime.FxDt) { Size(sun, 1.6f * u * Out(t / 0.35f)); sun.transform.Rotate(0, 0, 60f * BattleTime.FxDt); yield return null; }
            var ray = Ast(Pw + "g_beam", sunP, 0f, Al(Color.white, 0.8f), 5, -90f, new Vector2(0f, 0.5f));
            float len = sunP.y - user.Feet.y;
            for (float t = 0f; t < 0.9f; t += BattleTime.FxDt)
            {
                float f = t > 0.7f ? 1f - (t - 0.7f) / 0.2f : 1f;
                SizeWH(ray, len * Out(t / 0.15f), user.Height * 1.1f * f * (1f + 0.05f * Mathf.Sin(t * 20f)));
                sun.transform.Rotate(0, 0, 60f * BattleTime.FxDt);
                if (Random.value < 0.6f) { var p = FeetOf(user) + Vector3.right * Rnd(-0.8f, 0.8f) * u; Mote(p, p + Vector3.up * user.Height * Rnd(0.6f, 1.2f), 0.2f * u, Color.white, 0.6f, 0f, Pw + "g_mote", 7); }
                yield return null;
            }
            Destroy(ray.gameObject);
            Anim(sun, 0.3f, (s, k) => s.color = Al(Color.white, 1f - k));
            Run(user.Flash(C(0xd8ff8a), 2, 0.08f));
            yield return BattleTime.FxWait(0.25f);
        }

        /// <summary>전기 충전: 사방에서 번개가 몸으로 빨려 들어와 지지직 쌓이고, 털이 곤두서듯 전기가 튄다</summary>
        IEnumerator ChargeUpFx(PalView user, Vector3 aim)
        {
            float u = U;
            var c = user.Center;
            GameAudio.Se("stat_up", 0.7f);
            var ring = Ast(Pw + "e_ring", c, 0f, Color.white, 6);
            for (float t = 0f; t < 0.8f; t += BattleTime.FxDt)
            {
                Size(ring, user.Height * 1.3f * (1f - 0.3f * t)); ring.transform.Rotate(0, 0, 400f * BattleTime.FxDt); ring.flipY = Random.value < 0.5f;
                if (Random.value < 0.4f) BoltLine(c + Dir(Rnd(0, 360)) * 2f * u, c, 0.3f * u, 0.07f);
                if (Random.value < 0.3f) { var sp = Ast(Pw + "e_spark", c + (Vector3)(Random.insideUnitCircle * user.Height * 0.4f), 0.5f * u, Color.white, 7, Rnd(0, 360)); Anim(sp, 0.1f, (s, k) => s.color = Al(Color.white, 1f - k)); }
                yield return null;
            }
            Destroy(ring.gameObject);
            Run(user.Flash(C(0xffe94a), 3, 0.05f));
            SpeedLines(c, 4f * u, 0.3f, C(0xffe94a));
            yield return BattleTime.FxWait(0.3f);
        }

        /// <summary>바위 갑옷: 땅에서 돌조각들이 떠올라 몸 둘레를 돌다가, 하나씩 몸에 착착 달라붙어 단단한 갑옷이 된다</summary>
        IEnumerator RockArmorFx(PalView user, Vector3 aim)
        {
            float u = U;
            var c = user.Center;
            var f = FeetOf(user);
            GameAudio.Se("stat_down", 0.5f);
            Dust(f, u, 6);
            const int n = 8;
            var rs = new SpriteRenderer[n];
            for (int i = 0; i < n; i++) rs[i] = Ast(Pw + "r_rock", f + Vector3.right * Rnd(-1f, 1f) * u, Rnd(0.35f, 0.5f) * u, Color.white, 6, Rnd(0, 360));
            for (float t = 0f; t < 0.7f; t += BattleTime.FxDt)
            {
                for (int i = 0; i < n; i++)
                {
                    float an = t * 7f + i * Mathf.PI * 2f / n, rad = user.Height * 0.6f;
                    var orbit = c + new Vector3(Mathf.Cos(an) * rad, Mathf.Sin(an) * rad * 0.4f, 0f);
                    rs[i].transform.position = Vector3.Lerp(rs[i].transform.position, orbit, 0.2f);
                    rs[i].sortingOrder = Mathf.Sin(an) < 0 ? sortingOrder + 6 : user.SortingOrder - 1;
                }
                yield return null;
            }
            for (int i = 0; i < n; i++)
            {
                var target = c + (Vector3)(Random.insideUnitCircle * user.Height * 0.3f);
                Run(Throw(rs[i], rs[i].transform.position, target, 0.1f, 0f, 300f));
                GameAudio.Se("hit", 0.3f);
                yield return BattleTime.FxWait(0.04f);
            }
            Ring(f, 0.4f * u, 2.6f * u, Color.white, 0.4f, Pw + "r_quake", 0.35f);
            Run(user.Flash(C(0xc89a5a), 2, 0.08f));
            Run(CameraShake(0.06f, 0.15f));
            yield return BattleTime.FxWait(0.3f);
        }

        /// <summary>서리 안개: 차가운 안개를 상대 쪽으로 불어 보내, 눈송이가 휘감아 돌며 몸을 굳게 만든다</summary>
        IEnumerator FrostMistFx(PalView user, Vector3 aim)
        {
            float u = U;
            Vector3 from = user.Center;
            GameAudio.Se("shine", 0.6f);
            for (int i = 0; i < 6; i++)
            {
                var m = Ast(Pw + "i_mist", from, 0f, Al(Color.white, 0f), 6 + i % 2, Rnd(0, 360));
                var to = aim + (Vector3)(Random.insideUnitCircle * 0.7f * u);
                Anim(m, 1.2f, (s, k) => { s.transform.position = Vector3.Lerp(from, to, Out(k * 1.6f)); Size(s, 2.2f * u * Out(k * 1.6f)); s.color = Al(Color.white, 0.8f * Am * Bell(k)); });
                yield return BattleTime.FxWait(0.05f);
            }
            for (float t = 0f; t < 0.7f; t += BattleTime.FxDt)
            {
                float an = t * 10f;
                for (int j = 0; j < 2; j++) { var p = aim + new Vector3(Mathf.Cos(an + j * 3.14f), Mathf.Sin(an + j * 3.14f) * 0.5f, 0f) * 1f * u; Mote(p, p + Vector3.down * 0.2f * u, 0.22f * u, Color.white, 0.25f, 0f, Pw + "i_snow", 8); }
                if (t > 0.2f && !astHit) { GameAudio.Se("stat_down", 0.6f); if (!astMissed) Run(astTarget.Flash(C(0x7cc8ff), 3, 0.08f)); AstHit(0f); }
                yield return null;
            }
            yield return BattleTime.FxWait(0.2f);
        }

        /// <summary>용의 춤: 용의 기운을 두르고 몸을 들썩이며 춤추면, 비늘 고리가 빙글빙글 돌며 기운이 차오른다</summary>
        IEnumerator DragonDanceFx(PalView user, Vector3 aim)
        {
            float u = U;
            var tr = user.transform; var home = tr.position;
            var aura = Ast(Pw + "dr_aura", FeetOf(user), 0f, Color.white, Behind(user), 0f, new Vector2(0.5f, 0f));
            GameAudio.Se("stat_up", 0.8f);
            const int n = 8;
            var sc = new SpriteRenderer[n];
            for (int i = 0; i < n; i++) sc[i] = Ast(Pw + "dr_scale", user.Center, 0.35f * u, Color.white, 7);
            for (float t = 0f; t < 1f; t += BattleTime.FxDt)
            {
                tr.position = home + Vector3.up * Mathf.Abs(Mathf.Sin(t * 12f)) * 0.18f * u + Vector3.right * Mathf.Sin(t * 6f) * 0.08f * u;
                SizeWH(aura, user.Height * 1.3f, user.Height * 1.6f * Out(t / 0.3f) * (t > 0.8f ? (1f - t) / 0.2f : 1f));
                aura.flipX = Mathf.Repeat(t, 0.1f) < 0.05f;
                for (int i = 0; i < n; i++)
                {
                    float an = t * 9f + i * Mathf.PI * 2f / n;
                    sc[i].transform.position = user.Center + new Vector3(Mathf.Cos(an) * 0.75f, Mathf.Sin(an) * 0.3f + (t - 0.5f) * 0.6f, 0f) * user.Height;
                    sc[i].sortingOrder = Mathf.Sin(an) < 0 ? sortingOrder + 7 : user.SortingOrder - 1;
                }
                yield return null;
            }
            tr.position = home;
            Destroy(aura.gameObject);
            foreach (var s in sc) Destroy(s.gameObject);
            Ring(user.Center, 0.5f * u, 2.8f * u, Color.white, 0.4f, Pw + "dr_shock");
            Run(user.Flash(C(0xff8ae0), 2, 0.07f));
            yield return BattleTime.FxWait(0.25f);
        }

        /// <summary>공포의 눈빛: 주위가 캄캄해지고 등 뒤에 어둠의 마법진이 떠오르며, 날카로운 눈빛이 상대를 꿰뚫어 움츠러들게 한다</summary>
        IEnumerator FearGazeFx(PalView user, Vector3 aim)
        {
            float u = U;
            Run(DimTo(0.7f, 0.25f));
            var rune = Ast(Pw + "d_rune", user.Center, 0f, Color.white, Behind(user));
            GameAudio.Se("stat_down", 0.7f);
            for (float t = 0f; t < 0.4f; t += BattleTime.FxDt) { Size(rune, user.Height * 1.6f * Out(t / 0.4f)); rune.transform.Rotate(0, 0, -60f * BattleTime.FxDt); yield return null; }
            var eye = user.Center + Vector3.up * user.Height * 0.25f;
            var d = aim - eye;
            var gaze = Ast(Pw + "d_beam", eye, 0f, Al(C(0xff4a7a), 1f), 8, Ang(d), new Vector2(0f, 0.5f));
            Anim(gaze, 0.35f, (s, k) => SizeWH(s, d.magnitude * Out(k * 4f), 0.25f * u * (1f - k)));
            var glint = Ast(Pw + "d_bloomray", eye, 0f, Color.white, 9);
            Anim(glint, 0.3f, (s, k) => { Size(s, 1f * u * Bell(k)); s.transform.Rotate(0, 0, 300f * BattleTime.FxDt); });
            yield return BattleTime.FxWait(0.15f);
            if (!astMissed)
            {
                var tr = astTarget.transform; var home = tr.position;
                Run(astTarget.Flash(C(0xa04dff), 3, 0.08f));
                for (float t = 0f; t < 0.4f; t += BattleTime.FxDt) { tr.position = home + Vector3.right * Mathf.Sin(t * 70f) * 0.05f * u; yield return null; }
                tr.position = home;
            }
            AstHit(0f);
            Anim(rune, 0.3f, (s, k) => s.color = Al(Color.white, 1f - k));
            yield return BattleTime.FxWait(0.2f);
            yield return DimTo(0f, 0.25f);
        }
    }
}
