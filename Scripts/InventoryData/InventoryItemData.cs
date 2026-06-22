using UnityEngine;
using static WeaponItemData;

[CreateAssetMenu(menuName = "Inventory/Item")]
public class InventoryItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public int slotSize; // np. 1 - ma³y, 2 - œredni, 3 - du¿y
    public GameObject prefab;

    public bool isKeyItem; // ?? np. true dla breloczka
    public string keyId;   // np. "GateA", "RoomB"

    [Header("Stacking")]
    public bool stackable = false;
    public int maxStack = 99;

    [TextArea]
    public string description;

    public virtual WeaponSlot GetWeaponSlot()
    {
        return WeaponSlot.Riffles; // domyœlnie, nadpisane w pochodnych
    }
}



