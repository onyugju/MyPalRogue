using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>테스트용 파티 한 마리 (어떤 팰을 몇 레벨로)</summary>
    [System.Serializable]
    public class PartyMember
    {
        public PalData pal;
        [Range(1, GameConfig.MaxLevel)] public int level = 10;
    }

    /// <summary>
    /// 전투 화면 전체를 움직이는 컨트롤러. Battle(전투 로직)이 쌓아 둔 진행 기록을 순서대로 재생한다.
    /// 흐름: 등장 메시지 → 커맨드 선택 → (싸운다) 스킬 선택 → 턴 재생(메시지·HP바·쓰러짐) → 다시 커맨드
    ///       상대를 쓰러뜨리거나 잡으면 → 돈 받기 → 다음 웨이브 (파티 상태는 그대로 이어진다, Run 참고)
    /// 입력: 마우스 클릭, 방향키 + Enter/Space(확인), Esc(뒤로). 화면 UI 이벤트만 쓰기 때문에 입력 시스템과 상관없이 동작한다.
    /// 참조 칸들은 메뉴 PalRogue/10 이 자동으로 연결한다.
    /// </summary>
    public class BattleScreen : MonoBehaviour
    {
        [Header("전투 설정 (테스트용)")]
        [Tooltip("내 파티 (최대 6마리, 1번이 먼저 나간다). 비어 있으면 아래 playerPal 한 마리로 싸운다")]
        public List<PartyMember> playerParty = new List<PartyMember>();
        public PalData playerPal;
        [Tooltip("야생 팰 목록이 비어 있을 때만 쓰는 상대")]
        public PalData enemyPal;
        [Range(1, GameConfig.MaxLevel)] public int playerLevel = 10;
        [Tooltip("(웨이브 방식에서는 쓰지 않음 - 적 레벨은 GameConfig.EnemyLevel)")]
        [Range(1, GameConfig.MaxLevel)] public int enemyLevel = 8;

        [Header("웨이브 (런)")]
        [Tooltip("웨이브마다 이 중에서 상대가 나온다. 비어 있으면 플레이할 때 Assets의 모든 PalData로 자동으로 채운다\n(컴포넌트 오른쪽 위 ⋮ → '야생 팰 목록 다시 채우기')")]
        public List<PalData> wildPals = new List<PalData>();
        [Tooltip("오른쪽 위 '마을 - 1' 글자. 비어 있으면 이름(AreaText)으로 찾는다")]
        public TMP_Text areaText;
        [Tooltip("오른쪽 위 '1,000 G' 글자. 비어 있으면 이름(MoneyText)으로 찾는다")]
        public TMP_Text moneyText;
        [Tooltip("-1이면 매번 다른 결과, 0 이상이면 같은 전투를 다시 볼 수 있다")]
        public int seed = -1;

        [Header("필드")]
        public PalView playerView;
        public PalView enemyView;
        public PalInfoBox playerInfo;
        public PalInfoBox enemyInfo;

        [Header("메시지 / 커맨드")]
        public TMP_Text messageText;
        public GameObject commandBox;
        public Image commandCursor;
        public Button fightButton, sphereButton, palsButton, runButton;

        [Header("스킬 선택")]
        public GameObject skillPanel;
        public GameObject skillInfoPanel;
        public Image skillCursor;
        public Button[] skillButtons;      // 4개
        public TMP_Text[] skillTexts;      // 4개
        [Tooltip("스킬 속성 뱃지 (Skill_type 폴더 이미지)")]
        public RawImage typeBadge;
        [Tooltip("뱃지 이미지가 없는 속성(Void 등)일 때 대신 보여줄 글자")]
        public TMP_Text typeFallbackText;
        [Tooltip("스킬 종류 뱃지 (Resources의 Attack Icon / Status Icon 이미지)")]
        public RawImage categoryBadge;
        [Tooltip("종류 아이콘 이미지를 못 찾았을 때 대신 보여줄 글자")]
        public TMP_Text categoryText;
        public TMP_Text ppValueText, powerValueText, accuracyValueText;

        [Header("파티 화면")]
        public PartyScreen partyScreen;

        [Header("예/아니오 선택 창")]
        public ChoiceMenu choiceMenu;

        [Header("팰 스피어")]
        public SphereMenu sphereMenu;
        [Tooltip("시작할 때 가진 스피어 개수: 팰 / 기가 / 하이퍼 / 얼티밋 / 엑조틱")]
        public int[] startingSpheres = { 10, 5, 3, 2, 1 };

        [Header("끝난 뒤 다시 시작")]
        public Button restartButton;

        [Header("스타팅 팰 선택")]
        [Tooltip("켜면 모험을 시작할 때마다 스타팅 팰 선택 화면이 나온다. 끄면 위 playerParty(테스트 파티)로 바로 시작한다")]
        public bool useStarterSelect = true;

        static float CharsPerSecond => GameSettings.CharsPerSecond;   // 설정의 대사 속도
        const string SelectPrompt = "팰을 선택하세요.";   // 파티 화면 안내 문구
        const float LineHoldSeconds = 1.2f;    // 문장이 다 나온 뒤 자동으로 넘어가기까지 기다리는 시간
        const float SkipGuardSeconds = 0.25f;
        const float HeldLineSeconds = 0.3f;    // 넘기기 키를 누르고 있을 때 한 문장을 보여 주는 최소 시간 (읽을 틈은 남긴다)  // 메시지가 뜬 직후 이 시간 동안은 눌러도 넘기지 않음
        static readonly Color Dim = new Color(0.55f, 0.55f, 0.55f);
        static readonly Color AttackColor = new Color(0.85f, 0.33f, 0.18f);
        static readonly Color StatusColor = new Color(0.4f, 0.5f, 0.75f);
        // Skill_type 이미지(1024x572)에서 뱃지가 실제로 그려진 부분만 잘라 쓰는 영역 (투명 여백 제외)
        static readonly Rect BadgeUv = new Rect(255f / 1024f, 129f / 572f, 514f / 1024f, 310f / 572f);
        // Attack Icon / Status Icon 이미지(1024x572)에서 그려진 부분 (두 이미지를 모두 덮는 범위)
        static readonly Rect CategoryUv = new Rect(152f / 1024f, 129f / 572f, 737f / 1024f, 318f / 572f);
        const string AttackIconPath = "Attack Icon";
        const string StatusIconPath = "Status Icon";

        enum Mode { Busy, Command, Skill, Ended }
        Mode mode = Mode.Busy;
        Battle battle;
        MenuEntry lastEntry;

        void Start()
        {
            GameAudio.Ensure();
            AchievementToast.Font = messageText != null ? messageText.font : null;
#if UNITY_EDITOR
            FillWildPals();                          // 에디터에서는 새로 추가한 팰까지 항상 야생 목록에 넣는다
#endif
            bool hasPlayer = playerPal != null || playerParty.Any(m => m != null && m.pal != null);
            if ((!hasPlayer && !useStarterSelect) || WildPool().Count == 0)
            {
                Debug.LogWarning("[BattleScreen] 내 파티(playerParty 또는 playerPal)와 야생 팰 목록(wildPals 또는 enemyPal)에 PalData 에셋을 넣어주세요.");
                enabled = false;
                return;
            }

            var missing = MissingReferences();
            if (missing.Count > 0)
            {
                Debug.LogError("[BattleScreen] 연결되지 않은 칸이 있습니다: " + string.Join(", ", missing) +
                               "\n→ 메뉴 'PalRogue > 10. 전투 화면 연결'을 다시 실행해 주세요.");
                enabled = false;
                return;
            }

            messageText.textWrappingMode = TextWrappingModes.Normal;
            ItemCatalog.SetSkillPool(KnownSkills());          // 기술머신 아이템 목록 만들기
            if (areaText == null) areaText = FindLabel("AreaText");
            if (moneyText == null) moneyText = FindLabel("MoneyText");
            // 아이템 줄: 왼쪽 위 = 내 아이템 / 오른쪽 위 = 상대가 지닌 아이템 (상대 정보창이 보일 때만)
            itemBar = ItemBar.Create(messageText.font, () => run,
                () => battle != null && enemyInfo.gameObject.activeSelf && battle.Enemy != null && !battle.Enemy.IsFainted ? battle.Enemy : null,
                areaText, moneyText,
                () => (partyScreen != null && partyScreen.IsOpen) || (summaryScreen != null && summaryScreen.gameObject.activeSelf));   // 파티·능력치 화면에서는 숨김

            Bind(fightButton, OnFight);
            Bind(sphereButton, OnSphere);
            Bind(palsButton, OnPals);
            Bind(runButton, OnRun);
            for (int i = 0; i < skillButtons.Length; i++)
            {
                int index = i;
                Bind(skillButtons[i], () => OnSkill(index));
            }
            Bind(restartButton, () =>
            {
                if (mode != Mode.Ended) return;
                if (fromSession && GameSession.CanLoad(GameSession.TitleScene))
                {
                    mode = Mode.Busy;
                    restartButton.gameObject.SetActive(false);
                    GameSession.Go(GameSession.TitleScene, messageText.font);
                }
                else StartCoroutine(BeginRun(false));
            });

#if UNITY_EDITOR
            if (TryStartAstralymTest()) return;       // 메뉴 61(F6): 강한 테스트 파티로 바로 제로버스전
#endif
            // 타이틀 → 팰 선택·저장 슬롯을 거쳐 들어온 경우 (씬 이동). 아니면(에디터에서 이 씬을 바로 플레이) 이 씬 안에서 팰 선택
            if (GameSession.Loaded != null)
            {
                var save = GameSession.Loaded;
                GameSession.Loaded = null;
                fromSession = true;
                StartLoadedRun(save);
            }
            else if (GameSession.NewParty != null && GameSession.NewParty.Count > 0)
            {
                var picked = GameSession.NewParty;
                GameSession.NewParty = null;
                fromSession = true;
                runNumber = PlayerSave.NextRunNumber();
                playSeconds = 0;
                StartNewRun(picked.Select(d => new Pal(d, GameConfig.StarterLevel)).ToList());
            }
            else StartCoroutine(BeginRun(true));
        }

        void OnDestroy() => battle?.Detach();

        // 선택이 사라져도(빈 곳 클릭 등) 키보드 조작이 끊기지 않게 마지막 항목을 다시 잡아준다
        void Update()
        {
            if (run != null && mode != Mode.Ended) playSeconds += Time.unscaledDeltaTime;   // 저장 슬롯의 플레이 시간
            if (mode == Mode.Command && MenuKeys.Cancel && Time.unscaledTime - menuClosedAt > 0.2f && Time.unscaledTime - commandShownAt > 0.15f)
                StartCoroutine(OpenGameMenu());
            UpdateSkillPeek();
            if (mode != Mode.Command && mode != Mode.Skill && mode != Mode.Ended) return;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null && lastEntry != null && lastEntry.gameObject.activeInHierarchy)
                es.SetSelectedGameObject(lastEntry.gameObject);
        }

        /// <summary>Inspector에서 비어 있는 참조 칸의 이름 목록</summary>
        List<string> MissingReferences()
        {
            var list = new List<string>();
            void Check(UnityEngine.Object o, string label) { if (o == null) list.Add(label); }

            Check(playerView, "playerView"); Check(enemyView, "enemyView");
            Check(playerInfo, "playerInfo"); Check(enemyInfo, "enemyInfo");
            Check(messageText, "messageText"); Check(commandBox, "commandBox"); Check(commandCursor, "commandCursor");
            Check(fightButton, "fightButton"); Check(sphereButton, "sphereButton");
            Check(palsButton, "palsButton"); Check(runButton, "runButton");
            Check(skillPanel, "skillPanel"); Check(skillInfoPanel, "skillInfoPanel"); Check(skillCursor, "skillCursor");
            Check(typeBadge, "typeBadge"); Check(typeFallbackText, "typeFallbackText");
            Check(categoryBadge, "categoryBadge"); Check(categoryText, "categoryText");
            Check(ppValueText, "ppValueText"); Check(powerValueText, "powerValueText"); Check(accuracyValueText, "accuracyValueText");
            Check(restartButton, "restartButton");
            if (skillButtons == null || skillButtons.Length < 4 || skillButtons.Any(b => b == null)) list.Add("skillButtons");
            if (skillTexts == null || skillTexts.Length < 4 || skillTexts.Any(t => t == null)) list.Add("skillTexts");
            return list;
        }

        // ───────── 런(모험) / 웨이브 ─────────
        Run run;

        /// <summary>새 모험을 처음부터 시작한다 (파티·스피어·돈 초기화, 웨이브 1)</summary>
        void StartNewRun(List<Pal> party)
        {
            var rng = seed >= 0 ? new System.Random(seed) : new System.Random();
            BeginWithRun(new Run(party, startingSpheres, rng));
        }

        // ───────── 저장 / 불러오기 ─────────
        /// <summary>타이틀을 거쳐 들어왔는지 (끝나면 타이틀로 돌아간다)</summary>
        bool fromSession;
        /// <summary>테스트 판 (메뉴 61: 저장·이력·업적을 남기지 않는다)</summary>
        bool debugRun;
        double playSeconds;
        int runNumber;

        /// <summary>저장 슬롯의 모험을 이어서 한다 (저장된 웨이브의 처음부터)</summary>
        void StartLoadedRun(RunSave save)
        {
            debugRun = false;
            playSeconds = save.playSeconds;
            runNumber = save.runNumber;
            var restored = SaveSlots.Restore(save, WildPool(), KnownSkills());
            if (restored.Party.Count == 0)
            {
                Debug.LogError("[저장] 불러온 파티가 비어 있어서 새 모험을 시작합니다.");
                StartCoroutine(BeginRun(true));
                return;
            }
            resumeNote = $"{restored.AreaName} - 웨이브 {restored.Wave}부터 이어서 모험한다!";
            BeginWithRun(restored);
        }
        string resumeNote;          // 이어하기로 시작했을 때 첫 웨이브에 한 번 보여 주는 문구

        /// <summary>웨이브가 시작될 때마다 그 슬롯에 자동 저장 (포켓로그처럼)</summary>
        /// <summary>
        /// 지금 런을 슬롯에 저장한다. 전투가 끝나고 보상을 받은 뒤(다음 웨이브로 넘어가기 직전)마다 저장하고 아이콘을 보여 준다.
        /// 불러오면 그 다음 웨이브부터 이어서 한다.
        /// </summary>
        void AutoSave(bool showIcon)
        {
            if (GameSession.Slot < 0 || run == null) return;
            bool ok = SaveSlots.Save(GameSession.Slot, SaveSlots.Capture(run, playSeconds, runNumber));
            PlayerSave.LastSlot = GameSession.Slot;            // 타이틀의 "계속하기"
            if (showIcon || !ok) { SaveIcon.Show(ok, messageText.font); if (ok) GameAudio.Se("save", 0.6f); }
        }

        void BeginWithRun(Run newRun)
        {
            run = newRun;
            debugRun = false;
            Achievements.StartRun();
            if (partyScreen != null) partyScreen.OpenSummary = OpenSummary;   // 파티 화면 "능력치를 본다"
            spheres = run.Spheres;                     // 스피어 메뉴가 쓰는 배열 = 런이 가진 배열
            ApplyAreaScenery(true);                    // 첫 지역(초원) 배경으로 (게임 오버 후 다시 시작해도)
            AutoSave(false);                           // 새 모험도 바로 슬롯에 남긴다 (타이틀의 "계속하기"용, 아이콘 없이)
            StartWave(true);
        }

        // ───────── 스타팅 팰 선택 ─────────
        StarterSelectScreen starterScreen;
        StarterSelectScreen StarterScreen => starterScreen != null ? starterScreen : (starterScreen = StarterSelectScreen.Create(messageText.font));

        /// <summary>
        /// 새 모험 시작: 스타팅 팰 선택 화면에서 파티를 고른 뒤(코스트 합계 10 이하) 웨이브 1로.
        /// 처음 켰을 때는 바로 선택 화면이 나오고, 게임 오버 뒤에는 검게 어두워졌다가 선택 화면으로.
        /// </summary>
        IEnumerator BeginRun(bool first)
        {
            mode = Mode.Busy;
            restartButton.gameObject.SetActive(false);
            if (!useStarterSelect)
            {
                StartNewRun(BuildPlayerParty());
                yield break;
            }

            HidePanels();
            if (!first) yield return Fade.FadeTo(1f, 0.4f);
            var screen = StarterScreen;
            GameAudio.Bgm("menu");
            screen.Open(WildPool());
            if (!first) yield return Fade.FadeTo(0f, 0.3f);
            yield return screen.Choose(WildPool());

            yield return Fade.FadeTo(1f, 0.4f);
            screen.Hide();
            var party = screen.Result.Select(d => new Pal(d, GameConfig.StarterLevel)).ToList();
            messageText.text = "";
            StartNewRun(party);
            yield return Fade.FadeTo(0f, 0.4f);
        }

        /// <summary>지금 웨이브의 상대를 만들고 전투를 시작한다</summary>
        void StartWave(bool firstWave)
        {
            battle?.Detach();
            AstralymPhase.RunNumber = runNumber;            // 제로버스는 몇 번째 모험인지 알고 있다
            if (voidPhase) { voidPhase = false; Fx.SetBossAura(null, false); ApplyAreaScenery(true); }   // 2페이즈 끝 (다시 도전 등)
            var synergyNews = run.UpdateSynergies();             // 첫 웨이브: 시작 파티의 속성 시너지
            if (synergyNews.Count > 0) pendingSynergyMessages = synergyNews;
            var enemy = run.CreateEnemy(WildPool());
            GameAudio.Bgm(GameAudio.AreaBgm(run.Wave, run.EnemyKind));      // 지역 음악 / 보스 음악 / 최종 보스 음악
            battle = new Battle(run.Party, new List<Pal> { enemy }, run.Rng, null, firstWave, run.EnemyKind);
            run.SyncParty(battle.PlayerParty);         // 쓰러진 팰 대신 다른 팰이 앞으로 나왔을 수 있다
            battle.ExpMultiplier = run.ExpMultiplier;  // 경험부적
            battle.ExpShareRatio = run.ExpShareRatio;  // 학습장치
            battle.PlayerBerrySave = run.BerrySave;     // 열매 주머니
            battle.EnemySmartness = GameConfig.EnemySmartness(run.Wave, run.EnemyKind);   // 상대 AI
            battle.LevelCap = run.LevelCap;            // 레벨 한도 (10웨이브 보스를 이길 때마다 오른다)
            GameConfig.ActiveLevelCap = run.LevelCap;  // 한도에 닿은 레벨은 빨간 숫자

            // 내 팰을 새로 내보내면 (첫 웨이브 등) 스피어를 던지는 연출 전까지 숨겨 둔다
            if (battle.PlayerSentOutAtStart)
            {
                playerView.Hide();
                playerInfo.gameObject.SetActive(false);
            }
            else
            {
                playerView.Show(battle.Player.Data, true);
                playerInfo.gameObject.SetActive(true);
                playerInfo.Refresh(battle.Player);
            }
            enemyView.Show(battle.Enemy.Data, false);
            if (sphereView != null) sphereView.Hide();
            enemyInfo.gameObject.SetActive(false);              // 상대가 다 들어온 뒤에 나타난다
            enemyInfo.Refresh(battle.Enemy);
            UpdateHud();

            restartButton.gameObject.SetActive(false);
            StartCoroutine(Intro(firstWave));
        }

        // ───────── 등장 연출 (포켓로그처럼) ─────────
        const float SlideSeconds = 0.9f;          // 상대가 왼쪽 밖에서 들어오는 시간
        const float SlideDistance = 18f;          // 화면 한 폭 (월드 유닛, 320px ÷ 18)
        const float BoxSlideSeconds = 0.25f;      // 정보창이 미끄러져 들어오는 시간
        const float BoxSlidePixels = 160f;
        // 던지기 시작: 화면 왼쪽 끝, 메시지창 바로 위 (화면 픽셀 x 0, y 128 → 월드). 더 아래는 메시지창에 가려진다
        static readonly Vector3 ThrowFromWorld = new Vector3(-9.2f, -2.1f, 0f);
        const float SendOutThrowSeconds = 0.6f, SendOutArc = 2.6f;

        Transform enemyBase, playerBase;
        Vector3 enemyBaseHome, playerBaseHome;
        Vector2 enemyInfoHome, playerInfoHome;
        bool homesSaved;
        SphereThrowView sendOutView;

        void SaveHomes()
        {
            if (homesSaved) return;
            homesSaved = true;
            var field = enemyView.transform.parent;
            enemyBase = field != null ? field.Find("EnemyBase") : null;
            playerBase = field != null ? field.Find("PlayerBase") : null;
            if (enemyBase != null) enemyBaseHome = enemyBase.position;
            if (playerBase != null) playerBaseHome = playerBase.position;
            enemyInfoHome = ((RectTransform)enemyInfo.transform).anchoredPosition;
            playerInfoHome = ((RectTransform)playerInfo.transform).anchoredPosition;
        }

        /// <summary>상대 팰이 발판과 함께 왼쪽 화면 밖에서 들어온다 (첫 웨이브는 내 발판도 오른쪽에서). 끝나면 상대 정보창이 들어온다</summary>
        IEnumerator EnemyEntrance(bool firstWave)
        {
            SaveHomes();
            bool boss = run != null && run.EnemyKind != BossKind.None;
            if (boss) enemyView.SetSilhouette(true);          // 보스는 검은 실루엣으로 들어온다
            Vector3 enemyHome = enemyView.transform.position;
            for (float t = 0f; t < SlideSeconds; t += BattleTime.Dt)
            {
                float k = t / SlideSeconds;
                float off = (1f - k) * (1f - k) * (1f - k) * SlideDistance;    // 빠르게 들어와서 천천히 멈춘다
                enemyView.transform.position = enemyHome + Vector3.left * off;
                if (enemyBase != null) enemyBase.position = enemyBaseHome + Vector3.left * off;
                if (firstWave && playerBase != null) playerBase.position = playerBaseHome + Vector3.right * off;
                yield return null;
            }
            enemyView.transform.position = enemyHome;
            if (enemyBase != null) enemyBase.position = enemyBaseHome;
            if (playerBase != null) playerBase.position = playerBaseHome;

            if (boss) yield return BossCutIn();

            enemyInfo.gameObject.SetActive(true);
            yield return SlideBox((RectTransform)enemyInfo.transform, enemyInfoHome, -BoxSlidePixels);
        }

        // ───────── 보스 등장 연출 ─────────
        BossIntro bossIntro;
        static readonly Color BossColor = new Color(0.95f, 0.25f, 0.2f);     // 보스: 빨강
        static readonly Color LegendColor = new Color(1f, 0.75f, 0.2f);      // 속성의 왕: 금색
        static readonly Color FinalColor = new Color(0.7f, 0.35f, 1f);       // 최종 보스: 보라
        const float BossShakeSeconds = 0.35f, BossShakeAmount = 0.12f;

        /// <summary>
        /// 실루엣으로 들어온 보스 위로 컷인("보스 출현!" / "속성의 왕 출현!" / "최종 보스 출현!")이 지나간 뒤,
        /// 하얗게 번쩍이며 본모습이 드러나고 화면이 흔들린다.
        /// </summary>
        IEnumerator BossCutIn()
        {
            if (bossIntro == null) bossIntro = BossIntro.Create(messageText.font);
            var enemy = battle?.Enemy;
            string name = enemy != null ? PalName(enemy) : "";
            string lv = enemy != null ? $"Lv.{enemy.Level}" : "";
            var (title, color) = run.EnemyKind switch
            {
                BossKind.Final => ("최종 보스 출현!", FinalColor),
                BossKind.Legend => ("속성의 왕 출현!", LegendColor),
                _ => ("보스 출현!", BossColor),
            };
            // 전설 팰은 이명을 이름 위에 띄운다:  "전설 속 유성의 용" / 제트래곤 / 속성의 왕 출현! · Lv.160
            string epithet = GameConfig.EpithetOf(enemy?.Data);
            GameAudio.Se("menu_open");
            if (epithet != null) yield return bossIntro.Play(epithet, name, $"{title}  {lv}", color);
            else yield return bossIntro.Play(null, title, $"{name}  {lv}", color);

            // 본모습 공개: 실루엣 → 하얀 번쩍임 → 원래 색, 화면 흔들림
            GameAudio.Se("hit_strong");
            enemyView.SetSilhouette(false);
            StartCoroutine(enemyView.Flash(Color.white, 2, 0.06f));
            yield return CameraShake(BossShakeSeconds, BossShakeAmount);
        }

        // ───────── 제로버스 2페이즈 ─────────
        /// <summary>2페이즈가 시작되면 true: 지역 이름이 "??? - ???"로 보인다 (다음 웨이브·새 모험에서 풀림)</summary>
        bool voidPhase;
        static readonly Color VoidPhaseTint = new Color(0.86f, 0.74f, 1f);    // 2페이즈 제로버스: 공허에 물든 색

        static Sprite phase2Sprite;
        static float phase2Scale;

        /// <summary>제로버스 2페이즈 그림을 화면 높이에 맞는 크기로 만든다 (없으면 null)</summary>
        MegaShift megaShift;

        /// <param name="viewScale">상대 자리 팰의 크기 배율 (그림이 이 배율로 커져도 화면 높이가 FinalPhase2Height가 되게)</param>
        static Sprite Phase2Sprite(float viewScale)
        {
            if (phase2Sprite != null && Mathf.Approximately(phase2Scale, viewScale)) return phase2Sprite;
            var src = Resources.LoadAll<Sprite>(GameConfig.FinalPhase2Sprite).OrderBy(x => x.name, System.StringComparer.Ordinal).FirstOrDefault();
            Texture2D tex = src != null ? src.texture : Resources.Load<Texture2D>(GameConfig.FinalPhase2Sprite);
            if (tex == null) { Debug.LogWarning($"[PalRogue] 2페이즈 그림이 없습니다: Resources/{GameConfig.FinalPhase2Sprite}"); return null; }
            var rect = src != null ? src.rect : new Rect(0f, 0f, tex.width, tex.height);
            phase2Scale = viewScale;
            phase2Sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), rect.height * Mathf.Max(0.01f, viewScale) / GameConfig.FinalPhase2Height);
            phase2Sprite.name = "Astralym_Phase2";
            return phase2Sprite;
        }

        /// <summary>보스 패턴마다 곁들이는 소리 (새 패턴만. 예전 패턴은 연출만)</summary>
        static void PatternSound(BossFx fx)
        {
            switch (fx)
            {
                case BossFx.PhaseOmen: GameAudio.StopBgm(1.5f); GameAudio.Se("stat_down"); break;   // 음악이 멎고 공허가 요동친다
                case BossFx.Blessing: GameAudio.Se("shine"); break;
                case BossFx.Horizon: GameAudio.Se("stat_down"); break;
                case BossFx.Nova: GameAudio.Se("hit_strong"); break;
            }
        }

        /// <summary>
        /// 제로버스 2페이즈 변신: 공허 폭발 → 화면이 하얗게 타오르는 사이 배경이 End로, 지역이 ??? - ???로 바뀐다 →
        /// "멸성의 재앙" 대형 컷인 → 크게 흔들림. HP바는 변신 직전 칸에서 다음 HpChanged로 가득 차오른다.
        /// </summary>
        IEnumerator PlayPhaseShift(BattleEvent ev)
        {
            var boss = ev.Pal;
            if (bossIntro == null) bossIntro = BossIntro.Create(messageText.font);
            if (megaShift == null) megaShift = MegaShift.Create();
#if UNITY_EDITOR
            bool recording = debugRun;                                     // 테스트 판: 연출을 Temp/PhaseShiftFrames에 프레임으로 남긴다
            if (recording) StartCoroutine(ShiftRecorder.Record(() => recording));
#endif
            var ui = new[] { Group(enemyInfo), Group(playerInfo) };       // 연출 동안 정보창은 흐려졌다가 2페이즈에서 다시 보인다

            // 메가진화처럼: 빛의 구 → 굳어서 금 → 화면이 멈추고 깨지며 2페이즈가 드러난다
            yield return megaShift.Play(enemyView, playerView, ui, () =>
            {
                // (멈춘 화면 뒤에서) 2페이즈 맵·모습·정보창으로 바꿔 둔다
                voidPhase = true;
                AreaScenery.Apply(enemyView.transform.parent, GameConfig.FinalPhase2Biome);
                UpdateHud();
                var anim = Resources.Load<RuntimeAnimatorController>(GameConfig.FinalPhase2Animator);
                if (!enemyView.SetAnimatedForm(anim, GameConfig.FinalPhase2Height, boss.Data))   // 2페이즈 대기 애니메이션 (Astralym_Phase2)
                {
                    var form = Phase2Sprite(enemyView.transform.localScale.y);
                    if (form != null) enemyView.SetForm(form, boss.Data);                       // 애니메이션이 없으면 멈춘 그림
                    else { enemyView.SetBaseTint(VoidPhaseTint); Fx.SetBossAura(enemyView, true); }   // 그림도 없으면 공허에 물든 색 + 불꽃
                }
                enemyInfo.Refresh(boss);                                    // 실드 6칸 · Lv.??? · 새 능력치 단계
                enemyInfo.SetHp(ev.MaxHp > 0 ? Mathf.Clamp01(ev.From / (float)ev.MaxHp) : 0f);
                GameAudio.Bgm(GameConfig.FinalPhase2Bgm);
            });

            // 멸성의 재앙 / 제로버스 / Lv.???
            yield return bossIntro.PlayGrand(GameConfig.FinalPhase2Epithet, PalName(boss), "Lv.???", FinalColor);
            GameAudio.Se("hit_strong");
            StartCoroutine(enemyView.Flash(Color.white, 2, 0.06f));
            yield return CameraShake(0.55f, 0.2f);
#if UNITY_EDITOR
            recording = false;
#endif
        }

        static CanvasGroup Group(Component c)
        {
            if (c == null) return null;
            var g = c.GetComponent<CanvasGroup>();
            return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
        }

        IEnumerator CameraShake(float seconds, float amount)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            var home = cam.transform.position;
            for (float t = 0f; t < seconds; t += BattleTime.FxDt)
            {
                float a = amount * (1f - t / seconds);
                cam.transform.position = home + new Vector3(Random.Range(-a, a), Random.Range(-a, a), 0f);
                yield return null;
            }
            cam.transform.position = home;
        }

        /// <summary>내 팰 내보내기: (나와 있던 팰은 스피어로 돌아오고) 화면 왼쪽 아래 밖에서 그 팰을 잡은 스피어를 던져 열린 곳에서 튀어나온다</summary>
        IEnumerator PlaySendOut(Pal pal)
        {
            SaveHomes();
            yield return PlayRecall();                           // 아직 나와 있는 팰이 있으면 먼저 넣는다

            if (sendOutView == null) sendOutView = SphereThrowView.Create(playerView.transform.parent);
            playerView.Show(pal.Data, true);                     // 모습·위치를 먼저 정해 두고 숨긴다
            playerView.Hide();
            Vector3 open = new Vector3(playerView.transform.position.x, playerView.feetWorldY + 0.8f, 0f);
            yield return sendOutView.Throw(pal.CaughtSphere, open, SendOutThrowSeconds, ThrowFromWorld, SendOutArc);
            sendOutView.Hide();
            yield return playerView.EmergeFrom(open);

            playerInfo.gameObject.SetActive(true);
            playerInfo.Refresh(pal);
            yield return SlideBox((RectTransform)playerInfo.transform, playerInfoHome, BoxSlidePixels);
        }

        /// <summary>"돌아와!": 나와 있던 내 팰이 빨갛게 변하며 스피어 쪽으로 빨려 들어간다</summary>
        IEnumerator PlayRecall()
        {
            playerInfo.gameObject.SetActive(false);
            if (!playerView.IsVisible) yield break;
            Vector3 recallPoint = new Vector3(playerView.transform.position.x, playerView.feetWorldY + 0.6f, 0f);
            yield return playerView.AbsorbInto(recallPoint, 0.25f);
        }

