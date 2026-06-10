using System.Collections.Generic;
using UnityEngine;

public class WeaponHUDNotifier : MonoBehaviour
{
    private WeaponManager weaponManager;
    private GunUI gunUI;

    private readonly List<InventoryItemInstance> owned = new();

    void Awake()
    {
        weaponManager = GetComponent<WeaponManager>();
        gunUI = FindFirstObjectByType<GunUI>();
    }

    public void Refresh()
    {
        RefreshInternal(useActiveWeapon: true);
    }

    public void RefreshHands()
    {
        RefreshInternal(useActiveWeapon: false);
    }

    private void RefreshInternal(bool useActiveWeapon)
    {
        if (weaponManager == null) return;

        if (gunUI == null)
            gunUI = FindFirstObjectByType<GunUI>();

        if (gunUI == null) return;

        GameObject[] slots = weaponManager.GetWeaponSlots();

        owned.Clear();

        for (int i = 0; i < slots.Length; i++)
        {
            InventoryItemInstance instance = GetInstanceFromSlot(slots[i]);

            if (instance != null)
                owned.Add(instance);
        }

        InventoryItemInstance active = null;

        if (useActiveWeapon && !weaponManager.IsUsingHandsOnly())
        {
            int index = weaponManager.GetRawCurrentWeaponIndex();

            if (index >= 0 && index < slots.Length)
                active = GetInstanceFromSlot(slots[index]);
        }

        gunUI.UpdateWeaponHUD(owned, active);
    }

    private InventoryItemInstance GetInstanceFromSlot(GameObject slot)
    {
        if (slot == null) return null;

        Gun gun = slot.GetComponentInChildren<Gun>(true);
        if (gun != null)
            return gun.inventoryInstance;

        Grenade grenade = slot.GetComponentInChildren<Grenade>(true);
        if (grenade != null)
            return grenade.inventoryInstance;

        Melee melee = slot.GetComponentInChildren<Melee>(true);
        if (melee != null)
            return melee.inventoryInstance;

        return null;
    }
}