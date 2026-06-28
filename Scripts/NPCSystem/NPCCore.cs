using System;
using UnityEngine;

public class NPCCore : MonoBehaviour
{
    public enum NPCImportance
    {
        Ambient,        // zwyk³y NPC / przechodzieñ
        Mission,        // wa¿ny dla misji, mo¿e dostaæ obra¿enia, ale nie powinien zgin¹æ
        StoryCritical   // fabularny, ca³kowicie odporny
    }

    [Header("Identity")]
    [SerializeField] private string npcId;
    [SerializeField] private string displayName = "NPC";
    [SerializeField] private NPCImportance importance = NPCImportance.Ambient;

    [Header("Health")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float currentHP = 100f;

    [Header("Damage Rules")]
    [SerializeField] private bool invulnerable = false;

    [Tooltip("NPC mo¿e otrzymaæ obra¿enia, ale nie spadnie poni¿ej 1 HP.")]
    [SerializeField] private bool preventDeath = false;

    [Header("Runtime")]
    [SerializeField] private bool isDead = false;

    public string NpcId => npcId;
    public string DisplayName => displayName;
    public NPCImportance Importance => importance;

    public float MaxHP => maxHP;
    public float CurrentHP => currentHP;
    public bool IsDead => isDead;

    public bool IsInvulnerable =>
        invulnerable || importance == NPCImportance.StoryCritical;

    public bool PreventDeath =>
        preventDeath || importance == NPCImportance.Mission;

    public event Action<NPCCore, string, float> Damaged;
    public event Action<NPCCore, string> DeathRequested;
    public event Action<NPCCore, string> Died;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(npcId))
            npcId = gameObject.name;

        currentHP = Mathf.Clamp(currentHP <= 0f ? maxHP : currentHP, 0f, maxHP);
    }

    public void ApplyProfile(NPCProfile profile)
    {
        if (profile == null) return;

        displayName = profile.displayName;
        importance = profile.importance;

        maxHP = Mathf.Max(1f, profile.maxHP);
        currentHP = maxHP;

        invulnerable = profile.invulnerable;
        preventDeath = profile.preventDeath;

        isDead = false;
    }

    public DamageResult TryTakeDamage(float damage, string attackerName)
    {
        DamageResult result = new DamageResult();

        if (isDead)
        {
            result.blocked = true;
            result.reason = "NPC is already dead.";
            return result;
        }

        damage = Mathf.Max(0f, damage);

        if (damage <= 0f)
        {
            result.blocked = true;
            result.reason = "Damage is zero.";
            return result;
        }

        if (IsInvulnerable)
        {
            result.blocked = true;
            result.invulnerable = true;
            result.reason = "NPC is invulnerable.";
            return result;
        }

        float oldHP = currentHP;
        currentHP -= damage;

        if (currentHP <= 0f)
        {
            if (PreventDeath)
            {
                currentHP = 1f;

                result.damageApplied = oldHP - currentHP;
                result.currentHP = currentHP;
                result.wouldDie = false;
                result.preventedDeath = true;

                Damaged?.Invoke(this, attackerName, result.damageApplied);
                return result;
            }

            currentHP = 0f;

            result.damageApplied = oldHP;
            result.currentHP = currentHP;
            result.wouldDie = true;

            Damaged?.Invoke(this, attackerName, result.damageApplied);
            DeathRequested?.Invoke(this, attackerName);

            return result;
        }

        result.damageApplied = oldHP - currentHP;
        result.currentHP = currentHP;
        result.wouldDie = false;

        Damaged?.Invoke(this, attackerName, result.damageApplied);

        return result;
    }

    public void ConfirmDeath(string attackerName)
    {
        if (isDead) return;

        isDead = true;
        currentHP = 0f;

        Died?.Invoke(this, attackerName);
    }

    public void ForceKill(string attackerName)
    {
        if (isDead) return;

        currentHP = 0f;
        ConfirmDeath(attackerName);
    }

    public void HealFull()
    {
        if (isDead) return;
        currentHP = maxHP;
    }

    public void SetInvulnerable(bool value)
    {
        invulnerable = value;
    }

    public void SetPreventDeath(bool value)
    {
        preventDeath = value;
    }

    public void SetImportance(NPCImportance newImportance)
    {
        importance = newImportance;
    }

    [Serializable]
    public struct DamageResult
    {
        public bool blocked;
        public bool invulnerable;
        public bool preventedDeath;
        public bool wouldDie;

        public float damageApplied;
        public float currentHP;

        public string reason;
    }
}