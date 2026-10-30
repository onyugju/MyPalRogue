using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    /// <summary>
    /// 모험 결과 화면 (게임 오버 / 클리어 뒤). 도달 웨이브, 지역, 플레이 시간, 잡은 팰·쓰러뜨린 보스 수,
    /// 마지막 파티, 이번에 새로 얻은 업적, 최고 기록. Enter·클릭으로 닫는다.
    /// </summary>
    public class RunResultScreen : MonoBehaviour
    {
        const int Order = 420;
        TMP_FontAsset font;
        TMP_Text title, lines, achv, hint;
        Image[] icons;
        TMP_Text[] levels;
        bool done;
        float openedAt;

        public static RunResultScreen Create(TMP_FontAsset font)
        {
            var go = new GameObject("RunResultScreen", typeof(RectTransform));
            var s = go.AddComponent<RunResultScreen>();
            s.font = font;
            var root = UiKit.Canvas(go, Order, new Color(0, 0, 0, 0.8f));
            UiKit.Frame(root, "Window", 24, 8, 272, 164, UiKit.FrameRed, UiKit.FrameFill, out var inner);
            s.title = UiKit.Label(inner, font, "Title", 0, 4, 268, 18, 15, TextAlignmentOptions.Center, UiKit.Gold);
            s.lines = UiKit.Label(inner, font, "Lines", 12, 26, 244, 50, 9, TextAlignmentOptions.TopLeft, UiKit.White);
            s.lines.lineSpacing = -4f;
            s.icons = new Image[GameConfig.MaxPartySize];
            s.levels = new TMP_Text[GameConfig.MaxPartySize];
            for (int i = 0; i < s.icons.Length; i++)
            {
                var icon = UiKit.Box(inner, "Icon" + i, 26 + i * 38, 78, 26, 22).gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                s.icons[i] = icon;
                s.levels[i] = UiKit.Label(inner, font, "Lv" + i, 20 + i * 38, 100, 38, 9, 7, TextAlignmentOptions.Center, UiKit.White);
            }
            s.achv = UiKit.Label(inner, font, "Achv", 12, 112, 244, 34, 8, TextAlignmentOptions.TopLeft, UiKit.White);
            s.achv.lineSpacing = -6f;
            s.hint = UiKit.Label(inner, font, "Hint", 0, 146, 268, 12, 8, TextAlignmentOptions.Center, UiKit.Soft);
            UiKit.Clickable(inner.gameObject, () => { }, () => { if (Time.unscaledTime - s.openedAt > 0.4f) s.done = true; });
            go.SetActive(false);
            return s;
        }

        public IEnumerator Show(Run run, bool cleared, double playSeconds, bool newBest, string nextText)
        {
            title.text = cleared ? "모험 성공!" : "모험 끝";
            title.color = cleared ? UiKit.Gold : UiKit.Hex(0xff8080);
            var t = System.TimeSpan.FromSeconds(System.Math.Max(0, playSeconds));
            int wave = Mathf.Min(run.Wave, GameConfig.FinalWave);
            lines.text =
                $"도달 웨이브  <color=#f8d030>{wave}</color>  ({run.AreaName})" + (newBest ? "   <color=#78c850>최고 기록!</color>" : $"   최고 {Mathf.Max(PlayerSave.BestWave, wave)}") + "\n" +
                $"플레이 시간  {t.Days:00}:{t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}\n" +
                $"잡은 팰  {run.CaughtCount}마리     쓰러뜨린 보스  {run.BossesDefeated}마리";
            for (int i = 0; i < icons.Length; i++)
            {
                var p = i < run.Party.Count ? run.Party[i] : null;
                icons[i].enabled = p != null && p.Data.frontSprite != null;
                if (icons[i].enabled)
                {
                    icons[i].sprite = p.Data.frontSprite;
                    icons[i].color = p.IsFainted ? new Color(0.4f, 0.4f, 0.4f) : Color.white;
                }
                levels[i].text = p != null ? $"Lv.{p.Level}" : "";
            }
            var got = Achievements.ThisRun;
            achv.text = got.Count == 0 ? "<color=#9a9aae>이번 모험에서 새로 얻은 업적이 없어요.</color>"
                      : "<color=#f8d030>새 업적</color>  " + string.Join(",  ", got.Take(6).Select(a => a.Name)) + (got.Count > 6 ? $" 외 {got.Count - 6}개" : "");
            hint.text = nextText;
            gameObject.SetActive(true);
            GameAudio.Se(cleared ? "level_up" : "menu_open");
            openedAt = Time.unscaledTime;
            done = false;
            while (!done)
            {
                if (Time.unscaledTime - openedAt > 0.4f && (MenuKeys.Confirm || MenuKeys.Cancel)) done = true;
                yield return null;
            }
            GameAudio.Se("select");
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 기록 화면 (타이틀 → 기록): [플레이 이력] 최근 모험 30개 / [업적] 전체 목록과 달성 여부.
    /// 좌우 방향키 탭 바꾸기, 위아래 스크롤, Esc 닫기.
    /// </summary>
    public class RecordsScreen : MonoBehaviour
    {
        const int Order = 150;
        const int Rows = 5;
        const float RowH = 26f, Top = 22f;

        TMP_FontAsset font;
        Dictionary<string, PalData> pals = new Dictionary<string, PalData>();
        TMP_Text[] tabs;
        TMP_Text summary;
        readonly List<(RectTransform rt, TMP_Text a, TMP_Text b, Image[] icons)> rows = new List<(RectTransform, TMP_Text, TMP_Text, Image[])>();
        int tab, scroll;
        bool open;
        float openedAt;
        public bool IsOpen => open;
        public float ClosedAt { get; private set; } = -10f;

        public static RecordsScreen Create(TMP_FontAsset font, IEnumerable<PalData> allPals)
        {
            var go = new GameObject("RecordsScreen", typeof(RectTransform));
            var s = go.AddComponent<RecordsScreen>();
            s.font = font;
            s.pals = (allPals ?? Enumerable.Empty<PalData>()).Where(p => p != null).GroupBy(p => p.name).ToDictionary(g => g.Key, g => g.First());
            var root = UiKit.Canvas(go, Order, UiKit.Hex(0x006a62));
            s.tabs = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                int t = i;
                s.tabs[i] = UiKit.Label(root, font, "Tab" + i, 10 + i * 70, 3, 64, 16, 11, TextAlignmentOptions.MidlineLeft, UiKit.White);
                s.tabs[i].text = i == 0 ? "플레이 이력" : "업적";
                UiKit.Clickable(s.tabs[i].gameObject, () => { }, () => { s.tab = t; s.scroll = 0; s.Refresh(); });
            }
            s.summary = UiKit.Label(root, font, "Summary", 150, 3, 164, 16, 8, TextAlignmentOptions.MidlineRight, UiKit.White);
            for (int i = 0; i < Rows; i++)
            {
                var rt = UiKit.Frame(root, "Row" + i, 8, Top + i * (RowH + 2), 304, RowH, UiKit.FrameRed, UiKit.FrameFill, out var inner);
                var a = UiKit.Label(inner, font, "A", 6, 1, 200, 12, 9, TextAlignmentOptions.MidlineLeft, UiKit.White);
                var b = UiKit.Label(inner, font, "B", 6, 12, 200, 12, 8, TextAlignmentOptions.MidlineLeft, UiKit.Soft);
                var icons = new Image[GameConfig.MaxPartySize];
                for (int j = 0; j < icons.Length; j++)
                {
                    icons[j] = UiKit.Box(inner, "Icon" + j, 178 + j * 20, 2, 18, 20).gameObject.AddComponent<Image>();
                    icons[j].preserveAspect = true;
                    icons[j].raycastTarget = false;
                }
                s.rows.Add((rt, a, b, icons));
            }
            var hint = UiKit.Label(root, font, "Hint", 8, 168, 304, 10, 7, TextAlignmentOptions.MidlineLeft, UiKit.White);
            hint.text = "좌우 방향키: 탭 바꾸기   위아래: 넘기기   Esc: 닫기";
            go.SetActive(false);
            return s;
        }

        public IEnumerator Open()
        {
            gameObject.SetActive(true);
            open = true;
            tab = 0; scroll = 0;
            openedAt = Time.unscaledTime;
            GameAudio.Se("menu_open");
            Refresh();
            while (open) yield return null;
            ClosedAt = Time.unscaledTime;
            gameObject.SetActive(false);
        }

        int Count => tab == 0 ? PlayerSave.History.Count : Achievements.All.Count;

        void Update()
        {
            if (!open || Time.unscaledTime - openedAt < 0.15f) return;
            int h = MenuKeys.Horizontal;
            if (h != 0) { tab = (tab + h + 2) % 2; scroll = 0; GameAudio.Se("select"); Refresh(); }
            int v = MenuKeys.Vertical;
            if (v != 0)
            {
                int max = Mathf.Max(0, Count - Rows);
                int next = Mathf.Clamp(scroll + v, 0, max);
                if (next != scroll) { scroll = next; GameAudio.Se("select"); Refresh(); }
            }
            if (MenuKeys.Cancel || MenuKeys.Confirm) { GameAudio.Se("select"); open = false; }
        }

        void Refresh()
        {
            for (int i = 0; i < tabs.Length; i++) tabs[i].color = i == tab ? UiKit.Gold : UiKit.Soft;
            if (tab == 0)
            {
                var h = PlayerSave.History;
                summary.text = $"최고 기록 웨이브 {PlayerSave.BestWave}   모험 {h.Count}번";
                for (int i = 0; i < rows.Count; i++)
                {
                    var (rt, a, b, icons) = rows[i];
                    int idx = scroll + i;
                    rt.gameObject.SetActive(idx < h.Count || (i == 0 && h.Count == 0));
                    foreach (var ic in icons) ic.enabled = false;
                    if (h.Count == 0) { a.text = "아직 끝난 모험이 없어요."; b.text = ""; continue; }
                    if (idx >= h.Count) continue;
                    var r = h[idx];
                    var t = System.TimeSpan.FromSeconds(System.Math.Max(0, r.playSeconds));
                    a.text = $"모험 {r.runNumber}   " + (r.cleared ? "<color=#f8d030>성공</color>" : "실패") + $"   웨이브 {r.wave} ({r.area})";
                    b.text = $"{r.EndedAt:yyyy.MM.dd HH:mm}   {t.Days:00}:{t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}   잡은 팰 {r.caught}";
                    for (int j = 0; j < icons.Length && j < r.party.Count; j++)
                    {
                        if (!pals.TryGetValue(r.party[j].pal, out var d) || d.frontSprite == null) continue;
                        icons[j].enabled = true;
                        icons[j].sprite = d.frontSprite;
                        icons[j].color = r.party[j].fainted ? new Color(0.4f, 0.4f, 0.4f) : Color.white;
                    }
                }
            }
            else
            {
                var all = Achievements.All;
                summary.text = $"달성 {all.Count(x => PlayerSave.HasAchievement(x.Id))} / {all.Count}";
                for (int i = 0; i < rows.Count; i++)
                {
                    var (rt, a, b, icons) = rows[i];
                    foreach (var ic in icons) ic.enabled = false;
                    int idx = scroll + i;
                    rt.gameObject.SetActive(idx < all.Count);
                    if (idx >= all.Count) continue;
                    var def = all[idx];
                    bool got = PlayerSave.HasAchievement(def.Id);
                    a.text = got ? $"<color=#f8d030>{def.Name}  (달성)</color>" : $"<color=#9a9aae>{def.Name}</color>";
                    b.text = def.Desc;
                }
            }
        }
    }
}
