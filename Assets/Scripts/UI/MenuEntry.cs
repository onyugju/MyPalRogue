using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PalRogue
{
    /// <summary>
    /// 메뉴 항목 하나에 붙는 선택 감지기. 키보드로 선택되거나 마우스를 올리면 Selected가 울려서
    /// 화면이 ▶ 커서를 그 위치로 옮긴다. 취소 키(Esc 등)는 Cancelled로 전달된다.
    /// </summary>
    public class MenuEntry : MonoBehaviour, ISelectHandler, IPointerEnterHandler, ICancelHandler
    {
        public event Action<MenuEntry> Selected;
        public event Action Cancelled;

        /// <summary>코드로 항목을 고를 때 잠깐 켜서 커서 소리를 끈다</summary>
        public static bool Quiet;

        public void OnSelect(BaseEventData eventData)
        {
            if (!Quiet) GameAudio.Se("select");                 // 커서가 움직이는 소리
            Selected?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var selectable = GetComponent<UnityEngine.UI.Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return;   // 잠긴 메뉴는 무시
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnCancel(BaseEventData eventData) => Cancelled?.Invoke();
    }
}
