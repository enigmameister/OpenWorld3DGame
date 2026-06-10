using static WeaponItemData;
using UnityEngine;

public class MeleeItemData : InventoryItemData
{
    public WeaponSlot weaponSlot = WeaponSlot.Melees; // 👈 Dodaj to pole

    // MeleeItemData.cs
    [Header("Detekcja trafienia")]
    [Tooltip("Promień 'stożka' trafienia (OverlapSphere/SphereCast)")]
    public float hitRadius = 0.35f;

    [Header("Szybki atak")]
    public float fastDamage = 30f;
    public float fastCooldown = 0.6f;

    [Header("Silny atak")]
    public float strongDamage = 60f;
    public float strongCooldown = 1.2f;

    [Header("Zasięg i animacja")]
    public float range = 1.5f;
    public AnimationClip fastAttackAnim;
    public AnimationClip strongAttackAnim;

    public override WeaponSlot GetWeaponSlot()
    {
        return weaponSlot;
    }

}