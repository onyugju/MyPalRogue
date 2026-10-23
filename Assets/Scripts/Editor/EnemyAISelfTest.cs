using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/21. 상대 AI 자동 테스트
    ///  ① 무작위 상황 2000개: 가장 똑똑할 때 항상 점수 1등 기술을 고르는지, 효과 없는 기술·걸 수 없는 상태이상을 안 쓰는지
    ///  ② 똑똑함별로 "효과가 굉장한 기술"과 "쓰러뜨릴 수 있는 기술"을 얼마나 고르는지 (무작위와 비교)
    ///  ③ 같은 팰끼리 싸움 400판: AI 상대 vs 무작위 상대의 승률
    /// 결과는 Console에 "[AI 테스트]"로 나온다.
    /// </summary>
    public static class EnemyAISelfTest
    {
        [MenuItem("PalRogue/21. 상대 AI 자동 테스트")]
        static void RunAll()
        {
            var pals = AssetDatabase.FindAssets("t:PalData")
                .Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null && p.learnset.Any(e => e.skill != null)).OrderBy(p => p.name).ToList();
            var skills = pals.SelectMany(p => p.learnset).Where(e => e.skill != null).Select(e => e.skill).Distinct().ToList();
            if (pals.Count < 2 || skills.Count < 4) { Debug.LogError("[AI 테스트] PalData/SkillData가 부족합니다."); return; }

            var rng = new System.Random(2024);
            var report = new StringBuilder();
            int pass = 0, fail = 0;
            void Test(string name, Func<string> body)
            {
                try { report.AppendLine($"  ✔ {name}: {body()}"); pass++; }
                catch (Exception e) { report.AppendLine($"  ✘ {name}: {e.GetType().Name} {e.Message}"); fail++; Debug.LogException(e); }
            }

            Test("규칙 (2000개 상황)", () => TestRules(pals, skills, rng));
            Test("똑똑함별 선택", () => TestSmartness(pals, skills, rng));
            Test("승률 (400판)", () => TestWinRate(pals, rng));

            string head = $"[AI 테스트] 통과 {pass} / 실패 {fail}";
            if (fail > 0) Debug.LogError(head + "\n" + report); else Debug.Log(head + "\n" + report);
        }

        /// <summary>무작위 상황: 무작위 팰 둘, 내 기술 4개 무작위, HP·상태이상도 무작위</summary>
        static (Pal me, Pal foe) RandomSituation(List<PalData> pals, List<SkillData> skills, System.Random rng)
        {
            var me = new Pal(pals[rng.Next(pals.Count)], rng.Next(10, 80), rng);
            var foe = new Pal(pals[rng.Next(pals.Count)], Mathf.Clamp(me.Level + rng.Next(-8, 9), 1, 100), rng);
            me.Skills.Clear();
            foreach (var s in skills.OrderBy(_ => rng.Next()).Take(4)) me.Skills.Add(new SkillInstance(s));
            if (rng.NextDouble() < 0.5) foe.TakeDamage(rng.Next(foe.MaxHP));
            if (rng.NextDouble() < 0.25) foe.TrySetStatus((StatusCondition)rng.Next(1, 5));
            if (rng.NextDouble() < 0.15) me.Skills[rng.Next(4)].PP = 0;
            return (me, foe);
        }

        static string TestRules(List<PalData> pals, List<SkillData> skills, System.Random rng)
        {
            int immuneAvoid = 0, statusAvoid = 0, koTaken = 0, koChances = 0;
            for (int n = 0; n < 2000; n++)
            {
                var (me, foe) = RandomSituation(pals, skills, rng);
                var options = EnemyAI.Evaluate(me, foe);
                int pick = EnemyAI.ChooseSkill(me, foe, 1f, rng);
                if (options.Count == 0) { if (pick != -1) throw new Exception("PP가 없는데 발버둥이 아님"); continue; }
                var chosen = options.First(o => o.Index == pick);
                float best = options.Max(o => o.Score);
                if (chosen.Score < best - 0.001f) throw new Exception($"1등이 아닌 기술 ({chosen.Reason} {chosen.Score:0} < {best:0})");
                if (!me.Skills[pick].HasPP) throw new Exception("PP가 없는 기술을 고름");

                bool hasGood = options.Any(o => o.Score > 0f);
                var data = me.Skills[pick].Data;
                if (hasGood && data.category != SkillCategory.Status &&
                    TypeChart.GetMultiplier(data.type, foe.Data.primaryType, foe.Data.SecondaryType) <= 0f)
                    throw new Exception($"효과 없는 기술 {data.skillName}을 고름");
                if (hasGood && data.category == SkillCategory.Status && data.statusEffect != StatusCondition.None
                    && !foe.CanReceiveStatus(data.statusEffect) && EnemyAI.SupportValue(me, foe, data) <= 0f)
                    throw new Exception($"걸 수 없는 상태이상 기술 {data.skillName}을 고름 (상대 {foe.Status})");
                if (options.Any(o => o.Score <= 0f && me.Skills[o.Index].Data.category == SkillCategory.Status)) statusAvoid++;
                if (options.Any(o => o.Reason.StartsWith("효과 없음"))) immuneAvoid++;
                if (options.Any(o => o.CanKo)) { koChances++; if (chosen.CanKo) koTaken++; }
            }
            if (koChances > 0 && koTaken != koChances) throw new Exception($"쓰러뜨릴 수 있는데 안 함 {koChances - koTaken}번");
            return $"항상 1등 기술 / 효과 없는 기술 피함 {immuneAvoid}번 / 못 거는 상태이상 피함 {statusAvoid}번 / 막타 {koTaken}/{koChances}";
        }

        static string TestSmartness(List<PalData> pals, List<SkillData> skills, System.Random rng)
        {
            var sb = new List<string>();
            float lastBestRate = -1f;
            foreach (float smart in new[] { 0f, 0.3f, 0.6f, 0.9f, 1f })
            {
                int bestPicks = 0, seChances = 0, sePicks = 0, total = 0;
                var r = new System.Random(77);                                   // 똑똑함마다 같은 상황
                for (int n = 0; n < 1500; n++)
                {
                    var (me, foe) = RandomSituation(pals, skills, r);
                    var options = EnemyAI.Evaluate(me, foe);
                    if (options.Count < 2) continue;
                    int pick = EnemyAI.ChooseSkill(me, foe, smart, rng);
                    float best = options.Max(o => o.Score);
                    total++;
                    if (options.First(o => o.Index == pick).Score >= best - 0.001f) bestPicks++;
                    bool SuperEffective(int i)
                    {
                        var d = me.Skills[i].Data;
                        return d.category != SkillCategory.Status && TypeChart.GetMultiplier(d.type, foe.Data.primaryType, foe.Data.SecondaryType) > 1f;
                    }
                    if (options.Any(o => SuperEffective(o.Index)) && !options.All(o => SuperEffective(o.Index)))
                    {
                        seChances++;
                        if (SuperEffective(pick)) sePicks++;
                    }
                }
                float bestRate = bestPicks * 100f / Mathf.Max(1, total);
                if (bestRate + 3f < lastBestRate) throw new Exception($"똑똑함 {smart}인데 1등 선택률이 내려감 ({bestRate:0}%)");
                lastBestRate = bestRate;
                sb.Add($"{smart:0.#}: 1등 {bestRate:0}% · 효과굉장 {sePicks * 100f / Mathf.Max(1, seChances):0}%");
            }
            return string.Join(" / ", sb);
        }

        static string TestWinRate(List<PalData> pals, System.Random rng)
        {
            var normal = pals.Where(p => !GameConfig.IsLegendary(p)).ToList();
            int aiWins = 0, randomWins = 0, games = 200;
            for (int g = 0; g < games; g++)
            {
                var a = normal[rng.Next(normal.Count)];
                var b = normal[rng.Next(normal.Count)];
                int seed = rng.Next();
                if (Fight(a, b, 1f, seed)) aiWins++;          // 같은 대진·같은 난수로 AI / 무작위 비교
                if (Fight(a, b, 0f, seed)) randomWins++;
            }
            float ai = aiWins * 100f / games, rand = randomWins * 100f / games;
            if (ai < rand) throw new Exception($"AI 승률({ai:0}%)이 무작위({rand:0}%)보다 낮음");
            return $"상대 승률: AI {ai:0}% / 무작위 {rand:0}% (내 쪽은 무작위로 기술 사용, 같은 대진)";
        }

        /// <summary>상대가 이기면 true. 내 팰은 무작위로 기술을 쓴다</summary>
        static bool Fight(PalData mine, PalData theirs, float smart, int seed)
        {
            var rng = new System.Random(seed);
            var me = new Pal(mine, 30, rng);
            var foe = new Pal(theirs, 30, rng);
            var battle = new Battle(new List<Pal> { me }, new List<Pal> { foe }, rng) { EnemySmartness = smart };
            for (int t = 0; t < 60 && battle.State == BattleState.Ongoing; t++)
            {
                while (battle.TryDequeueEvent(out _)) { }
                var usable = me.Skills.Select((s, i) => (s, i)).Where(x => x.s.HasPP).Select(x => x.i).ToList();
                battle.ExecuteTurn(BattleAction.Skill(usable.Count == 0 ? -1 : usable[rng.Next(usable.Count)]));
            }
            while (battle.TryDequeueEvent(out _)) { }
            battle.Detach();
            return me.IsFainted && !foe.IsFainted;
        }
    }
}
