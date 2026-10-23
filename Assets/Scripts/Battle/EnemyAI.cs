using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 상대 팰의 기술 고르기.
    /// 기술마다 "이번 턴에 얼마나 이득인가"를 점수로 매기고, 똑똑함(0~1)에 따라 고른다.
    ///  - 공격 기술: 예상 데미지(상성·자속·명중 반영). 쓰러뜨릴 수 있으면 크게 가산, 그중에서도 명중이 높고 선공인 기술을 고른다.
    ///             효과가 없는 기술(상성 0배)은 쓰지 않는다. 얼어 있는 상대에게 불 기술은 녹여 주므로 덜 쓴다.
    ///  - 상태이상: 상대가 이미 상태이상이거나 면역이면 0점. 곧 쓰러뜨릴 수 있으면 굳이 쓰지 않는다.
    ///  - 똑똑함 1 = 항상 가장 좋은 기술, 낮을수록 가끔 엉뚱한 기술을 고른다 (초반 야생 팰).
    /// 똑똑함은 GameConfig.EnemySmartness(웨이브, 보스 종류)로 정한다.
    /// </summary>
    public static class EnemyAI
    {
        /// <summary>한 기술의 점수와 이유 (테스트·디버그용)</summary>
        public struct Option
        {
            public int Index;            // -1 = 발버둥
            public float Score;
            public float Damage;         // 예상 데미지 (명중 반영 전)
            public bool CanKo;
            public string Reason;
        }

        /// <summary>쓸 기술 번호 (PP가 하나도 없으면 -1 = 발버둥)</summary>
        public static int ChooseSkill(Pal me, Pal foe, float smartness, System.Random rng)
        {
            var options = Evaluate(me, foe);
            if (options.Count == 0) return -1;
            if (options.Count == 1) return options[0].Index;

            if (smartness <= 0f) return options[rng.Next(options.Count)].Index;    // 0 = 완전히 무작위 (예전 방식)
            smartness = Mathf.Clamp01(smartness);
            // 가끔은 아무 기술이나 (야생 초반의 "실수")
            if (rng.NextDouble() < (1f - smartness) * 0.35f) return options[rng.Next(options.Count)].Index;

            float best = options.Max(o => o.Score);
            if (best <= 0f) return options[rng.Next(options.Count)].Index;
            if (smartness >= 0.95f) return options.Where(o => o.Score >= best - 0.001f).OrderBy(_ => rng.Next()).First().Index;

            // 점수가 좋을수록 잘 고른다. 똑똑할수록 1등에 몰린다
            float sharp = 2f + 14f * smartness;
            var weights = options.Select(o => Mathf.Exp((o.Score / best - 1f) * sharp)).ToList();
            float roll = (float)rng.NextDouble() * weights.Sum();
            for (int i = 0; i < options.Count; i++)
            {
                if (roll < weights[i]) return options[i].Index;
                roll -= weights[i];
            }
            return options[options.Count - 1].Index;
        }

        /// <summary>PP가 남은 기술마다 점수를 매긴다</summary>
        public static List<Option> Evaluate(Pal me, Pal foe)
        {
            var list = new List<Option>();
            for (int i = 0; i < me.Skills.Count; i++)
            {
                if (!me.Skills[i].HasPP) continue;
                list.Add(Score(me, foe, i, me.Skills[i].Data));
            }
            if (list.Count == 0) return list;

            // 쓰러뜨릴 수 있는 기술이 있으면 상태이상 기술은 쓸 이유가 없다
            if (list.Any(o => o.CanKo))
                for (int i = 0; i < list.Count; i++)
                    if (me.Skills[list[i].Index].Data.category == SkillCategory.Status)
                    {
                        var o = list[i]; o.Score *= 0.1f; o.Reason += ", 쓰러뜨릴 수 있어서 보류"; list[i] = o;
                    }
            return list;
        }

        static Option Score(Pal me, Pal foe, int index, SkillData s)
        {
            var o = new Option { Index = index };
            float acc = s.accuracy >= 100 ? 1f : s.accuracy / 100f;
            int foeHp = Mathf.Max(1, foe.CurrentHP);

            if (s.category == SkillCategory.Status)
            {
                float v = StatusValue(me, foe, s.statusEffect);
                o.Score = v * acc * Mathf.Clamp01(Mathf.Max(s.statusChance, 1) / 100f) * foe.MaxHP;
                o.Reason = v > 0f ? $"{StatusRules.Korean(s.statusEffect)} 걸기" : "걸 수 없음";
                return o;
            }

            float typeMult = TypeChart.GetMultiplier(s.type, foe.Data.primaryType, foe.Data.SecondaryType);
            if (typeMult <= 0f) { o.Reason = "효과 없음"; return o; }

            o.Damage = EstimateDamage(me, foe, s, typeMult);
            float dealt = Mathf.Min(o.Damage, foeHp);                     // 넘치는 데미지는 의미 없음
            o.CanKo = o.Damage >= foeHp;
            o.Score = dealt * acc;
            o.Reason = typeMult > 1f ? "효과가 굉장함" : typeMult < 1f ? "효과가 별로" : "보통";

            if (o.CanKo)
            {
                // 확실히 쓰러뜨리는 기술 우선: 명중률과 선공을 크게 쳐준다
                o.Score = foeHp * (1.5f + acc) + foe.MaxHP * 0.5f + s.priority * foeHp * 0.5f;
                o.Reason += ", 쓰러뜨릴 수 있음";
            }

            // 추가 효과로 상태이상
            if (s.statusEffect != StatusCondition.None && s.statusChance > 0)
                o.Score += StatusValue(me, foe, s.statusEffect) * acc * (s.statusChance / 100f) * foe.MaxHP * 0.5f;

            // 얼어 있는 상대에게 불 기술은 녹여 준다
            if (foe.Status == StatusCondition.Freeze && s.type == PalType.Fire && !o.CanKo)
            {
                o.Score *= 0.5f;
                o.Reason += ", 얼음을 녹임";
            }
            return o;
        }

        /// <summary>
        /// 상대를 이 상태이상으로 만드는 가치 (최대 HP 대비 비율). 못 걸면 0.
        /// 마비·얼음은 행동을 막고, 화상은 공격이 높은 상대에게, 독은 HP가 많은 상대에게 좋다.
        /// </summary>
        static float StatusValue(Pal me, Pal foe, StatusCondition s)
        {
            if (s == StatusCondition.None || !foe.CanReceiveStatus(s)) return 0f;
            float hpLeft = (float)foe.CurrentHP / Mathf.Max(1, foe.MaxHP);
            float v = s switch
            {
                StatusCondition.Freeze => 0.45f,
                StatusCondition.Paralysis => 0.35f,
                StatusCondition.Burn => foe.GetStat(StatType.Attack) >= foe.GetStat(StatType.Defense) ? 0.35f : 0.2f,
                StatusCondition.Poison => 0.3f,
                _ => 0f,
            };
            // 상대 HP가 얼마 안 남았으면 상태이상보다 공격이 낫다
            return v * Mathf.Lerp(0.3f, 1f, hpLeft);
        }

        /// <summary>
        /// 예상 데미지 (난수 평균, 급소 없음). 실제 계산과 같은 공식이지만
        /// 강화 효과의 상태를 바꾸지 않도록 효과 함수는 부르지 않는다.
        /// </summary>
        public static float EstimateDamage(Pal me, Pal foe, SkillData s, float typeMult)
        {
            if (s.category == SkillCategory.Status || s.power <= 0) return 0f;
            int atk = me.GetStat(StatType.Attack);
            int def = Mathf.Max(1, foe.GetStat(StatType.Defense));
            int levelFactor = (2 * me.Level) / 5 + 2;
            float baseDamage = (levelFactor * s.power * atk / def) / 50 + 2;
            float stab = TypeChart.GetStab(s.type, me.Data.primaryType, me.Data.SecondaryType);
            return baseDamage * typeMult * stab * 0.925f;
        }
    }
}
