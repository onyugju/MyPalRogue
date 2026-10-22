#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/17: 예/아니오 같은 작은 선택 창(ChoiceMenu)을 만들고 BattleScreen에 연결한다.
    /// 포켓로그처럼 메시지창 바로 위, 화면 오른쪽에 붙는다. 다시 실행하면 지우고 새로 만든다.
    /// </summary>
    public static class ChoiceMenuBuilder
    {
        const string UiDir = "Assets/palrogue_assets_core/images/ui";

        const float WindowWidth = 92f;
        const float BottomOffset = 48f;        // 메시지창 높이만큼 위로
        const float RowX = 8f, RowWidth = 80f, RowHeight = 14f, TextX = 12f;
        const float CursorX = 10f;
        const float Font = 11f;
        const int Rows = 6;

        [MenuItem("PalRogue/17. 선택 창(예·아니오) 만들기", false, 1017)]
        static void Build()
        {
            var canvasGo = GameObject.Find("BattleCanvas");
            var screen = canvasGo != null ? canvasGo.GetComponent<BattleScreen>() : null;
            if (screen == null)
            {
                EditorUtility.DisplayDialog("먼저 실행하세요", "메뉴 9, 10을 먼저 실행해서 전투 화면(BattleCanvas)을 만들어 주세요.", "확인");
                return;
            }
            var anyText = canvasGo.GetComponentInChildren<TextMeshProUGUI>(true);
            var font = anyText != null ? anyText.font : null;

            var old = canvasGo.transform.Find("ChoiceMenu");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject("ChoiceMenu", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            var win = (RectTransform)go.transform;
            win.anchorMin = win.anchorMax = win.pivot = new Vector2(1, 0);
            win.anchoredPosition = new Vector2(0, BottomOffset);
            win.sizeDelta = new Vector2(WindowWidth, ChoiceMenu.RowTop + ChoiceMenu.RowStep * 4 + ChoiceMenu.BottomPad);
            var bg = go.AddComponent<Image>();
            bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/windows/window_1.png");
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            var buttons = new Button[Rows];
            var labels = new TMP_Text[Rows];
            for (int i = 0; i < Rows; i++)
            {
                var row = TopLeft(win, $"Row{i + 1}", new Vector2(RowX, ChoiceMenu.RowTop + ChoiceMenu.RowStep * i), new Vector2(RowWidth, RowHeight));
                var hit = row.gameObject.AddComponent<Image>();
                hit.color = new Color(1, 1, 1, 0);
                hit.canvasRenderer.cullTransparentMesh = false;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                row.gameObject.AddComponent<MenuEntry>();
                buttons[i] = btn;

                var t = TopLeft(row, "Label", new Vector2(TextX, 0), new Vector2(RowWidth - TextX, RowHeight)).gameObject.AddComponent<TextMeshProUGUI>();
                if (font != null) t.font = font;
                t.fontSize = Font;
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.color = Color.white;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.raycastTarget = false;
                labels[i] = t;
            }

            var cursorRt = TopLeft(win, "Cursor", new Vector2(CursorX, ChoiceMenu.RowTop + 1), new Vector2(6, 10));
            var cursor = cursorRt.gameObject.AddComponent<Image>();
            cursor.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/cursor.png");
            cursor.raycastTarget = false;

            var menu = go.AddComponent<ChoiceMenu>();
            menu.window = win;
            menu.rowButtons = buttons;
            menu.labels = labels;
            menu.cursor = cursor;
            go.SetActive(false);

            screen.choiceMenu = menu;
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[PalRogue] 선택 창(ChoiceMenu)을 만들었습니다.");
        }

        static RectTransform TopLeft(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = size;
            return rt;
        }
    }
}
#endif
