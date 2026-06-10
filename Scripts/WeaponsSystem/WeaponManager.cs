using UnityEngine;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    private WeaponHolsterController holsters; // Holsters Controller Reference
    private WeaponHUDNotifier hud; // GUI Hud Controller Reference
    private WeaponViewModelController viewModels; // Weapons View Model Reference
    private WeaponInventorySlots slots; // PlayerInventory Slots Reference
    private WeaponDropController dropper; // WeaponDrop Controller Reference
    private WeaponSwitcher switcher; // WeapnonSwitcher Reference
    private WeaponStateSnapshotController snapshots; // State Snapshots Controller
    private WeaponGrenadeController grenades; // Grenades Controller 
    private WeaponPickupHandler pickupHandler; // Adding Weapons to Slots Controller

    [HideInInspector] public GameObject[] weaponSlots;
    [HideInInspector] public bool[] hasWeapon;

    private int currentWeaponIndex = 0;
    private int previousWeaponIndex = 0;

    [Header("Model rąk (pustych)")]
    private bool forceHandsManual = false;

    public bool IsArmed => currentWeaponIndex >= 0 && weaponSlots[currentWeaponIndex] != null;

    [SerializeField] public InventoryUI inventoryUI; // przypisz ręcznie w Inspectorze

    private void Awake()
    {
        holsters = GetComponent<WeaponHolsterController>();
        hud = GetComponent<WeaponHUDNotifier>();
        viewModels = GetComponent<WeaponViewModelController>();
        slots = GetComponent<WeaponInventorySlots>();
        dropper = GetComponent<WeaponDropController>();
        switcher = GetComponent<WeaponSwitcher>();
        snapshots = GetComponent<WeaponStateSnapshotController>();
        grenades = GetComponent<WeaponGrenadeController>();
        pickupHandler = GetComponent<WeaponPickupHandler>();

        if (slots != null)
        {
            weaponSlots = slots.GetSlots();
            hasWeapon = slots.hasWeapon;
        }
    }
    private void Start()
    {
        ActivateHandsOnly();
        holsters?.Refresh();
    }

    public void SetCurrentWeaponIndex(int index)
    {
        currentWeaponIndex = index;
    }

    public void SetForceHandsManual(bool value)
    {
        forceHandsManual = value;
    }

    public void DropCurrentWeapon()
    {
        dropper?.DropCurrentWeapon();
    }

    public void DropWeapon(int index)
    {
        dropper?.DropWeapon(index);
    }

    public void DropWeapon(int index, InventoryItemInstance dropOverride)
    {
        dropper?.DropWeapon(index, dropOverride);
    }
    public int FindSlotIndexForInstance(InventoryItemInstance instance)
    {
        return slots.FindSlotIndexForInstance(instance);
    }

    public void PickUpWeapon(
      int slotIndex,
      string weaponPrefabName,
      int currentAmmo = -1,
      int totalAmmo = -1,
      InventoryItemInstance instance = null)
    {
        pickupHandler?.PickUpWeapon(
            slotIndex,
            weaponPrefabName,
            currentAmmo,
            totalAmmo,
            instance
        );
    }

    public int GetWeaponIndex(InventoryItemInstance instance)
    {
        if (weaponSlots == null)
        {
            Debug.LogError("❌ weaponSlots nie jest przypisany w WeaponManager!");
            return -1;
        }

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] == null)
                continue;

            var gun = weaponSlots[i].GetComponentInChildren<Gun>();
            var melee = weaponSlots[i].GetComponentInChildren<Melee>();
            var grenade = weaponSlots[i].GetComponentInChildren<Grenade>();

            var heldInstance = gun?.GetInstance() ?? melee?.GetInstance() ?? grenade?.GetInstance();
            if (heldInstance != null && heldInstance.data == instance.data)
                return i;
        }

        return -1;
    }
    public void CheckAutoSwitchWeapon()
    {
        if (currentWeaponIndex < 0 || currentWeaponIndex >= weaponSlots.Length)
            return;

        GameObject currentWeapon = weaponSlots[currentWeaponIndex];

        if (currentWeapon == null || !hasWeapon[currentWeaponIndex])
        {
            switcher?.TrySwitchToAvailableWeapon();
            return;
        }
    }

    public GameObject[] GetWeaponSlots()
    {
        return slots.GetSlots();
    }

    public bool HasWeapon(int index)
    {
        return slots.HasWeapon(index);
    }

    public InventoryItemInstance GetWeaponInstance(int index)
    {
        return slots.GetInstance(index);
    }

    public int GetCurrentWeaponIndex()
    {
        return currentWeaponIndex < 0 ? previousWeaponIndex : currentWeaponIndex;
    }

    public int GetRawCurrentWeaponIndex()
    {
        return currentWeaponIndex;
    }

    public void RefreshWeaponHUD()
    {
        hud?.Refresh();
    }

    public void ActivateHandsOnly()
    {
        if (currentWeaponIndex >= 0 && currentWeaponIndex < weaponSlots.Length && weaponSlots[currentWeaponIndex] != null)
        {
            var oldGun = weaponSlots[currentWeaponIndex].GetComponentInChildren<Gun>(true);
            if (oldGun != null)
                oldGun.ResetAimStateOnHolster();   // ✅
            viewModels?.HideWeapon(weaponSlots[currentWeaponIndex]);
        }

        viewModels?.ShowHands();

        previousWeaponIndex = currentWeaponIndex;
        currentWeaponIndex = -1;

        holsters?.Refresh();
        hud?.RefreshHands();

        forceHandsManual = true;
    }

    public bool IsUsingHandsOnly()
    {
        return currentWeaponIndex == -1 || weaponSlots[currentWeaponIndex] == null || !hasWeapon[currentWeaponIndex];
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= weaponSlots.Length) return;

        bool wasCurrent = currentWeaponIndex == index;

        GameObject oldObj = slots.GetSlotObject(index);
        if (oldObj != null)
            oldObj.SetActive(false);

        slots.SetSlotObject(index, null);
        slots.SetHasWeapon(index, false);

        holsters?.Refresh();

        if (wasCurrent)
        {
            int next = FindBestAvailableWeaponIndexSafe();

            if (next >= 0)
                SelectWeapon(next);
            else
                ActivateHandsOnly();
        }
        else
        {
            hud?.Refresh();
        }
    }

    private int FindBestAvailableWeaponIndexSafe()
    {
        if (hasWeapon == null || weaponSlots == null) return -1;

        int best = -1;
        int bestPriority = int.MinValue;

        for (int i = 0; i < hasWeapon.Length && i < weaponSlots.Length; i++)
        {
            if (!hasWeapon[i]) continue;
            if (weaponSlots[i] == null) continue;

            int p = GetPriorityForSlot(i);
            if (p > bestPriority)
            {
                bestPriority = p;
                best = i;
            }
        }

        return best;
    }

    public InventoryItemInstance GetActiveInstance()
    {
        int index = GetCurrentWeaponIndex();
        if (index < 0 || index >= weaponSlots.Length) return null;

        var obj = slots.GetSlotObject(index);
        if (obj == null) return null;

        return obj.GetComponent<IInventoryItemInstanceProvider>()?.GetInstance();
    }

    public GameObject GetCurrentWeaponSlotObject()
    {
        int index = GetCurrentWeaponIndex();
        if (index < 0 || index >= weaponSlots.Length) return null;
        return slots.GetSlotObject(index);
    }


 

    public WeaponStateSnapshotController.WeaponSnapshot GetSnapshot()
    {
        return snapshots.GetSnapshot();
    }

    public void ApplySnapshot(WeaponStateSnapshotController.WeaponSnapshot snap)
    {
        snapshots.ApplySnapshot(snap);
    }

    public void SelectWeapon(int index)
    {
        switcher?.SelectWeapon(index);
    }

    public void ScrollWeaponInput(int direction)
    {
        switcher?.ScrollWeapon(direction);
    }

    public void ToggleLastUsedWeapon()
    {
        switcher?.ToggleLastUsed();
    }

    public void TrySwitchToAvailableWeapon(int excludeIndex = -1)
    {
        switcher?.TrySwitchToAvailableWeapon(excludeIndex);
    }

    public int GetPriorityForSlot(int index)
    {
        return WeaponSwitcher.GetPriorityForIndex(index);
    }

    public void TryAutoReturnFromHands()
    {
        if (IsUsingHandsOnly() && !forceHandsManual)
        {
            int best = switcher.FindBestAvailableWeaponIndex();
            if (best != -1 && GetCurrentWeaponIndex() != best)
                SelectWeapon(best);
        }
    }

    public int GrenadeCount => grenades != null ? grenades.GrenadeCount : 0;

    public int GetGrenadeCount()
    {
        return grenades != null ? grenades.GetGrenadeCount() : 0;
    }

    public void AddGrenade(int amount = 1)
    {
        grenades?.AddGrenade(amount);
    }

    public void ConsumeGrenade()
    {
        grenades?.ConsumeGrenade();
    }

    public void CheckAutoSwitchWeaponIfNeeded()
    {
        if (!IsUsingHandsOnly())
            CheckAutoSwitchWeapon();
    }

    public bool IsCurrentScopeActive()
    {
        if (IsUsingHandsOnly()) return false;
        if (currentWeaponIndex < 0 || currentWeaponIndex >= weaponSlots.Length) return false;

        var curObj = weaponSlots[currentWeaponIndex];
        var gun = curObj ? curObj.GetComponentInChildren<Gun>(true) : null;

        return gun != null && gun.IsSniperWeapon() && gun.IsScoped();
    }

    public void HandleMeleeInput()
    {
        if (IsUsingHandsOnly()) return;
        if (currentWeaponIndex < 0 || currentWeaponIndex >= weaponSlots.Length) return;

        var weaponObj = weaponSlots[currentWeaponIndex];
        var melee = weaponObj ? weaponObj.GetComponentInChildren<Melee>(true) : null;

        if (melee == null || !weaponObj.activeInHierarchy)
            return;

        bool fastPressed =
            (PlayerInputHandler.Instance?.FirePressed ?? false)
            || Input.GetButtonDown("Fire1");

        bool strongPressed =
            (PlayerInputHandler.Instance?.FireAltPressed ?? false)
            || Input.GetButtonDown("Fire2");

        if (fastPressed) melee.TryFastAttack();
        if (strongPressed) melee.TryStrongAttack();
    }

    public void SyncGrenadeSlotFromInventory(InventoryItemData grenadeData)
    {
        if (grenadeData == null) return;

        if (inventoryUI == null)
            inventoryUI = FindFirstObjectByType<InventoryUI>();

        if (inventoryUI == null) return;

        int total = inventoryUI.GetTotalCountForData(grenadeData);
        InventoryItemInstance firstRemaining = inventoryUI.GetFirstInstanceForData(grenadeData);

        int previousIndex = currentWeaponIndex;
        bool wasHands = IsUsingHandsOnly();
        bool wasNadesActive = currentWeaponIndex == 3;

        // Gracz nie ma już żadnych granatów.
        if (total <= 0 || firstRemaining == null)
        {
            if (HasWeapon(3))
                ClearSlot(3);

            RefreshWeaponHUD();
            return;
        }

        // Gracz ma jeszcze granaty, ale slot Nades mógł wskazywać na usunięty stack.
        // Najbezpieczniej przebudować slot Nades na pierwszą istniejącą instancję.
        if (HasWeapon(3))
        {
            GameObject oldObj = slots.GetSlotObject(3);
            if (oldObj != null)
                oldObj.SetActive(false);

            slots.SetSlotObject(3, null);
            slots.SetHasWeapon(3, false);
        }

        if (firstRemaining.data != null && firstRemaining.data.prefab != null)
        {
            PickUpWeapon(
                3,
                firstRemaining.data.prefab.name,
                firstRemaining.currentAmmo,
                firstRemaining.totalAmmo,
                firstRemaining
            );
        }

        // Transfer/split nie powinien sam zmieniać aktywnej broni poza przypadkiem,
        // gdy gracz miał aktywne granaty.
        if (wasHands)
        {
            ActivateHandsOnly();
        }
        else if (wasNadesActive && HasWeapon(3))
        {
            SelectWeapon(3);
        }
        else if (previousIndex >= 0 && previousIndex != 3 && HasWeapon(previousIndex))
        {
            SelectWeapon(previousIndex);
        }

        RefreshWeaponHUD();
    }
}