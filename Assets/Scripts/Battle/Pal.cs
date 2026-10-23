using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    public enum StatType { Attack, Defense, Speed }

    public static class StatNames
    {
        public static string Korean(StatType s) => s switch
        {
            StatType.Attack => "공격",
            StatType.Defense => "방어",
            _ => "스피드",
        };
    }

    /// <summary>팰이 실제로 들고 있는 스킬 (남은 PP 포함)</summary>
    public class SkillInstance
    {
        public SkillData Data { get; }
        public int PP;
        /// <summary>포인트업/포인트맥스로 늘어난 최대 PP</summary>
        public int BonusPP;
        /// <summary>포인트업을 쓴 횟수 (포인트맥스는 한 번에 끝까지)</summary>
        public int PpUps;
        public const int MaxPpUps = 3;
        public int MaxPP => Data.maxPP + BonusPP;
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
        /// <summary>개체값 최대 (포켓몬과 같은 0~31). 평균이 예전 고정값 15와 비슷하다</summary>
        public const int MaxIV = 31;
        static readonly System.Random ivRandom = new System.Random();

        public PalData Data { get; }
        public int Level { get; private set; }
        public int MaxHP { get; private set; }
        public int CurrentHP { get; private set; }
        public bool IsFainted => CurrentHP <= 0;
        /// <summary>이 팰을 잡은 스피어 (내보낼 때 이 스피어를 던진다). 처음 파티 팰은 팰 스피어</summary>
        public SphereType CaughtSphere { get; set; } = SphereType.Pal;

        public List<SkillInstance> Skills { get; } = new();
        public List<BattleEffect> Effects { get; } = new();

        /// <summary>HP가 바뀔 때마다 (팰, 이전 HP, 이후 HP). 화면의 HP바 연출에 쓴다.</summary>
        public event Action<Pal, int, int> HpChanged;

        /// <summary>현재 상태이상. 교체해도 유지되고, 쓰러지면 풀린다.</summary>
        public StatusCondition Status { get; private set; }

        /// <summary>상태이상이 걸리거나 풀릴 때 (팰, 새 상태). 화면의 상태 뱃지에 쓴다.</summary>
        public event Action<Pal, StatusCondition> StatusChanged;

        readonly Dictionary<StatType, int> rawStats = new();
        readonly Dictionary<StatType, int> stages = new();

        /// <summary>개체값: [0]=HP, [1]=공격, [2]=방어, [3]=스피드 (0~31). 태어날 때 무작위로 정해진다</summary>
        public int[] IVs { get; } = new int[4];
        public int IV(StatType s) => IVs[(int)s + 1];
        public int HpIV => IVs[0];

        // ───────── 만난 기록 (능력치 화면의 트레이너 메모) ─────────
        /// <summary>만난 웨이브 (0 = 모험을 함께 시작한 팰)</summary>
        public int MetWave;
        public int MetLevel;
        public string MetArea;
        /// <summary>보스(보스·전설·최종)로 만났는지</summary>
        public bool MetAsBoss;

        public Pal(PalData data, int level, System.Random rng = null)
        {
            Data = data;
            Level = Mathf.Clamp(level, 1, GameConfig.MaxLevel);
            MetLevel = Level;
            var r = rng ?? ivRandom;
            for (int i = 0; i < IVs.Length; i++) IVs[i] = r.Next(MaxIV + 1);
            foreach (StatType t in Enum.GetValues(typeof(StatType))) stages[t] = 0;
            RecalculateStats();
            CurrentHP = MaxHP;
            AutoLearnSkills();
        }

        // ───────── 능력치 ─────────
        // 포켓몬 공식에서 노력치를 뺀 단순 버전
        //  HP   = (2*종족값 + 15) * Lv / 100 + Lv + 10
        //  기타 = (2*종족값 + 15) * Lv / 100 + 5
        static int CalcHP(int baseValue, int iv, int level) => (2 * baseValue + iv) * level / 100 + level + 10;
        static int CalcOther(int baseValue, int iv, int level) => (2 * baseValue + iv) * level / 100 + 5;

        /// <summary>효과·별을 빼고 레벨·종족값·개체값만으로 낸 최대 HP (능력치 화면 색 비교용)</summary>
        public int BaseMaxHP => CalcHP(Data.baseHP, HpIV, Level);
        /// <summary>효과·별·랭크를 빼고 레벨·종족값·개체값만으로 낸 능력치</summary>
        public int RawStat(StatType s) => rawStats[s];

        /// <summary>응축할 때: 두 팰 중 더 높은 개체값을 이어받는다</summary>
        public void InheritBetterIVs(Pal other)
        {
            if (other == null) return;
            for (int i = 0; i < IVs.Length; i++) IVs[i] = Mathf.Max(IVs[i], other.IVs[i]);
            RecalculateStats();
        }

        /// <summary>기술 두 개의 순서를 바꾼다 (능력치 화면의 "기술 순서 교체")</summary>
        public void SwapSkills(int a, int b)
        {
            if (a < 0 || b < 0 || a >= Skills.Count || b >= Skills.Count || a == b) return;
            (Skills[a], Skills[b]) = (Skills[b], Skills[a]);
        }

        // ───────── 응축 ★ ─────────
        /// <summary>응축 랭크 (0~4). ★ 하나당 모든 능력치 +10%</summary>
        public int Stars { get; private set; }
        public float StarMultiplier => 1f + GameConfig.StarStatBonus * Stars;
        public bool CanCondense => Stars < GameConfig.MaxStars;

        public void SetStars(int stars)
        {
            Stars = Mathf.Clamp(stars, 0, GameConfig.MaxStars);
            RecalculateStats();
        }

        /// <summary>같은 팰을 응축해서 ★ +1. 오르면 true</summary>
        public bool AddStar()
        {
            if (!CanCondense) return false;
            SetStars(Stars + 1);
            return true;
        }

        /// <summary>각성해 있는지 (전투 중)</summary>
        public bool IsAwakened => Effects.OfType<Awakening>().Any(a => a.Active);

        public void RecalculateStats()
        {
            int oldMax = MaxHP;
            float hpMult = StarMultiplier;
            foreach (var e in Effects) hpMult *= e.MaxHpMultiplier;
            MaxHP = Mathf.Max(1, Mathf.RoundToInt(CalcHP(Data.baseHP, HpIV, Level) * hpMult));
            rawStats[StatType.Attack]    = CalcOther(Data.baseAttack, IV(StatType.Attack), Level);
            rawStats[StatType.Defense]   = CalcOther(Data.baseDefense, IV(StatType.Defense), Level);
            rawStats[StatType.Speed]     = CalcOther(Data.baseSpeed, IV(StatType.Speed), Level);

            // 레벨업으로 최대 HP가 늘면 늘어난 만큼 현재 HP도 회복 (포켓몬과 동일)
            if (oldMax > 0 && !IsFainted) CurrentHP = Mathf.Clamp(CurrentHP + (MaxHP - oldMax), 1, MaxHP);
        }

        /// <summary>전투에서 실제로 쓰는 능력치 = 기본 × 단계 보정 × 효과 배율</summary>
        public int GetStat(StatType stat)
        {
            int stage = stages[stat];
            float stageMult = stage >= 0 ? (2f + stage) / 2f : 2f / (2f - stage);
            float effectMult = StarMultiplier;
            foreach (var e in EffectsSnapshot()) effectMult *= e.StatMultiplier(stat);
            if (stat == StatType.Attack && Status == StatusCondition.Burn) effectMult *= 0.5f;      // 화상: 공격 절반
            if (stat == StatType.Speed && Status == StatusCondition.Paralysis) effectMult *= 0.5f; // 마비: 속도 절반
            return Mathf.Max(1, Mathf.FloorToInt(rawStats[stat] * stageMult * effectMult));
        }

        public int GetStage(StatType stat) => stages[stat];

        /// <summary>능력치 단계를 올리거나 내린다 (-6 ~ +6). 실제로 바뀐 만큼을 반환.</summary>
        public int ModifyStage(StatType stat, int delta)
        {
            int before = stages[stat];
            stages[stat] = Mathf.Clamp(before + delta, MinStage, MaxStage);
            int changed = stages[stat] - before;
            if (changed != 0) StageChanged?.Invoke(this, stat, changed);
            return changed;
        }

        public const int StageLimit = MaxStage;

        /// <summary>능력치 단계가 바뀔 때 (팰, 능력치, 바뀐 양). 전투가 화면 연출(화살표)로 보낸다</summary>
        public event Action<Pal, StatType, int> StageChanged;

        /// <summary>떨어진 능력치 단계를 모두 0으로 되돌린다 (보스의 정화). 하나라도 되돌렸으면 true</summary>
        public bool ClearNegativeStages()
        {
            bool any = false;
            foreach (StatType t in Enum.GetValues(typeof(StatType)))
                if (stages[t] < 0) { ModifyStage(t, -stages[t]); any = true; }
            return any;
        }

        public void ResetBattleState()
        {
            foreach (StatType t in Enum.GetValues(typeof(StatType))) stages[t] = 0;
        }

        // ───────── HP ─────────
        public int TakeDamage(int amount)
        {
            int broken = 0;
            if (ShieldsLeft > 0) amount = ClampForShields(amount, out broken);
            int dealt = Mathf.Clamp(amount, 0, CurrentHP);
            int before = CurrentHP;
            CurrentHP -= dealt;
            if (dealt > 0) HpChanged?.Invoke(this, before, CurrentHP);
            if (broken > 0)
            {
                ShieldsLeft -= broken;
                ShieldsBroken?.Invoke(this, broken);
            }
            return dealt;
        }

        // ───────── 보스 실드 (포켓로그 보스 세그먼트) ─────────
        // 보스의 HP바는 BossSegments칸으로 나뉜다. 칸 경계(실드)를 넘는 데미지는 경계에서 멈추고 실드가 깨진다.
        // 아주 큰 데미지(한 칸의 2배, 4배, 8배…)면 실드를 여러 개 한 번에 깰 수 있다 (포켓로그와 같은 규칙).

        /// <summary>HP바 칸 수 (1 = 보스 아님)</summary>
        public int BossSegments { get; private set; } = 1;
        /// <summary>남은 실드 수 (= 지금 칸 위쪽에 남은 경계 수). 0이면 실드 없음</summary>
        public int ShieldsLeft { get; private set; }
        public bool IsBoss => BossSegments > 1;
        /// <summary>실드가 깨졌을 때 (팰, 깨진 개수)</summary>
        public event Action<Pal, int> ShieldsBroken;

        public void MakeBoss(int segments)
        {
            BossSegments = Mathf.Max(1, segments);
            ShieldsLeft = BossSegments - 1;
        }

        /// <summary>보스 상태를 없앤다 (잡아서 내 팰이 되면)</summary>
        public void ClearBoss()
        {
            BossSegments = 1;
            ShieldsLeft = 0;
        }

        /// <summary>shield번째 실드(아래에서부터 1, 2, …)가 있는 HP</summary>
        public int ShieldHp(int shield) => Mathf.RoundToInt(MaxHP * shield / (float)BossSegments);

        int ClampForShields(int amount, out int broken)
        {
            broken = 0;
            int threshold = ShieldHp(ShieldsLeft);
            if (CurrentHP < threshold || CurrentHP - amount > threshold) return amount;   // 이번 공격은 실드에 안 닿음

            float segment = MaxHP / (float)BossSegments;
            int remainder = CurrentHP - threshold;
            int bypassed = 0;
            // 남은 데미지가 한 칸의 2배, 4배, 8배… 이상이면 실드를 하나씩 더 뚫는다 (마지막 칸은 남긴다)
            while (bypassed + 1 < ShieldsLeft && amount - remainder >= Mathf.RoundToInt(segment * Mathf.Pow(2, bypassed + 1)))
                bypassed++;
            broken = 1 + bypassed;
            return Mathf.Max(1, CurrentHP - ShieldHp(ShieldsLeft - bypassed));
        }

        public int Heal(int amount)
        {
            if (IsFainted) return 0;
            int healed = Mathf.Clamp(amount, 0, MaxHP - CurrentHP);
            int before = CurrentHP;
            CurrentHP += healed;
            if (healed > 0) HpChanged?.Invoke(this, before, CurrentHP);
            return healed;
        }

        /// <summary>기절한 상태에서도 HP를 되살린다 (부활 효과용)</summary>
        public void Revive(int hp)
        {
            int before = CurrentHP;
            CurrentHP = Mathf.Clamp(hp, 1, MaxHP);
            if (CurrentHP != before) HpChanged?.Invoke(this, before, CurrentHP);
        }

        // ───────── 상태이상 ─────────
        /// <summary>이 상태이상에 지금 걸릴 수 있는지 (이미 다른 상태이거나, 쓰러졌거나, 속성 면역이면 false)</summary>
        public bool CanReceiveStatus(StatusCondition s)
        {
            if (s == StatusCondition.None || IsFainted || Status != StatusCondition.None) return false;
            var immune = StatusRules.ImmuneType(s);
            return !(immune.HasValue && Data.HasType(immune.Value));
        }

        /// <summary>상태이상을 건다. 걸리지 않으면 false.</summary>
        public bool TrySetStatus(StatusCondition s)
        {
            if (!CanReceiveStatus(s)) return false;
            Status = s;
            StatusChanged?.Invoke(this, Status);
            return true;
        }

        /// <summary>상태이상을 푼다 (회복 아이템, 얼음이 녹을 때 등). 풀린 게 있으면 true.</summary>
        public bool CureStatus()
        {
            if (Status == StatusCondition.None) return false;
            Status = StatusCondition.None;
            StatusChanged?.Invoke(this, Status);
            return true;
        }

        /// <summary>쓰러졌을 때 상태이상을 조용히 지운다 (화면에는 기절 뱃지가 대신 나온다)</summary>
        public void ClearStatusOnFaint() => Status = StatusCondition.None;

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
        // ───────── 경험치 ─────────
        /// <summary>지금 레벨 안에서 모은 경험치 (레벨이 오르면 넘친 만큼만 남는다)</summary>
        public int Exp { get; private set; }
        public int ExpToNext => GameConfig.ExpToNext(Level);
        /// <summary>지금까지 모은 경험치 합계 (Lv.1부터)</summary>
        public int TotalExp
        {
            get
            {
                int total = Exp;
                for (int l = 1; l < Level; l++) total += GameConfig.ExpToNext(l);
                return total;
            }
        }
        /// <summary>EXP바 채움 비율 (0~1). 만렙이면 0</summary>
        public float ExpRatio => ExpToNext > 0 ? Mathf.Clamp01((float)Exp / ExpToNext) : 0f;
        public bool IsMaxLevel => Level >= GameConfig.MaxLevel;

        /// <summary>
        /// 경험치를 얻는다. 오른 레벨마다 (새 레벨, 그 레벨에 배울 수 있는 스킬들)을 돌려준다.
        /// 스킬 장착이나 강화 효과 고르기는 부른 쪽(화면)에서 처리한다.
        /// </summary>
        public List<(int level, List<SkillData> newSkills)> GainExp(int amount)
        {
            var ups = new List<(int, List<SkillData>)>();
            if (amount <= 0 || IsMaxLevel) return ups;
            Exp += amount;
            while (!IsMaxLevel && Exp >= ExpToNext)
            {
                Exp -= ExpToNext;
                var skills = LevelUp();
                ups.Add((Level, skills));
            }
            if (IsMaxLevel) Exp = 0;
            return ups;
        }

        public List<SkillData> LevelUp()
        {
            if (Level >= GameConfig.MaxLevel) return new List<SkillData>();
            Level++;
            RecalculateStats();
            return Data.learnset.Where(e => e.skill != null && e.level == Level).Select(e => e.skill).ToList();
        }

        // ───────── 효과 ─────────
        public void AddEffect(BattleEffect effect)
        {
            if (effect == null) return;
            Effects.Add(effect);
            RecalculateStats();                 // 최대 HP 같은 상시 효과를 바로 반영
        }

        public void RemoveEffect(BattleEffect effect)
        {
            if (Effects.Remove(effect)) RecalculateStats();
        }

        /// <summary>훅 실행 중에 효과가 추가/제거돼도 안전하도록 복사본을 돌린다</summary>
        public BattleEffect[] EffectsSnapshot() => Effects.ToArray();
    }
}
