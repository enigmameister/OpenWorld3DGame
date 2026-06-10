using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Destructible : MonoBehaviour, IDamageable
{
    [Header("Ustawienia zdrowia i eksplozji")]
    public float health = 50f;
    public bool isExplosive = false;
    public float explosionRadius = 5f;
    public float explosionDamage = 40f;
    public GameObject explosionEffect;

    [Header("Wizualne")]
    public Light fallbackLight;
    public Color destroyedLightColor = Color.green;

    [Header("Zachowanie po zniszczeniu")]
    public bool canFall = false;
    public float fallDelay = 2f;
    public float fallForce = 5f;
    public DestructionType destructionType = DestructionType.KnockBack;

    private bool isDestroyed = false;
    private Rigidbody rb;

    public enum DestructionType
    {
        FallDown,
        KnockBack
    }

    void Start()
    {
        rb = GetComponentInChildren<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"❌ Brak Rigidbody w '{name}' – upadek nie zadziała!");
        }
        else
        {
            rb.isKinematic = true;
        }
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f)
        {
            Debug.LogWarning($"❗ {name} otrzymał 0 obrażeń — ignoruję.");
            return;
        }

        health -= damage;
        Debug.Log($"🔻 {name} HP: {health}");

        if (health <= 0f)
        {
            DestroySelf();
        }
    }

    // Implementacja interfejsu IDamageable
    public void TakeDamage(int damage, string attackerName)
    {
        Debug.Log($"🎯 {name} trafiony przez {attackerName} za {damage} dmg");
        TakeDamage((float)damage);
    }

    void DestroySelf()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (fallbackLight != null)
        {
            fallbackLight.color = destroyedLightColor;
            Debug.Log("💡 Zmieniono kolor światła po zniszczeniu");
        }

        if (isExplosive && explosionEffect != null)
        {
            HandleExplosion();
        }

        if (canFall && rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            Vector3 forceDir = destructionType switch
            {
                DestructionType.FallDown => Vector3.down + Vector3.forward * 0.1f,
                DestructionType.KnockBack => -transform.forward,
                _ => Vector3.zero
            };

            ForceMode mode = destructionType == DestructionType.KnockBack
                ? ForceMode.VelocityChange
                : ForceMode.Impulse;

            rb.AddForceAtPosition(forceDir * fallForce, transform.position + Vector3.up * 0.5f, mode);

            Destroy(gameObject, fallDelay);
        }
        else
        {
            Destroy(gameObject, 0.1f);
        }
    }

    private void HandleExplosion()
    {
        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider col in hitColliders)
        {
            float distance = Vector3.Distance(transform.position, col.transform.position);
            float distanceFactor = Mathf.Clamp01(1f - (distance / explosionRadius));
            float scaledDamage = explosionDamage * distanceFactor;

            if (col.TryGetComponent(out Destructible other))
            {
                other.TakeDamage(scaledDamage);
            }

            if (col.CompareTag("Player") && col.TryGetComponent(out PlayerStats stats))
            {
                stats.TakeDamage((int)scaledDamage);
            }

            if (col.attachedRigidbody != null)
            {
                col.attachedRigidbody.AddExplosionForce(
                    explosionDamage * 10f,
                    transform.position,
                    explosionRadius,
                    1f,
                    ForceMode.Impulse
                );
            }
        }
    }
}
