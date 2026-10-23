using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    /// <summary>이번 웨이브 상대의 종류</summary>
    public enum BossKind
    {
        None,    // 일반 야생 팰
        Boss,    // 10웨이브마다 나오는 보스
        Legend,  // 20웨이브마다 확률로 나오는 속성의 왕 (전설 팰)
        Final,   // 마지막 웨이브의 최종 보스 제로버스
    }

    /// <summary>
    /// 한 번의 모험(런). 포켓로그처럼 웨이브를 하나씩 이겨 나가며, 파티 상태가 다음 웨이브로 그대로 이어진다.
    ///  - 이어지는 것: HP, 상태이상, 기절, PP, 레벨·경험치, 강화 효과, 스피어 개수, 돈
    ///  - 매 웨이브 초기화: 능력치 단계 변화(전투 시작 때 Battle이 초기화)
    /// MonoBehaviour가 아닌 일반 클래스라서 UI 없이도 돌릴 수 있다.
    /// </summary>
    public class Run
    {
        public List<Pal> Party { get; private set; }
        public int Wave { get; private set; } = 1;
        public int Money { get; set; } = GameConfig.StartingMoney;
        /// <summary>가진 스피어 개수 (SphereType 순서)</summary>
        public int[] Spheres { get; }
        public System.Random Rng { get; }

        // ───── 아이템으로 쌓이는 런 효과 ─────
        public const int MaxAmuletCoins = 5;
        /// <summary>부적금화 개수 (1개당 받는 돈 +20%)</summary>
        public int AmuletCoins { get; set; }
        public float MoneyMultiplier => 1f + 0.2f * AmuletCoins;
        /// <summary>경험부적들이 더한 경험치 보너스 (0.25 = +25%)</summary>
        public float ExpBonus { get; set; }
        public float ExpMultiplier => 1f + ExpBonus;
        /// <summary>학습장치 개수 (1개당 싸우지 않은 팰도 경험치 20%, 최대 5개)</summary>
        public const int MaxExpShare = 5;
        public int ExpShareCount { get; set; }
        public float ExpShareRatio => 0.2f * ExpShareCount;
        /// <summary>사탕단지 개수 (이상한사탕 1개당 레벨 +1씩 더)</summary>
        public const int MaxCandyJars = 3;
        public int CandyJars { get; set; }
        /// <summary>열매 주머니 개수 (1개당 열매가 남을 확률 30%, 최대 3개 = 90%)</summary>
        public const int MaxBerryPouches = 3;
        public int BerryPouches { get; set; }
        public float BerrySave => 0.3f * BerryPouches;
        /// <summary>황금 스피어로 늘어난 보상 선택지 수</summary>
        public int ExtraRewards { get; set; }

        /// <summary>보스가 나오는 웨이브인지 (보스·전설·최종 보스 모두 포함)</summary>
        public bool IsBossWave => GameConfig.IsBossWave(Wave);
        /// <summary>CreateEnemy로 만든 이번 상대의 종류</summary>
        public BossKind EnemyKind { get; private set; }
        /// <summary>마지막 웨이브까지 모두 이겼는지</summary>
        public bool IsCleared => Wave > GameConfig.FinalWave;
        public string AreaName => GameConfig.AreaName(Mathf.Min(Wave, GameConfig.FinalWave));
        /// <summary>파티가 전멸했는지</summary>
        public bool IsWiped => Party.All(p => p.IsFainted);

        PalData lastEnemy;   // 같은 팰이 연달아 나오지 않게

        /// <summary>트레이너 ID (능력치 화면의 IDNo.). 모험마다 새로 정해진다</summary>
        public int TrainerId { get; }

        public Run(IEnumerable<Pal> party, int[] startingSpheres, System.Random rng = null)
        {
            Party = party.ToList();
            Rng = rng ?? new System.Random();
            TrainerId = Rng.Next(10000, 100000);
            Spheres = new int[PalSpheres.Count];
            for (int i = 0; i < Spheres.Length && startingSpheres != null && i < startingSpheres.Length; i++)
                Spheres[i] = Mathf.Max(0, startingSpheres[i]);
        }

        /// <summary>전투에서 바뀐 파티(교체 순서, 잡은 팰, 놓아준 팰)를 런에 반영한다. 잡은 보스의 강화는 떼어 낸다</summary>
        public void SyncParty(IEnumerable<Pal> party)
        {
            Party = party.ToList();
            foreach (var pal in Party)
            {
                foreach (var aura in pal.Effects.OfType<BossAura>().ToList())
                    pal.RemoveEffect(aura);
                foreach (var awakening in pal.Effects.OfType<Awakening>()) awakening.Sleep();   // 전투가 끝나면 각성이 풀린다
            }
        }

        /// <summary>파티 속성 시너지를 다시 계산한다. 바뀐 시너지 알림 문장을 돌려준다</summary>
        public List<string> UpdateSynergies() => Synergies.Apply(Party);

        /// <summary>다음 웨이브로. 랭크업 도구 같은 기간 효과를 1웨이브씩 줄인다</summary>
        /// <summary>파티 전원 완전 회복: 기절 부활, HP 가득, 상태이상 치료, 모든 기술 PP 가득 (10웨이브 보스를 이긴 뒤)</summary>
        public void HealAll()
        {
            foreach (var pal in Party)
            {
                if (pal.IsFainted) pal.Revive(pal.MaxHP);
                else pal.Heal(pal.MaxHP);
                pal.CureStatus();
                foreach (var s in pal.Skills) s.PP = s.MaxPP;
            }
        }

        public void NextWave()
        {
            Wave++;
            foreach (var pal in Party)
                foreach (var boost in pal.Effects.OfType<ITimedEffect>().ToList())
                    if (--boost.WavesLeft <= 0) pal.RemoveEffect((BattleEffect)boost);
        }

        /// <summary>
        /// 이번 웨이브의 상대를 만든다.
        ///  마지막 웨이브: 무조건 최종 보스 제로버스
        ///  20웨이브마다: LegendChance 확률로 그 웨이브의 속성의 왕 (전설 팰, 강화 붙음)
        ///  10웨이브마다(보스): 전설이 아닌 팰 중 능력치 총합 상위 1/3 + 레벨 보너스
        ///  일반 웨이브: 전설이 아닌, 잡을 수 있는 팰. 초반에는 약한 팰만 나오고 점점 강한 팰이 섞인다.
        ///             지역 속성 팰은 2배로 잘 나온다
        /// </summary>
        public Pal CreateEnemy(IReadOnlyList<PalData> pool)
        {
            var all = pool.Where(d => d != null).Distinct().ToList();
            if (all.Count == 0) return null;

            // ① 최종 보스 / 속성의 왕
            if (Wave >= GameConfig.FinalWave)
            {
                var final = all.FirstOrDefault(d => d.name == GameConfig.FinalBossAsset);
                if (final != null) return MakeEnemy(final, BossKind.Final);
            }
            string legendAsset = GameConfig.LegendAssetFor(Wave);
            if (legendAsset != null && Rng.NextDouble() < GameConfig.LegendChance)
            {
                var legend = all.FirstOrDefault(d => d.name == legendAsset);
                if (legend != null) return MakeEnemy(legend, BossKind.Legend);
            }

            // ② 일반 보스 / 야생 팰 (전설 팰은 빼고)
            var normal = all.Where(d => !GameConfig.IsLegendary(d)).ToList();
            if (normal.Count == 0) normal = all;

            List<PalData> candidates;
            if (IsBossWave)
            {
                var ranked = normal.OrderByDescending(StatTotal).ToList();
                candidates = ranked.Take(Mathf.Max(1, ranked.Count / 3)).ToList();
            }
            else
            {
                var wild = normal.Where(d => d.canBeCaught).OrderBy(StatTotal).ToList();
                if (wild.Count == 0) wild = normal.OrderBy(StatTotal).ToList();
                int count = Mathf.Clamp(Mathf.CeilToInt(wild.Count * GameConfig.WildPoolShare(Wave)), Mathf.Min(3, wild.Count), wild.Count);
                candidates = wild.Take(count).ToList();
                var areaType = GameConfig.AreaType(Wave);
                candidates.AddRange(candidates.Where(d => d.HasType(areaType)).ToList());   // 지역 속성은 2배
            }
            if (candidates.Distinct().Count() > 1 && lastEnemy != null) candidates.RemoveAll(d => d == lastEnemy);

            var pick = candidates[Rng.Next(candidates.Count)];
            return MakeEnemy(pick, IsBossWave ? BossKind.Boss : BossKind.None);
        }

        Pal MakeEnemy(PalData data, BossKind kind)
        {
            lastEnemy = data;
            EnemyKind = kind;
            var pal = new Pal(data, GameConfig.EnemyLevel(Wave, kind, Rng), Rng)
            {
                MetWave = Wave, MetArea = AreaName, MetAsBoss = kind != BossKind.None,   // 잡으면 트레이너 메모에 나온다
            };
            if (kind == BossKind.Legend)
                pal.AddEffect(new BossAura("전설의 기운", GameConfig.LegendStatMultiplier, GameConfig.LegendHpMultiplier));
            else if (kind == BossKind.Final)
                pal.AddEffect(new BossAura("공허의 기운", GameConfig.FinalBossStatMultiplier, GameConfig.FinalBossHpMultiplier));
            pal.SetStars(GameConfig.EnemyStars(Wave, kind));    // 깊은 웨이브의 상대는 응축 ★을 달고 나온다
            if (kind == BossKind.Legend || kind == BossKind.Final)                     // 전설·최종 보스는 각성한다
                pal.AddEffect(new Awakening(AwakeningCatalog.ForEnemy(pal), kind == BossKind.Final));
            EnemyLoadout.Equip(pal, this, Wave, kind, Rng);   // 웨이브가 깊을수록 아이템을 많이·강하게 지닌다
            pal.Heal(pal.MaxHP);                             // 최대 HP가 늘어난 만큼 채운다
            if (kind != BossKind.None) pal.MakeBoss(GameConfig.BossSegments(kind, pal.Level));
            BossPattern.Attach(pal, kind, Wave);             // 보스 패턴: 힘 모으기·분노·속성별 고유 패턴
            return pal;
        }

        static int StatTotal(PalData d) => d.baseHP + d.baseAttack + d.baseDefense + d.baseSpeed;
    }

    /// <summary>전설·최종 보스가 받는 강화 (모든 능력치·최대 HP 배율). 잡으면 사라진다</summary>
    public class BossAura : BattleEffect
    {
        readonly float statMult, hpMult;
        public BossAura(string name, float statMult, float hpMult)
        {
            PerkName = name;
            PerkDescription = $"모든 능력치 ×{statMult:0.##}, 최대 HP ×{hpMult:0.##}";
            this.statMult = statMult;
            this.hpMult = hpMult;
        }
        public override float StatMultiplier(StatType stat) => statMult;
        public override float MaxHpMultiplier => hpMult;
    }
}
