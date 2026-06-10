public interface IInventorySlotOwner
{
    void OnSlotClicked(InventorySlot slot);
    bool TryReceiveItem(InventoryItemInstance item);
    bool RemoveItemFromOwner(InventoryItemInstance item);
    bool RemoveStackAmountFromOwner(InventoryItemInstance item, int amount);
}