using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace PalRogue
{
    /// <summary>
    /// 코드로 만드는 화면(타이틀·저장 슬롯)이 같이 쓰는 도우미: 캔버스, 칸, 글자, 커서 그림, 키 입력, 마우스.
    /// 모두 320x180 기준 픽셀, 왼쪽 위가 (0, 0).
    /// </summary>
    public static class UiKit
    {
        public static readonly Color FrameRed = Hex(0xd23c3c), FrameFill = Hex(0x3b3950), PanelFill = Hex(0x2c2a3c);
        public static readonly Color White = Hex(0xf8f8f8), Gold = Hex(0xf8d030), Soft = Hex(0x9a9aae), Shadow = Hex(0x3a3a3a);

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        /// <summary>화면 전체를 덮는 캔버스 + 가운데 320x180 판. 판을 돌려준다</summary>
        public static RectTransform Canvas(GameObject go, int order, Color background)
        {
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320f, 180f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
            var bgRt = bg.rectTransform;
            bgRt.SetParent(go.transform, false);
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(-4f, -4f); bgRt.offsetMax = new Vector2(4f, 4f);
            bg.color = background;

            var root = new GameObject("Layout", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(go.transform, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(320f, 180f);
            return root;
        }

        /// <summary>왼쪽 위 (x, y)에 w×h 칸. color가 있으면 단색 Image</summary>
        public static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            if (color.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = color.Value;
                img.raycastTarget = false;
            }
            return rt;
        }

        /// <summary>테두리(2픽셀) 칸. 바깥 칸을 돌려주고 안쪽 칸은 inner로</summary>
        public static RectTransform Frame(Transform parent, string name, float x, float y, float w, float h, Color border, Color fill, out RectTransform inner)
        {
            var outer = Box(parent, name, x, y, w, h, border);
            inner = Box(outer, "Inner", 2, 2, w - 4, h - 4, fill);
            return outer;
        }

        public static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, float x, float y, float w, float h, float size,
                                     TextAlignmentOptions align, Color color)
        {
            var rt = Box(parent, name, x, y, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.color = color;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        static Sprite triangle;
        /// <summary>▶ 모양 커서 (5x9 픽셀)</summary>
        public static Sprite Triangle
        {
            get
            {
                if (triangle != null) return triangle;
                const int w = 5, h = 9;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        tex.SetPixel(x, y, x <= 4 - Mathf.Abs(y - 4) ? Color.white : Color.clear);
                tex.Apply();
                triangle = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
                return triangle;
            }
        }

        public static Image Cursor(Transform parent)
        {
            var img = Box(parent, "Cursor", 0, 0, 5, 9).gameObject.AddComponent<Image>();
            img.sprite = Triangle;
            img.color = White;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>마우스: 올리면 enter, 누르면 click</summary>
        public static void Clickable(GameObject go, Action enter, Action click)
        {
            var img = go.GetComponent<Graphic>();
            if (img == null) { img = go.AddComponent<Image>(); img.color = new Color(1, 1, 1, 0); }
            img.raycastTarget = true;
            var c = go.AddComponent<ClickRelay>();
            c.enter = enter;
            c.click = click;
        }

        class ClickRelay : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
        {
            public Action enter, click;
            public void OnPointerEnter(PointerEventData e) => enter?.Invoke();
            public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) click?.Invoke(); }
        }
    }

    /// <summary>메뉴 키 입력 (새 입력 시스템 / 예전 입력 둘 다). 방향키, 확인(Enter·Z·Space), 취소(Esc·X·Backspace)</summary>
    public static class MenuKeys
    {
#if ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Keyboard Kb => UnityEngine.InputSystem.Keyboard.current;
        public static bool Confirm => Kb != null && (Kb.enterKey.wasPressedThisFrame || Kb.numpadEnterKey.wasPressedThisFrame || Kb.zKey.wasPressedThisFrame || Kb.spaceKey.wasPressedThisFrame);
        public static bool Cancel => Kb != null && (Kb.escapeKey.wasPressedThisFrame || Kb.xKey.wasPressedThisFrame || Kb.backspaceKey.wasPressedThisFrame);
        public static int Vertical => Kb == null ? 0 : (Kb.downArrowKey.wasPressedThisFrame ? 1 : 0) - (Kb.upArrowKey.wasPressedThisFrame ? 1 : 0);
        public static int Horizontal => Kb == null ? 0 : (Kb.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (Kb.leftArrowKey.wasPressedThisFrame ? 1 : 0);
        public static bool AnyClick => UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#else
        public static bool Confirm => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space);
        public static bool Cancel => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Backspace);
        /// <summary>아래 = +1</summary>
        public static int Vertical => (Input.GetKeyDown(KeyCode.DownArrow) ? 1 : 0) - (Input.GetKeyDown(KeyCode.UpArrow) ? 1 : 0);
        public static int Horizontal => (Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKeyDown(KeyCode.LeftArrow) ? 1 : 0);
        public static bool AnyClick => Input.GetMouseButtonDown(0);
#endif
    }

    /// <summary>
    /// 씬 사이에 넘겨주는 정보 + 씬 이동.
    /// 타이틀(TitleScene) → 새 게임: 팰 선택·저장 슬롯(StarterScene) → 본게임(GameScene)
    ///                    → 불러오기: 저장 슬롯(StarterScene) → 본게임(GameScene)
    /// </summary>
    public static class GameSession
    {
        public const string TitleScene = "TitleScene", StarterScene = "StarterScene", GameScene = "GameScene";

        /// <summary>StarterScene을 어떤 화면으로 열지: false = 새 게임(팰 선택부터), true = 불러오기(슬롯 선택만)</summary>
        public static bool OpenForLoad;
        /// <summary>새 게임으로 고른 팰 (본게임 씬이 받아서 모험을 시작한다)</summary>
        public static List<PalData> NewParty;
        /// <summary>불러온 저장 데이터</summary>
        public static RunSave Loaded;
        /// <summary>이번 모험을 저장하는 슬롯 (-1 = 저장 안 함: 에디터에서 GameScene을 바로 플레이한 경우)</summary>
        public static int Slot = -1;

        public static void Clear() { NewParty = null; Loaded = null; }

        /// <summary>씬이 빌드 설정에 들어 있는지</summary>
        public static bool CanLoad(string scene) => Application.CanStreamedLevelBeLoaded(scene);

        /// <summary>검게 어두워졌다가 씬을 바꾸고 다시 밝아진다</summary>
        public static void Go(string scene, TMP_FontAsset font)
        {
            var fade = ScreenFade.Create(font);
            UnityEngine.Object.DontDestroyOnLoad(fade.gameObject);
            fade.gameObject.SetActive(true);
            fade.StartCoroutine(GoRoutine(fade, scene));
        }

        static IEnumerator GoRoutine(ScreenFade fade, string scene)
        {
            yield return fade.FadeTo(1f, 0.35f);
            fade.gameObject.SetActive(true);
            var op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            yield return null;                       // 새 씬의 Start가 먼저 돌게
            UnityEngine.Object.Destroy(fade.gameObject, 0.5f);   // 밝아지는 연출이 끝나면 지운다 (다 밝아지면 스스로 꺼지므로 미리 예약)
            yield return fade.FadeTo(0f, 0.4f);
        }
    }
}
