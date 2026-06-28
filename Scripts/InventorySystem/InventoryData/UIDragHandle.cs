using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Tooltip("Przeciπgany RectTransform (np. DropCashDialog). Jeúli puste, weümie parent.")]
    public RectTransform target;

    private Vector2 _pointerOffset;

    void Awake()
    {
        if (!target)
            target = transform.parent as RectTransform;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!target) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            target, eventData.position, eventData.pressEventCamera, out _pointerOffset
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!target) return;

        RectTransform parentRect = target.parent as RectTransform;
        if (!parentRect) return;

        Vector2 localPointerPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, eventData.pressEventCamera, out localPointerPos))
        {
            target.anchoredPosition = localPointerPos - _pointerOffset;
        }
    }
}
