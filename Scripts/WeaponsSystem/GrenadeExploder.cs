using UnityEngine;

public class GrenadeExploder : MonoBehaviour
{
    private GameObject explosionEffect;
    private float delay;
    private float radius;
    private float damage;
    private bool hasExploded = false;

    public void Init(GameObject effect, float delay, float radius, float damage, bool explodeImmediately = false, Vector3? position = null)
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

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        if (explosionEffect != null)
        {
            GameObject effect = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2.5f); // zniszcz efekt po 2.5 sekundach (dopasuj do długości efektu)
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                var stats = hit.GetComponent<PlayerStats>();
                if (stats != null && !stats.IsDead)
                    stats.TakeDamage((int)damage, "Grenade");
            }

            if (hit.CompareTag("Enemy") || hit.GetComponent<NPCController>() != null)
            {
                var npc = hit.GetComponent<IDamageable>();
                if (npc != null)
                    npc.TakeDamage((int)damage, "Grenade");
            }

            if (hit.TryGetComponent(out Destructible destructible))
            {
                destructible.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
    }

}
