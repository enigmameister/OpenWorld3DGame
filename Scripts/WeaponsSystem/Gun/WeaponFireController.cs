using System.Collections.Generic;
using UnityEngine;

public class WeaponFireController : MonoBehaviour
{
    [SerializeField] private Gun gun;
    [SerializeField] private SniperRifleBehaviour sniperCfg;
    [SerializeField] private ShotgunBehaviour shotgunCfg;
    [SerializeField] private RecoilController recoilController;
    [SerializeField] private WeaponRecoilController weaponRecoil;

    [Header("Runtime refs - assigned by Gun.cs")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Animator animator;

    [Header("Runtime weapon config - assigned by Gun.cs")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private GameObject hitEffectPrefab;

    [Header("FX")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private ParticleSystem shellEjectParticles;
    [SerializeField] private string fireTriggerName = "FireTrigger";

    private Collider[] ownColliders;
    private readonly RaycastHit[] raycastHits = new RaycastHit[32];

    private static readonly Dictionary<Collider, (Vector3 pos, float time)> lastDecal = new();
    private const float DecalMinTimeGap = 0.03f;
    private const float DecalMinPosGapSqr = 0.01f;



    void Awake()
    {
        if (!gun) gun = GetComponent<Gun>();
        if (!sniperCfg) sniperCfg = GetComponent<SniperRifleBehaviour>();
        if (!shotgunCfg) shotgunCfg = GetComponent<ShotgunBehaviour>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);

        weaponRecoil ??= GetComponent<WeaponRecoilController>();
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    public void BindCamera(Camera cam)
    {
        playerCamera = cam;
    }

    public void ApplyWeaponRefs(
        GameObject bullet,
        Transform firePointRef,
        LayerMask mask,
        GameObject hitFx,
        ParticleSystem muzzle,
        ParticleSystem shell,
        RecoilController recoil,
        string triggerName)
    {
        bulletPrefab = bullet;
        firePoint = firePointRef;
        hitMask = mask;
        hitEffectPrefab = hitFx;
        muzzleFlash = muzzle;
        shellEjectParticles = shell;
        recoilController = recoil;
        fireTriggerName = triggerName;

        if (!weaponRecoil)
            weaponRecoil = GetComponent<WeaponRecoilController>();

        weaponRecoil?.SetCameraRecoilController(recoil);
    }

    public void TryFire()
    {
        if (!gun) return;
        if (InventoryUI.IsInventoryOpen) return;
        if (gun.IsPlayerDead()) return;

        if (gun.IsShotgunWeapon())
            FireShotgun();
        else
            FireStandard();
    }

    private void FireStandard()
    { 

        if (!playerCamera || !firePoint) return;
        if (!gun.HasInfiniteAmmo() && gun.GetCurrentAmmo() <= 0) return;

        if (gun.HasInfiniteAmmo() && gun.GetCurrentAmmo() <= 0)
        {
            gun.SetAmmo(gun.GetMagazineSize(), gun.GetTotalAmmo());
            gun.RefreshBulletUI();
        }

        if (!gun.HasInfiniteAmmo())
        {
            gun.SetAmmo(gun.GetCurrentAmmo() - 1, gun.GetTotalAmmo());
            gun.RefreshBulletUI();
        }

        PlayFireFX();

        if (!MountedSniperState.IsActive)
            CameraSwitcher.Instance?.ForceUpdateCameraNow();

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float maxDistance = 2000f;


        int hitCount = Physics.RaycastNonAlloc(
            ray,
            raycastHits,
            maxDistance,
            hitMask,
            QueryTriggerInteraction.Ignore
        );

        SortHitsByDistance(raycastHits, hitCount);

        bool hasFirstValidHit = false;
        RaycastHit firstValidHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit h = raycastHits[i];

            if (IsOwnCollider(h.collider))
                continue;

            firstValidHit = h;
            hasFirstValidHit = true;
            break;
        }

        Vector3 impactPoint = hasFirstValidHit
            ? firstValidHit.point
            : ray.origin + ray.direction * 100f;

        if (!gun.isControlledByNPC)
            Gun.OnPlayerShot?.Invoke(
                firePoint ? firePoint.position : ray.origin,
                ray.direction.normalized,
                impactPoint
            );

        float currentDamage = gun.GetDamage();
        string shooterName = gun.isControlledByNPC ? gameObject.name : "Player";

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit h = raycastHits[i];

            if (IsOwnCollider(h.collider))
                continue;

            SpawnBulletHole(h);
            SpawnHitEffect(h);

            if (h.collider.CompareTag("Wallbang"))
            {
                currentDamage *= 0.5f;
                continue;
            }

            IDamageable damageable = h.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(Mathf.RoundToInt(currentDamage), shooterName);
                break;
            }

            break;
        }


        bool wasScopedBeforeShot = sniperCfg && sniperCfg.IsScoped();
        int prevZoomLevel = sniperCfg ? sniperCfg.GetZoomLevel() : 1;

        Vector3 visualTarget = hasFirstValidHit
            ? firstValidHit.point
            : ray.origin + ray.direction * maxDistance;

        float minTracerDistance = 8f;

        if ((visualTarget - firePoint.position).sqrMagnitude < minTracerDistance * minTracerDistance)
        {
            visualTarget = ray.origin + ray.direction * minTracerDistance;
        }

        Vector3 shootDir = (visualTarget - firePoint.position).normalized;

        SpawnBullet(shootDir);

        ApplyRecoilAndScopeReset();

        bool lastBullet = gun.GetCurrentAmmo() == 0;
        bool haveReserve = gun.GetTotalAmmo() > 0;


        if (sniperCfg)
        {
            if (sniperCfg.autoReturnAfterShot && !lastBullet)
            {
                gun.StartSniperFireCooldown(gun.GetFireRate());
                sniperCfg.OnFireShot(wasScopedBeforeShot, prevZoomLevel);
            }

            if (sniperCfg.autoReturnAfterShot && lastBullet && haveReserve && !gun.IsReloading)
            {
                gun.StopSniperFireCooldown();
                gun.StartAutoReloadAfterDelay(gun.GetReloadTime() * 0.05f);
            }
        }
    }

