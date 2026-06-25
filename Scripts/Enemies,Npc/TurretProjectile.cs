using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class TurretProjectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float lifeTime = 4f;
    [SerializeField] private GameObject hitFx;

    private int damage;
    private GameObject owner;
    private Rigidbody rb;
    private bool hitDone;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Init(GameObject shooter, Vector3 direction, float speed, int damageAmount)
    {
        owner = shooter;
        damage = damageAmount;

        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;

        transform.rotation = Quaternion.LookRotation(direction);

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = direction * speed;
#else
        rb.velocity = direction * speed;
#endif

        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null)
            HandleHit(collision.collider);
    }

    private void HandleHit(Collider other)
    {
        if (hitDone)
            return;

        if (other == null)
            return;

        if (owner != null && other.transform.IsChildOf(owner.transform))
            return;

        hitDone = true;

        if (other.CompareTag("Player"))
        {
            PlayerStats stats = other.GetComponentInParent<PlayerStats>();

            if (stats != null)
                stats.TakeDamage(damage);
        }
        else
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();

            if (damageable != null)
                damageable.TakeDamage(damage, owner != null ? owner.name : "Turret");
        }

        if (hitFx != null)
            Instantiate(hitFx, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}