using System;

public static class InventoryTransferService
{
    public static bool MoveItemBetweenOwners(
        InventoryItemInstance item,
        IInventorySlotOwner sourceOwner,
        IInventorySlotOwner targetOwner,
        Action onSuccess = null,
        Action onFail = null)
    {
        if (item == null || item.data == null)
        {
            onFail?.Invoke();
            return false;
        }

        if (targetOwner == null)
        {
            onFail?.Invoke();
            return false;
        }

        if (ReferenceEquals(sourceOwner, targetOwner))
        {
            // Ten przypadek obs³uguje grid/place wewn¹trz tego samego UI.
            onSuccess?.Invoke();
            return true;
        }

        bool addedToTarget = targetOwner.TryReceiveItem(item);

        if (!addedToTarget)
        {
            onFail?.Invoke();
            return false;
        }

        bool removedFromSource = sourceOwner == null || sourceOwner.RemoveItemFromOwner(item);

        if (!removedFromSource)
        {
            // Cofka anty-duplikat.
            targetOwner.RemoveItemFromOwner(item);
            onFail?.Invoke();
            return false;
        }

        onSuccess?.Invoke();
        return true;
    }

    public static bool MoveStackAmountBetweenOwners(
        InventoryItemInstance sourceItem,
        int amount,
        IInventorySlotOwner sourceOwner,
        IInventorySlotOwner targetOwner,
        Action<InventoryItemInstance> onCreatedPart = null,
        Action onSuccess = null,
        Action onFail = null)
    {
        if (sourceItem == null || sourceItem.data == null)
        {
            onFail?.Invoke();
            return false;
        }

        if (sourceOwner == null || targetOwner == null)
        {
            onFail?.Invoke();
            return false;
        }

        amount = UnityEngine.Mathf.Clamp(amount, 1, UnityEngine.Mathf.Max(1, sourceItem.count));

        InventoryItemInstance part = InventoryStackService.CloneStackPart(sourceItem, amount);

        if (part == null)
        {
            onFail?.Invoke();
            return false;
        }

        bool added = targetOwner.TryReceiveItem(part);

        if (!added)
        {
            onFail?.Invoke();
            return false;
        }

        bool removed = sourceOwner.RemoveStackAmountFromOwner(sourceItem, amount);

        if (!removed)
        {
            targetOwner.RemoveItemFromOwner(part);
            onFail?.Invoke();
            return false;
        }

        onCreatedPart?.Invoke(part);
        onSuccess?.Invoke();
        return true;
    }
}