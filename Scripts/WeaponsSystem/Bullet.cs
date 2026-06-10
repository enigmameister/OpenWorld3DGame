using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Bullet : MonoBehaviour
{
    bool _stuck;    // GUARD
    public bool spawnImpactDecal = true;

    void OnTriggerEnter(Collider other) { TryStick(other, transform.position, -transform.forward); }

    void TryStick(Collider col, Vector3 point, Vector3 normal)
    {
        if (_stuck) return;              // <--- chroni przed duplikacją
        _stuck = true;

        // ...twój kod: spawn modelu pocisku, parent do trafionego, SFX/FX...
        // Na koniec koniecznie wyłącz dalszą fizykę:
        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;
        var myCol = GetComponent<Collider>();
        if (myCol) myCol.enabled = false;
    }

    private GameObject shooter;
    private Gun sourceGun;
    private float? manualDamageOverride = null;

    // Jeśli false → pocisk jest tylko wizualnym tracerem (brak dmg).
    public bool applyDamage = true;

    [Header("Efekty trafienia")]
    public GameObject bulletHolePrefab;
    public float bulletHoleLifetime = 10f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        Destroy(gameObject, 5f);
    }

    public void SetShooter(GameObject shooterObj, Gun gun)
    {
        shooter = shooterObj;
        sourceGun = gun;
        manualDamageOverride = null;
    }

    public void SetShooter(GameObject shooterObj, float damageAmount)
    {
        shooter = shooterObj;
        sourceGun = null;
        manualDamageOverride = damageAmount;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_stuck) return;

        // tracer wizualny też ma się zatrzymać po trafieniu
        if (!applyDamage)
        {
            _stuck = true;

            var rb = GetComponent<Rigidbody>();
            if (rb)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            var myCol = GetComponent<Collider>();
            if (myCol) myCol.enabled = false;

            if (spawnImpactDecal)
                SpawnDecal(collision);

            Destroy(gameObject, 0.02f);
            return;
        }

        _stuck = true;

        // "przyklejenie" / zatrzymanie
        TryStick(collision.collider, collision.contacts[0].point, collision.contacts[0].normal);

        GameObject hitObject = collision.gameObject;
        float damage = manualDamageOverride ?? sourceGun?.GetDamage() ?? 0f;

        if (spawnImpactDecal)
            SpawnDecal(collision);

        bool hitSomething = false;

        if (hitObject.GetComponentInParent<IDamageable>() is IDamageable damageable)
        {
            damageable.TakeDamage(Mathf.RoundToInt(damage), shooter?.name ?? "WeaponLogic");
            hitSomething = true;
        }
        else if (hitObject.TryGetComponent<Target>(out var targetObj))
        {
            targetObj.Hit();
            hitSomething = true;
        }
        else if (hitObject.TryGetComponent<Destructible>(out var destructible))
        {
            destructible.TakeDamage(damage);
            hitSomething = true;
        }
        else if (hitObject.TryGetComponent<TimedDamageTarget>(out var timed))
        {
            timed.TakeDamage(damage);
            hitSomething = true;
        }

        if (!hitSomething)
        {
            Debug.Log($"💥 Bullet trafił w: {hitObject.name}, ale nie znaleziono komponentu do zadania obrażeń.");
        }

        var rb2 = GetComponent<Rigidbody>();
        if (rb2)
        {
            rb2.linearVelocity = Vector3.zero;
            rb2.angularVelocity = Vector3.zero;
            rb2.isKinematic = true;
        }

        var col2 = GetComponent<Collider>();
        if (col2) col2.enabled = false;

        Destroy(gameObject);
    }

    private void SpawnDecal(Collision collision)
    {
        if (!bulletHolePrefab) return;
        ContactPoint contact = collision.GetContact(0);
        Quaternion rot = Quaternion.LookRotation(-contact.normal);
        GameObject hole = Instantiate(bulletHolePrefab, contact.point + contact.normal * 0.01f, rot);
        hole.transform.SetParent(collision.collider.transform);
        Destroy(hole, bulletHoleLifetime);
    }
}