/// <summary>실드가 깨질 때 정보창이 좌우로 흔들린다</summary>
        IEnumerator ShakeBox(RectTransform box)
        {
            Vector2 home = box.anchoredPosition;
            const float duration = 0.3f;
            for (float t = 0f; t < duration; t += BattleTime.Dt)
            {
                float k = t / duration;
                box.anchoredPosition = home + new Vector2(Mathf.Sin(k * Mathf.PI * 8f) * 2f * (1f - k), 0f);
                yield return null;
            }
            box.anchoredPosition = home;
        }

                IEnumerator SlideBox(RectTransform box, Vector2 home, float fromDx)
        {
            for (float t = 0f; t < BoxSlideSeconds; t += BattleTime.Dt)
            {
                float k = t / BoxSlideSeconds;
                box.anchoredPosition = home + Vector2.right * (fromDx * (1f - k) * (1f - k));
                yield return null;
            }
            box.anchoredPosition = home;
        }

        /// <summary>웨이브가 끝났을 때(이김 / 잡음 / 도망): 돈 받기 → 다음 웨이브 또는 게임 끝</summary>
        IEnumerator FinishWave(bool fled)
        {
            mode = Mode.Busy;
            HidePanels();
            if (battle.CaughtPal != null)
            {
                bool newStarter = PlayerSave.RecordCatch(battle.CaughtPal.Data);   // 처음 잡은 팰은 다음 모험부터 스타팅 팰로 고를 수 있다
                run.CaughtCount++;
                Achievements.CheckCatch(GameConfig.IsLegendary(battle.CaughtPal.Data));
                if (newStarter)
                {
                    GameAudio.Se("achv");
                    yield return ShowMessage($"{Josa.I(PalName(battle.CaughtPal))} 스타팅 팰 목록에 등록되었다!");
                }
            }
            if (!fled && run.EnemyKind != BossKind.None)                       // 보스·전설을 쓰러뜨리거나 잡음
            {
                bool legend = run.EnemyKind == BossKind.Legend || run.EnemyKind == BossKind.Final;
                run.BossesDefeated++;
                if (!debugRun) { PlayerSave.AddBossDefeat(legend); Achievements.CheckBoss(legend); }   // 테스트 판은 기록하지 않는다
            }
            if (battle.PendingCatch != null) yield return ResolveCatch();
            run.SyncParty(battle.PlayerParty);
            Achievements.CheckParty(run.Party.Count);
            foreach (var msg in run.UpdateSynergies()) yield return ShowMessage(msg);   // 파티가 바뀌면 속성 시너지도

            // 상대가 사라졌으니 상대 정보창·스피어도 치운다
            enemyInfo.gameObject.SetActive(false);
            enemyView.Hide();
            if (sphereView != null) sphereView.Hide();

            if (!fled)
            {
                int money = Mathf.RoundToInt(GameConfig.WinMoney(run.Wave, run.IsBossWave) * run.MoneyMultiplier);   // 보스는 3배, 부적금화 +20%씩
                run.Money += money;
                UpdateHud();
                yield return ShowMessage($"{money:N0}G를 손에 넣었다!");
            }

            if (run.IsWiped) { yield return GameOver(); yield break; }

            if (run.Wave >= GameConfig.FinalWave)
            {
                GameAudio.Bgm("end");
                if (!debugRun) Achievements.Cleared();
                yield return ShowMessage("공허의 왕을 쓰러뜨리고 모든 웨이브를 돌파했다!");
                yield return ShowMessage($"웨이브 {GameConfig.FinalWave} 클리어! 모험 성공!");
                yield return EndRun();
                yield break;
            }

            if (!fled)
            {
                // 고정 보상 (8웨이브: 학습장치, 25웨이브: 경험부적)
                foreach (var (wave, itemId) in GameConfig.GuaranteedRewards)
                {
                    if (wave != run.Wave) continue;
                    var item = ItemCatalog.Get(itemId);
                    if (item == null) continue;
                    yield return ShowMessage($"웨이브 {wave} 클리어 보상!\n{Josa.Eul(item.Name)} 손에 넣었다!");
                    var outcome = item.Apply(new ItemContext { Run = run });
                    if (!string.IsNullOrEmpty(outcome?.Message)) yield return ShowMessage(outcome.Message);
                }

                // 이기거나 잡으면 보상 아이템 하나를 고른다 (도망치면 없음)
                yield return RewardPhase();

                // 초반 휴식: 살아 있는 팰이 최대 HP의 조금을 회복한다 (보스 웨이브는 아래에서 완전 회복)
                float rest = GameConfig.RestHeal(run.Wave);
                if (rest > 0f && !(GameConfig.HealAfterBoss && run.IsBossWave))
                {
                    int healed = 0;
                    foreach (var p in run.Party)
                        if (!p.IsFainted) healed += p.Heal(Mathf.Max(1, Mathf.RoundToInt(p.MaxHP * rest)));
                    if (healed > 0)
                    {
                        playerInfo.Refresh(battle.Player);
                        GameAudio.Se("heal");
                        yield return ShowMessage("잠시 숨을 골랐다!\n팰들의 HP가 조금 회복되었다.");
                    }
                }

                // 보스(10웨이브마다)를 이기면 파티 완전 회복
                if (GameConfig.HealAfterBoss && run.IsBossWave)
                {
                    run.HealAll();
                    GameAudio.Se("heal");
                    playerInfo.Refresh(battle.Player);
                    yield return ShowMessage("보스를 물리쳤다!\n파티의 팰들이 모두 기운을 되찾았다!");
                }
            }

            string areaBefore = run.AreaName;
            int capBefore = run.LevelCap;
            run.NextWave();
            if (run.LevelCap > capBefore)
            {
                GameConfig.ActiveLevelCap = run.LevelCap;
                GameAudio.Se("level_up");
                yield return ShowMessage($"보스를 쓰러뜨렸다!\n팰들의 최대 레벨이 {Josa.Ro("Lv." + run.LevelCap)} 올랐다!");
                if (run.LevelCap >= GameConfig.MaxLevel)
                    yield return ShowMessage("최종 결전이 다가온다!\n이제부터 상대를 쓰러뜨리면 파티 전원이 경험치를 크게 받는다.");
            }
            Achievements.CheckWave(run.Wave);
            AutoSave(true);                            // 보상까지 받은 뒤 자동 저장 (오른쪽 아래 아이콘 3초)
            if (run.AreaName != areaBefore) yield return ChangeArea();
            StartWave(false);
        }

        // ───────── 지역 이동 (포켓로그처럼 검게 페이드 → 배경 교체 → 다시 밝게) ─────────
        ScreenFade screenFade;
        const float AreaFadeOut = 0.6f, AreaFadeIn = 0.6f, AreaTitleHold = 0.9f;

        ScreenFade Fade => screenFade != null ? screenFade : (screenFade = ScreenFade.Create(messageText.font));

        /// <summary>지금 웨이브의 지역 배경을 바로 깐다 (연출 없음). 같은 배경이면 아무것도 안 한다</summary>
        void ApplyAreaScenery(bool force = false)
        {
            if (run == null) return;
            string biome = GameConfig.AreaBiome(run.Wave);
            if (!force && biome == AreaScenery.Current) return;
            AreaScenery.Apply(enemyView.transform.parent, biome);
            PlayerSave.LastBiome = biome;              // 타이틀 화면 배경 = 마지막으로 플레이한 지역
        }

        /// <summary>
        /// 다음 지역으로: 화면이 검게 닫히고, 검은 화면에 지역 이름이 떴다가 사라지는 동안 배경·발판을 바꾸고, 다시 밝아진다.
        /// 내 팰은 그대로 서 있고, 밝아진 뒤 "○○로 이동했다!"를 보여 준다.
        /// </summary>
        IEnumerator ChangeArea()
        {
            HidePanels();
            GameAudio.StopBgm(AreaFadeOut);                 // 검게 닫히는 동안 음악도 잦아들고, 다음 웨이브에 새 지역 음악
            yield return Fade.FadeTo(1f, AreaFadeOut);
            ApplyAreaScenery();
            UpdateHud();
            int index = GameConfig.AreaIndex(run.Wave) + 1;
            yield return Fade.ShowTitle(run.AreaName, $"지역 {index} / {GameConfig.AreaNames.Length}", AreaTitleHold);
            yield return Fade.FadeTo(0f, AreaFadeIn);
            yield return ShowMessage($"{Josa.Ro(run.AreaName)} 이동했다!");
        }

