using System.Collections.Generic;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 전투 배경(지역) 바꾸기. 포켓로그 arenas 그림을 쓴다.
    ///  - {biome}_bg  : 배경 (320x180)
    ///  - {biome}_a   : 내 발판 (320x132, 화면 위쪽에 맞춰 그려진 그림)
    ///  - {biome}_b   : 상대 발판
    ///  - {biome}_b_1~3 : 상대 발판 위 장식 (바위·선인장 등). 발판과 같은 크기일 때만 쓴다 (다른 크기는 움직이는 그림 묶음)
    /// 그림은 Resources/Arenas 에 있고, PPU 18(월드 1유닛 = 화면 18픽셀)로 만든다.
    /// 장식은 상대 발판의 자식으로 붙여서, 발판이 미끄러져 들어올 때 함께 움직인다.
    /// </summary>
    public static class AreaScenery
    {
        public const string Folder = "Arenas";
        public const float PixelsPerUnit = 18f;
        public const int MaxProps = 3;

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>지금 화면에 깔린 배경 이름 (같으면 다시 바꾸지 않는다)</summary>
        public static string Current { get; private set; }

        /// <summary>Resources/Arenas/{name} 을 스프라이트로 (없으면 null)</summary>
        public static Sprite Load(string name)
        {
            if (cache.TryGetValue(name, out var cached) && cached != null) return cached;
            Sprite sprite = null;
            var tex = Resources.Load<Texture2D>($"{Folder}/{name}");
            if (tex != null)
            {
                // 임포트 설정과 관계없이 항상 같은 크기·같은 중심으로 만든다 (도트가 번지지 않게 Point)
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = name;
            }
            cache[name] = sprite;
            return sprite;
        }

        /// <summary>이 배경의 그림(배경·발판 2개)이 모두 있는지</summary>
        public static bool Has(string biome) => Load(biome + "_bg") != null && Load(biome + "_a") != null && Load(biome + "_b") != null;

        /// <summary>
        /// field(BattleField) 아래의 Background / PlayerBase / EnemyBase 그림을 바꾼다.
        /// 그림이 없으면 바꾸지 않고 false.
        /// </summary>
        public static bool Apply(Transform field, string biome)
        {
            if (field == null || string.IsNullOrEmpty(biome)) return false;
            if (!Has(biome))
            {
                Debug.LogWarning($"[PalRogue] 지역 배경 그림이 없음: Resources/{Folder}/{biome}_bg / _a / _b");
                return false;
            }

            SetSprite(field.Find("Background"), Load(biome + "_bg"));
            SetSprite(field.Find("PlayerBase"), Load(biome + "_a"));
            var enemyBase = field.Find("EnemyBase");
            var baseSprite = Load(biome + "_b");
            SetSprite(enemyBase, baseSprite);
            if (enemyBase != null) SetProps(enemyBase, biome, baseSprite);
            Current = biome;
            return true;
        }

        static void SetSprite(Transform t, Sprite sprite)
        {
            if (t == null || sprite == null) return;
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = sprite;
        }

        static void SetProps(Transform enemyBase, string biome, Sprite baseSprite)
        {
            var baseRenderer = enemyBase.GetComponent<SpriteRenderer>();
            for (int i = 1; i <= MaxProps; i++)
            {
                var prop = Load($"{biome}_b_{i}");
                bool usable = prop != null && baseSprite != null && prop.rect.size == baseSprite.rect.size;

                var t = enemyBase.Find("Prop" + i);
                if (t == null)
                {
                    if (!usable) continue;
                    var go = new GameObject("Prop" + i);
                    t = go.transform;
                    t.SetParent(enemyBase, false);
                    go.AddComponent<SpriteRenderer>();
                }
                t.localPosition = new Vector3(0f, 0f, -0.01f * i);   // 발판보다 살짝 앞
                var sr = t.GetComponent<SpriteRenderer>();
                if (baseRenderer != null)
                {
                    sr.sortingLayerID = baseRenderer.sortingLayerID;
                    sr.sortingOrder = baseRenderer.sortingOrder;      // 팰보다는 뒤
                }
                sr.sprite = usable ? prop : null;
                sr.enabled = usable;
            }
        }
    }
}
