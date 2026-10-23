using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/19. 아이템 자동 테스트
    /// 모든 아이템을 하나씩 실제로 써 보고(대상 팰·기술 고르기 포함), 그 파티로 전투를 몇 턴 돌려서
    /// 지니는 아이템·기간 효과가 오류 없이 동작하는지 확인한다. 아이템 그림이 있는지도 본다.
    /// 결과는 Console에 "[아이템 테스트]"로 나온다. 플레이 모드가 아닐 때 실행.
    /// </summary>
    public static class ItemSelfTest
    {
        const int TestLevel = 25;
        const int BattleTurns = 25;

        [MenuItem("PalRogue/19. 아이템 자동 테스트")]
        static void RunAll()
        {
            var pals = AssetDatabase.FindAssets("t:PalData")
                .Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).OrderBy(p => p.name).ToList();
            var partyData = pals.Where(p => p.canBeCaught && !GameConfig.IsLegendary(p)).Take(6).ToList();
            if (partyData.Count < 2) { Debug.LogError("[아이템 테스트] PalData가 부족합니다."); return; }

            ItemCatalog.SetSkillPool(pals.SelectMany(p => p.learnset).Where(e => e.skill != null).Select(e => e.skill).Distinct());

            int pass = 0, fail = 0, skipped = 0;
            var report = new StringBuilder();
            var rng = new System.Random(1234);

            foreach (var item in ItemCatalog.All.ToList())
            {
                try
                {
                    var run = MakeRun(partyData, rng);
                    string result = TryItem(item, run, rng);
                    if (result.StartsWith("SKIP"))
                    {
                        skipped++;
                        report.AppendLine($"  - {item.Name} ({item.Tier}): {result}");
                        Debug.LogWarning($"[아이템 테스트] 건너뜀: {item.Name} — {result}");
                        continue;
                    }

                    // 그림
                    if (!item.Sphere.HasValue && Resources.Load<Texture2D>(item.IconPath) == null)
                        throw new Exception($"그림 없음: Resources/{item.IconPath}.png");

                    // 그 파티로 전투를 돌려 지니는 아이템/기간 효과를 실제로 동작시킨다
                    SimulateBattle(run, pals, rng);
                    run.NextWave();                                    // 기간 효과 줄이기
                    pass++;
                    report.AppendLine($"  ✔ {item.Name} ({ItemTiers.Names[(int)item.Tier]}) → {result}");
                }
                catch (Exception e)
                {
                    fail++;
                    report.AppendLine($"  ✘ {item.Name} ({item.Tier}): {e.GetType().Name} {e.Message}");
                    Debug.LogException(e);
                }
            }

            // 보상 뽑기: 모든 웨이브에서 오류 없이 뽑히는지 + 등급 분포
            var tierCount = new int[5];
            int rolls = 0;
            try
            {
                for (int wave = 1; wave <= GameConfig.FinalWave; wave += 3)
                {
                    var run = MakeRun(partyData, rng);
                    for (int w = 1; w < wave; w++) run.NextWave();
                    var picks = ItemCatalog.RollRewards(run, ItemCatalog.RewardCount(run), rng);
                    if (picks.Count != ItemCatalog.RewardCount(run)) throw new Exception($"웨이브 {wave}: 보상 {picks.Count}개 (기대 {ItemCatalog.RewardCount(run)})");
                    if (picks.Distinct().Count() != picks.Count) throw new Exception($"웨이브 {wave}: 같은 아이템 중복");
                    foreach (var p in picks) tierCount[(int)p.Tier]++;
                    rolls += picks.Count;
                    var shop = ItemCatalog.ShopItems(run.Wave);
                    if (shop.Count == 0) throw new Exception($"웨이브 {wave}: 상점이 비었음");
                }
                pass++;
            }
            catch (Exception e) { fail++; report.AppendLine($"  ✘ 보상 뽑기: {e.Message}"); }

            string dist = string.Join(", ", Enumerable.Range(0, 5).Select(i => $"{ItemTiers.Names[i]} {tierCount[i] * 100f / Mathf.Max(1, rolls):0.#}%"));
            string head = $"[아이템 테스트] 통과 {pass} / 실패 {fail} / 건너뜀 {skipped}  (아이템 {ItemCatalog.All.Count}종)\n보상 등급 분포({rolls}개): {dist}";
            if (fail > 0) Debug.LogError(head + "\n" + report);
            else Debug.Log(head + "\n" + report);
        }

        /// <summary>테스트용 파티: 다친 팰, 기절한 팰, 상태이상, PP가 빈 기술이 섞여 있어서 거의 모든 아이템을 쓸 수 있다</summary>
        static Run MakeRun(List<PalData> data, System.Random rng)
        {
            var party = data.Select(d => new Pal(d, TestLevel)).ToList();
            party[0].TakeDamage(party[0].MaxHP / 2);
            if (party.Count > 1) party[1].TakeDamage(party[1].MaxHP);                         // 기절
            if (party.Count > 2) { party[2].TrySetStatus(StatusCondition.Poison); party[2].TakeDamage(party[2].MaxHP * 3 / 4); }
            foreach (var p in party.Take(3))
                foreach (var s in p.Skills) s.PP = 0;                                          // PP 바닥
            return new Run(party, new[] { 5, 5, 5, 5, 1 }, rng);
        }

        static string TryItem(ItemDef item, Run run, System.Random rng)
        {
            float w = item.Weight(run);
            var ctx = new ItemContext { Run = run };
            if (item.Target == ItemTarget.None)
            {
                string why = item.CannotUseNow(run);
                if (why != null) return $"SKIP (지금은 쓸 수 없음: {why})";
            }
            else
            {
                ctx.Target = run.Party.FirstOrDefault(p => item.CannotUse(p) == null);
                if (ctx.Target == null) return "SKIP (쓸 수 있는 팰 없음)";
                if (item.Target == ItemTarget.PalSkill)
                {
                    ctx.SkillIndex = ctx.Target.Skills.FindIndex(s => item.CannotUseSkill(s) == null);
                    if (ctx.SkillIndex < 0) return "SKIP (쓸 수 있는 기술 없음)";
                }
            }

            var before = Snapshot(ctx.Target, run);
            var outcome = item.Apply(ctx);
            if (outcome == null) throw new Exception("Apply가 null을 돌려줌");
            if (outcome.TeachSkill != null)
            {
                if (ctx.Target.Skills.Count >= Pal.MaxSkills) ctx.Target.TryLearnSkill(outcome.TeachSkill, 0);
                else ctx.Target.TryLearnSkill(outcome.TeachSkill);
                if (!ctx.Target.Skills.Any(s => s.Data == outcome.TeachSkill)) throw new Exception("기술을 배우지 못함");
            }
            var after = Snapshot(ctx.Target, run);
            string msg = outcome.Message ?? (outcome.LevelUps != null ? $"레벨업 {outcome.LevelUps.Count}번" : outcome.TeachSkill != null ? $"{outcome.TeachSkill.skillName} 배움" : "");
            return $"{msg}  [가중치 {w:0.##}] {before} → {after}";
        }

        /// <summary>바뀐 것을 한눈에 보기 위한 요약</summary>
        static string Snapshot(Pal p, Run r)
        {
            string pal = p == null ? "" :
                $"{p.Data.palName} Lv{p.Level} HP{p.CurrentHP}/{p.MaxHP} 공{p.GetStat(StatType.Attack)} 방{p.GetStat(StatType.Defense)} 스{p.GetStat(StatType.Speed)} " +
                $"PP{p.Skills.Sum(s => s.PP)}/{p.Skills.Sum(s => s.MaxPP)} 효과{p.Effects.Count} ";
            return pal + $"돈{r.Money} 스피어{string.Join("/", r.Spheres)} 경험+{r.ExpBonus:0.##} 학습{r.ExpShareCount} 사탕단지{r.CandyJars} 보상+{r.ExtraRewards}";
        }

        static void SimulateBattle(Run run, List<PalData> pals, System.Random rng)
        {
            var enemyData = pals[rng.Next(pals.Count)];
            var enemy = new Pal(enemyData, TestLevel);
            enemy.MakeBoss(3);                                                     // 보스 실드도 같이 확인
            var battle = new Battle(run.Party, new List<Pal> { enemy }, rng, null, true, BossKind.Boss)
            {
                ExpMultiplier = run.ExpMultiplier,
                ExpShareRatio = run.ExpShareRatio,
            };
            for (int turn = 0; turn < BattleTurns; turn++)
            {
                while (battle.TryDequeueEvent(out _)) { }
                if (battle.State == BattleState.WaitingPlayerSwitch)
                {
                    int next = battle.PlayerParty.ToList().FindIndex(p => !p.IsFainted);
                    if (next < 0 || !battle.SwitchAfterFaint(next)) break;
                    continue;
                }
                if (battle.State != BattleState.Ongoing) break;
                var usable = battle.Player.Skills.Select((s, i) => (s, i)).Where(t => t.s.HasPP).Select(t => t.i).ToList();
                var action = usable.Count == 0 ? BattleAction.Skill(-1) : BattleAction.Skill(usable[rng.Next(usable.Count)]);
                if (!battle.ExecuteTurn(action)) throw new Exception("행동이 거부됨");
            }
            while (battle.TryDequeueEvent(out _)) { }
            battle.Detach();
            run.SyncParty(battle.PlayerParty);
        }
    }
}
