using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragWindowByHeader : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Refs")]
    [SerializeField] private RectTransform windowRoot;
    [SerializeField] private Canvas canvas;

    [Header("Settings")]
    [SerializeField] private bool clampToScreen = true;

    private bool dragging;

    private readonly Vector3[] windowCorners = new Vector3[4];
    private readonly Vector3[] canvasCorners = new Vector3[4];

    private void Awake()
    {
        if (windowRoot == null)
            windowRoot = transform.parent as RectTransform;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
            return;

        if (windowRoot == null || canvas == null)
            return;

        float scale = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;

        windowRoot.anchoredPosition += eventData.delta / scale;

        if (clampToScreen)
            ClampToCanvasByCorners();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        dragging = false;
    }

    private void ClampToCanvasByCorners()
    {
        RectTransform canvasRect = canvas.transform as RectTransform;

        if (canvasRect == null || windowRoot == null)
            return;

        windowRoot.GetWorldCorners(windowCorners);
        canvasRect.GetWorldCorners(canvasCorners);

        float moveX = 0f;
        float moveY = 0f;

        float windowLeft = windowCorners[0].x;
        float windowBottom = windowCorners[0].y;
        float windowRight = windowCorners[2].x;
        float windowTop = windowCorners[2].y;

        float canvasLeft = canvasCorners[0].x;
        float canvasBottom = canvasCorners[0].y;
        float canvasRight = canvasCorners[2].x;
        float canvasTop = canvasCorners[2].y;

        if (windowLeft < canvasLeft)
            moveX = canvasLeft - windowLeft;
        else if (windowRight > canvasRight)
            moveX = canvasRight - windowRight;

        if (windowBottom < canvasBottom)
            moveY = canvasBottom - windowBottom;
        else if (windowTop > canvasTop)
            moveY = canvasTop - windowTop;

        if (Mathf.Abs(moveX) < 0.001f && Mathf.Abs(moveY) < 0.001f)
            return;

        Vector2 correction = new Vector2(moveX, moveY);

        float scale = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
        windowRoot.anchoredPosition += correction / scale;
    }
}