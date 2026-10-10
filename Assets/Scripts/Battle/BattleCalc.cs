using System;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    /// <summary>명중 판정 / 데미지 계산 / 데미지 적용</summary>
    public static class BattleCalc
    {
        public const float CritChance = 1f / 16f;
        public const float CritMultiplier = 1.5f;

        public static bool CheckHit(SkillData skill, System.Random rng) =>
            skill.accuracy >= 100 || rng.NextDouble() * 100.0 < skill.accuracy;

        /// <summary>
        /// 포켓몬식 데미지 공식:
        /// 기본 = ((2*Lv/5+2) * 위력 * 공격 / 방어) / 50 + 2
        /// 최종 = 기본 × 상성 × 자속 × 급소 × 난수(0.85~1) × 강화효과 배율 + 강화효과 고정 추가
        /// </summary>
        public static DamageInfo CalculateDamage(Pal attacker, Pal defender, SkillData skill, System.Random rng)
        {
            var info = new DamageInfo { Attacker = attacker, Defender = defender, Skill = skill };
            if (skill.category == SkillCategory.Status || skill.power <= 0) return info;   // 데미지 없음

            bool physical = skill.category == SkillCategory.Physical;
            int atk = attacker.GetStat(physical ? StatType.Attack : StatType.SpAttack);
            int def = defender.GetStat(physical ? StatType.Defense : StatType.SpDefense);

            int levelFactor = (2 * attacker.Level) / 5 + 2;
            info.BaseDamage = (levelFactor * skill.power * atk / def) / 50 + 2;

            info.TypeMultiplier = TypeChart.GetMultiplier(skill.type, defender.Data.primaryType, defender.Data.SecondaryType);
            info.StabMultiplier = TypeChart.GetStab(skill.type, attacker.Data.primaryType, attacker.Data.SecondaryType);
            info.IsCritical = rng.NextDouble() < CritChance;
            info.CritMultiplier = info.IsCritical ? CritMultiplier : 1f;
            info.RandomMultiplier = 0.85f + (float)rng.NextDouble() * 0.15f;

            foreach (var e in attacker.EffectsSnapshot()) e.ModifyOutgoingDamage(attacker, info);
            foreach (var e in defender.EffectsSnapshot()) e.ModifyIncomingDamage(defender, info);

            float dmg = info.BaseDamage * info.TypeMultiplier * info.StabMultiplier
                      * info.CritMultiplier * info.RandomMultiplier * info.ExtraMultiplier;
            info.FinalDamage = Mathf.Max(1, Mathf.FloorToInt(dmg) + info.FlatBonus);
            return info;
        }

        /// <summary>계산된 데미지를 실제로 적용하고 공격 후/피격/기절 훅을 호출한다. 실제로 깎인 HP를 반환.</summary>
        public static int ApplyDamage(DamageInfo info, BattleContext ctx)
        {
            var attacker = info.Attacker;
            var defender = info.Defender;
            int dealt = defender.TakeDamage(info.FinalDamage);

            foreach (var e in attacker.EffectsSnapshot()) e.OnAfterAttack(attacker, info, ctx);
            foreach (var e in defender.EffectsSnapshot()) e.OnDamaged(defender, info, ctx);

            if (defender.IsFainted)
            {
                foreach (var e in defender.EffectsSnapshot())
                    if (e.TryPreventFaint(defender, ctx)) break;      // 부활 등으로 기절이 취소될 수 있음

                if (defender.IsFainted)
                    foreach (var e in defender.EffectsSnapshot()) e.OnFainted(defender, ctx);
            }
            return dealt;
        }
    }
}
