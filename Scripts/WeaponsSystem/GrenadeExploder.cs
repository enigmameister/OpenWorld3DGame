using System.Collections.Generic;
using UnityEngine;

public class GrenadeExploder : MonoBehaviour
{
    private GameObject explosionEffect;
    private float delay;
    private float radius;
    private float damage;
    private bool hasExploded = false;

    [Header("Explosion Mask")]
    [SerializeField] private LayerMask explosionMask = ~0;

    [Header("NPC Reaction")]
    [SerializeField] private float reactionRadiusMultiplier = 1.5f;
    [SerializeField] private int noiseReactionDamageForMelee = 1;

    public void Init(
        GameObject effect,
        float delay,
        float radius,
        float damage,
        bool explodeImmediately = false,
        Vector3? position = null)
    {
        this.explosionEffect = effect;
        this.delay = delay;
        this.radius = radius;
        this.damage = damage;

        if (explodeImmediately)
        {
            transform.position = position ?? transform.position;
            Explode();
        }
        else
        {
            Invoke(nameof(Explode), delay);
        }
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Vector3 explosionPos = transform.position;

        if (explosionEffect != null)
        {
            GameObject effect = Instantiate(explosionEffect, explosionPos, Quaternion.identity);
            Destroy(effect, 2.5f);
        }

        DealDamage(explosionPos);
        ReactNearbyNPCs(explosionPos);

        Destroy(gameObject);
    }

    private void DealDamage(Vector3 explosionPos)
    {
        Collider[] hits = Physics.OverlapSphere(
            explosionPos,
            radius,
            explosionMask,
            QueryTriggerInteraction.Ignore
        );

        HashSet<NPCController> damagedControllers = new HashSet<NPCController>();
        HashSet<NPCMelee> damagedMelees = new HashSet<NPCMelee>();
        HashSet<PlayerStats> damagedPlayers = new HashSet<PlayerStats>();
        HashSet<Destructible> damagedDestructibles = new HashSet<Destructible>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            Vector3 hitPoint = hit.bounds.center;
            int finalDamage = CalculateFalloffDamage(explosionPos, hitPoint);

            if (finalDamage <= 0)
                continue;

            // =========================
            // 1. NPCController
            // =========================
            NPCController npc = hit.GetComponentInParent<NPCController>();

            if (npc != null)
            {
                if (!npc.IsDead && !damagedControllers.Contains(npc))
                {
                    damagedControllers.Add(npc);

                    Debug.Log($"[GRENADE] Damage NPCController: {npc.name}, dmg={finalDamage}");

                    npc.TakeDamage(finalDamage, "Grenade");
                }

                continue;
            }

            // =========================
            // 2. NPCMelee
            // =========================
            NPCMelee melee = hit.GetComponentInParent<NPCMelee>();

            if (melee != null)
            {
                if (!melee.IsDead && !damagedMelees.Contains(melee))
                {
                    damagedMelees.Add(melee);

                    Debug.Log($"[GRENADE] Damage NPCMelee: {melee.name}, dmg={finalDamage}");

                    melee.TakeDamage(finalDamage, "Grenade");
                }

                continue;
            }

            // =========================
            // 3. Player
            // =========================
            PlayerStats playerStats = hit.GetComponentInParent<PlayerStats>();

            if (playerStats != null)
            {
                if (!playerStats.IsDead && !damagedPlayers.Contains(playerStats))
                {
                    damagedPlayers.Add(playerStats);

                    Debug.Log($"[GRENADE] Damage Player: {playerStats.name}, dmg={finalDamage}");

                    playerStats.TakeDamage(finalDamage, "Grenade");
                }

                continue;
            }

            // =========================
            // 4. Destructible
            // =========================
            Destructible destructible = hit.GetComponentInParent<Destructible>();

            if (destructible != null)
            {
                if (!damagedDestructibles.Contains(destructible))
                {
                    damagedDestructibles.Add(destructible);

                    destructible.TakeDamage(finalDamage);
                }
            }
        }
    }

    private int CalculateFalloffDamage(Vector3 explosionPos, Vector3 targetPos)
    {
        float dist = Vector3.Distance(explosionPos, targetPos);
        float t = Mathf.Clamp01(dist / Mathf.Max(0.01f, radius));

        // Środek eksplozji = pełny damage.
        // Krawędź promienia = 35% damage.
        float multiplier = Mathf.Lerp(1f, 0.35f, t);

        return Mathf.RoundToInt(damage * multiplier);
    }

    private void ReactNearbyNPCs(Vector3 explosionPos)
    {
        float reactionRadius = radius * Mathf.Max(1f, reactionRadiusMultiplier);

        Collider[] hits = Physics.OverlapSphere(
            explosionPos,
            reactionRadius,
            explosionMask,
            QueryTriggerInteraction.Ignore
        );

        HashSet<NPCController> reactedControllers = new HashSet<NPCController>();
        HashSet<NPCMelee> reactedMelees = new HashSet<NPCMelee>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            NPCController npc = hit.GetComponentInParent<NPCController>();

            if (npc != null)
            {
                if (!npc.IsDead && !reactedControllers.Contains(npc))
                {
                    reactedControllers.Add(npc);

                    if (!npc.IsProvoked)
                    {
                        Debug.Log($"[GRENADE] React NPCController: {npc.name}");
                        npc.ForceReactToAggression();
                    }
                }

                continue;
            }

            NPCMelee melee = hit.GetComponentInParent<NPCMelee>();

            if (melee != null)
            {
                if (!melee.IsDead && !reactedMelees.Contains(melee))
                {
                    reactedMelees.Add(melee);

                    if (!melee.IsAggro)
                    {
                        Debug.Log($"[GRENADE] React NPCMelee: {melee.name}");

                        // Na razie wymuszamy reakcję lekkim damage.
                        // Później zrobimy osobną metodę ReactToExplosion().
                        melee.TakeDamage(noiseReactionDamageForMelee, "Explosion Noise");
                    }
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius * Mathf.Max(1f, reactionRadiusMultiplier));
    }
#endif
}