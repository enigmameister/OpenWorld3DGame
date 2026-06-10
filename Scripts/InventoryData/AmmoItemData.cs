using UnityEngine;
using static WeaponItemData;

[CreateAssetMenu(menuName = "Inventory/Ammo")]
public class AmmoItemData : InventoryItemData
{
    [Header("Powi¹zanie")]
    public WeaponItemData weapon;        // do jakiej broni pasuje

    [Header("Parametry paczki")]
    public int amountPerUnit = 30;       // ile ammo daje 1 szt. w stacku

    // NOWE: ka¿dy magazynek jest osobnym itemem (nie scala siê w stack)
    public bool individualMagazines = true;

    public override WeaponSlot GetWeaponSlot()
    {
        return weapon != null ? weapon.weaponSlot : base.GetWeaponSlot();
    }
}