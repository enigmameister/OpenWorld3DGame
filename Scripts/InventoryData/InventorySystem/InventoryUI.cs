using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InventoryUI : MonoBehaviour, IInventorySlotOwner
{
    private InventoryGridController grid;
    private InventoryDragController dragController;
    private InventoryDropService dropService;
    private InventoryCashController cashController;
    private InventoryWeaponBridge weaponBridge;

    public static InventoryUI Instance { get; private set; }
    public static bool IsInventoryOpen { get; private set; }
    public static bool IsDraggingInventoryItem { get; private set; }
    public static InventoryItemInstance draggedItem;

    public static IInventorySlotOwner DragSourceOwner;
    public static InventorySlot DragSourceSlot;

    [Header("Refs")]
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private PlayerStats playerStats;

    [Header("World drop")]
    [SerializeField] private LayerMask dropObstacleMask;

    [Header("Root")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private RectTransform inventoryRoot;
    [SerializeField] private RectTransform dragArea;

    [Header("Slots")]
    [SerializeField] private GameObject slotsParent;
    [SerializeField] private GameObject unlockedSlotPrefab;
    [SerializeField] private GameObject lockedSlotPrefab;
    [SerializeField, Range(0, 40)] private int unlockedSlotCount = 16;
    [SerializeField] private int totalSlots = 30;
    [SerializeField] private int slotsPerPage = 20;
    [SerializeField] private int slotsPerRow = 4;

    [Header("Tabs")]
    [SerializeField] private Button page1Button;
    [SerializeField] private Button page2Button;

    [Header("UI")]
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI cashText;
    [SerializeField] private Image dragGhost;

    [Header("Placement Preview")]
    [SerializeField] private Color placementValidColor = new Color(1f, 0.78f, 0.05f, 0.45f);
    [SerializeField] private Color placementInvalidColor = new Color(1f, 0.05f, 0.05f, 0.45f);

    [Header("Drag Ghost")]
    [SerializeField] private float dragGhostCellSize = 45f;
    [SerializeField] private float dragGhostScale = 0.9f;

    private InventorySlot lastPreviewStartSlot;

    [Header("Money Drop UI")]
    [SerializeField] private Button moneyDropButton;
    [SerializeField] private MoneyDropDialog moneyDropDialog;
    [SerializeField] private MoneyDropSpawner moneyDropSpawner;
    [SerializeField] private ItemAmountDialog amountDialog;

    [Header("Preview")]
    [SerializeField] private InventoryCharacterPreview characterPreview;
    [SerializeField] private Button turnLeftButton;
    [SerializeField] private Button turnRightButton;
    [SerializeField] private Button centerButton;

    // ---- runtime/state ----
    private readonly List<InventorySlot> slotList = new();
    private int currentPage;
    private float fadeSpeed = 10f;
    private readonly float[] currentAlphas = new float[2];


    private Vector2 dragOffset;
    public static int DraggedGrabCellOffset { get; private set; }
    public static int DraggedGrabCellOffsetY { get; private set; }

    private bool isDraggingWindow;
    private Vector2 defaultPosition;

    private InventorySlot draggedSlot;
    private int draggedOriginSlotIndex = -1;

    private ATMUIController atmUI;
    private BankDialogueUI bankDialogueUI;
    private MouseLook mouseLook;
    private GunUI gunUI;
    private RectTransform slotsParentRect;
    private Transform cachedPlayerTransform;
    private Camera cachedMainCamera;

    private PointerEventData cachedPointerData;
    private readonly List<RaycastResult> uiRaycastResults = new();

    private void Awake() => InitSingleton();

    private void Start()
    {
        CacheRefs();

        InitUI();
        InitControllers();
        InitButtons();
    }

    private void Update()
    {
        // ✅ ESC ma priorytet (działa nawet jeśli ATM jest open)
        bool escPressed =
#if ENABLE_INPUT_SYSTEM
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
#else
        Input.GetKeyDown(KeyCode.Escape);
#endif

        if (CarRaceManager.IsRaceLoading)
            return;

        if (IsInventoryOpen && escPressed)
        {
            bool itemDialogOpen =
                (amountDialog != null && amountDialog.IsOpen) ||
                (ItemAmountDialog.Instance != null && ItemAmountDialog.Instance.IsOpen);

            bool oldMoneyDialogOpen =
                (moneyDropDialog != null && moneyDropDialog.IsOpen);

            if (itemDialogOpen)
            {
                if (amountDialog != null && amountDialog.IsOpen)
                    amountDialog.Close();
                else
                    ItemAmountDialog.Instance.Close();
            }
            else if (oldMoneyDialogOpen)
            {
                moneyDropDialog.Close();
            }
            else
            {
                ToggleInventory();
            }

            return;
        }

        // dopiero po ESC blokuj resztę logiki
        if (atmUI != null && atmUI.IsOpen)
            return;

        // ✅ Toggle Inventory na "I" (z Twojego input handlera)
        if (!DevConsole.IsOpen && PlayerInputHandler.Instance != null && PlayerInputHandler.Instance.InventoryPressed)
            ToggleInventory();

        if (!IsInventoryOpen) return;

        UpdateTabAlphaSmooth();
        HandleWindowDrag();

        dragController?.UpdateGhostPosition();
        dragController?.HandleRotationInput();

        if (draggedItem != null)
        {
            InventorySlot hoverSlot = GetInventorySlotUnderMouse();

            // Ważne: InventoryUI ma pokazywać preview tylko na swoich slotach,
            // ale źródłem draga może być InventoryUI albo BoxInventoryUI.
            bool hoverInventorySlot =
                hoverSlot != null &&
                hoverSlot.owner == this;

            if (hoverInventorySlot)
            {
                int previewStartIndex =
                    hoverSlot.slotIndex
                    - DraggedGrabCellOffset
                    - (DraggedGrabCellOffsetY * slotsPerRow);

                if (hoverSlot != lastPreviewStartSlot)
                {
                    lastPreviewStartSlot = hoverSlot;
                    PreviewPlacement(previewStartIndex, draggedItem);
                }
            }
            else
            {
                ClearPlacementPreview();
            }
        }
        else
        {
            ClearPlacementPreview();
        }

        // reader podczas przeciągania key itemów
        if (draggedItem != null)
        {
            if (cachedMainCamera == null)
                cachedMainCamera = Camera.main;

            var cam = cachedMainCamera;
            if (cam != null)
            {
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 5f))
                {
                    var reader = hit.collider.GetComponent<ReaderZone>();
                    if (reader != null && draggedItem.data.isKeyItem)
                    {
                        reader.onActivatedExternally = () =>
                        {
                            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
                            draggedItem = null;
                            draggedSlot = null;
                        };
                        reader.TryActivateWithItem(draggedItem);
                    }
                }
            }
        }

        // klik poza oknem -> drop przeciąganego itemu
        if (IsInventoryOpen && Input.GetMouseButtonDown(0))
        {
            if (ItemAmountDialog.Instance != null && ItemAmountDialog.Instance.IsOpen)
                return;

            if (cachedPointerData == null)
                cachedPointerData = new PointerEventData(EventSystem.current);

            cachedPointerData.position = Input.mousePosition;

            uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(cachedPointerData, uiRaycastResults);

            bool overUI = false;

            foreach (var r in uiRaycastResults)
            {
                Transform hitTransform = r.gameObject.transform;

                bool overInventory =
                    inventoryPanel != null &&
                    hitTransform.IsChildOf(inventoryPanel.transform);

                bool overBox =
                    BoxInventoryUI.Instance != null &&
                    BoxInventoryUI.Instance.IsOpen &&
                    BoxInventoryUI.Instance.ContainsTransform(hitTransform);

                if (overInventory || overBox)
                {
                    overUI = true;
                    break;
                }
            }

            if (!overUI && draggedItem != null)
            {
                TryDropDraggedItemToWorldWithDialog();
                return;
            }
        }
    }

    private void InitControllers()
    {
        InitDropService();
        InitCashController();
        InitWeaponBridge();
    }


    void OnEnable() => RefreshCashUI();

    private void InitSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void CacheRefs()
    {
        if (playerStats == null) playerStats = FindFirstObjectByType<PlayerStats>();

        if (weaponManager == null && playerStats != null)
            weaponManager = playerStats.GetComponentInChildren<WeaponManager>();

        if (moneyDropSpawner == null) moneyDropSpawner = FindFirstObjectByType<MoneyDropSpawner>();

        if (atmUI == null) atmUI = FindFirstObjectByType<ATMUIController>();

        if (bankDialogueUI == null) bankDialogueUI = FindFirstObjectByType<BankDialogueUI>();

        if (mouseLook == null) mouseLook = FindFirstObjectByType<MouseLook>();

        if (gunUI == null) gunUI = FindFirstObjectByType<GunUI>();

        if (amountDialog == null) amountDialog = ItemAmountDialog.Instance;

        if (Camera.main != null) cachedMainCamera = Camera.main;

        if (slotsParent != null) slotsParentRect = slotsParent.GetComponent<RectTransform>();
    }

    private void InitUI()
    {
        if (dragGhost != null)
        {
            dragGhost.gameObject.SetActive(false);
            dragGhost.raycastTarget = false;
            dragGhost.transform.SetAsLastSibling();
        }

        if (inventoryPanel == gameObject && panelGroup == null)
            panelGroup = inventoryPanel.GetComponent<CanvasGroup>() ?? inventoryPanel.AddComponent<CanvasGroup>();

        defaultPosition = inventoryRoot.anchoredPosition;

        GenerateSlots(totalSlots);
        InitGridController();
        InitDragController();

        ShowPage(0);
        ShowPanel(false);

        currentAlphas[0] = 1f;
        currentAlphas[1] = 1f;
    }

    private void InitGridController()
    {
        grid = new InventoryGridController(
            slotList,
            slotsPerRow,
            placementValidColor,
            placementInvalidColor,
            () => draggedItem,
            () => IsDraggingInventoryItem
        );
    }

    private void InitDragController()
    {
        dragController = new InventoryDragController(
            dragGhost,
            dragGhostCellSize,
            dragGhostScale,
            () => draggedItem,
            SetDraggingVisualForItem,
            ClearPlacementPreview,
            () =>
            {
                if (BoxInventoryUI.Instance != null && BoxInventoryUI.Instance.IsOpen)
                    BoxInventoryUI.Instance.ClearPlacementPreviewExternal();
            },
            RefreshOccupiedHighlights
        );
    }

    private void InitDropService()
    {
        dropService = new InventoryDropService(
            this,
            dropObstacleMask,
            () =>
            {
                if (playerStats == null)
                    playerStats = FindFirstObjectByType<PlayerStats>();

                return playerStats;
            }
        );
    }

    private void InitCashController()
    {
        cashController = new InventoryCashController(
            this,
            () =>
            {
                if (playerStats == null)
                    playerStats = FindFirstObjectByType<PlayerStats>();

                return playerStats;
            },
            moneyText,
            cashText
        );

        cashController.Init();
    }

    private void InitWeaponBridge()
    {
        weaponBridge = new InventoryWeaponBridge(
            () =>
            {
                if (weaponManager == null)
                    weaponManager = FindFirstObjectByType<WeaponManager>();

                return weaponManager;
            },
            () =>
            {
                if (gunUI == null)
                    gunUI = FindFirstObjectByType<GunUI>();

                return gunUI;
            },
            BuildOwnedItemsForGunUI
        );
    }

    private void InitButtons()
    {
        closeButton?.onClick.AddListener(ToggleInventory);

        page1Button?.onClick.AddListener(() => ShowPage(0));
        page2Button?.onClick.AddListener(() => ShowPage(1));

        if (characterPreview != null)
        {
            AddHold(turnLeftButton, () => characterPreview.HoldLeft_On(true), () => characterPreview.HoldLeft_On(false));
            AddHold(turnRightButton, () => characterPreview.HoldRight_On(true), () => characterPreview.HoldRight_On(false));
            centerButton?.onClick.AddListener(characterPreview.CenterView);
        }

        if (moneyDropButton != null)
        {
            moneyDropButton.onClick.AddListener(OpenCashDropDialog);
        }
    }

    // Dodaje EventTrigger do przycisku: OnPointerDown → onDown, OnPointerUp/Exit → onUpOrExit
    void AddHold(Button btn, System.Action onDown, System.Action onUpOrExit)
    {
        if (btn == null) return;
        var trig = btn.GetComponent<EventTrigger>();
        if (trig == null) trig = btn.gameObject.AddComponent<EventTrigger>();

        void Add(EventTriggerType type, System.Action call)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => call?.Invoke());
            trig.triggers.Add(entry);
        }

        Add(EventTriggerType.PointerDown, onDown);
        Add(EventTriggerType.PointerUp, onUpOrExit);
        Add(EventTriggerType.PointerExit, onUpOrExit);
    }

    public void ToggleInventory()
    {
       // Debug.Log($"[INV] ToggleInventory called. IsInventoryOpen(before)={IsInventoryOpen} panelActive={inventoryPanel.activeSelf} alpha={(panelGroup ? panelGroup.alpha : -1f)}");

        if (DevConsole.IsOpen && !IsInventoryOpen) return;

        // ✅ nie otwieraj inventory, jeśli trwa dialog NPC
        if (!IsInventoryOpen && bankDialogueUI != null && bankDialogueUI.IsOpen)
            return;

        bool currentlyVisible = (inventoryPanel == gameObject)
            ? (panelGroup != null && panelGroup.alpha > 0.5f)
            : inventoryPanel.activeSelf;

        bool active = !currentlyVisible;

        // Jeżeli zamykamy inventory podczas dragowania,
        // najpierw przywróć wizuale slotów, dopiero potem chowaj panel.
        if (!active)
            ResetDragState();

        IsInventoryOpen = active;

        // okienko
        ShowPanel(active);

        // ✅ agregacja locka: inventory NIE MOŻE zdejmować blokady, jeśli inne UI nadal jest otwarte
        bool atmOpen = atmUI != null && atmUI.IsOpen;
        bool dlgOpen = bankDialogueUI != null && bankDialogueUI.IsOpen;

        // czy jakiekolwiek UI chce kursora/locka
        bool uiWantsLock = active || atmOpen || dlgOpen;

        // blokada rozglądania
        MouseLook.IsLookLocked = uiWantsLock;

        // kursor
        Cursor.visible = uiWantsLock;
        Cursor.lockState = uiWantsLock ? CursorLockMode.None : CursorLockMode.Locked;

        if (active)
        {
            inventoryRoot.anchoredPosition = defaultPosition;
            mouseLook?.ResetLook();

            characterPreview?.OpenPreview();

            RefreshOccupiedHighlights();

            EnsureWeaponManager();

            GameObject curSlotObj = null;
            Gun gun = null;

            if (weaponManager != null &&
                weaponManager.GetWeaponSlots() != null &&
                weaponManager.GetWeaponSlots().Length > 0)
            {
                curSlotObj = weaponManager.GetCurrentWeaponSlotObject();
                if (curSlotObj != null)
                    gun = curSlotObj.GetComponentInChildren<Gun>(true);
            }

            if (gun != null)
            {
                if (gun.IsReloading)
                    gun.SendMessage("StopReload", SendMessageOptions.DontRequireReceiver);

                gun.SendMessage("ForceExitADS", SendMessageOptions.DontRequireReceiver);
            }
        }

        else
        {

            if (BoxInventoryUI.Instance != null && BoxInventoryUI.Instance.IsOpen)
                BoxInventoryUI.Instance.Close();

            moneyDropDialog?.Close();

            if (amountDialog != null && amountDialog.IsOpen)
                amountDialog.Close();
            else if (ItemAmountDialog.Instance != null && ItemAmountDialog.Instance.IsOpen)
                ItemAmountDialog.Instance.Close();

            foreach (var slot in slotList) slot.tooltip?.Hide();
            characterPreview?.ClosePreview();
        }
    }

    // ─── Usunięto LateUpdate/FreezeCamera/UnfreezeCamera i wszystkie zmienne od kamery ───

    private void ResetDragState()
    {
        InventoryItemInstance itemToRestore = draggedItem;

        if (itemToRestore != null)
            SetDraggingVisualForItem(itemToRestore, false);

        if (DragSourceSlot != null && DragSourceSlot.item != null)
            SetDraggingVisualForItem(DragSourceSlot.item, false);

        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);

        dragController?.ResetPointerOffset();
        SetDraggedGrabCellOffset(0, 0);

        ClearPlacementPreview();

        draggedSlot = null;

        ClearSharedDragState();

        RebuildSlotVisualsFromCurrentState();
        RebuildSlotsLayout();
        RefreshOccupiedHighlights();
    }

    private void FinishInventoryDrag()
    {
        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);

        dragController?.ResetPointerOffset();
        SetDraggedGrabCellOffset(0, 0);

        draggedItem = null;
        draggedSlot = null;
        DragSourceOwner = null;
        DragSourceSlot = null;
        IsDraggingInventoryItem = false;

        ClearPlacementPreview();

        RebuildSlotVisualsFromCurrentState();
        RebuildSlotsLayout();
        RefreshOccupiedHighlights();
    }

    private void HandleWindowDrag()
    {
        if (!IsInventoryOpen) return;

        if (Input.GetMouseButtonDown(0))
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragArea, Input.mousePosition, null, out var localMousePos);

            if (dragArea.rect.Contains(localMousePos))
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    inventoryRoot.parent as RectTransform, Input.mousePosition, null, out var pointerPos);

                dragOffset = pointerPos - inventoryRoot.anchoredPosition;
                isDraggingWindow = true;
            }
        }

        if (Input.GetMouseButtonUp(0))
            isDraggingWindow = false;

        if (isDraggingWindow)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                inventoryRoot.parent as RectTransform, Input.mousePosition, null, out var pointerPos);

            inventoryRoot.anchoredPosition = pointerPos - dragOffset;
        }
    }


    public void GenerateSlots(int total)
    {
        foreach (Transform child in slotsParent.transform)
            Destroy(child.gameObject);

        slotList.Clear();

        for (int i = 0; i < total; i++)
        {
            GameObject prefab = i < unlockedSlotCount ? unlockedSlotPrefab : lockedSlotPrefab;
            GameObject slotGO = Instantiate(prefab, slotsParent.transform);
            InventorySlot slot = slotGO.GetComponent<InventorySlot>();
            slot.owner = this;
            slotList.Add(slot);
            slot.slotIndex = i;
        }

        if (grid != null)
            grid.SetSlotsPerRow(slotsPerRow);
    }

    public bool TryAddItem(InventoryItemInstance instance)
    {
        if (instance == null || instance.data == null)
            return false;

        if (instance.data is AmmoItemData a && a.individualMagazines)
        {
            int payload = Mathf.Max(0, Mathf.Max(instance.totalAmmo, instance.currentAmmo));
            instance.totalAmmo = payload;
            instance.currentAmmo = payload;
            instance.count = Mathf.Max(1, instance.count);
        }

        // Stack tylko gdy to nie jest indywidualny magazynek i nie jest karta bankowa.
        bool isBankCard = instance.data is BankCardItemData || instance.hasBankCardMeta;

        bool canStack =
            !(instance.data is AmmoItemData ammo && ammo.individualMagazines) &&
            !isBankCard;

        if (canStack)
        {
            foreach (var slot in slotList)
            {
                if (slot == null || slot.item == null)
                    continue;

                if (InventoryStackService.CanMergeStacks(instance, slot.item))
                {
                    int addAmount = Mathf.Max(1, instance.count);

                    slot.item.count += addAmount;
                    slot.UpdateCountDisplay();

                    if (instance.data is GrenadeItemData)
                    {
                        if (weaponBridge == null) InitWeaponBridge();
                        weaponBridge?.SyncGrenadeSlotFromInventory(instance.data);
                    }

                    RefreshGunUIFromWeaponManager();
                    RefreshOccupiedHighlights();

                    return true;
                }
            }
        }

        // Auto-placement 2D:
        // TryAddItemAt() samo sprawdza WidthSlots / HeightSlots / rotated.
        for (int index = 0; index < slotList.Count; index++)
        {
            if (TryAddItemAt(index, instance))
            {
                RebuildSlotsLayout();
                RefreshOccupiedHighlights();
                return true;
            }
        }

        return false;
    }

    public void DropItem(InventoryItemInstance instance)
    {
        if (instance == null || instance.data == null) return;

        // ✅ BANK CARD DROP (osobna ścieżka)
        if (instance.data is BankCardItemData)
        {
            SpawnBankCard(instance);
            RemoveItem(instance, 1);
            return;
        }

        if (weaponManager != null && weaponManager.weaponSlots != null)
        {
            int index = weaponManager.GetWeaponIndex(instance);
            if (index >= 0) { weaponManager.DropWeapon(index); return; }
        }

        int cur = instance.currentAmmo;
        int tot = instance.totalAmmo;
        if (instance.data is AmmoItemData)
        {
            int mag = Mathf.Max(0, instance.totalAmmo); // ← tylko totalAmmo
            cur = tot = mag;
        }

        SpawnPickup(instance, cur, tot);
        RemoveItem(instance, 1);
    }

    private void SpawnPickup(InventoryItemData data, int currentAmmo = -1, int totalAmmo = -1)
    {
        if (dropService == null)
            InitDropService();

        dropService?.SpawnPickup(data, currentAmmo, totalAmmo);
    }
    private void SpawnPickup(InventoryItemInstance source, int currentAmmo = -1, int totalAmmo = -1)
    {
        if (dropService == null)
            InitDropService();

        dropService?.SpawnPickup(source, currentAmmo, totalAmmo);
    }

    private void SpawnBankCard(InventoryItemInstance instance)
    {
        if (dropService == null)
            InitDropService();

        dropService?.SpawnBankCard(instance);
    }

    private void SpawnDraggedPickupOnly(InventoryItemInstance instance)
    {
        if (dropService == null)
            InitDropService();

        dropService?.SpawnDraggedPickupOnly(instance);
    }


    public bool RemoveItem(InventoryItemInstance instance, int amount)
    {
        if (instance == null || amount <= 0) return false;

        int newCount = instance.count - amount;

        if (newCount > 0)
        {
            instance.count = newCount;
            for (int i = 0; i < slotList.Count; i++)
            {
                var slot = slotList[i];
                if (slot.item == instance)
                {
                    slot.UpdateCountDisplay();
                    break;
                }
            }
            return true;
        }

        int size = instance.data.slotSize;
        for (int i = 0; i <= slotList.Count - size; i++)
        {
            var start = slotList[i];
            bool matches = (start.isOccupied && start.item == instance);

            if (!matches) continue;

            for (int j = 0; j < size; j++)
            {
                var s = slotList[i + j];
                s.isOccupied = false;
                s.item = null;

                if (s.countText != null)
                {
                    s.countText.text = "";
                    s.countText.gameObject.SetActive(false);
                }

                if (s.fillImage != null) s.fillImage.SetActive(true);
                if (s.borderImage != null) s.borderImage.SetActive(true);
                if (s.iconImage != null)
                {
                    s.iconImage.enabled = false;
                    s.iconImage.sprite = null;
                }

                s.transform.localScale = Vector3.one;
            }

            instance.count = 0;
            RefreshOccupiedHighlights();
            return true;
        }

        for (int i = 0; i < slotList.Count; i++)
        {
            var s = slotList[i];
            if (s.isOccupied && s.item == instance)
            {
                s.isOccupied = false;
                s.item = null;

                s.ClearOccupiedHighlight();
                s.ClearPlacementPreview();

                if (s.countText != null)
                {
                    s.countText.text = "";
                    s.countText.gameObject.SetActive(false);
                }

                if (s.fillImage != null) s.fillImage.SetActive(true);
                if (s.borderImage != null) s.borderImage.SetActive(true);
                if (s.iconImage != null)
                {
                    s.iconImage.enabled = false;
                    s.iconImage.sprite = null;
                }

                s.transform.localScale = Vector3.one;
            }
        }

        instance.count = 0;
        RefreshOccupiedHighlights();
        return true;
    }

    public bool RemoveItem(InventoryItemInstance instance) => RemoveItem(instance, 1);


    public InventoryItemInstance FindKeyItem(string keyId)
    {
        if (string.IsNullOrEmpty(keyId)) return null;

        foreach (var slot in slotList)
        {
            var item = slot.item;
            if (item == null || item.data == null) continue;

            if (item.data.isKeyItem && item.data.keyId == keyId)
                return item;
        }

        return null;
    }

    public InventoryItemInstance GetInstanceForItem(InventoryItemData data)
    {
        foreach (var slot in slotList)
        {
            if (slot.item == null) continue;

            // ❌ nie dla kart
            if (slot.item.data is BankCardItemData)
                continue;

            if (slot.item.data == data)
                return slot.item;
        }
        return null;
    }

    public InventorySlot GetSlotForInstance(InventoryItemInstance instance)
    {
        foreach (var slot in slotList)
        {
            if (slot != null && slot.item == instance)
                return slot;
        }
        return null;
    }

    void ShowPage(int pageIndex)
    {
        currentPage = pageIndex;
        slotsPerPage = Mathf.Min(slotsPerPage, 20);

        int start = pageIndex * slotsPerPage;
        int end = Mathf.Min(start + slotsPerPage, slotList.Count);

        for (int i = 0; i < slotList.Count; i++)
            slotList[i].gameObject.SetActive(i >= start && i < end);

        bool hasSecondPage = unlockedSlotCount > slotsPerPage;
        page2Button.gameObject.SetActive(hasSecondPage);

        if (currentPage == 1 && !hasSecondPage)
            ShowPage(0);
    }

    void ShowPanel(bool show)
    {
        if (inventoryPanel == gameObject)
        {
            if (panelGroup == null)
                panelGroup = inventoryPanel.GetComponent<CanvasGroup>() ?? inventoryPanel.AddComponent<CanvasGroup>();

            panelGroup.alpha = show ? 1f : 0f;
            panelGroup.blocksRaycasts = show;
            panelGroup.interactable = show;
        }
        else
        {
            inventoryPanel.SetActive(show);
        }
    }

    void UpdateTabAlphaSmooth()
    {
        Image img1 = page1Button.GetComponent<Image>();
        Image img2 = page2Button.GetComponent<Image>();

        float target1 = currentPage == 0 ? 1f : 0f;
        float target2 = currentPage == 1 ? 1f : 0f;

        currentAlphas[0] = Mathf.Lerp(currentAlphas[0], target1, Time.deltaTime * fadeSpeed);
        currentAlphas[1] = Mathf.Lerp(currentAlphas[1], target2, Time.deltaTime * fadeSpeed);

        if (img1 != null)
        {
            Color c = img1.color; c.a = currentAlphas[0]; img1.color = c;
        }
        if (img2 != null)
        {
            Color c = img2.color; c.a = currentAlphas[1]; img2.color = c;
        }
    }

    public void UnlockNextSlot()
    {
        if (unlockedSlotCount >= totalSlots) return;

        unlockedSlotCount++;

        for (int i = 0; i < slotList.Count; i++)
        {
            if (slotList[i].CompareTag("LockedSlot"))
            {
                Destroy(slotList[i].gameObject);

                GameObject newSlotGO = Instantiate(unlockedSlotPrefab, slotsParent.transform);
                newSlotGO.transform.SetSiblingIndex(i);

                InventorySlot newSlot = newSlotGO.GetComponent<InventorySlot>();
                slotList[i] = newSlot;
                break;
            }
        }
        ShowPage(currentPage);
    }

    public void UpdateStackDisplay(InventoryItemInstance instance)
    {
        foreach (var slot in slotList)
            if (slot.item == instance) slot.UpdateCountDisplay();
    }

    public void RefreshCountDisplay(InventoryItemInstance instance)
    {
        foreach (var slot in slotList)
            if (slot.item == instance) slot.UpdateCountDisplay();
    }

    public void OnSlotClicked(InventorySlot clickedSlot)
    {

        int clickedIndex = slotList.IndexOf(clickedSlot);
        if (clickedIndex < 0) return;

        int placeIndex = clickedIndex;

        if (draggedItem != null)
        {
            placeIndex -= DraggedGrabCellOffset;
            placeIndex -= DraggedGrabCellOffsetY * slotsPerRow;
        }

        // 1) klik na źródłowy slot -> anuluj drag i odbuduj item 1x2/1x3
        if (draggedItem != null && draggedSlot == clickedSlot)
        {
            FinishInventoryDrag();
            return;
        }

        // 2) DROP NA ZAJĘTY SLOT (np. ammo -> broń)
        if (draggedItem != null && clickedSlot.isOccupied && clickedSlot != draggedSlot)
        {
            // Jeśli kliknięty slot należy do TEGO SAMEGO przeciąganego itemu,
            // nie traktuj tego jako kolizji. Pozwól spróbować położyć item od tego indeksu.
            bool clickedOwnDraggedItem =
                ReferenceEquals(DragSourceOwner, this) &&
                clickedSlot.item == draggedItem;

            if (!clickedOwnDraggedItem)
            {
                if (TryMergeDraggedStackIntoInventorySlot(clickedSlot))
                    return;

                if (draggedItem.data is AmmoItemData ammo &&
                    clickedSlot.item?.data is WeaponItemData wd &&
                    IsCompatible(ammo, wd))
                {
                    if (TryApplyAmmoToWeapon(draggedItem, clickedSlot.item))
                    {
                        SetDraggingVisualForItem(draggedItem, false);

                        if (dragGhost != null)
                            dragGhost.gameObject.SetActive(false);

                        draggedItem = null;
                        draggedSlot = null;
                        InventoryUI.DragSourceOwner = null;
                        InventoryUI.DragSourceSlot = null;
                        IsDraggingInventoryItem = false;

                        ClearPlacementPreview();
                        RefreshOccupiedHighlights();

                        return;
                    }
                }

                // Zajęty slot innym itemem — nie kasuj draga, tylko zostaw go w ręce.
                return;
            }

            // Jeżeli to własny stary slot tego samego itemu, przechodzimy dalej do dropu.
        }

        // 3) DROP NA SLOT.
        // Slot może być pusty albo może należeć do tego samego przeciąganego itemu.
        if (draggedItem != null)
        {
            if (clickedIndex < 0) return;
            if (slotList[clickedIndex].CompareTag("LockedSlot")) return;

            if (InventoryUI.DragSourceOwner is BoxInventoryUI && IsCombatItemData(draggedItem.data))
            {
                if (!CanReceiveWeaponFromBox(draggedItem))
                {
                    HideDragGhost();
                    ClearSharedDragState();
                    draggedSlot = null;

                    BoxInventoryUI.Instance?.ShowTransferMessagePublic("ALREADY WEAPON THIS TYPE");
                    return;
                }
            }

            if (!CanPlaceDraggedItemAt(placeIndex, draggedItem))
                return;

            IInventorySlotOwner sourceOwner = InventoryUI.DragSourceOwner;

            bool wantsSplit =
                InventoryStackService.IsStackSplitModifierHeld() &&
                InventoryStackService.CanSplitStack(draggedItem);

            // SHIFT + split stacka
            if (wantsSplit)
            {
                ItemAmountDialog dialog = ItemAmountDialog.Instance;
                if (dialog == null) return;

                int targetIndex = placeIndex;
                InventoryItemInstance sourceItem = draggedItem;
                IInventorySlotOwner splitSource = sourceOwner;

                bool sameOwner = ReferenceEquals(splitSource, this);

                int maxAmount = sameOwner
                    ? Mathf.Max(1, sourceItem.count - 1)
                    : sourceItem.count;

                if (maxAmount <= 0)
                    return;

                if (sourceItem != null)
                    SetDraggingVisualForItem(sourceItem, false);

                HideDragGhost();

                DraggedGrabCellOffset = 0;
                DraggedGrabCellOffsetY = 0;

                ClearPlacementPreview();

                draggedSlot = null;
                ClearSharedDragState();

                RebuildSlotVisualsFromCurrentState();
                RebuildSlotsLayout();
                RefreshOccupiedHighlights();

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

                        if (!CanFitShape(targetIndex, GetItemWidth(part), GetItemHeight(part), slotsPerRow))
                            return;

                        if (splitSource != null && splitSource.RemoveStackAmountFromOwner(sourceItem, amount))
                        {
                            TryAddItemAt(targetIndex, part);

                            RebuildSlotVisualsFromCurrentState();
                            RebuildSlotsLayout();
                            RefreshCountDisplay(sourceItem);
                            RefreshCountDisplay(part);
                            RefreshOccupiedHighlights();
                            RefreshGunUIFromWeaponManager();
                        }
                    },
                    cancel: null
                );

                return;
            }

            // Normalne przeniesienie całego itemu/stacka
            InventoryItemInstance movedItem = draggedItem;

            if (ReferenceEquals(sourceOwner, this))
            {
                ForceRemoveItemCompletely(movedItem);

                if (!TryAddItemAt(placeIndex, movedItem))
                {
                    TryAddItemAt(draggedOriginSlotIndex, movedItem);
                    FinishInventoryDrag();
                    return;
                }
            }
            else
            {
                if (TryAddItemAt(placeIndex, movedItem))
                {
                    bool removedFromSource = sourceOwner?.RemoveItemFromOwner(movedItem) ?? false;

                    if (removedFromSource && IsCombatItemData(movedItem.data))
                    {
                        if (!RegisterWeaponFromBoxTransfer(movedItem))
                        {
                            ForceRemoveItemCompletely(movedItem);
                            RefreshGunUIFromWeaponManager();

                            FinishInventoryDrag();
                            return;
                        }
                    }
                }
                else
                {
                    return;
                }
            }

            FinishInventoryDrag();
            return;
        }

        if (draggedItem == null && clickedSlot.isOccupied && clickedSlot.item != null)
        {
            draggedItem = clickedSlot.item;
            draggedSlot = clickedSlot;
            draggedOriginSlotIndex = clickedIndex;

            InventoryUI.DragSourceOwner = this;
            InventoryUI.DragSourceSlot = clickedSlot;

            // WAŻNE: najpierw stan drag, dopiero potem ShowDragGhost(),
            // bo ShowDragGhost() odpala RefreshOccupiedHighlights().
            IsDraggingInventoryItem = true;

            ShowDragGhost(draggedItem, clickedSlot);

            RefreshOccupiedHighlights();
            return;
        }

        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);
    }

    private static bool IsCompatible(AmmoItemData ammo, WeaponItemData weapon)
    {
        if (ammo.weapon == weapon) return true; // referencja
        if (ammo.weapon == null || weapon == null) return false;

        // fallbacki – jeśli masz duplikaty assetów
        if (!string.IsNullOrEmpty(ammo.weapon.itemName) &&
            ammo.weapon.itemName == weapon.itemName) return true;

        if (ammo.weapon.prefab != null && weapon.prefab != null &&
            ammo.weapon.prefab.name == weapon.prefab.name) return true;

        // opcjonalnie: po typie amunicji
        if (!string.IsNullOrEmpty(ammo.weapon.bulletType) &&
            ammo.weapon.bulletType == weapon.bulletType) return true;

        return false;
    }

    private bool TryApplyAmmoToWeapon(InventoryItemInstance ammoInst, InventoryItemInstance weaponInst)
    {
        var ammoData = ammoInst.data as AmmoItemData;
        var weaponData = weaponInst.data as WeaponItemData;

        if (ammoData == null || weaponData == null)
            return false;

        Gun gun = null;

        if (weaponManager != null)
        {
            int slotIndex = weaponManager.GetWeaponIndex(weaponInst);

            if (slotIndex >= 0)
            {
                var slots = weaponManager.GetWeaponSlots();

                if (slots != null && slotIndex < slots.Length && slots[slotIndex] != null)
                    gun = slots[slotIndex].GetComponentInChildren<Gun>(true);
            }
        }

        int cap = weaponData.magazineSize * 3;
        int reserve = gun != null ? gun.GetTotalAmmo() : weaponInst.totalAmmo;
        int free = Mathf.Max(0, cap - reserve);

        if (free <= 0)
            return false;

        // Dostępna zawartość tylko z tego jednego magazynka/paczki.
        int available = ammoData.individualMagazines
            ? Mathf.Max(0, ammoInst.totalAmmo)
            : Mathf.Max(1, ammoData.amountPerUnit);

        int add = Mathf.Min(free, available);

        if (add <= 0)
            return false;

        void ConsumeFromMag(int taken)
        {
            if (ammoData.individualMagazines)
            {
                ammoInst.totalAmmo -= taken;

                if (ammoInst.totalAmmo < 0)
                    ammoInst.totalAmmo = 0;

                ammoInst.currentAmmo = ammoInst.totalAmmo;

                if (ammoInst.totalAmmo <= 0)
                {
                    // Pusty magazynek wyrzucany jako +0.
                    SpawnPickup(ammoInst.data, 0, 0);
                    RemoveItem(ammoInst, 1);
                }
                else
                {
                    // Częściowe zużycie magazynka.
                    RefreshCountDisplay(ammoInst);
                }
            }
            else
            {
                // Stary tryb paczek amunicji.
                RemoveItem(ammoInst, 1);
            }
        }

        // Broń aktywna / istnieje Gun — używamy animacji/paska wkładania ammo.
        if (gun != null)
        {
            int toInsert = add;

            bool started = gun.StartReserveInsert(
                toInsert,
                onApplied: () =>
                {
                    ConsumeFromMag(toInsert);
                    RefreshGunUIFromWeaponManager();
                }
            );

            if (started)
                return true;
        }

        // Broń nieaktywna albo brak Gun — dopisz ammo bezpośrednio do instancji broni.
        weaponInst.totalAmmo = Mathf.Min(cap, weaponInst.totalAmmo + add);

        ConsumeFromMag(add);
        RefreshGunUIFromWeaponManager();

        return true;
    }

    public bool HasAnyItemOnLayer(LayerMask mask)
    {
        if (mask.value == 0) return false;

        foreach (var slot in slotList)
        {
            var inst = slot.item;
            if (inst == null) continue;
            if (inst.data == null) continue;
            if (inst.data.prefab == null) continue;

            int layer = inst.data.prefab.layer;
            if ((mask.value & (1 << layer)) != 0)
                return true;
        }

        return false;
    }



    public List<InventoryItemInstance> GetAllBankCards(bool onlyUsable = false)
    {
        var result = new List<InventoryItemInstance>();
        var seen = new HashSet<InventoryItemInstance>();

        foreach (var slot in slotList)
        {
            var it = slot?.item;
            if (it == null || it.data == null) continue;

            if (!it.hasBankCardMeta) continue;

            if (seen.Add(it))
                result.Add(it);
        }

        if (onlyUsable)
        {
            result = result
                .Where(i => i.hasBankCardMeta && i.bankCard.status != BankCardStatus.Blocked)
                .ToList();
        }

        return result;
    }
    public bool GiveOrDrop(InventoryItemInstance inst)
    {
        if (inst == null) return false;

        if (TryAddItem(inst))
            return true;

        // Nie weszło do inv -> zespawnuj obok gracza
        SpawnDroppedItemNearPlayer(inst);
        return false;
    }

    private void SpawnDroppedItemNearPlayer(InventoryItemInstance inst)
    {
        // Na teraz sensownie obsłużmy bankcard (bo tego potrzebujesz).
        // Inne itemy możesz dodać później analogicznie.
        if (inst.hasBankCardMeta)
        {
            SpawnBankCard(inst);   // <-- masz to już w InventoryUI i to jest OK
            return;
        }

       // Debug.LogWarning($"[INV] GiveOrDrop: no drop handler for item '{inst.data?.name}'.");
    }

    public bool HasBankCardId(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return false;

        var cards = GetAllBankCards(false);
        for (int i = 0; i < cards.Count; i++)
        {
            var it = cards[i];
            if (it != null && it.hasBankCardMeta &&
                string.Equals(it.bankCard.cardId, cardId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public bool RemoveBankCardId(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return false;

        var cards = GetAllBankCards(false);
        for (int i = 0; i < cards.Count; i++)
        {
            var it = cards[i];
            if (it != null && it.hasBankCardMeta &&
                string.Equals(it.bankCard.cardId, cardId, System.StringComparison.OrdinalIgnoreCase))
            {
                RemoveItem(it, it.count <= 0 ? 1 : it.count);
                return true;
            }
        }
        return false;
    }

    public bool TryReceiveItem(InventoryItemInstance item)
    {
        return TryAddItem(item);
    }

    public bool RemoveItemFromOwner(InventoryItemInstance item)
    {
        if (item == null || item.data == null) return false;

        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>();

        // SPECJALNIE DLA GRANATÓW:
        // najpierw usuwamy stack z InventoryUI,
        // potem sprawdzamy czy graczowi zostały jeszcze inne stacki tego samego typu.
        if (item.data is GrenadeItemData)
        {
            ForceRemoveItemCompletely(item);

            if (item.count <= 0)
                item.count = 1;

            if (weaponManager != null)
                weaponManager.SyncGrenadeSlotFromInventory(item.data);

            RefreshGunUIFromWeaponManager();
            return true;
        }

        if (weaponBridge == null)
            InitWeaponBridge();

        weaponBridge?.RemoveCombatItemIfNeeded(item);

        ForceRemoveItemCompletely(item);

        if (item.count <= 0)
            item.count = 1;

        RefreshGunUIFromWeaponManager();

        return true;
    }

    public static void SetInventoryItemDragging(bool value)
    {
        IsDraggingInventoryItem = value;
    }

    public static void ClearSharedDragState()
    {
        draggedItem = null;
        DragSourceOwner = null;
        DragSourceSlot = null;
        IsDraggingInventoryItem = false;
        DraggedGrabCellOffset = 0;
        DraggedGrabCellOffsetY = 0;
    }

    public static void SetDraggedGrabCellOffset(int x, int y)
    {
        DraggedGrabCellOffset = Mathf.Max(0, x);
        DraggedGrabCellOffsetY = Mathf.Max(0, y);
    }

    public int GetUnlockedSlotCount()
    {
        return unlockedSlotCount;
    }


   



    private void DropDraggedItemToWorldDirect(
      InventoryItemInstance sourceItem,
      IInventorySlotOwner sourceOwner)
    {
        if (sourceItem == null || sourceItem.data == null)
            return;

        SpawnDraggedPickupOnly(sourceItem);

        if (sourceOwner != null)
            sourceOwner.RemoveItemFromOwner(sourceItem);
        else
            RemoveItem(sourceItem, 1);
    }

    private void TryDropDraggedItemToWorldWithDialog()
    {
        if (draggedItem == null || draggedItem.data == null)
            return;

        InventoryItemInstance sourceItem = draggedItem;
        IInventorySlotOwner sourceOwner = DragSourceOwner;
        InventorySlot sourceSlot = DragSourceSlot;

        bool useAmountDialog =
            InventoryStackService.CanSplitStack(sourceItem) &&
            sourceItem.count > 1;

        if (!useAmountDialog)
        {
            DropDraggedItemToWorldDirect(sourceItem, sourceOwner);
            FinishInventoryDrag();
            RefreshGunUIFromWeaponManager();
            return;
        }

        ItemAmountDialog dialog = amountDialog != null ? amountDialog : ItemAmountDialog.Instance;

        if (dialog == null)
        {
            DropDraggedItemToWorldDirect(sourceItem, sourceOwner);
            FinishInventoryDrag();
            RefreshGunUIFromWeaponManager();
            return;
        }

        int maxAmount = Mathf.Max(1, sourceItem.count);
        int startAmount = Mathf.CeilToInt(maxAmount / 2f);

        // Przywróć wizual źródła przed dialogiem.
        // Dzięki temu Cancel zostawia stack dokładnie tam, gdzie był.
        if (sourceSlot != null)
            sourceSlot.SetDraggingVisual(false);

        SetDraggingVisualForItem(sourceItem, false);

        HideDragGhost();
        ClearPlacementPreview();

        dragController?.ResetPointerOffset();
        SetDraggedGrabCellOffset(0, 0);

        draggedSlot = null;
        ClearSharedDragState();

        RebuildSlotVisualsFromCurrentState();
        RebuildSlotsLayout();
        RefreshOccupiedHighlights();

        dialog.Open(
            $"DROP {sourceItem.data.itemName}",
            1,
            maxAmount,
            startAmount,
            amount =>
            {
                amount = Mathf.Clamp(amount, 1, maxAmount);

                DropStackAmountToWorld(sourceItem, sourceOwner, amount);

                RebuildSlotVisualsFromCurrentState();
                RebuildSlotsLayout();
                RefreshOccupiedHighlights();
                RefreshGunUIFromWeaponManager();
            },
            cancel: () =>
            {
                RebuildSlotVisualsFromCurrentState();
                RebuildSlotsLayout();
                RefreshOccupiedHighlights();
                RefreshGunUIFromWeaponManager();
            }
        );
    }

    private void DropStackAmountToWorld(
     InventoryItemInstance sourceItem,
     IInventorySlotOwner sourceOwner,
     int amount)
    {
        if (sourceItem == null || sourceItem.data == null)
            return;

        amount = Mathf.Clamp(amount, 1, Mathf.Max(1, sourceItem.count));

        // Spawn fizycznych prefabów 1:1.
        // DROP x2 = dwa osobne prefabry na ziemi.
        for (int i = 0; i < amount; i++)
        {
            InventoryItemInstance singleDrop =
                InventoryStackService.CloneStackPart(sourceItem, 1);

            if (singleDrop == null)
                continue;

            singleDrop.count = 1;

            SpawnDraggedPickupOnly(singleDrop);
        }

        // Z inventory/boxa odejmujemy całą wybraną ilość naraz.
        if (sourceOwner != null)
            sourceOwner.RemoveStackAmountFromOwner(sourceItem, amount);
        else
            RemoveItem(sourceItem, amount);
    }

    private void OpenCashDropDialog()
    {
        // Gdy BoxUI jest otwarty, Coin nie wyrzuca pieniędzy.
        if (BoxInventoryUI.Instance != null && BoxInventoryUI.Instance.IsOpen)
            return;

        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();

        if (moneyDropSpawner == null)
            moneyDropSpawner = FindFirstObjectByType<MoneyDropSpawner>();

        if (playerStats == null || moneyDropSpawner == null)
            return;

        if (playerStats.money <= 0)
            return;

        ItemAmountDialog dialog = amountDialog != null ? amountDialog : ItemAmountDialog.Instance;
        if (dialog == null)
            return;

        int max = playerStats.money;

        dialog.Open(
            "DROP CASH",
            1,
            max,
            max,
            amount =>
            {
                amount = Mathf.Clamp(amount, 1, playerStats.money);

                // Odejmij kasę z UI/gracza.
                ApplyMoneyChange(-amount);

                // Zespawnuj fizyczną gotówkę na ziemi.
                moneyDropSpawner.SpawnCash(amount);

                RefreshCashUI();
            }
        );
    }

    public bool RemoveStackAmountFromOwner(InventoryItemInstance item, int amount)
    {
        if (item == null || amount <= 0) return false;

        amount = Mathf.Clamp(amount, 1, item.count);

        if (amount >= item.count)
            return RemoveItemFromOwner(item);

        item.count -= amount;
        RefreshCountDisplay(item);

        if (item.data is GrenadeItemData && weaponManager != null)
            weaponManager.SyncGrenadeSlotFromInventory(item.data);

        RefreshGunUIFromWeaponManager();

        return true;
    }

    public int GetTotalCountForData(InventoryItemData data)
    {
        if (data == null) return 0;

        int total = 0;
        var seen = new HashSet<InventoryItemInstance>();

        foreach (var slot in slotList)
        {
            var inst = slot?.item;
            if (inst == null || inst.data == null) continue;

            if (!seen.Add(inst))
                continue;

            if (IsSameStackData(inst.data, data))
                total += Mathf.Max(1, inst.count);
        }

        return total;
    }

    public InventoryItemInstance GetFirstInstanceForData(InventoryItemData data)
    {
        if (data == null) return null;

        foreach (var slot in slotList)
        {
            var inst = slot?.item;
            if (inst == null || inst.data == null) continue;

            if (IsSameStackData(inst.data, data))
                return inst;
        }

        return null;
    }

    private bool IsSameStackData(InventoryItemData a, InventoryItemData b)
    {
        if (a == null || b == null) return false;
        if (a == b) return true;

        string aKey = GetItemStackKey(a);
        string bKey = GetItemStackKey(b);

        return !string.IsNullOrEmpty(aKey) &&
               !string.IsNullOrEmpty(bKey) &&
               aKey == bKey;
    }
    private List<InventoryItemInstance> BuildOwnedItemsForGunUI()
    {
        var result = new List<InventoryItemInstance>();
        var grenadeGroups = new Dictionary<string, InventoryItemInstance>();

        foreach (var inst in GetAllInstancesDistinct())
        {
            if (inst == null || inst.data == null)
                continue;

            // GunUI ma dostawać TYLKO broń/melee/granaty.
            // Keycardy, ammo, itemy questowe itd. nie mogą tu trafiać,
            // bo zwykły InventoryItemData domyślnie zwraca WeaponSlot.Riffles.
            if (!IsCombatItemData(inst.data))
                continue;

            if (inst.data is GrenadeItemData)
            {
                string key = GetItemStackKey(inst.data);

                if (!grenadeGroups.TryGetValue(key, out var aggregate))
                {
                    aggregate = new InventoryItemInstance(inst.data, inst.currentAmmo, inst.totalAmmo);
                    aggregate.count = 0;
                    grenadeGroups[key] = aggregate;
                    result.Add(aggregate);
                }

                aggregate.count += Mathf.Max(1, inst.count);
                continue;
            }

            result.Add(inst);
        }

        return result;
    }

    private string GetItemStackKey(InventoryItemData data)
    {
        if (data == null) return "";

        if (data.prefab != null)
            return data.prefab.name;

        if (!string.IsNullOrEmpty(data.itemName))
            return data.itemName;

        return data.name;
    }

    private void RebuildSlotsLayout()
    {
        if (slotsParentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotsParentRect);
    }

    private void EnsureWeaponManager()
    {
        if (weaponManager != null &&
            weaponManager.GetWeaponSlots() != null &&
            weaponManager.GetWeaponSlots().Length > 0)
            return;

        if (playerStats != null)
            weaponManager = playerStats.GetComponentInChildren<WeaponManager>();

        if (weaponManager != null &&
            weaponManager.GetWeaponSlots() != null &&
            weaponManager.GetWeaponSlots().Length > 0)
            return;

        weaponManager = FindFirstObjectByType<WeaponManager>();
    }
    private InventorySlot GetInventorySlotUnderMouse()
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

            // Preview tylko dla slotów gracza, nie Boxa.
            if (slot.owner == this)
                return slot;
        }

        return null;
    }

    private void SetDraggingVisualForItem(InventoryItemInstance item, bool dragging)
    {
        if (item == null)
            return;

        for (int i = 0; i < slotList.Count; i++)
        {
            InventorySlot slot = slotList[i];

            if (slot != null && slot.item == item)
                slot.SetDraggingVisual(dragging);
        }
    }

    // =====================================================
    // Grid Controller Wrappers
    // =====================================================

    private int GetItemWidth(InventoryItemInstance item)
    {
        return grid != null ? grid.GetItemWidth(item) : 1;
    }

    private int GetItemHeight(InventoryItemInstance item)
    {
        return grid != null ? grid.GetItemHeight(item) : 1;
    }

    private bool CanFitShape(int startIndex, int width, int height, int rowSize)
    {
        return grid != null && grid.CanFitShape(startIndex, width, height);
    }

    private bool CanPlaceDraggedItemAt(int startIndex, InventoryItemInstance item)
    {
        return grid != null &&
               grid.CanPlaceDraggedItemAt(startIndex, item, this, DragSourceOwner);
    }

    private bool TryAddItemAt(int startIndex, InventoryItemInstance instance)
    {
        return grid != null && grid.TryPlaceAt(startIndex, instance);
    }

    private void ForceRemoveItemCompletely(InventoryItemInstance instance)
    {
        grid?.ForceRemoveCompletely(instance);
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
        grid?.PreviewPlacement(startIndex, item, this, DragSourceOwner);
    }

    private void RebuildSlotVisualsFromCurrentState()
    {
        grid?.RebuildSlotVisualsFromCurrentState();
    }

    public int GetUsedSlotCount()
    {
        return grid != null ? grid.CountUsedSlots() : 0;
    }

    public List<InventoryItemInstance> GetAllInstancesDistinct()
    {
        return grid != null
            ? grid.GetAllInstancesDistinct()
            : new List<InventoryItemInstance>();
    }

    // =====================================================
    // Drag Controller Wrappers
    // =====================================================

    public void ShowDragGhost(InventoryItemInstance item, InventorySlot sourceSlot)
    {
        dragController?.ShowGhost(item, sourceSlot);
    }

    public void HideDragGhost()
    {
        dragController?.HideGhost();
    }

    // =====================================================
    // Stack Service Wrappers
    // =====================================================

    public static bool IsStackSplitModifierHeld()
    {
        return InventoryStackService.IsStackSplitModifierHeld();
    }

    public static bool CanSplitStack(InventoryItemInstance item)
    {
        return InventoryStackService.CanSplitStack(item);
    }

    public static InventoryItemInstance CloneStackPart(InventoryItemInstance source, int amount)
    {
        return InventoryStackService.CloneStackPart(source, amount);
    }

    private bool TryMergeDraggedStackIntoInventorySlot(InventorySlot targetSlot)
    {
        return InventoryStackService.TryMergeDraggedStackIntoSlot(
            draggedItem,
            targetSlot,
            DragSourceOwner,
            RefreshCountDisplay,
            () =>
            {
                FinishInventoryDrag();
                RefreshGunUIFromWeaponManager();
            }
        );
    }

    // =====================================================
    // InventoryCash Wrappers
    // =====================================================

    public void RefreshCashUI()
    {
        if (cashController == null)
            InitCashController();

        cashController?.RefreshCashUI();
    }

    public void ApplyMoneyChange(int delta)
    {
        if (cashController == null)
            InitCashController();

        cashController?.ApplyMoneyChange(delta);
    }

    // =====================================================
    // InventoryWeaponBridge Wrappers
    // =====================================================

    private bool IsCombatItemData(InventoryItemData data)
    {
        return InventoryWeaponBridge.IsCombatItemData(data);
    }

    public bool CanReceiveWeaponFromBox(InventoryItemInstance item)
    {
        if (weaponBridge == null)
            InitWeaponBridge();

        return weaponBridge != null && weaponBridge.CanReceiveWeaponFromBox(item);
    }
    
    public bool RegisterWeaponFromBoxTransfer(InventoryItemInstance item)
    {
        if (weaponBridge == null)
            InitWeaponBridge();

        return weaponBridge != null && weaponBridge.RegisterWeaponFromBoxTransfer(item);
    }

    public void RefreshGunUIFromWeaponManager()
    {
        if (weaponBridge == null)
            InitWeaponBridge();

        weaponBridge?.RefreshGunUI();
    }

    public bool TryTransferCombatItemFromBoxToPlayer(
    InventoryItemInstance item,
    IInventorySlotOwner boxOwner)
    {
        if (weaponBridge == null)
            InitWeaponBridge();

        return weaponBridge != null &&
               weaponBridge.TryTransferCombatItemFromBoxToPlayer(
                   item,
                   boxOwner,
                   this,
                   removeFromPlayerOnFail: () => RemoveItemFromOwner(item)
               );
    }
}