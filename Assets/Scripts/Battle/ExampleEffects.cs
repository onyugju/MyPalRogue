using UnityEngine;

namespace PalRogue
{
    // 강화 효과(퍽)가 어떻게 동작하는지 보여주는 예시. 나중에 PerkData로 대체/확장한다.

    /// <summary>능력치 상시 배율 (예: 공격 +10% → new StatBoost(StatType.Attack, 1.1f))</summary>
    public class StatBoost : BattleEffect
    {
        readonly StatType stat; readonly float multiplier;
        public StatBoost(StatType stat, float multiplier) { this.stat = stat; this.multiplier = multiplier; }
        public override string Name => $"{stat} x{multiplier}";
        public override float StatMultiplier(StatType s) => s == stat ? multiplier : 1f;
    }

    /// <summary>HP가 절반 이하일 때 내가 주는 데미지 +20%</summary>
    public class LowHpBoost : BattleEffect
    {
        public override string Name => "배수의 진";
        public override void ModifyOutgoingDamage(Pal self, DamageInfo info)
        {
            if (self.CurrentHP * 2 <= self.MaxHP) info.ExtraMultiplier *= 1.2f;
        }
    }

    /// <summary>쓰러질 때 딱 한 번 HP 절반으로 부활</summary>
    public class ReviveOnce : BattleEffect
    {
        bool used;
        public override string Name => "불사의 의지";
        public override bool TryPreventFaint(Pal self, BattleContext ctx)
        {
            if (used) return false;
            used = true;
            self.Revive(Mathf.Max(1, self.MaxHP / 2));
            ctx.Log($"{self.Data.palName}(은)는 기력을 되찾았다!");
            return true;
        }
    }
}
