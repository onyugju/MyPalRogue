using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 설정 창: 전체 볼륨 / 배경음악 / 효과음 / 대사 속도. ↑↓ 고르기, ←→ 바꾸기, Esc·닫기로 나가기.
    /// 바꾸는 즉시 적용·저장된다 (GameSettings). 타이틀과 게임 중 메뉴에서 같이 쓴다.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        static readonly string[] Rows = { "전체 볼륨", "배경음악", "효과음", "대사 속도", "닫기" };
        const float W = 224, H = 112, RowY = 22, RowStep = 16;

        TMP_FontAsset font;
        RectTransform window;
        TMP_Text[] labels, values;
        Image[][] blocks;
        Image cursor;
        int index;
        bool open;
        float openedAt;

        public bool IsOpen => open;
        /// <summary>마지막으로 닫힌 시각 (닫을 때 누른 Esc가 바깥 메뉴까지 닫지 않게)</summary>
        public float ClosedAt { get; private set; } = -10f;

        public static SettingsPanel Create(TMP_FontAsset font, int order)
        {
            var go = new GameObject("SettingsPanel", typeof(RectTransform));
            var p = go.AddComponent<SettingsPanel>();
            p.font = font;
            var root = UiKit.Canvas(go, order, new Color(0, 0, 0, 0.55f));
            p.window = UiKit.Frame(root, "Window", (320 - W) / 2, (180 - H) / 2, W, H, UiKit.FrameRed, UiKit.FrameFill, out var inner);
            UiKit.Label(inner, font, "Title", 0, 3, W - 4, 14, 11, TextAlignmentOptions.Center, UiKit.Gold).text = "설정";
            p.labels = new TMP_Text[Rows.Length];
            p.values = new TMP_Text[Rows.Length];
            p.blocks = new Image[Rows.Length][];
            for (int i = 0; i < Rows.Length; i++)
            {
                int row = i;
                float y = RowY + i * RowStep;
                var label = UiKit.Label(inner, font, "Row" + i, 14, y, 80, RowStep, 10, TextAlignmentOptions.MidlineLeft, UiKit.White);
                label.text = Rows[i];
                p.labels[i] = label;
                UiKit.Clickable(label.gameObject, () => { p.index = row; p.Refresh(); }, () => { p.index = row; p.Confirm(); });
                if (i == Rows.Length - 1) continue;

                var left = UiKit.Label(inner, font, "Less" + i, 98, y, 10, RowStep, 10, TextAlignmentOptions.Center, UiKit.Soft);
                left.text = "<";
                UiKit.Clickable(left.gameObject, () => { }, () => { p.index = row; p.Change(-1); });
                var right = UiKit.Label(inner, font, "More" + i, 180, y, 10, RowStep, 10, TextAlignmentOptions.Center, UiKit.Soft);
                right.text = ">";
                UiKit.Clickable(right.gameObject, () => { }, () => { p.index = row; p.Change(1); });

                if (i < 3)
                {
                    p.blocks[i] = new Image[GameSettings.MaxVolume];
                    for (int b = 0; b < GameSettings.MaxVolume; b++)
                    {
                        var img = UiKit.Box(inner, "Bar", 110 + b * 7, y + 4, 6, 8, UiKit.Soft).GetComponent<Image>();
                        p.blocks[i][b] = img;
                    }
                }
                else p.values[i] = UiKit.Label(inner, font, "Value" + i, 108, y, 72, RowStep, 10, TextAlignmentOptions.Center, UiKit.White);
            }
            p.cursor = UiKit.Cursor(inner);
            go.SetActive(false);
            return p;
        }

        /// <summary>창을 열고 닫힐 때까지 기다린다</summary>
        public IEnumerator Open()
        {
            gameObject.SetActive(true);
            open = true;
            index = 0;
            openedAt = Time.unscaledTime;
            Refresh();
            GameAudio.Se("menu_open");
            while (open) yield return null;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!open || Time.unscaledTime - openedAt < 0.15f) return;
            int v = MenuKeys.Vertical;
            if (v != 0) { index = (index + v + Rows.Length) % Rows.Length; GameAudio.Se("select"); Refresh(); }
            int h = MenuKeys.Horizontal;
            if (h != 0) Change(h);
            if (MenuKeys.Confirm) Confirm();
            else if (MenuKeys.Cancel) Close();
        }

        void Confirm()
        {
            if (index == Rows.Length - 1) Close();
            else Change(1, wrap: true);
        }

        void Close()
        {
            GameAudio.Se("select");
            open = false;
            ClosedAt = Time.unscaledTime;
        }

        void Change(int d, bool wrap = false)
        {
            switch (index)
            {
                case 0: GameSettings.Master = Step(GameSettings.Master, d, GameSettings.MaxVolume, wrap); break;
                case 1: GameSettings.Bgm = Step(GameSettings.Bgm, d, GameSettings.MaxVolume, wrap); break;
                case 2: GameSettings.Se = Step(GameSettings.Se, d, GameSettings.MaxVolume, wrap); break;
                case 3: GameSettings.TextSpeed = Step(GameSettings.TextSpeed, d, GameSettings.TextSpeedNames.Length - 1, wrap); break;
                default: return;
            }
            GameAudio.Se("select");          // 효과음 크기를 바로 들어 볼 수 있게
            Refresh();
        }

        static int Step(int value, int d, int max, bool wrap)
        {
            int v = value + d;
            if (wrap) return v > max ? 0 : v < 0 ? max : v;
            return Mathf.Clamp(v, 0, max);
        }

        void Refresh()
        {
            int[] vols = { GameSettings.Master, GameSettings.Bgm, GameSettings.Se };
            for (int i = 0; i < 3; i++)
                for (int b = 0; b < blocks[i].Length; b++)
                    blocks[i][b].color = b < vols[i] ? UiKit.Gold : new Color(0.35f, 0.35f, 0.45f);
            values[3].text = GameSettings.TextSpeedNames[GameSettings.TextSpeed];
            for (int i = 0; i < labels.Length; i++) labels[i].color = i == index ? UiKit.Gold : UiKit.White;
            cursor.rectTransform.anchoredPosition = new Vector2(5, -(RowY + index * RowStep + (RowStep - 9) / 2f));
        }
    }

    /// <summary>
    /// 게임 중 메뉴 (전투에서 명령을 고를 때 Esc).
    /// 계속하기 / 설정 / 타이틀로 / 게임 종료. 타이틀·종료는 한 번 더 묻는다.
    /// 저장은 보상을 받을 때마다 자동으로 되어 있으므로, 다시 불러오면 이번 웨이브 처음부터 이어서 한다.
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        public enum Choice { Resume, Title, Quit }

        const int Order = 430;
        const float W = 96, RowStep = 16;

        TMP_FontAsset font;
        RectTransform window, dialog;
        TMP_Text[] items;
        Image cursor, dialogCursor;
        TMP_Text dialogText;
        TMP_Text[] dialogItems;
        SettingsPanel settings;
        readonly List<string> rows = new List<string>();
        int index, dialogIndex, dialogAnswer;
        bool waiting, inDialog;
        float openedAt;
        int clicked = -1;

        public Choice Result { get; private set; }

        public static GameMenu Create(TMP_FontAsset font, bool canGoTitle)
        {
            var go = new GameObject("GameMenu", typeof(RectTransform));
            var m = go.AddComponent<GameMenu>();
            m.font = font;
            m.rows.Add("계속하기");
            m.rows.Add("설정");
            if (canGoTitle) m.rows.Add("타이틀로");
            m.rows.Add("게임 종료");

            var root = UiKit.Canvas(go, Order, new Color(0, 0, 0, 0.45f));
            float h = 10 + RowStep * m.rows.Count;
            m.window = UiKit.Frame(root, "Window", 320 - W - 4, 4, W, h, UiKit.FrameRed, UiKit.FrameFill, out var inner);
            m.items = new TMP_Text[m.rows.Count];
            for (int i = 0; i < m.rows.Count; i++)
            {
                int row = i;
                var t = UiKit.Label(inner, font, "Item" + i, 14, 3 + i * RowStep, W - 18, RowStep, 10, TextAlignmentOptions.MidlineLeft, UiKit.White);
                t.text = m.rows[i];
                UiKit.Clickable(t.gameObject, () => { if (!m.inDialog) { m.index = row; m.Refresh(); } }, () => { if (!m.inDialog) { m.index = row; m.clicked = row; } });
                m.items[i] = t;
            }
            m.cursor = UiKit.Cursor(inner);

            m.dialog = UiKit.Frame(root, "Dialog", 40, 64, 240, 52, UiKit.FrameRed, UiKit.FrameFill, out var dInner);
            m.dialogText = UiKit.Label(dInner, font, "Text", 8, 4, 224, 26, 9, TextAlignmentOptions.TopLeft, UiKit.White);
            m.dialogText.textWrappingMode = TextWrappingModes.Normal;
            m.dialogItems = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                int row = i;
                m.dialogItems[i] = UiKit.Label(dInner, font, "Choice" + i, 30 + i * 80, 30, 60, 14, 10, TextAlignmentOptions.MidlineLeft, UiKit.White);
                m.dialogItems[i].text = i == 0 ? "예" : "아니오";
                UiKit.Clickable(m.dialogItems[i].gameObject, () => { m.dialogIndex = row; m.RefreshDialog(); }, () => { m.dialogAnswer = row; });
            }
            m.dialogCursor = UiKit.Cursor(dInner);
            m.dialog.gameObject.SetActive(false);

            m.settings = SettingsPanel.Create(font, Order + 5);
            go.SetActive(false);
            return m;
        }

        /// <summary>메뉴를 열고 고를 때까지 기다린다 (Result)</summary>
        public IEnumerator Open()
        {
            gameObject.SetActive(true);
            index = 0;
            Result = Choice.Resume;
            openedAt = Time.unscaledTime;
            GameAudio.Se("menu_open");
            Refresh();
            while (true)
            {
                waiting = true;
                clicked = -1;
                while (clicked < 0) yield return null;
                waiting = false;
                string row = rows[clicked];
                GameAudio.Se("select");
                if (row == "계속하기") { Result = Choice.Resume; break; }
                if (row == "설정") { window.gameObject.SetActive(false); yield return settings.Open(); window.gameObject.SetActive(true); openedAt = Time.unscaledTime; continue; }
                if (row == "타이틀로")
                {
                    yield return Ask("타이틀로 돌아갈까요?\n(보상을 받을 때마다 저장되어 있어서, 이번 웨이브 처음부터 이어서 할 수 있어요)");
                    if (dialogAnswer == 0) { Result = Choice.Title; break; }
                    continue;
                }
                if (row == "게임 종료")
                {
                    yield return Ask("게임을 끌까요?\n(다음에 \"계속하기\"로 이번 웨이브 처음부터 이어서 할 수 있어요)");
                    if (dialogAnswer == 0) { Result = Choice.Quit; break; }
                    continue;
                }
            }
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (Time.unscaledTime - openedAt < 0.15f || settings.IsOpen || Time.unscaledTime - settings.ClosedAt < 0.15f) return;
            if (inDialog)
            {
                int h = MenuKeys.Horizontal + MenuKeys.Vertical;
                if (h != 0) { dialogIndex = (dialogIndex + h + 2) % 2; GameAudio.Se("select"); RefreshDialog(); }
                if (MenuKeys.Confirm) dialogAnswer = dialogIndex;
                else if (MenuKeys.Cancel) dialogAnswer = 1;
                return;
            }
            if (!waiting) return;
            int v = MenuKeys.Vertical;
            if (v != 0) { index = (index + v + rows.Count) % rows.Count; GameAudio.Se("select"); Refresh(); }
            if (MenuKeys.Confirm) clicked = index;
            else if (MenuKeys.Cancel) clicked = 0;            // Esc = 계속하기
        }

        IEnumerator Ask(string question)
        {
            dialogText.text = question;
            dialogIndex = 1;
            dialogAnswer = -1;
            inDialog = true;
            dialog.SetAsLastSibling();
            dialog.gameObject.SetActive(true);
            RefreshDialog();
            yield return null;
            while (dialogAnswer < 0) yield return null;
            GameAudio.Se("select");
            dialog.gameObject.SetActive(false);
            inDialog = false;
            openedAt = Time.unscaledTime;
        }

        void Refresh()
        {
            for (int i = 0; i < items.Length; i++) items[i].color = i == index ? UiKit.Gold : UiKit.White;
            cursor.rectTransform.anchoredPosition = new Vector2(5, -(3 + index * RowStep + (RowStep - 9) / 2f));
        }

        void RefreshDialog() => dialogCursor.rectTransform.anchoredPosition = new Vector2(22 + dialogIndex * 80, -32.5f);
    }
}
