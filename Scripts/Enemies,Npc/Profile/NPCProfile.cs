using UnityEngine;

[CreateAssetMenu(menuName = "NPC/NPC Profile", fileName = "NPCProfile")]
public class NPCProfile : ScriptableObject
{
    public enum NPCArchetype
    {
        Civilian,
        BankEmployee,
        Fighter,
        Aggressive,
        Melee,
        Story
    }

    [Header("Identity")]
    public string displayName = "NPC";
    public NPCArchetype archetype = NPCArchetype.Civilian;

    [Header("Core")]
    public NPCCore.NPCImportance importance = NPCCore.NPCImportance.Ambient;
    public float maxHP = 100f;
    public bool invulnerable = false;
    public bool preventDeath = false;

    [Header("NPC Controller")]
    public bool useNPCController = true;
    public NPCController.NPCReactionType reactionType = NPCController.NPCReactionType.Coward;
    public NPCController.FighterVariant fighterVariant = NPCController.FighterVariant.Blue;

    [Header("Weapons")]
    public bool useWeaponSystem = false;
    public bool allowWeaponDrop = false;

    [Range(0f, 100f)]
    public float weaponDropChance = 0f;

    public NPCGun[] availableWeapons;

    [Header("Melee")]
    public bool useMelee = false;
    public int meleeMaxHP = 80;
    public int meleeDamagePerHit = 12;
    public float meleeChaseSpeed = 4.2f;
    public float meleeEnragedChaseSpeed = 6f;

    [Header("Interaction")]
    public bool allowReactiveInteraction = true;
}