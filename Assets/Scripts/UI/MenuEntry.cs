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

        public void OnSelect(BaseEventData eventData) => Selected?.Invoke(this);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnCancel(BaseEventData eventData) => Cancelled?.Invoke();
    }
}
