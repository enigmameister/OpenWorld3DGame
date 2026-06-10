using System.Collections.Generic;
using UnityEngine;

public enum WeaponCategory
{
    Melees,
    Pistols,
    Riffles,
    Nades
}

[CreateAssetMenu(menuName = "Inventory/Weapon Item Data")]
public class WeaponItemData : InventoryItemData
{
    [Header("Kategoria broni")]
    public WeaponCategory category;
    public enum WeaponSlot { Melees = 0, Pistols = 1, Riffles = 2, Nades = 3 }
    public WeaponSlot weaponSlot;

    [Header("Weapon Settings")]
    public float fireRate = 0.1f;
    public float bulletSpeed = 50f;
    public int magazineSize = 30;
    public float reloadTime = 1.5f;
    public float damage = 25f;
    public string bulletType = "Bullet";

    [Header("Shotgun Settings")]
    public float minShotgunDamage = 5f;
    public float shotgunRange = 10f;

    [Header("Recoil")]
    public List<Vector2> recoilPattern = new();
    public float recoilResetTime = 0.4f;

    [Header("ADS & Zoom")]
    public float adsFOV = 40f;
    public bool isSniper = false;
    public bool isShotgun = false;

    public enum WeaponWeight { Light, Medium, Heavy }
    public WeaponWeight carryWeight = WeaponWeight.Medium;

    // Mno¿nik prêdkoœci biegu/chodu gdy broñ jest wyjêta (1 = bez zmian)
    [Range(0.5f, 1.2f)] public float moveSpeedMultiplier = 1f;

    // Mno¿nik kosztu staminy (drain) przy sprincie i wysi³ku (1 = bez zmian)
    [Range(0.5f, 2f)] public float staminaDrainMultiplier = 1f;

    [Header("Animations")]
    public AnimatorOverrideController overrideController;
    public AnimationClip fireClip;
    public AnimationClip reloadClip;
    public AnimationClip pickupClip;

    public override WeaponSlot GetWeaponSlot()
    {
        return weaponSlot;
    }

    public (float moveMul, float staminaMul) GetDefaultLoad()
    {
        switch (category)
        {
            case WeaponCategory.Melees: return (1.02f, 0.95f);  // ciut szybciej, tañsza stamina
            case WeaponCategory.Pistols: return (0.96f, 1.05f);  // lekki koszt
            case WeaponCategory.Riffles: return (0.88f, 1.20f);  // wyraŸny koszt
            case WeaponCategory.Nades: return (0.98f, 1.00f);
            default: return (1f, 1f);
        }
    }

}