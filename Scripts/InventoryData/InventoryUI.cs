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
    public static InventoryUI Instance { get; private set; }
    public static bool IsInventoryOpen { get; private set; }
    public static bool IsDraggingInventoryItem { get; private set; }
    public static InventoryItemInstance draggedItem;

    public static IInventorySlotOwner DragSourceOwner;
    public static InventorySlot DragSourceSlot;

    [Header("Refs")]
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerMovement playerMovement;

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
    private Vector3 dragGhostPointerOffset;

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
    [SerializeField] private Color placedItemColor = new Color(0.25f, 0.65f, 1f, 0.22f);

    [Header("Drag Ghost")]
    [SerializeField] private float dragGhostCellSize = 45f;
    [SerializeField] private float dragGhostScale = 0.9f;

    private readonly List<InventorySlot> placementPreviewSlots = new();
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

    private Coroutine moneyAnimCo;
    private int uiCashShown = -1;

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
        InitButtons();
        InitMoneyUI();
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

        // ghost za kursorem
        if (dragGhost != null && dragGhost.gameObject.activeSelf)
            dragGhost.rectTransform.position = Input.mousePosition + dragGhostPointerOffset;

        HandleDragRotationInput();

        if (draggedItem != null && ReferenceEquals(DragSourceOwner, this))
        {
            InventorySlot hoverSlot = GetInventorySlotUnderMouse();
            if (hoverSlot != null)
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
                if (DragSourceSlot != null)
                    DragSourceSlot.SetDraggingVisual(false);

                DropDraggedItemToWorld();

                if (dragGhost != null)
                    dragGhost.gameObject.SetActive(false);

                draggedItem = null;
                draggedSlot = null;
                DragSourceOwner = null;
                DragSourceSlot = null;
                IsDraggingInventoryItem = false;
            }
        }
    }

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

        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();

        if (weaponManager == null && playerStats != null) weaponManager = playerStats.GetComponentInChildren<WeaponManager>();

        if (moneyDropSpawner == null) moneyDropSpawner = FindFirstObjectByType<MoneyDropSpawner>();

        if (atmUI == null) atmUI = FindFirstObjectByType<ATMUIController>();

        if (bankDialogueUI == null) bankDialogueUI = FindFirstObjectByType<BankDialogueUI>();

        if (mouseLook == null) mouseLook = FindFirstObjectByType<MouseLook>();

        if (gunUI == null) gunUI = FindFirstObjectByType<GunUI>();

        if (amountDialog == null) amountDialog = ItemAmountDialog.Instance;

        if (playerStats != null) cachedPlayerTransform = playerStats.transform;

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
        ShowPage(0);
        ShowPanel(false);

        currentAlphas[0] = 1f;
        currentAlphas[1] = 1f;

        // podepnij PlayerStats moneyText
        if (playerStats != null && moneyText != null)
        {
            playerStats.moneyText = moneyText;
            playerStats.UpdateMoneyUI();
        }
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

    private void InitMoneyUI()
    {
        uiCashShown = (playerStats != null) ? playerStats.money : -1;
        if (moneyAnimCo != null) StopCoroutine(moneyAnimCo);
        RefreshCashUI();
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

        dragGhostPointerOffset = Vector3.zero;
        DraggedGrabCellOffset = 0;

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

        dragGhostPointerOffset = Vector3.zero;
        DraggedGrabCellOffset = 0;
        DraggedGrabCellOffsetY = 0;

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

                if (CanMergeStacks(instance, slot.item))
                {
                    int addAmount = Mathf.Max(1, instance.count);

                    slot.item.count += addAmount;
                    slot.UpdateCountDisplay();

                    if (instance.data is GrenadeItemData && weaponManager != null)
                        weaponManager.SyncGrenadeSlotFromInventory(instance.data);

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

    private bool TryAddItemAt(int startIndex, InventoryItemInstance instance)
    {
        if (instance == null || instance.data == null)
            return false;

        int width = GetItemWidth(instance);
        int height = GetItemHeight(instance);

        if (!CanFitShape(startIndex, width, height, slotsPerRow))
            return false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = startIndex + y * slotsPerRow + x;
                var s = slotList[index];

                s.isOccupied = true;
                s.item = instance;

                if (x == 0 && y == 0)
                {
                    s.SetItem(instance);
                }
                else
                {
                    if (s.fillImage != null) s.fillImage.SetActive(false);
                    if (s.borderImage != null) s.borderImage.SetActive(false);

                    if (s.iconImage != null)
                    {
                        s.iconImage.sprite = null;
                        s.iconImage.enabled = false;
                    }

                    s.SetOccupiedHighlight(true);
                }
            }
        }

        RefreshOccupiedHighlights();
        return true;
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
        GameObject pickupPrefab =
            (data != null && data.prefab != null)
                ? data.prefab
                : Resources.Load<GameObject>($"Pickups/{data.itemName}") ??
                  Resources.Load<GameObject>($"Pickups/{data.name}");

        if (pickupPrefab == null)
        {
          //  Debug.LogWarning($"❌ SpawnPickup: Brak prefabu w Resources/Pickups dla: '{data.itemName}' / '{data.name}'");
            return;
        }

        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();

        if (cachedPlayerTransform == null && playerStats != null)
            cachedPlayerTransform = playerStats.transform;

        var player = playerStats;
        var t = cachedPlayerTransform;

        // --- wyznacz pozycję przed graczem, z uwzględnieniem ścian ---
        Vector3 originPos = (t != null ? t.position : Vector3.zero) + Vector3.up * 1.0f;
        Vector3 fwd = (t != null ? t.forward : Vector3.forward);

        float sphereRadius = 0.25f;
        float castDistance = 0.8f;

        Vector3 dropPos = originPos + fwd * 0.6f;

        if (t != null)
        {
            int mask = (dropObstacleMask.value != 0) ? dropObstacleMask.value : ~0;
            if (Physics.SphereCast(originPos, sphereRadius, fwd, out RaycastHit hit,
                                   castDistance, mask, QueryTriggerInteraction.Ignore))
            {
                // tuż przed drzwiami/ścianą po stronie gracza
                dropPos = hit.point - fwd * (sphereRadius + 0.05f);
                dropPos.y = originPos.y;
            }
        }

        // teraz tylko szukamy podłoża pod wybranym punktem
        Vector3 pos = dropPos + Vector3.up * 0.4f;
        if (Physics.Raycast(pos, Vector3.down, out RaycastHit ground, 3f, ~0, QueryTriggerInteraction.Ignore))
            pos = ground.point + ground.normal * 0.05f;

        Quaternion rot = (t != null)
            ? Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, Vector3.up), Vector3.up)
            : Quaternion.identity;

        var dropped = Instantiate(pickupPrefab, pos, rot);
        GameObjectUtil.CopyTagAndLayer(pickupPrefab, dropped);

        InventoryItemInstance inst;
        var cardPickup = dropped.GetComponentInChildren<ItemPickup>(true);
        if (cardPickup != null && data is BankCardItemData)
        {
            // tu NIE mamy meta (bo jest tylko InventoryItemData), więc nie inicjalizujemy instancją
            return;
        }

        var pickup = dropped.GetComponentInChildren<WeaponPickup>(true);
        if (pickup == null)
        {
           // Debug.LogError("❌ SpawnPickup: Brak komponentu WeaponPickup na prefabie (ani w dzieciach).");
            return;
        }

        if (data is AmmoItemData ammoData)
        {
            pickup.ammoOnly = true;
            pickup.itemData = ammoData.weapon;       // do czego pasuje magazynek
            pickup.ammoInventoryData = ammoData;

            int magAmount = (totalAmmo >= 0) ? totalAmmo
                           : (currentAmmo >= 0) ? currentAmmo
                           : ammoData.amountPerUnit;

            inst = new InventoryItemInstance(ammoData, magAmount, magAmount);
        }
        else
        {
            inst = new InventoryItemInstance(data, currentAmmo, totalAmmo);
        }

        pickup.Initialize(inst, player ? player.gameObject : null);

        if (inst.data is AmmoItemData && inst.totalAmmo <= 0 && inst.currentAmmo <= 0)
            pickup.nonInteractable = true;

        if (data is AmmoItemData)
        {
            pickup.totalAmmo = inst.totalAmmo;
            pickup.currentAmmo = inst.currentAmmo;
        }

        // fizyka + drobny impuls, z ochroną przed auto-pickupem
        pickup.SetupPhysics(isPickupFromScene: true);
        pickup.IgnoreAutoPickupFrom(player ? player.gameObject : null, 0.6f);

        var rb = dropped.GetComponentInChildren<Rigidbody>() ?? dropped.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position += Vector3.up * 0.05f;
            if (t != null)
            {
                rb.AddForce(fwd * 2.5f + Vector3.up * 1.0f, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 1.5f, ForceMode.Impulse);
            }
        }
    }

    // OVERLOAD: drop z instancji (np. karta z meta)
    private void SpawnPickup(InventoryItemInstance source, int currentAmmo = -1, int totalAmmo = -1)
    {
        if (source == null || source.data == null) return;

        var data = source.data;

        GameObject pickupPrefab =
            (data.prefab != null) ? data.prefab :
            Resources.Load<GameObject>($"Pickups/{data.itemName}") ??
            Resources.Load<GameObject>($"Pickups/{data.name}");

        if (pickupPrefab == null)
        {
           // Debug.LogWarning($"❌ SpawnPickup: Brak prefabu w Resources/Pickups dla: '{data.itemName}' / '{data.name}'");
            return;
        }

        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();

        if (cachedPlayerTransform == null && playerStats != null)
            cachedPlayerTransform = playerStats.transform;

        var player = playerStats;
        var t = cachedPlayerTransform;

        Vector3 originPos = (t ? t.position : Vector3.zero) + Vector3.up * 1.0f;
        Vector3 fwd = (t ? t.forward : Vector3.forward);

        float sphereRadius = 0.25f;
        float castDistance = 0.8f;
        Vector3 dropPos = originPos + fwd * 0.6f;

        int mask = (dropObstacleMask.value != 0) ? dropObstacleMask.value : ~0;
        if (t && Physics.SphereCast(originPos, sphereRadius, fwd, out RaycastHit hit,
                                    castDistance, mask, QueryTriggerInteraction.Ignore))
        {
            dropPos = hit.point - fwd * (sphereRadius + 0.05f);
            dropPos.y = originPos.y;
        }

        Vector3 pos = dropPos + Vector3.up * 0.4f;
        if (Physics.Raycast(pos, Vector3.down, out RaycastHit ground, 3f, ~0, QueryTriggerInteraction.Ignore))
            pos = ground.point + ground.normal * 0.05f;

        Quaternion rot = t
            ? Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, Vector3.up), Vector3.up)
            : Quaternion.identity;

        var dropped = Instantiate(pickupPrefab, pos, rot);
        GameObjectUtil.CopyTagAndLayer(pickupPrefab, dropped);

        var itemPickup = dropped.GetComponentInChildren<ItemPickup>(true);
        if (itemPickup != null)
        {
            itemPickup.InitializeFromInstance(source);
            itemPickup.IgnorePickupFor(0.6f);
        }

        var rb = dropped.GetComponentInChildren<Rigidbody>() ?? dropped.GetComponent<Rigidbody>();
        if (rb != null && t != null)
        {
            rb.position += Vector3.up * 0.05f;
            rb.AddForce(fwd * 2.5f + Vector3.up * 1.0f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 1.5f, ForceMode.Impulse);
        }
    }

    private void SpawnBankCard(InventoryItemInstance instance)
    {
        var data = instance.data as BankCardItemData;
        if (data == null || data.prefab == null)
        {
          //  Debug.LogWarning("❌ SpawnBankCard: Brak prefabu w BankCardItemData.");
            return;
        }

        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();

        if (cachedPlayerTransform == null && playerStats != null)
            cachedPlayerTransform = playerStats.transform;

        var player = playerStats;
        var t = cachedPlayerTransform;

        Vector3 originPos = (t != null ? t.position : Vector3.zero) + Vector3.up * 1.0f;
        Vector3 fwd = (t != null ? t.forward : Vector3.forward);

        float sphereRadius = 0.25f;
        float castDistance = 0.8f;
        Vector3 dropPos = originPos + fwd * 0.6f;

        int mask = (dropObstacleMask.value != 0) ? dropObstacleMask.value : ~0;
        if (t != null && Physics.SphereCast(originPos, sphereRadius, fwd, out RaycastHit hit, castDistance, mask, QueryTriggerInteraction.Ignore))
        {
            dropPos = hit.point - fwd * (sphereRadius + 0.05f);
            dropPos.y = originPos.y;
        }

        Vector3 pos = dropPos + Vector3.up * 0.4f;
        if (Physics.Raycast(pos, Vector3.down, out RaycastHit ground, 3f, ~0, QueryTriggerInteraction.Ignore))
            pos = ground.point + ground.normal * 0.05f;

        Quaternion rot = (t != null)
            ? Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, Vector3.up), Vector3.up)
            : Quaternion.identity;

        var dropped = Instantiate(data.prefab, pos, rot);
        GameObjectUtil.CopyTagAndLayer(data.prefab, dropped);

        // ✅ wstrzyknij meta
        var pickup = dropped.GetComponentInChildren<ItemPickup>(true);
        if (pickup != null)
        {
            pickup.InitializeFromInstance(instance);
            pickup.IgnorePickupFor(0.6f); // <— ważne
        }

        // fizyka impuls jak u Ciebie
        var rb = dropped.GetComponentInChildren<Rigidbody>() ?? dropped.GetComponent<Rigidbody>();
        if (rb != null && t != null)
        {
            rb.position += Vector3.up * 0.05f;
            rb.AddForce(fwd * 2.0f + Vector3.up * 1.0f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 1.0f, ForceMode.Impulse);
        }
    }

    private bool CanFitItem(int startIndex, int size, int slotsPerRow = 4)
    {
        return CanFitShape(startIndex, Mathf.Max(1, size), 1, slotsPerRow);
    }

    private bool CanFitShape(int startIndex, int width, int height, int rowSize)
    {
        if (startIndex < 0)
            return false;

        if (width <= 0 || height <= 0)
            return false;

        int startCol = startIndex % rowSize;
        int startRow = startIndex / rowSize;

        if (startCol + width > rowSize)
            return false;

        int lastIndex = startIndex + (height - 1) * rowSize + (width - 1);
        if (lastIndex >= slotList.Count)
            return false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = startIndex + y * rowSize + x;

                if (index < 0 || index >= slotList.Count)
                    return false;

                InventorySlot slot = slotList[index];

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

    private void ForceRemoveItemCompletely(InventoryItemInstance instance)
    {
        if (instance == null)
            return;

        for (int i = 0; i < slotList.Count; i++)
        {
            var s = slotList[i];

            if (s == null || s.item != instance)
                continue;

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
                s.iconImage.color = Color.white;
            }

            s.ClearPlacementPreview();
            s.ClearOccupiedHighlight();

            s.transform.localScale = Vector3.one;

            var rt = s.GetComponent<RectTransform>();
            if (rt != null)
                rt.pivot = new Vector2(0.5f, 0.5f);
        }

        RefreshOccupiedHighlights();
    }

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
                IsStackSplitModifierHeld() &&
                CanSplitStack(draggedItem);

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

                dragGhostPointerOffset = Vector3.zero;
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

                        InventoryItemInstance part = CloneStackPart(sourceItem, amount);
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

    public List<InventoryItemInstance> GetAllInstancesDistinct()
    {
        var set = new HashSet<InventoryItemInstance>();
        var list = new List<InventoryItemInstance>();

        foreach (var slot in slotList) // slotList masz prywatne, ale tu jesteśmy w InventoryUI
        {
            var inst = slot?.item;
            if (inst == null) continue;

            if (set.Add(inst))
                list.Add(inst);
        }

        return list;
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
    public void RefreshCashUI()
    {
        if (playerStats == null) playerStats = FindFirstObjectByType<PlayerStats>();
        if (playerStats == null || cashText == null) return;

        if (cachedPlayerTransform == null && playerStats != null)
            cachedPlayerTransform = playerStats.transform;

        int v = (uiCashShown >= 0) ? uiCashShown : playerStats.money;
        cashText.text = $"Cash: {v:n0}$";
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

    private float GetMoneyAnimDuration(int delta)
    {
        // delta = ile przybywa/ubywa
        delta = Mathf.Abs(delta);

        // 0..1 na podstawie log10 (duże kwoty szybciej)
        float t = Mathf.Clamp01(Mathf.Log10(delta + 1f) / 4f); // 10^4 = 10000 -> t ~ 1

        float maxDur = 0.9f;  // małe kwoty
        float minDur = 0.18f; // duże kwoty

        return Mathf.Lerp(maxDur, minDur, t);
    }

    private IEnumerator CoAnimateCashUI(int from, int to, float duration)
    {
        if (cashText == null) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);

            int v = Mathf.RoundToInt(Mathf.Lerp(from, to, a));
            uiCashShown = v;
            cashText.text = $"Cash: {v:n0}$";

            yield return null;
        }

        uiCashShown = to;
        cashText.text = $"Cash: {to:n0}$";
    }

    public void ApplyMoneyChange(int delta)
    {
        if (playerStats == null) playerStats = FindFirstObjectByType<PlayerStats>();
        if (playerStats == null) return;

        if (cachedPlayerTransform == null && playerStats != null)
            cachedPlayerTransform = playerStats.transform;

        int before = playerStats.money;
        int after = Mathf.Max(0, before + delta);

        playerStats.money = after;
        playerStats.UpdateMoneyUI();

        int fromShown = (uiCashShown >= 0) ? uiCashShown : before;
        float dur = GetMoneyAnimDuration(after - fromShown);

        if (moneyAnimCo != null) StopCoroutine(moneyAnimCo);
        moneyAnimCo = StartCoroutine(CoAnimateCashUI(fromShown, after, dur));
    }

    void OnEnable() => RefreshCashUI();

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

        bool isCombatItem = IsCombatItemData(item.data);

        if (isCombatItem && weaponManager != null)
        {
            int weaponIndex = weaponManager.FindSlotIndexForInstance(item);

            if (weaponIndex < 0)
                weaponIndex = weaponManager.GetWeaponIndex(item);

            if (weaponIndex < 0)
                weaponIndex = GetCombatSlotIndexFromData(item.data);

            if (weaponIndex >= 0)
            {
                weaponManager.ClearSlot(weaponIndex);
            }
        }

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

    public void ShowDragGhost(InventoryItemInstance item, InventorySlot sourceSlot)
    {
        if (item == null || item.data == null || sourceSlot == null || dragGhost == null)
            return;

        dragGhost.sprite = item.data.icon;
        dragGhost.color = Color.white;

        if (item.hasBankCardMeta && BankSystem.Instance != null)
            dragGhost.color = BankSystem.Instance.GetVariantColor(item.bankCard.colorVariant);

        int size = Mathf.Max(1, item.data.slotSize);

        int width = GetItemWidth(item);
        int height = GetItemHeight(item);

        RectTransform ghostRect = dragGhost.rectTransform;

        float cellW = dragGhostCellSize * dragGhostScale;
        float cellH = dragGhostCellSize * dragGhostScale;

        float w = cellW * width;
        float h = cellH * height;

        ghostRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        ghostRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);

        ghostRect.localScale = Vector3.one;

        // Zachowaj miejsce złapania itemu.
        RectTransform sourceRect = null;

        if (sourceSlot.iconImage != null)
            sourceRect = sourceSlot.iconImage.rectTransform;

        if (sourceRect == null)
            sourceRect = sourceSlot.GetComponent<RectTransform>();

        dragGhostPointerOffset = sourceRect != null
            ? sourceRect.position - Input.mousePosition
            : Vector3.zero;

        float localMouseXFromLeft = (w * 0.5f) - dragGhostPointerOffset.x;
        float localMouseYFromTop = (h * 0.5f) + dragGhostPointerOffset.y;

        DraggedGrabCellOffset = Mathf.Clamp(
            Mathf.FloorToInt(localMouseXFromLeft / Mathf.Max(1f, cellW)),
            0,
            width - 1
        );

        DraggedGrabCellOffsetY = Mathf.Clamp(
            Mathf.FloorToInt(localMouseYFromTop / Mathf.Max(1f, cellH)),
            0,
            height - 1
        );

        ghostRect.position = Input.mousePosition + dragGhostPointerOffset;

        dragGhost.preserveAspect = false;
        dragGhost.raycastTarget = false;

        dragGhost.transform.SetAsLastSibling();
        dragGhost.gameObject.SetActive(true);

        // Bezpiecznik: niezależnie od tego, skąd startuje drag,
        // highlight ma wiedzieć, że item jest w ręce.
        IsDraggingInventoryItem = true;

        SetDraggingVisualForItem(item, true);

        RefreshOccupiedHighlights();
        ClearPlacementPreview();
        lastPreviewStartSlot = null;
    }

    public void HideDragGhost()
    {
        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);
    }

    public int GetUsedSlotCount()
    {
        int used = 0;

        foreach (var slot in slotList)
        {
            if (slot != null && slot.isOccupied)
                used++;
        }

        return used;
    }

    public int GetUnlockedSlotCount()
    {
        return unlockedSlotCount;
    }

    public bool CanReceiveWeaponFromBox(InventoryItemInstance item)
    {
        if (item == null || item.data == null) return false;

        // Zwykły item zawsze może wejść, jeśli jest miejsce.
        if (!IsCombatItemData(item.data))
            return true;

        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>();

        if (weaponManager == null) return false;

        int slotIndex = GetCombatSlotIndexFromData(item.data);
        if (slotIndex < 0) return false;

        // Granaty zostawiamy jako stackowalne na późniejszą logikę ilości.
        if (slotIndex == 3)
            return true;

        // Melee/Pistol/Riffle: tylko jedna broń danego typu.
        return !weaponManager.HasWeapon(slotIndex);
    }

    public bool RegisterWeaponFromBoxTransfer(InventoryItemInstance item)
    {
        if (item == null || item.data == null) return false;

        // Nie broń, nie melee, nie granat — nie rejestrujemy w WeaponManager.
        if (!IsCombatItemData(item.data))
            return true;

        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>();

        if (weaponManager == null) return false;

        int slotIndex = GetCombatSlotIndexFromData(item.data);
        if (slotIndex < 0) return false;

        bool isNade = slotIndex == 3;

        if (!isNade && weaponManager.HasWeapon(slotIndex))
            return false;

        bool wasHands = weaponManager.IsUsingHandsOnly();
        int previousIndex = weaponManager.GetRawCurrentWeaponIndex();

        if (item.data.prefab == null)
        {
            Debug.LogWarning($"[InventoryUI] Item '{item.data.name}' nie ma prefabu.");
            return false;
        }

        weaponManager.PickUpWeapon(
            slotIndex,
            item.data.prefab.name,
            item.currentAmmo,
            item.totalAmmo,
            item
        );

        // Transfer z Boxa NIE ma wymuszać zmiany aktywnej broni.
        if (wasHands)
        {
            weaponManager.ActivateHandsOnly();
        }
        else if (previousIndex >= 0 &&
                 previousIndex != slotIndex &&
                 weaponManager.HasWeapon(previousIndex))
        {
            weaponManager.SelectWeapon(previousIndex);
        }

        RefreshGunUIFromWeaponManager();
        return true;
    }

    public void RefreshGunUIFromWeaponManager()
    {
        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>();

        if (weaponManager == null) return;

        weaponManager.RefreshWeaponHUD();

        if (gunUI == null)
            gunUI = FindFirstObjectByType<GunUI>();

        if (gunUI == null) return;

        // Ważne: GunUI dostaje itemy z Inventory, ale granaty są zsumowane.
        var owned = BuildOwnedItemsForGunUI();

        InventoryItemInstance active = weaponManager.IsUsingHandsOnly()
            ? null
            : weaponManager.GetActiveInstance();

        gunUI.UpdateWeaponHUD(owned, active);
    }

    private void DropDraggedItemToWorld()
    {
        if (draggedItem == null) return;

        IInventorySlotOwner source = DragSourceOwner;

        // Najpierw spawn na ziemi.
        SpawnDraggedPickupOnly(draggedItem);

        // Potem usuń z właściwego właściciela: Inventory albo Box.
        if (source != null)
        {
            source.RemoveItemFromOwner(draggedItem);
        }
        else
        {
            RemoveItem(draggedItem, 1);
        }
    }

    private void SpawnDraggedPickupOnly(InventoryItemInstance instance)
    {
        if (instance == null || instance.data == null) return;

        if (instance.data is BankCardItemData)
        {
            SpawnBankCard(instance);
            return;
        }

        int cur = instance.currentAmmo;
        int tot = instance.totalAmmo;

        if (instance.data is AmmoItemData)
        {
            int mag = Mathf.Max(0, instance.totalAmmo);
            cur = tot = mag;
        }

        SpawnPickup(instance, cur, tot);
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

    public static bool IsStackSplitModifierHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null &&
               (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#else
    return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }

    public static bool CanSplitStack(InventoryItemInstance item)
    {
        if (item == null || item.data == null) return false;
        if (item.count <= 1) return false;

        // Bank cards nie powinny być stackowane/dzielone.
        if (item.data is BankCardItemData || item.hasBankCardMeta)
            return false;

        // Indywidualne magazynki też raczej NIE, bo każdy magazynek ma własny stan ammo.
        if (item.data is AmmoItemData ammo && ammo.individualMagazines)
            return false;

        return true;
    }

    public static InventoryItemInstance CloneStackPart(InventoryItemInstance source, int amount)
    {
        if (source == null || source.data == null) return null;

        amount = Mathf.Clamp(amount, 1, source.count);

        var copy = new InventoryItemInstance(source.data, source.currentAmmo, source.totalAmmo);
        copy.count = amount;

        // Jeżeli kiedyś będziesz dzielił itemy z meta, tutaj trzeba kopiować meta.
        // Na teraz bank card wykluczamy z CanSplitStack().

        return copy;
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

    private bool TryMergeDraggedStackIntoInventorySlot(InventorySlot targetSlot)
    {
        if (targetSlot == null || targetSlot.item == null) return false;
        if (draggedItem == null || draggedItem.data == null) return false;
        if (ReferenceEquals(targetSlot.item, draggedItem)) return false;

        InventoryItemInstance sourceItem = draggedItem;
        InventoryItemInstance targetItem = targetSlot.item;

        if (!CanMergeStacks(sourceItem, targetItem))
            return false;

        IInventorySlotOwner sourceOwner = DragSourceOwner;
        if (sourceOwner == null) return false;

        int maxAmount = sourceItem.count;
        if (maxAmount <= 0) return false;

        bool useDialog = IsStackSplitModifierHeld() && CanSplitStack(sourceItem);

        if (useDialog)
        {
            ItemAmountDialog dialog = ItemAmountDialog.Instance;
            if (dialog == null) return true;

            int startValue = Mathf.CeilToInt(maxAmount / 2f);

            HideDragGhost();
            ClearSharedDragState();
            draggedSlot = null;

            dialog.Open(
                $"MERGE {sourceItem.data.itemName}",
                1,
                maxAmount,
                startValue,
                amount =>
                {
                    amount = Mathf.Clamp(amount, 1, maxAmount);
                    MergeStackAmountToInventoryTarget(sourceOwner, sourceItem, targetItem, amount);
                },
                cancel: null
            );

            return true;
        }

        MergeStackAmountToInventoryTarget(sourceOwner, sourceItem, targetItem, maxAmount);

        HideDragGhost();
        ClearSharedDragState();
        draggedSlot = null;

        return true;
    }

    private void MergeStackAmountToInventoryTarget(
        IInventorySlotOwner sourceOwner,
        InventoryItemInstance sourceItem,
        InventoryItemInstance targetItem,
        int amount)
    {
        if (sourceOwner == null || sourceItem == null || targetItem == null) return;

        amount = Mathf.Clamp(amount, 1, sourceItem.count);

        if (!sourceOwner.RemoveStackAmountFromOwner(sourceItem, amount))
            return;

        targetItem.count += amount;
        RefreshCountDisplay(targetItem);

        if (targetItem.data is GrenadeItemData && weaponManager != null)
            weaponManager.SyncGrenadeSlotFromInventory(targetItem.data);

        RefreshGunUIFromWeaponManager();
    }

    public static bool CanMergeStacks(InventoryItemInstance source, InventoryItemInstance target)
    {
        if (source == null || target == null) return false;
        if (source.data == null || target.data == null) return false;
        if (source.data != target.data) return false;

        if (source.data is BankCardItemData || source.hasBankCardMeta || target.hasBankCardMeta)
            return false;

        if (source.data is AmmoItemData ammo && ammo.individualMagazines)
            return false;

        return source.count > 0 && target.count > 0;
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

    private void ClearPlacementPreview()
    {
        for (int i = 0; i < placementPreviewSlots.Count; i++)
        {
            if (placementPreviewSlots[i] != null)
                placementPreviewSlots[i].ClearPlacementPreview();
        }

        placementPreviewSlots.Clear();
        lastPreviewStartSlot = null;
    }

    private bool CanPlaceDraggedItemAt(int startIndex, InventoryItemInstance item)
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
        if (lastIndex >= slotList.Count)
            return false;

        bool movingInsideThisInventory = ReferenceEquals(DragSourceOwner, this);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = startIndex + y * slotsPerRow + x;

                if (index < 0 || index >= slotList.Count)
                    return false;

                InventorySlot slot = slotList[index];

                if (slot == null)
                    return false;

                if (slot.CompareTag("LockedSlot"))
                    return false;

                if (slot.isOccupied)
                {
                    bool occupiedBySameDraggedItem =
                        movingInsideThisInventory &&
                        slot.item == item;

                    if (!occupiedBySameDraggedItem)
                        return false;
                }
            }
        }

        return true;
    }

    private void PreviewPlacement(int startIndex, InventoryItemInstance item)
    {
        ClearPlacementPreview();

        if (item == null || item.data == null)
            return;

        int width = GetItemWidth(item);
        int height = GetItemHeight(item);

        if (startIndex < 0 || startIndex >= slotList.Count)
            return;

        bool valid = CanPlaceDraggedItemAt(startIndex, item);
        Color color = valid ? placementValidColor : placementInvalidColor;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = startIndex + y * slotsPerRow + x;

                if (index < 0 || index >= slotList.Count)
                    continue;

                InventorySlot slot = slotList[index];

                if (slot == null)
                    continue;

                slot.SetPlacementPreview(true, color);
                placementPreviewSlots.Add(slot);
            }
        }
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

    private void RefreshOccupiedHighlights()
    {
        foreach (var slot in slotList)
        {
            if (slot == null)
                continue;

            bool belongsToDraggedItem =
                IsDraggingInventoryItem &&
                draggedItem != null &&
                slot.item == draggedItem;

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

    private void RebuildSlotVisualsFromCurrentState()
    {
        HashSet<InventoryItemInstance> firstSlots = new HashSet<InventoryItemInstance>();

        foreach (var slot in slotList)
        {
            if (slot == null)
                continue;

            if (!slot.isOccupied || slot.item == null)
            {
                slot.Clear();
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

    private bool DragRotatePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
    return Input.GetMouseButtonDown(1);
#endif
    }

    private void HandleDragRotationInput()
    {
        if (draggedItem == null || draggedItem.data == null)
            return;

        if (!draggedItem.CanRotate)
            return;

        if (!DragRotatePressedThisFrame())
            return;

        draggedItem.ToggleRotation();

        RebuildDragGhostShapeAfterRotation();

        ClearPlacementPreview();
        lastPreviewStartSlot = null;

        if (BoxInventoryUI.Instance != null && BoxInventoryUI.Instance.IsOpen)
            BoxInventoryUI.Instance.ClearPlacementPreviewExternal();
    }

    private void RebuildDragGhostShapeAfterRotation()
    {
        if (dragGhost == null || draggedItem == null || draggedItem.data == null)
            return;

        int width = GetItemWidth(draggedItem);
        int height = GetItemHeight(draggedItem);

        float cellW = dragGhostCellSize * dragGhostScale;
        float cellH = dragGhostCellSize * dragGhostScale;

        float w = cellW * width;
        float h = cellH * height;

        RectTransform ghostRect = dragGhost.rectTransform;

        ghostRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        ghostRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
        ghostRect.localScale = Vector3.one;

        DraggedGrabCellOffset = Mathf.Clamp(DraggedGrabCellOffset, 0, width - 1);
        DraggedGrabCellOffsetY = Mathf.Clamp(DraggedGrabCellOffsetY, 0, height - 1);

        // Trzymaj pod kursorem aktualnie złapaną kratkę itemu.
        dragGhostPointerOffset = new Vector3(
            (w * 0.5f) - ((DraggedGrabCellOffset + 0.5f) * cellW),
            (-h * 0.5f) + ((DraggedGrabCellOffsetY + 0.5f) * cellH),
            0f
        );

        ghostRect.position = Input.mousePosition + dragGhostPointerOffset;
    }
}