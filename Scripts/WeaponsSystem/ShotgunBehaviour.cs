using UnityEngine;

[RequireComponent(typeof(Gun))]
public class ShotgunBehaviour : MonoBehaviour
{
    [Header("Pellets / Spread")]
    [Min(1)] public int pelletCount = 6;
    [Min(0f)] public float spreadAngle = 1.25f;
    public bool randomizeYawPitch = false;

    [Header("Zasiêg / Obra¿enia")]
    [Tooltip("Jeœli < 0, u¿yje z weaponData.shotgunRange")]
    public float rangeOverride = -1f;
    public AnimationCurve damageFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0.35f);

    [Header("Kolizje / Wallbang")]
    [Min(0)] public int maxWallbangs = 1;
    [Range(0f, 1f)] public float wallbangDamageFactor = 0.5f;

    [Header("Player aim cluster")]
    [Tooltip("Promieñ skupienia pelletów w metrach na dystansie 10m.")]
    public float pelletClusterRadiusAt10m = 0.18f;

    [Tooltip("Minimalny odstêp miêdzy pelletami w tym samym strzale (w metrach, na dystansie 10m).")]
    public float minPelletSeparationAt10m = 0.06f;

    private Gun gun;
    private Collider[] ownColliders;
    private readonly RaycastHit[] pelletHits = new RaycastHit[32];
    private Vector2[] randomPelletPatternCache;

    private static readonly Vector2[] PelletPattern6 =
    {
    new Vector2( 0.00f,  0.00f), // œrodek
    new Vector2(-0.90f,  0.25f),
    new Vector2( 0.90f,  0.25f),
    new Vector2(-0.55f, -0.70f),
    new Vector2( 0.55f, -0.70f),
    new Vector2( 0.00f,  0.95f),
};

    private void Awake()
    {
        gun = GetComponent<Gun>();
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    private Vector3 GetPlayerAimPoint(float range)
    {
        if (gun != null && !gun.isControlledByNPC && gun.playerCamera != null)
        {
            Ray ray = gun.playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, range, gun.hitMask, QueryTriggerInteraction.Ignore))
                return hit.point;

            return ray.origin + ray.direction * range;
        }

        if (gun != null && gun.firePoint != null)
            return gun.firePoint.position + gun.firePoint.forward * range;

        return transform.position + transform.forward * range;
    }

    private Vector3 GetPlayerPelletDirection(Vector3 aimPoint, Vector2 patternOffset)
    {
        if (gun == null || gun.firePoint == null)
            return Vector3.forward;

        Camera cam = gun.playerCamera;
        if (cam == null)
            return (aimPoint - gun.firePoint.position).normalized;

        float dist = Vector3.Distance(cam.transform.position, aimPoint);
        float scale = dist / 10f;

        Vector3 offset =
            cam.transform.right * (patternOffset.x * scale) +
            cam.transform.up * (patternOffset.y * scale);

        Vector3 pelletTarget = aimPoint + offset;
        return (pelletTarget - gun.firePoint.position).normalized;
    }

    private Vector2[] BuildRandomPelletPattern(int pellets)
    {
        if (pellets <= 0)
            return System.Array.Empty<Vector2>();

        if (randomPelletPatternCache == null || randomPelletPatternCache.Length != pellets)
            randomPelletPatternCache = new Vector2[pellets];

        Vector2[] result = randomPelletPatternCache;

        for (int i = 0; i < pellets; i++)
            result[i] = Vector2.zero;

        result[0] = Vector2.zero;

        float minSep = Mathf.Max(0.001f, minPelletSeparationAt10m);
        float maxR = Mathf.Max(minSep * 1.2f, pelletClusterRadiusAt10m);

        for (int i = 1; i < pellets; i++)
        {
            bool placed = false;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector2 candidate = Random.insideUnitCircle * maxR;

                bool ok = true;
                for (int j = 0; j < i; j++)
                {
                    if (Vector2.Distance(candidate, result[j]) < minSep)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    result[i] = candidate;
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                // fallback: roz³ó¿ po okrêgu
                float ang = (360f / Mathf.Max(1, pellets - 1)) * (i - 1) * Mathf.Deg2Rad;
                result[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * maxR;
            }
        }

        return result;
    }

    public void FirePlayerShot()
    {
        if (!gun || !gun.firePoint || gun.weaponData == null) return;

        int pellets = Mathf.Max(1, pelletCount);
        float range = rangeOverride > 0f ? rangeOverride
                                         : (gun.weaponData != null ? gun.weaponData.shotgunRange : 30f);

        float baseDmg = gun.weaponData != null ? gun.weaponData.damage : 25f;
        float minDmg = gun.weaponData != null ? gun.weaponData.minShotgunDamage : 5f;

        Vector3 aimPoint = GetPlayerAimPoint(range);
        Vector3 lastImpact = aimPoint;

        Vector2[] pattern = BuildRandomPelletPattern(pellets);
        for (int i = 0; i < pellets; i++)
        {
            Vector3 shootDir = GetPlayerPelletDirection(aimPoint, pattern[i]);

            Vector3 impact = DoPelletRaycastAndDamage(shootDir, range, baseDmg, minDmg, lastImpact);
            lastImpact = impact;

            Vector3 tracerDir = (impact - gun.firePoint.position).normalized;
            gun.SpawnBullet(tracerDir);
        }

        gun.RaiseOnPlayerShot(lastImpact);
    }

    public void TryNPCFire(GameObject shooter, Vector3 aimDirection)
    {
        if (gun == null || gun.weaponData == null || gun.firePoint == null) return;

        int pellets = Mathf.Max(1, pelletCount);
        float range = rangeOverride > 0f
            ? rangeOverride
            : Mathf.Max(1f, gun.weaponData.shotgunRange);

        float baseDmg = Mathf.Max(1f, gun.weaponData.damage);
        float minDmg = Mathf.Max(1f, gun.weaponData.minShotgunDamage);
        float spread = Mathf.Max(0f, spreadAngle);

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = GetPelletDirection(i, spread, aimDirection);

            Vector3 impact = DoPelletRaycastAndDamage(
                dir,
                range,
                baseDmg,
                minDmg,
                gun.firePoint.position + dir * 3f,
                isNpc: true,
                npcName: shooter ? shooter.name : "NPC"
            );

            Vector3 tracerDir = (impact - gun.firePoint.position).normalized;
            gun.SpawnBullet(tracerDir);
        }
    }

    private Vector3 GetPlayerBaseDirection()
    {
        if (gun == null) return Vector3.forward;

        if (!gun.isControlledByNPC && gun.playerCamera != null)
        {
            Ray ray = gun.playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return ray.direction.normalized;
        }

        if (gun.firePoint != null)
            return gun.firePoint.forward.normalized;

        return transform.forward;
    }

    private Vector3 GetPelletDirection(int pelletIndex, float spreadDeg, Vector3? centerDir = null)
    {
        Vector3 fwd;

        if (centerDir.HasValue)
        {
            fwd = centerDir.Value.normalized;
        }
        else if (!gun.isControlledByNPC && gun.playerCamera != null)
        {
            Ray ray = gun.playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            fwd = ray.direction.normalized;
        }
        else if (gun.firePoint != null)
        {
            fwd = gun.firePoint.forward.normalized;
        }
        else
        {
            fwd = transform.forward;
        }

        Quaternion look = Quaternion.LookRotation(fwd, Vector3.up);

        Vector2 p = PelletPattern6[Mathf.Clamp(pelletIndex, 0, PelletPattern6.Length - 1)];

        float yaw = p.x * spreadDeg;
        float pitch = randomizeYawPitch ? p.y * spreadDeg : 0f;

        Quaternion localSpread = Quaternion.Euler(pitch, yaw, 0f);
        return (look * localSpread * Vector3.forward).normalized;
    }

    private bool IsOwnCollider(Collider c)
    {
        if (c == null || ownColliders == null) return false;

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] == c)
                return true;
        }

        return false;
    }

    private Vector3 DoPelletRaycastAndDamage(
        Vector3 dir,
        float range,
        float baseDmg,
        float minDmg,
        Vector3 fallbackImpact,
        bool isNpc = false,
        string npcName = null
    )
    {
        Vector3 origin = gun.firePoint.position;
        Ray ray = new Ray(origin, dir);

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            pelletHits,
            range,
            gun.hitMask,
            QueryTriggerInteraction.Ignore
        );

        if (hitCount <= 0)
            return fallbackImpact;

        SortHitsByDistance(pelletHits, hitCount);

        int wallbangsLeft = Mathf.Max(0, maxWallbangs);
        float dmgMul = 1f;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = pelletHits[i];

            if (IsOwnCollider(hit.collider))
                continue;

            float t = Mathf.Clamp01(hit.distance / Mathf.Max(1f, range));
            float falloff = damageFalloff != null ? damageFalloff.Evaluate(t) : 1f;
            int dmg = Mathf.RoundToInt(Mathf.Lerp(baseDmg, minDmg, t) * falloff * dmgMul);
            dmg = Mathf.Max(1, dmg);

            gun.SpawnBulletHole(hit, ignoreDecalDedup: true);

            var dmgTarget = hit.collider.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                if (isNpc) dmgTarget.TakeDamage(dmg, npcName ?? "NPC");
                else dmgTarget.TakeDamage(dmg, "Player");

                return hit.point;
            }

            bool isObstacle = IsObstacle(hit.collider.gameObject.layer);
            if (isObstacle && wallbangsLeft > 0)
            {
                wallbangsLeft--;
                dmgMul *= wallbangDamageFactor;
                continue;
            }

            return hit.point;
        }

        return fallbackImpact;
    }

    private bool IsObstacle(int layer)
    {
        string layerName = LayerMask.LayerToName(layer);
        return layerName == "Obstacle" || layerName == "Default" || layerName == "Wall";
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