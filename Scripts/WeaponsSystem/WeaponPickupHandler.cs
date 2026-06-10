using UnityEngine;
using System.Collections.Generic;

public class WeaponPickupHandler : MonoBehaviour
{
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private WeaponInventorySlots slots;
    [SerializeField] private WeaponHolsterController holsters;
    [SerializeField] private WeaponHUDNotifier hud;
    [SerializeField] private WeaponSwitcher switcher;
    [SerializeField] private Transform weaponInventory;

    [SerializeField] private InventoryUI inventoryUI;

    private readonly HashSet<string> acquiredWeapons = new();

    void Awake()
    {
        if (!weaponManager) weaponManager = GetComponent<WeaponManager>();
        if (!slots) slots = GetComponent<WeaponInventorySlots>();
        if (!holsters) holsters = GetComponent<WeaponHolsterController>();
        if (!hud) hud = GetComponent<WeaponHUDNotifier>();
        if (!switcher) switcher = GetComponent<WeaponSwitcher>();

        if (!inventoryUI) inventoryUI = weaponManager ? weaponManager.inventoryUI : null;
        if (!inventoryUI) inventoryUI = FindFirstObjectByType<InventoryUI>();
    }

    public void PickUpWeapon(
        int slotIndex,
        string weaponPrefabName,
        int currentAmmo = -1,
        int totalAmmo = -1,
        InventoryItemInstance instance = null)
    {
        if (slotIndex < 0 || slotIndex >= slots.GetSlots().Length)
        {
            Debug.LogWarning($"❌ PickUpWeapon: Nieprawidłowy slotIndex = {slotIndex}");
            return;
        }

        if (slotIndex == 0 && slots.HasWeapon(0))
        {
            Debug.Log("⚠️ Już masz melee – ignoruję pickup.");
            return;
        }

        GameObject found = FindWeaponObject(weaponPrefabName);

        if (found == null)
        {
            Debug.LogWarning($"❌ PickUpWeapon: Nie znaleziono prefab '{weaponPrefabName}' w weaponInventory");
            return;
        }

        if (slots.GetSlotObject(slotIndex) != null && slotIndex != 3)
        {
            slots.GetSlotObject(slotIndex).SetActive(false);
            slots.SetSlotObject(slotIndex, null);
        }

        slots.SetSlotObject(slotIndex, found);
        slots.SetHasWeapon(slotIndex, true);
        found.SetActive(false);

        acquiredWeapons.Add(weaponPrefabName);

        AssignInstance(slotIndex, found, instance, currentAmmo, totalAmmo);
        DecideSelection(slotIndex, found);

        hud?.Refresh();
    }

    private GameObject FindWeaponObject(string weaponPrefabName)
    {
        if (!weaponInventory) return null;

        foreach (Transform category in weaponInventory)
        {
            foreach (Transform weapon in category)
            {
                if (weapon.gameObject.name == weaponPrefabName)
                    return weapon.gameObject;
            }
        }

        return null;
    }

    private void AssignInstance(
        int slotIndex,
        GameObject found,
        InventoryItemInstance instance,
        int currentAmmo,
        int totalAmmo)
    {
        if (instance == null) return;

        InventoryItemInstance usedInstance = instance;

        var fromUI = inventoryUI != null
            ? inventoryUI.GetInstanceForItem(instance.data)
            : null;

        if (fromUI != null)
            usedInstance = fromUI;

        if (slotIndex == 3)
        {
            var grenade = found.GetComponentInChildren<Grenade>(true);

            if (grenade != null)
            {
                grenade.SetInventoryInstance(usedInstance);
                slots.SetHasWeapon(3, true);

                if (weaponManager.GetCurrentWeaponIndex() == 3)
                {
                    found.SetActive(true);
                    switcher?.SelectWeapon(3);
                }
            }

            return;
        }

        var gun = found.GetComponentInChildren<Gun>(true);
        if (gun != null)
        {
            gun.SetInventoryInstance(usedInstance);

            if (usedInstance.data is WeaponItemData weaponData)
                gun.ApplyWeaponData(weaponData);

            if (currentAmmo >= 0 || totalAmmo >= 0)
                gun.SetAmmo(currentAmmo, totalAmmo);
        }

        var melee = found.GetComponentInChildren<Melee>(true);
        if (melee != null)
            melee.inventoryInstance = usedInstance;
    }

    private void DecideSelection(int slotIndex, GameObject found)
    {
        int currentIndex = weaponManager.GetRawCurrentWeaponIndex();

        int newPriority = weaponManager.GetPriorityForSlot(slotIndex);
        int currentPriority = currentIndex >= 0
            ? weaponManager.GetPriorityForSlot(currentIndex)
            : -1;

        bool isUsingHands = weaponManager.IsUsingHandsOnly();

        bool shouldSelect =
            isUsingHands ||
            currentIndex < 0 ||
            newPriority > currentPriority;

        bool sameActiveSlot =
            currentIndex == slotIndex && !isUsingHands;

        if (shouldSelect)
        {
            if (currentIndex != -1 &&
                currentIndex != slotIndex &&
                slots.GetSlotObject(currentIndex) != null)
            {
                slots.GetSlotObject(currentIndex).SetActive(false);
            }

            found.SetActive(true);
            switcher?.SelectWeapon(slotIndex);
        }
        else
        {
            if (sameActiveSlot)
            {
                found.SetActive(true);
            }
            else
            {
                found.SetActive(false);
                holsters?.Refresh();
            }
        }
    }
}