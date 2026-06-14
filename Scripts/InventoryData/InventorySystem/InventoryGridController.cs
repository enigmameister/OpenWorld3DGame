using System.Collections.Generic;
using UnityEngine;

public class InventoryGridController
{
    private readonly List<InventorySlot> slots;
    private readonly System.Func<InventoryItemInstance> getDraggedItem;
    private readonly System.Func<bool> getIsDragging;

    private readonly List<InventorySlot> placementPreviewSlots = new();

    private int slotsPerRow;
    private Color placementValidColor;
    private Color placementInvalidColor;

    public InventorySlot LastPreviewStartSlot { get; set; }

    public IReadOnlyList<InventorySlot> Slots => slots;
    public int SlotsPerRow => slotsPerRow;

    public InventoryGridController(
        List<InventorySlot> slots,
        int slotsPerRow,
        Color placementValidColor,
        Color placementInvalidColor,
        System.Func<InventoryItemInstance> getDraggedItem,
        System.Func<bool> getIsDragging)
    {
        this.slots = slots;
        this.slotsPerRow = Mathf.Max(1, slotsPerRow);
        this.placementValidColor = placementValidColor;
        this.placementInvalidColor = placementInvalidColor;
        this.getDraggedItem = getDraggedItem;
        this.getIsDragging = getIsDragging;
    }

    public void SetSlotsPerRow(int value)
    {
        slotsPerRow = Mathf.Max(1, value);
    }

    public void SetPreviewColors(Color valid, Color invalid)
    {
        placementValidColor = valid;
        placementInvalidColor = invalid;
    }

    public int GetItemWidth(InventoryItemInstance item)
    {
        if (item == null || item.data == null)
            return 1;

        return Mathf.Max(1, item.WidthSlots);
    }

    public int GetItemHeight(InventoryItemInstance item)
    {
        if (item == null || item.data == null)
            return 1;

        return Mathf.Max(1, item.HeightSlots);
    }

    public bool CanFitItem(int startIndex, int size)
    {
        return CanFitShape(startIndex, Mathf.Max(1, size), 1);
    }

