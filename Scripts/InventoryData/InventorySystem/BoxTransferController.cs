using System;
using System.Collections.Generic;

public class BoxTransferController
{
    private readonly Func<WorldBoxInventory> getCurrentBox;
    private readonly Func<InventoryUI> getInventoryUI;
    private readonly Func<BoxInventoryUI> getBoxUI;

    private readonly Action refreshSlots;
    private readonly Action refreshCapacityTexts;
    private readonly Action refreshCashTexts;
    private readonly Action tryOpenCashTransferBoxToPlayer;
    private readonly Action tryOpenCashTransferPlayerToBox;
    private readonly Action<string> showTransferMessage;

    public BoxTransferController(
        Func<WorldBoxInventory> getCurrentBox,
        Func<InventoryUI> getInventoryUI,
        Func<BoxInventoryUI> getBoxUI,
        Action refreshSlots,
        Action refreshCapacityTexts,
        Action refreshCashTexts,
        Action tryOpenCashTransferBoxToPlayer,
        Action tryOpenCashTransferPlayerToBox,
        Action<string> showTransferMessage)
    {
        this.getCurrentBox = getCurrentBox;
        this.getInventoryUI = getInventoryUI;
        this.getBoxUI = getBoxUI;
        this.refreshSlots = refreshSlots;
        this.refreshCapacityTexts = refreshCapacityTexts;
        this.refreshCashTexts = refreshCashTexts;
        this.tryOpenCashTransferBoxToPlayer = tryOpenCashTransferBoxToPlayer;
        this.tryOpenCashTransferPlayerToBox = tryOpenCashTransferPlayerToBox;
        this.showTransferMessage = showTransferMessage;
    }

    public void TransferAllBoxToPlayer()
    {
        WorldBoxInventory box = getCurrentBox?.Invoke();
        InventoryUI inventory = getInventoryUI?.Invoke();
        BoxInventoryUI boxUI = getBoxUI?.Invoke();

        if (box == null || inventory == null || boxUI == null)
            return;

        HashSet<int> duplicateSlots = GetDuplicateNonStackWeaponSlotsInBox(box);

        bool skippedDuplicateType = false;
        bool blockedByPlayerWeapon = false;

        List<InventoryItemInstance> copy = new List<InventoryItemInstance>(box.GetItems());

        foreach (InventoryItemInstance item in copy)
        {
            if (item == null || item.data == null)
                continue;

            if (InventoryWeaponBridge.IsCombatItemData(item.data))
            {
                int slotIndex = InventoryWeaponBridge.GetCombatSlotIndexFromData(item.data);

                // Je¿eli w boxie s¹ np. dwa karabiny, nie przerzucamy automatem obu.
                if (slotIndex != 3 && duplicateSlots.Contains(slotIndex))
                {
                    skippedDuplicateType = true;
                    continue;
                }

                // Je¿eli gracz ju¿ ma ten typ broni, blokujemy.
                if (slotIndex != 3 && !inventory.CanReceiveWeaponFromBox(item))
                {
                    blockedByPlayerWeapon = true;
                    continue;
                }

                inventory.TryTransferCombatItemFromBoxToPlayer(item, boxUI);
                continue;
            }

            if (inventory.TryAddItem(item))
                box.GetItems().Remove(item);
        }

        if (skippedDuplicateType)
            showTransferMessage?.Invoke("TWO WEAPONS SAME TYPE - TRANSFER MANUALLY");
        else if (blockedByPlayerWeapon)
            showTransferMessage?.Invoke("ALREADY WEAPON THIS TYPE");

        inventory.RefreshGunUIFromWeaponManager();

        refreshSlots?.Invoke();
        refreshCapacityTexts?.Invoke();
        refreshCashTexts?.Invoke();

        tryOpenCashTransferBoxToPlayer?.Invoke();
    }

    public void TransferAllPlayerToBox()
    {
        WorldBoxInventory box = getCurrentBox?.Invoke();
        InventoryUI inventory = getInventoryUI?.Invoke();
        BoxInventoryUI boxUI = getBoxUI?.Invoke();

        if (box == null || inventory == null || boxUI == null)
            return;

        List<InventoryItemInstance> playerItems = inventory.GetAllInstancesDistinct();

        foreach (InventoryItemInstance item in playerItems)
        {
            if (item == null || item.data == null)
                continue;

            InventoryTransferService.MoveItemBetweenOwners(
                item,
                inventory,
                boxUI
            );
        }

        inventory.RefreshGunUIFromWeaponManager();

        refreshSlots?.Invoke();
        refreshCapacityTexts?.Invoke();
        refreshCashTexts?.Invoke();

        tryOpenCashTransferPlayerToBox?.Invoke();
    }

    private HashSet<int> GetDuplicateNonStackWeaponSlotsInBox(WorldBoxInventory box)
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();
        HashSet<int> duplicates = new HashSet<int>();

        if (box == null)
            return duplicates;

        foreach (InventoryItemInstance item in box.GetItems())
        {
            if (item == null || item.data == null)
                continue;

            if (!InventoryWeaponBridge.IsCombatItemData(item.data))
                continue;

            int slot = InventoryWeaponBridge.GetCombatSlotIndexFromData(item.data);

            // Granaty pomijamy — s¹ stackowalne.
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
}