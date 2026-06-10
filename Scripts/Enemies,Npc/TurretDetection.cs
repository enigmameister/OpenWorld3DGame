using UnityEngine;

public class TurretDetection : MonoBehaviour
{
    public float detectionRadius = 15f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayers;
    public Transform rotatingPart;
    public float rotationSpeed = 3f;

    [Header("Laser Settings")]
    public Transform laserOrigin;
    public LineRenderer laserLine;
    public Color laserColor = Color.red;

    private Transform player;
    private Renderer turretRenderer;
    private Quaternion defaultRotation;
    private bool playerVisible = false;
    private bool laserLockedOnPlayer = false;

    [Header("Celowanie na wysokości Y")]
    public float verticalOffset = 1.6f;
    private float lastSeenTime = -999f;
    public float gracePeriod = 0.3f;

    [Header("Atakowanie")]
    public bool canAttack = false;
    public GameObject bulletPrefab;
    public Transform shotPoint;
    public ParticleSystem muzzleFlash;
    public float bulletSpeed = 40f;
    public float fireRate = 0.2f;
    public int burstCount = 4;
    public float burstCooldown = 2f;
    public float bulletDamage = 5f;

    private bool isFiring = false;
    private float lastFireTime = -999f;
    private int burstShotsRemaining;
    private CharacterController playerController;
    private WeaponManager playerWeaponManager;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        turretRenderer = GetComponentInChildren<Renderer>();
        defaultRotation = rotatingPart.rotation;
        playerWeaponManager = player.GetComponentInChildren<WeaponManager>();

        SetTurretColor(Color.green);

        if (player != null)
            playerController = player.GetComponent<CharacterController>();

        if (laserLine != null)
        {
            laserLine.enabled = false;
            laserLine.startColor = laserColor;
            laserLine.endColor = laserColor;
            laserLine.positionCount = 2;
        }
    }

    void Update()
    {
        if (player == null) return;

        Vector3 directionToPlayer = player.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;
        float offsetY = playerController != null ? playerController.height / 2f : verticalOffset;
        Vector3 targetPoint = player.position + Vector3.up * offsetY;

        if (distanceToPlayer <= detectionRadius)
        {
            Vector3 origin = transform.position;
            Vector3 dir = (targetPoint - origin).normalized;

            if (Physics.SphereCast(origin, 0.2f, dir, out RaycastHit hit, distanceToPlayer, obstacleLayers | playerLayer))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    lastSeenTime = Time.time;
                }
            }
        }

        playerVisible = (Time.time - lastSeenTime) <= gracePeriod;
        bool playerUnarmed = playerWeaponManager != null && playerWeaponManager.IsUsingHandsOnly();

        if (playerVisible)
        {
            RotateTowards(player.position); // śledzi zawsze
            if (!playerUnarmed)
            {
                SetTurretColor(Color.red);
                UpdateLaser(true);

                if (canAttack && laserLockedOnPlayer && !isFiring && Time.time >= lastFireTime + burstCooldown)
                {
                    StartCoroutine(FireBurst());
                }
            }
            else
            {
                SetTurretColor(Color.green);
                UpdateLaser(false);
            }
        }
    }

    void RotateTowards(Vector3 target)
    {
        Vector3 lookDir = target - rotatingPart.position;
        lookDir.y = 0f;
        Quaternion targetRotation = Quaternion.LookRotation(lookDir);
        rotatingPart.rotation = Quaternion.Slerp(rotatingPart.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    void RotateToDefault()
    {
        rotatingPart.rotation = Quaternion.Slerp(rotatingPart.rotation, defaultRotation, Time.deltaTime * rotationSpeed);
    }

    void SetTurretColor(Color color)
    {
        if (turretRenderer != null && turretRenderer.material.color != color)
        {
            turretRenderer.material.color = color;
        }
    }

    void UpdateLaser(bool active)
    {
        if (laserLine == null || laserOrigin == null) return;

        laserLine.enabled = active;
        if (!active)
        {
            laserLockedOnPlayer = false;
            return;
        }

        Vector3 origin = laserOrigin.position;
        float offsetY = playerController != null ? playerController.height / 2f : verticalOffset;
        Vector3 targetPos = player.position + Vector3.up * offsetY;
        Vector3 direction = targetPos - origin;

        if (Physics.SphereCast(origin, 0.2f, direction.normalized, out RaycastHit hit, 100f, obstacleLayers | playerLayer))
        {
            laserLine.SetPosition(0, origin);
            laserLine.SetPosition(1, hit.point);
            laserLockedOnPlayer = hit.collider.CompareTag("Player");
        }
        else
        {
            laserLine.SetPosition(0, origin);
            laserLine.SetPosition(1, origin + direction.normalized * 100f);
            laserLockedOnPlayer = false;
        }
    }

    private Vector3 GetLaserTargetPoint()
    {
        float offsetY = playerController != null ? playerController.height / 2f : verticalOffset;
        return laserLine.enabled ? laserLine.GetPosition(1) : (player.position + Vector3.up * offsetY);
    }

    private System.Collections.IEnumerator FireBurst()
    {
        isFiring = true;
        burstShotsRemaining = burstCount;

        while (burstShotsRemaining > 0)
        {
            FireBullet();
            burstShotsRemaining--;
            yield return new WaitForSeconds(fireRate);
        }

        lastFireTime = Time.time;
        isFiring = false;
    }

    void FireBullet()
    {
        if (bulletPrefab == null || shotPoint == null) return;

        GameObject bullet = Instantiate(bulletPrefab, shotPoint.position, Quaternion.identity);

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        Collider bulletCol = bullet.GetComponent<Collider>();

        Vector3 targetPos = GetLaserTargetPoint();
        Vector3 shootDir = (targetPos - shotPoint.position).normalized;

        if (rb != null)
        {
            bullet.transform.rotation = Quaternion.LookRotation(shootDir);
            bullet.transform.position += shootDir * 0.2f;
            rb.linearVelocity = shootDir * bulletSpeed;
        }

        Collider[] turretCols = shotPoint.root.GetComponentsInChildren<Collider>();
        foreach (var col in turretCols)
        {
            if (bulletCol != null && col != null)
                Physics.IgnoreCollision(bulletCol, col);
        }

        var bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetShooter(gameObject, bulletDamage);
        }

        if (muzzleFlash != null)
            muzzleFlash.Play();
    }
}
