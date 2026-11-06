#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/11. 자동 플레이 점검 — 화면 없이 모험을 여러 번 끝까지 돌리며 버그를 찾는다.
    ///  매 턴: 모든 팰의 HP가 0~최대 HP 안인지, 쓰러짐과 HP 0이 맞는지, 실드 수가 맞는지
    ///  매 웨이브: 보상 아이템을 무작위로 쓰고, 레벨업 강화·각성도 무작위로 붙이고, 시너지를 다시 계산해도 HP가 그대로인지
    ///  몇 웨이브마다: 저장 → 불러오기(JSON 거쳐서) 해서 HP·최대 HP·능력치·효과·기술이 똑같은지
    ///  예외가 나면 어디서 났는지 모아서 보여 준다.
    /// 결과는 콘솔에 요약 + 문제마다 한 줄 (같은 문제는 횟수로 묶는다)
    /// </summary>
    public static class PlaySimulator
    {
        const int Runs = 40, DeepRuns = 15, MaxWaves = 200, MaxTurns = 300;

        [MenuItem("PalRogue/11. 자동 플레이 점검 (모험 40번, 버그 찾기)", false, 201)]
        static void RunAll()
        {
            var pals = AssetDatabase.FindAssets("t:PalData").Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
                                    .Where(p => p != null).ToList();
            var skills = AssetDatabase.FindAssets("t:SkillData").Select(g => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g)))
                                      .Where(s => s != null).ToList();
            var pool = pals.Where(p => !GameConfig.IsLegendary(p)).ToList();
            var problems = new Dictionary<string, int>();
            var firstSeen = new Dictionary<string, string>();
            void Problem(string key, string detail)
            {
                problems[key] = problems.TryGetValue(key, out var n) ? n + 1 : 1;
                if (!firstSeen.ContainsKey(key)) firstSeen[key] = detail;
            }

            int totalWaves = 0, totalTurns = 0, saves = 0, items = 0, best = 0;
            var waveReached = new List<int>();
            try
            {
                for (int r = 0; r < Runs + DeepRuns; r++)
                {
                    bool deep = r >= Runs;                    // 깊이 가기: 웨이브마다 완전 회복해서 보스·전설·최종 보스까지 본다
                    EditorUtility.DisplayProgressBar("자동 플레이 점검", $"모험 {r + 1}/{Runs + DeepRuns}", r / (float)(Runs + DeepRuns));
                    var rng = new System.Random(1000 + r);
                    var starters = pals.Where(GameConfig.IsStarterPal).ToList();
                    if (starters.Count == 0) starters = pool;
                    var party = Enumerable.Range(0, 3).Select(_ => new Pal(starters[rng.Next(starters.Count)], GameConfig.StarterLevel, rng)).ToList();
                    var run = new Run(party, new[] { 10, 5, 3, 2, 1 }, rng);
                    int wave = 0;
                    try
                    {
                        for (; wave < MaxWaves && !run.IsWiped && run.Wave <= GameConfig.FinalWave; wave++)
                        {
                            totalWaves++;
                            // 시너지 다시 계산: HP가 바뀌면 안 된다 (최대 HP가 바뀐 만큼만)
                            var before = run.Party.Select(p => (p, p.CurrentHP, p.MaxHP)).ToList();
                            run.UpdateSynergies();
                            foreach (var (p, hp, max) in before)
                            {
                                int expect = p.IsFainted ? 0 : Mathf.Clamp(hp + (p.MaxHP - max), 1, p.MaxHP);
                                if (p.CurrentHP != expect) Problem("시너지 갱신 후 HP가 이상하게 바뀜", $"{p.Data.palName} {hp}/{max} → {p.CurrentHP}/{p.MaxHP} (예상 {expect})");
                            }

                            var enemy = run.CreateEnemy(pool);
                            var battle = new Battle(run.Party, new List<Pal> { enemy }, run.Rng, null, wave == 0, run.EnemyKind);
                            run.SyncParty(battle.PlayerParty);
                            battle.ExpMultiplier = run.ExpMultiplier;
                            battle.ExpShareRatio = run.ExpShareRatio;
                            battle.PlayerBerrySave = run.BerrySave;
                            battle.EnemySmartness = GameConfig.EnemySmartness(run.Wave, run.EnemyKind);
                battle.LevelCap = run.LevelCap;
                            Drain(battle, Problem);

                            int turns = 0;
                            while (turns++ < MaxTurns)
                            {
                                if (battle.State == BattleState.WaitingPlayerSwitch)
                                {
                                    int next = Enumerable.Range(0, battle.PlayerParty.Count).FirstOrDefault(i => !battle.PlayerParty[i].IsFainted);
                                    if (!battle.SwitchAfterFaint(next)) { Problem("쓰러진 뒤 교체 실패", $"웨이브 {run.Wave}"); break; }
                                    Drain(battle, Problem);
                                    continue;
                                }
                                if (battle.State != BattleState.Ongoing) break;
                                var action = PickAction(battle, run, rng);
                                if (!battle.ExecuteTurn(action)) action = BattleAction.Skill(battle.Player.Skills.FindIndex(s => s.HasPP));
                                if (!battle.IsValidPlayerAction(action) && battle.State == BattleState.Ongoing && !battle.ExecuteTurn(action))
                                    battle.ExecuteTurn(BattleAction.Skill(-1));
                                totalTurns++;
                                Drain(battle, Problem);
                                Check(battle.PlayerParty.Concat(battle.EnemyParty), $"웨이브 {run.Wave} 턴 {turns}", Problem);
                            }
                            if (turns >= MaxTurns) Problem("전투가 끝나지 않음 (300턴)", $"웨이브 {run.Wave} {battle.Player.Data.palName} vs {battle.Enemy.Data.palName}");

                            // 잡은 팰
                            if (battle.PendingCatch != null && !battle.AddCaughtToParty()) battle.ReleaseCaught();
                            run.SyncParty(battle.PlayerParty);
                            battle.Detach();
                            if (battle.State == BattleState.PlayerLost || run.IsWiped) break;

                            // 레벨업 강화·각성 (화면에서는 레벨마다 고른다 — 여기선 가끔 무작위로)
                            foreach (var p in run.Party.Where(p => !p.IsFainted))
                            {
                                if (rng.NextDouble() < 0.25)
                                {
                                    var offers = PerkCatalog.RollChoices(p, 3, rng);
                                    if (offers.Count > 0) Guard(() => PerkCatalog.Apply(p, offers[rng.Next(offers.Count)]), "강화 적용", Problem);
                                }
                                if (p.Level >= 50 && !p.Effects.OfType<Awakening>().Any() && rng.NextDouble() < 0.3)
                                {
                                    var aw = AwakeningCatalog.RollChoices(p, 3, rng);
                                    if (aw.Count > 0) p.AddEffect(new Awakening(aw[0]));
                                }
                            }
                            Check(run.Party, $"웨이브 {run.Wave} 강화 뒤", Problem);

                            // 보상 아이템 2~3개
                            int count = 2 + rng.Next(2);
                            var candidates = ItemCatalog.All.Where(d => d.Apply != null && SafeWeight(d, run) > 0f).ToList();
                            for (int i = 0; i < count && candidates.Count > 0; i++)
                            {
                                var item = candidates[rng.Next(candidates.Count)];
                                if (UseItem(item, run, rng, Problem)) items++;
                            }
                            Check(run.Party, $"웨이브 {run.Wave} 아이템 뒤 ({run.Party.Count}마리)", Problem);

                            if ((GameConfig.HealAfterBoss && run.IsBossWave) || deep) run.HealAll();
                            if (deep) foreach (var p in run.Party) for (int k = 0; k < 2 && !p.IsMaxLevel; k++) p.GainExp(p.ExpToNext);   // 레벨도 웨이브를 따라가게
                            run.NextWave();

                            // 저장 → 불러오기
                            if (wave % 3 == 2)
                            {
                                saves++;
                                run = SaveRoundTrip(run, pals, skills, pool, Problem);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Problem("예외: " + e.GetType().Name + " " + e.Message, $"모험 {r + 1} 웨이브 {run.Wave}\n{e.StackTrace}");
                    }
                    waveReached.Add(run.Wave);
                    best = Mathf.Max(best, run.Wave);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            var sb = new StringBuilder();
            sb.AppendLine($"[자동 플레이 점검] 모험 {Runs}+{DeepRuns}번 · 웨이브 {totalWaves}개 · 턴 {totalTurns}개 · 아이템 {items}번 · 저장/불러오기 {saves}번 · 최고 웨이브 {best} · 평균 {waveReached.Average():0.0}");
            if (problems.Count == 0) sb.AppendLine("문제 없음!");
            foreach (var kv in problems.OrderByDescending(k => k.Value))
                sb.AppendLine($"  ✗ {kv.Key} ×{kv.Value}\n      예: {firstSeen[kv.Key]}");
            if (problems.Count == 0) Debug.Log(sb.ToString()); else Debug.LogWarning(sb.ToString());
            System.IO.Directory.CreateDirectory("Captures");
            System.IO.File.WriteAllText("Captures/sim_report.txt", sb.ToString());
        }

        static float SafeWeight(ItemDef d, Run run) { try { return d.Weight(run); } catch { return 0f; } }

        static void Guard(Action a, string what, Action<string, string> problem)
        {
            try { a(); }
            catch (Exception e) { problem($"예외({what}): {e.GetType().Name} {e.Message}", e.StackTrace); }
        }

        internal static BattleAction PickAction(Battle battle, Run run, System.Random rng, bool careful = false)
        {
            var foe0 = battle.Enemy;
            if (careful)
            {
                // 잘하는 플레이어: 상성이 안 좋으면 더 센 기술을 가진 팰로 바꾼다
                float Best(Pal p) => p.Skills.Where(s => s.HasPP && s.Data.power > 0)
                    .Select(s => s.Data.power * TypeChart.GetMultiplier(s.Data.type, foe0.Data.primaryType, foe0.Data.SecondaryType)
                                 * TypeChart.GetStab(s.Data.type, p.Data.primaryType, p.Data.SecondaryType) * p.GetStat(StatType.Attack)).DefaultIfEmpty(0f).Max();
                float mine = Best(battle.Player);
                int better = Enumerable.Range(1, battle.PlayerParty.Count - 1).Where(i => !battle.PlayerParty[i].IsFainted && battle.PlayerParty[i].CurrentHP * 2 > battle.PlayerParty[i].MaxHP)
                    .OrderByDescending(i => Best(battle.PlayerParty[i])).DefaultIfEmpty(-1).First();
                if (better > 0 && Best(battle.PlayerParty[better]) > mine * 1.8f) return BattleAction.SwitchTo(better);
            }
            // 가끔 교체, 가끔 스피어 (HP가 낮을 때), 나머지는 PP가 남은 기술
            if (!careful && rng.NextDouble() < 0.05)
            {
                var alive = Enumerable.Range(0, battle.PlayerParty.Count).Where(i => i != 0 && !battle.PlayerParty[i].IsFainted).ToList();
                if (alive.Count > 0) return BattleAction.SwitchTo(alive[rng.Next(alive.Count)]);
            }
            var foe = battle.Enemy;
            if (foe.CurrentHP * 3 < foe.MaxHP && rng.NextDouble() < 0.5 && battle.CatchBlockReason() == null)
            {
                for (int s = PalSpheres.Count - 1; s >= 0; s--)
                    if (run.Spheres[s] > 0) { run.Spheres[s]--; return BattleAction.Throw((SphereType)s); }
            }
            var usable = battle.Player.Skills.Select((sk, i) => (sk, i)).Where(t => t.sk.HasPP).ToList();
            if (usable.Count == 0) return BattleAction.Skill(-1);
            if (!careful && rng.NextDouble() < 0.2) return BattleAction.Skill(usable[rng.Next(usable.Count)].i);   // 가끔은 아무 기술 (변화 기술도)
            // 대부분은 위력 × 상성이 가장 센 기술
            var best = usable.OrderByDescending(t => t.sk.Data.power * TypeChart.GetMultiplier(t.sk.Data.type, foe.Data.primaryType, foe.Data.SecondaryType)
                                                     * TypeChart.GetStab(t.sk.Data.type, battle.Player.Data.primaryType, battle.Player.Data.SecondaryType)).First();
            return BattleAction.Skill(best.i);
        }

        internal static bool UseItem(ItemDef item, Run run, System.Random rng, Action<string, string> problem)
        {
            try
            {
                if (item.CannotUseNow?.Invoke(run) != null) return false;
                var ctx = new ItemContext { Run = run };
                if (item.Target == ItemTarget.Pal || item.Target == ItemTarget.PalSkill)
                {
                    var ok = run.Party.Where(p => item.CannotUse?.Invoke(p) == null).ToList();
                    if (ok.Count == 0) return false;
                    ctx.Target = ok[rng.Next(ok.Count)];
                    if (item.Target == ItemTarget.PalSkill)
                    {
                        var sk = Enumerable.Range(0, ctx.Target.Skills.Count).Where(i => item.CannotUseSkill?.Invoke(ctx.Target.Skills[i]) == null).ToList();
                        if (sk.Count == 0) return false;
                        ctx.SkillIndex = sk[rng.Next(sk.Count)];
                    }
                }
                var before = ctx.Target != null ? (ctx.Target.CurrentHP, ctx.Target.MaxHP) : (0, 0);
                item.Apply(ctx);
                if (ctx.Target != null && ctx.Target.CurrentHP > ctx.Target.MaxHP)
                    problem("아이템 뒤 HP가 최대 HP보다 큼", $"{item.Name} {ctx.Target.Data.palName} {before} → {ctx.Target.CurrentHP}/{ctx.Target.MaxHP}");
                return true;
            }
            catch (Exception e)
            {
                problem($"예외(아이템 {item.Id}): {e.GetType().Name} {e.Message}", e.StackTrace);
                return false;
            }
        }

        static void Drain(Battle battle, Action<string, string> problem)
        {
            while (battle.TryDequeueEvent(out var ev))
            {
                if (ev.Type != BattleEventType.HpChanged) continue;
                if (ev.From < 0 || ev.To < 0) problem("HP 이벤트에 음수", $"{ev.Pal.Data.palName} {ev.From}→{ev.To}");
                if (ev.To > ev.MaxHp || ev.From > ev.MaxHp && ev.From > ev.To) problem("HP 이벤트가 그때의 최대 HP를 넘음", $"{ev.Pal.Data.palName} {ev.From}→{ev.To} / {ev.MaxHp}");
                bool active = ev.Pal == battle.Player || ev.Pal == battle.Enemy;
                if (!active) problem("싸우지 않는 팰의 HP 이벤트 (화면에서 다른 팰 HP바가 움직임)", $"{ev.Pal.Data.palName} {ev.From}→{ev.To}");
            }
        }

        static void Check(IEnumerable<Pal> pals, string where, Action<string, string> problem)
        {
            foreach (var p in pals)
            {
                if (p.CurrentHP < 0 || p.CurrentHP > p.MaxHP) problem("HP가 0~최대 HP 밖", $"{where} {p.Data.palName} {p.CurrentHP}/{p.MaxHP}");
                if (p.MaxHP <= 0) problem("최대 HP가 0 이하", $"{where} {p.Data.palName}");
                if (p.ShieldsLeft < 0 || p.ShieldsLeft >= Mathf.Max(1, p.BossSegments) && p.IsBoss) problem("실드 수가 이상함", $"{where} {p.Data.palName} {p.ShieldsLeft}/{p.BossSegments}");
                if (p.IsFainted && p.Status != StatusCondition.None) problem("쓰러졌는데 상태이상이 남음", $"{where} {p.Data.palName} {p.Status}");
                foreach (var s in p.Skills)
                    if (s.PP < 0 || s.PP > s.MaxPP) problem("PP가 0~최대 밖", $"{where} {p.Data.palName} {s.Data.skillName} {s.PP}/{s.MaxPP}");
            }
        }

        /// <summary>저장 → JSON → 불러오기. 되살린 런이 원래와 같은지 비교하고, 되살린 런으로 계속한다</summary>
        static Run SaveRoundTrip(Run run, List<PalData> pals, List<SkillData> skills, List<PalData> pool, Action<string, string> problem)
        {
            run.UpdateSynergies();                                  // 실제 게임도 저장 직전 시너지가 붙어 있다
            var save = SaveSlots.Capture(run, 0, 1);
            var json = JsonUtility.ToJson(save);
            var loaded = SaveSlots.Restore(JsonUtility.FromJson<RunSave>(json), pals, skills);
            loaded.UpdateSynergies();                               // 실제 게임도 첫 웨이브 시작에 시너지를 붙인다
            if (loaded.Party.Count != run.Party.Count) { problem("불러오기 후 파티 수가 다름", $"{run.Party.Count} → {loaded.Party.Count}"); return run; }
            for (int i = 0; i < run.Party.Count; i++)
            {
                Pal a = run.Party[i], b = loaded.Party[i];
                string who = $"웨이브 {run.Wave} {a.Data.palName} Lv.{a.Level}";
                if (a.CurrentHP != b.CurrentHP || a.MaxHP != b.MaxHP) problem("불러오기 후 HP가 다름", $"{who} {a.CurrentHP}/{a.MaxHP} → {b.CurrentHP}/{b.MaxHP}  효과: {string.Join(", ", a.Effects.Select(e => e.Name))}");
                foreach (StatType s in Enum.GetValues(typeof(StatType)))
                    if (a.GetStat(s) != b.GetStat(s)) problem("불러오기 후 능력치가 다름", $"{who} {s} {a.GetStat(s)} → {b.GetStat(s)}  효과: {string.Join(", ", a.Effects.Select(e => e.Name))} ⇢ {string.Join(", ", b.Effects.Select(e => e.Name))}");
                var ea = a.Effects.Select(e => e.Name + (e is ITimedEffect t ? "(" + t.WavesLeft + ")" : "")).OrderBy(x => x).ToList();
                var eb = b.Effects.Select(e => e.Name + (e is ITimedEffect t ? "(" + t.WavesLeft + ")" : "")).OrderBy(x => x).ToList();
                if (!ea.SequenceEqual(eb)) problem("불러오기 후 효과가 다름", $"{who} [{string.Join(", ", ea)}] → [{string.Join(", ", eb)}]");
                if (a.Level != b.Level || a.Exp != b.Exp || a.Stars != b.Stars || a.Status != b.Status) problem("불러오기 후 레벨·경험치·별·상태가 다름", who);
                if (!a.Skills.Select(s => (s.Data.name, s.PP, s.MaxPP)).SequenceEqual(b.Skills.Select(s => (s.Data.name, s.PP, s.MaxPP)))) problem("불러오기 후 기술이 다름", who);
            }
            if (run.Money != loaded.Money || run.Wave != loaded.Wave) problem("불러오기 후 돈·웨이브가 다름", $"{run.Money}/{run.Wave} → {loaded.Money}/{loaded.Wave}");
            if (Math.Abs(run.ExpMultiplier - loaded.ExpMultiplier) > 0.001f || Math.Abs(run.ExpShareRatio - loaded.ExpShareRatio) > 0.001f || Math.Abs(run.MoneyMultiplier - loaded.MoneyMultiplier) > 0.001f)
                problem("불러오기 후 파티 전체 아이템 효과가 다름", $"경험 {run.ExpMultiplier}/{loaded.ExpMultiplier} 학습 {run.ExpShareRatio}/{loaded.ExpShareRatio} 돈 {run.MoneyMultiplier}/{loaded.MoneyMultiplier}");
            if (!run.Spheres.SequenceEqual(loaded.Spheres)) problem("불러오기 후 스피어가 다름", "");
            // 이어하기를 해도 이번 웨이브 상대가 같아야 한다
            string lastA = run.LastEnemyName, lastB = loaded.LastEnemyName;
            var ea2 = run.CreateEnemy(pool); var eb2 = loaded.CreateEnemy(pool);
            run.LastEnemyName = lastA; loaded.LastEnemyName = lastB;
            if (ea2 == null || eb2 == null || ea2.Data != eb2.Data || ea2.Level != eb2.Level || ea2.MaxHP != eb2.MaxHP)
                problem("불러오기 후 이번 웨이브 상대가 달라짐", $"웨이브 {run.Wave}: {ea2?.Data.palName} Lv.{ea2?.Level} → {eb2?.Data.palName} Lv.{eb2?.Level}");
            return loaded;
        }
    }
}
#endif
