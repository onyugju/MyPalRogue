namespace PalRogue
{
    /// <summary>
    /// 상태이상 (포켓몬식). 한 번에 하나만 걸리고, 교체해도 풀리지 않는다.
    /// 기절(쓰러짐)은 상태이상이 아니라 HP 0 상태라서 여기 없다. 화면에는 기절 뱃지가 따로 나온다.
    /// 숫자 값은 에셋에 저장되므로 순서를 바꾸지 말고, 새 상태는 맨 끝에 추가할 것.
    /// </summary>
    public enum StatusCondition
    {
        None = 0,
        Burn = 1,       // 화상: 턴 끝마다 최대 HP의 1/16 데미지, 공격력 절반
        Freeze = 2,     // 얼음: 행동 불가, 턴마다 20% 확률로 녹음 (불 속성 공격을 맞아도 녹음)
        Paralysis = 3,  // 마비: 속도 절반, 25% 확률로 행동 불가
        Poison = 4,     // 독:   턴 끝마다 최대 HP의 1/8 데미지
    }

    public static class StatusRules
    {
        public const float FreezeThawChance = 0.2f;
        public const float ParalysisSkipChance = 0.25f;
        public const int BurnDamageDivisor = 16;
        public const int PoisonDamageDivisor = 8;

        /// <summary>이 속성의 팰은 이 상태이상에 걸리지 않는다 (불→화상, 얼음→얼음, 번개→마비, 어둠→독)</summary>
        public static PalType? ImmuneType(StatusCondition s) => s switch
        {
            StatusCondition.Burn => PalType.Fire,
            StatusCondition.Freeze => PalType.Ice,
            StatusCondition.Paralysis => PalType.Electric,
            StatusCondition.Poison => PalType.Dark,
            _ => null,
        };

        /// <summary>속성별 공격 스킬의 기본 추가 효과 (메뉴 13에서 사용)</summary>
        public static StatusCondition DefaultForSkillType(PalType t) => t switch
        {
            PalType.Fire => StatusCondition.Burn,
            PalType.Ice => StatusCondition.Freeze,
            PalType.Electric => StatusCondition.Paralysis,
            PalType.Dark => StatusCondition.Poison,
            _ => StatusCondition.None,
        };

        public static string Korean(StatusCondition s) => s switch
        {
            StatusCondition.Burn => "화상",
            StatusCondition.Freeze => "얼음",
            StatusCondition.Paralysis => "마비",
            StatusCondition.Poison => "독",
            _ => "",
        };

        /// <summary>상태이상에 걸렸을 때 메시지 뒷부분 (예: "파이호은(는) 화상을 입었다!")</summary>
        public static string InflictText(StatusCondition s) => s switch
        {
            StatusCondition.Burn => "화상을 입었다!",
            StatusCondition.Freeze => "얼어붙었다!",
            StatusCondition.Paralysis => "마비되어 기술이 나오기 어려워졌다!",
            StatusCondition.Poison => "독에 걸렸다!",
            _ => "",
        };

        /// <summary>Resources/statuses 폴더의 뱃지 이미지 이름</summary>
        public static string BadgeName(StatusCondition s) => s switch
        {
            StatusCondition.Burn => "Burn",
            StatusCondition.Freeze => "Freeze",
            StatusCondition.Paralysis => "Paralysis",
            StatusCondition.Poison => "Poison",
            _ => null,
        };

        public const string FaintedBadgeName = "Fainting";
    }
}
