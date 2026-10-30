using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    // ═════════════════ 저장 파일 내용 ═════════════════

    [Serializable]
    public class SkillSave
    {
        public string skill;
        public int pp, bonusPP, ppUps;
    }

    /// <summary>팰에 붙은 효과 하나. kind: perk(강화 효과) / awaken(각성) / held(지닌 아이템)</summary>
    [Serializable]
    public class EffectSave
    {
        public string kind, id;
        public int rank;
        public bool flag;
    }

    [Serializable]
    public class PalSave
    {
        public string pal;
        public int level, exp, hp, status, stars, sphere;
        public int[] ivs;
        public int metWave, metLevel;
        public string metArea;
        public bool metAsBoss;
        public List<SkillSave> skills = new List<SkillSave>();
        public List<EffectSave> effects = new List<EffectSave>();
    }

    /// <summary>모험 하나의 저장 데이터 (웨이브가 시작될 때마다 자동 저장)</summary>
    [Serializable]
    public class RunSave
    {
        public int version = 1;
        public int runNumber;
        public int wave, money, trainerId;
        public int[] spheres;
        public int amuletCoins, expShareCount, candyJars, berryPouches, extraRewards;
        public int caughtCount, bossesDefeated;
        public float expBonus;
        public double playSeconds;
        public long savedAtTicks;
        public List<PalSave> party = new List<PalSave>();

        public DateTime SavedAt => new DateTime(savedAtTicks);
        public string AreaName => GameConfig.AreaName(Mathf.Min(Mathf.Max(1, wave), GameConfig.FinalWave));

        /// <summary>2026. 10. 24. 오후 7:48:51</summary>
        public string SavedAtText
        {
            get
            {
                var t = SavedAt;
                string ampm = t.Hour < 12 ? "오전" : "오후";
                int h = t.Hour % 12 == 0 ? 12 : t.Hour % 12;
                return $"{t.Year}. {t.Month}. {t.Day}. {ampm} {h}:{t.Minute:00}:{t.Second:00}";
            }
        }

        /// <summary>플레이 시간 일:시:분:초 (00:00:06:56)</summary>
        public string PlayTimeText
        {
            get
            {
                var s = TimeSpan.FromSeconds(Math.Max(0, playSeconds));
                return $"{s.Days:00}:{s.Hours:00}:{s.Minutes:00}:{s.Seconds:00}";
            }
        }
    }

    // ═════════════════ 저장 슬롯 ═════════════════

    /// <summary>
    /// 모험 저장 슬롯 (포켓로그의 세션 슬롯). 파일: persistentDataPath/slot{번호}.json
    /// 웨이브가 시작될 때마다 그 슬롯에 자동 저장되고, 모험이 끝나면(게임 오버·클리어) 지워진다.
    /// </summary>
    public static class SaveSlots
    {
        public const int Count = 5;

        static string PathOf(int slot) => Path.Combine(Application.persistentDataPath, $"slot{slot + 1}.json");

        public static bool Exists(int slot) => slot >= 0 && File.Exists(PathOf(slot));
        public static bool AnyExists => Enumerable.Range(0, Count).Any(Exists);

        public static RunSave Load(int slot)
        {
            if (!Exists(slot)) return null;
            try { return JsonUtility.FromJson<RunSave>(File.ReadAllText(PathOf(slot))); }
            catch (Exception e) { Debug.LogWarning($"[저장] 슬롯 {slot + 1}을 읽지 못했습니다: {e.Message}"); return null; }
        }

        public static void Save(int slot, RunSave data)
        {
            if (slot < 0 || data == null) return;
            try { File.WriteAllText(PathOf(slot), JsonUtility.ToJson(data)); }
            catch (Exception e) { Debug.LogWarning($"[저장] 슬롯 {slot + 1}에 저장하지 못했습니다: {e.Message}"); }
        }

        public static void Delete(int slot)
        {
            try { if (Exists(slot)) File.Delete(PathOf(slot)); }
            catch (Exception e) { Debug.LogWarning($"[저장] 슬롯 {slot + 1}을 지우지 못했습니다: {e.Message}"); }
        }

        // ───── 런 → 저장 데이터 ─────
        public static RunSave Capture(Run run, double playSeconds, int runNumber)
        {
            var s = new RunSave
            {
                runNumber = runNumber,
                wave = run.Wave, money = run.Money, trainerId = run.TrainerId,
                spheres = run.Spheres.ToArray(),
                amuletCoins = run.AmuletCoins, expShareCount = run.ExpShareCount, candyJars = run.CandyJars,
                berryPouches = run.BerryPouches, extraRewards = run.ExtraRewards, expBonus = run.ExpBonus,
                caughtCount = run.CaughtCount, bossesDefeated = run.BossesDefeated,
                playSeconds = playSeconds,
                savedAtTicks = DateTime.Now.Ticks,
            };
            foreach (var p in run.Party) s.party.Add(CapturePal(p));
            return s;
        }

        static PalSave CapturePal(Pal p)
        {
            var s = new PalSave
            {
                pal = p.Data.name, level = p.Level, exp = p.Exp, hp = p.CurrentHP, status = (int)p.Status,
                stars = p.Stars, sphere = (int)p.CaughtSphere, ivs = p.IVs.ToArray(),
                metWave = p.MetWave, metLevel = p.MetLevel, metArea = p.MetArea, metAsBoss = p.MetAsBoss,
            };
            foreach (var sk in p.Skills)
                s.skills.Add(new SkillSave { skill = sk.Data.name, pp = sk.PP, bonusPP = sk.BonusPP, ppUps = sk.PpUps });
            foreach (var e in p.Effects)
            {
                if (e is Awakening a) s.effects.Add(new EffectSave { kind = "awaken", id = a.Def.Id, flag = a.IsTrue });
                else if (e.HeldItemId != null) s.effects.Add(new EffectSave { kind = "held", id = e.HeldItemId });
                else if (e.PerkId != null && PerkCatalog.Get(e.PerkId) != null) s.effects.Add(new EffectSave { kind = "perk", id = e.PerkId, rank = e.PerkRank });
                // 속성 시너지는 웨이브마다 다시 계산되고, 랭크업 같은 기간 효과는 저장하지 않는다
            }
            return s;
        }

        // ───── 저장 데이터 → 런 ─────
        /// <summary>저장 데이터로 런을 되살린다. 팰·기술 에셋은 이름으로 찾는다 (못 찾은 것은 건너뛴다)</summary>
        public static Run Restore(RunSave s, IEnumerable<PalData> pals, IEnumerable<SkillData> skills)
        {
            var palMap = pals.Where(p => p != null).GroupBy(p => p.name).ToDictionary(g => g.Key, g => g.First());
            var skillMap = skills.Where(k => k != null).GroupBy(k => k.name).ToDictionary(g => g.Key, g => g.First());

            var party = new List<Pal>();
            var restoreLater = new List<(Pal pal, PalSave save)>();
            foreach (var ps in s.party)
            {
                if (!palMap.TryGetValue(ps.pal, out var data)) { Debug.LogWarning($"[저장] 팰을 찾지 못했습니다: {ps.pal}"); continue; }
                var pal = new Pal(data, ps.level);
                party.Add(pal);
                restoreLater.Add((pal, ps));
            }

            var run = new Run(party, s.spheres);
            run.RestoreProgress(s.wave, s.trainerId);
            run.Money = s.money;
            run.AmuletCoins = s.amuletCoins; run.ExpShareCount = s.expShareCount; run.CandyJars = s.candyJars;
            run.BerryPouches = s.berryPouches; run.ExtraRewards = s.extraRewards; run.ExpBonus = s.expBonus;
            run.CaughtCount = s.caughtCount; run.BossesDefeated = s.bossesDefeated;

            foreach (var (pal, ps) in restoreLater)
            {
                pal.Skills.Clear();
                foreach (var sk in ps.skills)
                {
                    if (!skillMap.TryGetValue(sk.skill, out var data)) { Debug.LogWarning($"[저장] 기술을 찾지 못했습니다: {sk.skill}"); continue; }
                    pal.Skills.Add(new SkillInstance(data) { BonusPP = sk.bonusPP, PpUps = sk.ppUps, PP = sk.pp });
                }
                if (pal.Skills.Count == 0) pal.AutoLearnSkills();

                foreach (var e in ps.effects)
                {
                    switch (e.kind)
                    {
                        case "perk":
                            var perk = PerkCatalog.Get(e.id);
                            if (perk != null) pal.AddEffect(perk.Create(Mathf.Max(1, e.rank)));
                            break;
                        case "awaken":
                            var def = AwakeningCatalog.Get(e.id);
                            if (def != null) pal.AddEffect(new Awakening(def, e.flag));
                            break;
                        case "held":
                            var item = ItemCatalog.All.FirstOrDefault(d => d.IsHeld && d.Name == e.id);
                            if (item != null) item.Apply(new ItemContext { Run = run, Target = pal });
                            else Debug.LogWarning($"[저장] 지닌 아이템을 찾지 못했습니다: {e.id}");
                            break;
                    }
                }
                pal.RestoreState(ps.level, ps.exp, ps.hp, (StatusCondition)ps.status, ps.ivs, ps.stars);
                pal.CaughtSphere = (SphereType)ps.sphere;
                pal.MetWave = ps.metWave; pal.MetLevel = ps.metLevel; pal.MetArea = ps.metArea; pal.MetAsBoss = ps.metAsBoss;
            }
            return run;
        }
    }
}
