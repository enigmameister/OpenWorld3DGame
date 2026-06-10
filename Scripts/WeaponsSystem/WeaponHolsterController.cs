using System.Collections.Generic;
using UnityEngine;

public class WeaponHolsterController : MonoBehaviour
{
    [System.Serializable]
    public class HolsterEntry
    {
        public InventoryItemData itemData;
        public GameObject holsterObject;
    }

    [Header("Holstery")]
    public List<HolsterEntry> holsterEntries = new();

    private Dictionary<string, GameObject> holsteredModels = new();

    private WeaponManager weaponManager;

    void Awake()
    {
        weaponManager = GetComponent<WeaponManager>();

        foreach (var entry in holsterEntries)
        {
            if (entry.itemData != null && entry.holsterObject != null)
            {
                string name =
                    entry.itemData.prefab != null
                    ? entry.itemData.prefab.name
                    : entry.itemData.name;

                holsteredModels[name] = entry.holsterObject;

                entry.holsterObject.SetActive(false);
            }
        }
    }

    public void Refresh()
    {
        if (weaponManager == null) return;

        // 1. Najpierw schowaj wszystkie holstery
        HideAll();

        var weaponSlots = weaponManager.GetWeaponSlots();
        int currentWeaponIndex = weaponManager.GetRawCurrentWeaponIndex();
        bool handsActive = weaponManager.IsUsingHandsOnly();

        // 2. W³¹cz tylko te, które gracz naprawdê posiada
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            var weaponObj = weaponSlots[i];

            if (weaponObj == null)
                continue;

            var gun = weaponObj.GetComponentInChildren<Gun>(true);
            var grenade = weaponObj.GetComponentInChildren<Grenade>(true);
            var melee = weaponObj.GetComponentInChildren<Melee>(true);

            var instance =
                  gun?.inventoryInstance
               ?? grenade?.inventoryInstance
               ?? melee?.inventoryInstance;

            if (instance == null || instance.data == null)
                continue;

            string weaponName =
                instance.data.prefab != null
                ? instance.data.prefab.name
                : instance.data.name;

            if (holsteredModels.TryGetValue(weaponName, out var holster))
            {
                bool isCurrent = i == currentWeaponIndex && !handsActive;

                // aktualna broñ w rêce = schowana z holstera
                // broñ nieaktywna / rêce aktywne = widoczna na ciele
                holster.SetActive(!isCurrent);
            }
        }
    }

    public void HideAll()
    {
        foreach (var kvp in holsteredModels)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(false);
        }
    }
}