#if UNITY_EDITOR
        /// <summary>테스트 메뉴(24)용: 다음 지역의 첫 웨이브로 건너뛰며 지역 이동 연출을 보여 준다</summary>
        public void DebugNextArea()
        {
            if (run == null || mode == Mode.Busy) { Debug.LogWarning("[PalRogue] 명령을 고르는 중일 때 다시 눌러 주세요."); return; }
            if (GameConfig.AreaIndex(run.Wave) >= GameConfig.AreaNames.Length - 1) { Debug.Log("[PalRogue] 마지막 지역이라 더 갈 곳이 없습니다."); return; }
            StartCoroutine(DebugNextAreaRoutine());
        }

        /// <summary>
        /// 테스트 메뉴(61)용: 마지막 웨이브(제로버스)로 건너뛴다. 제로버스는 마지막 실드까지 깎인 채로 나와서
        /// 아무 기술이나 한 번 쓰면 바로 2페이즈 연출을 볼 수 있다.
        /// </summary>
        public void DebugAstralymPhase()
        {
            if (run == null || mode == Mode.Busy) { Debug.LogWarning("[PalRogue] 명령을 고르는 중일 때 다시 눌러 주세요."); return; }
            StartCoroutine(DebugAstralymRoutine());
        }

        /// <summary>에디터 메뉴 61이 켜 두면, 게임 씬이 시작될 때 바로 제로버스 테스트 판을 연다</summary>
        public const string AstralymTestKey = "PalRogue.AstralymTest";

        /// <summary>
        /// 메뉴 61(F6)로 플레이를 시작했을 때: 전설 팰 6마리(최대 레벨)로 바로 마지막 웨이브 제로버스전.
        /// 제로버스는 마지막 실드까지 깎인 채라 아무 기술이나 한 번 쓰면 2페이즈 연출이 나온다. 저장·기록은 하지 않는다.
        /// </summary>
        bool TryStartAstralymTest()
        {
            if (!UnityEditor.SessionState.GetBool(AstralymTestKey, false)) return false;
            UnityEditor.SessionState.EraseBool(AstralymTestKey);
            var pool = WildPool().Where(d => d != null && d.name != GameConfig.FinalBossAsset).ToList();
            if (pool.Count == 0) return false;
            var picks = pool.Where(GameConfig.IsLegendary).Take(6).ToList();
            picks.AddRange(pool.Where(d => !picks.Contains(d))
                .OrderByDescending(d => d.baseHP + d.baseAttack + d.baseDefense + d.baseSpeed).Take(6 - picks.Count));

            GameSession.Loaded = null;
            GameSession.NewParty = null;
            GameSession.Slot = -1;
            fromSession = false;
            debugRun = true;
            runNumber = Mathf.Max(2, PlayerSave.RunsStarted);
            playSeconds = 0;
            mode = Mode.Busy;
            run = new Run(picks.Select(d => new Pal(d, GameConfig.MaxLevel)).ToList(), startingSpheres, new System.Random());
            if (partyScreen != null) partyScreen.OpenSummary = OpenSummary;
            spheres = run.Spheres;
            while (run.Wave < GameConfig.FinalWave) run.NextWave();
            GameConfig.ActiveLevelCap = run.LevelCap;
            ApplyAreaScenery(true);
            AstralymPhase.DebugNearShift = true;
            Debug.Log($"[PalRogue] 제로버스 2페이즈 테스트: {string.Join(", ", picks.Select(p => p.palName))} Lv.{GameConfig.MaxLevel}. 아무 기술이나 한 번 쓰면 2페이즈! (저장·기록 안 함)");
            StartWave(false);
            return true;
        }

        IEnumerator DebugAstralymRoutine()
        {
            mode = Mode.Busy;
            debugRun = true;
            GameSession.Slot = -1;                     // 테스트용 점프: 저장 슬롯을 건드리지 않는다 (지거나 이겨도 슬롯이 지워지거나 덮이지 않게)
            Debug.Log("[PalRogue] 제로버스 테스트: 이번 플레이는 저장하지 않습니다.");
            HidePanels();
            enemyInfo.gameObject.SetActive(false);
            enemyView.Hide();
            string before = run.AreaName;
            while (run.Wave < GameConfig.FinalWave) run.NextWave();
            if (run.AreaName != before) yield return ChangeArea();
            AstralymPhase.DebugNearShift = true;
            StartWave(false);
        }

        IEnumerator DebugNextAreaRoutine()
        {
            mode = Mode.Busy;
            HidePanels();
            enemyInfo.gameObject.SetActive(false);
            enemyView.Hide();
            int next = GameConfig.AreaIndex(run.Wave) + 1;
            string before = run.AreaName;
            while (GameConfig.AreaIndex(run.Wave) < next) run.NextWave();    // 다음 지역의 첫 웨이브 (21, 41, …)
            if (run.AreaName != before) yield return ChangeArea();
            StartWave(false);
        }
