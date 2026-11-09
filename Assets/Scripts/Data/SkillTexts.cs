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
            ["Air_Cannon"] = "압축한 공기 덩어리를 빠르게 쏘아 상대를 퍽 하고 때린다.",
            ["Double_Fang"] = "재빠르게 달려들어 위아래 송곳니로 두 번 연달아 물어뜯는다.",
            ["Power_Shot"] = "에너지를 탄환으로 뭉쳐 반동과 함께 쏘아 보낸다.",
            ["Power_Bomb"] = "머리 위에 거대한 에너지 구체를 키운 뒤 내던져 폭발시킨다.",
            ["Air_Blade"] = "부채꼴로 퍼지는 공기 칼날을 날려 상대를 벤다.",
            ["Star_Mine"] = "상대 주위에 별을 흩뿌린 뒤 점점 달아오르게 해 연쇄로 터뜨린다.",
            ["Pal_Blast"] = "온몸의 힘을 모아 굵은 광선으로 내뿜는다.",
            ["Radiant_Barrage"] = "빛의 구체를 띄워 여러 갈래로 갈라지는 빛탄을 퍼붓는다.",
            ["Holy_Burst"] = "상대가 있는 자리를 신성한 빛의 기둥으로 폭발시킨다.",
            ["Rolling_Fluff"] = "솜털 몸을 공처럼 말아 데굴데굴 굴러 부딪친다.",
            ["Meow_Punch"] = "상대를 쫓아가며 양팔로 냥냥 마구 두드린다.",
            ["Lancer_Charge"] = "방패를 앞세우고 빛의 길을 따라 창을 겨눈 채 돌진한다.",
            ["Wild_Fang"] = "날쌔게 달려들어 거칠게 물고 늘어진다.",
            ["Fire_Shot"] = "불씨와 연기를 끄는 화염탄을 쏘아 펑 터뜨린다.",
            ["Spirit_Fire"] = "도깨비불을 쏘아 다섯 갈래로 갈라지게 해 연달아 터뜨린다.",
            ["Fire_Arrow"] = "하늘로 쏜 불화살 셋이 휘어 내려와 꽂히며 불기둥을 일으킨다.",
            ["Fire_Breath"] = "불씨를 빨아들였다가 뜨거운 화염을 길게 내뿜는다.",
            ["Flame_Storm"] = "양옆에 불 회오리를 일으켜 상대 자리에서 하나로 합쳐 터뜨린다.",
            ["Flame_Wall"] = "상대 발밑 땅을 갈라 불길의 벽을 솟구치게 한다.",
            ["Flame_Panel"] = "몸 둘레에 불덩이를 잔뜩 띄워 기관총처럼 쏟아붓는다.",
            ["Eruption"] = "하늘로 화산탄을 쏘아 올려 상대 주변에 쏟아지게 한다.",
            ["Inferno"] = "상대 둘레의 땅을 달궈 불기둥을 연쇄로 터뜨리고 크게 폭발시킨다.",
            ["Fireball"] = "머리 위에 거대한 불덩이를 키워 내던져 큰 폭발을 일으킨다.",
            ["Burning_Horn"] = "뿔에 불을 붙이고 돌진해 불꽃 초승달로 올려친다.",
            ["Flame_Charge"] = "불의 고리를 두르고 돌진해 지나간 자리를 불태우며 들이받는다.",
            ["Volcanic_Strike"] = "잔상을 끌며 파고든 뒤 멈춘 자리에서 불기둥과 용암을 솟구치게 한다.",
            ["Water_Jet"] = "작은 물탄 세 발을 빠르게 연달아 쏜다.",
            ["Aqua_Shot"] = "물방울을 빚어 곧게 쏘아 철퍽 터뜨린다.",
            ["Bubble_Shot"] = "흔들흔들 떠가는 거품 여러 개로 상대를 감싸 퐁퐁 터뜨린다.",
            ["Acid_Rain"] = "상대 머리 위에 먹구름을 불러 산성 빗줄기를 퍼붓는다.",
            ["Line_Splash"] = "발밑에서 상대까지 한 줄로 물기둥을 차례로 솟구치게 한다.",
            ["Water_Bomb"] = "머리 위에 커다란 물구슬을 빚어 내던져 큰 물보라를 일으킨다.",
            ["High_Pressure_Jet"] = "고압 물줄기를 꽂은 뒤 맞은 자리에서 두 갈래로 갈라지게 한다.",
            ["Wall_Splash"] = "상대 둘레에 물의 벽을 세 겹 잇달아 세우고 파도로 덮친다.",
            ["Hydro_Slicer"] = "칼날 같은 물줄기로 베어 내린 뒤 벤 자리에서 물을 뿜어 올린다.",
            ["Hydro_Stream"] = "굵은 물줄기를 길게 내뿜어 상대를 쓸어 버린다.",
            ["Aqua_Surge"] = "양옆에서 밀려든 파도가 부딪쳐 한가운데서 물기둥으로 터진다.",
            ["Water_Geyser"] = "상대 둘레에 물기둥을 잇달아 뿜어 올리고 가운데를 가장 높이 솟구치게 한다.",
            ["Tarasonic_Laser"] = "고압 광선으로 세 번 휩쓸고 휩쓴 자리마다 물기둥을 솟게 한다.",
            ["Sea_Serpent"] = "몸에 물줄기를 휘감고 포효한 뒤 파도를 끌고 돌진한다.",
            ["Wind_Blade"] = "눈 깜짝할 새 날아가는 초록 칼바람으로 벤다.",
            ["Seed_Machine_Gun"] = "딱딱한 씨앗을 다다다 연사한다.",
            ["Seed_Mine"] = "씨앗을 상대 발밑에 박아 꽃봉오리로 부풀려 펑 터뜨린다.",
            ["Multi_Cutter"] = "바람 칼날 세 개를 일렬로 날려 같은 곳을 연달아 벤다.",
            ["Grass_Tornado"] = "양옆에 풀잎 회오리를 일으켜 X자로 엇갈려 날려 상대를 휘감는다.",
            ["Wind_Edge"] = "팔을 휘둘러 다섯 갈래로 퍼지는 칼바람을 부채꼴로 날린다.",
            ["Thorn_Vine"] = "땅속 뿌리가 상대를 쫓아가 발밑에서 가시덩굴로 솟구쳐 찌른다.",
            ["Circle_Vine"] = "상대 둘레에 뿌리를 둥글게 솟구치게 한 뒤 한가운데를 가장 큰 뿌리로 찌른다.",
            ["Reflect_Leaf"] = "이리저리 튕기는 칼바람이 지그재그로 오가며 여러 번 벤다.",
            ["Ambush_Blades"] = "상대 둘레 허공에 칼바람을 둘러 띄워 사방에서 한꺼번에 몰아친다.",
            ["Solar_Blast"] = "머리 위에 작은 태양을 띄워 황금빛 광선으로 내리쏜다.",
            ["Cross_Wind"] = "상대 앞에 띄운 커다란 칼바람 둘을 X자로 엇갈려 벤다.",
            ["Wind_Blast"] = "자기 둘레에 거대한 회오리바람을 일으켜 상대 쪽까지 휩쓴다.",
            ["Muscle_Punch"] = "굵은 주먹으로 잽을 퍼붓고 마지막에 크게 휘둘러 날려 버린다.",
            ["Sniping_Shot"] = "조준선을 겹쳐 맞춘 뒤 화살 한 발로 정확히 꿰뚫는다.",
            ["Tail_Whip"] = "꼬리로 땅을 내리쳐 풀이 줄지어 솟는 충격을 상대까지 보낸다.",
            ["Lightning_Spear"] = "번개 창을 만들어 눈 깜짝할 새 꽂아 감전시킨다.",
            ["Spark_Shot"] = "지직거리는 불꽃 다섯 개를 부채꼴로 흩뿌린다.",
            ["Electric_Wave"] = "몸에 모은 전기를 고리 모양 파동으로 사방에 퍼뜨린다.",
            ["Lightning_Ball"] = "천천히 다가가는 번개 덩어리를 들러붙게 해 여러 갈래로 방전시킨다.",
            ["Plasma_Panel"] = "몸 둘레에 띄운 플라즈마 구슬을 전기로 잇고 연발로 쏟아낸다.",
            ["Lock_On_Laser"] = "조준 꺾쇠로 표적을 고정한 뒤 아주 빠른 레이저로 꿰뚫는다.",
            ["Line_Thunder"] = "땅을 타고 지그재그로 내달리는 뇌격을 상대 발밑에서 튀어 오르게 한다.",
            ["Tri_Thunder"] = "상대 머리 위에 먹구름을 불러 벼락을 세 번 떨어뜨린다.",
            ["Tri_Spark"] = "곧게 꽂히는 번개와 휘어 쫓아가는 번개 구슬 둘을 함께 쏜다.",
            ["Thunder_Strike"] = "자기 몸에 벼락을 떨어뜨려 모은 전기로 사방에 방전 폭발을 일으킨다.",
            ["Thunder_Rain"] = "먹구름에 번개를 모았다가 벼락을 비처럼 쏟아붓는다.",
            ["Electric_Bolt"] = "온몸의 전기를 끌어모아 굵은 번개 기둥으로 내다꽂는다.",
            ["Omni_Thunder"] = "여덟 갈래 뇌격이 사방의 땅을 가르며 내달린다.",
            ["Thunder_Rail"] = "전기 레일을 여러 줄 깔고 그 위로 전격을 한꺼번에 쏘아 보낸다.",
            ["Thunderstorm"] = "거대한 번개 회오리를 일으켜 상대 쪽으로 몰아가며 벼락을 떨어뜨린다.",
            ["Execution_Laser"] = "조준을 고정하고 하늘에서 번개 레이저를 연달아 내리꽂는다.",
            ["Electric_Claw"] = "전기 발톱으로 두 번 할퀸 뒤 앞발로 땅을 내리쳐 충격을 퍼뜨린다.",
            ["Thunder_Tank"] = "미니건을 난사한 뒤 총구에 모은 전기를 한 발의 거대 전격으로 쏜다.",
            ["Summon_Thunder"] = "번개를 불러 떨어뜨리고 그 번개 길을 따라 섬광처럼 돌진한다.",
            ["Ice_Missile"] = "허공에 고드름 셋을 만들어 차례로 날려 꽂는다.",
            ["Ice_Blade"] = "초승달 모양 얼음 날을 던져 서리 자국을 남기며 벤다.",
            ["Iceberg"] = "상대 발밑을 얼린 뒤 날카로운 얼음칼을 솟구치게 한다.",
            ["Frost_Breath"] = "눈송이 섞인 냉기를 길게 내뿜어 상대를 얼어붙게 한다.",
            ["Freeze_Wall"] = "땅에서 솟은 두꺼운 얼음 벽을 미끄러뜨려 들이받고 부순다.",
            ["Icicle_Bullet"] = "하늘에 고드름 여섯 개를 부채꼴로 만들어 한 발씩 쏘아 보낸다.",
            ["Icicle_Line"] = "세 갈래로 얼음 기둥을 줄지어 솟구치게 하며 뻗어 나간다.",
            ["Blizzard_Spike"] = "눈보라로 빚은 거대한 얼음덩어리를 던져 서리 충격파를 퍼뜨린다.",
            ["Diamond_Fall"] = "상대 머리 위로 얼음덩어리를 잇달아 떨어뜨리고 마지막에 가장 큰 덩어리로 내리찍는다.",
            ["Double_Blizzard_Spike"] = "거대한 얼음덩어리 둘을 연달아 던져 내리꽂는다.",
            ["Frost_Out"] = "상대 발밑을 중심으로 얼음 기둥을 겹겹의 동심원으로 터뜨린다.",
            ["Crystal_Wing"] = "얼음 결정 날개를 펼치고 질주해 날개로 베어 가른다.",
            ["Snowball_Roll"] = "눈덩이를 굴려 키운 뒤 가장 커졌을 때 던져 터뜨린다.",
            ["Mud_Shot"] = "끈적한 진흙 덩어리를 던져 상대 몸에 철퍽 들러붙게 한다.",
            ["Rock_Burst"] = "돌멩이를 끌어모아 산탄처럼 흩뿌린다.",
            ["Rock_Cannon"] = "땅에서 바위를 뽑아 올려 대포알처럼 쏘아 박살 낸다.",
            ["Sand_Tornado"] = "모래 회오리 둘을 일으켜 상대를 감싸 빙빙 돌며 할퀸다.",
            ["Rock_Blast"] = "발을 굴러 땅을 뒤흔들고 둘레의 암석 덩어리를 사방으로 튀긴다.",
            ["Rock_Spear"] = "땅 밑으로 균열을 뻗어 상대 발밑에서 거대한 바위 창을 솟구치게 한다.",
            ["Sand_Twister"] = "모래 회오리 셋이 상대 둘레를 휘몰아 돌다가 한가운데로 모여 터진다.",
            ["Stone_Beat"] = "거대한 바위를 공중 높이 띄워 올렸다가 머리 위로 내리찍는다.",
            ["Fighting_Slash"] = "입에 문 봉을 휘둘러 위아래로 두 번 후려친다.",
            ["Spin_Leg_Slash"] = "팽이처럼 회전하며 다가가 튼튼한 다리로 크게 돌려찬다.",
            ["Force_Drive"] = "순식간에 파고들어 주먹을 쏟아붓고 마지막 한 방으로 날려 버린다.",
            ["Ground_Smasher"] = "높이 뛰어올랐다가 떨어지며 땅을 내리쳐 바위를 겹겹이 솟게 한다.",
            ["Somersault_Scratch"] = "발톱으로 할퀸 뒤 공중제비를 돌며 뒤로 물러난다.",
            ["Rolling_Scratch"] = "몸을 말아 굴러 파고든 뒤 X자로 할퀸다.",
            ["Blazing_Missile"] = "미사일을 줄줄이 쏘아 올려 상대 둘레에 쏟아지게 해 연쇄 폭발시킨다.",
            ["Boar_Rush"] = "앞발로 땅을 긁어 겨눈 뒤 흙바람을 일으키며 맹렬히 들이받는다.",
            ["Dark_Shot"] = "출발은 느리지만 점점 빨라지는 암흑탄을 쏜다.",
            ["Poison_Shot"] = "독 진흙 덩어리를 던져 독 거품과 독안개를 퍼뜨린다.",
            ["Dark_Orb"] = "둥실둥실 떠가는 어둠 구체를 닿는 순간 터뜨린다.",
            ["Dark_Wave"] = "어둠 구체 다섯을 부채꼴로 퍼뜨린다.",
            ["Dark_Cannon"] = "어둠을 한 점에 꾹 눌러 담아 대포처럼 쏜다.",
            ["Shadow_Burst"] = "발밑 그림자를 부풀려 몸 둘레로 어둠의 힘을 터뜨린다.",
            ["Dark_Arrow"] = "휘어 돌며 상대를 쫓아가는 그림자 화살 넷을 잇달아 쏜다.",
            ["Poison_Shower"] = "독 구슬을 하늘로 흩뿌려 사방에 비처럼 떨어뜨린다.",
            ["Spirit_Flame"] = "원념 서린 도깨비불 셋이 상대를 쫓아가 빙글 돈 뒤 덮친다.",
            ["Nightmare_Ball"] = "어둠의 마법진에서 거대한 어둠 구슬을 불러내 빠르게 쏜다.",
            ["Apocalypse"] = "상대 발밑에 마법진을 새기고 둘레에 어둠의 회오리를 일으킨다.",
            ["Dark_Laser"] = "앞에 세운 마법진으로 어둠을 모아 굵은 광선으로 꿰뚫는다.",
            ["Dark_Wisp"] = "어둠 구체 여덟 개를 소용돌이치게 풀어 상대를 쫓아가 터지게 한다.",
            ["Psycho_Gravity"] = "염동력으로 상대를 허공에 띄웠다가 땅바닥에 내리꽂는다.",
            ["Nightmare_Ray"] = "모습을 감췄다가 상대 위에 나타나 꽃처럼 퍼지는 광선을 쏟아낸다.",
            ["Flame_Waltz"] = "춤추듯 화염 고리를 퍼뜨리고 고리에서 떨어진 불꽃이 상대를 쫓아간다.",
            ["Phantom_Gallop"] = "유령 말처럼 달려 나가며 흔들리는 화염구를 잇달아 날린다.",
            ["Dragon_Cannon"] = "육각 무늬가 도는 용의 에너지탄을 쏜다.",
            ["Dragon_Wave"] = "용의 기운을 끌어올려 포효하며 충격파를 겹겹이 퍼뜨린다.",
            ["Dragon_Breath"] = "비늘 무늬가 일렁이는 용의 숨결을 길게 내뿜는다.",
            ["Comet"] = "하늘 저편에서 운석 하나를 비스듬히 떨어뜨려 충격파를 일으킨다.",
            ["Shockwave_Cannon"] = "에너지탄을 쏘아 맞은 자리에서 앞으로 폭발을 연쇄시킨다.",
            ["Beam_Slicer"] = "용의 광선으로 휩쓸고 지나간 자국을 시간차로 터뜨린다.",
            ["Charge_Cannon"] = "몸이 떨릴 만큼 에너지를 모아 쏘아 맞은 자리를 넓게 폭발시킨다.",
            ["Comet_Fall"] = "운석 셋을 연달아 떨어뜨려 상대 자리를 거듭 내리찍는다.",
            ["Dragon_Meteor"] = "작은 운석 여럿을 불러 포물선을 그리며 상대에게 날린다.",
            ["Meteor_Rain"] = "운석을 비처럼 쏟아붓고 마지막 거대 운석으로 짓누른다.",
            ["Mystic_Hurricane"] = "분홍빛 회오리 둘이 엇갈려 휘돌며 상대를 쫓아가 감아올린다.",
            ["Rocket_Tackle"] = "기운을 모았다가 뒤로 폭발을 일으키며 로켓처럼 들이받는다.",
            ["Meteor_Beam"] = "기운을 끌어올려 양 날개에서 수많은 광선을 쏟아 맞은 곳마다 폭발시킨다.",
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
