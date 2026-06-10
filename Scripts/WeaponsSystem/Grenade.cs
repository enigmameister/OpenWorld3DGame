using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class Grenade : MonoBehaviour, IInventoryItemInstanceProvider
{
    public InventoryItemInstance inventoryInstance;

    [Header("Prefabs & Settings")]
    public GameObject thrownPrefab;
    public GameObject explosionEffect;
    public Transform throwPoint;
    public Camera playerCamera;

    [Header("Throw Force")]
    public float throwForce = 25f;
    public float maxChargeTime = 1.2f; // krótki czas ładowania jak w CS
    public float explosionDelay = 3f;
    public float explosionRadius = 4f;
    public float explosionDamage = 100f;

    [Header("Target Preview")]
    public bool showLandingMarker = true;
    public GameObject landingMarkerPrefab;
    public int trajectorySteps = 80;
    public float simulationStep = 0.05f;

    private float chargeTimer = 0f;
    private bool isCharging = false;
    public bool hasThrown = false;
    private GameObject landingMarkerInstance;
    private Quaternion originalThrowPointRotation;
    private Camera cachedCamera;
    private Transform cachedCameraTransform;

    [Header("Dynamika eksplozji")]
    public float minExplosionDelay = 0.5f;

    [Header("Trajectory Optimization")]
    [SerializeField] private float trajectoryUpdateInterval = 0.05f;

    private float nextTrajectoryUpdateTime;

    public void SetInventoryInstance(InventoryItemInstance instance)
    {
        inventoryInstance = instance;
    }

    public InventoryItemInstance GetInstance()
    {
        return inventoryInstance;
    }
    void Update()
    {

        if (GetComponentInParent<PlayerStats>()?.IsDead ?? false)
            return;

        if (InventoryUI.IsInventoryOpen || EventSystem.current.IsPointerOverGameObject())
            return;

        if (hasThrown || inventoryInstance == null)
            return;

        if (PlayerInputHandler.Instance == null)
            return;

        // --- DRY-THROW (0 sztuk) ---
        if (inventoryInstance.count <= 0)
        {
            // pozwól kliknąć, żeby przełączyć (tak jak z bronią 0/0)
            if (PlayerInputHandler.Instance.FirePressedThisFrame)
            {
                var wm = GetComponentInParent<WeaponManager>();
                if (wm != null && !InventoryUI.IsDraggingInventoryItem)
                    wm.TrySwitchToAvailableWeapon(3); // pomiń slot granatu
            }
            return; // nic więcej nie robimy przy 0
        }

        // 🔘 Start ładowania (tylko jeśli mamy sztuki)
        if (PlayerInputHandler.Instance.FireHeld)
        {
            isCharging = true;
            chargeTimer += Time.deltaTime;
            chargeTimer = Mathf.Clamp(chargeTimer, 0f, maxChargeTime);

            if (showLandingMarker && Time.time >= nextTrajectoryUpdateTime)
            {
                nextTrajectoryUpdateTime = Time.time + trajectoryUpdateInterval;
                SimulateTrajectoryPreview();
            }
        }

        // 🔘 Rzut dopiero po puszczeniu
        if (isCharging && PlayerInputHandler.Instance.FireReleasedThisFrame)
        {
            ThrowGrenade();
        }

        // Dynamiczne położenie ThrowPoint (kąt rzutu zależny od patrzenia w górę/dół)
        if (throwPoint != null)
        {
            float t = Mathf.Clamp01(chargeTimer / maxChargeTime);
            float xRotation = Mathf.Lerp(45f, 0f, t); // od 45 do 0 stopni
            Vector3 euler = throwPoint.localEulerAngles;
            euler.x = xRotation;
            throwPoint.localEulerAngles = euler;
        }
            
    }
    // Nie próbuj odpalać particle w rękach!
    public void ApplyGrenadeData(GrenadeItemData data)
    {
        explosionRadius = data.explosionRadius;
        explosionDelay = data.explosionDelay;
        explosionDamage = data.explosionDamage;
        thrownPrefab = data.thrownPrefab;

        // 🔴 NIE uruchamiaj tu niczego na explosionEffect!
    }

    void ThrowGrenade()
    {
        if (hasThrown) return;
        hasThrown = true;

        WeaponManager wm = GetComponentInParent<WeaponManager>();
        if (wm != null)
        {
            wm.ConsumeGrenade(); // tylko raz!
        }

        // Spawn fizycznego granata
        Transform cam = GetCameraTransform();
        if (cam == null) return;

        Vector3 spawnPosition = cam.position + cam.forward * 0.6f;

        GameObject thrown = Instantiate(thrownPrefab, spawnPosition, Quaternion.identity);
        Rigidbody rb = thrown.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Destroy(thrown);
            return;
        }

        float t = Mathf.Clamp01(chargeTimer / maxChargeTime);
        float strength = Mathf.Lerp(0.6f, 1.4f, Mathf.Pow(t, 1.2f));

        Vector3 direction = cam.forward;
        float verticalFactor = Mathf.Clamp01(Vector3.Dot(cam.forward, Vector3.up) * -1f);
        direction += cam.up * 0.1f * verticalFactor;
        direction.Normalize();

        rb.AddForce(direction * throwForce * strength, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 12f, ForceMode.Impulse);

        rb.linearDamping = 0.4f;
        rb.angularDamping = 0.8f;

        float normalized = Mathf.Clamp01(chargeTimer / maxChargeTime);
        float dynamicDelay = Mathf.Lerp(minExplosionDelay, explosionDelay, normalized);

        GrenadeExploder exploder = thrown.GetComponent<GrenadeExploder>();
        if (exploder != null)
            exploder.Init(explosionEffect, dynamicDelay, explosionRadius, explosionDamage);

        if (landingMarkerInstance != null)
        {
            Destroy(landingMarkerInstance);
            landingMarkerInstance = null;
        }

        // 👇 Nie wyłączaj granatu – WeaponManager sam się tym zajmie
        isCharging = false;
        chargeTimer = 0f;

        Invoke(nameof(ResetThrowState), 0.3f);
    }

    void ResetThrowState()
    {
        hasThrown = false;
    }

    void SimulateTrajectoryPreview()
    {
        if (landingMarkerPrefab == null) return;
        if (landingMarkerInstance == null)
            landingMarkerInstance = Instantiate(landingMarkerPrefab);

        Transform cam = GetCameraTransform();
        if (cam == null) return;

        Vector3 startPos = cam.position + cam.forward * 0.6f;

        float strength = Mathf.Lerp(0.4f, 1.0f, chargeTimer / maxChargeTime);

        // Zaktualizowany kierunek zgodny z kamerą
        Vector3 direction = cam.forward;
        float verticalFactor = Mathf.Clamp01(Vector3.Dot(cam.forward, Vector3.up) * -1f);
        direction += cam.up * 0.1f * verticalFactor;
        direction.Normalize();

        Vector3 velocity = direction * throwForce * strength;

        Vector3 currentPosition = startPos;
        Vector3 currentVelocity = velocity;

        for (int i = 0; i < trajectorySteps; i++)
        {
            currentVelocity += Physics.gravity * simulationStep;
            Vector3 nextPosition = currentPosition + currentVelocity * simulationStep;

            if (Physics.Raycast(currentPosition, nextPosition - currentPosition, out RaycastHit hit, (nextPosition - currentPosition).magnitude))
            {
                landingMarkerInstance.transform.position = hit.point + Vector3.up * 0.05f;
                return;
            }

            currentPosition = nextPosition;
        }

        landingMarkerInstance.transform.position = currentPosition;
    }

    private Transform GetCameraTransform()
    {
        if (playerCamera != null)
        {
            cachedCamera = playerCamera;
            cachedCameraTransform = playerCamera.transform;
            return cachedCameraTransform;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;

            if (cachedCamera != null)
                cachedCameraTransform = cachedCamera.transform;
        }

        return cachedCameraTransform;
    }
    void OnEnable()
    {
        hasThrown = false;
        isCharging = false;
        chargeTimer = 0f;
        nextTrajectoryUpdateTime = 0f;

        if (landingMarkerInstance != null)
            landingMarkerInstance.SetActive(false);

        if (inventoryInstance == null)
        {
            Debug.Log("[Grenade] ⏳ inventoryInstance jeszcze nie przypisany – czekam");
            return;
        }
    }

    void OnDisable()
    {
        if (landingMarkerInstance != null)
        {
            Destroy(landingMarkerInstance);
            landingMarkerInstance = null;
        }
    }
}
    