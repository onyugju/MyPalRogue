using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 기술 연출 (임팩트). 내 팰·상대 팰 모두 같은 연출을 쓴다.
    /// 그림은 Resources/3.VFX/Pixel VFX Essentials/Spritesheets 의 32×32 칸 가로 시트를 그대로 잘라 쓴다.
    ///
    /// 연출 종류
    ///  - 발사(Projectile): 쓰는 팰에서 날아가 맞는다 (여러 발이면 연사)
    ///  - 빔(Beam):        쓰는 팰부터 상대까지 줄기가 뻗는다 (브레스·레이저)
    ///  - 직격(Strike):    기를 모았다가 상대 자리에서 터진다 (번개·폭발·베기)
    ///  - 돌진(Melee):     몸으로 부딪친다 (펀치·할퀴기·태클)
    ///  - 낙하(Rain):      하늘에서 여러 개가 쏟아진다 (운석·폭풍)
    ///  - 기운(Aura):      변화 기술. 자신에게 쓰면 자기 몸에, 상대에게 쓰면 상대 몸에 기운이 감돈다 (맞는 연출 없음)
    /// 능력치가 오르면 붉은 화살표(Buff Up), 떨어지면 푸른 화살표(Debuff Down)와 함께 팰이 번쩍인다 (PlayStatStage).
    /// 보스 패턴(힘 모으기·분노·정화 등)은 PlayBossFx.
    /// 맞으면 상대가 깜빡이며 흔들리고, 급소·효과 굉장·위력 100 이상이면 화면도 흔들린다.
    /// 기술별 연출은 아래 표(BySkill, 키 = 기술 에셋 이름)에, 표에 없으면 속성 기본 연출(ByType)을 쓴다.
    /// </summary>
    public class SkillFx : MonoBehaviour
    {
        const string Root = "3.VFX/Pixel VFX Essentials/Spritesheets/";
        const int FrameSize = 32;
        const float ImpactFps = 20f, TravelFps = 16f;
        const int SortingAbovePals = 20;

        enum Kind { Projectile, Beam, Strike, Melee, Rain, Aura }

        class Def
        {
            public Kind kind;
            public string travel, impact, impact2, cast;
            public Color tint = Color.white;
            public int count = 1;
            public float travelScale = 1.6f, impactScale = 2.2f;
            public float travelTime = 0.32f;
            public bool arc;
        }

        // ═════════════════ 연출 표 ═════════════════

        static Color C(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        static readonly Color Purple = C(0xb07cff), DarkPurple = C(0x8a4dd6), Blue = C(0x6cc4ff), Green = C(0x9cff7a),
                              Yellow = C(0xfff27a), Brown = C(0xc9a070), Ice = C(0xbfeaff), VoidCol = C(0x9a6bff);

        static Def Proj(string travel, string impact, Color? tint = null, int count = 1, float scale = 1.6f, bool arc = false,
                        string impact2 = null, string cast = null, float impactScale = 2.2f, float time = 0.32f) =>
            new Def { kind = Kind.Projectile, travel = travel, impact = impact, impact2 = impact2, cast = cast, tint = tint ?? Color.white,
                      count = count, travelScale = scale, arc = arc, impactScale = impactScale, travelTime = time };
        static Def Beam(string travel, string impact, Color? tint = null, float scale = 1.6f, string impact2 = null) =>
            new Def { kind = Kind.Beam, travel = travel, impact = impact, impact2 = impact2, tint = tint ?? Color.white, travelScale = scale };
        static Def Strike(string impact, string impact2 = null, Color? tint = null, float scale = 2.4f, string cast = null) =>
            new Def { kind = Kind.Strike, impact = impact, impact2 = impact2, cast = cast, tint = tint ?? Color.white, impactScale = scale };
        static Def Melee(string impact, string impact2 = null, Color? tint = null, float scale = 2.2f) =>
            new Def { kind = Kind.Melee, impact = impact, impact2 = impact2, tint = tint ?? Color.white, impactScale = scale };
        static Def Aura(string impact, string impact2 = null, Color? tint = null, float scale = 2.4f, string cast = null) =>
            new Def { kind = Kind.Aura, impact = impact, impact2 = impact2, cast = cast, tint = tint ?? Color.white, impactScale = scale };
        static Def Rain(string travel, string impact, Color? tint = null, int count = 3, float scale = 1.8f, float impactScale = 2.2f) =>
            new Def { kind = Kind.Rain, travel = travel, impact = impact, tint = tint ?? Color.white, count = count, travelScale = scale, impactScale = impactScale };

        // 시트 이름 줄임
        const string HitSpark = "Hit Sparks/Hit Spark", HeavyHit = "Hit Sparks/Heavy Hit", Punch = "Hit Sparks/Punch Impact",
                     FireHit = "Hit Sparks/Fire Hit", IceHit = "Hit Sparks/Ice Hit", ShockHit = "Hit Sparks/Shock Hit",
                     PoisonHit = "Hit Sparks/Poison Hit", SlashHit = "Hit Sparks/Slash Hit", Thud = "Hit Sparks/Blunt Thud",
                     MultiHit = "Hit Sparks/Multi Hit", WeakHit = "Hit Sparks/Weak Hit", Pierce = "Hit Sparks/Pierce Spark",
                     CritStar = "Hit Sparks/Critical Star";
        const string BigBoom = "Explosions/Big Boom", Blast = "Explosions/Blast", IceShatter = "Explosions/Ice Shatter",
                     RockBurst = "Explosions/Rock Burst", Shockwave = "Explosions/Shockwave", SmallPop = "Explosions/Small Pop",
                     FireBurst = "Explosions/Fireball Burst", SmokeBurst = "Explosions/Smoke Burst", PlasmaBlast = "Explosions/Plasma Blast",
                     Nuke = "Explosions/Nuke Mini";
        const string FireOrb = "Fire/Fire Orb", Flamethrower = "Fire/Flamethrower", Ignite = "Fire/Ignite", FireRing = "Fire/Fire Ring",
                     BurningGround = "Fire/Burning Ground", PhoenixFlare = "Fire/Phoenix Flare";
        const string BoltStrike = "Lightning/Bolt Strike", ThunderImpact = "Lightning/Thunder Impact", ElectricShock = "Lightning/Electric Shock",
                     SparkBurst = "Lightning/Spark Burst", StormFlash = "Lightning/Storm Flash", ZapRing = "Lightning/Zap Ring",
                     TeslaArc = "Lightning/Tesla Arc", Emp = "Lightning/EMP", Overload = "Lightning/Overload";
        const string WaterSplash = "Magic/Water Splash", FrostNova = "Magic/Frost Nova", PoisonCloud = "Magic/Poison Cloud",
                     DarkCurse = "Magic/Dark Curse", WindGust = "Magic/Wind Gust", HolyLight = "Magic/Holy Light", Portal = "Magic/Portal";
        const string FireballShot = "Projectiles/Fireball Shot", IceShard = "Projectiles/Ice Shard Shot", EnergyBolt = "Projectiles/Energy Bolt",
                     RockThrow = "Projectiles/Rock Throw", PoisonDart = "Projectiles/Poison Dart", MagicMissile = "Projectiles/Magic Missile",
                     PlasmaShot = "Projectiles/Plasma Shot", HomingOrb = "Projectiles/Homing Orb", Laser = "Projectiles/Laser Beam",
                     Cannonball = "Projectiles/Cannonball", Spear = "Projectiles/Spear Throw", Arrow = "Projectiles/Arrow Trail";
        const string CrossSlash = "Slashes/Cross Slash", DarkSlash = "Slashes/Dark Slash", TripleClaw = "Slashes/Triple Claw",
                     WideCleave = "Slashes/Wide Cleave", SlashDown = "Slashes/Slash Down", IceSlash = "Slashes/Ice Slash",
                     FireSlash = "Slashes/Fire Slash", SpinSlash = "Slashes/Spin Slash", DoubleSlash = "Slashes/Double Slash", Thrust = "Slashes/Thrust";
        const string SandBurst = "Smoke and Dust/Sand Burst", DustCloud = "Smoke and Dust/Dust Cloud", DirtKick = "Smoke and Dust/Dirt Kick",
                     Steam = "Smoke and Dust/Steam", Leaves = "Ambient/Falling Leaves", Bubbles = "Ambient/Bubbles", StarBurst = "Pickups and UI/Star Burst";
        // 변화 기술·능력치 변화·보스 패턴
        const string BuffUp = "Magic/Buff Up", DebuffDown = "Magic/Debuff Down", HealFx = "Magic/Heal", HealCross = "Pickups and UI/Heal Cross",
                     ShieldBubble = "Magic/Shield Bubble", CastCircle = "Magic/Cast Circle", ChargeUp = "Lightning/Charge Up",
                     StaticLoop = "Lightning/Static Loop", ShockAura = "Lightning/Shock Aura", FireShield = "Fire/Fire Shield",
                     EmberRise = "Fire/Ember Rise", FogDrift = "Smoke and Dust/Fog Drift", SmokePuff = "Smoke and Dust/Smoke Puff",
                     Dissipate = "Smoke and Dust/Dissipate", RankUp = "Pickups and UI/Rank Up";

        static readonly Dictionary<string, Def> BySkill = new Dictionary<string, Def>
        {
            // ── 무 ──
            ["Air_Cannon"]     = Proj(PlasmaShot, HitSpark, Color.white),
            ["Power_Shot"]     = Proj(EnergyBolt, Shockwave, Color.white, cast: StarBurst),
            ["Power_Bomb"]     = Proj(Cannonball, Blast, Color.white, scale: 2f, arc: true, time: 0.45f),
            ["Pal_Blast"]      = Beam(Laser, PlasmaBlast, Color.white, impact2: Shockwave),
            ["Holy_Burst"]     = Strike(HolyLight, BigBoom, C(0xfff1b0), 2.5f, cast: StarBurst),
            ["Rolling_Fluff"]  = Melee(Thud, DustCloud),
            ["Meow_Punch"]     = Melee(Punch, MultiHit),
            ["Quick_Strike"]   = Melee(Thrust, HitSpark),
            ["Lancer_Charge"]  = Melee(Pierce, Shockwave, null, 2.5f),
            ["Struggle"]       = Melee(WeakHit),
            // ── 화염 ──
            ["Fire_Shot"]      = Proj(FireballShot, FireHit),
            ["Spirit_Fire"]    = Proj(FireOrb, FireBurst, count: 3, scale: 1.3f),
            ["Fire_Breath"]    = Beam(Flamethrower, FireHit, impact2: FireBurst),
            ["Flame_Panel"]    = Strike(BurningGround, FireRing, null, 2.6f),
            ["Fireball"]       = Proj(FireOrb, BigBoom, scale: 2.2f, arc: true, time: 0.5f, impactScale: 2.6f),
            ["Burning_Horn"]   = Melee(FireSlash, FireHit),
            ["Blazing_Missile"]= Proj(FireballShot, Blast, count: 3, arc: true, time: 0.42f),
            ["Flame_Waltz"]    = Strike(FireRing, FireBurst, C(0xff8ae0), 2.6f),
            // ── 물 ──
            ["Water_Jet"]      = Proj(PlasmaShot, WaterSplash, Blue),
            ["Bubble_Shot"]    = Proj(Bubbles, WaterSplash, null, count: 3, scale: 1.2f),
            ["Line_Splash"]    = Beam(EnergyBolt, WaterSplash, Blue),
            ["Hydro_Stream"]   = Beam(EnergyBolt, WaterSplash, Blue, 2f, impact2: Bubbles),
            ["Water_Geyser"]   = Strike(WaterSplash, Bubbles, null, 2.6f),
            ["Tarasonic_Laser"]= Beam(EnergyBolt, PlasmaBlast, C(0xbff4ff), 2.2f, impact2: WaterSplash),
            // ── 풀 ──
            ["Wind_Blade"]     = Proj(WindGust, SlashDown, Green),
            ["Seed_Machine_Gun"] = Proj(PoisonDart, WeakHit, Green, count: 5, scale: 1.4f, time: 0.22f),
            ["Multi_Cutter"]   = Strike(DoubleSlash, CrossSlash, Green),
            ["Reflect_Leaf"]   = Proj(Leaves, SlashHit, null, count: 3, scale: 1.2f),
            ["Wind_Blast"]     = Strike(WindGust, Shockwave, Green, 2.6f),
            // ── 번개 ──
            ["Lightning_Spear"]= Proj(Spear, ShockHit, Yellow),
            ["Electric_Wave"]  = Strike(ZapRing, ElectricShock, Yellow),
            ["Line_Thunder"]   = Beam(TeslaArc, SparkBurst),
            ["Thunder_Strike"] = Strike(ThunderImpact, SparkBurst, null, 2.6f),
            ["Thunderstorm"]   = Rain(BoltStrike, StormFlash, null, count: 3, scale: 2.4f, impactScale: 2.4f),
            ["Electric_Claw"]  = Melee(TripleClaw, ShockHit, Yellow),
            ["Thunder_Tank"]   = Melee(Overload, PlasmaBlast, Yellow, 2.5f),
            // ── 땅 ──
            ["Mud_Shot"]       = Proj(RockThrow, DirtKick, Brown),
            ["Rock_Burst"]     = Strike(RockBurst, SandBurst),
            ["Rock_Cannon"]    = Proj(RockThrow, RockBurst, null, scale: 2.2f, arc: true, time: 0.42f),
            ["Rock_Spear"]     = Strike(Thrust, RockBurst, Brown, 2.6f),
            ["Stone_Beat"]     = Rain(RockThrow, RockBurst, null, count: 4),
            ["Fighting_Slash"] = Melee(WideCleave, SlashHit),
            ["Spin_Leg_Slash"] = Melee(SpinSlash, SlashHit),
            ["Force_Drive"]    = Melee(HeavyHit, Shockwave, null, 2.6f),
            ["Ground_Smasher"] = Melee(RockBurst, DustCloud, null, 2.6f),
            ["Somersault_Scratch"] = Melee(TripleClaw, SlashHit),
            ["Rolling_Scratch"]    = Melee(SpinSlash, TripleClaw),
            // ── 얼음 ──
            ["Ice_Missile"]    = Proj(IceShard, IceHit, null, count: 3),
            ["Ice_Blade"]      = Strike(IceSlash, IceHit),
            ["Frost_Breath"]   = Beam(Steam, FrostNova, Ice),
            ["Icicle_Line"]    = Proj(IceShard, IceShatter, null, count: 4, time: 0.24f),
            ["Diamond_Fall"]   = Rain(IceShard, IceShatter, null, count: 5),
            ["Crystal_Wing"]   = Strike(FrostNova, IceShatter, null, 2.6f),
            // ── 용 ──
            ["Dragon_Cannon"]  = Proj(PlasmaShot, PlasmaBlast, Purple),
            ["Dragon_Wave"]    = Strike(Emp, Shockwave, Purple),
            ["Dragon_Breath"]  = Beam(Flamethrower, PlasmaBlast, Purple),
            ["Beam_Slicer"]    = Beam(Laser, CrossSlash, Purple),
            ["Dragon_Meteor"]  = Rain(FireOrb, BigBoom, Purple, count: 4, scale: 2f, impactScale: 2.6f),
            ["Mystic_Hurricane"] = Strike(WindGust, Portal, Purple, 2.6f),
            ["Rocket_Tackle"]  = Melee(HeavyHit, SmokeBurst),
            ["Meteor_Beam"]    = Beam(Laser, BigBoom, C(0xffd0ff), impact2: Shockwave),
            // ── 어둠 ──
            ["Dark_Shot"]      = Proj(HomingOrb, DarkSlash, DarkPurple),
            ["Shadow_Burst"]   = Strike(DarkCurse, SmokeBurst, DarkPurple),
            ["Dark_Arrow"]     = Proj(Arrow, Pierce, DarkPurple),
            ["Dark_Laser"]     = Beam(Laser, DarkSlash, DarkPurple),
            ["Dark_Wisp"]      = Proj(MagicMissile, DarkCurse, DarkPurple, count: 3),
            ["Nightmare_Ray"]  = Beam(Laser, DarkCurse, DarkPurple, impact2: Portal),
            // ── 공허 (제로버스) ──
            ["Void_Shot"]      = Proj(HomingOrb, Portal, VoidCol),
            ["Void_Pulse"]     = Strike(Emp, Portal, VoidCol, 2.5f),
            ["Annihilate"]     = Strike(Portal, Nuke, VoidCol, 3f, cast: DarkCurse),

            // ── 새 팰 20종 전용 기술 ──
            ["Gloomy_Sting"]   = Proj(PoisonDart, PoisonHit, DarkPurple, impact2: PoisonCloud),                  // 뚱코알라: 독침 → 독 구름
            ["Penguin_Rocket"] = Melee(HeavyHit, WaterSplash, Blue, 2.4f),                                        // 펭키: 몸통 로켓
            ["Vine_Barrage"]   = Melee(MultiHit, Leaves, Green),                                                  // 몽지: 덩굴 연타
            ["Ember_Dash"]     = Melee(HeavyHit, FireBurst),                                                      // 불꽃밤비: 불꽃 돌진
            ["Boar_Rush"]      = Melee(HeavyHit, SandBurst, null, 2.5f),                                          // 돌진돼지: 흙먼지 박치기
            ["Fang_Raid"]      = Melee(DoubleSlash, CritStar),                                                    // 다크울프: 송곳니
            ["Static_Scratch"] = Melee(TripleClaw, ShockHit, Yellow),                                             // 번개냥: 전기 발톱
            ["Surf_Slash"]     = Melee(WideCleave, WaterSplash, Blue, 2.5f),                                      // 씨무기: 파도 베기
            ["Hawkeye_Arrow"]  = Proj(Arrow, Pierce, Green, count: 3, time: 0.22f, impact2: CritStar),           // 로빈몽: 화살 3연발
            ["Hell_Dive"]      = Strike(DarkSlash, SmokeBurst, DarkPurple, 2.6f, cast: WindGust),                 // 헬가루다: 급강하
            ["Petal_Storm"]    = Strike(Leaves, WindGust, C(0xffb0d8), 2.7f, cast: CastCircle),                   // 플로라디노: 꽃잎 폭풍
            ["Blaze_Gallop"]   = Melee(PhoenixFlare, FireBurst, null, 2.6f),                                      // 파이린: 불꽃 질주
            ["Frost_Tail"]     = Melee(IceSlash, IceShatter),                                                     // 빙호: 얼음 꼬리
            ["Jumping_Claw"]   = Strike(TripleClaw, ThunderImpact, Yellow, 2.6f, cast: SparkBurst),               // 썬도그: 번개 발톱 내려찍기
            ["Panda_Lariat"]   = Melee(HeavyHit, Shockwave, Green, 2.7f),                                         // 풀바오: 라리아트
            ["Inferno_Claw"]   = Melee(FireSlash, BurningGround, null, 2.6f),                                     // 만티파이어: 지옥불 발톱
            ["Phantom_Scythe"] = Strike(DarkSlash, DarkCurse, DarkPurple, 2.7f, cast: SmokePuff),                 // 고스호스: 망령의 낫
            ["Tidal_Wave"]     = Strike(WaterSplash, PlasmaBlast, C(0x9fdcff), 3f, cast: Bubbles),                // 레비드라: 대해일
            ["Dragon_Dive"]    = Melee(HeavyHit, PlasmaBlast, Purple, 2.7f),                                      // 페스키: 용의 급강하
            ["Iceberg_Drop"]   = Rain(IceShard, IceShatter, Ice, count: 2, scale: 3f, impactScale: 3f),           // 움포: 빙산 낙하

            // ── 변화 기술 (버프·디버프·회복) ──
            ["Focus_Up"]       = Aura(ChargeUp, StarBurst, C(0xffb070)),
            ["Burning_Spirit"] = Aura(FireShield, EmberRise),
            ["Aqua_Veil"]      = Aura(ShieldBubble, WaterSplash, Blue),
            ["Photosynthesis"] = Aura(HealFx, HealCross, Green),
            ["Charge_Up"]      = Aura(ChargeUp, StaticLoop, Yellow),
            ["Rock_Armor"]     = Aura(ShieldBubble, RockBurst, Brown),
            ["Frost_Mist"]     = Aura(FogDrift, FrostNova, Ice),
            ["Dragon_Dance"]   = Aura(CastCircle, WindGust, Purple),
            ["Fear_Gaze"]      = Aura(DarkCurse, SmokePuff, DarkPurple),
            ["Void_Erosion"]   = Aura(Portal, DarkCurse, VoidCol, 2.6f),
        };

        /// <summary>표에 없는 기술 (새로 추가한 기술 등)은 속성별 기본 연출</summary>
        static Def ByType(PalType t) => t switch
        {
            PalType.Fire => Proj(FireballShot, FireHit),
            PalType.Water => Proj(PlasmaShot, WaterSplash, Blue),
            PalType.Grass => Proj(WindGust, SlashHit, Green),
            PalType.Electric => Strike(BoltStrike, ShockHit),
            PalType.Ground => Proj(RockThrow, RockBurst),
            PalType.Ice => Proj(IceShard, IceHit),
            PalType.Dragon => Proj(PlasmaShot, PlasmaBlast, Purple),
            PalType.Dark => Proj(HomingOrb, DarkSlash, DarkPurple),
            PalType.Void => Strike(Portal, Emp, VoidCol),
            _ => Melee(HitSpark),
        };

        /// <summary>상태이상에 걸렸을 때 팰 위에 나오는 연출</summary>
        static (string sheet, Color tint) StatusSheet(StatusCondition s) => s switch
        {
            StatusCondition.Burn => (Ignite, Color.white),
            StatusCondition.Poison => (PoisonCloud, Color.white),
            StatusCondition.Paralysis => (ElectricShock, Yellow),
            StatusCondition.Freeze => (FrostNova, Color.white),
            _ => (null, Color.white),
        };

        /// <summary>표에 있는 시트 이름 전부 (테스트에서 그림이 있는지 확인)</summary>
        public static IEnumerable<string> AllSheets()
        {
            foreach (var s in new[] { BuffUp, DebuffDown, HealFx, ChargeUp, Shockwave, Dissipate, FireShield, HolyLight, DarkCurse,
                                      FrostNova, Portal, Ignite, ShockAura, RankUp })
                yield return s;
            foreach (var d in BySkill.Values)
                foreach (var s in new[] { d.travel, d.impact, d.impact2, d.cast })
                    if (s != null) yield return s;
            foreach (PalType t in Enum.GetValues(typeof(PalType)))
            {
                var d = ByType(t);
                foreach (var s in new[] { d.travel, d.impact, d.impact2, d.cast }) if (s != null) yield return s;
            }
            foreach (StatusCondition s in Enum.GetValues(typeof(StatusCondition)))
                if (StatusSheet(s).sheet != null) yield return StatusSheet(s).sheet;
            yield return CritStar;
        }

        public static bool HasCustomFx(SkillData s) => s != null && BySkill.ContainsKey(s.name);

        // ═════════════════ 그림 ═════════════════

        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        float pixelsPerUnit = -1f;

        /// <summary>시트를 32×32 칸으로 자른 프레임들 (없으면 null)</summary>
        public Sprite[] Frames(string sheet)
        {
            if (string.IsNullOrEmpty(sheet)) return null;
            if (cache.TryGetValue(sheet, out var f)) return f;
            var tex = Resources.Load<Texture2D>(Root + sheet);
            if (tex == null)
            {
                Debug.LogWarning($"[SkillFx] Resources/{Root}{sheet}.png 가 없습니다.");
                cache[sheet] = null;
                return null;
            }
            tex.filterMode = FilterMode.Point;
            int size = Mathf.Min(FrameSize, tex.height);
            int n = Mathf.Max(1, tex.width / size);
            var frames = new Sprite[n];
            for (int i = 0; i < n; i++)
                frames[i] = Sprite.Create(tex, new Rect(i * size, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            cache[sheet] = frames;
            return frames;
        }

        /// <summary>화면 320픽셀 = 카메라 폭. 시트 1픽셀이 화면 1픽셀이 되는 배율</summary>
        float PixelsPerUnit
        {
            get
            {
                if (pixelsPerUnit > 0f) return pixelsPerUnit;
                var cam = Camera.main;
                float width = cam != null && cam.orthographic ? cam.orthographicSize * 2f * cam.aspect : 18f;
                pixelsPerUnit = 320f / Mathf.Max(1f, width);
                return pixelsPerUnit;
            }
        }

        int sortingLayer, sortingOrder;
        int running;                                      // 아직 끝나지 않은 연출 수

        SpriteRenderer Spawn(Sprite first, Vector3 pos, float scale, Color tint, float angle = 0f, bool flipY = false)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = Vector3.one * scale;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = first;
            sr.color = tint;
            sr.flipY = flipY;
            sr.sortingLayerID = sortingLayer;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        void Run(IEnumerator routine) => StartCoroutine(Track(routine));

        IEnumerator Track(IEnumerator routine)
        {
            running++;
            try { yield return routine; }
            finally { running--; }
        }

        /// <summary>한 번 재생하고 사라지는 연출</summary>
        IEnumerator PlayOnce(string sheet, Vector3 pos, float scale, Color tint, float fps = ImpactFps, float angle = 0f, bool flipY = false)
        {
            var frames = Frames(sheet);
            if (frames == null || frames.Length == 0) yield break;
            var sr = Spawn(frames[0], pos, scale, tint, angle, flipY);
            float t = 0f;
            int last = -1;
            while (true)
            {
                int i = Mathf.FloorToInt(t * fps);
                if (i >= frames.Length) break;
                if (i != last) { sr.sprite = frames[i]; last = i; }
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            Destroy(sr.gameObject);
        }

        /// <summary>프레임을 돌리며 from → to로 날아간다 (arc면 포물선). 도착하면 onArrive</summary>
        IEnumerator Fly(string sheet, Vector3 from, Vector3 to, float duration, float scale, Color tint, bool arc, Action onArrive)
        {
            var frames = Frames(sheet);
            if (frames == null || frames.Length == 0) { onArrive?.Invoke(); yield break; }
            Vector3 dir = to - from;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            bool flip = dir.x < 0f;                                         // 왼쪽으로 날아갈 때 그림이 뒤집히지 않게
            var sr = Spawn(frames[0], from, scale, tint, angle, flip);
            float height = arc ? Mathf.Max(1f, dir.magnitude * 0.25f) : 0f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                Vector3 p = Vector3.Lerp(from, to, k) + Vector3.up * (height * 4f * k * (1f - k));
                if (arc)
                {
                    Vector3 next = Vector3.Lerp(from, to, k + 0.02f) + Vector3.up * (height * 4f * (k + 0.02f) * (1f - k - 0.02f));
                    Vector3 d = next - p;
                    sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                }
                sr.transform.position = new Vector3(p.x, p.y, 0f);
                sr.sprite = frames[Mathf.FloorToInt(t * TravelFps) % frames.Length];
                yield return null;
            }
            Destroy(sr.gameObject);
            onArrive?.Invoke();
        }

        /// <summary>쓰는 팰부터 상대까지 마디가 차례로 생겨 뻗는 줄기 (브레스·레이저)</summary>
        IEnumerator BeamTo(string sheet, Vector3 from, Vector3 to, float scale, Color tint, float hold)
        {
            var frames = Frames(sheet);
            if (frames == null || frames.Length == 0) yield break;
            Vector3 dir = to - from;
            float seg = FrameSize * scale / PixelsPerUnit * 0.8f;          // 마디끼리 조금 겹치게
            int n = Mathf.Max(2, Mathf.CeilToInt(dir.magnitude / seg));
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            bool flip = dir.x < 0f;
            var parts = new List<SpriteRenderer>();
            for (int i = 0; i < n; i++)
            {
                parts.Add(Spawn(frames[i % frames.Length], from + dir * ((i + 0.5f) / n), scale, tint, angle, flip));
                yield return new WaitForSecondsRealtime(0.018f);
            }
            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime)
            {
                int f = Mathf.FloorToInt(t * TravelFps);
                for (int i = 0; i < parts.Count; i++) parts[i].sprite = frames[(f + i) % frames.Length];
                yield return null;
            }
            for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime)
            {
                foreach (var p in parts) { var c = p.color; c.a = 1f - t / 0.12f; p.color = c; }
                yield return null;
            }
            foreach (var p in parts) Destroy(p.gameObject);
        }

        IEnumerator CameraShake(float strength, float duration)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            Vector3 home = cam.transform.position;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / duration;
                cam.transform.position = home + new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0f) * strength * k;
                yield return null;
            }
            cam.transform.position = home;
        }

        // ═════════════════ 재생 ═════════════════

        /// <summary>
        /// 기술 하나의 연출을 끝까지 재생한다. missed면 빗나가는 연출, 아니면 맞는 순간 상대가 깜빡이며 흔들린다.
        /// strong(급소·효과 굉장·위력 100 이상)이면 더 크게 흔들린다.
        /// </summary>
        public IEnumerator Play(SkillData skill, PalView user, PalView target, bool missed, bool critical, float effectiveness)
        {
            if (skill == null || user == null || target == null) yield break;
            var d = BySkill.TryGetValue(skill.name, out var def) ? def : ByType(skill.type);
            sortingLayer = target.SortingLayerId;
            sortingOrder = Mathf.Max(user.SortingOrder, target.SortingOrder) + SortingAbovePals;

            Vector3 from = user.Center, to = target.Center;
            Vector3 dir = to - from; dir.z = 0f;
            if (missed) to = to + dir.normalized * 1.6f + Vector3.up * 0.8f;     // 빗나가면 상대를 지나쳐 간다
            bool strong = !missed && (critical || effectiveness > 1f || skill.power >= 100);
            bool weak = !missed && effectiveness < 1f;
            float impactScale = d.impactScale * (critical ? 1.25f : weak ? 0.8f : 1f);
            bool impacted = false;

            void Impact(Vector3 at)
            {
                if (missed) return;
                impacted = true;
                Run(PlayOnce(d.impact, at, impactScale, d.tint));
                if (d.impact2 != null) Run(DelayThen(0.08f, PlayOnce(d.impact2, at, impactScale * 0.8f, d.tint)));
                if (critical) Run(PlayOnce(CritStar, at + Vector3.up * 0.4f, 2f, Color.white));
            }

            if (d.cast != null) Run(PlayOnce(d.cast, from, 1.5f, d.tint));

            switch (d.kind)
            {
                case Kind.Melee:
                    yield return user.Lunge(to, 2.2f, 0.32f, () => Impact(target.Center));
                    break;

                case Kind.Projectile:
                    if (d.cast != null) yield return new WaitForSecondsRealtime(0.15f);
                    for (int i = 0; i < d.count; i++)
                    {
                        Vector3 jitter = d.count > 1 ? new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(-0.35f, 0.35f), 0f) : Vector3.zero;
                        Vector3 end = to + jitter;
                        Run(Fly(d.travel, from + jitter * 0.3f, end, d.travelTime, d.travelScale, d.tint, d.arc, () => Impact(end)));
                        if (i < d.count - 1) yield return new WaitForSecondsRealtime(Mathf.Min(0.1f, d.travelTime * 0.45f));
                    }
                    yield return new WaitForSecondsRealtime(d.travelTime);
                    break;

                case Kind.Beam:
                    Run(BeamTo(d.travel, from, to, d.travelScale, d.tint, 0.3f));
                    yield return new WaitForSecondsRealtime(0.22f);
                    Impact(to);
                    break;

                case Kind.Strike:
                    yield return user.WindUp(target.Center, 0.18f);
                    Impact(missed ? to : target.Center);
                    if (missed) Run(PlayOnce(d.impact, to, impactScale * 0.7f, new Color(1f, 1f, 1f, 0.5f)));
                    break;

                case Kind.Aura:
                {
                    // 자신에게 쓰는 기술은 몸을 웅크렸다 펴며 자기 몸에, 상대에게 쓰는 기술은 상대 몸에 기운이 감돈다
                    bool self = skill.IsSelfOnly;
                    var at = self ? user : target;
                    yield return user.WindUp(self ? user.Center + Vector3.down : target.Center, 0.2f);
                    Vector3 p = self ? user.Center : (missed ? to : target.Center);
                    Color tint = missed ? new Color(d.tint.r, d.tint.g, d.tint.b, 0.5f) : d.tint;
                    Run(PlayOnce(d.impact, p, d.impactScale, tint, 14f));
                    if (d.impact2 != null) Run(DelayThen(0.15f, PlayOnce(d.impact2, p, d.impactScale * 0.8f, tint, 14f)));
                    if (!self && !missed) Run(DelayThen(0.1f, at.Flash(new Color(0.75f, 0.6f, 1f), 2)));
                    yield return new WaitForSecondsRealtime(0.3f);
                    break;
                }

                case Kind.Rain:
                    for (int i = 0; i < d.count; i++)
                    {
                        float x = UnityEngine.Random.Range(-0.6f, 0.6f);
                        Vector3 end = (missed ? to : target.Center) + new Vector3(x, UnityEngine.Random.Range(-0.3f, 0.2f), 0f);
                        Vector3 start = end + new Vector3(-1.2f, 6f, 0f);
                        Run(Fly(d.travel, start, end, 0.24f, d.travelScale, d.tint, false, () => Impact(end)));
                        yield return new WaitForSecondsRealtime(0.11f);
                    }
                    yield return new WaitForSecondsRealtime(0.24f);
                    break;
            }

            // 맞았으면 상대가 움찔한다 (화면도 흔들릴 수 있음)
            if (impacted)
            {
                Run(target.HitReact(strong));
                if (strong) Run(CameraShake(critical ? 0.18f : 0.12f, 0.25f));
            }
            while (running > 0) yield return null;
        }

        /// <summary>상태이상에 걸렸을 때 그 팰 위에 연출</summary>
        public IEnumerator PlayStatus(PalView view, StatusCondition status)
        {
            var (sheet, tint) = StatusSheet(status);
            if (sheet == null || view == null) yield break;
            sortingLayer = view.SortingLayerId;
            sortingOrder = view.SortingOrder + SortingAbovePals;
            yield return PlayOnce(sheet, view.Center, 2.2f, tint, 14f);
        }

        // ═════════════════ 능력치 변화 ═════════════════
        static readonly Color UpFlash = new Color(1f, 0.55f, 0.4f), DownFlash = new Color(0.45f, 0.65f, 1f);

        /// <summary>능력치가 오르면 붉은 화살표가 솟고, 떨어지면 푸른 화살표가 내려온다. 팰도 그 색으로 번쩍인다</summary>
        public IEnumerator PlayStatStage(PalView view, bool up)
        {
            if (view == null || !view.IsVisible) yield break;
            sortingLayer = view.SortingLayerId;
            sortingOrder = view.SortingOrder + SortingAbovePals;
            Run(view.Flash(up ? UpFlash : DownFlash, 3, 0.07f));
            Run(PlayOnce(up ? BuffUp : DebuffDown, view.Center, 2.6f, Color.white, 14f));
            while (running > 0) yield return null;
        }

        // ═════════════════ 보스 패턴 ═════════════════
        static readonly Color ChargeGlow = new Color(1f, 0.35f, 0.3f), RageRed = new Color(1f, 0.3f, 0.25f), HolyGold = new Color(1f, 0.95f, 0.6f);

        /// <summary>보스 패턴 연출. boss = 패턴을 쓴 팰, target = 연출이 나오는 팰 (상대에게 거는 패턴이면 상대)</summary>
        public IEnumerator PlayBossFx(BossFx fx, PalType bossType, PalView boss, PalView target)
        {
            if (boss == null) yield break;
            target ??= boss;
            sortingLayer = boss.SortingLayerId;
            sortingOrder = Mathf.Max(boss.SortingOrder, target.SortingOrder) + SortingAbovePals;
            switch (fx)
            {
                case BossFx.Charge:                       // 붉은 기운을 모으고, 다음 공격까지 몸이 붉게 맥동한다
                    Run(PlayOnce(ChargeUp, boss.Center, 2.8f, ChargeGlow, 14f));
                    Run(DelayThen(0.2f, PlayOnce(ChargeUp, boss.Center, 2.2f, ChargeGlow, 14f)));
                    Run(CameraShake(0.06f, 0.5f));
                    yield return new WaitForSecondsRealtime(0.35f);
                    boss.SetGlow(ChargeGlow);
                    break;
                case BossFx.Release:                      // 모은 힘이 터진다: 상대 자리에 충격파 + 큰 흔들림
                    boss.SetGlow(null);
                    Run(PlayOnce(Shockwave, target.Center, 3f, ChargeGlow));
                    Run(target.HitReact(true));
                    Run(CameraShake(0.25f, 0.4f));
                    break;
                case BossFx.ChargeBroken:                 // 모으던 기운이 흩어진다
                    boss.SetGlow(null);
                    Run(PlayOnce(Dissipate, boss.Center, 2.6f, ChargeGlow));
                    break;
                case BossFx.Enrage:                       // 분노: 붉게 번쩍이며 불꽃 기운, 화면이 흔들린다
                    Run(boss.Flash(RageRed, 4, 0.06f));
                    Run(PlayOnce(FireShield, boss.Center, 2.8f, RageRed, 14f));
                    Run(CameraShake(0.14f, 0.45f));
                    break;
                case BossFx.Purify:                       // 정화: 성스러운 빛
                    Run(boss.Flash(HolyGold, 2));
                    Run(PlayOnce(HolyLight, boss.Center, 2.8f, Color.white, 14f));
                    break;
                case BossFx.Curse:                        // 상대에게 거는 패턴 (속성마다 다른 그림)
                {
                    var (sheet, tint) = bossType switch
                    {
                        PalType.Ice => (FrostNova, Ice),
                        PalType.Void => (Portal, VoidCol),
                        _ => (DarkCurse, DarkPurple),
                    };
                    Run(PlayOnce(sheet, target.Center, 2.6f, tint, 14f));
                    Run(DelayThen(0.1f, target.Flash(tint, 2)));
                    break;
                }
                case BossFx.Heal:
                    Run(PlayOnce(HealFx, boss.Center, 2.4f, Green, 14f));
                    break;
                case BossFx.Aura:                         // 몸에 두른 기운이 반응 (작열 오라: 상대에게 불꽃 / 번개 가속: 전기)
                    if (bossType == PalType.Fire) Run(PlayOnce(Ignite, target.Center, 2.4f, Color.white, 14f));
                    else Run(PlayOnce(ShockAura, boss.Center, 2.6f, Yellow, 14f));
                    break;
            }
            while (running > 0) yield return null;
        }

        IEnumerator DelayThen(float seconds, IEnumerator then)
        {
            yield return new WaitForSecondsRealtime(seconds);
            yield return then;
        }
    }
}
