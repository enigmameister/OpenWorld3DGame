using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BoxInventoryUI : MonoBehaviour, IInventorySlotOwner
{
    // Controllers
    private InventoryGridController grid;
    private BoxCashController cashController;
    private BoxTransferController transferController;

    public static BoxInventoryUI Instance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI headerText;

    [Header("Slots")]
    [SerializeField] private Transform slotsParent;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private int totalSlots = 15;
    [SerializeField] private int slotsPerRow = 5;

    [Header("Cash")]
    [SerializeField] private TextMeshProUGUI boxCashText;
    [SerializeField] private TextMeshProUGUI playerCashText;
    [SerializeField] private ItemAmountDialog amountDialog;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button transferToPlayerButton;
    [SerializeField] private Button transferToBoxButton;

    [Header("Capacity texts")]
    [SerializeField] private TextMeshProUGUI boxCapacityText;
    [SerializeField] private TextMeshProUGUI playerCapacityText;

    [Header("Transfer message")]
    [SerializeField] private TextMeshProUGUI transferMessageText;
    [SerializeField] private float messageDuration = 2f;

    [Header("Drag Window")]
    [SerializeField] private RectTransform boxRoot;
    [SerializeField] private RectTransform dragArea;

    [Header("Placement Preview")]
    [SerializeField] private Color placementValidColor = new Color(1f, 0.78f, 0.05f, 0.45f);
    [SerializeField] private Color placementInvalidColor = new Color(1f, 0.05f, 0.05f, 0.45f);

    private readonly List<InventorySlot> slotList = new();
    private WorldBoxInventory currentBox;
    private PlayerStats playerStats;
    private int generatedSlotsPerRow = -1;

    private Vector2 dragOffset;
    private bool isDraggingWindow;
    private Vector2 defaultPosition;

    private float messageTimer;
    private InventorySlot lastPreviewStartSlot;

    private PointerEventData cachedPointerData;
    private readonly List<RaycastResult> uiRaycastResults = new();
    private readonly Dictionary<string, int> itemSlotMemory = new();

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (boxRoot == null)
        {
            if (root != null)
                boxRoot = root.transform as RectTransform;
            else
                boxRoot = transform as RectTransform;
        }

        if (boxRoot != null)
            defaultPosition = boxRoot.anchoredPosition;

        playerStats = FindFirstObjectByType<PlayerStats>();

        if (amountDialog == null) amountDialog = ItemAmountDialog.Instance;

        InitCashController();
        InitTransferController();

        closeButton?.onClick.AddListener(Close);
        transferToPlayerButton?.onClick.AddListener(TransferAllBoxToPlayer);
        transferToBoxButton?.onClick.AddListener(TransferAllPlayerToBox);

        Show(false);
    }

    private void Update()
    {
        if (!IsOpen) return;

#if ENABLE_INPUT_SYSTEM
        bool escPressed = UnityEngine.InputSystem.Keyboard.current != null &&
                          UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
    bool escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif

        if (escPressed)
        {
            Close();
            return;
        }

        HandleWindowDrag();
        UpdatePlacementPreview();

        if (messageTimer > 0f)
        {
            messageTimer -= Time.unscaledDeltaTime;

            if (messageTimer <= 0f && transferMessageText != null)
                transferMessageText.gameObject.SetActive(false);
        }
    }

    public void Open(WorldBoxInventory box)
    {
        if (box == null) return;

        isDraggingWindow = false;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        currentBox = box;
        totalSlots = Mathf.Max(1, box.totalSlots);
        slotsPerRow = Mathf.Max(1, box.slotsPerRow);

        if (headerText != null)
            headerText.text = box.boxName;

        GenerateSlotsIfNeeded();

        Show(true);

        RefreshSlots();
        RefreshOccupiedHighlights();
        RefreshCapacityTexts();
        RefreshCashTexts();

        if (InventoryUI.Instance != null && !InventoryUI.IsInventoryOpen)
            InventoryUI.Instance.ToggleInventory();

        MouseLook.IsLookLocked = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Close()
    {
        isDraggingWindow = false;
        messageTimer = 0f;

        HideTransferMessage();

        if (amountDialog != null && amountDialog.IsOpen)
            amountDialog.Close();
        else if (ItemAmountDialog.Instance != null && ItemAmountDialog.Instance.IsOpen)
            ItemAmountDialog.Instance.Close();

        Show(false);

        InventoryTooltip.Instance?.Hide();

        if (InventoryUI.draggedItem != null && ReferenceEquals(InventoryUI.DragSourceOwner, this))
            SetDraggingVisualForItem(InventoryUI.draggedItem, false);

        ClearPlacementPreview();
        RefreshOccupiedHighlights();

        InventoryUI.ClearSharedDragState();

        if (InventoryUI.Instance != null)
            InventoryUI.Instance.HideDragGhost();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        currentBox = null;
    }

    private void Show(bool show)
    {
        IsOpen = show;

        if (root != null)
            root.SetActive(show);
        else
            gameObject.SetActive(show);
    }

    private void GenerateSlotsIfNeeded()
    {
        if (slotList.Count == totalSlots && generatedSlotsPerRow == slotsPerRow)
            return;

        generatedSlotsPerRow = slotsPerRow;

        foreach (Transform child in slotsParent)
            Destroy(child.gameObject);

        slotList.Clear();

        for (int i = 0; i < totalSlots; i++)
        {
            GameObject go = Instantiate(slotPrefab, slotsParent);
            InventorySlot slot = go.GetComponent<InventorySlot>();
            slot.slotIndex = i;
            slot.owner = this;
            slotList.Add(slot);
        }

        InitGridController();
    }

    private void InitGridController()
    {
        grid = new InventoryGridController(
            slotList,
            slotsPerRow,
            placementValidColor,
            placementInvalidColor,
            () => InventoryUI.draggedItem,
            () => InventoryUI.IsDraggingInventoryItem
        );
    }

    private void InitCashController()
    {
        cashController = new BoxCashController(
            () => currentBox,
            () =>
            {
                if (playerStats == null)
                    playerStats = FindFirstObjectByType<PlayerStats>();

                return playerStats;
            },
            () => amountDialog != null ? amountDialog : ItemAmountDialog.Instance,
            boxCashText,
            playerCashText,
            RefreshCapacityTexts
        );
    }

    private void InitTransferController()
    {
        transferController = new BoxTransferController(
            () => currentBox,
            () => InventoryUI.Instance,
            () => this,
            RefreshSlots,
            RefreshCapacityTexts,
            RefreshCashTexts,
            TryOpenCashTransferBoxToPlayer,
            TryOpenCashTransferPlayerToBox,
            ShowTransferMessage
        );
    }

    private void RefreshSlots()
    {
        foreach (var slot in slotList)
            ClearSlotVisual(slot);

        if (currentBox != null)
        {
            var items = currentBox.GetItems();

            foreach (var inst in items)
            {
                if (inst == null || inst.data == null)
                    continue;

                if (TryPlaceItemAtRememberedSlot(inst))
                    continue;

                TryPlaceExistingInstance(inst);
            }
        }

        RefreshOccupiedHighlights();
    }

    public void OnSlotClicked(InventorySlot clickedSlot)
    {
        if (currentBox == null || clickedSlot == null)
            return;

        int clickedIndex = slotList.IndexOf(clickedSlot);
        if (clickedIndex < 0)
            return;

        int placeIndex = clickedIndex;

        if (InventoryUI.draggedItem != null)
        {
            placeIndex -= InventoryUI.DraggedGrabCellOffset;
            placeIndex -= InventoryUI.DraggedGrabCellOffsetY * slotsPerRow;
        }

        // 1) anulowanie drag, jeœli klikamy Ÿród³o
        if (InventoryUI.draggedItem != null && InventoryUI.DragSourceSlot == clickedSlot)
        {
            bool rotationChanged =
                InventoryUI.draggedItem.rotated != InventoryUI.DragOriginalRotated;

            if (!rotationChanged)
            {
                InventoryUI.RestoreDraggedRotationIfCanceled();
                ClearDragWithoutRefreshingSlots();
                return;
            }

            // Po obrocie nie anulujemy — idziemy dalej do normalnego dropu.
        }

        // 2) jeœli coœ przeci¹gamy
        if (InventoryUI.draggedItem != null)
        {
            InventoryItemInstance dragged = InventoryUI.draggedItem;

            // DROP NA ZAJÊTY SLOT W BOXIE
            if (clickedSlot.isOccupied && clickedSlot.item != null)
            {
                bool clickedOwnDraggedItem =
                    ReferenceEquals(InventoryUI.DragSourceOwner, this) &&
                    clickedSlot.item == dragged;

                if (!clickedOwnDraggedItem)
                {
                    bool wantsQuickSplit =
                        InventoryUI.IsStackQuickSplitModifierHeld() &&
                        InventoryUI.CanSplitStack(dragged);

                    if (wantsQuickSplit)
                    {
                        if (TryQuickSplitDraggedStackIntoBoxSlot(clickedSlot))
                            return;

                        return;
                    }

                    if (TryMergeDraggedStackIntoBoxSlot(clickedSlot))
                        return;

                    return;
                }

                // Jeœli to w³asny stary slot tego samego itemu, pozwól spróbowaæ po³o¿yæ od tego indeksu.
            }

            bool quickSplitStack =
              InventoryUI.IsStackQuickSplitModifierHeld() &&
              InventoryUI.CanSplitStack(dragged);

            bool dialogSplitStack =
                InventoryUI.IsStackSplitModifierHeld() &&
                InventoryUI.CanSplitStack(dragged);

            if (!CanPlaceDraggedItemAt(placeIndex, dragged))
                return;

            // 2A) CTRL + szybki split na pó³ bez okna
            if (quickSplitStack)
            {
                TryQuickSplitDraggedStackToBoxSlot(
                    placeIndex,
                    dragged,
                    InventoryUI.DragSourceOwner
                );

                return;
            }

            // 2B) SHIFT + split stacka z oknem
            if (dialogSplitStack)
            {
                ItemAmountDialog dialog = ItemAmountDialog.Instance;
                if (dialog == null)
                    return;

                InventoryItemInstance sourceItem = dragged;
                IInventorySlotOwner splitSource = InventoryUI.DragSourceOwner;
                int targetIndex = placeIndex;

                bool sameOwner = ReferenceEquals(splitSource, this);

                int maxAmount = sameOwner
                    ? Mathf.Max(1, sourceItem.count - 1)
                    : sourceItem.count;

                if (maxAmount <= 0)
                    return;

                ClearDragWithoutRefreshingSlots();

                dialog.Open(
                    $"TRANSFER {sourceItem.data.itemName}",
                    1,
                    maxAmount,
                    Mathf.CeilToInt(maxAmount / 2f),
                    amount =>
                    {
                        amount = Mathf.Clamp(amount, 1, maxAmount);

                        InventoryItemInstance part = InventoryStackService.CloneStackPart(sourceItem, amount);
                        if (part == null)
                            return;

                        if (!CanFitItem(targetIndex, Mathf.Max(1, part.data.slotSize)))
                            return;

                        if (splitSource != null && splitSource.RemoveStackAmountFromOwner(sourceItem, amount))
                        {
                            if (currentBox != null && !currentBox.GetItems().Contains(part))
                                currentBox.GetItems().Add(part);

                            PlaceAt(targetIndex, part);

                            // odœwie¿ Ÿród³o
                            if (splitSource is InventoryUI invSource)
                                invSource.RefreshVisualsAfterExternalStackChange(sourceItem);
                            else if (splitSource is BoxInventoryUI boxSource)
                                boxSource.RefreshVisualsAfterExternalStackChange(sourceItem);

                            // odœwie¿ target Box
                            RefreshVisualsAfterExternalStackChange(part);
                        }
                    },
                    cancel: null
                );

                return;
            }

            // 2B) normalne przeniesienie ca³ego itemu
            IInventorySlotOwner source = InventoryUI.DragSourceOwner;

            if (ReferenceEquals(source, this))
            {
                RemoveVisualOnly(dragged);
                PlaceAt(placeIndex, dragged);
            }
            else
            {
                if (source != null && source.RemoveItemFromOwner(dragged))
                {
                    if (!currentBox.GetItems().Contains(dragged))
                        currentBox.GetItems().Add(dragged);

                    if (dragged.count <= 0)
                        dragged.count = 1;

                    PlaceAt(placeIndex, dragged);
                }
                else
                {
                    return;
                }
            }

            ClearPlacementPreview();

            if (InventoryUI.Instance != null)
                InventoryUI.Instance.HideDragGhost();

            InventoryUI.ClearSharedDragState();

            RefreshCapacityTexts();
            RefreshOccupiedHighlights();

            if (InventoryUI.Instance != null)
                InventoryUI.Instance.RefreshGunUIFromWeaponManager();

            return;
        }

        // 3) START DRAG Z BOXA
        if (InventoryUI.draggedItem == null && clickedSlot.isOccupied && clickedSlot.item != null)
        {
            InventoryItemInstance item = clickedSlot.item;

            InventorySlot sourceSlot = GetTopLeftSlotForItem(item);
            if (sourceSlot == null)
                sourceSlot = clickedSlot;

            InventoryUI.draggedItem = item;
            InventoryUI.CaptureDragOriginalRotation(InventoryUI.draggedItem);

            InventoryUI.DragSourceOwner = this;
            InventoryUI.DragSourceSlot = sourceSlot;
            InventoryUI.SetInventoryItemDragging(true);

            InventoryUI.Instance?.ShowDragGhost(InventoryUI.draggedItem, sourceSlot);

            SetDraggingVisualForItem(InventoryUI.draggedItem, true);

            InventoryTooltip.Instance?.Hide();

            return;
        }
    }

    private bool TryQuickSplitDraggedStackIntoBoxSlot(InventorySlot targetSlot)
    {
        InventoryItemInstance sourceItem = InventoryUI.draggedItem;

        if (sourceItem == null || sourceItem.data == null)
            return false;

        if (targetSlot == null || targetSlot.item == null || targetSlot.item.data == null)
            return false;

        InventoryItemInstance targetItem = targetSlot.item;
        IInventorySlotOwner sourceOwner = InventoryUI.DragSourceOwner;

        if (sourceOwner == null)
            return false;

        if (!InventoryStackService.CanMergeStacks(sourceItem, targetItem))
            return false;

        bool sameOwner = ReferenceEquals(sourceOwner, this);

        int amount = InventoryStackService.GetQuickSplitHalfAmount(sourceItem, sameOwner);
        if (amount <= 0)
            return false;

        if (sourceOwner is BoxInventoryUI boxSourceBefore)
            boxSourceBefore.SetDraggingVisualForItem(sourceItem, false);

        InventoryUI.Instance?.HideDragGhost();
        ClearPlacementPreview();

        bool removed = sourceOwner.RemoveStackAmountFromOwner(sourceItem, amount);
        if (!removed)
            return false;

        targetItem.count += amount;
        RefreshCountDisplay(targetItem);

        InventoryUI.ClearSharedDragState();

        if (sourceOwner is InventoryUI invSource)
            invSource.RefreshVisualsAfterExternalStackChange(sourceItem);
        else if (sourceOwner is BoxInventoryUI boxSource)
            boxSource.RefreshVisualsAfterExternalStackChange(sourceItem);

        RefreshVisualsAfterExternalStackChange(targetItem);
        RefreshCapacityTexts();

        if (InventoryUI.Instance != null)
            InventoryUI.Instance.RefreshGunUIFromWeaponManager();

        return true;
    }

    private void ClearDragWithoutRefreshingSlots()
    {
        if (InventoryUI.draggedItem != null && ReferenceEquals(InventoryUI.DragSourceOwner, this))
            SetDraggingVisualForItem(InventoryUI.draggedItem, false);

        ClearPlacementPreview();

        InventoryUI.ClearSharedDragState();

        if (InventoryUI.Instance != null)
            InventoryUI.Instance.HideDragGhost();

        RefreshOccupiedHighlights();
    }

    public bool TryReceiveItem(InventoryItemInstance item)
    {
        if (currentBox == null || item == null || item.data == null) return false;

        if (!TryPlaceExistingInstance(item))
            return false;

        if (!currentBox.GetItems().Contains(item))
            currentBox.GetItems().Add(item);

        RefreshCapacityTexts();
        RefreshOccupiedHighlights();

        return true;
    }

    public bool RemoveItemFromOwner(InventoryItemInstance item)
    {
        if (currentBox == null || item == null)
            return false;

        bool removed = currentBox.GetItems().Remove(item);

        ForgetItemSlot(item);

        RemoveVisualOnly(item);

        RefreshCapacityTexts();
        RefreshOccupiedHighlights();

        return removed;
    }

    private void RefreshCapacityTexts()
    {
        if (currentBox != null && boxCapacityText != null)
        {
            int used = CountUsedSlots();
            boxCapacityText.text = $"{used}/{totalSlots}";
        }

        if (InventoryUI.Instance != null && playerCapacityText != null)
        {
            int used = InventoryUI.Instance.GetUsedSlotCount();
            int max = InventoryUI.Instance.GetUnlockedSlotCount();

            playerCapacityText.text = $"{used}/{max}";
        }
    }

    private void HandleWindowDrag()
    {
        if (!IsOpen || boxRoot == null || dragArea == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragArea,
                Input.mousePosition,
                null,
                out Vector2 localMousePos
            );

            if (dragArea.rect.Contains(localMousePos))
            {
                RectTransform parentRect = boxRoot.parent as RectTransform;
                if (parentRect == null) return;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    Input.mousePosition,
                    null,
                    out Vector2 pointerPos
                );

                dragOffset = pointerPos - boxRoot.anchoredPosition;
                isDraggingWindow = true;
            }
        }

        if (Input.GetMouseButtonUp(0))
            isDraggingWindow = false;

        if (isDraggingWindow)
        {
            RectTransform parentRect = boxRoot.parent as RectTransform;
            if (parentRect == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                Input.mousePosition,
                null,
                out Vector2 pointerPos
            );

            boxRoot.anchoredPosition = pointerPos - dragOffset;
        }
    }

    public bool ContainsTransform(Transform t)
    {
        if (t == null) return false;

        Transform rootTransform = root != null ? root.transform : transform;
        return t == rootTransform || t.IsChildOf(rootTransform);
    }

    private void ShowTransferMessage(string msg)
    {
        if (transferMessageText == null) return;

        transferMessageText.text = msg;
        transferMessageText.gameObject.SetActive(true);
        messageTimer = messageDuration;
    }

    private void HideTransferMessage()
    {
        if (transferMessageText == null) return;

        transferMessageText.text = "";
        transferMessageText.gameObject.SetActive(false);
        messageTimer = 0f;
    }

    public void ShowTransferMessagePublic(string msg)
    {
        ShowTransferMessage(msg);
    }

    public bool RemoveStackAmountFromOwner(InventoryItemInstance item, int amount)
    {
        if (currentBox == null || item == null || amount <= 0) return false;

        amount = Mathf.Clamp(amount, 1, item.count);

        if (amount >= item.count)
            return RemoveItemFromOwner(item);

        item.count -= amount;

        RefreshCountDisplay(item);
        RefreshCapacityTexts();

        return true;
    }

    private bool TryMergeDraggedStackIntoBoxSlot(InventorySlot targetSlot)
    {
        InventoryItemInstance sourceItem = InventoryUI.draggedItem;

        if (sourceItem == null || sourceItem.data == null)
            return false;

        if (targetSlot == null || targetSlot.item == null || targetSlot.item.data == null)
            return false;

        InventoryItemInstance targetItem = targetSlot.item;
        IInventorySlotOwner sourceOwner = InventoryUI.DragSourceOwner;

        if (sourceOwner == null)
            return false;

        if (!InventoryStackService.CanMergeStacks(sourceItem, targetItem))
            return false;

        int addAmount = Mathf.Max(1, sourceItem.count);

        // Przywróæ wizual Ÿród³a przed zdjêciem draga.
        if (sourceOwner is InventoryUI invSourceBefore)
            invSourceBefore.RefreshVisualsAfterExternalStackChange(sourceItem);
        else if (sourceOwner is BoxInventoryUI boxSourceBefore)
            boxSourceBefore.RefreshVisualsAfterExternalStackChange(sourceItem);

        InventoryUI.Instance?.HideDragGhost();
        ClearPlacementPreview();

        bool removed = sourceOwner.RemoveItemFromOwner(sourceItem);
        if (!removed)
            return false;

        targetItem.count += addAmount;

        RefreshCountDisplay(targetItem);

        InventoryUI.ClearSharedDragState();

        if (sourceOwner is InventoryUI invSource)
            invSource.RefreshVisualsAfterExternalStackChange(sourceItem);
        else if (sourceOwner is BoxInventoryUI boxSource)
            boxSource.RefreshVisualsAfterExternalStackChange(sourceItem);

        RefreshVisualsAfterExternalStackChange(targetItem);
        RefreshCapacityTexts();

        if (InventoryUI.Instance != null)
            InventoryUI.Instance.RefreshGunUIFromWeaponManager();

        return true;
    }

    private void RefreshCountDisplay(InventoryItemInstance item)
    {
        if (item == null) return;

        foreach (var slot in slotList)
        {
            if (slot != null && slot.item == item)
                slot.UpdateCountDisplay();
        }
    }

    public void RefreshVisualsAfterExternalStackChange(InventoryItemInstance item = null)
    {
        if (item != null)
        {
            SetDraggingVisualForItem(item, false);
            RefreshCountDisplay(item);
        }

        ClearPlacementPreview();

        RefreshCapacityTexts();
        RefreshOccupiedHighlights();

        if (InventoryUI.Instance != null)
            InventoryUI.Instance.RefreshGunUIFromWeaponManager();
    }

    private void UpdatePlacementPreview()
    {
        if (!IsOpen)
        {
            ClearPlacementPreview();
            return;
        }

        if (InventoryUI.draggedItem == null || !InventoryUI.IsDraggingInventoryItem)
        {
            ClearPlacementPreview();
            return;
        }

        InventorySlot hoverSlot = GetBoxSlotUnderMouse();

        if (hoverSlot != null)
        {
            int previewStartIndex =
                hoverSlot.slotIndex
                - InventoryUI.DraggedGrabCellOffset
                - (InventoryUI.DraggedGrabCellOffsetY * slotsPerRow);

            if (hoverSlot != lastPreviewStartSlot)
            {
                lastPreviewStartSlot = hoverSlot;
                PreviewPlacement(previewStartIndex, InventoryUI.draggedItem);
            }
        }
        else
        {
            ClearPlacementPreview();
        }
    }

    private InventorySlot GetBoxSlotUnderMouse()
    {
        if (EventSystem.current == null)
            return null;

        if (cachedPointerData == null)
            cachedPointerData = new PointerEventData(EventSystem.current);

        cachedPointerData.position = Input.mousePosition;

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(cachedPointerData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject go = uiRaycastResults[i].gameObject;
            if (go == null)
                continue;

            InventorySlot slot = go.GetComponentInParent<InventorySlot>();
            if (slot == null)
                continue;

            if (ReferenceEquals(slot.owner, this))
                return slot;
        }

        return null;
    }

    public void ClearPlacementPreviewExternal()
    {
        ClearPlacementPreview();
        lastPreviewStartSlot = null;
    }

    private bool TryQuickSplitDraggedStackToBoxSlot(
    int targetIndex,
    InventoryItemInstance sourceItem,
    IInventorySlotOwner splitSource)
    {
        if (currentBox == null)
            return false;

        if (sourceItem == null || sourceItem.data == null)
            return false;

        if (splitSource == null)
            return false;

        bool sameOwner = ReferenceEquals(splitSource, this);

        int amount = InventoryStackService.GetQuickSplitHalfAmount(sourceItem, sameOwner);

        if (amount <= 0)
            return false;

        InventoryItemInstance part = InventoryStackService.CloneStackPart(sourceItem, amount);

        if (part == null)
            return false;

        if (grid == null)
            InitGridController();

        if (grid == null)
            return false;

        if (!grid.CanFitShape(
                targetIndex,
                grid.GetItemWidth(part),
                grid.GetItemHeight(part)))
        {
            return false;
        }

        ClearDragWithoutRefreshingSlots();

        if (!splitSource.RemoveStackAmountFromOwner(sourceItem, amount))
            return false;

        if (!currentBox.GetItems().Contains(part))
            currentBox.GetItems().Add(part);

        PlaceAt(targetIndex, part);

        if (splitSource is InventoryUI invSource)
            invSource.RefreshVisualsAfterExternalStackChange(sourceItem);
        else if (splitSource is BoxInventoryUI boxSource)
            boxSource.RefreshVisualsAfterExternalStackChange(sourceItem);

        RefreshVisualsAfterExternalStackChange(part);

        return true;
    }

    private string GetItemMemoryKey(InventoryItemInstance item)
    {
        return item != null ? item.id : "";
    }

    private void RememberItemSlot(InventoryItemInstance item, int startIndex)
    {
        if (item == null || string.IsNullOrEmpty(item.id))
            return;

        itemSlotMemory[item.id] = startIndex;
    }

    private void ForgetItemSlot(InventoryItemInstance item)
    {
        if (item == null || string.IsNullOrEmpty(item.id))
            return;

        itemSlotMemory.Remove(item.id);
    }

    private int GetStartIndexOfItem(InventoryItemInstance item)
    {
        if (item == null)
            return -1;

        for (int i = 0; i < slotList.Count; i++)
        {
            InventorySlot slot = slotList[i];

            if (slot != null && slot.item == item)
                return i;
        }

        return -1;
    }

    private bool TryPlaceItemAtRememberedSlot(InventoryItemInstance item)
    {
        if (item == null || item.data == null || grid == null)
            return false;

        string key = GetItemMemoryKey(item);

        if (string.IsNullOrEmpty(key))
            return false;

        if (!itemSlotMemory.TryGetValue(key, out int rememberedIndex))
            return false;

        int width = grid.GetItemWidth(item);
        int height = grid.GetItemHeight(item);

        if (!grid.CanFitShape(rememberedIndex, width, height))
            return false;

        grid.PlaceAtUnsafe(rememberedIndex, item);
        RememberItemSlot(item, rememberedIndex);

        return true;
    }

    public bool TryRelayoutDraggedItemAfterRotation(InventoryItemInstance item)
    {
        if (item == null || item.data == null || grid == null)
            return false;

        int startIndex = grid.GetTopLeftIndexForItem(item);

        if (startIndex < 0 && itemSlotMemory.TryGetValue(item.id, out int remembered))
            startIndex = remembered;

        if (startIndex < 0)
            return false;

        grid.ForceRemoveCompletely(item);

        int width = grid.GetItemWidth(item);
        int height = grid.GetItemHeight(item);

        if (!grid.CanFitShape(startIndex, width, height))
        {
            item.ToggleRotation();

            width = grid.GetItemWidth(item);
            height = grid.GetItemHeight(item);

            if (!grid.CanFitShape(startIndex, width, height))
                return false;
        }

        PlaceAt(startIndex, item);

        SetDraggingVisualForItem(item, true);

        RefreshCapacityTexts();
        RefreshOccupiedHighlights();

        return true;
    }

    // =====================================================
    // Grid Controller Wrappers
    // =====================================================

    private bool CanFitItem(int startIndex, int size)
    {
        return grid != null && grid.CanFitItem(startIndex, size);
    }

    private bool CanPlaceDraggedItemAt(int startIndex, InventoryItemInstance item)
    {
        return grid != null &&
               grid.CanPlaceDraggedItemAt(startIndex, item, this, InventoryUI.DragSourceOwner);
    }

    private void PlaceAt(int startIndex, InventoryItemInstance inst)
    {
        if (grid == null || inst == null)
            return;

        grid.PlaceAtUnsafe(startIndex, inst);
        RememberItemSlot(inst, startIndex);
    }

    private bool TryPlaceExistingInstance(InventoryItemInstance inst)
    {
        if (grid == null || inst == null)
            return false;

        bool placed = grid.TryPlaceExistingInstance(inst);

        if (placed)
        {
            int index = GetStartIndexOfItem(inst);
            if (index >= 0)
                RememberItemSlot(inst, index);
        }

        return placed;
    }

    private void ClearSlotVisual(InventorySlot slot)
    {
        grid?.ClearSlotVisual(slot);
    }

    private void RemoveVisualOnly(InventoryItemInstance item)
    {
        grid?.RemoveVisualOnly(item);
    }
    private void RefreshOccupiedHighlights()
    {
        grid?.RefreshOccupiedHighlights();
    }
    private void ClearPlacementPreview()
    {
        grid?.ClearPlacementPreview();
        lastPreviewStartSlot = null;
    }

    private void PreviewPlacement(int startIndex, InventoryItemInstance item)
    {
        grid?.PreviewPlacement(startIndex, item, this, InventoryUI.DragSourceOwner);
    }
    private int CountUsedSlots()
    {
        return grid != null ? grid.CountUsedSlots() : 0;
    }

    private void SetDraggingVisualForItem(InventoryItemInstance item, bool dragging)
    {
        grid?.SetDraggingVisualForItem(item, dragging);
    }

    private InventorySlot GetTopLeftSlotForItem(InventoryItemInstance item)
    {
        return grid != null ? grid.GetTopLeftSlotForItem(item) : null;
    }

    // =====================================================
    // BoxCash Wrappers
    // =====================================================

    private void RefreshCashTexts()
    {
        if (cashController == null)
            InitCashController();

        cashController?.RefreshCashTexts();
    }

    private void TryOpenCashTransferBoxToPlayer()
    {
        if (cashController == null)
            InitCashController();

        cashController?.TryOpenCashTransferBoxToPlayer();
    }

    private void TryOpenCashTransferPlayerToBox()
    {
        if (cashController == null)
            InitCashController();

        cashController?.TryOpenCashTransferPlayerToBox();
    }

    // =====================================================
    // BoxTransfer Wrappers
    // =====================================================

    private void TransferAllBoxToPlayer()
    {
        HideTransferMessage();

        if (transferController == null)
            InitTransferController();

        transferController?.TransferAllBoxToPlayer();
    }

    private void TransferAllPlayerToBox()
    {
        HideTransferMessage();

        if (transferController == null)
            InitTransferController();

        transferController?.TransferAllPlayerToBox();
    }
}