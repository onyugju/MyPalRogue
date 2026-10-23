using System.Collections.Generic;
using UnityEngine;

namespace PalRogue
{
    /// <summary>기술 하나가 일으키는 능력치 변화 한 줄 (예: 자신 공격 +2, 상대 스피드 -1)</summary>
    [System.Serializable]
    public struct StatChange
    {
        public StatType stat;
        [Range(-3, 3)] public int stages;
        [Tooltip("체크하면 기술을 쓴 팰 자신, 아니면 상대")]
        public bool self;

        public StatChange(StatType stat, int stages, bool self) { this.stat = stat; this.stages = stages; this.self = self; }
    }

    public enum SkillCategory
    {
        Attack = 0,  // 공격 (물리/특수 구분 없음. 공격력·방어력으로 계산)
        Status = 2   // 변화 (버프/디버프/상태이상). 예전 값 1(특수)은 더 이상 쓰지 않는다
    }

    [CreateAssetMenu(fileName = "NewSkill", menuName = "PalRogue/Skill Data")]
    public class SkillData : ScriptableObject
    {
        [Header("기본 정보")]
        public string skillName;
        [TextArea] public string description;
        public PalType type;
        public SkillCategory category;

        [Header("수치")]
        [Min(0)] public int power = 40;       // 변화 기술은 0
        [Range(0, 100)] public int accuracy = 100;
        [Min(1)] public int maxPP = 15;
        [Range(-3, 3)] public int priority = 0; // 높을수록 먼저 행동

        [Header("추가 효과 (상태이상)")]
        public StatusCondition statusEffect = StatusCondition.None;
        [Tooltip("공격 스킬: 맞혔을 때 이 확률(%)로 상태이상. 변화 스킬: 명중하면 이 확률로 상태이상 (보통 100)")]
        [Range(0, 100)] public int statusChance = 0;

        [Header("능력치 변화 (버프·디버프)")]
        public List<StatChange> statChanges = new List<StatChange>();
        [Tooltip("공격 스킬: 맞혔을 때 이 확률(%)로 능력치 변화. 변화 스킬은 항상 일어난다")]
        [Range(0, 100)] public int statChangeChance = 100;

        [Header("회복")]
        [Tooltip("자신의 최대 HP의 몇 %를 회복하는지 (변화 스킬)")]
        [Range(0, 100)] public int healPercent = 0;

        /// <summary>상대에게 아무것도 하지 않는 기술 (자신 버프·회복만): 빗나가지 않고, 연출도 자기 자신에게</summary>
        public bool IsSelfOnly =>
            category == SkillCategory.Status && statusEffect == StatusCondition.None
            && (statChanges == null || statChanges.TrueForAll(c => c.self));

        public bool HasStatChanges => statChanges != null && statChanges.Count > 0;
    }
}