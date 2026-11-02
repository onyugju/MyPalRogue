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
        public int[] stages;               // 기술로 올리고 내린 능력치 랭크 (공격·방어·스피드). 10웨이브마다·교체할 때 풀린다
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
        public string lastEnemy;           // 지난 웨이브 상대 (이어하기에서도 같은 상대가 나오게)
        public List<string> runItemIds = new List<string>();
        public List<int> runItemCounts = new List<int>();
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

        static string BackupOf(int slot) => PathOf(slot) + ".bak";

        public static bool Exists(int slot) => slot >= 0 && (File.Exists(PathOf(slot)) || File.Exists(BackupOf(slot)));
        public static bool AnyExists => Enumerable.Range(0, Count).Any(Exists);

        public static RunSave Load(int slot)
        {
            if (!Exists(slot)) return null;
            // 본 파일이 깨졌으면(저장 중 꺼짐 등) 바로 전 저장(.bak)으로 이어 한다
            var data = Read(PathOf(slot), slot) ?? Read(BackupOf(slot), slot);
            if (data == null) Debug.LogWarning($"[저장] 슬롯 {slot + 1}을 읽지 못했습니다.");
            return data;
        }

        static RunSave Read(string path, int slot)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<RunSave>(File.ReadAllText(path));
                return IsValid(data) ? data : null;
            }
            catch (Exception e) { Debug.LogWarning($"[저장] {Path.GetFileName(path)}을 읽지 못했습니다: {e.Message}"); return null; }
        }

        /// <summary>불러올 수 있는 저장인지 (파티가 있고 웨이브가 1 이상)</summary>
        static bool IsValid(RunSave d) => d != null && d.wave >= 1 && d.party != null && d.party.Count > 0;

        /// <summary>
        /// 저장한다. 임시 파일에 쓰고 → 다시 읽어서 확인한 뒤 → 바꿔 끼운다 (이전 저장은 .bak으로 남긴다).
        /// 저장 도중 게임이 꺼져도 슬롯이 깨지지 않는다. 성공하면 true.
        /// </summary>
        public static bool Save(int slot, RunSave data)
        {
            if (slot < 0 || data == null) return false;
            string path = PathOf(slot), temp = path + ".tmp", backup = BackupOf(slot);
            try
            {
                string json = JsonUtility.ToJson(data);
                File.WriteAllText(temp, json);
                var check = JsonUtility.FromJson<RunSave>(File.ReadAllText(temp));
                if (!IsValid(check) || check.wave != data.wave) throw new Exception("저장한 내용을 다시 읽어 보니 다르다");
                if (File.Exists(path)) File.Copy(path, backup, true);
                File.Copy(temp, path, true);
                File.Delete(temp);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[저장] 슬롯 {slot + 1}에 저장하지 못했습니다: {e.Message}");
                return false;
            }
        }

        public static void Delete(int slot)
        {
            try
            {
                foreach (var p in new[] { PathOf(slot), BackupOf(slot), PathOf(slot) + ".tmp" })
                    if (File.Exists(p)) File.Delete(p);
            }
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
                caughtCount = run.CaughtCount, bossesDefeated = run.BossesDefeated, lastEnemy = run.LastEnemyName,
                playSeconds = playSeconds,
                savedAtTicks = DateTime.Now.Ticks,
            };
            foreach (var p in run.Party) s.party.Add(CapturePal(p));
            foreach (var kv in run.RunItems) { s.runItemIds.Add(kv.Key); s.runItemCounts.Add(kv.Value); }
            return s;
        }

        static PalSave CapturePal(Pal p)
        {
            var s = new PalSave
            {
                pal = p.Data.name, level = p.Level, exp = p.Exp, hp = p.CurrentHP, status = (int)p.Status,
                stars = p.Stars, sphere = (int)p.CaughtSphere, ivs = p.IVs.ToArray(), stages = Battle.StageSnapshot(p),
                metWave = p.MetWave, metLevel = p.MetLevel, metArea = p.MetArea, metAsBoss = p.MetAsBoss,
            };
            foreach (var sk in p.Skills)
                s.skills.Add(new SkillSave { skill = sk.Data.name, pp = sk.PP, bonusPP = sk.BonusPP, ppUps = sk.PpUps });
            foreach (var e in p.Effects)
            {
                if (e is Awakening a) s.effects.Add(new EffectSave { kind = "awaken", id = a.Def.Id, flag = a.IsTrue });
                // 몇 웨이브짜리 효과(플러스파워·크리티컬커터·응원 호루라기): 종류와 남은 웨이브
                else if (e is TempStatBoost ts) s.effects.Add(new EffectSave { kind = "timed", id = "stat:" + ts.Stat, rank = ts.WavesLeft });
                else if (e is TempCritBoost tc) s.effects.Add(new EffectSave { kind = "timed", id = "crit", rank = tc.WavesLeft });
                else if (e is TempTypeBoost tt) s.effects.Add(new EffectSave { kind = "timed", id = "type:" + tt.Type, rank = tt.WavesLeft });
                else if (e.HeldItemId != null) s.effects.Add(new EffectSave { kind = "held", id = e.HeldItemId });
                else if (e.PerkId != null && PerkCatalog.Get(e.PerkId) != null) s.effects.Add(new EffectSave { kind = "perk", id = e.PerkId, rank = e.PerkRank });
                else if (e.UsedItemId != null) s.effects.Add(new EffectSave { kind = "item", id = e.UsedItemId });
                // 속성 시너지는 웨이브마다 다시 계산하므로 저장하지 않는다
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
            run.CaughtCount = s.caughtCount; run.BossesDefeated = s.bossesDefeated; run.LastEnemyName = s.lastEnemy;
            if (s.runItemIds != null)
                for (int i = 0; i < s.runItemIds.Count && i < (s.runItemCounts?.Count ?? 0); i++) run.RunItems[s.runItemIds[i]] = s.runItemCounts[i];

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
                        case "timed":
                            BattleEffect timed = null;
                            if (e.id == "crit") timed = new TempCritBoost { WavesLeft = e.rank };
                            else if (e.id.StartsWith("stat:") && Enum.TryParse<StatType>(e.id.Substring(5), out var st)) timed = new TempStatBoost(st) { WavesLeft = e.rank };
                            else if (e.id.StartsWith("type:") && Enum.TryParse<PalType>(e.id.Substring(5), out var pt)) timed = new TempTypeBoost(pt) { WavesLeft = e.rank };
                            if (timed != null && e.rank > 0) pal.AddEffect(timed);
                            break;
                        case "item":
                            var used = ItemCatalog.Get(e.id);
                            if (used != null) used.Apply(new ItemContext { Run = run, Target = pal });
                            else Debug.LogWarning($"[저장] 사용한 아이템을 찾지 못했습니다: {e.id}");
                            break;
                        case "held":
                            var item = ItemCatalog.All.FirstOrDefault(d => d.IsHeld && d.Name == e.id);
                            if (item != null) item.Apply(new ItemContext { Run = run, Target = pal });
                            else Debug.LogWarning($"[저장] 지닌 아이템을 찾지 못했습니다: {e.id}");
                            break;
                    }
                }
            }
            // 속성 시너지(최대 HP +% 등)를 먼저 붙인 뒤 HP를 맞춘다 → 저장할 때와 최대 HP가 같다
            Synergies.Apply(party);
            foreach (var (pal, ps) in restoreLater)
            {
                pal.RestoreState(ps.level, ps.exp, ps.hp, (StatusCondition)ps.status, ps.ivs, ps.stars);
                pal.SetStages(ps.stages);                           // 기술로 올린 능력치 랭크도 이어진다
                pal.CaughtSphere = (SphereType)ps.sphere;
                pal.MetWave = ps.metWave; pal.MetLevel = ps.metLevel; pal.MetArea = ps.metArea; pal.MetAsBoss = ps.metAsBoss;
            }
            return run;
        }
    }
}
