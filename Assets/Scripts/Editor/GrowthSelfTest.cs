using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/20. 성장 시스템 자동 테스트
    /// 진화·메가진화·다이맥스 대신 넣은 성장 시스템 4가지를 실제로 돌려 본다.
    ///  ① 강화 효과(퍽): 모든 퍽의 모든 Lv를 붙이고 전투를 돌림, 카드 뽑기 규칙(중복 없음, 전설은 Lv.40부터, 최소 등급)
    ///  ② 각성: 모든 각성이 HP 절반 이하에서 깨어나고 능력치가 오르는지, 전투가 끝나면 잠드는지
    ///  ③ 응축 ★: 같은 팰을 잡으면 ★이 오르고 능력치·지닌 아이템이 넘어오는지
    ///  ④ 속성 시너지: 같은 속성 2/3/4마리에서 Lv이 바뀌고 알림이 나오는지
    /// 결과는 Console에 "[성장 테스트]"로 나온다. 플레이 모드가 아닐 때 실행.
    /// </summary>
    public static class GrowthSelfTest
    {
        [MenuItem("PalRogue/20. 성장 시스템 자동 테스트")]
        static void RunAll()
        {
            var pals = AssetDatabase.FindAssets("t:PalData")
                .Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).OrderBy(p => p.name).ToList();
            var normal = pals.Where(p => p.canBeCaught && !GameConfig.IsLegendary(p)).ToList();
            if (normal.Count < 4) { Debug.LogError("[성장 테스트] PalData가 부족합니다."); return; }

            var rng = new System.Random(77);
            int pass = 0, fail = 0;
            var report = new StringBuilder();

            void Test(string name, Func<string> body)
            {
                try { string r = body(); pass++; report.AppendLine($"  ✔ {name}: {r}"); }
                catch (Exception e) { fail++; report.AppendLine($"  ✘ {name}: {e.GetType().Name} {e.Message}"); Debug.LogException(e); }
            }

            Test("강화 효과 전부 붙이기 + 전투", () => TestPerks(normal, rng));
            Test("강화 카드 뽑기 규칙", () => TestPerkRolls(normal, rng));
            Test("각성 전부 깨어나기", () => TestAwakenings(normal, rng));
            Test("응축 ★", () => TestCondense(normal, rng));
            Test("속성 시너지", () => TestSynergies(normal));
            Test("상대 ★·각성", () => TestEnemies(normal, pals, rng));

            string head = $"[성장 테스트] 통과 {pass} / 실패 {fail}  (강화 {PerkCatalog.All.Count}종, 각성 {AwakeningCatalog.All.Count}종)";
            if (fail > 0) Debug.LogError(head + "\n" + report);
            else Debug.Log(head + "\n" + report);
        }

        static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }

        // ───────── ① 강화 효과 ─────────
        static string TestPerks(List<PalData> data, System.Random rng)
        {
            int applied = 0;
            foreach (var perk in PerkCatalog.All)
            {
                var d = perk.Type.HasValue ? data.FirstOrDefault(x => x.HasType(perk.Type.Value)) ?? data[0] : data[0];
                var pal = new Pal(d, 40);
                for (int rank = 1; rank <= perk.MaxRank; rank++)
                {
                    PerkCatalog.Apply(pal, new PerkOffer { Perk = perk, Rank = rank });
                    var mine = pal.Effects.Where(e => e.PerkId == perk.Id).ToList();
                    Check(mine.Count == 1, $"{perk.Name}: Lv{rank} 효과가 {mine.Count}개");
                    Check(mine[0].PerkRank == rank, $"{perk.Name}: Lv이 {mine[0].PerkRank} (기대 {rank})");
                    Check(!string.IsNullOrEmpty(perk.Describe(rank)), $"{perk.Name}: 설명 없음");
                    applied++;
                }
                Fight(new List<Pal> { pal }, new Pal(data[rng.Next(data.Count)], 40), rng, 12);
            }
            return $"{PerkCatalog.All.Count}종 / {applied}개 Lv 적용, 전투 오류 없음";
        }

        static string TestPerkRolls(List<PalData> data, System.Random rng)
        {
            int rolls = 0; var tiers = new int[4];
            for (int level = 10; level <= 100; level += 10)
                for (int i = 0; i < 40; i++)
                {
                    var pal = new Pal(data[rng.Next(data.Count)], level);
                    var offers = PerkCatalog.RollChoices(pal, GameConfig.PerkChoices, rng);
                    Check(offers.Count == GameConfig.PerkChoices, $"Lv{level}: 카드 {offers.Count}장");
                    Check(offers.Select(o => o.Perk).Distinct().Count() == offers.Count, "같은 카드 중복");
                    foreach (var o in offers)
                    {
                        Check(o.Perk.Type == null || pal.Data.HasType(o.Perk.Type.Value), $"{o.Perk.Name}: 다른 속성 카드");
                        Check(level >= GameConfig.LegendaryPerkLevel || o.Perk.Rarity != PerkRarity.Legendary, $"Lv{level}에 전설 카드");
                        tiers[(int)o.Perk.Rarity]++; rolls++;
                    }
                    var epic = PerkCatalog.RollChoices(pal, GameConfig.PerkChoices, rng, PerkRarity.Epic);
                    Check(epic.All(o => o.Perk.Rarity >= PerkRarity.Epic), "★4 보너스에 영웅 미만 카드");
                }
            // 최대 Lv까지 다 찍은 퍽은 다시 안 나와야 한다
            var maxed = new Pal(data[0], 60);
            var atk = PerkCatalog.Get("atk_up");
            PerkCatalog.Apply(maxed, new PerkOffer { Perk = atk, Rank = atk.MaxRank });
            for (int i = 0; i < 200; i++)
                Check(PerkCatalog.RollChoices(maxed, 3, rng).All(o => o.Perk != atk), "최대 Lv 퍽이 또 나옴");
            string dist = string.Join(", ", Enumerable.Range(0, 4).Select(t => $"{PerkRarities.Names[t]} {tiers[t] * 100f / rolls:0.#}%"));
            return $"{rolls}장 ({dist})";
        }

        // ───────── ② 각성 ─────────
        static string TestAwakenings(List<PalData> data, System.Random rng)
        {
            var names = new List<string>();
            foreach (var def in AwakeningCatalog.All)
            {
                var d = def.Type.HasValue ? data.FirstOrDefault(x => x.HasType(def.Type.Value)) ?? data[0] : data[0];
                var pal = new Pal(d, 50);
                var awakening = new Awakening(def);
                pal.AddEffect(awakening);
                int atkBefore = pal.GetStat(StatType.Attack);

                var foe = new Pal(data[rng.Next(data.Count)], 10);                         // 약한 상대: 내 팰이 쓰러지지 않게
                var msgs = new List<string>();
                var battle = new Battle(new List<Pal> { pal }, new List<Pal> { foe }, rng, msgs.Add);
                pal.TakeDamage(pal.MaxHP * 6 / 10);                                   // 절반 밑으로
                bool sawEvent = false;
                for (int t = 0; t < 3 && battle.State == BattleState.Ongoing && !awakening.Active; t++)
                {
                    battle.ExecuteTurn(BattleAction.Skill(Mathf.Max(0, pal.Skills.FindIndex(s => s.HasPP))));
                    while (battle.TryDequeueEvent(out var ev)) if (ev.Type == BattleEventType.Awakened) sawEvent = true;
                }
                if (pal.IsFainted) { battle.Detach(); continue; }                       // 운 나쁘게 쓰러지면 건너뜀
                Check(awakening.Active, $"{def.Name}: HP {pal.CurrentHP}/{pal.MaxHP}인데 각성 안 함");
                Check(sawEvent, $"{def.Name}: 각성 연출 이벤트 없음");
                Check(msgs.Any(m => m.Contains(def.Name)), $"{def.Name}: 각성 메시지 없음");
                Check(pal.GetStat(StatType.Attack) >= Mathf.FloorToInt(atkBefore * GameConfig.AwakenStatMultiplier) - 1,
                      $"{def.Name}: 공격이 {atkBefore} → {pal.GetStat(StatType.Attack)}");
                battle.Detach();
                awakening.Sleep();
                Check(!awakening.Active, $"{def.Name}: 전투가 끝나도 각성이 안 풀림");
                awakening.SetTrue(true);
                Check(awakening.IsTrue, "진각성 안 됨");
                names.Add(def.Name);
            }
            return $"{names.Count}/{AwakeningCatalog.All.Count}종 각성 확인 ({string.Join(", ", names.Take(4))} …)";
        }

        // ───────── ③ 응축 ─────────
        static string TestCondense(List<PalData> data, System.Random rng)
        {
            var d = data[0];
            var mine = new Pal(d, 30);
            int atk0 = mine.GetStat(StatType.Attack), hp0 = mine.MaxHP;
            var ivs0 = mine.IVs.ToArray();
            var party = new List<Pal> { mine, new Pal(data[1], 30) };
            string result = "";
            for (int star = 1; star <= GameConfig.MaxStars + 1; star++)
            {
                var wild = new Pal(d, 20);
                ItemCatalog.Get("leftovers").Apply(new ItemContext { Target = wild });          // 지닌 아이템도 넘어오는지
                var battle = new Battle(party, new List<Pal> { wild }, rng);
                battle.ExecuteTurn(BattleAction.Throw(SphereType.Exotic));
                while (battle.TryDequeueEvent(out _)) { }
                Check(battle.State == BattleState.Caught, "엑조틱 스피어로 못 잡음");
                if (star <= GameConfig.MaxStars)
                {
                    Check(battle.PendingCatch == wild, "같은 팰인데 응축을 묻지 않음 (바로 파티에 들어감)");
                    Check(battle.CondenseTarget(wild) == mine, "응축 대상이 다름");
                    var target = battle.CondenseCaught(out var moved);
                    Check(target == mine && mine.Stars == star, $"★이 {mine.Stars} (기대 {star})");
                    Check(moved.Contains("먹다남은음식"), "지닌 아이템이 안 넘어옴");
                    for (int i = 0; i < 4; i++)
                        Check(mine.IVs[i] >= wild.IVs[i] && mine.IVs[i] >= ivs0[i], "응축했는데 더 좋은 개체값을 못 이어받음");
                }
                else
                {
                    // ★4면 더 응축할 수 없다 → 파티에 그냥 들어간다
                    Check(battle.CondenseTarget(wild) == null, "★4인데 또 응축 대상");
                    Check(battle.PlayerParty.Contains(wild), "★4일 때 잡은 팰이 파티에 안 들어감");
                }
                party = battle.PlayerParty.ToList();
                battle.Detach();
            }
            int atk4 = mine.GetStat(StatType.Attack);
            float expect = 1f + GameConfig.StarStatBonus * GameConfig.MaxStars;
            // 응축하면 더 좋은 개체값도 이어받으므로, 기본 능력치(개체값 반영) × 별 배율로 비교한다
            Check(Mathf.Abs(atk4 - mine.RawStat(StatType.Attack) * expect) <= 2, $"★4 공격 {mine.RawStat(StatType.Attack)} → {atk4} (기대 ×{expect})");
            Check(Mathf.Abs(mine.MaxHP - mine.BaseMaxHP * expect) <= 2, $"★4 HP {mine.BaseMaxHP} → {mine.MaxHP}");
            Check(mine.IVs.All(v => v >= 0 && v <= Pal.MaxIV), "개체값 범위 밖");
            result = $"★0→★4: 공격 {atk0}→{atk4}, HP {hp0}→{mine.MaxHP}, 아이템 {mine.Effects.Count(e => e.HeldItemId != null)}개 넘어옴, ★4 뒤에는 파티로";
            return result;
        }

        // ───────── ④ 속성 시너지 ─────────
        static string TestSynergies(List<PalData> data)
        {
            // 같은 속성(시너지가 있는 속성) 팰이 4마리 이상 있는 속성을 찾는다
            PalType type = default; List<PalData> same = null;
            foreach (PalType t in Enum.GetValues(typeof(PalType)))
            {
                var list = data.Where(x => x.HasType(t)).ToList();
                if (list.Count >= 2 && Synergies.Describe(t, 1) != "") { type = t; same = list; if (list.Count >= 4) break; }
            }
            Check(same != null, "시너지를 시험할 속성이 없음");
            var other = data.First(x => !x.HasType(type));
            var party = new List<Pal> { new Pal(same[0], 30), new Pal(other, 30) };
            var msgs = Synergies.Apply(party);
            Check(!party[0].Effects.OfType<SynergyEffect>().Any(x => x.Type == type), "1마리인데 시너지");

            var log = new List<string>();
            for (int n = 2; n <= Mathf.Min(4, same.Count); n++)
            {
                party.Add(new Pal(same[(n - 1) % same.Count], 30));
                msgs = Synergies.Apply(party);
                int want = Synergies.LevelFor(party.Count(p => p.Data.HasType(type)));
                var syn = party[0].Effects.OfType<SynergyEffect>().Where(s => s.Type == type).ToList();
                Check(syn.Count == 1 && syn[0].Level == want, $"{n}마리: 시너지 Lv {(syn.Count == 0 ? 0 : syn[0].Level)} (기대 {want})");
                Check(!party[1].Effects.OfType<SynergyEffect>().Any(s => s.Type == type), "다른 속성 팰에게 시너지가 붙음");
                Check(msgs.Any(m => m.Contains("시너지")), $"{n}마리: 알림 없음");
                Check(Synergies.Apply(party).Count == 0, "바뀐 게 없는데 또 알림");
                log.Add($"{n}마리 Lv.{want}");
            }
            // 빼면 내려가고, 1마리만 남으면 사라진다
            while (party.Count(p => p.Data.HasType(type)) > 1) party.RemoveAt(party.FindLastIndex(p => p.Data.HasType(type)));
            msgs = Synergies.Apply(party);
            Check(!party[0].Effects.OfType<SynergyEffect>().Any(x => x.Type == type), "1마리로 줄었는데 시너지가 남음");
            Check(msgs.Any(m => m.Contains("사라졌다")), "사라짐 알림 없음");
            return $"{PalTypeNames.Korean(type)}: {string.Join(" → ", log)} → 해제 ({Synergies.Describe(type, 1)} …)";
        }

        // ───────── 상대 ─────────
        static string TestEnemies(List<PalData> normal, List<PalData> pals, System.Random rng)
        {
            var run = new Run(new List<Pal> { new Pal(normal[0], 50) }, new[] { 5, 5, 5, 5, 1 }, rng);
            var lines = new List<string>();
            for (int wave = 1; wave <= GameConfig.FinalWave; wave++)
            {
                if (wave > 1) run.NextWave();
                if (wave != 49 && wave != 50 && wave != 100 && wave != 150 && wave != GameConfig.FinalWave && wave != 160) continue;
                var enemy = run.CreateEnemy(pals);
                int want = GameConfig.EnemyStars(wave, run.EnemyKind);
                Check(enemy.Stars == want, $"웨이브 {wave}: ★{enemy.Stars} (기대 {want})");
                bool legend = run.EnemyKind == BossKind.Legend || run.EnemyKind == BossKind.Final;
                var aw = AwakeningCatalog.Of(enemy);
                Check(legend == (aw != null), $"웨이브 {wave} {run.EnemyKind}: 각성 {(aw != null ? "있음" : "없음")}");
                if (run.EnemyKind == BossKind.Final) Check(aw.IsTrue, "최종 보스가 진각성이 아님");
                if (legend) Fight(new List<Pal> { new Pal(normal[1], 100) }, enemy, rng, 30);   // 상대 각성도 오류 없이
                lines.Add($"W{wave} {run.EnemyKind} {enemy.Data.palName} ★{enemy.Stars}{(aw != null ? $" {aw.Def.Name}{(aw.IsTrue ? "(진)" : "")}" : "")}");
            }
            return string.Join(" / ", lines);
        }

        static void Fight(List<Pal> party, Pal enemy, System.Random rng, int turns)
        {
            var battle = new Battle(party, new List<Pal> { enemy }, rng);
            for (int t = 0; t < turns; t++)
            {
                while (battle.TryDequeueEvent(out _)) { }
                if (battle.State == BattleState.WaitingPlayerSwitch)
                {
                    int next = battle.PlayerParty.ToList().FindIndex(p => !p.IsFainted);
                    if (next < 0 || !battle.SwitchAfterFaint(next)) break;
                    continue;
                }
                if (battle.State != BattleState.Ongoing) break;
                var usable = battle.Player.Skills.Select((s, i) => (s, i)).Where(x => x.s.HasPP).Select(x => x.i).ToList();
                var action = usable.Count == 0 ? BattleAction.Skill(-1) : BattleAction.Skill(usable[rng.Next(usable.Count)]);
                if (!battle.ExecuteTurn(action)) throw new Exception("행동이 거부됨");
            }
            while (battle.TryDequeueEvent(out _)) { }
            battle.Detach();
        }
    }
}
