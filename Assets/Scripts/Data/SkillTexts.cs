using System.Collections.Generic;
using System.Linq;

namespace PalRogue
{
    /// <summary>
    /// 기술 설명 (포켓로그처럼: 무엇을 하는지 한 문장 + 추가 효과 문장).
    /// 추가 효과(상태이상·능력치 변화·선제)는 기술 데이터의 실제 값으로 만든다 → 수치를 바꾸면 설명도 같이 바뀐다.
    /// 능력치 화면의 기술 설명과 전투 화면의 C 키 보기가 같이 쓴다.
    /// </summary>
    public static class SkillTexts
    {
        /// <summary>기술 에셋 이름 → 무엇을 하는지 (추가 효과는 빼고)</summary>
        static readonly Dictionary<string, string> Flavor = new Dictionary<string, string>
        {
            // ── 팰월드 기술 (PalworldSkillTable과 같은 설명) ──
            ["Air_Cannon"] = "압축한 공기 덩어리를 빠르게 쏘아 보낸다.",
            ["Double_Fang"] = "재빠르게 달려들어 두 번 연달아 물어뜯는다.",
            ["Power_Shot"] = "에너지를 탄환으로 뭉쳐 쏘아 보낸다.",
            ["Power_Bomb"] = "에너지를 거대한 구체로 뭉쳐 날린다.",
            ["Air_Blade"] = "부채꼴로 퍼지는 공기 칼날을 날린다.",
            ["Star_Mine"] = "상대 주위에 별을 흩뿌려 차례로 터뜨린다.",
            ["Pal_Blast"] = "온몸의 힘을 굵은 광선으로 모아 쏜다.",
            ["Radiant_Barrage"] = "빛의 구체를 띄워 갈라지는 빛탄을 퍼붓는다.",
            ["Holy_Burst"] = "상대가 있는 자리를 신성한 빛으로 폭발시킨다.",
            ["Rolling_Fluff"] = "솜털 몸으로 데굴데굴 굴러 부딪친다.",
            ["Meow_Punch"] = "상대를 쫓아가며 양팔로 마구 두드린다.",
            ["Lancer_Charge"] = "방패를 앞세우고 창을 겨눈 채 돌진한다.",
            ["Wild_Fang"] = "날쌔게 달려들어 물고 늘어진다.",
            ["Fire_Shot"] = "곧게 날아가는 화염탄을 쏜다.",
            ["Spirit_Fire"] = "불구슬을 쏘아 작은 불꽃들로 흩어지게 한다.",
            ["Fire_Arrow"] = "상대를 쫓아가는 불화살 세 발을 쏜다.",
            ["Fire_Breath"] = "뜨거운 화염을 길게 내뿜는다.",
            ["Flame_Storm"] = "양옆에 불 회오리를 일으켜 상대에게 날린다.",
            ["Flame_Wall"] = "상대가 있는 자리에 꺼지지 않는 불길의 벽을 세운다.",
            ["Flame_Panel"] = "수많은 불덩이를 쉴 새 없이 쏘아 댄다.",
            ["Eruption"] = "하늘로 화산탄을 쏘아 올려 주변에 쏟아지게 한다.",
            ["Inferno"] = "상대 주변의 땅을 달궈 잇달아 폭발시킨다.",
            ["Fireball"] = "머리 위에 거대한 불덩이를 만들어 내던진다.",
            ["Burning_Horn"] = "불타는 뿔을 앞세우고 들이받는다.",
            ["Flame_Charge"] = "불길을 두르고 돌진해 지나간 자리를 태운다.",
            ["Volcanic_Strike"] = "돌진한 뒤 멈춘 자리에서 불꽃을 폭발시킨다.",
            ["Water_Jet"] = "빠른 물 탄환을 쏜다.",
            ["Aqua_Shot"] = "곧게 날아가는 물 탄환을 쏜다.",
            ["Bubble_Shot"] = "천천히 따라가는 거품 여러 개를 쏜다.",
            ["Acid_Rain"] = "산성 구름을 만들어 상대 머리 위에 비를 뿌린다.",
            ["Line_Splash"] = "일직선으로 물기둥을 솟구치게 한다.",
            ["Water_Bomb"] = "거대한 물구슬을 불러내 내던진다.",
            ["High_Pressure_Jet"] = "고압 물줄기를 쏘아 좌우로 갈라지게 한다.",
            ["Wall_Splash"] = "물기둥 벽을 잇달아 세운다.",
            ["Hydro_Slicer"] = "압축한 물줄기로 휩쓴 뒤 그 자리에서 물을 뿜게 한다.",
            ["Hydro_Stream"] = "물을 뿜어 넓은 범위를 쓸어 버린다.",
            ["Aqua_Surge"] = "사방에서 밀려든 파도가 한가운데서 물기둥으로 터진다.",
            ["Water_Geyser"] = "상대 주변에 물기둥을 잔뜩 뿜어 올린다.",
            ["Tarasonic_Laser"] = "고압 물줄기로 세 번 휩쓸고 맞은 자리에서 물이 솟는다.",
            ["Sea_Serpent"] = "물을 휘감은 몸으로 포효한 뒤 돌진한다.",
            ["Wind_Blade"] = "아주 빠른 바람 칼날을 곧게 날린다.",
            ["Seed_Machine_Gun"] = "딱딱한 씨앗을 쉴 새 없이 쏘아 댄다.",
            ["Seed_Mine"] = "씨앗을 쏘아 닿는 순간 튀어 오르며 터지게 한다.",
            ["Multi_Cutter"] = "바람 칼날 세 개를 일렬로 날린다.",
            ["Grass_Tornado"] = "양옆에 풀잎 회오리를 일으켜 날린다.",
            ["Wind_Edge"] = "부채꼴로 퍼지는 바람 칼날을 날린다.",
            ["Thorn_Vine"] = "땅속 뿌리가 상대를 쫓아가 발밑에서 찌른다.",
            ["Circle_Vine"] = "상대 자리와 그 둘레에 날카로운 뿌리를 뻗는다.",
            ["Reflect_Leaf"] = "이리저리 튕기는 바람 칼날로 여러 번 벤다.",
            ["Ambush_Blades"] = "바람 칼날을 셀 수 없이 날린다.",
            ["Solar_Blast"] = "태양의 힘을 모아 강한 광선을 쏜다.",
            ["Cross_Wind"] = "상대 앞에 만든 바람 칼날 두 개를 엇갈려 벤다.",
            ["Wind_Blast"] = "자기 둘레에 거대한 회오리바람을 일으킨다.",
            ["Muscle_Punch"] = "굵은 오른팔을 휘둘러 강펀치를 퍼붓는다.",
            ["Sniping_Shot"] = "과녁을 정조준해 화살을 쏜다.",
            ["Tail_Whip"] = "꼬리로 땅을 내리쳐 곧게 뻗는 충격을 보낸다.",
            ["Lightning_Spear"] = "곧게 날아가는 번개 창을 빠르게 던진다.",
            ["Spark_Shot"] = "부채꼴로 번개 덩어리를 흩뿌린다.",
            ["Electric_Wave"] = "몸 둘레로 전기 충격을 퍼뜨린다.",
            ["Lightning_Ball"] = "천천히 따라가는 번개 덩어리를 쏜다.",
            ["Plasma_Panel"] = "번개 덩어리를 셀 수 없이 쏘아 댄다.",
            ["Lock_On_Laser"] = "표적을 고정한 뒤 아주 빠른 번개 레이저를 쏜다.",
            ["Line_Thunder"] = "일직선으로 뇌격을 내달리게 한다.",
            ["Tri_Thunder"] = "상대 머리 위로 벼락을 세 번 떨어뜨린다.",
            ["Tri_Spark"] = "빠른 번개와 쫓아가는 번개를 함께 쏜다.",
            ["Thunder_Strike"] = "자기 몸에 벼락을 떨어뜨려 방전 폭발을 일으킨다.",
            ["Thunder_Rain"] = "번개를 불러 모은 뒤 때가 되면 벼락을 쏟는다.",
            ["Electric_Bolt"] = "강력한 전격을 날린다.",
            ["Omni_Thunder"] = "사방으로 일직선 뇌격을 내달리게 한다.",
            ["Thunder_Rail"] = "수많은 전격을 한꺼번에 쏜다.",
            ["Thunderstorm"] = "거대한 번개 회오리를 만들어 상대 쪽으로 몰아간다.",
            ["Execution_Laser"] = "번개 레이저를 여러 번 내리쏜다.",
            ["Electric_Claw"] = "전기 발톱으로 두 번 덮친 뒤 땅에 충격을 퍼뜨린다.",
            ["Thunder_Tank"] = "미니건을 난사한 뒤 충전한 한 발로 마무리한다.",
            ["Summon_Thunder"] = "번개를 불러낸 뒤 그 사이를 고속으로 돌진한다.",
            ["Ice_Missile"] = "하늘에 만든 얼음을 상대에게 날린다.",
            ["Ice_Blade"] = "초승달 모양 얼음 날을 앞으로 날린다.",
            ["Iceberg"] = "상대 발밑에서 날카로운 얼음칼을 솟구치게 한다.",
            ["Frost_Breath"] = "얼어붙는 냉기를 길게 내뿜는다.",
            ["Freeze_Wall"] = "땅에서 얼음 벽을 솟구치게 해 부딪친다.",
            ["Icicle_Bullet"] = "하늘에 고드름을 여럿 만들어 쏘아 보낸다.",
            ["Icicle_Line"] = "세 갈래로 얼음 기둥을 줄지어 솟구치게 한다.",
            ["Blizzard_Spike"] = "거대한 얼음 덩어리를 날려 충격을 퍼뜨린다.",
            ["Diamond_Fall"] = "상대 머리 위로 얼음덩어리를 잇달아 떨어뜨린다.",
            ["Double_Blizzard_Spike"] = "거대한 얼음 덩어리 두 개를 연달아 날린다.",
            ["Frost_Out"] = "상대 발밑을 중심으로 얼음 기둥을 넓게 뻗친다.",
            ["Crystal_Wing"] = "얼음 날개로 질주하며 상대를 벤다.",
            ["Snowball_Roll"] = "눈덩이를 굴려 가장 커졌을 때 던져 터뜨린다.",
            ["Mud_Shot"] = "끈적한 진흙을 상대에게 쏜다.",
            ["Rock_Burst"] = "작은 돌멩이를 모아 앞으로 흩뿌린다.",
            ["Rock_Cannon"] = "땅에서 바위를 뽑아 쏘아 보낸다.",
            ["Sand_Tornado"] = "양옆에 모래 회오리를 일으켜 날린다.",
            ["Rock_Blast"] = "땅을 뒤흔들어 둘레에 암석 덩어리를 흩뿌린다.",
            ["Rock_Spear"] = "상대 발밑에서 날카로운 바위 창을 솟구치게 한다.",
            ["Sand_Twister"] = "모래 회오리 여러 개를 빙글빙글 휘몰아친다.",
            ["Stone_Beat"] = "땅을 흔들어 거대한 바위를 띄워 떨어뜨린다.",
            ["Fighting_Slash"] = "입에 문 봉으로 두 번 연달아 벤다.",
            ["Spin_Leg_Slash"] = "튼튼한 다리로 크게 돌려차 쓰러뜨린다.",
            ["Force_Drive"] = "순식간에 다가가 강한 펀치를 연타한다.",
            ["Ground_Smasher"] = "높이 뛰어올라 땅을 내리쳐 넓게 충격을 퍼뜨린다.",
            ["Somersault_Scratch"] = "할퀸 뒤 공중제비로 물러난다.",
            ["Rolling_Scratch"] = "몸을 굴리며 빠르게 다가가 할퀸다.",
            ["Blazing_Missile"] = "날개에서 작열 미사일을 셀 수 없이 쏜다.",
            ["Boar_Rush"] = "표적을 정해 맹렬히 들이받는다.",
            ["Dark_Shot"] = "점점 빨라지는 암흑탄을 쏜다.",
            ["Poison_Shot"] = "독이 섞인 진흙을 쏜다.",
            ["Dark_Orb"] = "천천히 따라가는 어둠 구체를 쏜다.",
            ["Dark_Wave"] = "부채꼴로 퍼지는 어둠 구체를 쏜다.",
            ["Dark_Cannon"] = "어둠 에너지 탄환을 빠르게 쏜다.",
            ["Shadow_Burst"] = "어둠의 힘을 풀어 둘레에 충격을 준다.",
            ["Dark_Arrow"] = "상대를 쫓아가는 어둠 화살을 잇달아 쏜다.",
            ["Poison_Shower"] = "사방으로 독 구슬을 흩뿌린다.",
            ["Spirit_Flame"] = "상대를 쫓아가는 원념 셋을 쏜다.",
            ["Nightmare_Ball"] = "거대한 어둠 구슬을 불러내 빠르게 쏜다.",
            ["Apocalypse"] = "상대 둘레에 어둠의 회오리 여럿을 일으킨다.",
            ["Dark_Laser"] = "어둠 에너지를 모아 강한 광선을 쏜다.",
            ["Dark_Wisp"] = "상대를 쫓아가는 어둠 구체를 여럿 쏜다.",
            ["Psycho_Gravity"] = "중력으로 상대를 띄워 땅으로 내던진다.",
            ["Nightmare_Ray"] = "모습을 감췄다 나타나며 꽃처럼 퍼지는 광선을 쏜다.",
            ["Flame_Waltz"] = "둘레에 화염 고리를 일으키고 쫓아가는 불꽃을 쏜다.",
            ["Phantom_Gallop"] = "천천히 달리며 흔들리는 화염구를 계속 날린다.",
            ["Dragon_Cannon"] = "용의 힘이 깃든 에너지탄을 쏜다.",
            ["Dragon_Wave"] = "용의 힘을 풀어 둘레에 충격을 준다.",
            ["Dragon_Breath"] = "용의 숨결을 앞으로 길게 내뿜는다.",
            ["Comet"] = "운석을 곧게 떨어뜨려 충격파를 일으킨다.",
            ["Shockwave_Cannon"] = "에너지탄을 쏘아 맞은 자리에서 앞으로 퍼지는 폭발을 일으킨다.",
            ["Beam_Slicer"] = "용의 광선으로 휩쓸고 지나간 자리를 시간차로 터뜨린다.",
            ["Charge_Cannon"] = "용의 에너지탄을 쏘아 맞은 자리를 넓게 폭발시킨다.",
            ["Comet_Fall"] = "운석을 연달아 떨어뜨려 충격파를 일으킨다.",
            ["Dragon_Meteor"] = "작은 운석 여럿을 불러 상대에게 날린다.",
            ["Meteor_Rain"] = "운석을 셀 수 없이 떨어뜨려 충격파를 일으킨다.",
            ["Mystic_Hurricane"] = "상대를 쫓아가는 회오리 두 개를 날린다.",
            ["Rocket_Tackle"] = "힘을 모았다가 앞으로 돌진한다.",
            ["Meteor_Beam"] = "날개에서 수많은 광선을 쏘아 맞은 곳을 폭발시킨다.",
            // ── 공허 (제로버스) ──
            ["Void_Shot"]  = "공허를 응축한 탄환을 쏜다.",
            ["Void_Pulse"] = "모든 것을 지우는 공허의 파동을 퍼뜨린다.",
            ["Annihilate"] = "공허의 힘을 해방해 닿는 모든 것을 소멸시킨다.",
        };

