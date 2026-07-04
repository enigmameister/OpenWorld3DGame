using System;
using System.Collections.Generic;
using UnityEngine;
using static WeaponItemData;

public class InventoryWeaponBridge
{
    private readonly Func<WeaponManager> getWeaponManager;
    private readonly Func<GunUI> getGunUI;
    private readonly Func<List<InventoryItemInstance>> getOwnedItemsForGunUI;

    public InventoryWeaponBridge(
        Func<WeaponManager> getWeaponManager,
        Func<GunUI> getGunUI,
        Func<List<InventoryItemInstance>> getOwnedItemsForGunUI)
    {
        this.getWeaponManager = getWeaponManager;
        this.getGunUI = getGunUI;
        this.getOwnedItemsForGunUI = getOwnedItemsForGunUI;
    }

    public static bool IsCombatItemData(InventoryItemData data)
    {
        return data is WeaponItemData
            || data is MeleeItemData
            || data is GrenadeItemData;
    }

    public static int GetCombatSlotIndexFromData(InventoryItemData data)
    {
        if (data == null)
            return -1;

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

    public bool CanReceiveWeaponFromBox(InventoryItemInstance item)
    {
        if (item == null || item.data == null)
            return false;

        // Zwyk³y item zawsze mo¿e wejœæ, jeœli jest miejsce.
        if (!IsCombatItemData(item.data))
            return true;

        WeaponManager wm = getWeaponManager?.Invoke();

        if (wm == null)
            return false;

        int slotIndex = GetCombatSlotIndexFromData(item.data);

        if (slotIndex < 0)
            return false;

        // Granaty s¹ stackowalne.
        if (slotIndex == 3)
            return true;

        // Melee / Pistol / Riffle: tylko jedna broñ danego typu.
        return !wm.HasWeapon(slotIndex);
    }

    public bool RegisterWeaponFromBoxTransfer(InventoryItemInstance item)
    {
        if (item == null || item.data == null)
            return false;

        if (!IsCombatItemData(item.data))
            return true;

        WeaponManager wm = getWeaponManager?.Invoke();

        if (wm == null)
            return false;

        int slotIndex = GetCombatSlotIndexFromData(item.data);

        if (slotIndex < 0)
            return false;

        bool isNade = slotIndex == 3;

        if (!isNade && wm.HasWeapon(slotIndex))
            return false;

        bool wasHands = wm.IsUsingHandsOnly();
        int previousIndex = wm.GetRawCurrentWeaponIndex();

        if (item.data.prefab == null)
        {
            Debug.LogWarning($"[InventoryWeaponBridge] Item '{item.data.name}' nie ma prefabu.");
            return false;
        }

        wm.PickUpWeapon(
            slotIndex,
            item.data.prefab.name,
            item.currentAmmo,
            item.totalAmmo,
            item
        );

        // Transfer z boxa nie powinien wymuszaæ zmiany aktywnej broni.
        if (wasHands)
        {
            wm.ActivateHandsOnly();
        }
        else if (previousIndex >= 0 &&
                 previousIndex != slotIndex &&
                 wm.HasWeapon(previousIndex))
        {
            wm.SelectWeapon(previousIndex);
        }

        RefreshGunUI();
        return true;
    }

    public void RemoveCombatItemIfNeeded(InventoryItemInstance item)
    {
        if (item == null || item.data == null)
            return;

        if (!IsCombatItemData(item.data))
            return;

        WeaponManager wm = getWeaponManager?.Invoke();

        if (wm == null)
            return;

        int weaponIndex = wm.FindSlotIndexForInstance(item);

        if (weaponIndex < 0)
            weaponIndex = wm.GetWeaponIndex(item);

        if (weaponIndex < 0)
            weaponIndex = GetCombatSlotIndexFromData(item.data);

        if (weaponIndex >= 0)
            wm.ClearSlot(weaponIndex);
    }

    public void SyncGrenadeSlotFromInventory(InventoryItemData grenadeData)
    {
        if (grenadeData == null)
            return;

        WeaponManager wm = getWeaponManager?.Invoke();

        if (wm == null)
            return;

        wm.SyncGrenadeSlotFromInventory(grenadeData);
        RefreshGunUI();
    }

    public void RefreshGunUI()
    {
        WeaponManager wm = getWeaponManager?.Invoke();

        if (wm == null)
            return;

        wm.RefreshWeaponHUD();

        GunUI gunUI = getGunUI?.Invoke();

        if (gunUI == null)
            return;

        List<InventoryItemInstance> owned = getOwnedItemsForGunUI != null
            ? getOwnedItemsForGunUI.Invoke()
            : new List<InventoryItemInstance>();

        InventoryItemInstance active = wm.IsUsingHandsOnly()
            ? null
            : wm.GetActiveInstance();

        gunUI.UpdateWeaponHUD(owned, active);
    }

    public bool TryTransferCombatItemFromBoxToPlayer(
    InventoryItemInstance item,
    IInventorySlotOwner boxOwner,
    IInventorySlotOwner playerOwner,
    System.Action removeFromPlayerOnFail = null)
    {
        if (item == null || item.data == null)
            return false;

        if (!IsCombatItemData(item.data))
            return false;

        if (boxOwner == null || playerOwner == null)
            return false;

        if (!CanReceiveWeaponFromBox(item))
            return false;


        bool addedToPlayer = false;

        if (playerOwner is InventoryUI inventoryUI)
        {
            addedToPlayer = inventoryUI.TryReceiveItemFromBoxWithoutWeaponAutoRegister(item);
        }
        else
        {
            addedToPlayer = playerOwner.TryReceiveItem(item);
        }

        if (!addedToPlayer)
            return false;

        // Granaty s¹ stackowalne — po dodaniu do Inventory wystarczy usun¹æ z Boxa
        // i zsynchronizowaæ WeaponManager/GunUI.
        if (item.data is GrenadeItemData)
        {
            boxOwner.RemoveItemFromOwner(item);

            WeaponManager wm = getWeaponManager?.Invoke();
            if (wm != null)
                wm.SyncGrenadeSlotFromInventory(item.data);

            RefreshGunUI();
            return true;
        }

        // Broñ/melee musi zostaæ zarejestrowana w WeaponManager.
        if (!RegisterWeaponFromBoxTransfer(item))
        {
            removeFromPlayerOnFail?.Invoke();
            return false;
        }

        boxOwner.RemoveItemFromOwner(item);
        RefreshGunUI();

        return true;
    }
}