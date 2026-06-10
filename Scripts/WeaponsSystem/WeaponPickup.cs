using System.Collections.Generic;
using UnityEngine;

public class WeaponPickup : MonoBehaviour
   {
    private InventoryUI _invCache;
    private GunUI _gunUICache;
    private PlayerStats _playerStatsCache;
    private readonly List<InventoryItemInstance> _ownedCache = new();

    [HideInInspector] public bool nonInteractable = false;

    public int currentAmmo = -1;
    public int totalAmmo = -1;

    // 🔧 DODANE:
    private bool canPickUp = false;  // było używane, ale nie zdefiniowane
    public int count = 1;            // było używane w Initialize, ale nie zdefiniowane

    bool canAutoPickup; // (zostawiam, jeśli gdzieś indziej używasz)
    GameObject lastOwner;
    float blockUntil;

    private bool pickedUp = false;
    private float spawnTime;
    private float pickupCooldown = 0.5f;
    private bool initialized = false;

    [HideInInspector] public WeaponManager playerWeaponManager;
    [Header("Pickup type")]
    [Tooltip("Gdy TRUE: to jest paczka amunicji. Nie daje broni graczowi, jeśli jej nie posiada.")]
    public bool ammoOnly = false;

    public InventoryItemData itemData;
    public AmmoItemData ammoInventoryData; // przypiszesz w prefabie magazynka

    public InventoryItemData GetItemData() => itemData;

    // 👇 jeżeli nie masz tej metody, dodaj:
    public void AssignWeaponManager(WeaponManager wm)
    {
        playerWeaponManager = wm;
    }

    private InventoryUI GetInventoryUI()
    {
        if (_invCache == null)
            _invCache = FindFirstObjectByType<InventoryUI>();

        return _invCache;
    }

    private GunUI GetGunUI()
    {
        if (_gunUICache == null)
            _gunUICache = FindFirstObjectByType<GunUI>();

        return _gunUICache;
    }

    private WeaponManager GetPlayerWeaponManager()
    {
        if (playerWeaponManager != null)
            return playerWeaponManager;

        if (_playerStatsCache == null)
            _playerStatsCache = FindFirstObjectByType<PlayerStats>();

        if (_playerStatsCache != null)
            playerWeaponManager = _playerStatsCache.GetComponentInChildren<WeaponManager>();

        return playerWeaponManager;
    }

    private void Start()
    {
    // Start nie tyka wartości, jeśli Initialize już je ustawił.
    if (!initialized && itemData is WeaponItemData wd)
    {
        if (ammoOnly)
        {
            if (totalAmmo < 0 && currentAmmo < 0)   // tylko gdy naprawdę NIC nie ustawiono
            {
                int def = (ammoInventoryData != null) ? ammoInventoryData.amountPerUnit : wd.magazineSize;
                currentAmmo = totalAmmo = def;
            }
        }
        else
        {
            if (currentAmmo < 0) currentAmmo = wd.magazineSize;
            if (totalAmmo < 0) totalAmmo = wd.magazineSize * 3;
        }
    }


        currentAmmo = Mathf.Max(0, currentAmmo);
        totalAmmo = Mathf.Max(0, totalAmmo);
        spawnTime = Time.time;
    }


    void Update()
    {
        if (InventoryUI.IsInventoryOpen) return;
        if (nonInteractable) return;                 // ▼ DODANE
        if (pickedUp || !canPickUp || playerWeaponManager == null) return;

        // ⛔ blokada po spawnie / zrzucie
        if (Time.time - spawnTime < pickupCooldown) return;
        if (Time.time < blockUntil) return; // ⛔ ochrona przed autolootem właściciela chwilę po dropie

        TryAutoPickUp();
    }
    public void Initialize(InventoryItemInstance instance, GameObject owner = null)
    {
        if (ammoOnly)
        {
            // itemData jest już ustawione na broń (WeaponItemData) przed Initialize.
            int payload = Mathf.Max(instance.totalAmmo, instance.currentAmmo);
            totalAmmo = payload;
            currentAmmo = payload;
        }
        else
        {
            itemData = instance.data;
            currentAmmo = instance.currentAmmo;
            totalAmmo = instance.totalAmmo;
        }

        count = Mathf.Max(1, instance.count);
        lastOwner = owner;
        blockUntil = Time.time + 0.5f;
        initialized = true;
        spawnTime = Time.time;
    }

    private int GetSlotIndex()
    {
        if (itemData is WeaponItemData weaponItem)
            return (int)weaponItem.weaponSlot;
        return -1;
    }

    public void SetupPhysics(bool isPickupFromScene = true)
    {
        // 1) SOLID collider na root (żeby nie wpadał pod ziemię)
        var rootRb = GetComponentInParent<Rigidbody>() ?? GetComponent<Rigidbody>();
        if (rootRb == null) rootRb = gameObject.AddComponent<Rigidbody>();

        rootRb.isKinematic = false;
        rootRb.useGravity = true;
        rootRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var rootCol = rootRb.GetComponent<Collider>() ?? rootRb.gameObject.GetComponentInChildren<Collider>();
        if (rootCol == null) rootCol = rootRb.gameObject.AddComponent<BoxCollider>();
        rootCol.isTrigger = false; // ← TO JEST KLUCZ: solidna kolizja

        // 2) Trigger do wykrycia gracza (jeśli nie istnieje – utwórz)
        // najlepiej na tym samym GO, gdzie wisi WeaponPickup
        var myTrigger = GetComponent<Collider>();
        if (myTrigger == null || !myTrigger.isTrigger)
        {
            var triggerGO = (myTrigger != null) ? myTrigger.gameObject : gameObject;
            var sphere = triggerGO.GetComponent<SphereCollider>();
            if (sphere == null) sphere = triggerGO.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            if (sphere.radius < 0.3f) sphere.radius = 0.6f;
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (nonInteractable) return;                 // ▼ DODANE
        if (pickedUp || !other.CompareTag("Player")) return;

        WeaponManager wm = other.GetComponentInChildren<WeaponManager>();
        if (wm != null)
        {
            AssignWeaponManager(wm);
            canPickUp = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canPickUp = false;
            playerWeaponManager = null;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (nonInteractable) return;                 // ▼ DODANE
        if (pickedUp || !other.CompareTag("Player")) return;
        if (playerWeaponManager == null)
        {
            var wm = other.GetComponentInChildren<WeaponManager>();
            if (wm != null) { AssignWeaponManager(wm); canPickUp = true; }
        }
    }
    private void TryAutoPickUp()
    {
        if (Time.time < blockUntil) return;
        if (itemData == null || playerWeaponManager == null) return;

        // ⛔ MELEE – już masz? nic nie rób (jak karabiny)
        if (itemData is MeleeItemData && playerWeaponManager.HasWeapon(0))
            return;

        // 🔫 PISTOLS / RIFLES – jeśli slot zajęty innym modelem → brak auto-pickupu
        if (itemData is WeaponItemData w)
        {
            int slot = GetSlotIndexFromData(itemData);
            bool isGunSlot = (slot == 1 || slot == 2);

            if (isGunSlot && playerWeaponManager.HasWeapon(slot))
            {
                // spróbuj doładować REZERWĘ tylko jeśli to ten sam model (sprawdzi TryAddReserveAmmo)
                if (TryAddReserveAmmo(playerWeaponManager))
                    Destroy(transform.root.gameObject); // zużyty pickup
                                                        // w każdym innym wypadku: po prostu wyjdź (swap tylko przez E)
                return;
            }
        }

        // Key item
        if (itemData.isKeyItem)
        {
            TryAddToInventoryUI(new InventoryItemInstance(itemData));
            Destroy(gameObject);
            return;
        }

        // Granat
        if (itemData.prefab != null && itemData.prefab.name == "Grenade")
        {
            PickUp();
            return;
        }

        // AMMO-ONLY
        if (ammoOnly)
        {
            var playerGO = playerWeaponManager.GetComponentInParent<PlayerStats>()?.gameObject
                            ?? playerWeaponManager.gameObject;
            if (TryGiveAmmoToPlayer(playerGO))
                Destroy(transform.root.gameObject);
            return;
        }

        // ten sam model? (np. drugi taki sam pistolet) → dopinka rezerwy
        if (TryAddReserveAmmo(playerWeaponManager))
        {
            Destroy(transform.root.gameObject);
            return;
        }

        // slot wolny → normalny pickup
        if (!playerWeaponManager.HasWeapon(GetSlotIndex()))
            PickUp();
    }

    private void PickUp()
    {
        pickedUp = true;

        var wm = playerWeaponManager;
        if (wm == null || itemData == null) return;

        int ammoToUse = currentAmmo;
        int totalToUse = totalAmmo;

        // instancja pickupa (kanoniczna dla UI/WM)
        var newInstance = new InventoryItemInstance(itemData, ammoToUse, totalToUse);

        // --- GRANATY (slot 3, stack) ---
        if (itemData.prefab != null && itemData.prefab.name == "Grenade")
        {
            var ui = GetInventoryUI();

            if (ui != null)
            {
                ui.TryAddItem(newInstance);

                var canonical = ui.GetInstanceForItem(itemData);
                if (canonical != null)
                    newInstance = canonical;
            }

            wm.PickUpWeapon(3, itemData.prefab.name, -1, -1, newInstance);

            wm.RefreshWeaponHUD();

            Destroy(transform.root.gameObject);
            return;
        }

        int slotIndex = GetSlotIndexFromData(itemData);

        // --- JEŚLI to broń z amunicją i gracz ma już TEN SAM MODEL w tym slocie: dołóż do rezerwy ---
        if (itemData is WeaponItemData && wm.HasWeapon(slotIndex))
        {
            var slotGO = wm.GetWeaponSlots()[slotIndex];
            var existingGun = slotGO ? slotGO.GetComponentInChildren<Gun>(true) : null;

            if (existingGun != null && existingGun.inventoryInstance != null)
            {
                string existingName = existingGun.inventoryInstance.data?.prefab?.name
                                   ?? existingGun.inventoryInstance.data?.itemName;
                string pickupName = itemData.prefab?.name ?? itemData.itemName;

                bool sameWeaponModel = !string.IsNullOrEmpty(existingName)
                                     && existingName == pickupName;

                if (sameWeaponModel)
                {
                    int addToReserve = (totalToUse > 0) ? totalToUse : Mathf.Max(0, ammoToUse);
                    if (addToReserve > 0)
                    {
                        existingGun.SetAmmo(existingGun.currentAmmo, existingGun.totalAmmo + addToReserve);

                        // odśwież HUD/UI
                        var inv = GetInventoryUI();
                        inv?.RefreshCountDisplay(existingGun.inventoryInstance);

                        var gunUI = GetGunUI();
                        if (gunUI != null)
                        {
                            var owned = BuildOwnedWeaponInstances(wm);
                            gunUI.UpdateWeaponHUD(owned, existingGun.inventoryInstance);
                        }
                    }

                    Destroy(transform.root.gameObject); // pickup „zużyty”
                    return;
                }
            }
        }

        // --- STANDARDOWY PICKUP / PODMIANA SLOTU ---
        bool hadBefore = (slotIndex >= 0) && wm.HasWeapon(slotIndex);

        // WeaponManager sam odrzuci drugi melee w slocie 0
        wm.PickUpWeapon(slotIndex, itemData.prefab.name, ammoToUse, totalToUse, newInstance);

        bool hasAfter = (slotIndex >= 0) && wm.HasWeapon(slotIndex);

        // Jeśli wcześniej nie miałeś tej broni w ekwipunku – dodaj jej instancję do UI (stack itp.)
        if (!hadBefore && hasAfter)
        {
            var ui = GetInventoryUI();
            ui?.TryAddItem(newInstance);
        }

        // Jeżeli slot faktycznie został obsadzony – niszcz pickup; w przeciwnym razie pozwól spróbować jeszcze raz
        if (hasAfter)
        {
            Destroy(transform.root.gameObject);
        }
        else
        {
            pickedUp = false;
        }

        // HUD (aktualny set broni + aktywna)
        var hud = GetGunUI();
        if (hud != null)
        {
            var slots = wm.GetWeaponSlots();
            var allWeapons = BuildOwnedWeaponInstances(wm);

            // aktywna = ta w slocie (o ile została wybrana); jak nie – null i HUD sobie poradzi
            InventoryItemInstance active = null;
            int cur = wm.GetCurrentWeaponIndex();
            if (cur >= 0 && cur < slots.Length && slots[cur] != null)
            {
                active =
                    slots[cur].GetComponentInChildren<Gun>()?.inventoryInstance ??
                    slots[cur].GetComponentInChildren<Grenade>()?.inventoryInstance ??
                    slots[cur].GetComponentInChildren<Melee>()?.inventoryInstance;
            }

            hud.UpdateWeaponHUD(allWeapons, active);
        }
    }
    private int GetSlotIndexFromData(InventoryItemData data)
    {
        if (data is WeaponItemData weaponData)
        {
            return weaponData.category switch
            {
                WeaponCategory.Riffles => 2,
                WeaponCategory.Pistols => 1,
                WeaponCategory.Melees => 0,
                WeaponCategory.Nades => 3,
                _ => -1
            };
        }
        else if (data is MeleeItemData)
            return 0;
        else if (data is GrenadeItemData)
            return 3;

        return -1;
    }
    private bool TryAddToInventoryUI(InventoryItemInstance instance)
    {
        var inventory = GetInventoryUI();
        if (inventory != null)
            inventory.TryAddItem(instance);

        playerWeaponManager?.RefreshWeaponHUD();

        Destroy(gameObject);
        return true;
    }

    public void TryPickUpFromExternalRay()
    {
        if (InventoryUI.IsInventoryOpen || pickedUp || itemData == null) return;

        if (playerWeaponManager == null) playerWeaponManager = GetPlayerWeaponManager();
        if (playerWeaponManager == null) return;

        if (pickedUp || playerWeaponManager == null || itemData == null) return;

        // KeyItem
        if (itemData.isKeyItem)
        {
            TryAddToInventoryUI(new InventoryItemInstance(itemData));
            Destroy(gameObject);
            return;
        }

        // Granat
        if (itemData.prefab != null && itemData.prefab.name == "Grenade")
        {
            PickUp();
            return;
        }

        // jeśli to melee i masz już melee → pozwól tylko na SWAP, inaczej nic
        if (itemData is MeleeItemData && playerWeaponManager != null && playerWeaponManager.HasWeapon(0))
        {
            if (SwapIfSameSlot(playerWeaponManager)) return;
            return; // bez swapu – nic nie rób
        }

        if (!ammoOnly)
        {
            // Najpierw spróbuj SWAP (również dla tego samego modelu)
            if (SwapIfSameSlot(playerWeaponManager))
                return;
        }

        // 🔹 AMMO-ONLY → NAJPIERW próbujemy rezerwa→inventory
        if (ammoOnly)
        {
            var playerGO = playerWeaponManager.GetComponentInParent<PlayerStats>()?.gameObject
                            ?? playerWeaponManager.gameObject;

            if (TryGiveAmmoToPlayer(playerGO))
                Destroy(transform.root.gameObject); // pickup zużyty

            return; // ⬅️ KONIEC dla ammoOnly
        }

        // 🔹 dołóż zapas (ten sam model)
        if (TryAddReserveAmmo(playerWeaponManager))
        {
            Destroy(transform.root.gameObject);
            return;
        }

        // 🔹 inaczej normalny pickup broni (gdy gracz jej nie ma)
        PickUp();
    }

    public void IgnoreAutoPickupFrom(GameObject owner, float seconds)
    {
        lastOwner = owner;
        blockUntil = Time.time + seconds;
    }

    // WeaponPickup.cs
    private bool TryAddReserveAmmo(WeaponManager wm)
    {
        int slotIndex = GetSlotIndexFromData(itemData);
        if (!(itemData is WeaponItemData) || !wm.HasWeapon(slotIndex)) return false;

        var slotGO = wm.GetWeaponSlots()[slotIndex];
        var existingGun = slotGO ? slotGO.GetComponentInChildren<Gun>() : null;
        if (existingGun == null || existingGun.inventoryInstance == null) return false;

        // ✅ DOPIERO TU: upewnij się, że to TEN SAM MODEL
        string existingName = existingGun.inventoryInstance.data?.prefab?.name
                              ?? existingGun.inventoryInstance.data?.itemName;
        string pickupName = itemData?.prefab?.name ?? itemData?.itemName;
        bool sameWeaponModel = !string.IsNullOrEmpty(existingName)
                            && !string.IsNullOrEmpty(pickupName)
                            && existingName == pickupName;
        if (!sameWeaponModel) return false;

        // w tym pickupie "totalAmmo" to NABOJE w magazynku (ammoOnly=true)
        int available = Mathf.Max(0, GetAvailableFromPickup()); // używa (>=0)

        if (available <= 0) return false;

        int cap = existingGun.GetMaxReserveAmmo();
        int reserve = existingGun.GetTotalAmmo();
        int free = Mathf.Max(0, cap - reserve);
        if (free <= 0) return false;

        int add = Mathf.Min(free, available);
        existingGun.SetAmmo(existingGun.GetCurrentAmmo(), reserve + add);

        int leftover = available - add;

        // jeśli to paczka amunicji (magazynek)
        if (ammoOnly)
        {
            if (leftover <= 0)
            {
                return true; // zużyty cały – usuń pickup
            }
            // spróbuj dodać resztę do Inventory jako osobny magazynek
            var inv = GetInventoryUI();
        if (inv != null && ammoInventoryData != null)
        {
                var inst = new InventoryItemInstance(ammoInventoryData, leftover, leftover) { count = 1 };
                // asekuracja:
                if (inst.data is AmmoItemData m && m.individualMagazines && inst.totalAmmo <= 0)
                    inst.totalAmmo = Mathf.Max(inst.currentAmmo, m.amountPerUnit);

                if (inv.TryAddItem(inst)) return true;
        }
            totalAmmo = leftover;
            currentAmmo = leftover;   // ⬅️ ważne dla poprawnego UI przy ponownym podniesieniu
            return false;
        }
                // pickup z bronią – poprzednia ścieżka
                return true;
    }

    private bool TryGiveAmmoToPlayer(GameObject player)
    {
        if (!ammoOnly || player == null) return false;

        Gun gun = SafeFindMatchingGun(player);

        int available = GetAvailableFromPickup();
        int leftover = available;

        // PUSTY MAGAzynek też chcemy dodać do Inventory jako x0
        if (available <= 0)
            return AddLeftoverMagToInventory(0);

    if (gun != null)
        {
            int free = Mathf.Max(0, gun.GetMaxReserveAmmo() - gun.GetTotalAmmo());
            int add = Mathf.Min(leftover, free);
            if (add > 0)
            {
                gun.SetAmmo(gun.GetCurrentAmmo(), gun.GetTotalAmmo() + add); // ⬅️ faktycznie dodaj
                leftover -= add;                                            // ⬅️ i odejmij z pickupa

                // odśwież HUD
                var gunUI = GetGunUI();
                if (gunUI != null)
                {
                    var wm = gun.GetComponentInParent<WeaponManager>();
                    if (wm != null)
                    {
                        var owned = BuildOwnedWeaponInstances(wm);
                        gunUI.UpdateWeaponHUD(owned, gun.inventoryInstance);
                    }
                }
            }
        }

        return AddLeftoverMagToInventory(leftover);
    }

    public int GetAvailableFromPickup()
    {
        // Pokazujemy realną zawartość TEGO pickupa. Bez fallbacków do amountPerUnit.
        if (totalAmmo >= 0) return totalAmmo;
        if (currentAmmo >= 0) return currentAmmo;
        return 0;
    }

    // WeaponPickup.cs
    private Gun SafeFindMatchingGun(GameObject player)
    {
        var wm = player.GetComponentInChildren<WeaponManager>();
        if (wm == null || itemData is not WeaponItemData wd) return null;

        GameObject[] slots = null;
        try
        {
            slots = wm.GetWeaponSlots();   // może rzucić UnassignedReferenceException
        }
        catch (UnassignedReferenceException)
        {
            return null;                   // gracz nie ma skonfigurowanych slotów – traktuj jak brak broni
        }

        if (slots == null || slots.Length == 0) return null;

        foreach (var s in slots)
        {
            if (s == null) continue;
            var g = s.GetComponentInChildren<Gun>(true);
            if (g != null && g.weaponData == wd) return g;
        }
        return null;
    }

    private bool AddLeftoverMagToInventory(int leftover)
    {
        // Jeśli nic nie zostało – NIE dodawaj do inventory.
        // Zostaw pickup jako prop (0 ammo), a resztę logiki zrobi interaktor/UI.
        if (leftover <= 0)
        {
            totalAmmo = 0;
            currentAmmo = 0;
            return false; // NIE niszcz pickupa
        }

        _invCache ??= GetInventoryUI();
        if (_invCache != null && ammoInventoryData != null)
        {
            var inst = new InventoryItemInstance(ammoInventoryData, leftover, leftover) { count = 1 };
            if (_invCache.TryAddItem(inst)) return true; // dodane do UI → można zniszczyć pickup
        }

        // brak miejsca – zostaw na ziemi z realnym stanem
        totalAmmo = leftover;
        currentAmmo = leftover;
        return false;
    }

    public int GetDisplayAmount()
    {
    // Jeśli któraś z wartości jest ustawiona (>=0), pokaż ją – nawet gdy to 0.
    if (totalAmmo >= 0 || currentAmmo >= 0)
    {
        int explicitAmount = (totalAmmo >= 0) ? totalAmmo : currentAmmo;
        return Mathf.Max(0, explicitAmount);
    }

    // Tylko gdy NIC nie ustawiono (oba < 0), użyj fallbacku z definicji paczki.
    int fallback = (ammoInventoryData != null) ? ammoInventoryData.amountPerUnit : 0;
    return Mathf.Max(0, fallback);
    }

    public void EnableImmediatePickFor(WeaponManager wm)
    {
        playerWeaponManager = wm;
        canPickUp = true;
    }

    // WeaponPickup.cs

    // ZAMIANA: swap także dla TAKIEGO SAMEGO modelu (nie tylko innego)
    private bool SwapIfSameSlot(WeaponManager wm)
    {
        if (itemData == null || wm == null) return false;

        int slotIndex = GetSlotIndexFromData(itemData);
        if (slotIndex < 0) return false;

        // jeśli gracz NIE ma broni w tym slocie -> nie ma czego podmieniać
        if (!wm.HasWeapon(slotIndex)) return false;

        // aktualna broń w tym slocie
        var slotGO = wm.GetWeaponSlots()[slotIndex];
        var curGun = slotGO ? slotGO.GetComponentInChildren<Gun>(true) : null;
        var curMelee = slotGO ? slotGO.GetComponentInChildren<Melee>(true) : null;

        // jeżeli to nie gun/melee (dziwny stan) – nie rób nic
        if (curGun == null && curMelee == null) return false;

        // Niezależnie czy to ten sam, czy inny model — wykonaj SWAP:
        wm.DropWeapon(slotIndex); // spawnuje pickup z AKTUALNĄ amunicją tej broni i czyści slot
        PickUp();                 // podnieś tę z ziemi (z jej stanem ammo)
        return true;
    }

    private List<InventoryItemInstance> BuildOwnedWeaponInstances(WeaponManager wm)
    {
        _ownedCache.Clear();

        if (wm == null) return _ownedCache;

        GameObject[] slots = wm.GetWeaponSlots();
        if (slots == null) return _ownedCache;

        for (int i = 0; i < slots.Length; i++)
        {
            GameObject slot = slots[i];
            if (slot == null) continue;

            InventoryItemInstance instance = null;

            Gun gun = slot.GetComponentInChildren<Gun>(true);
            if (gun != null)
                instance = gun.inventoryInstance;

            if (instance == null)
            {
                Grenade grenade = slot.GetComponentInChildren<Grenade>(true);
                if (grenade != null)
                    instance = grenade.inventoryInstance;
            }

            if (instance == null)
            {
                Melee melee = slot.GetComponentInChildren<Melee>(true);
                if (melee != null)
                    instance = melee.inventoryInstance;
            }

            if (instance != null)
                _ownedCache.Add(instance);
        }

        return _ownedCache;
    }
}