        /// <summary>기술 설명 전체 (무엇을 하는지 + 추가 효과)</summary>
        public static string Describe(SkillData d)
        {
            if (d == null) return "";
            bool attack = d.category != SkillCategory.Status;
            // 변화 기술은 데이터에 쓴 설명이 효과까지 다 말한다
            if (!attack && !string.IsNullOrEmpty(d.description)) return d.description;

            string main = Flavor.TryGetValue(d.name, out var f) ? f
                        : !string.IsNullOrEmpty(d.description) && !d.description.Contains("확률") ? d.description
                        : attack ? $"{PalTypeNames.Korean(d.type)} 속성의 힘으로 상대를 공격한다." : $"{PalTypeNames.Korean(d.type)} 속성의 변화 기술.";

            var extra = new List<string>();
            if (d.priority > 0) extra.Add("반드시 먼저 공격할 수 있다.");
            if (d.statusEffect != StatusCondition.None && d.statusChance > 0)
                extra.Add($"{Chance(d.statusChance, attack)}상대를 {StatusRules.Korean(d.statusEffect)} 상태로 만든다.");
            if (d.HasStatChanges)
            {
                int chance = attack ? d.statChangeChance : 100;
                foreach (var g in d.statChanges.GroupBy(c => (c.self, c.stages)))
                {
                    string who = g.Key.self ? "자신의" : "상대의";
                    string stats = string.Join("·", g.Select(c => StatNames.Korean(c.stat)));
                    int n = System.Math.Abs(g.Key.stages);
                    string verb = g.Key.stages > 0 ? "올린다" : "떨어뜨린다";
                    extra.Add($"{Chance(chance, attack)}{who} {Josa.Eul(stats)} {n}단계 {verb}.");
                }
            }
            if (d.healPercent > 0) extra.Add($"자신의 HP를 최대 HP의 {d.healPercent}%만큼 회복한다.");
            return extra.Count == 0 ? main : main + " " + string.Join(" ", extra);
        }

        static string Chance(int chance, bool attack) => attack && chance < 100 ? $"{chance}% 확률로 " : "";
    }
}