    public bool CanFitShape(int startIndex, int width, int height)
    {
        if (startIndex < 0)
            return false;

        if (width <= 0 || height <= 0)
            return false;

        int startCol = startIndex % slotsPerRow;

        if (startCol + width > slotsPerRow)
            return false;

        int lastIndex = startIndex + (height - 1) * slotsPerRow + (width - 1);

        if (lastIndex >= slots.Count)
            return false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = startIndex + y * slotsPerRow + x;

                if (index < 0 || index >= slots.Count)
                    return false;

                InventorySlot slot = slots[index];

                if (slot == null)
                    return false;

                if (slot.CompareTag("LockedSlot"))
                    return false;

                if (slot.isOccupied)
                    return false;
            }
        }

        return true;
    }

    public bool CanPlaceDraggedItemAt(
        int startIndex,
        InventoryItemInstance item,
        IInventorySlotOwner currentOwner,
        IInventorySlotOwner dragSourceOwner)
    {
        if (item == null || item.data == null)
            return false;

        int width = GetItemWidth(item);
        int height = GetItemHeight(item);

        if (startIndex < 0)
            return false;

        int startCol = startIndex % slotsPerRow;

        if (startCol + width > slotsPerRow)
            return false;

        int lastIndex = startIndex + (height - 1) * slotsPerRow + (width - 1);

        if (lastIndex >= slots.Count)
            return false;

        bool movingInsideThisGrid = ReferenceEquals(dragSourceOwner, currentOwner);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = startIndex + y * slotsPerRow + x;

                if (index < 0 || index >= slots.Count)
                    return false;

                InventorySlot slot = slots[index];

                if (slot == null)
                    return false;

                if (slot.CompareTag("LockedSlot"))
                    return false;

                if (slot.isOccupied)
                {
                    bool occupiedBySameDraggedItem =
                        movingInsideThisGrid &&
                        slot.item == item;

                    if (!occupiedBySameDraggedItem)
                        return false;
                }
            }
        }

        return true;
    }

    public bool TryPlaceAt(int startIndex, InventoryItemInstance item)
    {
        if (item == null || item.data == null)
            return false;

        int width = GetItemWidth(item);
        int height = GetItemHeight(item);

        if (!CanFitShape(startIndex, width, height))
            return false;

        PlaceAtUnsafe(startIndex, item);
        return true;
    }

    public void PlaceAtUnsafe(int startIndex, InventoryItemInstance item)
    {
        if (item == null || item.data == null)
            return;

        int width = GetItemWidth(item);
        int height = GetItemHeight(item);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = startIndex + y * slotsPerRow + x;

                if (index < 0 || index >= slots.Count)
                    continue;

                InventorySlot slot = slots[index];

                if (slot == null)
                    continue;

                slot.isOccupied = true;
                slot.item = item;

                if (x == 0 && y == 0)
                {
                    slot.SetItem(item);
                }
                else
                {
                    if (slot.fillImage != null)
                        slot.fillImage.SetActive(false);

                    if (slot.borderImage != null)
                        slot.borderImage.SetActive(false);

                    if (slot.iconImage != null)
                    {
                        slot.iconImage.sprite = null;
                        slot.iconImage.enabled = false;
                        slot.iconImage.color = Color.white;
                    }

                    if (slot.countText != null)
                    {
                        slot.countText.text = "";
                        slot.countText.gameObject.SetActive(false);
                    }

                    slot.transform.localScale = Vector3.one;

                    RectTransform rt = slot.GetComponent<RectTransform>();
                    if (rt != null)
                        rt.pivot = new Vector2(0.5f, 0.5f);

                    slot.SetOccupiedHighlight(true);
                    slot.ClearPlacementPreview();
                }
            }
        }

        RefreshOccupiedHighlights();
    }

    public bool TryPlaceExistingInstance(InventoryItemInstance item)
    {
        if (item == null || item.data == null)
            return false;

        int width = GetItemWidth(item);
        int height = GetItemHeight(item);

        for (int i = 0; i < slots.Count; i++)
        {
            if (CanFitShape(i, width, height))
            {
                PlaceAtUnsafe(i, item);
                return true;
            }
        }

        return false;
    }

    public void ClearSlotVisual(InventorySlot slot)
    {
        if (slot == null)
            return;

        slot.isOccupied = false;
        slot.item = null;

        if (slot.iconImage != null)
        {
            slot.iconImage.sprite = null;
            slot.iconImage.enabled = false;
            slot.iconImage.color = Color.white;
        }

        if (slot.countText != null)
        {
            slot.countText.text = "";
            slot.countText.gameObject.SetActive(false);
        }

        if (slot.fillImage != null)
            slot.fillImage.SetActive(true);

        if (slot.borderImage != null)
            slot.borderImage.SetActive(true);

        slot.transform.localScale = Vector3.one;

        RectTransform rt = slot.GetComponent<RectTransform>();
        if (rt != null)
            rt.pivot = new Vector2(0.5f, 0.5f);

        slot.ClearPlacementPreview();
        slot.ClearOccupiedHighlight();
        slot.UpdateCountDisplay();
    }

    public void ClearAllSlotVisuals()
    {
        for (int i = 0; i < slots.Count; i++)
            ClearSlotVisual(slots[i]);
    }

    public void RemoveVisualOnly(InventoryItemInstance item)
    {
        if (item == null)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];

            if (slot != null && slot.item == item)
                ClearSlotVisual(slot);
        }

        RefreshOccupiedHighlights();
    }

    public void ForceRemoveCompletely(InventoryItemInstance item)
    {
        if (item == null)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];

            if (slot == null || slot.item != item)
                continue;

            ClearSlotVisual(slot);
        }

        RefreshOccupiedHighlights();
    }

    public void RefreshCountDisplay(InventoryItemInstance item)
    {
        if (item == null)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];

            if (slot != null && slot.item == item)
                slot.UpdateCountDisplay();
        }
    }

    public InventorySlot GetSlotForInstance(InventoryItemInstance item)
    {
        if (item == null)
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];

            if (slot != null && slot.item == item)
                return slot;
        }

        return null;
    }

    public void RefreshOccupiedHighlights()
    {
        InventoryItemInstance dragged = getDraggedItem != null ? getDraggedItem.Invoke() : null;
        bool dragging = getIsDragging != null && getIsDragging.Invoke();

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];

            if (slot == null)
                continue;

            bool belongsToDraggedItem =
                dragging &&
                dragged != null &&
                slot.item == dragged;

            if (belongsToDraggedItem)
            {
                slot.ClearOccupiedHighlight();
                continue;
            }

            if (slot.isOccupied && slot.item != null)
                slot.SetOccupiedHighlight(true);
            else
                slot.ClearOccupiedHighlight();
        }
    }

    public void ClearPlacementPreview()
    {
        for (int i = 0; i < placementPreviewSlots.Count; i++)
        {
            if (placementPreviewSlots[i] != null)
                placementPreviewSlots[i].ClearPlacementPreview();
        }

        placementPreviewSlots.Clear();
        LastPreviewStartSlot = null;
    }

    public void PreviewPlacement(
        int startIndex,
        InventoryItemInstance item,
        IInventorySlotOwner currentOwner,
        IInventorySlotOwner dragSourceOwner)
    {
        ClearPlacementPreview();

        if (item == null || item.data == null)
            return;

        int width = GetItemWidth(item);
        int height = GetItemHeight(item);

        if (startIndex < 0 || startIndex >= slots.Count)
            return;

        bool valid = CanPlaceDraggedItemAt(startIndex, item, currentOwner, dragSourceOwner);

        // Je¿eli cel jest zajêty, ale da siê zmergowaæ stack,
        // pokazuj jako poprawny drop, nie czerwony.
        if (!valid && startIndex >= 0 && startIndex < slots.Count)
        {
            InventorySlot targetSlot = slots[startIndex];

            if (targetSlot != null &&
                targetSlot.isOccupied &&
                targetSlot.item != null &&
                InventoryStackService.CanMergeStacks(item, targetSlot.item))
            {
                valid = true;
            }
        }

        Color color = valid ? placementValidColor : placementInvalidColor;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = startIndex + y * slotsPerRow + x;

                if (index < 0 || index >= slots.Count)
                    continue;

                InventorySlot slot = slots[index];

                if (slot == null)
                    continue;

                slot.SetPlacementPreview(true, color);
                placementPreviewSlots.Add(slot);
            }
        }
    }

    public void RebuildSlotVisualsFromCurrentState()
    {
        HashSet<InventoryItemInstance> firstSlots = new HashSet<InventoryItemInstance>();

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];

            if (slot == null)
                continue;

            if (!slot.isOccupied || slot.item == null)
            {
                ClearSlotVisual(slot);
                continue;
            }

            bool isFirstSlotOfItem = firstSlots.Add(slot.item);

            if (isFirstSlotOfItem)
            {
                slot.SetItem(slot.item);
            }
            else
            {
                slot.transform.localScale = Vector3.one;

                RectTransform rt = slot.GetComponent<RectTransform>();
                if (rt != null)
                    rt.pivot = new Vector2(0.5f, 0.5f);

                if (slot.iconImage != null)
                {
                    slot.iconImage.sprite = null;
                    slot.iconImage.enabled = false;
                    slot.iconImage.color = Color.white;
                }

                if (slot.countText != null)
                {
                    slot.countText.text = "";
                    slot.countText.gameObject.SetActive(false);
                }

                if (slot.fillImage != null)
                    slot.fillImage.SetActive(false);

                if (slot.borderImage != null)
                    slot.borderImage.SetActive(false);

                slot.SetOccupiedHighlight(true);
            }

            slot.ClearPlacementPreview();
        }

        RefreshOccupiedHighlights();
    }

    public int CountUsedSlots()
    {
        int used = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];

            if (slot != null && slot.isOccupied)
                used++;
        }

        return used;
    }

    public List<InventoryItemInstance> GetAllInstancesDistinct()
    {
        HashSet<InventoryItemInstance> set = new HashSet<InventoryItemInstance>();
        List<InventoryItemInstance> result = new List<InventoryItemInstance>();

        for (int i = 0; i < slots.Count; i++)
        {
            InventoryItemInstance item = slots[i]?.item;

            if (item == null)
                continue;

            if (set.Add(item))
                result.Add(item);
        }

        return result;
    }

    public InventorySlot GetTopLeftSlotForItem(InventoryItemInstance item)
    {
        if (item == null)
            return null;

        InventorySlot best = null;
        int bestIndex = int.MaxValue;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];

            if (slot == null || slot.item != item)
                continue;

            if (i < bestIndex)
            {
                bestIndex = i;
                best = slot;
            }
        }

        return best;
    }

    public int GetTopLeftIndexForItem(InventoryItemInstance item)
    {
        InventorySlot slot = GetTopLeftSlotForItem(item);
        return slot != null ? slot.slotIndex : -1;
    }

    public void SetDraggingVisualForItem(InventoryItemInstance item, bool dragging)
    {
        if (item == null)
            return;

        if (dragging)
        {
            // Podczas dragowania NIE niszczymy logicznego zajêcia slotów.
            // Pokazujemy je tylko jak normalne puste sloty.
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];

                if (slot == null || slot.item != item)
                    continue;

                ShowDraggedSourceCellAsEmpty(slot);
            }

            RefreshOccupiedHighlights();
            return;
        }

        // Po zakoñczeniu/cancel drag odbuduj pe³ny shape itemu:
        // top-left ma ikonê i skalê, reszta to ukryte komórki pomocnicze.
        RebuildVisualForItem(item);
        RefreshOccupiedHighlights();
    }

    private void ShowDraggedSourceCellAsEmpty(InventorySlot slot)
    {
        if (slot == null)
            return;

        // WA¯NE: zostawiamy slot.isOccupied = true i slot.item = item.
        // Zmieniamy tylko wygl¹d.

        if (slot.iconImage != null)
        {
            slot.iconImage.sprite = null;
            slot.iconImage.enabled = false;
            slot.iconImage.color = Color.white;
        }

        if (slot.countText != null)
        {
            slot.countText.text = "";
            slot.countText.gameObject.SetActive(false);
        }

        // Podczas dragowania slot ma byæ widoczny jak pusty slot,
        // nie znikaæ z grida.
        if (slot.fillImage != null)
            slot.fillImage.SetActive(true);

        if (slot.borderImage != null)
            slot.borderImage.SetActive(true);

        slot.transform.localScale = Vector3.one;

        RectTransform rt = slot.GetComponent<RectTransform>();
        if (rt != null)
            rt.pivot = new Vector2(0.5f, 0.5f);

        slot.ClearOccupiedHighlight();
        slot.ClearPlacementPreview();
    }

    public void RebuildVisualForItem(InventoryItemInstance item)
    {
        if (item == null)
            return;

        InventorySlot mainSlot = GetTopLeftSlotForItem(item);

        if (mainSlot == null)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];

            if (slot == null || slot.item != item)
                continue;

            if (slot == mainSlot)
            {
                slot.SetItem(item);
            }
            else
            {
                RestoreSecondaryCellVisual(slot);
            }
        }
    }

    private void RestoreSecondaryCellVisual(InventorySlot slot)
    {
        if (slot == null)
            return;

        if (slot.iconImage != null)
        {
            slot.iconImage.sprite = null;
            slot.iconImage.enabled = false;
            slot.iconImage.color = Color.white;
        }

        if (slot.countText != null)
        {
            slot.countText.text = "";
            slot.countText.gameObject.SetActive(false);
        }

        if (slot.fillImage != null)
            slot.fillImage.SetActive(false);

        if (slot.borderImage != null)
            slot.borderImage.SetActive(false);

        slot.transform.localScale = Vector3.one;

        RectTransform rt = slot.GetComponent<RectTransform>();
        if (rt != null)
            rt.pivot = new Vector2(0.5f, 0.5f);

        slot.SetOccupiedHighlight(true);
        slot.ClearPlacementPreview();
    }
}