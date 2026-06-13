using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InventoryDragController
{
    private readonly Image dragGhost;
    private readonly float cellSize;
    private readonly float ghostScale;

    private readonly System.Func<InventoryItemInstance> getDraggedItem;
    private readonly System.Action<InventoryItemInstance, bool> setDraggingVisualForItem;
    private readonly System.Action clearInventoryPreview;
    private readonly System.Action clearBoxPreview;
    private readonly System.Action refreshOccupiedHighlights;

    private Vector3 pointerOffset;

    public Vector3 PointerOffset => pointerOffset;

    public InventoryDragController(
        Image dragGhost,
        float cellSize,
        float ghostScale,
        System.Func<InventoryItemInstance> getDraggedItem,
        System.Action<InventoryItemInstance, bool> setDraggingVisualForItem,
        System.Action clearInventoryPreview,
        System.Action clearBoxPreview,
        System.Action refreshOccupiedHighlights)
    {
        this.dragGhost = dragGhost;
        this.cellSize = cellSize;
        this.ghostScale = ghostScale;
        this.getDraggedItem = getDraggedItem;
        this.setDraggingVisualForItem = setDraggingVisualForItem;
        this.clearInventoryPreview = clearInventoryPreview;
        this.clearBoxPreview = clearBoxPreview;
        this.refreshOccupiedHighlights = refreshOccupiedHighlights;
    }

    public void UpdateGhostPosition()
    {
        if (dragGhost == null || !dragGhost.gameObject.activeSelf)
            return;

        dragGhost.rectTransform.position = Input.mousePosition + pointerOffset;
    }

    public void ShowGhost(InventoryItemInstance item, InventorySlot sourceSlot)
    {
        if (item == null || item.data == null || sourceSlot == null || dragGhost == null)
            return;

        dragGhost.sprite = item.data.icon;
        dragGhost.color = Color.white;

        if (item.hasBankCardMeta && BankSystem.Instance != null)
            dragGhost.color = BankSystem.Instance.GetVariantColor(item.bankCard.colorVariant);

        int width = GetItemWidth(item);
        int height = GetItemHeight(item);

        RectTransform ghostRect = dragGhost.rectTransform;

        float cellW = cellSize * ghostScale;
        float cellH = cellSize * ghostScale;

        float w = cellW * width;
        float h = cellH * height;

        ghostRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        ghostRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
        ghostRect.localScale = Vector3.one;

        RectTransform sourceRect = null;

        if (sourceSlot.iconImage != null)
            sourceRect = sourceSlot.iconImage.rectTransform;

        if (sourceRect == null)
            sourceRect = sourceSlot.GetComponent<RectTransform>();

        pointerOffset = sourceRect != null
            ? sourceRect.position - Input.mousePosition
            : Vector3.zero;

        float localMouseXFromLeft = (w * 0.5f) - pointerOffset.x;
        float localMouseYFromTop = (h * 0.5f) + pointerOffset.y;

        InventoryUI.SetDraggedGrabCellOffset(
            Mathf.Clamp(
                Mathf.FloorToInt(localMouseXFromLeft / Mathf.Max(1f, cellW)),
                0,
                width - 1
            ),
            Mathf.Clamp(
                Mathf.FloorToInt(localMouseYFromTop / Mathf.Max(1f, cellH)),
                0,
                height - 1
            )
        );

        ghostRect.position = Input.mousePosition + pointerOffset;

        dragGhost.preserveAspect = false;
        dragGhost.raycastTarget = false;

        dragGhost.transform.SetAsLastSibling();
        dragGhost.gameObject.SetActive(true);

        InventoryUI.SetInventoryItemDragging(true);

        setDraggingVisualForItem?.Invoke(item, true);
        refreshOccupiedHighlights?.Invoke();
        clearInventoryPreview?.Invoke();
    }

    public void HideGhost()
    {
        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);
    }

    public void ResetPointerOffset()
    {
        pointerOffset = Vector3.zero;
    }

    public void HandleRotationInput()
    {
        InventoryItemInstance item = getDraggedItem?.Invoke();

        if (item == null || item.data == null)
            return;

        if (!item.CanRotate)
            return;

        if (!RotatePressedThisFrame())
            return;

        item.ToggleRotation();

        RebuildGhostShapeAfterRotation(item);

        clearInventoryPreview?.Invoke();
        clearBoxPreview?.Invoke();
    }

    private bool RotatePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    private void RebuildGhostShapeAfterRotation(InventoryItemInstance item)
    {
        if (dragGhost == null || item == null || item.data == null)
            return;

        int width = GetItemWidth(item);
        int height = GetItemHeight(item);

        float cellW = cellSize * ghostScale;
        float cellH = cellSize * ghostScale;

        float w = cellW * width;
        float h = cellH * height;

        RectTransform ghostRect = dragGhost.rectTransform;

        ghostRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        ghostRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
        ghostRect.localScale = Vector3.one;

        int xOffset = Mathf.Clamp(InventoryUI.DraggedGrabCellOffset, 0, width - 1);
        int yOffset = Mathf.Clamp(InventoryUI.DraggedGrabCellOffsetY, 0, height - 1);

        InventoryUI.SetDraggedGrabCellOffset(xOffset, yOffset);

        pointerOffset = new Vector3(
            (w * 0.5f) - ((xOffset + 0.5f) * cellW),
            (-h * 0.5f) + ((yOffset + 0.5f) * cellH),
            0f
        );

        ghostRect.position = Input.mousePosition + pointerOffset;
    }

    private int GetItemWidth(InventoryItemInstance item)
    {
        if (item == null || item.data == null)
            return 1;

        return Mathf.Max(1, item.WidthSlots);
    }

    private int GetItemHeight(InventoryItemInstance item)
    {
        if (item == null || item.data == null)
            return 1;

        return Mathf.Max(1, item.HeightSlots);
    }
}