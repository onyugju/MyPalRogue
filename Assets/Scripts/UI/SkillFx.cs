using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 기술 연출 (임팩트). 내 팰·상대 팰 모두 같은 연출을 쓴다.
    /// 그림은 두 곳에서 온다.
    ///  - Resources/3.VFX/PixelFx : pixelfx로 뽑은 시트 (이름이 "px:"로 시작). 크기·기준점은 manifest.json에 있고 1배로 쓴다.
    ///    발밑 연출(floor)은 팰 발에, 나머지는 팰 몸 가운데에 맞추고, 상대가 쓰면 좌우가 뒤집혀 쓰는 쪽에서 날아온다.
    ///  - Resources/3.VFX/Pixel VFX Essentials/Spritesheets : 기존 32×32 칸 가로 시트 (돌·거품·화살표 등에 계속 쓴다)
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
    /// 줄기(Beam)는 pixelfx 시트면 쓰는 팰에서 상대까지 한 장으로 늘려 뻗는다.
    /// 맞으면 상대가 깜빡이며 흔들리고, 급소·효과 굉장·위력 100 이상이면 화면도 흔들린다.
    /// 기술별 연출은 아래 표(BySkill, 키 = 기술 에셋 이름)에, 표에 없으면 속성 기본 연출(ByType)을 쓴다.
    /// </summary>
    public class SkillFx : MonoBehaviour
    {
        const string Root = "3.VFX/Pixel VFX Essentials/Spritesheets/";
        const string PxRoot = "3.VFX/PixelFx/", PxPrefix = "px:";
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

        // ═════════════════ pixelfx 시트 정보 ═════════════════

        [Serializable]
        class PxInfo
        {
            public string name;
            public int w, h, cols, count, fps;
            public float pivotX, pivotY;   // 칸 왼쪽 위에서 잰 기준점 (픽셀)
            public bool floor;             // 팰 발밑에 놓는 연출
            public float reach;            // 줄기: 기준점에서 끝까지 길이 (픽셀)
            public float hit;              // 시작 후 가장 세게 터지는 시각 (초)
            public bool loop;
        }

        [Serializable]
        class PxManifest { public PxInfo[] sheets; }

        static Dictionary<string, PxInfo> pxInfo;

        /// <summary>pixelfx 시트면 그 정보, 아니면 null</summary>
        static PxInfo Px(string sheet)
        {
            if (string.IsNullOrEmpty(sheet) || !sheet.StartsWith(PxPrefix, StringComparison.Ordinal)) return null;
            if (pxInfo == null)
            {
                pxInfo = new Dictionary<string, PxInfo>();
                var file = Resources.Load<TextAsset>(PxRoot + "manifest");
                if (file == null) Debug.LogWarning($"[SkillFx] Resources/{PxRoot}manifest.json 이 없습니다.");
                else foreach (var i in JsonUtility.FromJson<PxManifest>(file.text).sheets) pxInfo[i.name] = i;
            }
            return pxInfo.TryGetValue(sheet.Substring(PxPrefix.Length), out var info) ? info : null;
        }

        /// <summary>시트 이름 → Resources 경로</summary>
        public static string TexturePath(string sheet) =>
            sheet.StartsWith(PxPrefix, StringComparison.Ordinal) ? PxRoot + sheet.Substring(PxPrefix.Length) : Root + sheet;

        public static bool IsPxSheet(string sheet) => !string.IsNullOrEmpty(sheet) && sheet.StartsWith(PxPrefix, StringComparison.Ordinal);

        /// <summary>팰 몸집에 맞춘 pixelfx 배율 (기준: 키 3유닛인 상대 팰). 큰 보스일수록 크게</summary>
        static float Fit(PalView view) => Mathf.Clamp(view.Height / 3f, 0.9f, 1.8f);

        /// <summary>pixelfx 시트는 몸집 배율, 기존 시트는 표에 적힌 배율</summary>
        static float Sz(string sheet, float oldScale, float fit) => Px(sheet) != null ? fit : oldScale;

        /// <summary>시트를 놓을 자리: 발밑 연출이면 팰 발, 아니면 주어진 자리</summary>
        static Vector3 At(string sheet, PalView view, Vector3 otherwise)
        {
            var px = Px(sheet);
            return px != null && px.floor ? view.Feet : otherwise;
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

        const string PSpark = PxPrefix + "spark";
        const string PSpiritUp = PxPrefix + "spirit_up";
        const string PBlunt = PxPrefix + "blunt";
        const string PCrit = PxPrefix + "crit";
        const string PSlash = PxPrefix + "slash";
        const string PSlashG = PxPrefix + "slash_g";
        const string PClaw = PxPrefix + "claw";
        const string PClawY = PxPrefix + "claw_y";
        const string PXslash = PxPrefix + "xslash";
        const string PXslashG = PxPrefix + "xslash_g";
        const string PSpin = PxPrefix + "spin";
        const string PThrust = PxPrefix + "thrust";
        const string PUppercut = PxPrefix + "uppercut";
        const string PCleave = PxPrefix + "cleave";
        const string PScythe = PxPrefix + "scythe";
        const string PHammer = PxPrefix + "hammer";
        const string PBoom = PxPrefix + "boom";
        const string PBurst = PxPrefix + "burst";
        const string PBurstF = PxPrefix + "burst_f";
        const string PBurstB = PxPrefix + "burst_b";
        const string PBurstD = PxPrefix + "burst_d";
        const string PMana = PxPrefix + "mana";
        const string PManaW = PxPrefix + "mana_w";
        const string PImplosion = PxPrefix + "implosion";
        const string PDarkBoom = PxPrefix + "dark_boom";
        const string PDirt = PxPrefix + "dirt";
        const string PSlam = PxPrefix + "slam";
        const string PLanding = PxPrefix + "landing";
        const string PSpike = PxPrefix + "spike";
        const string PQuake = PxPrefix + "quake";
        const string PFireball = PxPrefix + "fireball";
        const string PFireBreath = PxPrefix + "fire_breath";
        const string PFireBreathP = PxPrefix + "fire_breath_p";
        const string PFireRing = PxPrefix + "fire_ring";
        const string PFlamePillar = PxPrefix + "flame_pillar";
        const string PFireTornado = PxPrefix + "fire_tornado";
        const string PShadowFlames = PxPrefix + "shadow_flames";
        const string PMeteor = PxPrefix + "meteor";
        const string PMeteorP = PxPrefix + "meteor_p";
        const string PWaterJet = PxPrefix + "water_jet";
        const string PBeamB = PxPrefix + "beam_b";
        const string PSplash = PxPrefix + "splash";
        const string PGeyser = PxPrefix + "geyser";
        const string PWave = PxPrefix + "wave";
        const string PDiscG = PxPrefix + "disc_g";
        const string PPlasmaG = PxPrefix + "plasma_g";
        const string PLeafStorm = PxPrefix + "leaf_storm";
        const string PVine = PxPrefix + "vine";
        const string PTornado = PxPrefix + "tornado";
        const string PTornadoP = PxPrefix + "tornado_p";
        const string PThorns = PxPrefix + "thorns";
        const string PAcid = PxPrefix + "acid";
        const string PBloom = PxPrefix + "bloom";
        const string PZap = PxPrefix + "zap";
        const string PBolt = PxPrefix + "bolt";
        const string PThunderclap = PxPrefix + "thunderclap";
        const string PChargeY = PxPrefix + "charge_y";
        const string PChargeW = PxPrefix + "charge_w";
        const string PLanceY = PxPrefix + "lance_y";
        const string PBeamY = PxPrefix + "beam_y";
        const string PLance = PxPrefix + "lance";
        const string PFrostBreath = PxPrefix + "frost_breath";
        const string PFrostNova = PxPrefix + "frost_nova";
        const string PIceSpikes = PxPrefix + "ice_spikes";
        const string PIcicle = PxPrefix + "icicle";
        const string PSnow = PxPrefix + "snow";
        const string PBlizzard = PxPrefix + "blizzard";
        const string PBeamP = PxPrefix + "beam_p";
        const string PBeamW = PxPrefix + "beam_w";
        const string PBeamD = PxPrefix + "beam_d";
        const string PPlasmaP = PxPrefix + "plasma_p";
        const string PPlasmaD = PxPrefix + "plasma_d";
        const string PPlasmaV = PxPrefix + "plasma_v";
        const string PPlasmaW = PxPrefix + "plasma_w";
        const string PDiscW = PxPrefix + "disc_w";
        const string PStarFall = PxPrefix + "star_fall";
        const string PSpell = PxPrefix + "spell";
        const string PHands = PxPrefix + "hands";
        const string PTendrils = PxPrefix + "tendrils";
        const string PHex = PxPrefix + "hex";
        const string PVoidOrb = PxPrefix + "void_orb";
        const string PPillar = PxPrefix + "pillar";
        const string PSmite = PxPrefix + "smite";
        const string PFeather = PxPrefix + "feather";
        const string PShield = PxPrefix + "shield";
        const string PShieldB = PxPrefix + "shield_b";
        const string PArrow = PxPrefix + "arrow";
        const string PRocket = PxPrefix + "rocket";

        // pixelfx 연출 표 쓰기용. 맞는 그림은 시트 자체가 기술 전체를 담고 있어서 한두 장이면 된다.
        static Def XProj(string travel, string hit, string hit2 = null, string cast = null, int count = 1, float time = 0.34f,
                         bool arc = false, float scale = 1.6f, Color? tint = null) =>
            new Def { kind = Kind.Projectile, travel = travel, impact = hit, impact2 = hit2, cast = cast, count = count,
                      travelTime = time, arc = arc, travelScale = scale, tint = tint ?? Color.white };
        static Def XCone(string cone, string hit, string hit2 = null, string cast = null) =>
            new Def { kind = Kind.Beam, travel = cone, impact = hit, impact2 = hit2, cast = cast };
        static Def XStrike(string hit, string hit2 = null, string cast = null, int count = 1) =>
            new Def { kind = Kind.Strike, impact = hit, impact2 = hit2, cast = cast, count = count };
        static Def XMelee(string hit, string hit2 = null) =>
            new Def { kind = Kind.Melee, impact = hit, impact2 = hit2 };
        static Def XAura(string fx, string fx2 = null) =>
            new Def { kind = Kind.Aura, impact = fx, impact2 = fx2 };

        static readonly Dictionary<string, Def> BySkill = new Dictionary<string, Def>
        {
            // ── 무 ──
            ["Air_Cannon"]     = XProj(PDiscW, PSpark),
            ["Power_Shot"]     = XProj(PPlasmaW, PManaW, cast: PChargeW),
            ["Power_Bomb"]     = XProj(Cannonball, PBoom, arc: true, time: 0.45f, scale: 2f),
            ["Pal_Blast"]      = XCone(PBeamW, PManaW, PBurst, PChargeW),
            ["Holy_Burst"]     = XStrike(PPillar, PSmite, PChargeW),
            ["Rolling_Fluff"]  = XMelee(PBlunt, PFeather),
            ["Meow_Punch"]     = XMelee(PUppercut, PSpark),
            ["Quick_Strike"]   = XMelee(PThrust, PSpark),
            ["Lancer_Charge"]  = XMelee(PThrust, PCrit),
            ["Struggle"]       = XMelee(PBlunt),
            // ── 불 ──
            ["Fire_Shot"]      = XProj(PFireball, PBurstF),
            ["Spirit_Fire"]    = XProj(PFireball, PBurstF, count: 3),
            ["Fire_Breath"]    = XCone(PFireBreath, PBurstF),
            ["Flame_Panel"]    = XStrike(PFireRing, PFlamePillar),
            ["Fireball"]       = XStrike(PMeteor, PFireRing),
            ["Burning_Horn"]   = XMelee(PThrust, PBurstF),
            ["Blazing_Missile"]= XProj(PRocket, PBoom, count: 3, arc: true, time: 0.42f),
            ["Flame_Waltz"]    = XStrike(PFireTornado, PShadowFlames),
            // ── 물 ──
            ["Water_Jet"]      = XCone(PWaterJet, PSplash),
            ["Bubble_Shot"]    = XProj(Bubbles, PSplash, count: 3, scale: 1.2f),
            ["Line_Splash"]    = XStrike(PWave, PSplash),
            ["Hydro_Stream"]   = XCone(PBeamB, PSplash, PBurstB),
            ["Water_Geyser"]   = XStrike(PGeyser, PSplash),
            ["Tarasonic_Laser"]= XCone(PBeamB, PBurstB, PGeyser, PSpell),
            // ── 풀 ──
            ["Wind_Blade"]     = XProj(PDiscG, PSlashG),
            ["Seed_Machine_Gun"] = XProj(PPlasmaG, PSpark, count: 5, time: 0.22f),
            ["Multi_Cutter"]   = XStrike(PXslashG, PSlashG),
            ["Reflect_Leaf"]   = XStrike(PLeafStorm, PSlashG),
            ["Wind_Blast"]     = XStrike(PTornado, PLeafStorm),
            // ── 번개 ──
            ["Lightning_Spear"]= XProj(PLanceY, PZap),
            ["Electric_Wave"]  = XStrike(PThunderclap, PZap),
            ["Line_Thunder"]   = XCone(PBeamY, PZap),
            ["Thunder_Strike"] = XStrike(PBolt, PThunderclap),
            ["Thunderstorm"]   = XStrike(PBolt, PThunderclap, PChargeY, 3),
            ["Electric_Claw"]  = XMelee(PClawY, PZap),
            ["Thunder_Tank"]   = XMelee(PCrit, PThunderclap),
            // ── 땅 ──
            ["Mud_Shot"]       = XProj(RockThrow, PDirt, tint: Brown),
            ["Rock_Burst"]     = XStrike(PDirt, PSpike),
            ["Rock_Cannon"]    = XProj(RockThrow, PSlam, arc: true, time: 0.42f, scale: 2.2f),
            ["Rock_Spear"]     = XStrike(PSpike),
            ["Stone_Beat"]     = XStrike(PQuake, PSpike, count: 3),
            ["Fighting_Slash"] = XMelee(PSlash, PSpark),
            ["Spin_Leg_Slash"] = XMelee(PSpin, PSpark),
            ["Force_Drive"]    = XMelee(PHammer, PCrit),
            ["Ground_Smasher"] = XMelee(PSlam, PQuake),
            ["Somersault_Scratch"] = XMelee(PClaw, PSpark),
            ["Rolling_Scratch"]    = XMelee(PSpin, PClaw),
            // ── 얼음 ──
            ["Ice_Missile"]    = XProj(PLance, PSnow, count: 3),
            ["Ice_Blade"]      = XStrike(PSlash, PSnow),
            ["Frost_Breath"]   = XCone(PFrostBreath, PSnow),
            ["Icicle_Line"]    = XProj(PLance, PIceSpikes, count: 4, time: 0.24f),
            ["Diamond_Fall"]   = XStrike(PIcicle, PSnow, count: 3),
            ["Crystal_Wing"]   = XStrike(PFrostNova, PSnow),
            // ── 용 ──
            ["Dragon_Cannon"]  = XProj(PPlasmaP, PMana),
            ["Dragon_Wave"]    = XStrike(PBurstD, PMana),
            ["Dragon_Breath"]  = XCone(PFireBreathP, PMana),
            ["Beam_Slicer"]    = XCone(PBeamP, PXslash),
            ["Dragon_Meteor"]  = XStrike(PMeteorP, PMana, count: 2),
            ["Mystic_Hurricane"] = XStrike(PTornadoP, PMana),
            ["Rocket_Tackle"]  = XMelee(PCrit, PLanding),
            ["Meteor_Beam"]    = XCone(PBeamP, PBoom, PStarFall, PSpell),
            // ── 어둠 ──
            ["Dark_Shot"]      = XProj(PPlasmaD, PBurstD),
            ["Shadow_Burst"]   = XStrike(PDarkBoom),
            ["Dark_Arrow"]     = XProj(Arrow, PBurstD, tint: DarkPurple),
            ["Dark_Laser"]     = XCone(PBeamD, PBurstD),
            ["Dark_Wisp"]      = XProj(PPlasmaD, PBurstD, count: 3),
            ["Nightmare_Ray"]  = XCone(PBeamD, PTendrils, PBurstD, PHex),
            // ── 공허 (제로버스) ──
            ["Void_Shot"]      = XProj(PPlasmaV, PImplosion),
            ["Void_Pulse"]     = XStrike(PImplosion, PBurstD),
            ["Annihilate"]     = XStrike(PVoidOrb, PDarkBoom, PHex),
            // 제로버스 2페이즈 전용 (ZerobusPhase가 실행 중에 만드는 기술)
            ["Zerobus_Starfall"]  = XStrike(PStarFall, PDarkBoom, PVoidOrb, count: 3),   // 멸성의 유성: 공허의 유성 세 줄기
            ["Zerobus_Judgement"] = XCone(PBeamP, PImplosion, PHex, PSpell),             // 공허의 단죄: 빛줄기 → 함몰

            // ── 새 팰 20종 전용 기술 ──
            ["Gloomy_Sting"]   = XProj(PoisonDart, PAcid, tint: DarkPurple),         // 뚱코알라: 독침 → 독 웅덩이
            ["Penguin_Rocket"] = XMelee(PCrit, PSplash),                             // 펭키: 몸통 로켓
            ["Vine_Barrage"]   = XStrike(PVine, PThorns),                            // 몽지: 덩굴 연타
            ["Ember_Dash"]     = XMelee(PBlunt, PBurstF),                            // 불꽃밤비: 불꽃 돌진
            ["Boar_Rush"]      = XMelee(PBlunt, PLanding),                           // 돌진돼지: 흙먼지 박치기
            ["Fang_Raid"]      = XMelee(PXslash, PCrit),                             // 다크울프: 송곳니
            ["Static_Scratch"] = XMelee(PClawY, PZap),                               // 번개냥: 전기 발톱
            ["Surf_Slash"]     = XMelee(PSlash, PWave),                              // 씨무기: 파도 베기
            ["Hawkeye_Arrow"]  = XProj(PArrow, PSpark, PCrit, count: 3, time: 0.22f),// 로빈몽: 화살 3연발
            ["Hell_Dive"]      = XStrike(PScythe, PDarkBoom),                        // 헬가루다: 급강하
            ["Petal_Storm"]    = XStrike(PLeafStorm, PThorns),                       // 플로라디노: 꽃잎 폭풍
            ["Blaze_Gallop"]   = XMelee(PCrit, PFireRing),                           // 파이린: 불꽃 질주
            ["Frost_Tail"]     = XMelee(PSlash, PSnow),                              // 빙호: 얼음 꼬리
            ["Jumping_Claw"]   = XStrike(PClawY, PThunderclap),                      // 썬도그: 번개 발톱 내려찍기
            ["Panda_Lariat"]   = XMelee(PCleave, PSpark),                            // 풀바오: 라리아트
            ["Inferno_Claw"]   = XMelee(PClaw, PFlamePillar),                        // 만티파이어: 지옥불 발톱
            ["Phantom_Scythe"] = XStrike(PScythe, PHands),                           // 고스호스: 망령의 낫
            ["Tidal_Wave"]     = XStrike(PWave, PGeyser),                            // 레비드라: 대해일
            ["Dragon_Dive"]    = XMelee(PCrit, PMana),                               // 페스키: 용의 급강하
            ["Iceberg_Drop"]   = XStrike(PIcicle, PFrostNova),                       // 움포: 빙산 낙하

            // ── 변화 기술 (버프·디버프·회복) ──
            ["Focus_Up"]       = XAura(PChargeW),
            ["Burning_Spirit"] = XAura(PSpiritUp, EmberRise),         // 투지 연소: 불꽃 기둥과 고리가 솟는 강화 연출
            ["Aqua_Veil"]      = XAura(PShieldB, PSplash),
            ["Photosynthesis"] = XAura(PBloom),
            ["Charge_Up"]      = XAura(PChargeY),
            ["Rock_Armor"]     = XAura(PShield, PLanding),
            ["Frost_Mist"]     = XAura(PBlizzard),
            ["Dragon_Dance"]   = XAura(PSpell, PTornadoP),
            ["Fear_Gaze"]      = XAura(PHex),
            ["Void_Erosion"]   = XAura(PVoidOrb, PHex),
        };

        /// <summary>표에 없는 기술 (새로 추가한 기술 등)은 속성별 기본 연출</summary>
        static Def ByType(PalType t) => t switch
        {
            PalType.Fire => XProj(PFireball, PBurstF),
            PalType.Water => XCone(PWaterJet, PSplash),
            PalType.Grass => XProj(PDiscG, PSlashG),
            PalType.Electric => XStrike(PBolt, PZap),
            PalType.Ground => XStrike(PDirt),
            PalType.Ice => XProj(PLance, PSnow),
            PalType.Dragon => XProj(PPlasmaP, PMana),
            PalType.Dark => XProj(PPlasmaD, PBurstD),
            PalType.Void => XStrike(PImplosion, PBurstD),
            _ => XMelee(PSpark),
        };

        /// <summary>상태이상에 걸렸을 때 팰 위에 나오는 연출</summary>
        static (string sheet, Color tint) StatusSheet(StatusCondition s) => s switch
        {
            StatusCondition.Burn => (PFireTornado, Color.white),
            StatusCondition.Poison => (PAcid, Color.white),
            StatusCondition.Paralysis => (PZap, Color.white),
            StatusCondition.Freeze => (PFrostNova, Color.white),
            _ => (null, Color.white),
        };


        /// <summary>표에 있는 시트 이름 전부 (테스트에서 그림이 있는지 확인)</summary>
        public static IEnumerable<string> AllSheets()
        {
            foreach (var s in new[] { BuffUp, DebuffDown, HealFx, ChargeUp, Shockwave, Dissipate, FireShield, HolyLight, DarkCurse,
                                      FrostNova, Portal, Ignite, ShockAura, RankUp, PCrit,
                                      PImplosion, PDarkBoom, PVoidOrb, PShadowFlames, Nuke, StarBurst, PSpiritUp })
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
            var tex = Resources.Load<Texture2D>(TexturePath(sheet));
            if (tex == null)
            {
                Debug.LogWarning($"[SkillFx] Resources/{TexturePath(sheet)}.png 가 없습니다.");
                cache[sheet] = null;
                return null;
            }
            tex.filterMode = FilterMode.Point;
            var px = Px(sheet);
            if (px != null)
            {
                var cells = new Sprite[px.count];
                var pivot = new Vector2(px.pivotX / px.w, 1f - px.pivotY / px.h);
                for (int i = 0; i < px.count; i++)
                {
                    int col = i % px.cols, row = i / px.cols;
                    var rect = new Rect(col * px.w, tex.height - (row + 1) * px.h, px.w, px.h);   // 텍스처는 아래가 0
                    cells[i] = Sprite.Create(tex, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                }
                cache[sheet] = cells;
                return cells;
            }
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

        SpriteRenderer Spawn(Sprite first, Vector3 pos, float scale, Color tint, float angle = 0f, bool flipY = false, bool flipX = false)
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
            sr.flipX = flipX;
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
        IEnumerator PlayOnce(string sheet, Vector3 pos, float scale, Color tint, float fps = ImpactFps, float angle = 0f, bool flipY = false, bool flipX = false)
        {
            var frames = Frames(sheet);
            if (frames == null || frames.Length == 0) yield break;
            var px = Px(sheet);
            if (px != null) fps = px.fps;                                   // pixelfx 시트는 뽑을 때 정한 속도로
            var sr = Spawn(frames[0], pos, scale, tint, angle, flipY, flipX);
            float t = 0f;
            int last = -1;
            while (true)
            {
                int i = Mathf.FloorToInt(t * fps);
                if (i >= frames.Length) break;
                if (i != last) { sr.sprite = frames[i]; last = i; }
                yield return null;
                t += BattleTime.FxDt;
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
            var px = Px(sheet);
            float frameRate = px != null ? px.fps : TravelFps;
            for (float t = 0f; t < duration; t += BattleTime.FxDt)
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
                sr.sprite = frames[Mathf.FloorToInt(t * frameRate) % frames.Length];
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
                yield return BattleTime.FxWait(0.018f);
            }
            for (float t = 0f; t < hold; t += BattleTime.FxDt)
            {
                int f = Mathf.FloorToInt(t * TravelFps);
                for (int i = 0; i < parts.Count; i++) parts[i].sprite = frames[(f + i) % frames.Length];
                yield return null;
            }
            for (float t = 0f; t < 0.12f; t += BattleTime.FxDt)
            {
                foreach (var p in parts) { var c = p.color; c.a = 1f - t / 0.12f; p.color = c; }
                yield return null;
            }
            foreach (var p in parts) Destroy(p.gameObject);
        }

        /// <summary>pixelfx 줄기: 쓰는 팰부터 상대까지 한 장을 늘려 뻗고, hold초 동안 반복한 뒤 사라진다</summary>
        IEnumerator ConeTo(string sheet, Vector3 from, Vector3 to, float hold, Color tint)
        {
            var frames = Frames(sheet);
            var px = Px(sheet);
            if (frames == null || frames.Length == 0 || px == null) yield break;
            Vector3 dir = to - from; dir.z = 0f;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float length = dir.magnitude / (Mathf.Max(8f, px.reach) / PixelsPerUnit);
            float thick = Mathf.Clamp(length, 1f, 1.3f);
            var sr = Spawn(frames[0], from, 1f, tint, angle, dir.x < 0f);
            const float grow = 0.12f, fade = 0.12f;
            for (float t = 0f; t < hold + fade; t += BattleTime.FxDt)
            {
                sr.transform.localScale = new Vector3(length * Mathf.Clamp01(t / grow), thick, 1f);
                sr.sprite = frames[Mathf.FloorToInt(t * px.fps) % frames.Length];
                if (t > hold) { var c = tint; c.a *= 1f - (t - hold) / fade; sr.color = c; }
                yield return null;
            }
            Destroy(sr.gameObject);
        }

        IEnumerator CameraShake(float strength, float duration)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            Vector3 home = cam.transform.position;
            for (float t = 0f; t < duration; t += BattleTime.FxDt)
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
            float hitFit = Fit(target) * (critical ? 1.15f : weak ? 0.85f : 1f);
            bool mirror = from.x > target.Center.x;                              // 오른쪽(상대)에서 쓰면 그림을 뒤집는다
            bool impacted = false;
            Vector3 shift = Vector3.zero;                                        // 여러 번 터지는 기술의 자리 어긋남

            void Impact(Vector3 at)
            {
                if (missed) return;
                impacted = true;
                Run(PlayOnce(d.impact, At(d.impact, target, at) + shift, Sz(d.impact, impactScale, hitFit), d.tint, flipX: mirror));
                if (d.impact2 != null)
                    Run(DelayThen(Px(d.impact2) != null ? 0.14f : 0.08f,
                        PlayOnce(d.impact2, At(d.impact2, target, at) + shift, Sz(d.impact2, impactScale * 0.8f, hitFit), d.tint, flipX: mirror)));
                if (critical) Run(PlayOnce(PCrit, at + Vector3.up * 0.3f, Fit(target), Color.white, flipX: mirror));
            }

            if (d.cast != null) Run(PlayOnce(d.cast, At(d.cast, user, from), Sz(d.cast, 1.5f, Fit(user)), d.tint, flipX: mirror));
            float castWait = d.cast == null ? 0f : Px(d.cast) != null ? 0.45f : 0.15f;

            switch (d.kind)
            {
                case Kind.Melee:
                    yield return user.Lunge(to, 2.2f, 0.32f, () => Impact(target.Center));
                    break;

                case Kind.Projectile:
                    if (d.cast != null) yield return BattleTime.FxWait(castWait);
                    for (int i = 0; i < d.count; i++)
                    {
                        Vector3 jitter = d.count > 1 ? new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(-0.35f, 0.35f), 0f) : Vector3.zero;
                        Vector3 end = to + jitter;
                        Run(Fly(d.travel, from + jitter * 0.3f, end, d.travelTime, Sz(d.travel, d.travelScale, 1f), d.tint, d.arc, () => Impact(end)));
                        if (i < d.count - 1) yield return BattleTime.FxWait(Mathf.Min(0.1f, d.travelTime * 0.45f));
                    }
                    yield return BattleTime.FxWait(d.travelTime);
                    break;

                case Kind.Beam:
                    if (Px(d.travel) != null)
                    {
                        if (d.cast != null) yield return BattleTime.FxWait(castWait);
                        Run(ConeTo(d.travel, from + dir.normalized * 0.9f, to, 0.6f, d.tint));
                        yield return BattleTime.FxWait(0.26f);
                    }
                    else
                    {
                        Run(BeamTo(d.travel, from, to, d.travelScale, d.tint, 0.3f));
                        yield return BattleTime.FxWait(0.22f);
                    }
                    Impact(to);
                    break;

                case Kind.Strike:
                    yield return user.WindUp(target.Center, 0.18f);
                    if (castWait > 0.18f) yield return BattleTime.FxWait(castWait - 0.18f);
                    for (int i = 0; i < d.count; i++)
                    {
                        shift = i == 0 ? Vector3.zero : new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), UnityEngine.Random.Range(-0.2f, 0.2f), 0f);
                        Impact(missed ? to : target.Center);
                        if (i < d.count - 1) yield return BattleTime.FxWait(0.2f);
                    }
                    shift = Vector3.zero;
                    if (missed) Run(PlayOnce(d.impact, to, Sz(d.impact, impactScale * 0.7f, hitFit), new Color(1f, 1f, 1f, 0.5f), flipX: mirror));
                    else if (Px(d.impact) != null) yield return BattleTime.FxWait(Px(d.impact).hit);   // 번개가 떨어지는 순간에 맞춰 움찔
                    break;

                case Kind.Aura:
                {
                    // 자신에게 쓰는 기술은 몸을 웅크렸다 펴며 자기 몸에, 상대에게 쓰는 기술은 상대 몸에 기운이 감돈다
                    bool self = skill.IsSelfOnly;
                    var at = self ? user : target;
                    yield return user.WindUp(self ? user.Center + Vector3.down : target.Center, 0.2f);
                    Vector3 p = self ? user.Center : (missed ? to : target.Center);
                    Color tint = missed ? new Color(d.tint.r, d.tint.g, d.tint.b, 0.5f) : d.tint;
                    Run(PlayOnce(d.impact, At(d.impact, at, p), Sz(d.impact, d.impactScale, Fit(at)), tint, 14f));
                    if (d.impact2 != null) Run(DelayThen(0.15f, PlayOnce(d.impact2, At(d.impact2, at, p), Sz(d.impact2, d.impactScale * 0.8f, Fit(at)), tint, 14f)));
                    if (!self && !missed) Run(DelayThen(0.1f, at.Flash(new Color(0.75f, 0.6f, 1f), 2)));
                    yield return BattleTime.FxWait(0.3f);
                    break;
                }

                case Kind.Rain:
                    for (int i = 0; i < d.count; i++)
                    {
                        float x = UnityEngine.Random.Range(-0.6f, 0.6f);
                        Vector3 end = (missed ? to : target.Center) + new Vector3(x, UnityEngine.Random.Range(-0.3f, 0.2f), 0f);
                        Vector3 start = end + new Vector3(-1.2f, 6f, 0f);
                        Run(Fly(d.travel, start, end, 0.24f, d.travelScale, d.tint, false, () => Impact(end)));
                        yield return BattleTime.FxWait(0.11f);
                    }
                    yield return BattleTime.FxWait(0.24f);
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
            yield return PlayOnce(sheet, At(sheet, view, view.Center), Sz(sheet, 2.2f, Fit(view)), tint, 14f);
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
                    yield return BattleTime.FxWait(0.35f);
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

                // ───── 제로버스 2페이즈 ─────
                case BossFx.PhaseOmen:                    // 전조: 몸이 검게 깜빡이고 공허가 안쪽으로 빨려 든다
                {
                    float fit = Fit(boss);
                    Run(boss.Flash(Color.black, 4, 0.1f));
                    Run(PlayOnce(PImplosion, boss.Center, fit * 1.4f, Color.white));
                    Run(DelayThen(0.35f, PlayOnce(Portal, boss.Center, 3.4f, VoidCol, 12f)));
                    Run(DelayThen(0.7f, PlayOnce(PImplosion, boss.Center, fit * 1.8f, Color.white)));
                    Run(CameraShake(0.05f, 1.2f));
                    break;
                }
                case BossFx.PhaseShift:                   // 변신: 공허의 폭발이 연달아 터지고 마지막에 거대한 섬광
                {
                    float fit = Fit(boss);
                    boss.SetGlow(null);
                    Run(boss.Flash(VoidCol, 8, 0.05f));
                    Run(PlayOnce(PVoidOrb, boss.Center, fit * 2.2f, Color.white));
                    for (int i = 0; i < 5; i++)
                    {
                        var off = new Vector3(UnityEngine.Random.Range(-0.9f, 0.9f), UnityEngine.Random.Range(-0.7f, 0.9f), 0f) * fit;
                        Run(DelayThen(0.12f + i * 0.16f, PlayOnce(PDarkBoom, boss.Center + off, fit * (1f + 0.15f * i), Color.white)));
                    }
                    Run(DelayThen(0.95f, PlayOnce(Nuke, boss.Center, 4.2f, VoidCol)));
                    Run(DelayThen(1.0f, PlayOnce(PImplosion, boss.Center, fit * 2.4f, Color.white)));
                    Run(CameraShake(0.32f, 1.5f));
                    break;
                }
                case BossFx.Blessing:                     // 별의 가호: 금빛 기둥과 별 가루가 내 팰을 감싼다
                {
                    float fit = Fit(target);
                    Run(target.Flash(HolyGold, 3, 0.08f));
                    Run(PlayOnce(HolyLight, target.Center, 3.2f, Color.white, 14f));
                    Run(DelayThen(0.2f, PlayOnce(PSpiritUp, target.Feet, fit * 1.3f, HolyGold)));
                    Run(DelayThen(0.35f, PlayOnce(StarBurst, target.Center, 3f, HolyGold, 14f)));
                    Run(DelayThen(0.55f, PlayOnce(HealFx, target.Center, 2.6f, Color.white, 14f)));
                    break;
                }
                case BossFx.Horizon:                      // 사건의 지평선: 상대 자리에 블랙홀이 열려 빨아들인다
                {
                    float fit = Fit(target);
                    Run(PlayOnce(Portal, target.Center, 3.4f, VoidCol, 12f));
                    Run(DelayThen(0.15f, PlayOnce(PImplosion, target.Center, fit * 1.6f, Color.white)));
                    Run(DelayThen(0.3f, target.HitReact(true)));
                    Run(DelayThen(0.3f, CameraShake(0.14f, 0.5f)));
                    Run(DelayThen(0.55f, PlayOnce(PVoidOrb, boss.Center, Fit(boss) * 1.2f, Color.white)));
                    break;
                }
                case BossFx.Nova:                         // 초신성 붕괴: 깨진 실드가 터지며 상대까지 폭발이 번진다
                {
                    Run(boss.Flash(Color.white, 2, 0.05f));
                    Run(PlayOnce(PDarkBoom, boss.Center, Fit(boss) * 1.5f, Color.white));
                    Run(DelayThen(0.15f, PlayOnce(Shockwave, target.Center, 3.2f, VoidCol)));
                    Run(DelayThen(0.2f, PlayOnce(PDarkBoom, target.Center, Fit(target) * 1.2f, Color.white)));
                    Run(DelayThen(0.2f, target.HitReact(true)));
                    Run(CameraShake(0.2f, 0.5f));
                    break;
                }
            }
            while (running > 0) yield return null;
        }

        // ───── 제로버스 2페이즈: 몸 아래에서 계속 일렁이는 공허의 불꽃 ─────
        Coroutine bossAura;
        SpriteRenderer bossAuraRenderer;

        /// <summary>보스 발밑에 계속 타오르는 공허의 불꽃을 켜고 끈다 (보스가 화면에서 사라지면 저절로 꺼진다)</summary>
        public void SetBossAura(PalView boss, bool on)
        {
            if (bossAura != null) { StopCoroutine(bossAura); bossAura = null; }
            if (bossAuraRenderer != null) { Destroy(bossAuraRenderer.gameObject); bossAuraRenderer = null; }
            if (on && boss != null) bossAura = StartCoroutine(BossAuraLoop(boss));
        }

        IEnumerator BossAuraLoop(PalView boss)
        {
            var frames = Frames(PShadowFlames);
            if (frames == null || frames.Length == 0) yield break;
            var px = Px(PShadowFlames);
            float fps = px != null ? px.fps : ImpactFps;
            var sr = Spawn(frames[0], boss.Feet, Fit(boss) * 1.3f, new Color(0.75f, 0.45f, 1f, 0.5f));
            sr.sortingLayerID = boss.SortingLayerId;
            sr.sortingOrder = boss.SortingOrder - 1;                       // 팰 뒤
            bossAuraRenderer = sr;
            float t = 0f;
            while (boss != null && boss.IsVisible && sr != null)
            {
                sr.transform.position = boss.Feet;
                sr.sprite = frames[Mathf.FloorToInt(t * fps) % frames.Length];
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
            bossAuraRenderer = null;
            bossAura = null;
        }

        IEnumerator DelayThen(float seconds, IEnumerator then)
        {
            yield return BattleTime.FxWait(seconds);
            yield return then;
        }
    }
}
