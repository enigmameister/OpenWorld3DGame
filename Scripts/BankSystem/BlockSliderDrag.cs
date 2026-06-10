using UnityEngine;
using UnityEngine.EventSystems;

public class BlockSliderDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public void OnBeginDrag(PointerEventData eventData) => eventData.Use();
    public void OnDrag(PointerEventData eventData) => eventData.Use();
}
