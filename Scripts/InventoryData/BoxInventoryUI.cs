using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BoxInventoryUI : MonoBehaviour, IInventorySlotOwner
{
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
    private float messageTimer;

    [Header("Drag Window")]
    [SerializeField] private RectTransform boxRoot;
    [SerializeField] private RectTransform dragArea;

    private Vector2 dragOffset;
    private bool isDraggingWindow;
    private Vector2 defaultPosition;

    private readonly List<InventorySlot> slotList = new();
    private WorldBoxInventory currentBox;
    private PlayerStats playerStats;
    private int generatedSlotsPerRow = -1;
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

        if (amountDialog == null)
            amountDialog = ItemAmountDialog.Instance;

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
        RefreshSlots();
        RefreshCapacityTexts();
        RefreshCashTexts();

        Show(true);

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
    }

    private void RefreshSlots()
    {
        foreach (var slot in slotList)
            ClearSlotVisual(slot);

        if (currentBox == null) return;

        var items = currentBox.GetItems();
        foreach (var inst in items)
        {
            TryPlaceExistingInstance(inst);
        }
    }

    private void ClearSlotVisual(InventorySlot slot)
    {
        if (slot == null) return;

        slot.isOccupied = false;
        slot.item = null;

        if (slot.iconImage != null)
        {
            slot.iconImage.sprite = null;
            slot.iconImage.enabled = false;
            slot.iconImage.color = Color.white;
        }

        if (slot.fillImage != null) slot.fillImage.SetActive(true);
        if (slot.borderImage != null) slot.borderImage.SetActive(true);

        slot.transform.localScale = Vector3.one;
        slot.UpdateCountDisplay();
    }

    private bool TryPlaceExistingInstance(InventoryItemInstance inst)
    {
        if (inst == null || inst.data == null) return false;

        int size = Mathf.Max(1, inst.data.slotSize);

        for (int i = 0; i <= slotList.Count - size; i++)
        {
            if (CanFitItem(i, size))
            {
                PlaceAt(i, inst);
                return true;
            }
        }

        return false;
    }

    private bool CanFitItem(int startIndex, int size)
    {
        if (startIndex < 0) return false;
        if (startIndex + size > slotList.Count) return false;

        int rowStart = startIndex / slotsPerRow;
        int rowEnd = (startIndex + size - 1) / slotsPerRow;
        if (rowStart != rowEnd) return false;

        for (int i = 0; i < size; i++)
        {
            if (slotList[startIndex + i].isOccupied)
                return false;
        }

        return true;
    }

    private void PlaceAt(int startIndex, InventoryItemInstance inst)
    {
        int size = Mathf.Max(1, inst.data.slotSize);

        for (int j = 0; j < size; j++)
        {
            InventorySlot slot = slotList[startIndex + j];

            slot.isOccupied = true;
            slot.item = inst;

            if (j == 0)
            {
                slot.SetItem(inst);
            }
            else
            {
                if (slot.fillImage != null) slot.fillImage.SetActive(false);
                if (slot.borderImage != null) slot.borderImage.SetActive(false);
                if (slot.iconImage != null)
                {
                    slot.iconImage.sprite = null;
                    slot.iconImage.enabled = false;
                }
            }
        }
    }

    public void OnSlotClicked(InventorySlot clickedSlot)
    {
        if (currentBox == null || clickedSlot == null) return;

        int clickedIndex = slotList.IndexOf(clickedSlot);
        if (clickedIndex < 0) return;

        // anulowanie drag, jeœli klikamy Ÿród³o
        if (InventoryUI.draggedItem != null && InventoryUI.DragSourceSlot == clickedSlot)
        {
            ClearDrag();
            return;
        }

        // DROP NA ZAJÊTY SLOT W BOXIE - MERGE STACKÓW
        if (InventoryUI.draggedItem != null && clickedSlot.isOccupied && clickedSlot.item != null)
        {
            if (TryMergeDraggedStackIntoBoxSlot(clickedSlot))
                return;
        }

        // DROP NA PUSTY SLOT W BOXIE
        if (InventoryUI.draggedItem != null && !clickedSlot.isOccupied)
        {
            var dragged = InventoryUI.draggedItem;
            int size = Mathf.Max(1, dragged.data.slotSize);

            bool splitStack =
                InventoryUI.IsStackSplitModifierHeld() &&
                InventoryUI.CanSplitStack(dragged);

            if (!CanFitItem(clickedIndex, size))
                return;
            if (splitStack)
            {
                ItemAmountDialog dialog = ItemAmountDialog.Instance;
                if (dialog == null) return;

                InventoryItemInstance sourceItem = dragged;
                IInventorySlotOwner splitSource = InventoryUI.DragSourceOwner;
                int targetIndex = clickedIndex;

                bool sameOwner = ReferenceEquals(splitSource, this);

                int maxAmount = sameOwner
                    ? Mathf.Max(1, sourceItem.count - 1)
                    : sourceItem.count;

                if (maxAmount <= 0)
                    return;

                // Od tego momentu operacjê przejmuje dialog.
                ClearDrag();

                dialog.Open(
                    $"TRANSFER {sourceItem.data.itemName}",
                    1,
                    maxAmount,
                    Mathf.CeilToInt(maxAmount / 2f),
                    amount =>
                    {
                        amount = Mathf.Clamp(amount, 1, maxAmount);

                        InventoryItemInstance part = InventoryUI.CloneStackPart(sourceItem, amount);
                        if (part == null) return;

                        if (!CanFitItem(targetIndex, Mathf.Max(1, part.data.slotSize)))
                            return;

                        if (splitSource != null && splitSource.RemoveStackAmountFromOwner(sourceItem, amount))
                        {
                            if (currentBox != null && !currentBox.GetItems().Contains(part))
                                currentBox.GetItems().Add(part);

                            PlaceAt(targetIndex, part);
                            RefreshCountDisplay(sourceItem);
                            RefreshCountDisplay(part);
                            RefreshCapacityTexts();

                            if (InventoryUI.Instance != null)
                                InventoryUI.Instance.RefreshGunUIFromWeaponManager();
                        }
                    },
                    cancel: null
                );

                return;
            }

            IInventorySlotOwner source = InventoryUI.DragSourceOwner;

            if (ReferenceEquals(source, this))
            {
                RemoveVisualOnly(dragged);
                PlaceAt(clickedIndex, dragged);
            }
            else
            {
                if (source != null && source.RemoveItemFromOwner(dragged))
                {
                    if (!currentBox.GetItems().Contains(dragged))
                        currentBox.GetItems().Add(dragged);

                    if (dragged.count <= 0)
                        dragged.count = 1;

                    PlaceAt(clickedIndex, dragged);
                }
            }

            ClearDrag();
            RefreshCapacityTexts();
            return;
        }



        // START DRAG Z BOXA
        if (InventoryUI.draggedItem == null && clickedSlot.isOccupied && clickedSlot.item != null)
        {
            InventoryUI.draggedItem = clickedSlot.item;
            InventoryUI.DragSourceOwner = this;
            InventoryUI.DragSourceSlot = clickedSlot;
            InventoryUI.SetInventoryItemDragging(true);

            InventoryUI.Instance?.ShowDragGhost(InventoryUI.draggedItem, clickedSlot);

            // Ghost zostaje ten z InventoryUI. Jeœli jest widoczny tylko po starcie z gracza,
            // mo¿na póŸniej dopisaæ publiczn¹ metodê ShowDragGhost w InventoryUI.
            return;
        }
    }

    private void RemoveVisualOnly(InventoryItemInstance item)
    {
        foreach (var slot in slotList)
        {
            if (slot.item == item)
                ClearSlotVisual(slot);
        }
    }

    public bool TryReceiveItem(InventoryItemInstance item)
    {
        if (currentBox == null || item == null || item.data == null) return false;

        if (!TryPlaceExistingInstance(item))
            return false;

        if (!currentBox.GetItems().Contains(item))
            currentBox.GetItems().Add(item);

        RefreshCapacityTexts();
        return true;
    }

    public bool RemoveItemFromOwner(InventoryItemInstance item)
    {
        if (currentBox == null || item == null) return false;

        bool removed = currentBox.GetItems().Remove(item);
        RemoveVisualOnly(item);
        RefreshSlots();
        RefreshCapacityTexts();

        return removed;
    }

    private void ClearDrag()
    {
        InventoryUI.ClearSharedDragState();

        if (InventoryUI.Instance != null)
            InventoryUI.Instance.HideDragGhost();
    }

    private void TransferAllBoxToPlayer()
    {
        if (currentBox == null || InventoryUI.Instance == null) return;

        HideTransferMessage();

        HashSet<int> duplicateSlots = GetDuplicateNonStackWeaponSlotsInBox();

        bool skippedDuplicateType = false;
        bool blockedByPlayerWeapon = false;

        var copy = new List<InventoryItemInstance>(currentBox.GetItems());

        foreach (var item in copy)
        {
            if (item == null || item.data == null) continue;

            if (IsCombatItemData(item.data))
            {
                int slotIndex = GetCombatSlotIndexFromData(item.data);

                // Jeœli w Boxie s¹ np. AK47 + SniperRiffle,
                // to blokujemy tylko Riffles, ale Pistol/Melee mog¹ przejœæ.
                if (slotIndex != 3 && duplicateSlots.Contains(slotIndex))
                {
                    skippedDuplicateType = true;
                    continue;
                }

                // Jeœli gracz ju¿ ma ten typ broni, te¿ blokujemy.
                if (slotIndex != 3 && !InventoryUI.Instance.CanReceiveWeaponFromBox(item))
                {
                    blockedByPlayerWeapon = true;
                    continue;
                }

                TryTransferCombatItemFromBoxToPlayerNoSwap(item);
                continue;
            }

            if (InventoryUI.Instance.TryAddItem(item))
                currentBox.GetItems().Remove(item);
        }

        if (skippedDuplicateType)
            ShowTransferMessage("TWO WEAPONS SAME TYPE - TRANSFER MANUALLY");
        else if (blockedByPlayerWeapon)
            ShowTransferMessage("ALREADY WEAPON THIS TYPE");

        InventoryUI.Instance.RefreshGunUIFromWeaponManager();

        RefreshSlots();
        RefreshCapacityTexts();
        RefreshCashTexts();

        TryOpenCashTransferBoxToPlayer();
    }

    private void TransferAllPlayerToBox()
    {
        if (currentBox == null || InventoryUI.Instance == null) return;

        HideTransferMessage();

        var playerItems = InventoryUI.Instance.GetAllInstancesDistinct();

        foreach (var item in playerItems)
        {
            if (item == null || item.data == null) continue;

            if (TryReceiveItem(item))
            {
                bool removedFromPlayer = InventoryUI.Instance.RemoveItemFromOwner(item);

                if (!removedFromPlayer)
                {
                    // cofka anty-duplikat, gdyby coœ siê nie uda³o
                    RemoveItemFromOwner(item);
                }
            }
        }

        InventoryUI.Instance.RefreshGunUIFromWeaponManager();

        RefreshSlots();
        RefreshCapacityTexts();
        RefreshCashTexts();

        TryOpenCashTransferPlayerToBox();
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

    private int CountUsedSlots()
    {
        int used = 0;

        foreach (var slot in slotList)
        {
            if (slot != null && slot.isOccupied)
                used++;
        }

        return used;
    }

    private bool TryTransferCombatItemFromBoxToPlayerNoSwap(InventoryItemInstance item)
    {
        if (currentBox == null || item == null || item.data == null) return false;
        if (!IsCombatItemData(item.data)) return false;
        if (InventoryUI.Instance == null) return false;

        if (!InventoryUI.Instance.CanReceiveWeaponFromBox(item))
            return false;

        if (!InventoryUI.Instance.TryAddItem(item))
            return false;

        // Granaty s¹ stackowalne, wiêc TryAddItem mo¿e je scaliæ z istniej¹cym stackiem.
        // Nie traktujemy ich jak zwyk³ej broni 1:1, tylko synchronizujemy WeaponManager/GunUI.
        if (item.data is GrenadeItemData)
        {
            currentBox.GetItems().Remove(item);

            InventoryUI.Instance.RefreshGunUIFromWeaponManager();

            var wm = FindFirstObjectByType<WeaponManager>();
            if (wm != null)
                wm.SyncGrenadeSlotFromInventory(item.data);

            return true;
        }

        if (!InventoryUI.Instance.RegisterWeaponFromBoxTransfer(item))
        {
            InventoryUI.Instance.RemoveItemFromOwner(item);
            return false;
        }

        currentBox.GetItems().Remove(item);
        return true;
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

    private bool IsCombatItemData(InventoryItemData data)
    {
        return data is WeaponItemData
            || data is MeleeItemData
            || data is GrenadeItemData;
    }

    private int GetCombatSlotIndexFromData(InventoryItemData data)
    {
        if (data == null) return -1;

        if (data is WeaponItemData weaponData)
        {
            return weaponData.category switch
            {
                WeaponCategory.Melees => 0,
                WeaponCategory.Pistols => 1,
                WeaponCategory.Riffles => 2,
                WeaponCategory.Nades => 3,
                _ => -1
            };
        }

        if (data is MeleeItemData)
            return 0;

        if (data is GrenadeItemData)
            return 3;

        return -1;
    }

    private HashSet<int> GetDuplicateNonStackWeaponSlotsInBox()
    {
        var counts = new Dictionary<int, int>();
        var duplicates = new HashSet<int>();

        if (currentBox == null)
            return duplicates;

        foreach (var item in currentBox.GetItems())
        {
            if (item == null || item.data == null) continue;
            if (!IsCombatItemData(item.data)) continue;

            int slot = GetCombatSlotIndexFromData(item.data);

            // Granaty pomijamy — stack/dialog póŸniej.
            if (slot < 0 || slot == 3)
                continue;

            counts.TryGetValue(slot, out int current);
            current++;
            counts[slot] = current;

            if (current >= 2)
                duplicates.Add(slot);
        }

        return duplicates;
    }

    private void RefreshCashTexts()
    {
        if (boxCashText != null)
        {
            int boxCash = currentBox != null ? currentBox.cash : 0;
            boxCashText.text = $"Cash: {boxCash:n0}$";
        }

        if (playerCashText != null)
        {
            if (playerStats == null)
                playerStats = FindFirstObjectByType<PlayerStats>();

            int playerCash = playerStats != null ? playerStats.money : 0;
            playerCashText.text = $"Cash: {playerCash:n0}$";
        }
    }

    private void TryOpenCashTransferBoxToPlayer()
    {
        if (currentBox == null) return;
        if (currentBox.cash <= 0) return;

        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();

        if (playerStats == null) return;

        ItemAmountDialog dialog = amountDialog != null ? amountDialog : ItemAmountDialog.Instance;
        if (dialog == null) return;

        int max = currentBox.cash;

        dialog.Open(
            "TRANSFER CASH TO PLAYER",
            1,
            max,
            max,
            amount =>
            {
                amount = Mathf.Clamp(amount, 1, currentBox.cash);

                currentBox.cash -= amount;

                if (InventoryUI.Instance != null)
                    InventoryUI.Instance.ApplyMoneyChange(amount);
                else
                    playerStats.SetMoney(playerStats.money + amount);

                RefreshCashTexts();
                RefreshCapacityTexts();
            }
        );
    }

    private void TryOpenCashTransferPlayerToBox()
    {
        if (currentBox == null) return;

        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();
        if (playerStats == null) return;
        if (playerStats.money <= 0) return;

        ItemAmountDialog dialog = amountDialog != null ? amountDialog : ItemAmountDialog.Instance;
        if (dialog == null) return;

        int max = playerStats.money;

        dialog.Open(
            "TRANSFER CASH TO BOX",
            1,
            max,
            max,
            amount =>
            {
                amount = Mathf.Clamp(amount, 1, playerStats.money);

                currentBox.cash += amount;

                if (InventoryUI.Instance != null)
                    InventoryUI.Instance.ApplyMoneyChange(-amount);
                else
                    playerStats.SetMoney(playerStats.money - amount);

                RefreshCashTexts();
                RefreshCapacityTexts();
            }
        );
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
        if (currentBox == null) return false;
        if (targetSlot == null || targetSlot.item == null) return false;
        if (InventoryUI.draggedItem == null || InventoryUI.draggedItem.data == null) return false;
        if (ReferenceEquals(targetSlot.item, InventoryUI.draggedItem)) return false;

        InventoryItemInstance sourceItem = InventoryUI.draggedItem;
        InventoryItemInstance targetItem = targetSlot.item;

        if (!InventoryUI.CanMergeStacks(sourceItem, targetItem))
            return false;

        IInventorySlotOwner sourceOwner = InventoryUI.DragSourceOwner;
        if (sourceOwner == null) return false;

        int maxAmount = sourceItem.count;
        if (maxAmount <= 0) return false;

        bool useDialog =
            InventoryUI.IsStackSplitModifierHeld() &&
            InventoryUI.CanSplitStack(sourceItem);

        if (useDialog)
        {
            ItemAmountDialog dialog = ItemAmountDialog.Instance;
            if (dialog == null) return true;

            int startValue = Mathf.CeilToInt(maxAmount / 2f);

            ClearDrag();

            dialog.Open(
                $"MERGE {sourceItem.data.itemName}",
                1,
                maxAmount,
                startValue,
                amount =>
                {
                    amount = Mathf.Clamp(amount, 1, maxAmount);
                    MergeStackAmountToBoxTarget(sourceOwner, sourceItem, targetItem, amount);
                },
                cancel: null
            );

            return true;
        }

        MergeStackAmountToBoxTarget(sourceOwner, sourceItem, targetItem, maxAmount);

        ClearDrag();
        return true;
    }

    private void MergeStackAmountToBoxTarget(
      IInventorySlotOwner sourceOwner,
      InventoryItemInstance sourceItem,
      InventoryItemInstance targetItem,
      int amount)
    {
        if (currentBox == null) return;
        if (sourceOwner == null || sourceItem == null || targetItem == null) return;

        amount = Mathf.Clamp(amount, 1, sourceItem.count);

        if (!sourceOwner.RemoveStackAmountFromOwner(sourceItem, amount))
            return;

        targetItem.count += amount;

        RefreshCountDisplay(sourceItem);
        RefreshCountDisplay(targetItem);
        RefreshCapacityTexts();

        if (InventoryUI.Instance != null)
            InventoryUI.Instance.RefreshGunUIFromWeaponManager();
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
}