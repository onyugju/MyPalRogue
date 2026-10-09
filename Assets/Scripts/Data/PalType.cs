namespace PalRogue
{
    /// <summary>팰월드 원작 9개 속성</summary>
    public enum PalType
    {
        Neutral,   // 무
        Fire,      // 불
        Water,     // 물
        Grass,     // 풀
        Electric,  // 번개
        Ground,    // 땅
        Ice,       // 얼음
        Dragon,    // 용
        Dark,      // 어둠
        Void       // 제로버스 전용 고유속성 (상성 없음, 항상 1배). 기존 값이 밀리지 않게 반드시 맨 끝에 둘 것
    }
}