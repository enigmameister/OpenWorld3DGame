using UnityEngine;

[DisallowMultipleComponent]
public class VehicleHudController : MonoBehaviour
{
    [Header("Vehicle")]
    [SerializeField] private GameObject carObject;

    [Header("Player / Weapon UI")]
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private GunUI gunUI;
    [SerializeField] private GameObject gunUiRoot;

    [Header("Speedometer")]
    [SerializeField] private SpeedometerUI speedometerPrefab;
    [SerializeField] private Transform hudParent;
    [SerializeField] private GameObject legacySpeedometerInScene;

    private SpeedometerUI activeHud;
    private bool hudWasInstantiated;

    private bool gunUiRootWasActive = true;
    private bool gunUiStateCached;

    private void Awake()
    {
        ResolveRefs();
    }

    private void ResolveRefs()
    {
        if (carObject == null)
            carObject = gameObject;

        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);

        if (gunUI == null)
            gunUI = FindFirstObjectByType<GunUI>(FindObjectsInactive.Include);

        if (gunUiRoot == null && gunUI != null)
            gunUiRoot = gunUI.gameObject;
    }

    public void SetContext(GameObject vehicleObject, Transform defaultHudParent = null)
    {
        if (vehicleObject != null)
            carObject = vehicleObject;

        if (hudParent == null && defaultHudParent != null)
            hudParent = defaultHudParent;

        ResolveRefs();
    }

    public void OnPlayerEnteredVehicle(CarControll controller)
    {
        ResolveRefs();

        HideGunUIForVehicle();

        if (weaponManager != null)
            weaponManager.enabled = false;

        SetupCarHud(controller);
    }

    public void OnPlayerRestoredInsideVehicle(CarControll controller)
    {
        ResolveRefs();

        HideGunUIForVehicle();

        if (weaponManager != null)
            weaponManager.enabled = false;

        ForceHideCarHud();
        SetupCarHud(controller);
    }

    public void OnPlayerExitedVehicle()
    {
        ResolveRefs();

        if (weaponManager != null)
        {
            weaponManager.enabled = true;
            weaponManager.RefreshWeaponHUD();
        }

        ForceRestoreGunUIAfterVehicle();

        TearDownCarHud();
        ForceHideCarHud();
    }

    public void HideGunUIForVehicle()
    {
        ResolveRefs();

        if (gunUiRoot == null)
            return;

        if (!gunUiStateCached)
        {
            gunUiRootWasActive = gunUiRoot.activeSelf;
            gunUiStateCached = true;
        }

        gunUiRoot.SetActive(false);
    }

    public void ForceRestoreGunUIAfterVehicle()
    {
        ResolveRefs();

        if (gunUiRoot != null)
            gunUiRoot.SetActive(true);

        if (gunUI != null)
            gunUI.enabled = true;

        gunUiRootWasActive = true;
        gunUiStateCached = false;

        if (weaponManager != null)
        {
            weaponManager.enabled = true;
            weaponManager.RefreshWeaponHUD();
        }

        InventoryUI.Instance?.RefreshGunUIFromWeaponManager();
    }

    public void SetupCarHud(CarControll controller)
    {
        if (controller == null)
            return;

        NitroSystem nitro = carObject != null
            ? carObject.GetComponent<NitroSystem>()
            : null;

        if (speedometerPrefab == null && legacySpeedometerInScene != null)
        {
            activeHud = legacySpeedometerInScene.GetComponent<SpeedometerUI>();
            hudWasInstantiated = false;
        }
        else if (speedometerPrefab != null)
        {
            Transform parent = ResolveHudParent();

            activeHud = Instantiate(speedometerPrefab, parent);
            hudWasInstantiated = true;
        }

        if (activeHud != null)
        {
            activeHud.carController = controller;
            activeHud.nitroSystem = nitro;

            if (activeHud.speedometerRoot != null)
                activeHud.speedometerRoot.SetActive(true);
            else
                activeHud.gameObject.SetActive(true);
        }
    }

    public void TearDownCarHud()
    {
        if (activeHud == null)
            return;

        if (hudWasInstantiated)
        {
            Destroy(activeHud.gameObject);
        }
        else if (activeHud.speedometerRoot != null)
        {
            activeHud.speedometerRoot.SetActive(false);
        }
        else
        {
            activeHud.gameObject.SetActive(false);
        }

        activeHud = null;
        hudWasInstantiated = false;
    }

    public void ForceHideCarHud()
    {
        if (activeHud != null)
        {
            if (activeHud.speedometerRoot != null)
                activeHud.speedometerRoot.SetActive(false);
            else
                activeHud.gameObject.SetActive(false);

            if (hudWasInstantiated)
                Destroy(activeHud.gameObject);

            activeHud = null;
            hudWasInstantiated = false;
        }

        if (legacySpeedometerInScene != null)
            legacySpeedometerInScene.SetActive(false);

        SpeedometerUI[] speedometers = FindObjectsByType<SpeedometerUI>(FindObjectsSortMode.None);

        for (int i = 0; i < speedometers.Length; i++)
        {
            SpeedometerUI speedometer = speedometers[i];

            if (speedometer == null)
                continue;

            speedometer.carController = null;
            speedometer.nitroSystem = null;

            if (speedometer.speedometerRoot != null)
                speedometer.speedometerRoot.SetActive(false);
            else
                speedometer.gameObject.SetActive(false);
        }
    }

    private Transform ResolveHudParent()
    {
        if (hudParent != null)
            return hudParent;

        if (VehicleHudRootProvider.Instance != null)
        {
            hudParent = VehicleHudRootProvider.Instance.SpeedometerHudParent;
            return hudParent;
        }

        VehicleHudRootProvider provider = FindFirstObjectByType<VehicleHudRootProvider>(FindObjectsInactive.Include);

        if (provider != null)
        {
            hudParent = provider.SpeedometerHudParent;
            return hudParent;
        }

        Debug.LogWarning($"[VehicleHudController] No VehicleHudRootProvider found. Speedometer will be spawned under no parent on {name}.");
        return null;
    }
}