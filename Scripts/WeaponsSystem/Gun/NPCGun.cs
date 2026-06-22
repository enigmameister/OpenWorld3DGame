using UnityEngine;

public class NPCGun : MonoBehaviour
{
    [Header("Weapon Data")]
    [SerializeField] private WeaponItemData weaponData;

    [SerializeField] private InventoryItemInstance inventoryInstance;

    [Header("Refs")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Raycast")]
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float maxDistance = 1000f;

    [Header("FX")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private ParticleSystem shellEjectParticles;
    [SerializeField] private GameObject hitEffectPrefab;

    [Header("Ammo")]
    [SerializeField] private bool useAmmo = true;
    [SerializeField] private int currentAmmo = 30;
    [SerializeField] private int totalAmmo = 90;
    [SerializeField] private bool infiniteAmmo = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly RaycastHit[] raycastHits = new RaycastHit[32];

    public Transform FirePoint => firePoint;
    public WeaponItemData WeaponData => weaponData;

    public float Damage => weaponData != null ? weaponData.damage : 25f;
    public float FireRate => weaponData != null ? weaponData.fireRate : 0.25f;
    public float BulletSpeed => weaponData != null ? weaponData.bulletSpeed : 50f;
    public int MagazineSize => weaponData != null ? weaponData.magazineSize : 30;
    public bool HasAmmo => infiniteAmmo || !useAmmo || currentAmmo > 0;

    public InventoryItemInstance GetInstance()
    {
        return inventoryInstance;
    }

    private void Awake()
    {
        if (firePoint == null)
        {
            Transform found = transform.Find("FirePoint");
            if (found != null)
                firePoint = found;
            else
                firePoint = GetComponentInChildren<Transform>(true);
        }

        if (weaponData != null && currentAmmo <= 0)
            currentAmmo = weaponData.magazineSize;
    }

    public void SetWeaponData(WeaponItemData data)
    {
        weaponData = data;

        if (weaponData != null)
        {
            currentAmmo = Mathf.Clamp(currentAmmo <= 0 ? weaponData.magazineSize : currentAmmo, 0, weaponData.magazineSize);
            totalAmmo = Mathf.Max(totalAmmo, weaponData.magazineSize * 3);
        }
    }

    public void SetAmmo(int current, int total)
    {
        currentAmmo = Mathf.Max(0, current);
        totalAmmo = Mathf.Max(0, total);
    }

    public bool TryFire(GameObject shooter, Vector3 direction)
    {
        if (firePoint == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[NPCGun] {name}: firePoint is null.");

            return false;
        }

        if (!HasAmmo)
        {
            if (debugLogs)
                Debug.Log($"[NPCGun] {name}: no ammo.");

            return false;
        }

        direction = direction.normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = firePoint.forward;

        if (useAmmo && !infiniteAmmo)
            currentAmmo = Mathf.Max(0, currentAmmo - 1);

        PlayFx();

        Ray ray = new Ray(firePoint.position, direction);

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            raycastHits,
            maxDistance,
            hitMask,
            QueryTriggerInteraction.Ignore
        );

        SortHitsByDistance(raycastHits, hitCount);

        Vector3 visualTarget = firePoint.position + direction * maxDistance;
        float damage = Damage;

        string attackerName = shooter != null ? shooter.name : "NPC";

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];

            if (hit.collider == null)
                continue;

            if (shooter != null && hit.collider.transform.IsChildOf(shooter.transform))
                continue;

            visualTarget = hit.point;

            SpawnHitEffect(hit);

            if (hit.collider.CompareTag("Wallbang"))
            {
                damage *= 0.5f;
                continue;
            }

            PlayerStats player = hit.collider.GetComponentInParent<PlayerStats>();
            if (player != null)
            {
                int finalDamage = Mathf.RoundToInt(damage * GameDifficulty.NpcDamageMultiplier);
                player.TakeDamage(finalDamage, attackerName);
                break;
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                int finalDamage = Mathf.RoundToInt(damage * GameDifficulty.NpcDamageMultiplier);
                damageable.TakeDamage(finalDamage, attackerName);
                break;
            }

            break;
        }

        SpawnVisualBulletOrTracer(visualTarget, direction);

        if (debugLogs)
            Debug.Log($"[NPCGun] {name}: fired. Ammo={currentAmmo}/{totalAmmo}");

        return true;
    }

    private void PlayFx()
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();

        if (shellEjectParticles != null)
            shellEjectParticles.Emit(1);
    }

    private void SpawnHitEffect(RaycastHit hit)
    {
        if (hitEffectPrefab == null)
            return;

        Quaternion rot = Quaternion.LookRotation(hit.normal);
        GameObject fx = Instantiate(hitEffectPrefab, hit.point + hit.normal * 0.01f, rot);
        Destroy(fx, 2f);
    }

    private void SpawnVisualBulletOrTracer(Vector3 targetPoint, Vector3 fallbackDirection)
    {
        if (bulletPrefab == null || firePoint == null)
            return;

        Vector3 dir = targetPoint - firePoint.position;

        if (dir.sqrMagnitude < 0.01f)
            dir = fallbackDirection;

        dir.Normalize();

        GameObject bullet = Instantiate(
            bulletPrefab,
            firePoint.position,
            Quaternion.LookRotation(dir)
        );

        if (bullet.TryGetComponent<Rigidbody>(out Rigidbody rb))
            rb.linearVelocity = dir * BulletSpeed;

        if (bullet.TryGetComponent<Bullet>(out Bullet bulletScript))
        {
            bulletScript.SetShooter(gameObject, Damage);
            bulletScript.applyDamage = false;
            bulletScript.spawnImpactDecal = false;
        }
    }

    private static void SortHitsByDistance(RaycastHit[] hits, int count)
    {
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (hits[j].distance < hits[i].distance)
                {
                    RaycastHit temp = hits[i];
                    hits[i] = hits[j];
                    hits[j] = temp;
                }
            }
        }
    }
}