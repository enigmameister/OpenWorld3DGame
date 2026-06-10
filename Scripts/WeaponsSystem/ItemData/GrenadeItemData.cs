using UnityEngine;
using static WeaponItemData;

[CreateAssetMenu(menuName = "Inventory/Grenade Item Data")]
public class GrenadeItemData : InventoryItemData
{
    [Header("Granat")]
    public float explosionRadius = 3.5f;
    public float explosionDelay = 2f;
    public float explosionDamage = 100f;

    [Tooltip("Prefab rzucanego granatu (np. z animacj¹ lotu)")]
    public GameObject thrownPrefab;

    public override WeaponSlot GetWeaponSlot()
    {
        return WeaponSlot.Nades;
    }

}