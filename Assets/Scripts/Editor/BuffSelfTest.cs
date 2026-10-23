using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/27. 버프·디버프·보스 패턴 자동 테스트 (메뉴 26을 먼저 실행)
    ///  ① 능력치 변화 메시지 (올라갔다 / 크게 / 더 이상 …)
    ///  ② 변화 기술 10종을 실제 전투에서 써 보기 (능력치·회복이 맞게 바뀌는지)
    ///  ③ 공격 기술의 능력치 변화 추가 효과
    ///  ④ 상대 AI가 버프·회복을 알맞을 때만 쓰는지
    ///  ⑤ 힘 모으기: 모으면 다음 공격 배율, 급소·효과 굉장에 흐트러짐
    ///  ⑥ 보스 종류·속성별 패턴 배치
    ///  ⑦ 보스전 시뮬레이션 (왕 9종 + 제로버스 + 일반 보스) — 오류 없이 끝나는지, 패턴이 실제로 나오는지
    /// </summary>
    public static class BuffSelfTest
    {
        const string SkillDir = "Assets/Data/Skills";

        [MenuItem("PalRogue/27. 버프·디버프·보스 패턴 자동 테스트")]
        static void RunAll()
        {
            var pals = AssetDatabase.FindAssets("t:PalData")
                .Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null && p.learnset.Any(e => e.skill != null)).OrderBy(p => p.name).ToList();
            var supports = SupportSkillBuilder.Supports
                .Select(s => AssetDatabase.LoadAssetAtPath<SkillData>($"{SkillDir}/{s.eng}.asset")).ToList();
            if (supports.Any(s => s == null)) { Debug.LogError("[버프 테스트] 변화 기술이 없습니다. 메뉴 26을 먼저 실행하세요."); return; }
            var weak = Skill("Air_Cannon") ?? pals.SelectMany(p => p.learnset).Select(e => e.skill)
                .First(s => s != null && s.category == SkillCategory.Attack);

            var rng = new System.Random(77);
            var report = new StringBuilder();
            int pass = 0, fail = 0;
            void Test(string name, Func<string> body)
            {
                try { report.AppendLine($"  ✔ {name}: {body()}"); pass++; }
                catch (Exception e) { report.AppendLine($"  ✘ {name}: {e.GetType().Name} {e.Message}"); fail++; Debug.LogException(e); }
            }

            Test("① 능력치 변화 메시지", () => TestMessages(pals));
            Test("② 변화 기술 10종", () => TestSupportSkills(pals, supports, weak, rng));
            Test("③ 추가 효과", () => TestSecondary(pals, supports[0], rng));
            Test("④ AI 판단", () => TestAi(pals, supports, weak, rng));
            Test("⑤ 힘 모으기", () => TestCharge(pals, weak, rng));
            Test("⑥ 패턴 배치", () => TestAttach(pals));
            Test("⑦ 보스전 시뮬레이션", () => TestBossBattles(pals, rng));

            string head = $"[버프 테스트] 통과 {pass} / 실패 {fail}";
            if (fail > 0) Debug.LogError(head + "\n" + report); else Debug.Log(head + "\n" + report);
        }

        static SkillData Skill(string eng) => AssetDatabase.LoadAssetAtPath<SkillData>($"{SkillDir}/{eng}.asset");

        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

        static Pal Make(PalData data, int level, System.Random rng, params SkillData[] skills)
        {
            var p = new Pal(data, level, rng);
            p.Skills.Clear();
            foreach (var s in skills) p.Skills.Add(new SkillInstance(s));
            return p;
        }

        static List<BattleEvent> Drain(Battle b)
        {
            var list = new List<BattleEvent>();
            while (b.TryDequeueEvent(out var e)) list.Add(e);
            return list;
        }

        // ① ─────────────────────────────
        static string TestMessages(List<PalData> pals)
        {
            var p = new Pal(pals[0], 30);
            var log = new List<string>();
            StatStages.Change(p, StatType.Attack, 2, log.Add);
            Check(log[log.Count - 1].EndsWith("크게 올라갔다!") && p.GetStage(StatType.Attack) == 2, $"+2: {log[log.Count - 1]}");
            StatStages.Change(p, StatType.Attack, 6, log.Add);
            Check(p.GetStage(StatType.Attack) == 6 && log[log.Count - 1].EndsWith("매우 크게 올라갔다!"), $"+4(한계): {log[log.Count - 1]}");
            StatStages.Change(p, StatType.Attack, 1, log.Add);
            Check(log[log.Count - 1].EndsWith("더 이상 올라가지 않는다!"), $"한계: {log[log.Count - 1]}");
            StatStages.ChangeMany(p, new[] { (StatType.Defense, -1), (StatType.Speed, -1) }, log.Add);
            Check(log[log.Count - 1].Contains("방어와 스피드가 떨어졌다!"), $"묶음: {log[log.Count - 1]}");
            StatStages.ChangeMany(p, new[] { (StatType.Attack, 1), (StatType.Defense, 1), (StatType.Speed, 1) }, log.Add);
            Check(log.Any(l => l.Contains("더 이상 올라가지")) && log[log.Count - 1].Contains("방어와 스피드가 올라갔다!"), $"섞임: {log[log.Count - 2]} / {log[log.Count - 1]}");
            Check(p.ClearNegativeStages() == false, "떨어진 능력치가 없는데 정화됨");
            p.ModifyStage(StatType.Speed, -3);
            Check(p.ClearNegativeStages() && p.GetStage(StatType.Speed) == 0, "정화 실패");
            return $"\"{log[0]}\" / \"{log[3]}\" 등 {log.Count}줄";
        }

        // ② ─────────────────────────────
        static string TestSupportSkills(List<PalData> pals, List<SkillData> supports, SkillData weak, System.Random rng)
        {
            var notes = new List<string>();
            foreach (var sk in supports)
            {
                var user = Make(pals[rng.Next(pals.Count)], 50, rng, sk);
                var foe = Make(pals[rng.Next(pals.Count)], 50, rng, weak);
                var battle = new Battle(new List<Pal> { user }, new List<Pal> { foe }, new System.Random(1), null, false);
                battle.EnemySmartness = 1f;
                Drain(battle);
                if (sk.healPercent > 0) user.TakeDamage(user.MaxHP * 6 / 10);
                int hpBefore = user.CurrentHP;
                foe.Heal(foe.MaxHP);
                Check(battle.ExecuteTurn(BattleAction.Skill(0)), $"{sk.skillName}: 턴 진행 실패");
                var events = Drain(battle);

                foreach (var c in sk.statChanges)
                {
                    var who = c.self ? user : foe;
                    Check(who.GetStage(c.stat) == c.stages,
                          $"{sk.skillName}: {(c.self ? "자신" : "상대")} {StatStages.Short(c.stat)} {who.GetStage(c.stat)} (기대 {c.stages})");
                }
                if (sk.healPercent > 0)
                    Check(events.Any(e => e.Type == BattleEventType.HpChanged && e.Pal == user && e.To > e.From),
                          $"{sk.skillName}: 회복 안 됨");
                if (sk.statChanges.Count > 0)
                    Check(events.Any(e => e.Type == BattleEventType.StatStage), $"{sk.skillName}: 화살표 연출 이벤트 없음");
                Check(!events.Any(e => e.Type == BattleEventType.Message && e.Text.Contains("아무 일도")), $"{sk.skillName}: 아무 일도 일어나지 않음");
                notes.Add(sk.skillName);
            }

            // 한계까지 오른 상태에서 또 쓰면 "더 이상"
            var focus = supports[0];
            var u2 = Make(pals[0], 50, rng, focus);
            var f2 = Make(pals[1], 10, rng, weak);
            var b2 = new Battle(new List<Pal> { u2 }, new List<Pal> { f2 }, new System.Random(2), null, false);
            for (int i = 0; i < 4; i++) b2.ExecuteTurn(BattleAction.Skill(0));
            var texts = Drain(b2).Where(e => e.Type == BattleEventType.Message).Select(e => e.Text).ToList();
            Check(u2.GetStage(StatType.Attack) == 6 && texts.Any(t => t.Contains("더 이상 올라가지")), "4번째 기합 모으기 메시지");
            return string.Join(", ", notes) + " — 모두 정상";
        }

        // ③ ─────────────────────────────
        static string TestSecondary(List<PalData> pals, SkillData idle, System.Random rng)
        {
            // 상대는 공격하지 않는 기합 모으기만 써서 (내 팰이 먼저 쓰러지지 않게) 추가 효과 발동률만 잰다
            var notes = new List<string>();
            foreach (var (eng, change, chance) in SupportSkillBuilder.Secondary)
            {
                var sk = Skill(eng);
                if (sk == null) { notes.Add($"{eng} 없음(건너뜀)"); continue; }
                Check(sk.HasStatChanges && sk.statChangeChance == chance, $"{sk.skillName}: 추가 효과가 안 붙음 (메뉴 26)");
                int hits = 0, n = 0;
                for (int i = 0; i < 300; i++)
                {
                    var user = Make(pals[rng.Next(pals.Count)], 5, rng, sk);
                    var foe = Make(pals[rng.Next(pals.Count)], 90, rng, idle);       // 오래 버티는 상대
                    var b = new Battle(new List<Pal> { user }, new List<Pal> { foe }, new System.Random(i), null, false);
                    b.ExecuteTurn(BattleAction.Skill(0));
                    if (user.IsFainted) continue;
                    var who = change.self ? user : foe;
                    if (who.GetStage(change.stat) != 0 && Math.Sign(who.GetStage(change.stat)) == Math.Sign(change.stages)) hits++;
                    n++;
                }
                float rate = n > 0 ? hits * 100f / n : 0f;
                Check(Mathf.Abs(rate - chance * sk.accuracy / 100f) < 12f, $"{sk.skillName}: 발동률 {rate:0}% (기대 약 {chance}%)");
                notes.Add($"{sk.skillName} {rate:0}%");
            }
            return string.Join(", ", notes);
        }

        // ④ ─────────────────────────────
        static string TestAi(List<PalData> pals, List<SkillData> supports, SkillData weak, System.Random rng)
        {
            var focus = supports.First(s => s.name == "Focus_Up");
            var photo = supports.First(s => s.name == "Photosynthesis");
            var fear = supports.First(s => s.name == "Fear_Gaze");

            // 상대가 단단하고 내 공격이 약하면, HP가 많을 때 기합 모으기를 쓴다
            int setups = 0;
            for (int i = 0; i < 50; i++)
            {
                var me = Make(pals[rng.Next(pals.Count)], 20, rng, weak, focus);
                var foe = Make(pals[rng.Next(pals.Count)], 45, rng, weak);
                if (EnemyAI.ChooseSkill(me, foe, 1f, rng) == 1) setups++;
            }
            Check(setups >= 35, $"강한 상대에게 버프를 거의 안 씀 ({setups}/50)");

            // HP가 적으면 버프 가치가 크게 줄고, 한계(+6)면 0
            var m = Make(pals[0], 30, rng, weak, focus);
            var f = Make(pals[1], 30, rng, weak);
            float full = EnemyAI.SupportValue(m, f, focus);
            m.TakeDamage(m.MaxHP * 9 / 10);
            float low = EnemyAI.SupportValue(m, f, focus);
            Check(low < full * 0.35f, $"HP 10%인데 버프 가치 {low:0.00} (가득일 때 {full:0.00})");
            m.ModifyStage(StatType.Attack, 6);
            Check(EnemyAI.SupportValue(m, f, focus) <= 0f, "공격 +6인데도 기합 모으기 가치가 있음");

            // 회복: HP가 가득이면 0, 적으면 크다
            var h = Make(pals[2], 30, rng, weak, photo);
            Check(EnemyAI.SupportValue(h, f, photo) <= 0f, "HP가 가득인데 광합성 가치가 있음");
            h.TakeDamage(h.MaxHP * 7 / 10);
            Check(EnemyAI.SupportValue(h, f, photo) > 0.4f, "HP 30%인데 광합성 가치가 작음");

            // 공격 -6인 상대에게 공포의 눈빛은 쓸모없다
            var d = Make(pals[3], 30, rng, weak, fear);
            var g = Make(pals[4], 30, rng, weak);
            g.ModifyStage(StatType.Attack, -6);
            Check(EnemyAI.SupportValue(d, g, fear) <= 0f, "공격 -6 상대에게 디버프 가치가 있음");
            return $"단단한 상대에게 버프 {setups}/50, HP 10% 버프 가치 {low / full:P0}, 회복·한계 판단 정상";
        }

        // ⑤ ─────────────────────────────
        static string TestCharge(List<PalData> pals, SkillData weak, System.Random rng)
        {
            var boss = Make(pals[5 % pals.Count], 50, rng, weak);
            var foe = Make(pals[6 % pals.Count], 50, rng, weak);
            var charge = new ChargeStrike(2, 2f);
            boss.AddEffect(charge);
            var ctx = new BattleContext();
            var fx = new List<BossFx>();
            ctx.PatternFx = (b, k, t) => fx.Add(k);
            charge.OnBattleStart(boss, ctx);
            charge.OnTurnEnd(boss, ctx);
            Check(!charge.Charged, "1턴 만에 모음 (2턴마다여야 함)");
            charge.OnTurnEnd(boss, ctx);
            Check(charge.Charged && fx.Contains(BossFx.Charge), "2턴째에 모으지 않음");
            Check(Battle.IsCharging(boss), "정보창 표시용 IsCharging이 false");

            var info = BattleCalc.CalculateDamage(boss, foe, weak, rng);
            Check(Mathf.Approximately(info.ExtraMultiplier, 2f), $"모은 공격 배율 {info.ExtraMultiplier}");
            charge.OnAfterAttack(boss, info, ctx);
            Check(!charge.Charged && fx.Contains(BossFx.Release), "터뜨린 뒤에도 모은 상태");
            var normal = BattleCalc.CalculateDamage(boss, foe, weak, rng);
            Check(Mathf.Approximately(normal.ExtraMultiplier, 1f), "터뜨린 뒤에도 배율이 남음");

            // 급소에 맞으면 흐트러진다
            charge.OnTurnEnd(boss, ctx); charge.OnTurnEnd(boss, ctx);
            Check(charge.Charged, "다시 모으지 않음");
            charge.OnDamaged(boss, new DamageInfo { Attacker = foe, Defender = boss, Skill = weak, IsCritical = false, TypeMultiplier = 1f }, ctx);
            Check(charge.Charged, "보통 공격에 흐트러짐");
            charge.OnDamaged(boss, new DamageInfo { Attacker = foe, Defender = boss, Skill = weak, IsCritical = true, TypeMultiplier = 1f }, ctx);
            Check(!charge.Charged && fx.Contains(BossFx.ChargeBroken), "급소에 맞아도 흐트러지지 않음");
            return "2턴마다 모음 → 다음 공격 2배 → 해제, 급소에 흐트러짐 정상";
        }

        // ⑥ ─────────────────────────────
        static string TestAttach(List<PalData> pals)
        {
            var notes = new List<string>();
            var any = pals[0];
            int Count(BossKind kind, int wave, PalData data = null)
            {
                var p = new Pal(data ?? any, 50);
                BossPattern.Attach(p, kind, wave);
                return p.Effects.OfType<BossPattern>().Count();
            }
            Check(Count(BossKind.None, 5) == 0, "일반 팰에 패턴이 붙음");
            Check(Count(BossKind.Boss, 10) == 1 && Count(BossKind.Boss, 50) == 2 && Count(BossKind.Boss, 100) == 3, "일반 보스 패턴 수");

            var expect = new Dictionary<PalType, Type>
            {
                [PalType.Fire] = typeof(BurnAura), [PalType.Grass] = typeof(Regenerate), [PalType.Ice] = typeof(PeriodicCurse),
                [PalType.Ground] = typeof(EarthArmor), [PalType.Electric] = typeof(Accelerate), [PalType.Dragon] = typeof(Enrage),
                [PalType.Dark] = typeof(PeriodicCurse),
            };
            foreach (var (wave, asset) in GameConfig.LegendKings)
            {
                var data = pals.FirstOrDefault(p => p.name == asset);
                if (data == null) { notes.Add($"{asset} 없음"); continue; }
                var p = new Pal(data, 60);
                BossPattern.Attach(p, BossKind.Legend, wave);
                var patterns = p.Effects.OfType<BossPattern>().ToList();
                Check(patterns.Count >= 2, $"{data.palName}: 패턴 {patterns.Count}개");
                if (expect.TryGetValue(data.primaryType, out var t))
                    Check(patterns.Any(x => x.GetType() == t), $"{data.palName}: 고유 패턴({t.Name}) 없음");
                notes.Add($"{data.palName}[{string.Join("·", patterns.Select(x => x.Name))}]");
            }
            var zero = pals.FirstOrDefault(p => p.name == GameConfig.FinalBossAsset);
            if (zero != null)
            {
                var z = new Pal(zero, 100);
                BossPattern.Attach(z, BossKind.Final, GameConfig.FinalWave);
                Check(z.Effects.OfType<BossPattern>().Count() == 4, "제로버스 패턴 4개가 아님");
                Check(z.Skills.Any(s => s.Data.category == SkillCategory.Status), "제로버스가 변화 기술(공허의 침식)을 안 들고 나옴");
                notes.Add($"{zero.palName}[{string.Join("·", z.Effects.OfType<BossPattern>().Select(x => x.Name))}]");
            }
            return string.Join(" ", notes);
        }

        // ⑦ ─────────────────────────────
        static string TestBossBattles(List<PalData> pals, System.Random rng)
        {
            var normal = pals.Where(p => !GameConfig.IsLegendary(p)).ToList();
            var bosses = new List<(PalData data, BossKind kind, int wave)>();
            foreach (var (wave, asset) in GameConfig.LegendKings)
            {
                var d = pals.FirstOrDefault(p => p.name == asset);
                if (d != null) bosses.Add((d, BossKind.Legend, wave));
            }
            var zero = pals.FirstOrDefault(p => p.name == GameConfig.FinalBossAsset);
            if (zero != null) bosses.Add((zero, BossKind.Final, GameConfig.FinalWave));
            bosses.Add((normal[rng.Next(normal.Count)], BossKind.Boss, 30));
            bosses.Add((normal[rng.Next(normal.Count)], BossKind.Boss, 110));

            var fxCount = new Dictionary<BossFx, int>();
            int battles = 0, wins = 0, turnsTotal = 0, supportUses = 0;
            foreach (var (data, kind, wave) in bosses)
            {
                for (int round = 0; round < 6; round++)
                {
                    int level = kind == BossKind.Final ? 100 : Mathf.Clamp(wave / 2 + 15, 1, 100);
                    var boss = new Pal(data, level, rng);
                    if (kind != BossKind.Boss) boss.AddEffect(new BossAura("기운", 1.25f, 1.5f));
                    boss.Heal(boss.MaxHP);
                    boss.MakeBoss(GameConfig.BossSegments(kind, level));
                    BossPattern.Attach(boss, kind, wave);
                    var party = Enumerable.Range(0, 4).Select(_ => new Pal(normal[rng.Next(normal.Count)], Mathf.Min(100, level + 5), rng)).ToList();

                    var b = new Battle(party, new List<Pal> { boss }, new System.Random(rng.Next()), null, true, kind);
                    b.EnemySmartness = 1f;
                    int turns = 0;
                    while (turns < 150 && (b.State == BattleState.Ongoing || b.State == BattleState.WaitingPlayerSwitch))
                    {
                        if (b.State == BattleState.WaitingPlayerSwitch)
                        {
                            int next = Enumerable.Range(0, b.PlayerParty.Count).First(i => !b.PlayerParty[i].IsFainted);
                            Check(b.SwitchAfterFaint(next), "교체 실패");
                        }
                        else
                        {
                            int pick = EnemyAI.ChooseSkill(b.Player, b.Enemy, 0.8f, rng);
                            Check(b.ExecuteTurn(BattleAction.Skill(pick)), "턴 진행 실패");
                            turns++;
                        }
                        foreach (var e in Drain(b))
                        {
                            if (e.Type == BattleEventType.BossPattern) fxCount[e.Fx] = fxCount.TryGetValue(e.Fx, out var c) ? c + 1 : 1;
                            if (e.Type == BattleEventType.SkillAnim && !e.IsPlayerSide && e.Skill.category == SkillCategory.Status) supportUses++;
                        }
                    }
                    battles++;
                    turnsTotal += turns;
                    if (b.State == BattleState.PlayerWon) wins++;
                    b.Detach();
                }
            }
            Check(fxCount.TryGetValue(BossFx.Charge, out var charges) && charges > 0, "힘 모으기가 한 번도 안 나옴");
            Check(fxCount.ContainsKey(BossFx.Release), "모은 힘을 한 번도 안 터뜨림");
            string fx = string.Join(", ", fxCount.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}"));
            return $"{battles}판 (승 {wins}, 평균 {turnsTotal / Mathf.Max(1, battles)}턴), 보스 변화 기술 {supportUses}번, 패턴: {fx}";
        }
    }
}
