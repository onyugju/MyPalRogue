#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/16: 포켓로그식 스피어(볼) 메뉴를 BattleCanvas 오른쪽 아래에 만들고 BattleScreen에 연결한다.
    /// 다시 실행하면 SphereMenu를 지우고 새로 만든다. 좌표는 320x180 화면 픽셀.
    /// </summary>
    public static class SphereMenuBuilder
    {
        const string UiDir = "Assets/palrogue_assets_core/images/ui";

        const float WindowWidth = 124f;               // 창 폭 (높이는 줄 수에 맞춰 코드가 바꾼다)
        const float RowX = 8f, RowWidth = 108f, RowHeight = 14f;
        const float CountX = 12f, CountWidth = 24f;   // "x82" (줄 왼쪽 기준)
        const float NameX = 36f, NameWidth = 70f;     // 이름은 오른쪽 정렬
        const float CursorX = 10f;
        const float Font = 11f;
        const int Rows = 6;                            // 스피어 5종 + 취소

        [MenuItem("PalRogue/16. 스피어 메뉴 만들기", false, 1016)]
        static void Build()
        {
            var canvasGo = GameObject.Find("BattleCanvas");
            var screen = canvasGo != null ? canvasGo.GetComponent<BattleScreen>() : null;
            if (screen == null)
            {
                EditorUtility.DisplayDialog("먼저 실행하세요", "메뉴 9, 10을 먼저 실행해서 전투 화면(BattleCanvas)을 만들어 주세요.", "확인");
                return;
            }
            var windowSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/windows/window_1.png");
            var cursorSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/cursor.png");
            var anyText = canvasGo.GetComponentInChildren<TextMeshProUGUI>(true);
            var font = anyText != null ? anyText.font : null;

            var old = canvasGo.transform.Find("SphereMenu");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // ── 창: 오른쪽 아래에 붙고 위로 늘어난다 ──
            var go = new GameObject("SphereMenu", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            var win = (RectTransform)go.transform;
            win.anchorMin = win.anchorMax = win.pivot = new Vector2(1, 0);
            win.anchoredPosition = Vector2.zero;
            win.sizeDelta = new Vector2(WindowWidth, SphereMenu.RowTop + SphereMenu.RowStep * Rows + SphereMenu.BottomPad);
            var bg = go.AddComponent<Image>();
            bg.sprite = windowSprite;
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            var buttons = new Button[Rows];
            var counts = new TMP_Text[Rows];
            var names = new TMP_Text[Rows];
            for (int i = 0; i < Rows; i++)
            {
                var row = TopLeft(win, $"Row{i + 1}", new Vector2(RowX, SphereMenu.RowTop + SphereMenu.RowStep * i), new Vector2(RowWidth, RowHeight));
                var hit = row.gameObject.AddComponent<Image>();
                hit.color = new Color(1, 1, 1, 0);           // 안 보이지만 클릭은 받는다
                hit.canvasRenderer.cullTransparentMesh = false;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                row.gameObject.AddComponent<MenuEntry>();
                buttons[i] = btn;
                counts[i] = Label(row, "Count", new Vector2(CountX, 0), new Vector2(CountWidth, RowHeight), font, TextAlignmentOptions.MidlineLeft);
                names[i] = Label(row, "Name", new Vector2(NameX, 0), new Vector2(NameWidth, RowHeight), font, TextAlignmentOptions.MidlineRight);
            }

            var cursorRt = TopLeft(win, "Cursor", new Vector2(CursorX, SphereMenu.RowTop + 1), new Vector2(6, 10));
            var cursor = cursorRt.gameObject.AddComponent<Image>();
            cursor.sprite = cursorSprite;
            cursor.raycastTarget = false;

            var menu = go.AddComponent<SphereMenu>();
            menu.window = win;
            menu.rowButtons = buttons;
            menu.countTexts = counts;
            menu.nameTexts = names;
            menu.cursor = cursor;
            go.SetActive(false);

            screen.sphereMenu = menu;
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[PalRogue] 스피어 메뉴를 만들었습니다. 시작 개수는 BattleCanvas > BattleScreen > Starting Spheres 에서 바꿀 수 있어요.");
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

        static TextMeshProUGUI Label(Transform parent, string name, Vector2 pos, Vector2 size, TMP_FontAsset font, TextAlignmentOptions align)
        {
            var t = TopLeft(parent, name, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = Font;
            t.alignment = align;
            t.color = Color.white;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.text = "";
            return t;
        }
    }
}
#endif
