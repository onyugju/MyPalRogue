using System;

namespace PalRogue
{
    /// <summary>전투 진행 정보. 효과가 로그를 남기거나 턴 수를 볼 때 쓴다.</summary>
    public class BattleContext
    {
        public int Turn;
        public Action<string> Log = _ => { };
        /// <summary>이 팰의 상대 (미니 블랙홀 등 상대를 건드리는 효과용). 전투가 채운다</summary>
        public Func<Pal, Pal> OpponentOf = _ => null;
        /// <summary>열매를 먹어도 남을 확률 (열매 주머니, 내 팰만). 전투가 채운다</summary>
        public Func<Pal, float> BerrySaveChance = _ => 0f;
    }

    /// <summary>
    /// 데미지 한 번의 계산 결과. 강화 효과는 ExtraMultiplier / FlatBonus 를 건드려서 데미지를 바꾼다.
    /// </summary>
    public class DamageInfo
    {
        public Pal Attacker;
        public Pal Defender;
        public SkillData Skill;

        public int BaseDamage;                    // 보정 곱하기 전 기본 데미지
        public float TypeMultiplier = 1f;         // 속성 상성
        public float StabMultiplier = 1f;         // 자속 보너스
        public float CritMultiplier = 1f;         // 급소
        public float RandomMultiplier = 1f;       // 0.85 ~ 1.0 난수
        public bool IsCritical;

        public float ExtraMultiplier = 1f;        // ← 강화 효과가 곱하는 칸
        public int FlatBonus;                     // ← 강화 효과가 더하는 칸
        public int FinalDamage;

        public string EffectivenessText =>
            TypeMultiplier > 1f ? "효과가 굉장했다!" :
            TypeMultiplier < 1f ? "효과가 별로인 듯하다..." : "";
    }

    /// <summary>
    /// 강화 효과(퍽)·특성·상태이상 등 "전투에 끼어드는 모든 것"의 부모 클래스.
    /// 필요한 훅만 override 하면 된다. 나중에 PerkData(ScriptableObject)가 이 클래스를 만들어 팰에 붙인다.
    /// </summary>
    public abstract class BattleEffect
    {
        public virtual string Name => PerkName ?? GetType().Name;

        /// <summary>강화 효과 고르기 화면에 보이는 설명</summary>
        public virtual string Description => PerkDescription ?? "";

        /// <summary>강화 효과(퍽)로 붙은 효과면 그 퍽의 번호·이름·설명 (PerkCatalog가 채운다). 아니면 null</summary>
        public string PerkId, PerkName, PerkDescription;
        /// <summary>지니는 아이템이면 그 아이템 이름 (훔치기·"지니고 있다" 표시에 쓴다). 지니는 게 아니면 null</summary>
        public string HeldItemId;

        /// <summary>최대 HP 배율 (예: +10% → 1.1f). 팰에 붙이면 바로 최대 HP가 다시 계산된다</summary>
        public virtual float MaxHpMultiplier => 1f;

        /// <summary>이 팰이 받는 경험치 배율 (행복의알 등). 여러 효과가 있으면 곱한다</summary>
        public virtual float ExpMultiplier => 1f;

        /// <summary>항상 적용되는 능력치 배율 (예: 공격 +10% → Attack일 때 1.1f)</summary>
        public virtual float StatMultiplier(StatType stat) => 1f;

        public virtual void OnBattleStart(Pal self, BattleContext ctx) { }
        public virtual void OnTurnStart(Pal self, BattleContext ctx) { }

        /// <summary>내가 공격할 때, 데미지 확정 전에 호출 (내 효과만)</summary>
        public virtual void ModifyOutgoingDamage(Pal self, DamageInfo info) { }

        /// <summary>내가 맞을 때, 데미지 확정 전에 호출 (내 효과만)</summary>
        public virtual void ModifyIncomingDamage(Pal self, DamageInfo info) { }

        public virtual void OnAfterAttack(Pal self, DamageInfo info, BattleContext ctx) { }
        public virtual void OnDamaged(Pal self, DamageInfo info, BattleContext ctx) { }

        /// <summary>기절 직전. 부활 같은 효과는 HP를 회복시키고 true를 반환하면 기절이 취소된다.</summary>
        public virtual bool TryPreventFaint(Pal self, BattleContext ctx) => false;

        public virtual void OnFainted(Pal self, BattleContext ctx) { }
        public virtual void OnTurnEnd(Pal self, BattleContext ctx) { }
    }
}
