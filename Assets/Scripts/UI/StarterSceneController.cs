using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// StarterScene: 팰 선택 → 저장 슬롯 고르기 → 본게임(GameScene).
    /// 타이틀에서 "불러오기"로 들어오면 저장 슬롯 고르기만 나온다.
    ///  - 팰 선택에서 Esc → 타이틀로
    ///  - 저장 슬롯에서 Esc → (새 게임) 팰 선택으로 / (불러오기) 타이틀로
    /// 화면은 코드로 만든다 (StarterSelectScreen, SaveSlotScreen). 씬은 메뉴 PalRogue/35가 만든다.
    /// </summary>
    public class StarterSceneController : MonoBehaviour
    {
        [Tooltip("글꼴 (전투 화면과 같은 한글 픽셀 글꼴)")]
        public TMP_FontAsset font;
        [Tooltip("선택 화면에 나오는 모든 팰. 에디터에서 플레이하면 Assets의 모든 PalData로 자동으로 채운다")]
        public List<PalData> allPals = new List<PalData>();

        IEnumerator Start()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            allPals = UnityEditor.AssetDatabase.FindAssets("t:PalData")
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<PalData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).OrderBy(p => p.name).ToList();
#endif
            var starter = StarterSelectScreen.Create(font);
            starter.AllowBack = true;
            var slots = SaveSlotScreen.Create(font);
            bool load = GameSession.OpenForLoad;

            while (true)
            {
                if (!load)
                {
                    yield return starter.Choose(allPals);
                    starter.Hide();
                    if (starter.BackRequested) { GameSession.Go(GameSession.TitleScene, font); yield break; }
                }

                yield return slots.Choose(!load, allPals);
                slots.Hide();
                if (slots.Result < 0)
                {
                    if (load) { GameSession.Go(GameSession.TitleScene, font); yield break; }
                    continue;                                   // 새 게임: 팰 선택으로 돌아간다
                }

                int slot = slots.Result;
                GameSession.Slot = slot;
                if (load)
                {
                    GameSession.Loaded = SaveSlots.Load(slot);
                    GameSession.NewParty = null;
                }
                else
                {
                    SaveSlots.Delete(slot);                     // 예전 모험을 지우고 새로 (첫 웨이브에서 저장된다)
                    GameSession.Loaded = null;
                    GameSession.NewParty = starter.Result;
                }

                if (!GameSession.CanLoad(GameSession.GameScene))
                {
                    Debug.LogError("[씬] GameScene이 빌드 설정에 없습니다. 메뉴 PalRogue/35를 실행해 주세요.");
                    yield break;
                }
                GameSession.Go(GameSession.GameScene, font);
                yield break;
            }
        }
    }
}
