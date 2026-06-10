using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableCodon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Transform originalParent;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (originalParent == null)
            originalParent = transform.parent;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = false;
        transform.SetParent(transform.root, true); // zachowaj pozycję świata
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.position += (Vector3)eventData.delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // Jeśli nie trafił w slot, wróć do miejsca startu
        if (transform.parent == transform.root)
        {
            transform.SetParent(originalParent);
            rectTransform.localPosition = Vector3.zero;
        }
    }
}
