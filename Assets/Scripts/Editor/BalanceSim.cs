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
    /// 메뉴 PalRogue/44. 밸런스 비교 — 실제 게임 코드로 "보통 플레이어"를 흉내 내어 모험을 여러 번 돌리고
    /// 예전 설정과 지금 설정에서 몇 웨이브까지 가는지, 상대와 레벨 차이가 얼마인지 비교한다.
    ///  보통 플레이어: 위력×상성이 센 기술 위주(가끔 아무 기술), 가끔 교체·포획, 새 기술은 더 세면 배움,
    ///  강화 레벨마다 강화 3개 중 1개, 보상은 다친 팰이 있으면 회복약 → 아니면 가장 높은 등급. 상점은 안 씀.
    /// 결과: Captures/balance_report.txt
    /// </summary>
    public static class BalanceSim
    {
        const int RunsPerConfig = 30, StrongRuns = 16, MaxWaves = 190, MaxTurns = 400;

        struct Cfg
        {
            public string Name; public float Discount; public float Share; public float Smart; public int ItemsFrom; public int SpreadUntil; public int Until; public float ExpDiv; public int BossBonus; public float ExpBonus; public int BossUntil; public int ShieldFrom; public float Rest; public float CapMargin;
            public void Apply()
            {
                GameConfig.EarlyLevelDiscount = Discount; GameConfig.WildShareStart = Share; GameConfig.WildSmartStart = Smart;
                EnemyLoadout.ItemsFromWave = ItemsFrom; GameConfig.EarlySpreadUntil = SpreadUntil;
                GameConfig.EarlyDiscountUntil = Until; GameConfig.ExpYieldDivisor = ExpDiv; GameConfig.EarlyBossBonus = BossBonus;
                GameConfig.EarlyExpBonus = ExpBonus; GameConfig.EarlyBossUntil = BossUntil; GameConfig.ShieldBoostFromWave = ShieldFrom; GameConfig.RestHealStart = Rest; GameConfig.LevelCapMargin = CapMargin;
            }
        }

        [MenuItem("PalRogue/44. 밸런스 비교 (지금 설정 vs 다른 설정)")]
        static void Compare()
        {
            var pals = AssetDatabase.FindAssets("t:PalData").Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
                                    .Where(p => p != null).OrderBy(p => p.name).ToList();
            var pool = pals.Where(p => !GameConfig.IsLegendary(p)).ToList();
            var starters = pals.Where(GameConfig.IsStarterPal).ToList();

            // 지금 값을 기억해 두고 끝나면 되돌린다
            var now = new Cfg { Name = "지금", Discount = GameConfig.EarlyLevelDiscount, Share = GameConfig.WildShareStart, Smart = GameConfig.WildSmartStart,
                                ItemsFrom = EnemyLoadout.ItemsFromWave, SpreadUntil = GameConfig.EarlySpreadUntil,
                                Until = GameConfig.EarlyDiscountUntil, ExpDiv = GameConfig.ExpYieldDivisor, BossBonus = GameConfig.EarlyBossBonus,
                                ExpBonus = GameConfig.EarlyExpBonus, BossUntil = GameConfig.EarlyBossUntil, ShieldFrom = GameConfig.ShieldBoostFromWave, Rest = GameConfig.RestHealStart, CapMargin = GameConfig.LevelCapMargin };
            var configs = new[]
            {
                now,
            };
            var sb = new StringBuilder();
            sb.AppendLine($"[밸런스 비교] 설정마다 모험 {RunsPerConfig}번 (스타팅 팰 3마리 무작위, 최대 웨이브 {MaxWaves})");
            try
            {
                for (int c = 0; c < configs.Length; c++)
                {
                    configs[c].Apply();
                    var ends = new List<int>();
                    var gaps = new Dictionary<int, List<int>> { [5] = new(), [10] = new(), [20] = new(), [30] = new(), [50] = new(), [80] = new() };
                    var wipeKinds = new Dictionary<string, int>();
                    for (int r = 0; r < RunsPerConfig; r++)
                    {
                        EditorUtility.DisplayProgressBar("밸런스 비교", $"{configs[c].Name} 모험 {r + 1}/{RunsPerConfig}", (c * RunsPerConfig + r) / (float)(configs.Length * RunsPerConfig));
                        var (end, g, kind) = OneRun(new System.Random(5000 + r), starters, pool);
                        ends.Add(end);
                        foreach (var kv in g) if (gaps.ContainsKey(kv.Key)) gaps[kv.Key].Add(kv.Value);
                        wipeKinds[kind] = wipeKinds.TryGetValue(kind, out var n) ? n + 1 : 1;
                    }
                    string Pct(int w) => $"{100f * ends.Count(e => e <= w) / ends.Count,3:0}%";
                    ends.Sort();
                    sb.AppendLine($"  [{configs[c].Name}] 끝난 웨이브 중앙값 {ends[ends.Count / 2]} | ≤5 {Pct(5)} ≤10 {Pct(10)} ≤20 {Pct(20)} ≤30 {Pct(30)} ≤50 {Pct(50)} ≤80 {Pct(80)}");
                    sb.AppendLine("      (내 최고 레벨 − 상대 레벨) 평균: " + string.Join(", ", gaps.Where(kv => kv.Value.Count > 3).Select(kv => $"w{kv.Key} {kv.Value.Average():+0.0;-0.0}")));
                    sb.AppendLine("      전멸한 웨이브 종류: " + string.Join(", ", wipeKinds.Select(kv => $"{kv.Key} {kv.Value}")));
                }
            now.Apply();
            // 튼튼한 플레이: 웨이브마다 완전 회복 → 끝까지 가서 레벨 곡선·한도·만렙 시점을 본다
            {
                var ends = new List<int>(); var allMax = new List<int>(); var strongKinds = new List<string>();
                var gaps = new Dictionary<int, List<int>> { [20] = new(), [40] = new(), [60] = new(), [80] = new(), [100] = new(), [130] = new(), [150] = new(), [170] = new(), [180] = new(), [185] = new(), [190] = new() };
                for (int r = 0; r < StrongRuns; r++)
                {
                    EditorUtility.DisplayProgressBar("밸런스 비교", $"튼튼한 플레이 {r + 1}/{StrongRuns}", r / (float)StrongRuns);
                    var (end, g, kind) = OneRun(new System.Random(9000 + r), starters, pool, true, allMax);
                    ends.Add(end); strongKinds.Add(kind);
                    foreach (var kv in g) if (gaps.ContainsKey(kv.Key)) gaps[kv.Key].Add(kv.Value);
                }
                var maxWaves = allMax.Where(x => x > 0 && x <= 1000).ToList();
                sb.AppendLine($"  [튼튼한 플레이 (웨이브마다 완전 회복) {StrongRuns}번] 최종 보스까지 클리어 {ends.Count(e => e > GameConfig.FinalWave)}번, 끝난 웨이브: {string.Join(" ", ends.OrderBy(x => x))}");
                sb.AppendLine("      (내 최고 레벨 − 상대 레벨): " + string.Join(", ", gaps.Where(kv => kv.Value.Count > 0).Select(kv => $"w{kv.Key} {kv.Value.Average():+0.0;-0.0}")));
                sb.AppendLine("      전멸한 상대: " + string.Join(", ", strongKinds.GroupBy(k => k).Select(g => $"{g.Key} {g.Count()}")));
                sb.AppendLine("      파티 전원 만렙이 된 웨이브: " + (maxWaves.Count > 0 ? $"{string.Join(" ", maxWaves.OrderBy(x => x))} (평균 {maxWaves.Average():0.0})" : "없음"));
            }
            }
            finally
            {
                now.Apply();
                EditorUtility.ClearProgressBar();
            }
            Debug.Log(sb.ToString());
            System.IO.Directory.CreateDirectory("Captures");
            System.IO.File.WriteAllText("Captures/balance_report.txt", sb.ToString());
        }

        static (int end, Dictionary<int, int> gaps, string kind) OneRun(System.Random rng, List<PalData> starters, List<PalData> pool, bool strong = false, List<int> allMax = null)
        {
            var party = Enumerable.Range(0, 3).Select(_ => new Pal(starters[rng.Next(starters.Count)], GameConfig.StarterLevel, rng)).ToList();
            var run = new Run(party, new[] { 10, 5, 3, 2, 1 }, rng);
            var gaps = new Dictionary<int, int>();
            var perkDone = new Dictionary<Pal, int>();
        

            while (run.Wave <= MaxWaves && !run.IsWiped)
            {
                run.UpdateSynergies();
                var enemy = run.CreateEnemy(pool);
                gaps[run.Wave] = run.Party.Max(p => p.Level) - enemy.Level;
                string kind = run.EnemyKind.ToString();
                var battle = new Battle(run.Party, new List<Pal> { enemy }, run.Rng, null, run.Wave == 1, run.EnemyKind);
                run.SyncParty(battle.PlayerParty);
                battle.ExpMultiplier = run.ExpMultiplier;
                battle.ExpShareRatio = run.ExpShareRatio;
                battle.PlayerBerrySave = run.BerrySave;
                battle.EnemySmartness = GameConfig.EnemySmartness(run.Wave, run.EnemyKind);
                battle.LevelCap = run.LevelCap;
                Learn(battle);
                int turns = 0;
                while (turns++ < MaxTurns)
                {
                    if (battle.State == BattleState.WaitingPlayerSwitch)
                    {
                        // 남은 팰 중 상대에게 가장 센 기술을 가진 팰
                        var foe = battle.Enemy;
                        int next = Enumerable.Range(0, battle.PlayerParty.Count).Where(i => !battle.PlayerParty[i].IsFainted)
                                             .OrderByDescending(i => BestPower(battle.PlayerParty[i], foe)).First();
                        battle.SwitchAfterFaint(next);
                        Learn(battle);
                        continue;
                    }
                    if (battle.State != BattleState.Ongoing) break;
                    var action = PlaySimulator.PickAction(battle, run, rng, strong);
                    if (!battle.ExecuteTurn(action) && !battle.ExecuteTurn(BattleAction.Skill(battle.Player.Skills.FindIndex(s => s.HasPP))))
                        battle.ExecuteTurn(BattleAction.Skill(-1));
                    Learn(battle);
                }
                if (battle.PendingCatch != null && !battle.AddCaughtToParty()) battle.ReleaseCaught();
                run.SyncParty(battle.PlayerParty);
                battle.Detach();
                if (battle.State == BattleState.PlayerLost || run.IsWiped || turns >= MaxTurns) return (run.Wave, gaps, kind);

                // 강화 레벨(10, 20…)마다 강화 1개, Lv.50부터 각성
                foreach (var p in run.Party)
                {
                    perkDone.TryGetValue(p, out int done);
                    for (int lv = (done / GameConfig.PerkLevelStep + 1) * GameConfig.PerkLevelStep; lv <= p.Level; lv += GameConfig.PerkLevelStep)
                    {
                        if (lv >= GameConfig.AwakenLevel && !p.Effects.OfType<Awakening>().Any())
                        {
                            var aw = AwakeningCatalog.RollChoices(p, 3, rng);
                            if (aw.Count > 0) { p.AddEffect(new Awakening(aw[0])); continue; }
                        }
                        var offers = PerkCatalog.RollChoices(p, GameConfig.PerkChoices, rng);
                        if (offers.Count > 0) PerkCatalog.Apply(p, offers[rng.Next(offers.Count)]);
                    }
                    perkDone[p] = p.Level;
                }

                foreach (var (wave, itemId) in GameConfig.GuaranteedRewards)
                    if (wave == run.Wave) ItemCatalog.Get(itemId)?.Apply(new ItemContext { Run = run });

                // 보상 1개 고르기
                var offer = ItemCatalog.RollRewards(run, ItemCatalog.RewardCount(run), rng);
                ItemDef pick = null;
                bool anyFainted = run.Party.Any(p => p.IsFainted);
                bool anyHurt = run.Party.Any(p => !p.IsFainted && p.CurrentHP < p.MaxHP * 0.6f);
                if (anyFainted) pick = offer.FirstOrDefault(d => d.Name.Contains("기력") || d.Name.Contains("부활"));
                if (pick == null && anyHurt) pick = offer.Where(d => d.Target == ItemTarget.Pal && d.Description.Contains("회복")).OrderByDescending(d => d.Tier).FirstOrDefault();
                if (pick == null) pick = offer.OrderByDescending(d => d.Tier).FirstOrDefault();
                if (pick != null) UseOn(pick, run, rng);

                float rest = GameConfig.RestHeal(run.Wave);
                if (rest > 0f) foreach (var p in run.Party) if (!p.IsFainted) p.Heal(Mathf.Max(1, Mathf.RoundToInt(p.MaxHP * rest)));
                if ((GameConfig.HealAfterBoss && run.IsBossWave) || strong) run.HealAll();
                if (allMax != null && run.Party.All(p => p.Level >= GameConfig.MaxLevel) && !allMax.Contains(-run.GetHashCode()))
                {
                    allMax.Add(run.Wave);                                 // 파티 전원이 만렙이 된 웨이브
                    allMax.Add(-run.GetHashCode());
                }
                run.NextWave();
            }
            return (run.Wave, gaps, "끝까지");
        }

        static void UseOn(ItemDef item, Run run, System.Random rng)
        {
            try
            {
                var ctx = new ItemContext { Run = run };
                if (item.Target == ItemTarget.Pal || item.Target == ItemTarget.PalSkill)
                {
                    var ok = run.Party.Where(p => item.CannotUse?.Invoke(p) == null).OrderBy(p => p.IsFainted ? -1f : p.CurrentHP / (float)p.MaxHP).ToList();
                    if (ok.Count == 0) return;
                    ctx.Target = ok[0];                         // 회복약은 가장 많이 다친 팰에게
                    if (item.Target == ItemTarget.PalSkill)
                    {
                        int sk = Enumerable.Range(0, ctx.Target.Skills.Count).FirstOrDefault(i => item.CannotUseSkill?.Invoke(ctx.Target.Skills[i]) == null);
                        ctx.SkillIndex = sk;
                    }
                }
                item.Apply(ctx);
            }
            catch (Exception) { }
        }

        static float BestPower(Pal p, Pal foe) => p.Skills.Where(s => s.HasPP && s.Data.power > 0)
            .Select(s => s.Data.power * TypeChart.GetMultiplier(s.Data.type, foe.Data.primaryType, foe.Data.SecondaryType)).DefaultIfEmpty(0f).Max();

        /// <summary>레벨업 이벤트에서 새 기술: 자리가 있으면 배우고, 꽉 찼으면 가장 약한 공격 기술보다 셀 때만 바꾼다</summary>
        static void Learn(Battle battle)
        {
            while (battle.TryDequeueEvent(out var ev))
            {
                if (ev.Type != BattleEventType.LevelUp || ev.NewSkills == null || !ev.IsPlayerSide) continue;
                foreach (var sk in ev.NewSkills)
                {
                    var p = ev.Pal;
                    if (p.Skills.Count < Pal.MaxSkills) { p.TryLearnSkill(sk); continue; }
                    int weakest = Enumerable.Range(0, p.Skills.Count).OrderBy(i => p.Skills[i].Data.power).First();
                    if (sk.power > p.Skills[weakest].Data.power) p.TryLearnSkill(sk, weakest);
                }
            }
        }
    }
}
#endif
