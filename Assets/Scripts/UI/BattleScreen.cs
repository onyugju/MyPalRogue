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
    /// 입력: 마우스 클릭, 방향키 + Enter/Space(확인), Esc(뒤로). 화면 UI 이벤트만 쓰기 때문에 입력 시스템과 상관없이 동작한다.
    /// 참조 칸들은 메뉴 PalRogue/10 이 자동으로 연결한다.
    /// </summary>
    public class BattleScreen : MonoBehaviour
    {
        [Header("전투 설정 (테스트용)")]
        [Tooltip("내 파티 (최대 6마리, 1번이 먼저 나간다). 비어 있으면 아래 playerPal 한 마리로 싸운다")]
        public List<PartyMember> playerParty = new List<PartyMember>();
        public PalData playerPal;
        public PalData enemyPal;
        [Range(1, GameConfig.MaxLevel)] public int playerLevel = 10;
        [Range(1, GameConfig.MaxLevel)] public int enemyLevel = 8;
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

        [Header("파티 화면 (메뉴 14로 생성)")]
        public PartyScreen partyScreen;

        [Header("예/아니오 선택 창 (메뉴 17로 생성)")]
        public ChoiceMenu choiceMenu;

        [Header("팰 스피어 (메뉴 16으로 생성)")]
        public SphereMenu sphereMenu;
        [Tooltip("시작할 때 가진 스피어 개수: 팰 / 기가 / 하이퍼 / 얼티밋 / 엑조틱")]
        public int[] startingSpheres = { 10, 5, 3, 2, 1 };

        [Header("끝난 뒤 다시 시작")]
        public Button restartButton;

        const float CharsPerSecond = 45f;
        const string SelectPrompt = "팰을 선택하세요.";   // 파티 화면 안내 문구
        const float LineHoldSeconds = 0.55f;
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
            bool hasPlayer = playerPal != null || playerParty.Any(m => m != null && m.pal != null);
            if (!hasPlayer || enemyPal == null)
            {
                Debug.LogWarning("[BattleScreen] 내 파티(playerParty 또는 playerPal)와 enemyPal 에 PalData 에셋을 넣어주세요.");
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

            Bind(fightButton, OnFight);
            Bind(sphereButton, OnSphere);
            Bind(palsButton, OnPals);
            Bind(runButton, () => StartCoroutine(Unavailable("도망칠 수 없어요!")));
            for (int i = 0; i < skillButtons.Length; i++)
            {
                int index = i;
                Bind(skillButtons[i], () => OnSkill(index));
            }
            Bind(restartButton, StartNewBattle);

            StartNewBattle();
        }

        void OnDestroy() => battle?.Detach();

        // 선택이 사라져도(빈 곳 클릭 등) 키보드 조작이 끊기지 않게 마지막 항목을 다시 잡아준다
        void Update()
        {
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

        // ───────── 전투 시작 ─────────
        void StartNewBattle()
        {
            battle?.Detach();
            var rng = seed >= 0 ? new System.Random(seed) : new System.Random();
            battle = new Battle(BuildPlayerParty(), new List<Pal> { new Pal(enemyPal, enemyLevel) }, rng);

            playerView.Show(battle.Player.Data, true);
            enemyView.Show(battle.Enemy.Data, false);
            if (sphereView != null) sphereView.Hide();
            spheres = new int[PalSpheres.Count];
            for (int i = 0; i < spheres.Length && startingSpheres != null && i < startingSpheres.Length; i++)
                spheres[i] = Mathf.Max(0, startingSpheres[i]);
            playerInfo.Refresh(battle.Player);
            enemyInfo.Refresh(battle.Enemy);

            restartButton.gameObject.SetActive(false);
            StartCoroutine(Intro());
        }

        IEnumerator Intro()
        {
            mode = Mode.Busy;
            HidePanels();
            yield return PlayEvents();
            ShowCommand();
        }

        // ───────── 커맨드 ─────────
        void ShowCommand()
        {
            mode = Mode.Command;
            skillPanel.SetActive(false);
            skillInfoPanel.SetActive(false);
            commandBox.SetActive(true);

            messageText.text = $"{PalName(battle.Player)}은(는)\n무엇을 할까?";
            messageText.maxVisibleCharacters = 9999;
            Select(fightButton);
        }

        // ───────── 파티 / 교체 ─────────
        List<Pal> BuildPlayerParty()
        {
            var list = playerParty.Where(m => m != null && m.pal != null).Take(6)
                                  .Select(m => new Pal(m.pal, m.level)).ToList();
            if (list.Count == 0) list.Add(new Pal(playerPal, playerLevel));
            return list;
        }

        // ───────── 팰 스피어 ─────────
        int[] spheres = new int[PalSpheres.Count];   // 지금 가진 스피어 개수
        SphereThrowView sphereView;

        void OnSphere()
        {
            if (mode != Mode.Command) return;
            if (sphereMenu == null) { StartCoroutine(Unavailable("스피어 메뉴가 아직 없어요! (메뉴 16 실행)")); return; }

            string blocked = battle.CatchBlockReason();
            if (blocked != null) { StartCoroutine(Unavailable(blocked)); return; }
            if (spheres.All(n => n <= 0)) { StartCoroutine(Unavailable("가진 팰 스피어가 없다!")); return; }

            StartCoroutine(SphereFromMenu());
        }

        IEnumerator SphereFromMenu()
        {
            mode = Mode.Busy;
            HidePanels();
            yield return sphereMenu.Choose(spheres);
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
                yield return ShowMessage($"지닌 팰이 가득 찼습니다. {name}을(를)\n대신해 다른 팰을 놓아주시겠습니까?");

                int choice = ChoiceNo;
                if (choiceMenu != null)
                {
                    yield return choiceMenu.Choose(FullPartyChoices, ChoiceYes);
                    choice = choiceMenu.Result < 0 ? ChoiceNo : choiceMenu.Result;   // Esc = 아니오
                }
                else Debug.LogWarning("[BattleScreen] 선택 창이 없어서 잡은 팰을 놓아줍니다. (메뉴 17 실행)");

                if (choice == ChoiceStats || choice == ChoiceDex)
                {
                    yield return ShowMessage("아직 준비 중인 기능이에요!");
                    continue;
                }

                if (choice == ChoiceNo)
                {
                    battle.ReleaseCaught();
                    yield return ShowMessage($"{name}을(를) 놓아주었다!");
                    break;
                }

                // 예: 파티 화면에서 놓아줄 팰 고르기 ("그만둔다"를 누르면 다시 질문으로)
                if (partyScreen == null) { battle.ReleaseCaught(); break; }
                yield return partyScreen.Choose(battle.PlayerParty, true, "놓아줄 팰을 선택하세요.", null, "놓아준다");
                if (partyScreen.Result < 0) continue;

                var released = battle.ReplaceWithCaught(partyScreen.Result);
                yield return ShowMessage($"{PalName(released)}을(를) 놓아주었다!");
                yield return ShowMessage($"{name}이(가) 파티에 들어왔다!");
            }

            msgRt.sizeDelta = new Vector2(normalWidth, msgRt.sizeDelta.y);
        }

        void OnPals()
        {
            if (mode != Mode.Command) return;
            if (partyScreen == null)
            {
                StartCoroutine(Unavailable("파티 화면이 아직 없어요! (메뉴 14 실행)"));
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
            if (pal.IsFainted) return $"{PalName(pal)}은(는) 싸울 수 있는 기력이 없다!";
            if (index == battle.PlayerIndex) return $"{PalName(pal)}은(는) 이미 싸우고 있다!";
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
                skillTexts[i].color = has && pal.Skills[i].HasPP ? Color.white : Dim;
            }
            Select(skillButtons[0]);
        }

        void ShowSkillInfo(int index)
        {
            var pal = battle.Player;
            if (index < 0 || index >= pal.Skills.Count)
            {
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

            if (battle.State == BattleState.PlayerWon || battle.State == BattleState.PlayerLost || battle.State == BattleState.Caught)
            {
                if (battle.PendingCatch != null) yield return ResolveFullParty();
                yield return ShowMessage("화면을 클릭하면 다시 시작해요.");
                mode = Mode.Ended;
                restartButton.gameObject.SetActive(true);
                Select(restartButton);
                yield break;
            }
            ShowCommand();
        }

        /// <summary>Battle이 쌓아 둔 진행 기록을 순서대로 화면에 재생한다</summary>
        IEnumerator PlayEvents()
        {
            while (battle.TryDequeueEvent(out var ev))
            {
                var box = ev.IsPlayerSide ? playerInfo : enemyInfo;
                var view = ev.IsPlayerSide ? playerView : enemyView;
                switch (ev.Type)
                {
                    case BattleEventType.Message:
                        yield return ShowMessage(ev.Text);
                        break;
                    case BattleEventType.HpChanged:
                        if (ev.From <= 0 && ev.To > 0) box.SetStatus(StatusCondition.None);   // 부활하면 기절 뱃지 지움
                        yield return box.AnimateHp(ev.Pal, ev.From, ev.To);
                        break;
                    case BattleEventType.StatusChanged:
                        box.SetStatus(ev.Status);
                        break;
                    case BattleEventType.SphereThrown:
                        yield return PlayThrow(ev.Sphere);
                        break;
                    case BattleEventType.SphereShakes:
                        yield return PlayShakes(ev.Count);
                        break;
                    case BattleEventType.SphereCaught:
                        sphereView.ShowCaught();
                        break;
                    case BattleEventType.SphereBroke:
                        yield return PlayBreakFree();
                        break;
                    case BattleEventType.Fainted:
                        box.ShowFainted();
                        yield return view.PlayFaint();
                        break;
                    case BattleEventType.SentOut:
                        view.Show(ev.Pal.Data, ev.IsPlayerSide);
                        box.Refresh(ev.Pal);
                        break;
                }
            }
        }

        // ───────── 메시지 (한 글자씩 나오는 효과) ─────────
        IEnumerator ShowMessage(string text)
        {
            messageText.text = text;
            messageText.maxVisibleCharacters = 0;
            messageText.ForceMeshUpdate();
            int total = messageText.textInfo.characterCount;

            float shown = 0f;
            while (shown < total)
            {
                shown += CharsPerSecond * Time.unscaledDeltaTime;
                messageText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            messageText.maxVisibleCharacters = 9999;
            yield return new WaitForSecondsRealtime(LineHoldSeconds);
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
        }

        void OnCancel()
        {
            if (mode == Mode.Skill) ShowCommand();
        }

        void Select(Button button)
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);               // 같은 항목을 다시 골라도 커서가 갱신되게
            EventSystem.current.SetSelectedGameObject(button.gameObject);
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
            PalType.Dragon => "용",
            PalType.Dark => "어둠",
            PalType.Void => "공허",
            _ => type.ToString(),
        };
    }
}
