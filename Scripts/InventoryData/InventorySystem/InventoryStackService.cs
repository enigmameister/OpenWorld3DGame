using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class InventoryStackService
{
    public static bool CanSplitStack(InventoryItemInstance item)
    {
        if (item == null || item.data == null)
            return false;

        if (item.count <= 1)
            return false;

        if (item.data is AmmoItemData ammo && ammo.individualMagazines)
            return false;

        if (item.data is BankCardItemData)
            return false;

        if (item.hasBankCardMeta)
            return false;

        return true;
    }

    public static bool CanMergeStacks(InventoryItemInstance source, InventoryItemInstance target)
    {
        if (source == null || target == null)
            return false;

        if (source == target)
            return false;

        if (source.data == null || target.data == null)
            return false;

        if (source.data != target.data)
            return false;

        if (source.data is AmmoItemData ammo && ammo.individualMagazines)
            return false;

        if (source.data is BankCardItemData || target.data is BankCardItemData)
            return false;

        if (source.hasBankCardMeta || target.hasBankCardMeta)
            return false;

        return true;
    }

    public static InventoryItemInstance CloneStackPart(InventoryItemInstance source, int amount)
    {
        if (source == null || source.data == null)
            return null;

        amount = Mathf.Clamp(amount, 1, source.count);

        InventoryItemInstance copy;

        if (source.hasBankCardMeta && source.data is BankCardItemData cardData)
        {
            copy = new InventoryItemInstance(cardData, source.bankCard);
        }
        else
        {
            copy = new InventoryItemInstance(
                source.data,
                source.currentAmmo,
                source.totalAmmo
            );
        }

        copy.count = amount;
        copy.rotated = source.rotated;

        return copy;
    }

    public static bool IsStackSplitModifierHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            return keyboard.leftShiftKey.isPressed ||
                   keyboard.rightShiftKey.isPressed;
        }

        return false;
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }

    public static int GetMaxSplitAmount(InventoryItemInstance sourceItem, bool sameOwner)
    {
        if (sourceItem == null)
            return 0;

        return sameOwner
            ? Mathf.Max(1, sourceItem.count - 1)
            : sourceItem.count;
    }

    public static bool IsStackQuickSplitModifierHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            return keyboard.leftCtrlKey.isPressed ||
                   keyboard.rightCtrlKey.isPressed;
        }

        return false;
#else
    return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#endif
    }

    public static int GetQuickSplitHalfAmount(InventoryItemInstance sourceItem, bool sameOwner)
    {
        if (sourceItem == null)
            return 0;

        if (!CanSplitStack(sourceItem))
            return 0;

        int maxAmount = GetMaxSplitAmount(sourceItem, sameOwner);

        if (maxAmount <= 0)
            return 0;

        // x4 -> 2, x5 -> 2, x3 -> 1, x2 -> 1
        int half = Mathf.FloorToInt(sourceItem.count / 2f);

        return Mathf.Clamp(half, 1, maxAmount);
    }

    public static bool TryMergeDraggedStackIntoSlot(
    InventoryItemInstance dragged,
    InventorySlot targetSlot,
    IInventorySlotOwner sourceOwner,
    System.Action<InventoryItemInstance> refreshCountDisplay,
    System.Action afterSuccess)
    {
        if (dragged == null || dragged.data == null)
            return false;

        if (targetSlot == null || targetSlot.item == null || targetSlot.item.data == null)
            return false;

        InventoryItemInstance target = targetSlot.item;

        if (!CanMergeStacks(dragged, target))
            return false;

        int addAmount = Mathf.Max(1, dragged.count);

        // Najpierw zwiêkszamy target.
        target.count += addAmount;

        refreshCountDisplay?.Invoke(target);

        // Potem usuwamy Ÿród³o z w³aœciciela: Inventory albo Box.
        bool removedFromSource = sourceOwner != null && sourceOwner.RemoveItemFromOwner(dragged);

        if (!removedFromSource)
        {
            // Cofka anty-duplikat, gdyby source nie da³o siê usun¹æ.
            target.count = Mathf.Max(1, target.count - addAmount);
            refreshCountDisplay?.Invoke(target);
            return false;
        }

        refreshCountDisplay?.Invoke(target);
        afterSuccess?.Invoke();

        return true;
    }
}