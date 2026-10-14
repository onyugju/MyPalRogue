using UnityEngine;

namespace PalRogue
{
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
    }
}