#endif

        // ───────── 웨이브 보상 (포켓로그와 같은 방식) ─────────
        RewardScreen rewardScreen;
        bool itemUsed;                                // UseItem 결과: 실제로 썼는지 (취소하면 false)
        const float RewardErrorSeconds = 1.1f;
        static readonly string[] YesNo = { "예", "아니오" };

        /// <summary>
        /// 보상 화면: 보상 중 하나를 고르면 끝. 그 전에 돈으로 상점 아이템을 사거나, 돈을 내고 보상을 다시 뽑을 수 있다.
        /// Esc를 누르면 보상 없이 넘어갈지 묻는다.
        /// </summary>
        IEnumerator RewardPhase()
        {
            if (rewardScreen == null)
            {
                var parent = areaText != null ? areaText.transform.parent : messageText.canvas.rootCanvas.transform;
                rewardScreen = RewardScreen.Create(parent, messageText.font, commandCursor.sprite, messageText);
            }

            int rerolls = 0;
            var rewards = ItemCatalog.RollRewards(run, ItemCatalog.RewardCount(run), run.Rng);
            float normalWidth = WidenMessage();
            SetInfoBoxFaded(true);                            // 내 팰 정보창도 뒤로 어둡게
            int lastPick = 0;
            var focus = RewardScreen.Pick.None;               // 지닌 도구 관리·파티 확인에서 돌아오면 그 버튼에 커서
            bool reveal = true;                               // 처음 열 때와 갱신했을 때는 스피어가 떨어지는 연출

            while (true)
            {
                var shop = ItemCatalog.ShopItems(run.Wave);
                int rerollCost = ItemCatalog.RerollCost(run.Wave, rerolls);
                rewardScreen.Open(rewards, shop, rerollCost, run.Money, reveal);
                BringHudToFront();
                if (reveal) yield return rewardScreen.PlayReveal();
                reveal = false;
                yield return rewardScreen.Choose(lastPick, focus);
                focus = RewardScreen.Pick.None;
                var kind = rewardScreen.ResultKind;
                int index = rewardScreen.ResultIndex;

                if (kind == RewardScreen.Pick.Reroll)
                {
                    if (run.Money < rerollCost) { yield return RewardError("돈이 부족하다!"); continue; }
                    run.Money -= rerollCost;
                    GameAudio.Se("buy");
                    rerolls++;
                    UpdateHud();
                    rewards = ItemCatalog.RollRewards(run, ItemCatalog.RewardCount(run), run.Rng);
                    reveal = true;
                    lastPick = 0;
                    continue;
                }
                if (kind == RewardScreen.Pick.Party)
                {
                    if (partyScreen == null) continue;
                    rewardScreen.Hide();
                    yield return partyScreen.Choose(run.Party, true, "파티의 상태를 확인하세요.", null, "닫는다");
                    focus = RewardScreen.Pick.Party;
                    continue;
                }
                if (kind == RewardScreen.Pick.HeldItems)
                {
                    // 지닌 도구 관리는 웨이브를 클리어한 뒤 이 보상 화면에서만 할 수 있다
                    if (partyScreen == null) continue;
                    rewardScreen.Hide();
                    yield return ManageHeldItems();
                    focus = RewardScreen.Pick.HeldItems;
                    continue;
                }
                if (kind == RewardScreen.Pick.Skip)
                {
                    rewardScreen.Hide();
                    yield return ShowMessage("보상 아이템을 고르지 않고 넘어갈까요?");
                    if (choiceMenu == null) break;
                    choiceMenu.transform.SetAsLastSibling();
                    yield return choiceMenu.Choose(YesNo, 1);
                    if (choiceMenu.Result == 0) break;
                    continue;
                }
                if (kind == RewardScreen.Pick.Shop)
                {
                    var (item, price) = shop[index];
                    if (run.Money < price) { yield return RewardError("돈이 부족하다!"); continue; }
                    yield return UseItem(item);
                    if (itemUsed)
                    {
                        run.Money -= price;
                        GameAudio.Se("buy");
                        UpdateHud();
                    }
                    continue;                                       // 사도 보상 화면은 계속된다
                }

                // 보상 아이템: 쓰면 보상 끝, 대상 고르기를 취소하면 다시 고른다
                lastPick = index;
                yield return UseItem(rewards[index]);
                if (itemUsed) break;
            }

            rewardScreen.Close();
            SetInfoBoxFaded(false);
            RestoreMessage(normalWidth);
            playerInfo.Refresh(battle.Player);
        }

        /// <summary>보상 화면 동안 내 팰 정보창을 흐리게 (정보창은 다른 UI보다 위에 그려져서 어두운 막에 안 덮인다)</summary>
        void SetInfoBoxFaded(bool faded)
        {
            var group = playerInfo.GetComponent<CanvasGroup>();
            if (group == null) group = playerInfo.gameObject.AddComponent<CanvasGroup>();
            group.alpha = faded ? 0.12f : 1f;
        }

        IEnumerator RewardError(string text)
        {
            GameAudio.Se("error");
            rewardScreen.SetMessage(text);
            yield return new WaitForSecondsRealtime(RewardErrorSeconds);
        }

        /// <summary>보상 화면 위에 웨이브·돈 글자가 보이게</summary>
        void BringHudToFront()
        {
            rewardScreen.MirrorHud(areaText, moneyText);   // 보상 화면 안에 같은 글자를 복사해서 보여준다
        }

        /// <summary>아이템 하나를 쓴다. 대상(팰·기술)이 필요하면 고르게 하고, 취소하면 itemUsed = false</summary>
        IEnumerator UseItem(ItemDef item)
        {
            itemUsed = false;
            var ctx = new ItemContext { Run = run };

            if (item.Target == ItemTarget.None)
            {
                string why = item.CannotUseNow(run);
                if (why != null) { yield return RewardError(why); yield break; }
            }
            else
            {
                if (partyScreen == null) { yield return RewardError("파티 화면이 아직 없어요!"); yield break; }
                rewardScreen.Hide();
                string prompt = item.IsHeld ? $"{Josa.Eul(item.Name)} 누구에게 지니게 할까?" : $"{Josa.Eul(item.Name)} 누구에게 사용할까?";
                while (true)
                {
                    yield return partyScreen.Choose(run.Party, true, prompt, i => item.CannotUse(run.Party[i]),
                                                    item.IsHeld ? "지니게 한다" : "사용한다");
                    if (partyScreen.Result < 0) yield break;                       // 취소 → 보상 화면으로
                    ctx.Target = run.Party[partyScreen.Result];

                    if (item.Target != ItemTarget.PalSkill || choiceMenu == null) break;

                    // PP 회복: 기술 고르기
                    var skills = ctx.Target.Skills;
                    messageText.text = $"{Josa.Eul(item.Name)} 어느 기술에 사용할까?";
                    messageText.maxVisibleCharacters = 9999;
                    var labels = skills.Select(s => $"{s.Data.skillName}  {s.PP}/{s.MaxPP}").Concat(new[] { "그만둔다" }).ToArray();
                    choiceMenu.transform.SetAsLastSibling();
                    yield return choiceMenu.Choose(labels, 0, PpMenuWidth);
                    int pick = choiceMenu.Result;
                    if (pick < 0 || pick >= skills.Count) continue;                 // 그만둔다 → 다시 팰 고르기
                    string skillError = item.CannotUseSkill(skills[pick]);
                    if (skillError != null) { yield return ShowMessage(skillError); continue; }
                    ctx.SkillIndex = pick;
                    break;
                }
            }

            rewardScreen.Hide();
            var outcome = item.Apply(ctx);
            itemUsed = true;
            if (outcome?.TeachSkill != null)
            {
                // 기술머신: 배우기를 그만두면 아이템을 쓰지 않은 것으로 하고 보상 화면으로 돌아간다
                yield return LearnSkill(ctx.Target, outcome.TeachSkill);
                itemUsed = lastSkillLearned;
                yield break;
            }
            UpdateHud();
            playerInfo.Refresh(battle.Player);
            if (outcome != null && !string.IsNullOrEmpty(outcome.Message)) yield return ShowMessage(outcome.Message);
            if (outcome?.LevelUps != null && outcome.LevelUps.Count > 0)            // 여러 레벨이 올라도 한 번에
                yield return PlayLevelUps(ctx.Target, outcome.LevelUps[0].level - 1, outcome.LevelUps[outcome.LevelUps.Count - 1].level,
                                          outcome.LevelUps.Where(u => u.newSkills != null).SelectMany(u => u.newSkills).ToList());
        }

        const float PpMenuWidth = 140f;

        IEnumerator GameOver()
        {
            GameAudio.StopBgm(1.2f);
            yield return ShowMessage($"웨이브 {run.Wave}에서 모험이 끝났다...");
            yield return EndRun();
        }

        RunResultScreen resultScreen;

#if UNITY_EDITOR
        /// <summary>(테스트) 아이템 줄 확인용: 파티 전체 아이템·몇 웨이브짜리·지닌 아이템을 나와 상대에게 잔뜩 준다 (메뉴 PalRogue/57)</summary>
        public void DebugFillItems(int heldPerPal)
        {
            if (run == null || battle == null) return;
            var rng = new System.Random();
            foreach (var id in new[] { "exp_charm", "exp_charm", "exp_share", "amulet_coin", "candy_jar", "berry_pouch", "x_attack", "dire_hit" })
                ItemCatalog.Get(id)?.Apply(new ItemContext { Run = run });
            var held = ItemCatalog.All.Where(d => d.IsHeld && d.Id != "mini_black_hole").ToList();
            foreach (var pal in run.Party.Concat(new[] { battle.Enemy }))
                for (int i = 0; i < heldPerPal; i++)
                    held[rng.Next(held.Count)].Apply(new ItemContext { Run = run, Target = pal });
            Debug.Log($"[아이템 줄] 파티 전체 아이템 {run.RunItems.Count}종, 팰마다 지닌 아이템 {heldPerPal}개씩 (상대 포함)");
        }

        /// <summary>(테스트) 결과 화면만 미리 본다 — 기록·저장은 건드리지 않는다 (메뉴 PalRogue/56)</summary>
        public void DebugShowResult()
        {
            if (run == null || mode != Mode.Command) { Debug.LogWarning("[결과 화면] 명령을 고르는 화면에서 실행해 주세요."); return; }
            StartCoroutine(DebugResult());
        }

        IEnumerator DebugResult()
        {
            mode = Mode.Busy;
            HidePanels();
            if (resultScreen == null) resultScreen = RunResultScreen.Create(messageText.font);
            yield return resultScreen.Show(run, false, playSeconds, false, "Enter 또는 클릭: 닫기 (미리보기)");
            ShowCommand();
        }