    private void FireShotgun()
    {
        if (gun.GetCurrentAmmo() <= 0) return;

        if (!gun.HasInfiniteAmmo())
        {
            gun.SetAmmo(gun.GetCurrentAmmo() - 1, gun.GetTotalAmmo());
            gun.RefreshBulletUI();
        }

        PlayFireFX();

        if (shotgunCfg)
        {
            shotgunCfg.FirePlayerShot();
            ApplyRecoilAndScopeReset();
            return;
        }

        Vector3 lastImpact = firePoint
            ? firePoint.position + firePoint.forward * 3f
            : transform.position + transform.forward * 3f;

        float range = gun.GetWeaponData() ? gun.GetWeaponData().shotgunRange : 30f;
        float baseDmg = gun.GetWeaponData() ? gun.GetWeaponData().damage : 25f;

        Vector3 dir = firePoint ? firePoint.forward : transform.forward;
        Vector3 origin = firePoint ? firePoint.position : transform.position;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            dir,
            raycastHits,
            range,
            hitMask,
            QueryTriggerInteraction.Ignore
        );

        SortHitsByDistance(raycastHits, hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];

            if (IsOwnCollider(hit.collider))
                continue;

            SpawnBulletHole(hit);
            lastImpact = hit.point;

            IDamageable dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(
                    Mathf.RoundToInt(baseDmg),
                    gun.isControlledByNPC ? gameObject.name : "Player"
                );
                break;
            }

            break;
        }

        SpawnBullet(dir);
        RaiseOnPlayerShot(lastImpact);

        ApplyRecoilAndScopeReset();
    }

    private void PlayFireFX()
    {
        muzzleFlash?.Play();
        shellEjectParticles?.Emit(1);

        if (animator)
        {
            animator.ResetTrigger(fireTriggerName);
            animator.SetTrigger(fireTriggerName);
        }
    }

    public bool TryNPCFire(GameObject shooter, Vector3 direction)
    {
        if (!gun) return false;
        if (gun.IsReloading || gun.GetCurrentAmmo() <= 0 || firePoint == null)
            return false;

        if (gun.GetWeaponData() != null && gun.GetWeaponData().isShotgun && shotgunCfg)
        {
            if (!gun.HasInfiniteAmmo())
            {
                gun.SetAmmo(gun.GetCurrentAmmo() - 1, gun.GetTotalAmmo());
                gun.RefreshBulletUI();
            }

            PlayFireFX();
            shotgunCfg.TryNPCFire(shooter, direction);
            return true;
        }

        gun.SetAmmo(gun.GetCurrentAmmo() - 1, gun.GetTotalAmmo());
        PlayFireFX();

        float maxDistance = 1000f;
        float currentDamage = gun.GetWeaponData() != null
    ? gun.GetWeaponData().damage
    : 25f;

        string attackerName = shooter != null ? shooter.name : "NPC";

        int hitCount = Physics.RaycastNonAlloc(
            firePoint.position,
            direction,
            raycastHits,
            maxDistance
        );

        SortHitsByDistance(raycastHits, hitCount);

        bool hasFirstValidHit = false;
        RaycastHit firstValidHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];

            SpawnBulletHole(hit);

            if (!hasFirstValidHit)
            {
                firstValidHit = hit;
                hasFirstValidHit = true;
            }

            if (hit.collider.CompareTag("Wallbang"))
            {
                currentDamage *= 0.5f;
                continue;
            }

            var playerVictim = hit.collider.GetComponentInParent<PlayerStats>();
            if (playerVictim != null)
            {
                int finalDmg = Mathf.RoundToInt(
                    currentDamage * GameDifficulty.NpcDamageMultiplier
                );

                playerVictim.TakeDamage(finalDmg, attackerName);
                break;
            }

            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                int finalDmg = Mathf.RoundToInt(
                    currentDamage * GameDifficulty.NpcDamageMultiplier
                );

                damageable.TakeDamage(finalDmg, attackerName);
                break;
            }

            break;
        }

        Vector3 shootDir = hasFirstValidHit
            ? (firstValidHit.point - firePoint.position).normalized
            : direction;

        SpawnBullet(shootDir);
        return true;
    }

    public void SpawnBullet(Vector3 direction)
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject bullet = Instantiate(
            bulletPrefab,
            firePoint.position,
            Quaternion.LookRotation(direction)
        );

        if (bullet.TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = direction * gun.GetBulletSpeed();

        if (bullet.TryGetComponent<Bullet>(out var bulletScript))
        {
            bulletScript.SetShooter(gameObject, gun);
            bulletScript.applyDamage = false;
            bulletScript.spawnImpactDecal = false;
        }

    }

    public void SpawnBulletHole(RaycastHit hit, bool ignoreDecalDedup = false)
    {
        if (bulletPrefab == null) return;

        var bulletScript = bulletPrefab.GetComponent<Bullet>();
        if (bulletScript == null || bulletScript.bulletHolePrefab == null) return;

        var col = hit.collider;
        if (col == null) return;

        if (!ignoreDecalDedup)
        {
            if (lastDecal.TryGetValue(col, out var last))
            {
                if ((Time.time - last.time) < DecalMinTimeGap &&
                    (hit.point - last.pos).sqrMagnitude < DecalMinPosGapSqr)
                    return;
            }

            lastDecal[col] = (hit.point, Time.time);
        }

        Quaternion rot = Quaternion.LookRotation(-hit.normal);

        GameObject hole = Instantiate(
            bulletScript.bulletHolePrefab,
            hit.point + hit.normal * 0.01f,
            rot
        );

        hole.transform.SetParent(col.transform);
        Destroy(hole, bulletScript.bulletHoleLifetime);
    }

    private void SpawnHitEffect(RaycastHit hit)
    {
        if (!hitEffectPrefab) return;

        Quaternion rot = Quaternion.LookRotation(hit.normal);
        GameObject fx = Instantiate(hitEffectPrefab, hit.point, rot);

        Destroy(fx, 2f);
    }

    private void ApplyRecoilAndScopeReset()
    {
        weaponRecoil?.ApplyShotRecoil();

        if (sniperCfg != null && sniperCfg.IsScoped())
        {
            sniperCfg.SetZoomBlocked(true);
            sniperCfg.ResetZoom();

            gun.SetADS(false);
            gun.ApplyScopedSensitivityPublic(false, false);
        }
    }

    public void RaiseOnPlayerShot(Vector3 impactPoint)
    {
        if (gun.isControlledByNPC) return;

        Vector3 origin = firePoint
            ? firePoint.position
            : playerCamera
                ? playerCamera.transform.position
                : transform.position;

        Vector3 forward = playerCamera
            ? playerCamera.transform.forward
            : transform.forward;

        Gun.OnPlayerShot?.Invoke(origin, forward, impactPoint);
    }

    private bool IsOwnCollider(Collider c)
    {
        if (c == null || ownColliders == null) return false;

        for (int i = 0; i < ownColliders.Length; i++)
            if (ownColliders[i] == c)
                return true;

        return false;
    }

    private void SortHitsByDistance(RaycastHit[] hits, int count)
    {
        for (int i = 1; i < count; i++)
        {
            RaycastHit key = hits[i];
            float keyDistance = key.distance;
            int j = i - 1;

            while (j >= 0 && hits[j].distance > keyDistance)
            {
                hits[j + 1] = hits[j];
                j--;
            }

            hits[j + 1] = key;
        }
    }
}