using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 전투 화면 전체를 움직이는 컨트롤러. Battle(전투 로직)이 쌓아 둔 진행 기록을 순서대로 재생한다.
    /// 흐름: 등장 메시지 → 커맨드 선택 → (싸운다) 스킬 선택 → 턴 재생(메시지·HP바·쓰러짐) → 다시 커맨드
    /// 입력: 마우스 클릭, 방향키 + Enter/Space(확인), Esc(뒤로). 화면 UI 이벤트만 쓰기 때문에 입력 시스템과 상관없이 동작한다.
    /// 참조 칸들은 메뉴 PalRogue/10 이 자동으로 연결한다.
    /// </summary>
    public class BattleScreen : MonoBehaviour
    {
        [Header("전투 설정 (테스트용)")]
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
        public TMP_Text ppText;
        public TMP_Text typeText;
        public Image skillTypeIcon;

        [Header("끝난 뒤 다시 시작")]
        public Button restartButton;

        const float CharsPerSecond = 45f;
        const float LineHoldSeconds = 0.55f;
        static readonly Color Dim = new Color(0.55f, 0.55f, 0.55f);

        enum Mode { Busy, Command, Skill, Ended }
        Mode mode = Mode.Busy;
        Battle battle;
        MenuEntry lastEntry;

        void Start()
        {
            if (playerPal == null || enemyPal == null)
            {
                Debug.LogWarning("[BattleScreen] playerPal / enemyPal 에 PalData 에셋을 넣어주세요.");
                enabled = false;
                return;
            }

            messageText.textWrappingMode = TextWrappingModes.Normal;

            Bind(fightButton, OnFight);
            Bind(sphereButton, () => StartCoroutine(Unavailable("팰 스피어는 아직 쓸 수 없어요!")));
            Bind(palsButton, () => StartCoroutine(Unavailable("교체할 수 있는 팰이 없어요!")));
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

        // ───────── 전투 시작 ─────────
        void StartNewBattle()
        {
            battle?.Detach();
            var rng = seed >= 0 ? new System.Random(seed) : new System.Random();
            battle = new Battle(new List<Pal> { new Pal(playerPal, playerLevel) },
                                new List<Pal> { new Pal(enemyPal, enemyLevel) }, rng);

            playerView.Show(battle.Player.Data, true);
            enemyView.Show(battle.Enemy.Data, false);
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
                ppText.text = "";
                typeText.text = "";
                skillTypeIcon.enabled = false;
                return;
            }
            var inst = pal.Skills[index];
            ppText.text = $"PP {inst.PP}/{inst.MaxPP}";
            typeText.text = PalTypeNames.Korean(inst.Data.type);
            skillTypeIcon.sprite = Resources.Load<Sprite>($"1.팰/2.Elements/ui_type/{inst.Data.type}");
            skillTypeIcon.enabled = skillTypeIcon.sprite != null;
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

            // 내 팰이 쓰러졌는데 교체할 팰이 남은 경우: 팰 선택 화면이 생기기 전까지는 첫 번째 생존 팰을 자동으로 내보낸다
            while (battle.State == BattleState.WaitingPlayerSwitch)
            {
                int next = battle.PlayerParty.ToList().FindIndex(p => !p.IsFainted);
                battle.SwitchAfterFaint(next);
                yield return PlayEvents();
            }

            if (battle.State == BattleState.PlayerWon || battle.State == BattleState.PlayerLost)
            {
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
                        yield return box.AnimateHp(ev.Pal, ev.From, ev.To);
                        break;
                    case BattleEventType.Fainted:
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