#endif

        IEnumerator EndRun()
        {
            if (GameSession.Slot >= 0) SaveSlots.Delete(GameSession.Slot);   // 끝난 모험은 슬롯에서 지운다 (포켓로그와 같음)

            // 플레이 이력에 남기고 결과 화면
            bool cleared = !run.IsWiped && run.Wave >= GameConfig.FinalWave;
            var record = new PlayerSave.RunRecord
            {
                runNumber = runNumber, wave = Mathf.Min(run.Wave, GameConfig.FinalWave), cleared = cleared,
                caught = run.CaughtCount, bosses = run.BossesDefeated, endedTicks = System.DateTime.Now.Ticks,
                playSeconds = playSeconds, area = run.AreaName,
            };
            foreach (var p in run.Party) record.party.Add(new PlayerSave.RecordPal { pal = p.Data.name, level = p.Level, fainted = p.IsFainted });
            bool best = !debugRun && PlayerSave.AddHistory(record);          // 테스트 판은 플레이 이력에 남기지 않는다
            bool toTitle = fromSession && GameSession.CanLoad(GameSession.TitleScene);
            HidePanels();
            if (resultScreen == null) resultScreen = RunResultScreen.Create(messageText.font);
            yield return resultScreen.Show(run, cleared, playSeconds, best, toTitle ? "Enter 또는 클릭: 타이틀로" : "Enter 또는 클릭: 새 모험");

            if (toTitle)
            {
                mode = Mode.Busy;
                GameAudio.StopBgm(0.4f);
                GameSession.Go(GameSession.TitleScene, messageText.font);
                yield break;
            }
            yield return ShowMessage("화면을 클릭하면 새 모험을 시작해요.");
            mode = Mode.Ended;
            restartButton.gameObject.SetActive(true);
            Select(restartButton);
        }

        void UpdateHud()
        {
            if (run == null) return;
            if (areaText != null) areaText.text = voidPhase ? GameConfig.FinalPhase2AreaText : $"{run.AreaName} - {run.Wave}";
            if (moneyText != null) moneyText.text = $"{run.Money:N0} G";
            Achievements.CheckMoney(run.Money);
        }

        // ───────── 도망간다 ─────────
        void OnRun()
        {
            if (mode != Mode.Command) return;
            if (run.IsBossWave) { StartCoroutine(Unavailable("보스에게서는 도망칠 수 없다!")); return; }
            StartCoroutine(Flee());
        }

        IEnumerator Flee()
        {
            mode = Mode.Busy;
            HidePanels();
            battle.Detach();
            GameAudio.Se("flee");
            yield return ShowMessage("무사히 도망쳤다!");
            yield return FinishWave(true);            // 도망쳐도 다음 웨이브로 넘어간다 (경험치·돈은 없음)
        }

        // ───────── 야생 팰 목록 ─────────
        List<PalData> WildPool()
        {
            var pool = wildPals.Where(p => p != null).ToList();
            if (pool.Count == 0 && enemyPal != null) pool.Add(enemyPal);
            return pool;
        }

