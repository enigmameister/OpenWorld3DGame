using System;
using System.Collections;
using UnityEngine;

public class NPCCore : MonoBehaviour
{
    public enum NPCImportance
    {
        Ambient,        // Standard NPC
        Mission,        // Mission NPC - wont die
        StoryCritical   // Critical NPC - wont die
    }

    [Header("World Coordinator")]
    [SerializeField] private bool autoRegisterInWorldCoordinator = true;
    [SerializeField] private bool autoUnregisterFromWorldCoordinator = true;

    [Header("Identity")]
    [SerializeField] private string npcId;
    [SerializeField] private string displayName = "NPC";
    [SerializeField] private NPCImportance importance = NPCImportance.Ambient;

    [Header("Health")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float currentHP = 100f;

    [Header("Damage Rules")]
    [SerializeField] private bool invulnerable = false;

    [Header("Hit Feedback")]
    [SerializeField] private bool useCoreHitColorFeedback = true;

    [Tooltip("If FALSE, this NPC will not flash red when damaged.")]
    [SerializeField] private bool allowHitColorFeedback = true;

    [SerializeField] private Renderer[] bodyRenderers;
    [SerializeField] private float hitFlashDuration = 0.25f;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color fallbackBaseColor = Color.white;

    [Tooltip("NPC can take damage but cannot die.")]
    [SerializeField] private bool preventDeath = false;

    [Header("Runtime")]
    [SerializeField] private bool isDead = false;

    private MaterialPropertyBlock mpb;
    private Coroutine hitFlashCoroutine;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    public string NpcId => npcId;
    public string DisplayName => displayName;
    public NPCImportance Importance => importance;

    public float MaxHP => maxHP;
    public float CurrentHP => currentHP;
    public bool IsDead => isDead;
    public bool AllowsCoreHitFeedback =>
        useCoreHitColorFeedback &&
        allowHitColorFeedback &&
        importance == NPCImportance.Ambient &&
        !IsInvulnerable;

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
        mpb = new MaterialPropertyBlock();
        RefreshBodyRenderersIfNeeded();
    }

    private void Start()
    {
        if (autoRegisterInWorldCoordinator && NPCWorldCoordinator.Instance != null)
            NPCWorldCoordinator.Instance.RegisterNPC(gameObject);
    }

    private void OnDestroy()
    {
        if (autoUnregisterFromWorldCoordinator && NPCWorldCoordinator.Instance != null)
            NPCWorldCoordinator.Instance.UnregisterNPC(gameObject);
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

                PlayCoreHitColorFeedback();
                Damaged?.Invoke(this, attackerName, result.damageApplied);
                return result;
            }

            currentHP = 0f;

            result.damageApplied = oldHP;
            result.currentHP = currentHP;
            result.wouldDie = true;

            PlayCoreHitColorFeedback();
            Damaged?.Invoke(this, attackerName, result.damageApplied);
            DeathRequested?.Invoke(this, attackerName);

            return result;
        }

        result.damageApplied = oldHP - currentHP;
        result.currentHP = currentHP;
        result.wouldDie = false;

        PlayCoreHitColorFeedback();
        Damaged?.Invoke(this, attackerName, result.damageApplied);

        return result;
    }

    public void PlayCoreHitColorFeedback()
    {
        if (!AllowsCoreHitFeedback)
            return;

        if (bodyRenderers == null || bodyRenderers.Length == 0)
            return;

        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);

        hitFlashCoroutine = StartCoroutine(CoreHitFlashRoutine());
    }

    public void ApplyCoreBodyColor(Color color)
    {
        if (bodyRenderers == null || bodyRenderers.Length == 0)
            return;

        if (mpb == null)
            mpb = new MaterialPropertyBlock();

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            Renderer renderer = bodyRenderers[i];

            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorID, color);
            mpb.SetColor(ColorID, color);
            renderer.SetPropertyBlock(mpb);
        }
    }

    private IEnumerator CoreHitFlashRoutine()
    {
        ApplyCoreBodyColor(hitColor);

        yield return new WaitForSeconds(hitFlashDuration);

        if (!isDead)
            ApplyCoreBodyColor(fallbackBaseColor);

        hitFlashCoroutine = null;
    }

    private void RefreshBodyRenderersIfNeeded()
    {
        if (bodyRenderers != null && bodyRenderers.Length > 0)
            return;

        bodyRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public void ConfirmDeath(string attackerName)
    {
        if (isDead)
            return;

        isDead = true;
        currentHP = 0f;

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

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