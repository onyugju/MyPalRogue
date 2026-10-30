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
            // ── 무 ──
            ["Air_Cannon"]   = "압축한 공기 덩어리를 쏘아 상대를 공격한다.",
            ["Power_Shot"]   = "힘을 모은 에너지 탄을 상대에게 날린다.",
            ["Power_Bomb"]   = "에너지를 한껏 모아 상대 앞에서 터뜨린다.",
            ["Pal_Blast"]    = "온몸의 힘을 한 줄기 광선으로 모아 상대를 꿰뚫는다.",
            ["Holy_Burst"]   = "신성한 빛을 폭발시켜 주위를 휩쓴다.",
            ["Quick_Strike"] = "눈에 보이지 않는 속도로 파고들어 상대를 친다.",
            // ── 화염 ──
            ["Fire_Shot"]    = "작은 불덩이를 쏘아 상대를 공격한다.",
            ["Spirit_Fire"]  = "영혼처럼 흔들리는 불꽃을 날려 상대를 태운다.",
            ["Fire_Breath"]  = "입에서 뜨거운 불길을 내뿜어 상대를 감싼다.",
            ["Flame_Panel"]  = "발밑에 불꽃 기둥을 줄지어 솟구치게 한다.",
            ["Fireball"]     = "거대한 불덩이를 만들어 상대에게 내던진다.",
            // ── 물 ──
            ["Water_Jet"]    = "세찬 물줄기를 쏘아 상대를 공격한다.",
            ["Bubble_Shot"]  = "단단한 물방울을 연달아 날려 상대를 친다.",
            ["Line_Splash"]  = "물살을 일직선으로 밀어내 상대를 휩쓴다.",
            ["Hydro_Stream"] = "엄청난 수압의 물줄기를 뿜어 상대를 밀어붙인다.",
            ["Water_Geyser"] = "상대의 발밑에서 거대한 물기둥을 터뜨린다.",
            // ── 풀 ──
            ["Wind_Blade"]       = "날카로운 바람의 칼날로 상대를 벤다.",
            ["Seed_Machine_Gun"] = "단단한 씨앗을 기관총처럼 쏟아붓는다.",
            ["Multi_Cutter"]     = "여러 갈래의 잎 칼날을 날려 상대를 벤다.",
            ["Reflect_Leaf"]     = "빛을 머금은 잎을 튕겨 날려 상대를 공격한다.",
            ["Wind_Blast"]       = "잎사귀를 실은 돌풍을 일으켜 상대를 날려 버린다.",
            // ── 번개 ──
            ["Lightning_Spear"] = "번개로 만든 창을 던져 상대를 꿰뚫는다.",
            ["Electric_Wave"]   = "전기를 띤 파장을 퍼뜨려 상대를 공격한다.",
            ["Line_Thunder"]    = "땅을 따라 번개를 일직선으로 내달리게 한다.",
            ["Thunder_Strike"]  = "하늘에서 굵은 번개를 내리꽂는다.",
            ["Thunderstorm"]    = "뇌운을 불러 번개를 비처럼 쏟아붓는다.",
            // ── 땅 ──
            ["Mud_Shot"]    = "진흙 덩어리를 뭉쳐 상대에게 던진다.",
            ["Rock_Burst"]  = "바위를 터뜨려 파편으로 상대를 공격한다.",
            ["Rock_Cannon"] = "커다란 바위를 대포처럼 날려 상대를 친다.",
            ["Rock_Spear"]  = "땅에서 뾰족한 바위 창을 솟아오르게 한다.",
            ["Stone_Beat"]  = "땅을 거세게 내리쳐 바위를 솟구치게 한다.",
            // ── 얼음 ──
            ["Ice_Missile"]  = "뾰족한 얼음 덩어리를 쏘아 상대를 공격한다.",
            ["Ice_Blade"]    = "얼음 칼날을 날려 상대를 벤다.",
            ["Frost_Breath"] = "얼어붙을 듯한 입김을 내뿜어 상대를 감싼다.",
            ["Icicle_Line"]  = "땅을 따라 날카로운 고드름을 줄지어 솟게 한다.",
            ["Diamond_Fall"] = "하늘에서 거대한 얼음 결정을 떨어뜨린다.",
            // ── 용 ──
            ["Dragon_Cannon"] = "용의 기운을 뭉친 탄을 쏘아 상대를 공격한다.",
            ["Dragon_Wave"]   = "용의 힘을 담은 파동을 퍼뜨린다.",
            ["Dragon_Breath"] = "용의 숨결을 내뿜어 상대를 휩쓴다.",
            ["Beam_Slicer"]   = "날카로운 광선을 휘둘러 상대를 베어 낸다.",
            ["Dragon_Meteor"] = "하늘에서 용의 운석을 불러 떨어뜨린다.",
            // ── 어둠 ──
            ["Dark_Shot"]    = "어둠의 기운을 뭉쳐 상대에게 쏜다.",
            ["Shadow_Burst"] = "그림자를 폭발시켜 상대를 덮친다.",
            ["Dark_Arrow"]   = "어둠으로 만든 화살을 여러 발 날린다.",
            ["Dark_Laser"]   = "어둠을 모은 광선으로 상대를 꿰뚫는다.",
            ["Dark_Wisp"]    = "불길한 도깨비불을 날려 상대를 휘감는다.",
            // ── 공허 ──
            ["Void_Shot"]  = "공허를 응축한 탄환을 쏜다.",
            ["Void_Pulse"] = "모든 것을 지우는 공허의 파동을 퍼뜨린다.",
            ["Annihilate"] = "공허의 힘을 해방해 닿는 모든 것을 소멸시킨다.",

            // ── 팰 전용 ──
            ["Rolling_Fluff"]      = "솜털 같은 몸을 굴려 상대에게 부딪친다.",
            ["Meow_Punch"]         = "작은 앞발로 재빠르게 펀치를 날린다.",
            ["Fighting_Slash"]     = "투지를 담은 검을 휘둘러 상대를 벤다.",
            ["Burning_Horn"]       = "불타는 뿔을 앞세워 상대를 들이받는다.",
            ["Mystic_Hurricane"]   = "신비한 힘이 깃든 회오리바람을 일으킨다.",
            ["Rocket_Tackle"]      = "몸을 쭉 펴고 로켓처럼 날아가 들이받는다.",
            ["Spin_Leg_Slash"]     = "몸을 회전시키며 다리로 상대를 베어 차낸다.",
            ["Force_Drive"]        = "힘을 실은 연속 공격을 퍼붓는다.",
            ["Ground_Smasher"]     = "온 힘으로 땅을 내리쳐 충격파를 일으킨다.",
            ["Somersault_Scratch"] = "공중제비를 돌며 날카로운 발톱으로 할퀸다.",
            ["Rolling_Scratch"]    = "몸을 굴리며 상대를 마구 할퀸다.",
            ["Blazing_Missile"]    = "작열하는 미사일을 연달아 발사한다.",
            ["Electric_Claw"]      = "전기가 흐르는 거대한 발톱으로 할퀸다.",
            ["Thunder_Tank"]       = "번개를 두르고 전차처럼 돌진한다.",
            ["Lancer_Charge"]      = "창을 앞세워 빛의 속도로 돌격한다.",
            ["Crystal_Wing"]       = "얼음 결정의 날개를 펼쳐 상대를 베어 낸다.",
            ["Tarasonic_Laser"]    = "바다의 힘을 모은 초음파 광선을 쏜다.",
            ["Meteor_Beam"]        = "하늘 높이에서 유성 같은 광선을 내리쏜다.",
            ["Nightmare_Ray"]      = "악몽을 품은 빛줄기로 상대를 감싼다.",
            ["Flame_Waltz"]        = "검은 불꽃을 두르고 춤추듯 상대를 휘감는다.",
            ["Gloomy_Sting"]       = "축 처진 몸으로 독침을 찔러 넣는다.",
            ["Penguin_Rocket"]     = "몸을 로켓처럼 날려 상대에게 부딪친다.",
            ["Vine_Barrage"]       = "덩굴을 휘둘러 상대를 마구 때린다.",
            ["Ember_Dash"]         = "불꽃을 두르고 상대에게 돌진한다.",
            ["Boar_Rush"]          = "흙먼지를 일으키며 상대를 들이받는다.",
            ["Fang_Raid"]          = "날카로운 송곳니를 드러내고 덮친다.",
            ["Static_Scratch"]     = "정전기가 흐르는 발톱으로 할퀸다.",
            ["Surf_Slash"]         = "파도를 타고 미끄러지듯 상대를 베어 낸다.",
            ["Hawkeye_Arrow"]      = "빈틈을 노려 화살 세 발을 쏜다.",
            ["Hell_Dive"]          = "하늘 높이 올랐다가 지옥의 기운과 함께 내리꽂힌다.",
            ["Petal_Storm"]        = "꽃잎 폭풍을 일으켜 상대를 휘감는다.",
            ["Blaze_Gallop"]       = "온몸을 불태우며 상대를 향해 질주한다.",
            ["Frost_Tail"]         = "얼어붙은 꼬리로 상대를 후려친다.",
            ["Jumping_Claw"]       = "높이 뛰어올라 번개 발톱으로 내려친다.",
            ["Panda_Lariat"]       = "굵은 팔을 크게 휘둘러 상대를 후려친다.",
            ["Inferno_Claw"]       = "지옥불을 두른 발톱으로 상대를 할퀸다.",
            ["Phantom_Scythe"]     = "망령의 낫을 휘둘러 상대의 영혼을 벤다.",
            ["Tidal_Wave"]         = "거대한 해일을 일으켜 모든 것을 삼킨다.",
            ["Dragon_Dive"]        = "하늘에서 용의 힘을 싣고 내리꽂힌다.",
            ["Iceberg_Drop"]       = "거대한 빙산을 만들어 상대 위로 떨어뜨린다.",
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
