using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 전투 화면의 아이템 줄 (포켓로그의 modifier bar).
    ///  - 왼쪽 위 (상대 정보창 위): 내가 가진 파티 전체 아이템(경험부적·학습장치…, 개수) + 몇 웨이브짜리 아이템(남은 웨이브)
    ///    + 내 팰이 지닌 아이템(팰 그림 + 작은 아이템 그림, 개수)
    ///  - 오른쪽 위 (웨이브 이름 위): 상대 팰이 지닌 아이템. 있으면 웨이브·돈 글자가 그만큼 아래로 내려간다
    ///  - 한 줄 최대 10개, 2줄까지. 더 많으면 마지막 칸이 "+" — 마우스를 올리면 나머지가 모두 펼쳐진다
    ///  - 아이콘에 마우스를 올리면 이름(등급 색)과 효과 설명
    /// 0.25초마다 내용을 확인해서 바뀌었을 때만 다시 그린다.
    /// </summary>
    public class ItemBar : MonoBehaviour
    {
        const int Order = 45;                 // 전투 UI 위, 보상 화면(50)·파티 화면(80) 아래
        const int PerRow = 10, MaxRows = 2;
        const float Cell = 10.5f, Icon = 10f, Margin = 1f;   // 칸 10 + 간격 0.5 → 2줄이어도 상대 체력창(약 27) 위에 들어간다
        const float PalSize = 8.5f, HeldItem = 6.5f;           // 지닌 아이템: 팰 그림(모두 같은 크기) + 오른쪽 아래 아이템

        public class Entry
        {
            public string key;                // 같은 칸으로 묶는 기준
            public ItemDef def;
            public string name, desc;
            public Color nameColor = Color.white;
            public Sprite pal;                 // 팰이 지닌 아이템이면 그 팰 그림
            public int count;                  // 아래 왼쪽 숫자 (개수 또는 남은 웨이브)
            public bool turns;                 // count가 남은 웨이브인지 (설명에 표시)
        }

        TMP_FontAsset font;
        RectTransform root, playerRow, enemyRow, overflow;
        Func<bool> hidden;                    // 파티·능력치 화면이 열려 있으면 숨긴다
        Func<Run> getRun;
        Func<Pal> getEnemy;
        RectTransform areaText, moneyText;
        Vector2 areaHome, moneyHome;
        string lastSignature;
        float nextCheck;
        // "+" 펼치기
        List<Entry> overflowEntries;
        bool overPlus, overOverflow;
        float overflowHideAt;
        bool overflowIsEnemy;

        public static ItemBar Create(TMP_FontAsset font, Func<Run> run, Func<Pal> enemy, TMP_Text areaText, TMP_Text moneyText, Func<bool> hidden = null)
        {
            var go = new GameObject("ItemBar", typeof(RectTransform));
            var bar = go.AddComponent<ItemBar>();
            bar.font = font;
            bar.getRun = run;
            bar.getEnemy = enemy;
            bar.hidden = hidden;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320f, 180f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            bar.root = new GameObject("Layout", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.root.SetParent(go.transform, false);
            bar.root.anchorMin = bar.root.anchorMax = bar.root.pivot = new Vector2(0.5f, 0.5f);
            bar.root.sizeDelta = new Vector2(320f, 180f);
            bar.playerRow = UiKit.Box(bar.root, "Player", 0, 0, 160, 40);
            bar.enemyRow = UiKit.Box(bar.root, "Enemy", 160, 0, 160, 40);

            bar.overflow = UiKit.Frame(bar.root, "Overflow", 0, 0, 160, 40, UiKit.FrameRed, UiKit.FrameFill, out _);
            UiKit.Clickable(bar.overflow.gameObject, () => bar.overOverflow = true, () => { });
            AddExit(bar.overflow.gameObject, () => { bar.overOverflow = false; bar.overflowHideAt = Time.unscaledTime + 0.15f; });
            bar.overflow.gameObject.SetActive(false);

            if (areaText != null) { bar.areaText = areaText.rectTransform; bar.areaHome = bar.areaText.anchoredPosition; }
            if (moneyText != null) { bar.moneyText = moneyText.rectTransform; bar.moneyHome = bar.moneyText.anchoredPosition; }
            return bar;
        }

        // ═════════════════ 내용 모으기 ═════════════════

        static string PalName(Pal p) => string.IsNullOrEmpty(p.Data.palName) ? p.Data.name : p.Data.palName;

        List<Entry> PlayerEntries(Run run)
        {
            var list = new List<Entry>();
            if (run == null) return list;
            // ① 파티 전체 아이템 (개수)
            foreach (var kv in run.RunItems)
            {
                var def = ItemCatalog.Get(kv.Key);
                if (def == null) continue;
                list.Add(new Entry { key = "run:" + kv.Key, def = def, name = def.Name, desc = def.Description, nameColor = ItemTiers.Color(def.Tier), count = kv.Value });
            }
            // ② 몇 웨이브짜리 아이템 (남은 웨이브) — 파티 전원에게 같이 붙으므로 한 칸으로
            var seen = new HashSet<string>();
            foreach (var p in run.Party)
                foreach (var e in p.Effects)
                {
                    if (e is not ITimedEffect timed) continue;
                    var def = TimedDef(e);
                    string key = "timed:" + (def != null ? def.Id : e.GetType().Name);
                    if (!seen.Add(key)) continue;
                    list.Add(new Entry
                    {
                        key = key, def = def, name = def?.Name ?? e.Name, desc = def?.Description ?? e.Description,
                        nameColor = def != null ? ItemTiers.Color(def.Tier) : Color.white, count = timed.WavesLeft, turns = true,
                    });
                }
            // ③ 내 팰이 지닌 아이템 (팰마다, 같은 아이템은 개수로)
            foreach (var p in run.Party) list.AddRange(HeldEntries(p, "mine"));
            return list;
        }

        static IEnumerable<Entry> HeldEntries(Pal p, string side)
        {
            if (p == null) yield break;
            foreach (var g in p.Effects.Where(e => e.HeldItemId != null).GroupBy(e => e.HeldItemId))
            {
                var def = ItemCatalog.ByName(g.Key);
                yield return new Entry
                {
                    key = $"{side}:{p.GetHashCode()}:{g.Key}", def = def, name = g.Key,
                    desc = (def?.Description ?? g.First().Description) + $"\n<color=#9a9aae>{Josa.I(PalName(p))} 지니고 있다</color>",
                    nameColor = def != null ? ItemTiers.Color(def.Tier) : Color.white, pal = p.Data.frontSprite, count = g.Count(),
                };
            }
        }

        /// <summary>몇 웨이브짜리 효과 → 그 효과를 준 아이템</summary>
        static ItemDef TimedDef(BattleEffect e) => e switch
        {
            TempStatBoost s => ItemCatalog.Get(s.Stat switch { StatType.Attack => "x_attack", StatType.Defense => "x_defense", _ => "x_speed" }),
            TempCritBoost => ItemCatalog.Get("dire_hit"),
            TempTypeBoost t => ItemCatalog.All.FirstOrDefault(d => d.Id.StartsWith("whistle_") && d.Name.EndsWith(PalTypeNames.Korean(t.Type))),
            _ => null,
        };

        // ═════════════════ 그리기 ═════════════════

        void Update()
        {
            bool hide;
            try { hide = hidden != null && hidden(); }
            catch (Exception) { hide = false; }
            if (hide != !root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(!hide);
                if (hide) { HideTip(); overPlus = overOverflow = false; overflow.gameObject.SetActive(false); }
            }
            if (hide) return;
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.25f;
                Run run; Pal enemy;
                try { run = getRun(); enemy = getEnemy(); }
                catch (Exception) { return; }      // 씬이 닫히는 중 (전투 화면이 먼저 사라짐)
                var mine = PlayerEntries(run);
                var theirs = enemy != null ? HeldEntries(enemy, "enemy").ToList() : new List<Entry>();
                string sig = string.Join("|", mine.Select(e => e.key + e.count)) + "#" + string.Join("|", theirs.Select(e => e.key + e.count));
                if (sig != lastSignature)
                {
                    lastSignature = sig;
                    Draw(playerRow, mine, false);
                    int rows = Draw(enemyRow, theirs, true);
                    float push = rows * Cell + (rows > 0 ? 1 : 0);
                    if (areaText != null) areaText.anchoredPosition = areaHome + Vector2.down * push;   // 웨이브 이름을 아이템 아래로
                    if (moneyText != null) moneyText.anchoredPosition = moneyHome + Vector2.down * push;
                }
            }
            if (overflow.gameObject.activeSelf && !overPlus && !overOverflow && Time.unscaledTime > overflowHideAt)
            {
                overflow.gameObject.SetActive(false);
                HideTip();
            }
        }

        /// <summary>한 쪽 줄을 다시 그린다. 쓴 줄 수를 돌려준다</summary>
        int Draw(RectTransform row, List<Entry> entries, bool rightAligned)
        {
            for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
            HideTip();
            if ((rightAligned == overflowIsEnemy) && overflow.gameObject.activeSelf) overflow.gameObject.SetActive(false);
            if (entries.Count == 0) return 0;
            int max = PerRow * MaxRows;
            bool cut = entries.Count > max;
            int shown = cut ? max - 1 : entries.Count;
            for (int i = 0; i < shown; i++) MakeCell(row, entries[i], i, rightAligned);
            if (cut)
            {
                var rest = entries.Skip(shown).ToList();
                var plus = CellRect(row, max - 1, rightAligned);
                var bg = plus.gameObject.AddComponent<Image>();
                bg.color = UiKit.FrameRed;                                   // 빨간 테두리 칸 안에 "+N"
                var fill = UiKit.Box(plus, "Fill", 1, 1, Icon - 2, Icon - 2, UiKit.FrameFill);
                var t = UiKit.Label(fill, font, "Plus", 0, 0, Icon - 2, Icon - 2, 9, TextAlignmentOptions.Center, UiKit.Gold);
                t.text = $"+{rest.Count}";
                t.enableAutoSizing = true; t.fontSizeMin = 4; t.fontSizeMax = 7;
                UiKit.Clickable(plus.gameObject, () => { overPlus = true; ShowOverflow(rest, plus, rightAligned); }, () => { });
                AddExit(plus.gameObject, () => { overPlus = false; overflowHideAt = Time.unscaledTime + 0.15f; });
            }
            return Mathf.Min(MaxRows, (Mathf.Min(entries.Count, max) + PerRow - 1) / PerRow);
        }

        RectTransform CellRect(RectTransform parent, int index, bool rightAligned)
        {
            int r = index / PerRow, c = index % PerRow;
            float x = rightAligned ? 160 - Margin - (c + 1) * Cell : Margin + c * Cell;
            return UiKit.Box(parent, "Cell" + index, x, Margin + r * Cell, Icon, Icon);
        }

        void MakeCell(RectTransform parent, Entry e, int index, bool rightAligned, bool forOverflow = false)
        {
            var cell = forOverflow ? UiKit.Box(parent, "Cell" + index, 3 + (index % PerRow) * Cell, 3 + (index / PerRow) * Cell, Icon, Icon)
                                   : CellRect(parent, index, rightAligned);
            if (e.pal != null)
            {
                // 팰이 지닌 아이템: 팰 그림(투명한 여백을 잘라 모든 팰을 같은 크기로) + 오른쪽 아래 작은 아이템 그림
                PalIcon(cell, e.pal, 0, 0, PalSize);
                if (e.def != null) AddItemIcon(cell, e.def, Icon - HeldItem + 1f, Icon - HeldItem + 1f, HeldItem + 1f);
            }
            else if (e.def != null) AddItemIcon(cell, e.def, -1.25f, -1.25f, Icon + 2.5f);   // 아이템 그림의 투명 여백만큼 크게 그려 칸을 채운다

            if (e.turns) TurnBadge(cell, e.count);                 // 몇 웨이브짜리: 노란 동그라미 안에 남은 웨이브
            else if (e.count > 0) OutlinedNumber(cell, e.count.ToString());   // 개수: 검은 테두리 흰 숫자
            var hit = cell.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            hit.raycastTarget = true;
            UiKit.Clickable(cell.gameObject, () =>
            {
                if (forOverflow) overOverflow = true;
                ShowTip(e, cell);
            }, () => { });
            AddExit(cell.gameObject, () =>
            {
                HideTip();
                if (forOverflow) { overOverflow = false; overflowHideAt = Time.unscaledTime + 0.15f; }
            });
        }


        // ── 숫자 표시 ──
        static readonly Vector2[] OutlineDirs =
        {
            new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1),
            new Vector2(-1, -1), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1),
        };

        /// <summary>왼쪽 아래 개수: 검은 글자 8개를 사방으로 깔고 그 위에 흰 글자 → 검은 테두리</summary>
        void OutlinedNumber(RectTransform cell, string text)
        {
            const float x = 0.3f, y = Icon - 6.5f, w = 10f, h = 7f, size = 7f, o = 0.45f;
            foreach (var d in OutlineDirs)
            {
                var b = UiKit.Label(cell, font, "Outline", x + d.x * o, y + d.y * o, w, h, size, TextAlignmentOptions.BottomLeft, Color.black);
                b.text = text;
            }
            UiKit.Label(cell, font, "Count", x, y, w, h, size, TextAlignmentOptions.BottomLeft, UiKit.White).text = text;
        }

        /// <summary>오른쪽 아래 노란 동그라미 + 진한 숫자 (몇 웨이브 남았는지)</summary>
        void TurnBadge(RectTransform cell, int turns)
        {
            string text = turns.ToString();
            float w = text.Length > 1 ? 8.5f : 6.5f, h = 6.5f;
            var badge = UiKit.Box(cell, "Turns", Icon - w + 1f, Icon - h + 1f, w, h);
            var img = badge.gameObject.AddComponent<Image>();
            img.sprite = Circle;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            var t = UiKit.Label(badge, font, "N", 0.15f, -0.3f, w, h, 5, TextAlignmentOptions.Center, UiKit.Hex(0x202020));
            t.text = text;
        }

        static Sprite circle;
        /// <summary>노란 동그라미 (진한 테두리 1픽셀). 9-슬라이스라 두 자리 숫자면 옆으로 늘어난다</summary>
        static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                Color edge = UiKit.Hex(0x3a3218), fill = UiKit.Hex(0xf8d030), hi = UiKit.Hex(0xfff09a);
                float c = (n - 1) / 2f, R = n / 2f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                        float a = Mathf.Clamp01(R - d);                                       // 바깥 경계 부드럽게
                        Color col = d > R - 3.5f ? edge : (y > c + 3 && d > R - 7f ? hi : fill);   // 진한 테두리 + 위쪽 밝은 테
                        col.a = a;
                        px[y * n + x] = col;
                    }
                tex.SetPixels32(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
                return circle;
            }
        }

        // ── 팰 그림 여백 자르기 (모든 팰을 같은 크기로) ──
        // 새 Sprite를 만들지 않고, 원래 텍스처에서 보이는 부분(uv)만 RawImage로 그린다 (지워질 걱정 없음)
        static readonly Dictionary<Sprite, Rect> trimmed = new Dictionary<Sprite, Rect>();

        /// <summary>box(size×size) 안에 팰을 가운데 맞춰 그린다. 긴 쪽이 칸을 꽉 채운다</summary>
        public static void PalIcon(RectTransform parent, Sprite s, float x, float y, float size)
        {
            if (s == null || s.texture == null) return;
            var px = TrimRect(s);                                     // 텍스처 픽셀 단위
            float k = size / Mathf.Max(px.width, px.height);
            float w = px.width * k, h = px.height * k;
            var img = UiKit.Box(parent, "Pal", x + (size - w) / 2, y + (size - h) / 2, w, h).gameObject.AddComponent<RawImage>();
            var tex = s.texture;
            img.texture = tex;
            img.uvRect = new Rect(px.x / tex.width, px.y / tex.height, px.width / tex.width, px.height / tex.height);
            img.raycastTarget = false;
        }

        static Rect TrimRect(Sprite s)
        {
            if (trimmed.TryGetValue(s, out var done)) return done;
            Rect r = s.textureRect, result = r;
            try
            {
                int x0 = (int)r.x, y0 = (int)r.y, w = (int)r.width, h = (int)r.height;
                var px = ReadPixels(s.texture);
                int tw = s.texture.width, minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[(y0 + y) * tw + x0 + x].a > 20)
                        {
                            if (x < minX) minX = x; if (x > maxX) maxX = x;
                            if (y < minY) minY = y; if (y > maxY) maxY = y;
                        }
                if (maxX >= minX && maxY >= minY) result = new Rect(x0 + minX, y0 + minY, maxX - minX + 1, maxY - minY + 1);
            }
            catch (Exception) { }                                     // 못 읽으면 원래 크기 그대로
            trimmed[s] = result;
            return result;
        }

        /// <summary>텍스처 픽셀 읽기. 읽기 불가 텍스처는 RenderTexture로 복사해서 읽는다</summary>
        static Color32[] ReadPixels(Texture2D tex)
        {
            if (tex.isReadable) return tex.GetPixels32();
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                copy.Apply();
                var px = copy.GetPixels32();
                Destroy(copy);
                return px;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static void AddItemIcon(RectTransform cell, ItemDef def, float x, float y, float size)
        {
            var (tex, uv) = def.LoadIcon();
            if (tex == null) return;
            var img = UiKit.Box(cell, "Item", x, y, size, size).gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.uvRect = uv;
            img.raycastTarget = false;
            if (tex is Texture2D t2) t2.filterMode = FilterMode.Point;
        }

        // ── "+" 펼치기 ──
        void ShowOverflow(List<Entry> rest, RectTransform plus, bool rightAligned)
        {
            overflowEntries = rest;
            overflowIsEnemy = rightAligned;
            var inner = (RectTransform)overflow.GetChild(0);
            for (int i = inner.childCount - 1; i >= 0; i--) Destroy(inner.GetChild(i).gameObject);
            int cols = Mathf.Min(PerRow, rest.Count), rows = (rest.Count + PerRow - 1) / PerRow;
            float w = cols * Cell + 8, h = rows * Cell + 8;
            overflow.sizeDelta = new Vector2(w, h);
            inner.sizeDelta = new Vector2(w - 4, h - 4);
            for (int i = 0; i < rest.Count; i++) MakeCell(inner, rest[i], i, false, true);
            // "+" 칸 바로 아래에 펼친다 (화면 밖으로 나가지 않게)
            var p = ToRoot(plus);
            float x = rightAligned ? Mathf.Max(2, p.x + Icon - w) : Mathf.Min(318 - w, p.x);
            float y = Mathf.Min(178 - h, p.y + Icon + 1);
            overflow.anchoredPosition = new Vector2(x, -y);
            overflow.SetAsLastSibling();
            overflow.gameObject.SetActive(true);
        }

        // ── 설명 ──
        void ShowTip(Entry e, RectTransform cell)
        {
            string desc = e.desc ?? "";
            if (e.turns) desc += $"\n<color=#f8d030>남은 웨이브 {e.count}</color>";
            ItemTooltip.Show(font, e.name, e.nameColor, desc, cell);
        }

        static void HideTip() => ItemTooltip.Hide();

        /// <summary>칸의 왼쪽 위 위치 (320x180 판 기준, 아래로 +)</summary>
        Vector2 ToRoot(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var local = root.InverseTransformPoint(corners[1]);                // 왼쪽 위
            return new Vector2(local.x + 160f, 90f - local.y);
        }

        static void AddExit(GameObject go, Action exit)
        {
            var relay = go.AddComponent<ExitRelay>();
            relay.exit = exit;
        }

        class ExitRelay : MonoBehaviour, IPointerExitHandler
        {
            public Action exit;
            public void OnPointerExit(PointerEventData e) => exit?.Invoke();
        }

    }
}