#if UNITY_EDITOR
        /// <summary>프로젝트의 모든 PalData 에셋으로 야생 팰 목록을 채운다 (에디터 전용)</summary>
        [ContextMenu("야생 팰 목록 다시 채우기")]
        void FillWildPals()
        {
            wildPals = UnityEditor.AssetDatabase.FindAssets("t:PalData")
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<PalData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null)
                .OrderBy(p => p.name)
                .ToList();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        /// <summary>게임에 나오는 모든 기술 (야생 팰·내 파티 팰의 학습 목록에서 모은다). 기술머신 목록에 쓴다</summary>
        IEnumerable<SkillData> KnownSkills()
        {
            var pals = WildPool().Concat(playerParty.Where(m => m != null && m.pal != null).Select(m => m.pal));
            if (playerPal != null) pals = pals.Append(playerPal);
            return pals.Where(p => p.learnset != null)
                       .SelectMany(p => p.learnset).Where(e => e.skill != null).Select(e => e.skill)
                       .Distinct();
        }

        static TMP_Text FindLabel(string objectName) =>
            FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == objectName);

        List<string> pendingSynergyMessages;

        IEnumerator Intro(bool firstWave)
        {
            mode = Mode.Busy;
            HidePanels();
            messageText.text = "";
            yield return EnemyEntrance(firstWave);
            if (resumeNote != null) { var note = resumeNote; resumeNote = null; yield return ShowMessage(note); }
            yield return PlayEvents();
            if (pendingSynergyMessages != null)
                foreach (var msg in pendingSynergyMessages) yield return ShowMessage(msg);
            pendingSynergyMessages = null;
            yield return AskSwitchAtStart();
            ShowCommand();
        }

        /// <summary>
        /// 배틀을 시작할 때마다 (싸울 수 있는 팰이 2마리 이상이면) "팰을 교체하시겠습니까?"를 묻는다.
        /// 예 → 파티 화면에서 고른 팰로 턴을 쓰지 않고 바꾼다 (그만두면 그대로 시작). 아니오 → 바로 시작.
        /// </summary>
        IEnumerator AskSwitchAtStart()
        {
            if (choiceMenu == null || partyScreen == null || battle == null) yield break;
            if (battle.PlayerParty.Count(p => !p.IsFainted) < 2) yield break;
            yield return ShowMessage("팰을 교체하시겠습니까?");
            yield return choiceMenu.Choose(YesNo, 0);
            if (choiceMenu.Result != 0) yield break;
            yield return partyScreen.Choose(battle.PlayerParty, true, SelectPrompt, SwitchError);
            if (partyScreen.Result < 0) yield break;
            if (battle.SwitchBeforeBattle(partyScreen.Result)) yield return PlayEvents();
        }

        ItemBar itemBar;

        // ───────── 게임 중 메뉴 (Esc) ─────────
        GameMenu gameMenu;
        float menuClosedAt;

        IEnumerator OpenGameMenu()
        {
            mode = Mode.Busy;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);   // 전투 메뉴가 키를 받지 않게
            bool canTitle = GameSession.CanLoad(GameSession.TitleScene);
            if (gameMenu == null) gameMenu = GameMenu.Create(messageText.font, canTitle);
            yield return gameMenu.Open();
            menuClosedAt = Time.unscaledTime;
            switch (gameMenu.Result)
            {
                case GameMenu.Choice.Title:
                    GameAudio.StopBgm(0.4f);
                    GameSession.Go(GameSession.TitleScene, messageText.font);
                    yield break;
                case GameMenu.Choice.Quit:
                    GameAudio.StopBgm(0.3f);
                    yield return new WaitForSecondsRealtime(0.3f);
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    yield break;
                default:
                    ShowCommand();
                    break;
            }
        }

        // ───────── 커맨드 ─────────
        float commandShownAt;

        void ShowCommand()
        {
            mode = Mode.Command;
            commandShownAt = Time.unscaledTime;     // 기술 창에서 Esc로 돌아온 그 순간에 메뉴가 열리지 않게
            skillPanel.SetActive(false);
            skillInfoPanel.SetActive(false);
            commandBox.SetActive(true);

            messageText.text = $"{Josa.Eun(PalName(battle.Player))}\n무엇을 할까?";
            messageText.maxVisibleCharacters = 9999;
            // 포켓로그처럼 마지막으로 고른 명령(싸운다/스피어/팰/도망)에 커서를 둔다
            var start = lastCommand != null && lastCommand.gameObject.activeInHierarchy && lastCommand.interactable ? lastCommand : fightButton;
            Select(start);
        }

        // 마지막으로 고른 명령 / 팰마다 마지막으로 고른 기술 (다음에 메뉴를 열면 그 자리에 커서)
        Button lastCommand;
        readonly Dictionary<Pal, int> lastSkillIndex = new Dictionary<Pal, int>();

        // ───────── 파티 / 교체 ─────────
        List<Pal> BuildPlayerParty()
        {
            var list = playerParty.Where(m => m != null && m.pal != null).Take(6)
                                  .Select(m => new Pal(m.pal, m.level)).ToList();
            if (list.Count == 0) list.Add(new Pal(playerPal, playerLevel));
            return list;
        }

        // ───────── 팰 스피어 ─────────
        int[] spheres = new int[PalSpheres.Count];   // 지금 가진 스피어 개수 (런이 시작될 때 run.Spheres로 바뀐다)

        // ───────── 레벨업: 새 기술 배우기 / 강화 효과 고르기 ─────────
        readonly System.Random perkRng = new System.Random();
        const float WidePromptWidth = 300f;          // 질문이 길 때 메시지 칸을 넓게 쓴다
        const float SkillMenuWidth = 120f;

        IEnumerator PlayLevelUp(Pal pal, int level, List<SkillData> newSkills) => PlayLevelUps(pal, level - 1, level, newSkills);

        /// <summary>
        /// 레벨업 한 번에 처리: 여러 레벨이 한꺼번에 올라도 "Lv.5 → Lv.8로 올랐다!" 한 줄.
        /// 그 사이에 배울 기술·강화 레벨(10단위)·진각성은 순서대로 한 번씩만 묻는다.
        /// </summary>
        IEnumerator PlayLevelUps(Pal pal, int fromLevel, int toLevel, List<SkillData> newSkills)
        {
            if (toLevel <= fromLevel) yield break;
            bool active = pal == battle.Player;
            if (active)
            {
                playerInfo.SetLevel(toLevel);
                playerInfo.RefreshHp(pal);
                playerInfo.SetExp(0f);
            }
            GameAudio.Se("level_up");
            yield return ShowMessage(toLevel - fromLevel > 1
                ? $"{Josa.Eun(PalName(pal))} Lv.{fromLevel} → {Josa.Ro("Lv." + toLevel)} 올랐다!"
                : $"{Josa.Eun(PalName(pal))} {Josa.Ro("Lv." + toLevel)} 올랐다!");

            if (newSkills != null)
                foreach (var skill in newSkills.Distinct())
                    yield return LearnSkill(pal, skill);

            for (int lv = fromLevel + 1; lv <= toLevel; lv++)
            {
                if (!GameConfig.IsPerkLevel(lv)) continue;
                // Lv.50부터는 아직 각성이 없으면 강화 효과 대신 각성을 고른다
                if (lv >= GameConfig.AwakenLevel && AwakeningCatalog.Of(pal) == null) yield return ChooseAwakening(pal);
                else yield return ChoosePerk(pal, $"{Josa.I(PalName(pal))} Lv.{lv}에 도달했다!");
            }

            // Lv.100: 각성이 진각성이 된다
            var awakening = AwakeningCatalog.Of(pal);
            if (toLevel >= GameConfig.TrueAwakenLevel && awakening != null && !awakening.IsTrue)
            {
                awakening.SetTrue(true);
                yield return ShowMessage($"{PalName(pal)}의 {Josa.I(awakening.Def.Name)} 진각성으로 거듭났다!\n" +
                                         $"각성하면 모든 능력치 ×{GameConfig.TrueAwakenStatMultiplier:0.##}!");
            }
        }

        bool lastSkillLearned;                       // LearnSkill 결과: 실제로 배웠는지

        IEnumerator LearnSkill(Pal pal, SkillData skill)
        {
            lastSkillLearned = false;
            string name = PalName(pal);
            if (pal.Skills.Any(s => s.Data == skill)) yield break;               // 이미 아는 기술
            if (pal.Skills.Count < Pal.MaxSkills)
            {
                pal.TryLearnSkill(skill);
                lastSkillLearned = true;
                yield return ShowMessage($"{Josa.Eun(name)} {Josa.Eul(skill.skillName)} 배웠다!");
                yield break;
            }
            if (choiceMenu == null) yield break;

            float normal = WidenMessage();
            bool first = true;
            while (true)
            {
                if (first)
                    yield return ShowMessage($"{Josa.Eun(name)} {Josa.Eul(skill.skillName)} 배우고 싶다...\n하지만 기술은 {Pal.MaxSkills}개까지만 쓸 수 있다!");
                first = false;

                // ① 다른 기술을 잊게 할지 묻는다
                yield return ShowMessage($"다른 기술을 잊게 하고\n{Josa.Eul(skill.skillName)} 배우게 할까?");
                yield return choiceMenu.Choose(YesNo, 0);
                if (choiceMenu.Result != 0)
                {
                    // 아니오 → 정말 포기할지 한 번 더
                    yield return ShowMessage($"{Josa.Eul(skill.skillName)} 배우는 것을 포기할까?");
                    yield return choiceMenu.Choose(YesNo, 1);
                    if (choiceMenu.Result == 0)
                    {
                        yield return ShowMessage($"{Josa.Eun(name)} {Josa.Eul(skill.skillName)} 배우지 않았다!");
                        break;
                    }
                    continue;
                }

                // ② 잊을 기술 고르기
                yield return ShowMessage("어느 기술을 잊게 할까?");
                var labels = pal.Skills.Select(s => s.Data.skillName).Concat(new[] { "그만둔다" }).ToArray();
                yield return choiceMenu.Choose(labels, 0, SkillMenuWidth);
                int pick = choiceMenu.Result;
                if (pick < 0 || pick >= pal.Skills.Count) continue;          // 그만둔다 → 처음 질문으로

                // ③ 정말 잊을지 확인
                string forgotten = pal.Skills[pick].Data.skillName;
                yield return ShowMessage($"{Josa.Eul(forgotten)} 잊고\n{Josa.Eul(skill.skillName)} 배우게 할까?");
                yield return choiceMenu.Choose(YesNo, 0);
                if (choiceMenu.Result != 0) continue;

                pal.TryLearnSkill(skill, pick);
                lastSkillLearned = true;
                yield return ShowMessage($"1, 2, … 짠!\n{Josa.Eun(name)} {Josa.Eul(forgotten)} 깨끗이 잊었다!");
                yield return ShowMessage($"그리고… {Josa.Eun(name)} {Josa.Eul(skill.skillName)} 배웠다!");
                break;
            }
            RestoreMessage(normal);
        }

        const float PerkMenuWidth = 120f;

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        /// <summary>
        /// 강화 효과 카드 고르기: 카드 3장(등급 색으로 표시) 중 1장. 커서를 옮기면 설명이 나온다.
        /// "다시 뽑기"로 공짜로 GameConfig.PerkFreeRerolls번 새로 뽑을 수 있다. minRarity 이상만 나온다 (★4 보너스는 영웅 이상)
        /// </summary>
        IEnumerator ChoosePerk(Pal pal, string reason, PerkRarity minRarity = PerkRarity.Common)
        {
            if (choiceMenu == null) yield break;
            var offers = PerkCatalog.RollChoices(pal, GameConfig.PerkChoices, perkRng, minRarity);
            if (offers.Count == 0) yield break;

            string name = PalName(pal);
            string prompt = $"{reason}\n강화 효과를 하나 골라 주세요.";
            int rerolls = GameConfig.PerkFreeRerolls;
            float normal = WidenMessage();
            yield return ShowMessage(prompt);

            string Title(PerkOffer o) => o.Perk.MaxRank > 1 ? $"{o.Perk.Name} Lv.{o.Rank}" : o.Perk.Name;
            void Describe(int i)
            {
                messageText.maxVisibleCharacters = 9999;
                if (i >= 0 && i < offers.Count)
                {
                    var o = offers[i];
                    string rarity = $"<color=#{Hex(PerkRarities.Color(o.Perk.Rarity))}>[{PerkRarities.Name(o.Perk.Rarity)}]</color>";
                    string upgrade = o.Rank > 1 ? " (강화)" : "";
                    messageText.text = $"{rarity} {Title(o)}{upgrade}\n{o.Perk.Describe(o.Rank)}";
                }
                else if (i == offers.Count) messageText.text = $"카드를 새로 뽑는다. (남은 횟수 {rerolls}번)";
                else messageText.text = prompt;
            }
            choiceMenu.Highlighted += Describe;
            PerkOffer chosen = null;
            while (chosen == null)                                                 // 반드시 하나 고른다 (Esc 무시)
            {
                var labels = offers.Select(o => $"<color=#{Hex(PerkRarities.Color(o.Perk.Rarity))}>{Title(o)}</color>").ToList();
                if (rerolls > 0) labels.Add("다시 뽑기");
                yield return choiceMenu.Choose(labels.ToArray(), 0, PerkMenuWidth);
                int r = choiceMenu.Result;
                if (r >= 0 && r < offers.Count) chosen = offers[r];
                else if (r == offers.Count && rerolls > 0)
                {
                    var again = PerkCatalog.RollChoices(pal, GameConfig.PerkChoices, perkRng, minRarity);
                    if (again.Count > 0) offers = again;
                    rerolls--;
                }
            }
            choiceMenu.Highlighted -= Describe;

            PerkCatalog.Apply(pal, chosen);
            if (pal == battle.Player) playerInfo.RefreshHp(pal);                   // 최대 HP 강화 반영
            yield return ShowMessage(chosen.Rank > 1 ? $"{PalName(pal)}의 [{chosen.Perk.Name}] 강화가 Lv.{chosen.Rank}로 올랐다!"
                                                     : $"{Josa.Eun(name)} [{chosen.Perk.Name}] 강화를 얻었다!");
            RestoreMessage(normal);
        }

        /// <summary>각성 고르기 (Lv.50 이후 첫 강화 레벨): 내 속성 각성 + 공용 각성 중 1개</summary>
        IEnumerator ChooseAwakening(Pal pal)
        {
            if (choiceMenu == null) yield break;
            var choices = AwakeningCatalog.RollChoices(pal, GameConfig.PerkChoices, perkRng);
            if (choices.Count == 0) yield break;

            string name = PalName(pal);
            float normal = WidenMessage();
            yield return ShowMessage($"{name}의 몸 깊은 곳에서\n숨겨진 힘이 느껴진다…!");
            yield return ShowMessage($"각성하면 전투 중 HP가 절반 이하일 때 깨어나\n그 전투 동안 모든 능력치 ×{GameConfig.AwakenStatMultiplier:0.##} + 고유 효과!");
            string prompt = "어떤 힘을 깨우칠까?";
            yield return ShowMessage(prompt);

            void Describe(int i)
            {
                messageText.maxVisibleCharacters = 9999;
                if (i < 0 || i >= choices.Count) { messageText.text = prompt; return; }
                var d = choices[i];
                string type = d.Type.HasValue ? $"<color=#{Hex(PerkRarities.Color(PerkRarity.Epic))}>[{PalTypeNames.Korean(d.Type.Value)}]</color> " : "[공용] ";
                messageText.text = $"{type}{d.Name}\n{d.Description}";
            }
            choiceMenu.Highlighted += Describe;
            AwakeningDef chosen = null;
            while (chosen == null)
            {
                yield return choiceMenu.Choose(choices.Select(c => c.Name).ToArray(), 0, PerkMenuWidth);
                if (choiceMenu.Result >= 0 && choiceMenu.Result < choices.Count) chosen = choices[choiceMenu.Result];
            }
            choiceMenu.Highlighted -= Describe;

            pal.AddEffect(new Awakening(chosen, pal.Level >= GameConfig.TrueAwakenLevel));
            yield return ShowMessage($"{Josa.Eun(name)} [{chosen.Name}]의 힘을 깨우쳤다!");
            RestoreMessage(normal);
        }

        float WidenMessage()
        {
            var rt = messageText.rectTransform;
            float normal = rt.sizeDelta.x;
            rt.sizeDelta = new Vector2(Mathf.Max(normal, WidePromptWidth), rt.sizeDelta.y);
            return normal;
        }

        void RestoreMessage(float normalWidth)
        {
            var rt = messageText.rectTransform;
            rt.sizeDelta = new Vector2(normalWidth, rt.sizeDelta.y);
        }
        SphereThrowView sphereView;

        void OnSphere()
        {
            if (mode != Mode.Command) return;
            if (sphereMenu == null) { StartCoroutine(Unavailable("스피어 메뉴가 아직 없어요!")); return; }

            string blocked = battle.CatchBlockReason();
            if (blocked != null) { StartCoroutine(Unavailable(blocked)); return; }
            if (spheres.All(n => n <= 0)) { StartCoroutine(Unavailable("가진 팰 스피어가 없다!")); return; }

            StartCoroutine(SphereFromMenu());
        }

        IEnumerator SphereFromMenu()
        {
            mode = Mode.Busy;
            // 포켓로그처럼 명령 창은 그대로 두고 그 바로 위에 스피어 창을 띄운다
            skillPanel.SetActive(false);
            skillInfoPanel.SetActive(false);
            sphereMenu.placeAbove = commandBox.transform as RectTransform;
            var commands = new[] { fightButton, sphereButton, palsButton, runButton };
            foreach (var c in commands) if (c != null) c.interactable = false;   // 스피어를 고르는 동안 명령 칸이 마우스로 선택되지 않게
            yield return sphereMenu.Choose(spheres);
            foreach (var c in commands) if (c != null) c.interactable = true;
            commandBox.SetActive(false);
            if (sphereMenu.Result < 0) { ShowCommand(); yield break; }        // 취소

            var type = (SphereType)sphereMenu.Result;
            spheres[(int)type]--;                                                // 던지면 하나 줄어든다
            yield return RunTurn(BattleAction.Throw(type));                      // 던지기는 한 턴을 쓴다
        }

        /// <summary>스피어가 상대에게 날아가 흡수하고 땅에 떨어지기까지</summary>
        IEnumerator PlayThrow(SphereType type)
        {
            if (sphereView == null) sphereView = SphereThrowView.Create(enemyView.transform.parent);
            Vector3 hit = enemyView.transform.position;                          // 상대 몸 가운데
            yield return sphereView.Throw(type, hit);
            yield return enemyView.AbsorbInto(hit);
            yield return sphereView.Drop(new Vector3(hit.x, enemyView.feetWorldY + sphereView.Radius, 0f));
        }

        IEnumerator PlayShakes(int count)
        {
            for (int i = 0; i < count; i++) yield return sphereView.Shake();
        }

        IEnumerator PlayBreakFree()
        {
            Vector3 at = sphereView.transform.position;
            sphereView.Hide();
            yield return enemyView.ReleaseFrom(at);
        }

        // ───────── 파티가 가득 찼을 때 잡은 팰 처리 (포켓로그와 같은 흐름) ─────────
        static readonly string[] FullPartyChoices = { "능력치를 본다", "팰 도감", "예", "아니오" };
        const int ChoiceStats = 0, ChoiceDex = 1, ChoiceYes = 2, ChoiceNo = 3;
        const float WideMessageWidth = 300f;     // 질문이 길어서 이때만 메시지 칸을 넓게 쓴다

        /// <summary>
        /// 잡은 팰 처리. 파티에 같은 팰이 있으면 응축(★ +1)할지 먼저 묻는다.
        /// 응축하지 않으면 파티에 넣고, 파티가 가득 찼으면 누구를 놓아줄지 묻는다.
        /// </summary>
        IEnumerator ResolveCatch()
        {
            var caught = battle.PendingCatch;
            string name = PalName(caught);
            var same = battle.CondenseTarget(caught);
            if (same != null && choiceMenu != null)
            {
                HidePanels();
                float normal = WidenMessage();
                string stars = same.Stars > 0 ? $" 별 {same.Stars}개" : "";
                yield return ShowMessage($"파티에 같은 팰이 있다! ({name} Lv.{same.Level}{stars})\n응축해서 별을 올릴까?");
                yield return choiceMenu.Choose(YesNo, 0);
                if (choiceMenu.Result == 0)
                {
                    var target = battle.CondenseCaught(out var moved);
                    yield return ShowMessage($"{Josa.Eul(name)} 응축했다!\n별 {target.Stars}개: 모든 능력치 +{Mathf.RoundToInt(target.Stars * GameConfig.StarStatBonus * 100)}%");
                    if (moved.Count > 0) yield return ShowMessage($"{Josa.Eul(string.Join(", ", moved))} 함께 넘겨받았다!");
                    if (target == battle.Player) playerInfo.Refresh(target);
                    Achievements.CheckStars(target.Stars);
                    RestoreMessage(normal);
                    if (target.Stars >= GameConfig.MaxStars && GameConfig.BonusPerkAtMaxStars)
                        yield return ChoosePerk(target, $"{PalName(target)}의 별이 가득 찼다!", PerkRarity.Epic);
                    yield break;
                }
                RestoreMessage(normal);
            }
            if (battle.AddCaughtToParty())                                      // 응축하지 않음 + 자리 있음
            {
                yield return ShowMessage($"{Josa.I(name)} 파티에 들어왔다!");
                yield break;
            }
            if (battle.PendingCatch != null) yield return ResolveFullParty();
        }

        IEnumerator ResolveFullParty()
        {
            var caught = battle.PendingCatch;
            string name = PalName(caught);
            HidePanels();

            var msgRt = messageText.rectTransform;
            float normalWidth = msgRt.sizeDelta.x;
            msgRt.sizeDelta = new Vector2(WideMessageWidth, msgRt.sizeDelta.y);

            while (battle.PendingCatch != null)
            {
                yield return ShowMessage($"지닌 팰이 가득 찼습니다. {Josa.Eul(name)}\n대신해 다른 팰을 놓아주시겠습니까?");

                int choice = ChoiceNo;
                if (choiceMenu != null)
                {
                    yield return choiceMenu.Choose(FullPartyChoices, ChoiceYes);
                    choice = choiceMenu.Result < 0 ? ChoiceNo : choiceMenu.Result;   // Esc = 아니오
                }
                else Debug.LogWarning("[BattleScreen] 선택 창이 없어서 잡은 팰을 놓아줍니다.");

                if (choice == ChoiceStats)
                {
                    yield return OpenSummary(new List<Pal> { caught }, 0);   // 잡은 팰의 능력치
                    continue;
                }
                if (choice == ChoiceDex)
                {
                    yield return ShowMessage("아직 준비 중인 기능이에요!");
                    continue;
                }

                if (choice == ChoiceNo)
                {
                    battle.ReleaseCaught();
                    yield return ShowMessage($"{Josa.Eul(name)} 놓아주었다!");
                    break;
                }

                // 예: 파티 화면에서 놓아줄 팰 고르기 ("그만둔다"를 누르면 다시 질문으로)
                if (partyScreen == null) { battle.ReleaseCaught(); break; }
                yield return partyScreen.Choose(battle.PlayerParty, true, "놓아줄 팰을 선택하세요.", null, "놓아준다");
                if (partyScreen.Result < 0) continue;

                var released = battle.ReplaceWithCaught(partyScreen.Result);
                yield return ShowMessage($"{Josa.Eul(PalName(released))} 놓아주었다!");
                yield return ShowMessage($"{Josa.I(name)} 파티에 들어왔다!");
            }

            msgRt.sizeDelta = new Vector2(normalWidth, msgRt.sizeDelta.y);
        }

        // ───────── 기술 연출 ─────────
        SkillFx skillFx;
        SkillFx Fx
        {
            get
            {
                if (skillFx == null) skillFx = new GameObject("SkillFx").AddComponent<SkillFx>();
                return skillFx;
            }
        }

        /// <summary>(테스트용, 메뉴 PalRogue/31) 지금 전투 화면에서 기술 연출을 차례로 보여준다. 내 팰 → 상대, 상대 → 내 팰 번갈아</summary>
        public void PreviewSkillFx(IList<SkillData> skills, bool enemyOnly = false, bool record = false)
        {
            StopCoroutine(nameof(PreviewRoutine));
            StartCoroutine(PreviewRoutine(skills, enemyOnly, record));
        }

        IEnumerator PreviewRoutine(IList<SkillData> skills, bool enemyOnly, bool record)
        {
            bool recording = record;
#if UNITY_EDITOR
            if (record) StartCoroutine(ShiftRecorder.Record(() => recording, 0.06f));
#endif
            for (int i = 0; i < skills.Count; i++)
            {
                bool mine = !enemyOnly && i % 2 == 0;
                messageText.maxVisibleCharacters = 9999;
                messageText.text = $"[연출 미리보기 {i + 1}/{skills.Count}]\n{skills[i].skillName} ({skills[i].name})";
                yield return Fx.Play(skills[i], mine ? playerView : enemyView, mine ? enemyView : playerView, false, !enemyOnly && i % 5 == 4, 1f);
                yield return new WaitForSecondsRealtime(enemyOnly ? 0.6f : 0.35f);
            }
            recording = false;
            messageText.text = "연출 미리보기 끝!";
        }

        // ───────── 능력치 화면 ─────────
        SummaryScreen summaryScreen;

        IEnumerator OpenSummary(IReadOnlyList<Pal> party, int index)
        {
            if (summaryScreen == null)
            {
                var parent = areaText != null ? areaText.transform.parent : messageText.canvas.rootCanvas.transform;
                summaryScreen = SummaryScreen.Create(parent, messageText.font, choiceMenu);
            }
            yield return summaryScreen.Show(party, index, run);
        }

        void OnPals()
        {
            if (mode != Mode.Command) return;
            if (partyScreen == null)
            {
                StartCoroutine(Unavailable("파티 화면이 아직 없어요!"));
                return;
            }
            StartCoroutine(SwitchFromMenu());
        }

        IEnumerator SwitchFromMenu()
        {
            mode = Mode.Busy;
            HidePanels();
            yield return partyScreen.Choose(battle.PlayerParty, true, SelectPrompt, SwitchError);
            if (partyScreen.Result < 0) { ShowCommand(); yield break; }       // 취소
            yield return RunTurn(BattleAction.SwitchTo(partyScreen.Result));  // 교체는 한 턴을 쓴다
        }

        /// <summary>그 칸으로 교체할 수 없으면 이유를, 괜찮으면 null</summary>
        string SwitchError(int index)
        {
            var pal = battle.PlayerParty[index];
            if (pal.IsFainted) return $"{Josa.Eun(PalName(pal))} 싸울 수 있는 기력이 없다!";
            if (index == battle.PlayerIndex) return $"{Josa.Eun(PalName(pal))} 이미 싸우고 있다!";
            return null;
        }

        void OnFight()
        {
            if (mode != Mode.Command) return;
            if (!battle.Player.Skills.Any(s => s.HasPP))
            {
                OnSkill(-1);                       // 모든 PP가 0이면 자동으로 발버둥
                return;
            }
            ShowSkills();
        }

        IEnumerator Unavailable(string text)
        {
            if (mode != Mode.Command) yield break;
            mode = Mode.Busy;
            commandBox.SetActive(false);
            yield return ShowMessage(text);
            ShowCommand();
        }

        // ───────── 스킬 선택 ─────────
        void ShowSkills()
        {
            mode = Mode.Skill;
            var pal = battle.Player;
            commandBox.SetActive(false);
            skillPanel.SetActive(true);
            skillInfoPanel.SetActive(true);

            for (int i = 0; i < skillButtons.Length; i++)
            {
                bool has = i < pal.Skills.Count;
                skillButtons[i].interactable = has;
                skillTexts[i].text = has ? pal.Skills[i].Data.skillName : "-";
                skillTexts[i].color = !has || !pal.Skills[i].HasPP ? Dim : EffectivenessColor(pal.Skills[i].Data, battle.Enemy);
            }
            int last = lastSkillIndex.TryGetValue(pal, out var li) && li >= 0 && li < pal.Skills.Count ? li : 0;
            Select(skillButtons[last]);
        }

        // 기술 이름 색: 상대에게 효과가 굉장하면 파랑, 별로(또는 효과 없음)면 주황, 보통이거나 변화 기술이면 흰색
        static readonly Color SuperEffective = new Color32(0x48, 0xa8, 0xf8, 0xff), NotEffective = new Color32(0xf8, 0x98, 0x30, 0xff);

        static Color EffectivenessColor(SkillData data, Pal foe)
        {
            if (data == null || foe == null || data.category == SkillCategory.Status) return Color.white;
            float m = TypeChart.GetMultiplier(data.type, foe.Data.primaryType, foe.Data.SecondaryType);
            return m > 1f ? SuperEffective : m < 1f ? NotEffective : Color.white;
        }

        void ShowSkillInfo(int index)
        {
            var pal = battle.Player;
            if (index < 0 || index >= pal.Skills.Count)
            {
                currentSkill = -1;
                typeBadge.enabled = false;
                typeFallbackText.text = "";
                categoryBadge.gameObject.SetActive(false);
                categoryText.text = "";
                ppValueText.text = powerValueText.text = accuracyValueText.text = "";
                return;
            }

            var inst = pal.Skills[index];
            var data = inst.Data;

            // 속성 뱃지: 이미지가 있으면 그림으로, 없으면(Void 등) 글자로
            var tex = Resources.Load<Texture2D>($"1.팰/2.Elements/Skill_type/{data.type}_sk");
            typeBadge.texture = tex;
            typeBadge.uvRect = BadgeUv;
            typeBadge.enabled = tex != null;
            typeFallbackText.text = tex != null ? "" : PalTypeNames.Korean(data.type);

            // 스킬 종류 뱃지: Resources/Attack Icon, Status Icon 이미지. 못 찾으면 색깔 글자 뱃지
            bool isAttack = data.category != SkillCategory.Status;
            var icon = Resources.Load<Texture2D>(isAttack ? AttackIconPath : StatusIconPath);
            categoryBadge.gameObject.SetActive(true);
            categoryBadge.texture = icon;
            categoryBadge.uvRect = icon != null ? CategoryUv : new Rect(0, 0, 1, 1);
            categoryBadge.color = icon != null ? Color.white : (isAttack ? AttackColor : StatusColor);
            categoryText.text = icon != null ? "" : (isAttack ? "공격" : "변화");

            currentSkill = index;
            ppValueText.text = $"{inst.PP}/{inst.MaxPP}";
            powerValueText.text = data.power > 0 ? data.power.ToString() : "-";
            accuracyValueText.text = data.accuracy > 0 ? data.accuracy.ToString() : "-";
        }

        void OnSkill(int index)
        {
            if (mode != Mode.Skill && mode != Mode.Command) return;
            var action = BattleAction.Skill(index);
            if (!battle.IsValidPlayerAction(action)) return;      // PP가 없는 스킬 등
            StartCoroutine(RunTurn(action));
        }

        // ───────── C 키: 능력치·기술 설명 보기 (포켓로그) ─────────
        // 기술을 고르는 동안 C를 누르고 있으면 → 내 정보창·상대 정보창 대신 공격·방어·스피드 단계, 기술 이름 칸 대신 고른 기술 설명.
        // C를 누른 채 방향키로 기술을 옮기면 설명도 바뀐다. 떼면 원래대로 (짧게 서서히).
        // 기술을 고르는 동안엔 상대 정보창 옆에 고른 기술의 상성 배율(0.5x · 2x …)이 나온다.
        int currentSkill = -1;
        TMP_Text skillDescText;
        public static bool DebugPeek;                 // (테스트) 메뉴 PalRogue/54: C를 누르고 있는 것처럼
        float peekFade;

        void UpdateSkillPeek()
        {
            bool skillMode = mode == Mode.Skill && battle != null && battle.Player != null;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool peek = skillMode && ((kb != null && kb.cKey.isPressed) || DebugPeek);

            playerInfo.SetPeek(peek, battle?.Player, battle?.Enemy);
            enemyInfo.SetPeek(peek && enemyInfo.gameObject.activeSelf, battle?.Enemy, battle?.Player);

            // 상성 배율
            SkillData data = skillMode && currentSkill >= 0 && currentSkill < battle.Player.Skills.Count ? battle.Player.Skills[currentSkill].Data : null;
            var foe = battle?.Enemy;
            if (data != null && foe != null && data.category != SkillCategory.Status && enemyInfo.gameObject.activeSelf)
                enemyInfo.SetEffectiveness(TypeChart.GetMultiplier(data.type, foe.Data.primaryType, foe.Data.SecondaryType));
            else enemyInfo.SetEffectiveness(null);

            // 기술 이름 칸 ↔ 기술 설명
            if (!skillMode) { if (peekFade > 0f) SetSkillPeekFade(0f, null); return; }
            float target = peek ? 1f : 0f;
            if (peekFade != target || peek)
                SetSkillPeekFade(Mathf.MoveTowards(peekFade, target, Time.unscaledDeltaTime * 8f), data);
        }

        void SetSkillPeekFade(float a, SkillData data)
        {
            peekFade = a;
            if (skillDescText == null)
            {
                var panel = (RectTransform)skillPanel.transform;
                var go = new GameObject("SkillDesc", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(panel, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(SkillDescPadX, SkillDescPadY); rt.offsetMax = new Vector2(-SkillDescPadX, -SkillDescPadY);
                skillDescText = go.AddComponent<TextMeshProUGUI>();
                skillDescText.font = messageText.font;
                skillDescText.fontSize = messageText.fontSize;
                skillDescText.enableAutoSizing = true;
                skillDescText.fontSizeMin = messageText.fontSize * 0.6f;
                skillDescText.fontSizeMax = messageText.fontSize;
                skillDescText.lineSpacing = messageText.lineSpacing;
                skillDescText.alignment = TextAlignmentOptions.TopLeft;
                skillDescText.textWrappingMode = TextWrappingModes.Normal;
                skillDescText.color = messageText.color;
                skillDescText.raycastTarget = false;
            }
            if (data != null) skillDescText.text = SummaryScreen.SkillDescription(data);
            skillDescText.alpha = a;
            for (int i = 0; i < skillTexts.Length; i++) skillTexts[i].alpha = 1f - a;
            if (skillCursor != null) { var c = skillCursor.color; c.a = 1f - a; skillCursor.color = c; }
        }

        const float SkillDescPadX = 8f, SkillDescPadY = 5f;

        // ───────── 턴 진행 ─────────
        IEnumerator RunTurn(BattleAction action)
        {
            mode = Mode.Busy;
            HidePanels();
            battle.ExecuteTurn(action);
            yield return PlayEvents();

            // 내 팰이 쓰러졌는데 교체할 팰이 남은 경우: 파티 화면에서 다음 팰을 고른다 (취소 불가)
            while (battle.State == BattleState.WaitingPlayerSwitch)
            {
                int next;
                if (partyScreen != null)
                {
                    yield return partyScreen.Choose(battle.PlayerParty, false, SelectPrompt, SwitchError);
                    next = partyScreen.Result;
                }
                else next = battle.PlayerParty.ToList().FindIndex(p => !p.IsFainted);   // 파티 화면이 없으면 자동
                battle.SwitchAfterFaint(next);
                yield return PlayEvents();
            }

            if (battle.State == BattleState.PlayerWon || battle.State == BattleState.Caught)
            {
                yield return FinishWave(false);
                yield break;
            }
            if (battle.State == BattleState.PlayerLost)
            {
                yield return GameOver();
                yield break;
            }
            ShowCommand();
        }

        /// <summary>Battle이 쌓아 둔 진행 기록을 순서대로 화면에 재생한다</summary>
        IEnumerator PlayEvents()
        {
            BattleEvent prev = null;
            while (battle.TryDequeueEvent(out var ev))
            {
                var box = ev.IsPlayerSide ? playerInfo : enemyInfo;
                var view = ev.IsPlayerSide ? playerView : enemyView;
                var before = prev;
                prev = ev;
                switch (ev.Type)
                {
                    case BattleEventType.StatStage:
                    {
                        box.ShowStages(ev.Stages, ev.Charging);
                        // 여러 능력치가 한꺼번에 같은 쪽으로 바뀌면 화살표는 한 번만
                        bool sameBurst = before != null && before.Type == BattleEventType.StatStage && before.Pal == ev.Pal
                                         && (before.Count > 0) == (ev.Count > 0);
                        if (!sameBurst) GameAudio.Se(ev.Count > 0 ? "stat_up" : "stat_down", 0.7f);
                        if (!sameBurst && view.IsVisible) yield return Fx.PlayStatStage(view, ev.Count > 0);
                        break;
                    }
                    case BattleEventType.BossPattern:
                    {
                        if (ev.Fx == BossFx.PhaseShift) { yield return PlayPhaseShift(ev); break; }   // 제로버스 2페이즈 대연출
                        PatternSound(ev.Fx);
                        box.ShowStages(ev.Stages, ev.Charging);
                        var targetView = ev.TargetIsPlayerSide ? playerView : enemyView;
                        if (view.IsVisible) yield return Fx.PlayBossFx(ev.Fx, ev.Pal.Data.primaryType, view, targetView);
                        if (ev.Fx == BossFx.Charge || ev.Fx == BossFx.Enrage) yield return ShakeBox((RectTransform)box.transform);
                        break;
                    }
                    case BattleEventType.Message:
                        yield return ShowMessage(ev.Text);
                        break;
                    case BattleEventType.HpChanged:
                        if (ev.From <= 0 && ev.To > 0) box.SetStatus(StatusCondition.None);   // 부활하면 기절 뱃지 지움
                        yield return box.AnimateHp(ev.Pal, ev.From, ev.To, ev.MaxHp);
                        break;
                    case BattleEventType.StatusChanged:
                        box.SetStatus(ev.Status);
                        if (ev.Status != StatusCondition.None && view.IsVisible) yield return Fx.PlayStatus(view, ev.Status);
                        break;
                    case BattleEventType.SkillAnim:
                    {
                        var targetView = ev.IsPlayerSide ? enemyView : playerView;
                        if (view.IsVisible && targetView.IsVisible)
                            yield return Fx.Play(ev.Skill, view, targetView, ev.Missed, ev.Critical, ev.Effectiveness);
                        // 맞는 소리 (포켓로그처럼 상성에 따라: 굉장 = 강한 소리, 별로 = 약한 소리)
                        if (!ev.Missed && ev.Skill != null && ev.Skill.category != SkillCategory.Status)
                            GameAudio.Se(ev.Effectiveness > 1f ? "hit_strong" : ev.Effectiveness < 1f ? "hit_weak" : "hit");
                        break;
                    }
                    case BattleEventType.SphereThrown:
                        GameAudio.Se("pb_throw");
                        yield return PlayThrow(ev.Sphere);
                        break;
                    case BattleEventType.SphereShakes:
                        GameAudio.Se("pb_bounce_1");
                        yield return PlayShakes(ev.Count);
                        break;
                    case BattleEventType.SphereCaught:
                        GameAudio.Se("pb_catch");
                        sphereView.ShowCaught();
                        enemyInfo.gameObject.SetActive(false);        // 잡히면 상대 정보창이 사라진다 (포켓로그와 같음)
                        break;
                    case BattleEventType.SphereBroke:
                        GameAudio.Se("pb_rel");
                        yield return PlayBreakFree();
                        break;
                    case BattleEventType.ExpBar:
                        if (ev.Pal == battle.Player) { GameAudio.Se("exp", 0.6f); yield return playerInfo.AnimateExp(ev.FromRatio, ev.ToRatio); }
                        break;
                    case BattleEventType.LevelUp:
                        if (ev.IsPlayerSide) Achievements.CheckLevel(ev.Level);
                        yield return PlayLevelUps(ev.Pal, ev.FromLevel > 0 ? ev.FromLevel : ev.Level - 1, ev.Level, ev.NewSkills);
                        break;
                    case BattleEventType.Fainted:
                        GameAudio.Se("faint");
                        box.ShowFainted();
                        yield return view.PlayFaint();
                        break;
                    case BattleEventType.ShieldBroken:
                        GameAudio.Se("hit_strong");
                        box.SetShields(ev.Pal);                        // 깨진 실드의 경계선이 사라진다
                        yield return ShakeBox((RectTransform)box.transform);
                        break;
                    case BattleEventType.Recalled:
                        yield return PlayRecall();
                        break;
                    case BattleEventType.Awakened:
                        GameAudio.Se("shine");
                        if (ev.IsPlayerSide) Achievements.Awakened();
                        yield return view.PlayAwaken();                 // 금빛으로 번쩍이며 각성
                        yield return ShakeBox((RectTransform)box.transform);
                        break;
                    case BattleEventType.SentOut:
                        if (ev.IsPlayerSide) { GameAudio.Se("pb_rel"); yield return PlaySendOut(ev.Pal); }   // 스피어를 던져서 내보낸다
                        else
                        {
                            view.Show(ev.Pal.Data, false);
                            box.Refresh(ev.Pal);
                        }
                        break;
                }
            }
        }

        // ───────── 지닌 도구 관리 (웨이브 클리어 후 보상 화면에서만) ─────────
        const string HeldModeHint = "지닌 도구 관리 모드를 바꿀 수 있습니다.";
        const int HeldMenuPage = 4;                  // 도구 종류가 많으면 한 번에 4개씩 (+ 다음 / 그만둔다)
        const float HeldMenuWidth = 124f;
        const float HeldNoticeSeconds = 1.1f;

        /// <summary>팰이 지닌 도구를 이름별로 묶는다 (같은 도구 여러 개 = 수량)</summary>
        static List<(string name, List<BattleEffect> items)> HeldGroups(Pal pal) =>
            pal.Effects.Where(e => e.HeldItemId != null).GroupBy(e => e.HeldItemId)
               .Select(g => (g.Key, g.ToList())).ToList();

        /// <summary>
        /// 포켓로그의 "지닌 도구 관리": 팰이 지닌 도구를 다른 팰에게 건네주거나 버린다.
        /// 파티 화면 왼쪽 아래 버튼으로 건넨다 ↔ 버리기 모드를 바꾼다. 그만둔다(Esc)로 보상 화면에 돌아간다.
        /// </summary>
        IEnumerator ManageHeldItems()
        {
            if (choiceMenu == null || partyScreen == null) yield break;
            bool toss = false;
            int startSlot = -1;
            choiceMenu.DrawOrder = HeldMenuOrder;        // 선택 창이 파티 화면 위에 보이게 (열릴 때마다 맞춘다)
            while (true)
            {
                string prompt = toss ? "도구를 버릴 팰을 선택하세요." : "도구를 옮길 팰을 선택하세요.";
                yield return partyScreen.Choose(run.Party, true, prompt,
                    i => HeldGroups(run.Party[i]).Count == 0 ? $"{Josa.Eun(run.Party[i].Data.palName)} 지닌 도구가 없다." : null,
                    toss ? "버린다" : "건넨다", direct: false, keepOpen: true, modeLabel: toss ? "버리기" : "건넨다", modeHint: HeldModeHint, startSlot: startSlot);
                int r = partyScreen.Result;
                if (r == PartyScreen.ModeToggled) { toss = !toss; startSlot = PartyScreen.ModeToggled; GameAudio.Se("select"); continue; }
                if (r < 0) break;
                startSlot = r;
                var src = run.Party[r];

                // ── 도구 고르기 (◀ ▶ 로 수량) ──
                var groups = HeldGroups(src);
                int pickIndex = -1, qty = 0;
                yield return ChooseHeldItem(groups, toss, (i, q) => { pickIndex = i; qty = q; });
                if (pickIndex < 0) continue;
                var (itemName, items) = groups[pickIndex];

                if (toss)
                {
                    partyScreen.SetMessage("이 도구를 정말로 버리시겠습니까?");
                    yield return choiceMenu.Choose(YesNo, 0);
                    if (choiceMenu.Result != 0) continue;
                    for (int k = 0; k < qty; k++) src.RemoveEffect(items[k]);
                    GameAudio.Se("select");
                    yield return PartyNotice($"{src.Data.palName}의 {Josa.Eul(qty > 1 ? $"{itemName} {qty}개" : itemName)} 버렸다.");
                    continue;
                }

                // ── 받을 팰 고르기 ──
                var def = ItemCatalog.ByName(itemName);
                yield return partyScreen.Choose(run.Party, true, $"{Josa.Eul(itemName)} 누구에게 건네줄까요?",
                    i => i == r ? "같은 팰에게는 건넬 수 없다."
                       : def?.CannotUse?.Invoke(run.Party[i]) is string why ? $"{run.Party[i].Data.palName}: {why}" : null,
                    null, direct: true, keepOpen: true);
                int to = partyScreen.Result;
                if (to < 0) continue;
                var dst = run.Party[to];
                int moved = 0;
                for (int k = 0; k < qty; k++)
                {
                    if (moved > 0 && def?.CannotUse?.Invoke(dst) != null) break;   // 한 개만 지닐 수 있는 도구
                    src.RemoveEffect(items[k]);
                    dst.AddEffect(items[k]);
                    moved++;
                }
                GameAudio.Se("select");
                yield return PartyNotice($"{src.Data.palName}의 {Josa.Eul(moved > 1 ? $"{itemName} {moved}개" : itemName)} {dst.Data.palName}에게 건넸다!");
            }
            partyScreen.Close();
        }
        const int HeldMenuOrder = 95;                 // 파티 화면(PartyScreen.SortingOrder)보다 위

        /// <summary>도구 목록 창 (팰 화면 위). 고르면 done(번호, 수량), 그만두면 done(-1, 0)</summary>
        IEnumerator ChooseHeldItem(List<(string name, List<BattleEffect> items)> groups, bool toss, System.Action<int, int> done)
        {
            var counts = groups.Select(_ => 1).ToArray();   // 처음 수량 = 1개 (좌우 방향키로 가진 만큼까지 늘린다)
            int page = 0, pages = groups.Count <= HeldMenuPage + 1 ? 1 : Mathf.CeilToInt(groups.Count / (float)HeldMenuPage);
            int perPage = pages == 1 ? groups.Count : HeldMenuPage;
            string hint = (toss ? "버릴" : "건네줄") + " 도구를 고르세요.\n좌우 방향키로 수량을 바꿀 수 있습니다.";
            string Label(int g) => counts[g] > 1 || groups[g].items.Count > 1 ? $"{groups[g].name} ×{counts[g]}" : groups[g].name;

            while (true)
            {
                int first = page * perPage, shown = Mathf.Min(perPage, groups.Count - first);
                var labels = new List<string>();
                for (int i = 0; i < shown; i++) labels.Add(Label(first + i));
                bool hasNext = pages > 1;
                if (hasNext) labels.Add($"다음 ({page + 1}/{pages})");
                labels.Add("그만둔다");

                partyScreen.SetMessage(hint);
                choiceMenu.HorizontalPressed = (row, dir) =>
                {
                    if (row < 0 || row >= shown) return;
                    int g = first + row, max = groups[g].items.Count;
                    int next = Mathf.Clamp(counts[g] + dir, 1, max);
                    if (next == counts[g]) return;
                    counts[g] = next;
                    choiceMenu.SetLabel(row, Label(g));
                    GameAudio.Se("select");
                };
                yield return choiceMenu.Choose(labels.ToArray(), 0, HeldMenuWidth);
                choiceMenu.HorizontalPressed = null;
                int pick = choiceMenu.Result;
                if (pick >= 0 && pick < shown) { done(first + pick, counts[first + pick]); yield break; }
                if (hasNext && pick == shown) { page = (page + 1) % pages; continue; }
                done(-1, 0);
                yield break;
            }
        }

        /// <summary>파티 화면 메시지창에 잠깐 알림을 띄운다 (누르면 바로 넘어감)</summary>
        IEnumerator PartyNotice(string text)
        {
            partyScreen.SetMessage(text);
            float t = 0f;
            while (t < HeldNoticeSeconds)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                if (t > 0.25f && AdvancePressed()) break;
            }
        }

        // ───────── 메시지 (한 글자씩 나오는 효과) ─────────
        IEnumerator ShowMessage(string text)
        {
            messageText.text = text;
            messageText.maxVisibleCharacters = 0;
            messageText.ForceMeshUpdate();
            int total = messageText.textInfo.characterCount;

            // 글자가 나오는 중에 누르면 문장을 한 번에 다 보여준다
            float elapsed = 0f;
            float shown = 0f;
            while (shown < total)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= SkipGuardSeconds && AdvancePressed()) break;
                // 넘기기 키를 누르고 있으면 글자가 바로 다 나온다 (빨리 넘기기)
                if (elapsed >= SkipGuardSeconds && BattleTime.AdvanceHeld) break;
                shown += CharsPerSecond * BattleTime.Multiplier * Time.unscaledDeltaTime;
                messageText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
            }
            messageText.maxVisibleCharacters = 9999;

            // 다 나온 뒤에는 잠깐 기다렸다가 넘어간다 (누르면 바로 넘어감, 누르고 있으면 아주 짧게만 보여 주고 넘어감)
            float hold = 0f;
            while (hold < LineHoldSeconds)
            {
                yield return null;
                hold += BattleTime.Dt;
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= SkipGuardSeconds * 2f && AdvancePressed()) break;
                if (BattleTime.AdvanceHeld && elapsed >= HeldLineSeconds) break;
            }
        }

        /// <summary>
        /// 메시지를 넘기는 입력: 마우스 왼쪽 클릭, Z, 엔터, 스페이스 (포켓로그와 같은 키).
        /// 메뉴를 고를 때 누른 클릭이 첫 메시지까지 넘겨 버리지 않도록, 메시지가 뜬 직후 잠깐은 무시한다.
        /// </summary>
        static bool AdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            if (kb != null && (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
            return false;
#else
            return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Z) ||
                   Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif
        }

        // ───────── 메뉴 / 커서 도우미 ─────────
        void HidePanels()
        {
            commandBox.SetActive(false);
            skillPanel.SetActive(false);
            skillInfoPanel.SetActive(false);
        }

        void Bind(Button button, UnityEngine.Events.UnityAction onClick)
        {
            button.onClick.AddListener(onClick);
            var entry = button.GetComponent<MenuEntry>();
            if (entry == null) return;
            entry.Selected += OnEntrySelected;
            entry.Cancelled += OnCancel;
        }

        void OnEntrySelected(MenuEntry entry)
        {
            lastEntry = entry;
            var button = entry.GetComponent<Button>();
            int skillIndex = System.Array.IndexOf(skillButtons, button);

            // 메뉴 항목의 부모(라벨) 위치 왼쪽에 ▶ 커서를 놓는다
            var label = entry.transform.parent as RectTransform;
            var cursor = skillIndex >= 0 ? skillCursor : commandCursor;
            if (label != null && cursor != null && button != restartButton)
                cursor.rectTransform.anchoredPosition = label.anchoredPosition + new Vector2(-7f, -1f);

            if (skillIndex >= 0) ShowSkillInfo(skillIndex);
            if (mode == Mode.Skill && skillIndex >= 0 && battle?.Player != null) lastSkillIndex[battle.Player] = skillIndex;
            if (mode == Mode.Command && (button == fightButton || button == sphereButton || button == palsButton || button == runButton)) lastCommand = button;
        }

        void OnCancel()
        {
            if (mode == Mode.Skill) ShowCommand();
        }

        void Select(Button button)
        {
            if (EventSystem.current == null) return;
            MenuEntry.Quiet = true;                                        // 코드로 고른 건 소리 없이
            EventSystem.current.SetSelectedGameObject(null);               // 같은 항목을 다시 골라도 커서가 갱신되게
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            MenuEntry.Quiet = false;
        }

        static string PalName(Pal pal) => string.IsNullOrEmpty(pal.Data.palName) ? pal.Data.name : pal.Data.palName;
    }

    /// <summary>속성의 한글 이름</summary>
    public static class PalTypeNames
    {
        public static string Korean(PalType type) => type switch
        {
            PalType.Neutral => "무",
            PalType.Fire => "불",
            PalType.Water => "물",
            PalType.Grass => "풀",
            PalType.Electric => "번개",
            PalType.Ground => "땅",
            PalType.Ice => "얼음",
            PalType.Dragon => "드래곤",
            PalType.Dark => "어둠",
            PalType.Void => "공허",
            _ => type.ToString(),
        };
    }
}
