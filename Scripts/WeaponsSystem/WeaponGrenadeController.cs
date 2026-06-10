using UnityEngine;

public class WeaponGrenadeController : MonoBehaviour
{
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private WeaponInventorySlots slots;
    [SerializeField] private WeaponHolsterController holsters;
    [SerializeField] private WeaponHUDNotifier hud;
    [SerializeField] private Transform weaponInventory;

    [SerializeField] private InventoryUI inventoryUI;

    private int grenadeCount = 0;

    public int GrenadeCount => GetGrenadeCount();

    void Awake()
    {
        if (!weaponManager) weaponManager = GetComponent<WeaponManager>();
        if (!slots) slots = GetComponent<WeaponInventorySlots>();
        if (!holsters) holsters = GetComponent<WeaponHolsterController>();
        if (!hud) hud = GetComponent<WeaponHUDNotifier>();

        if (!inventoryUI) inventoryUI = weaponManager ? weaponManager.inventoryUI : null;
        if (!inventoryUI) inventoryUI = FindFirstObjectByType<InventoryUI>();
    }

    public int GetGrenadeCount()
    {
        GameObject slotObj = slots.GetSlotObject(3);
        if (slotObj == null) return 0;

        var grenade = slotObj.GetComponentInChildren<Grenade>(true);
        var instance = grenade?.GetInstance();

        return instance?.count ?? 0;
    }

    public void AddGrenade(int amount = 1)
    {
        grenadeCount += amount;

        GameObject slotObj = slots.GetSlotObject(3);

        if (slotObj == null || slotObj.GetComponentInChildren<Grenade>(true) == null)
        {
            if (!weaponInventory || weaponInventory.childCount <= 3)
            {
                Debug.LogWarning("[WeaponGrenadeController] Brak weaponInventory albo slotu Nades.");
                return;
            }

            Transform nadesParent = weaponInventory.GetChild(3);
            Transform storedGrenade = nadesParent.Find("Grenade");

            if (storedGrenade != null)
            {
                GameObject g = storedGrenade.gameObject;

                if (g.GetComponentInChildren<Grenade>(true) == null)
                {
                    Debug.LogError("[AddGrenade] ❌ Prefab w slot 3 nie zawiera komponentu Grenade!");
                    return;
                }

                g.SetActive(false);
                slots.SetSlotObject(3, g);
                slots.SetHasWeapon(3, true);
            }
        }
        else
        {
            if (!slotObj.activeSelf)
                slotObj.SetActive(true);

            slots.SetHasWeapon(3, true);
        }

        hud?.Refresh();
    }

    public void ConsumeGrenade()
    {
        GameObject slotObj = slots.GetSlotObject(3);
        var grenade = slotObj ? slotObj.GetComponentInChildren<Grenade>(true) : null;
        var instance = grenade?.GetInstance();

        if (instance == null) return;

        if (inventoryUI == null) return;

        inventoryUI.RemoveItem(instance);
        inventoryUI.RefreshCountDisplay(instance);

        grenade.SetInventoryInstance(instance);
        grenadeCount = instance.count;

        if (grenadeCount <= 0)
        {
            if (slotObj != null)
                slotObj.SetActive(false);

            slots.SetSlotObject(3, null);
            slots.SetHasWeapon(3, false);

            if (weaponManager.GetRawCurrentWeaponIndex() == 3)
            {
                weaponManager.SetCurrentWeaponIndex(-1);
                weaponManager.TrySwitchToAvailableWeapon();
            }

            holsters?.Refresh();
            hud?.Refresh();
            return;
        }

        hud?.Refresh();
    }
}