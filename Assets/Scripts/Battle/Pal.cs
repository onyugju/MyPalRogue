using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    public enum StatType { Attack, Defense, SpAttack, SpDefense, Speed }

    /// <summary>팰이 실제로 들고 있는 스킬 (남은 PP 포함)</summary>
    public class SkillInstance
    {
        public SkillData Data { get; }
        public int PP;
        public int MaxPP => Data.maxPP;
        public bool HasPP => PP > 0;

        public SkillInstance(SkillData data) { Data = data; PP = data.maxPP; }
    }

    /// <summary>
    /// 런(Run) 중에 실제로 존재하는 팰 한 마리. PalData는 "원본 도감", Pal은 "내 파티의 개체".
    /// </summary>
    public class Pal
    {
        public const int MaxSkills = 4;
        const int MinStage = -6, MaxStage = 6;
        const int FixedIV = 15;   // 개체값을 따로 두지 않고 모두 같은 값으로 고정

        public PalData Data { get; }
        public int Level { get; private set; }
        public int MaxHP { get; private set; }
        public int CurrentHP { get; private set; }
        public bool IsFainted => CurrentHP <= 0;

        public List<SkillInstance> Skills { get; } = new();
        public List<BattleEffect> Effects { get; } = new();

        readonly Dictionary<StatType, int> rawStats = new();
        readonly Dictionary<StatType, int> stages = new();

        public Pal(PalData data, int level)
        {
            Data = data;
            Level = Mathf.Clamp(level, 1, GameConfig.MaxLevel);
            foreach (StatType t in Enum.GetValues(typeof(StatType))) stages[t] = 0;
            RecalculateStats();
            CurrentHP = MaxHP;
            AutoLearnSkills();
        }

        // ───────── 능력치 ─────────
        // 포켓몬 공식에서 노력치를 뺀 단순 버전
        //  HP   = (2*종족값 + 15) * Lv / 100 + Lv + 10
        //  기타 = (2*종족값 + 15) * Lv / 100 + 5
        static int CalcHP(int baseValue, int level) => (2 * baseValue + FixedIV) * level / 100 + level + 10;
        static int CalcOther(int baseValue, int level) => (2 * baseValue + FixedIV) * level / 100 + 5;

        public void RecalculateStats()
        {
            int oldMax = MaxHP;
            MaxHP = CalcHP(Data.baseHP, Level);
            rawStats[StatType.Attack]    = CalcOther(Data.baseAttack, Level);
            rawStats[StatType.Defense]   = CalcOther(Data.baseDefense, Level);
            rawStats[StatType.SpAttack]  = CalcOther(Data.baseSpAttack, Level);
            rawStats[StatType.SpDefense] = CalcOther(Data.baseSpDefense, Level);
            rawStats[StatType.Speed]     = CalcOther(Data.baseSpeed, Level);

            // 레벨업으로 최대 HP가 늘면 늘어난 만큼 현재 HP도 회복 (포켓몬과 동일)
            if (oldMax > 0 && !IsFainted) CurrentHP = Mathf.Clamp(CurrentHP + (MaxHP - oldMax), 1, MaxHP);
        }

        /// <summary>전투에서 실제로 쓰는 능력치 = 기본 × 단계 보정 × 효과 배율</summary>
        public int GetStat(StatType stat)
        {
            int stage = stages[stat];
            float stageMult = stage >= 0 ? (2f + stage) / 2f : 2f / (2f - stage);
            float effectMult = 1f;
            foreach (var e in EffectsSnapshot()) effectMult *= e.StatMultiplier(stat);
            return Mathf.Max(1, Mathf.FloorToInt(rawStats[stat] * stageMult * effectMult));
        }

        public int GetStage(StatType stat) => stages[stat];

        /// <summary>능력치 단계를 올리거나 내린다 (-6 ~ +6). 실제로 바뀐 만큼을 반환.</summary>
        public int ModifyStage(StatType stat, int delta)
        {
            int before = stages[stat];
            stages[stat] = Mathf.Clamp(before + delta, MinStage, MaxStage);
            return stages[stat] - before;
        }

        public void ResetBattleState()
        {
            foreach (StatType t in Enum.GetValues(typeof(StatType))) stages[t] = 0;
        }

        // ───────── HP ─────────
        public int TakeDamage(int amount)
        {
            int dealt = Mathf.Clamp(amount, 0, CurrentHP);
            CurrentHP -= dealt;
            return dealt;
        }

        public int Heal(int amount)
        {
            if (IsFainted) return 0;
            int healed = Mathf.Clamp(amount, 0, MaxHP - CurrentHP);
            CurrentHP += healed;
            return healed;
        }

        /// <summary>기절한 상태에서도 HP를 되살린다 (부활 효과용)</summary>
        public void Revive(int hp) => CurrentHP = Mathf.Clamp(hp, 1, MaxHP);

        // ───────── 스킬 ─────────
        /// <summary>현재 레벨까지 배운 스킬 중 최근에 배운 4개를 장착 (포켓몬 방식)</summary>
        public void AutoLearnSkills()
        {
            Skills.Clear();
            var picks = Data.learnset
                .Where(e => e.skill != null && e.level <= Level)
                .OrderByDescending(e => e.level).ThenBy(e => e.skill.name)
                .Select(e => e.skill).Distinct().Take(MaxSkills);
            foreach (var s in picks) Skills.Add(new SkillInstance(s));
        }

        /// <summary>스킬 추가. 4개가 꽉 찼으면 replaceIndex 위치의 스킬을 교체한다.</summary>
        public bool TryLearnSkill(SkillData skill, int replaceIndex = -1)
        {
            if (skill == null || Skills.Any(s => s.Data == skill)) return false;
            if (Skills.Count < MaxSkills) { Skills.Add(new SkillInstance(skill)); return true; }
            if (replaceIndex < 0 || replaceIndex >= Skills.Count) return false;
            Skills[replaceIndex] = new SkillInstance(skill);
            return true;
        }

        // ───────── 레벨업 ─────────
        /// <summary>
        /// 1레벨 올리고 능력치를 다시 계산한다. 이 레벨에 새로 배울 수 있는 스킬 목록을 반환.
        /// 강화 효과 선택 여부는 GameConfig.IsPerkLevel(Level)로 호출하는 쪽에서 판단한다.
        /// </summary>
        public List<SkillData> LevelUp()
        {
            if (Level >= GameConfig.MaxLevel) return new List<SkillData>();
            Level++;
            RecalculateStats();
            return Data.learnset.Where(e => e.skill != null && e.level == Level).Select(e => e.skill).ToList();
        }

        // ───────── 효과 ─────────
        public void AddEffect(BattleEffect effect) { if (effect != null) Effects.Add(effect); }
        public void RemoveEffect(BattleEffect effect) => Effects.Remove(effect);

        /// <summary>훅 실행 중에 효과가 추가/제거돼도 안전하도록 복사본을 돌린다</summary>
        public BattleEffect[] EffectsSnapshot() => Effects.ToArray();
    }
}
