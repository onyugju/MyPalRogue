using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PalRogue
{
    public class AchievementDef
    {
        public string Id, Name, Desc;
        public AchievementDef(string id, string name, string desc) { Id = id; Name = name; Desc = desc; }
    }

    /// <summary>
    /// 업적 목록과 달성 판정. 달성하면 화면 위쪽에 알림이 뜨고(효과음), 기록(PlayerSave)에 남는다.
    /// 전투 화면이 일이 생길 때마다 Check… 를 부른다.
    /// </summary>
    public static class Achievements
    {
        public static readonly List<AchievementDef> All = new List<AchievementDef>
        {
            new("wave10",   "첫 걸음",          "웨이브 10에 도달한다"),
            new("wave25",   "초원을 넘어서",    "웨이브 25에 도달한다"),
            new("wave50",   "베테랑 테이머",    "웨이브 50에 도달한다"),
            new("wave100",  "백 번의 전투",     "웨이브 100에 도달한다"),
            new("wave150",  "공허의 문턱",      "웨이브 150에 도달한다"),
            new("clear",    "팰로그 정복",      "웨이브 190의 제로버스를 쓰러뜨리고 모험에 성공한다"),
            new("catch1",   "첫 동료",          "팰을 처음으로 잡는다"),
            new("catch10",  "팰 수집가",        "팰을 모두 10마리 잡는다"),
            new("catch50",  "팰 박사",          "팰을 모두 50마리 잡는다"),
            new("species20","도감 채우기",      "서로 다른 팰을 20종 잡는다"),
            new("species40","살아 있는 도감",   "서로 다른 팰을 40종 잡는다"),
            new("boss1",    "보스 사냥꾼",      "보스를 처음으로 쓰러뜨린다"),
            new("boss10",   "보스 킬러",        "보스를 모두 10번 쓰러뜨린다"),
            new("legend1",  "왕을 넘어서",      "속성의 왕(전설 팰)을 쓰러뜨리거나 잡는다"),
            new("legendcatch","전설의 동료",    "속성의 왕(전설 팰)을 잡는다"),
            new("lv50",     "든든한 파트너",    "팰 한 마리를 Lv.50까지 키운다"),
            new("lv100",    "최강의 팰",        "팰 한 마리를 Lv.100까지 키운다"),
            new("stars4",   "완전 응축",        "팰 한 마리의 별을 4개까지 모은다"),
            new("awaken",   "숨겨진 힘",        "내 팰이 전투 중에 각성한다"),
            new("fullparty","대가족",           "파티를 6마리로 채운다"),
            new("rich",     "부자 테이머",      "돈을 10,000G 이상 모은다"),
        };

        /// <summary>이번 모험에서 새로 달성한 업적 (결과 화면)</summary>
        public static readonly List<AchievementDef> ThisRun = new List<AchievementDef>();

        public static AchievementDef Get(string id) => All.FirstOrDefault(a => a.Id == id);

        static void Try(string id)
        {
            if (!PlayerSave.UnlockAchievement(id)) return;
            var def = Get(id);
            if (def == null) return;
            ThisRun.Add(def);
            AchievementToast.Show(def);
        }

        public static void StartRun() => ThisRun.Clear();

        public static void CheckWave(int wave)
        {
            if (wave >= 10) Try("wave10");
            if (wave >= 25) Try("wave25");
            if (wave >= 50) Try("wave50");
            if (wave >= 100) Try("wave100");
            if (wave >= 150) Try("wave150");
        }

        public static void CheckCatch(bool legend)
        {
            Try("catch1");
            if (PlayerSave.TotalCaught >= 10) Try("catch10");
            if (PlayerSave.TotalCaught >= 50) Try("catch50");
            if (PlayerSave.DistinctCaught >= 20) Try("species20");
            if (PlayerSave.DistinctCaught >= 40) Try("species40");
            if (legend) { Try("legendcatch"); Try("legend1"); }
        }

        public static void CheckBoss(bool legend)
        {
            Try("boss1");
            if (PlayerSave.BossesDefeated >= 10) Try("boss10");
            if (legend) Try("legend1");
        }

        public static void CheckLevel(int level)
        {
            if (level >= 50) Try("lv50");
            if (level >= 100) Try("lv100");
        }

        public static void CheckStars(int stars) { if (stars >= GameConfig.MaxStars) Try("stars4"); }
        public static void CheckMoney(int money) { if (money >= 10000) Try("rich"); }
        public static void CheckParty(int count) { if (count >= GameConfig.MaxPartySize) Try("fullparty"); }
        public static void Awakened() => Try("awaken");
        public static void Cleared() => Try("clear");
    }

    /// <summary>업적 달성 알림: 화면 위 가운데로 내려왔다가 올라간다. 여러 개면 차례로</summary>
    public class AchievementToast : MonoBehaviour
    {
        const int Order = 460;
        const float W = 170, H = 26, Hold = 2.4f;
        static AchievementToast instance;
        readonly Queue<AchievementDef> queue = new Queue<AchievementDef>();
        RectTransform box;
        TMP_Text title, desc;
        bool running;

        public static TMP_FontAsset Font;

        public static void Show(AchievementDef def)
        {
            if (instance == null)
            {
                var go = new GameObject("AchievementToast", typeof(RectTransform));
                instance = go.AddComponent<AchievementToast>();
                instance.Build();
            }
            instance.queue.Enqueue(def);
            if (!instance.running) instance.StartCoroutine(instance.Play());
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Order;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320f, 180f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = new GameObject("Layout", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(320f, 180f);
            box = UiKit.Frame(root, "Toast", (320 - W) / 2, -H - 4, W, H, UiKit.Gold, UiKit.FrameFill, out var inner);
            title = UiKit.Label(inner, Font, "Title", 6, 1, W - 12, 11, 8, TextAlignmentOptions.MidlineLeft, UiKit.Gold);
            desc = UiKit.Label(inner, Font, "Desc", 6, 11, W - 12, 11, 8, TextAlignmentOptions.MidlineLeft, UiKit.White);
            box.gameObject.SetActive(false);
        }

        IEnumerator Play()
        {
            running = true;
            while (queue.Count > 0)
            {
                var def = queue.Dequeue();
                title.text = "업적 달성!  " + def.Name;
                desc.text = def.Desc;
                box.gameObject.SetActive(true);
                GameAudio.Se("achv");
                yield return Slide(-H - 4, 3, 0.3f);
                for (float t = 0; t < Hold; t += Time.unscaledDeltaTime) yield return null;
                yield return Slide(3, -H - 4, 0.3f);
                box.gameObject.SetActive(false);
            }
            running = false;
        }

        IEnumerator Slide(float fromY, float toY, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds; k = k * k * (3 - 2 * k);
                box.anchoredPosition = new Vector2(box.anchoredPosition.x, -Mathf.Lerp(fromY, toY, k));
                yield return null;
            }
            box.anchoredPosition = new Vector2(box.anchoredPosition.x, -toY);
        }
    }
}
