using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CarInteraction : MonoBehaviour
{
    [Header("Vehicle Modules")]
    [SerializeField] private VehicleHudController hudController;

    [Header("References")]
    public Transform seatPosition;
    public GameObject carCamera;
    public GameObject playerCamera;
    public GameObject playerObject;
    public GameObject carObject;
    public GameObject PlayerGUI;
    public GameObject weaponInventoryObject;
    public Transform exitPoint;

    [Header("Weapon UI")]
    [SerializeField] private GunUI gunUI;
    [SerializeField] private GameObject gunUiRoot;

    private bool gunUiRootWasActive = true;
    private bool gunUiStateCached = false;

    [Header("UI (interaction)")]
    public GameObject loadingBarRoot;
    public Image loadingBarFill;

    [Header("Car Cams")]
    public GameObject[] carCameras;
    public Camera activeCarCamera;
    public float cameraLerpSpeed = 5f;

    [Header("HUD Speedometer")]
    [SerializeField] private SpeedometerUI speedometerPrefab;
    [SerializeField] private Transform hudParent;
    [SerializeField] private GameObject legacySpeedometerInScene;

    [Header("Park brake (mass/drag)")]
    public bool useParkingMass = true;
    public float parkedMass = 10000f;
    public float parkedDrag = 5f;
    public float parkedAngularDrag = 5f;
    public bool freezeRotXZWhenParked = true;
    public bool hardStopOnPark = false;

    [Header("Pushing protection")]
    public bool ignorePlayerCollisionWhenParked = true;

    [Header("Player – Hidden objects")]
    [SerializeField] private GameObject playerVisualRoot;
    [SerializeField] private CharacterController playerCC;

    [Header("Freelock (orbit)")]
    public KeyCode freelockToggleKey = KeyCode.Mouse1;
    public float orbitYawSpeed = 180f;
    public float orbitPitchSpeed = 120f;
    public Vector2 orbitPitchLimits = new Vector2(-10f, 65f);
    public float orbitMinDistance = 2.5f;
    public float orbitMaxDistance = 8f;
    public float orbitZoomSpeed = 2.0f;
    public float orbitTargetHeight = 1.3f;
    public float orbitSmooth = 12f;

    private bool isPlayerNearby;
    private bool isInCar;
    private bool isBusy;

    private WeaponManager wm;
    private VehicleDestructible carDestructible;
    private PlayerStats playerScriptRef;
    private PlayerMovement playerMovement;

    private int currentCameraIndex = 0;
    private Transform currentCameraTarget;

    private Rigidbody carRb;
    private float _defMass;
    private float _defDrag;
    private float _defAngDrag;
    private RigidbodyConstraints _defConstraints;

    private Collider[] _carCols;
    private Collider[] _playerCols;

    private SpeedometerUI _activeHud;
    private bool _hudWasInstantiated;

    private bool _useFreelock = false;
    private bool _freelockJustLatched = false;
    private float _orbitYaw;
    private float _orbitPitch;
    private float _orbitDist;

    public event System.Action OnEnterCar;
    public event System.Action OnExitCar;

    public static event System.Action<CarInteraction> OnAnyPlayerEnteredCar;
    public static event System.Action<CarInteraction> OnAnyPlayerExitedCar;

    public static Transform ActiveVehicleTransform { get; private set; }
    public static CarInteraction ActiveCarInteraction { get; private set; }

    public bool IsPlayerInThisCar => isInCar;

    [System.Serializable]
    public struct VehicleCameraSnapshot
    {
        public int cameraIndex;

        public bool useFreelock;
        public float orbitYaw;
        public float orbitPitch;
        public float orbitDistance;
    }

    public Transform VehicleRoot =>
        carObject != null ? carObject.transform : transform;

    public Transform VehicleEnterTarget =>
        transform;

    public Transform VehicleExitPoint =>
        exitPoint != null ? exitPoint : transform;

    [Header("Race UI")]
    [SerializeField] private GameObject carRaceUiRoot;
    [SerializeField] private CarRaceManager raceManager;

    void Start()
    {
        if (playerObject != null)
        {
            playerMovement = playerObject.GetComponent<PlayerMovement>();
            if (!playerCC) playerCC = playerObject.GetComponent<CharacterController>();
        }

        if (weaponInventoryObject != null)
            wm = weaponInventoryObject.GetComponent<WeaponManager>();

        if (gunUI == null)
            gunUI = FindFirstObjectByType<GunUI>();

        if (gunUiRoot == null && gunUI != null)
            gunUiRoot = gunUI.gameObject;

        if (loadingBarRoot != null)
            loadingBarRoot.SetActive(false);

        if (hudController == null)
            hudController = GetComponent<VehicleHudController>();

        if (hudController != null)
        {
            Transform defaultHudParent = hudParent != null
                ? hudParent
                : (PlayerGUI != null ? PlayerGUI.transform : null);

            hudController.SetContext(carObject != null ? carObject : gameObject, defaultHudParent);
        }

        if (carObject != null)
        {
            carDestructible = carObject.GetComponent<VehicleDestructible>();
            carRb = carObject.GetComponent<Rigidbody>();

            if (carRb != null)
            {
                _defMass = carRb.mass;
                _defDrag = carRb.linearDamping;
                _defAngDrag = carRb.angularDamping;
                _defConstraints = carRb.constraints;

                carRb.isKinematic = false;
                carRb.useGravity = true;
                carRb.constraints &= ~(RigidbodyConstraints.FreezePositionX |
                                       RigidbodyConstraints.FreezePositionY |
                                       RigidbodyConstraints.FreezePositionZ);
            }

            _carCols = carObject.GetComponentsInChildren<Collider>(true);
        }

        if (playerObject != null)
        {
            playerScriptRef = playerObject.GetComponent<PlayerStats>();
            _playerCols = playerObject.GetComponentsInChildren<Collider>(true);
        }

        if (!playerVisualRoot && playerObject != null)
        {
            var model = playerObject.transform.Find("Model");
            if (model) playerVisualRoot = model.gameObject;
        }

        foreach (var cam in carCameras)
        {
            if (cam != null) cam.SetActive(false);
        }

        if (carCameras != null && carCameras.Length > 0 && carCameras[0] != null)
            currentCameraTarget = carCameras[0].transform;

        ResetFreelockDefaults();
        SetParked(true);
    }

    void Update()
    {
        if (playerScriptRef != null && playerScriptRef.IsDead) return;
        if (isBusy || PlayerInputHandler.Instance == null) return;
        if (InventoryUI.IsInventoryOpen) return;
        if (DevConsole.IsOpen) return;

        if (PlayerInputHandler.Instance.InteractPressedThisFrame)
        {
            if (!isInCar && isPlayerNearby)
            {
                StartCoroutine(EnterCarRoutine());
            }
            else if (isInCar)
            {
                if (CarRaceManager.AnyRaceBusy)
                    return;

                StartCoroutine(ExitCarRoutine());
            }
        }

        if (isInCar && PlayerInputHandler.Instance.SwitchCameraPressedThisFrame)
        {
            _useFreelock = false;
            SwitchToNextCarCamera();
        }

        if (isInCar &&
            !WorldMapUI.IsOpen &&
            Input.GetKeyDown(freelockToggleKey) &&
            !_useFreelock)
        {
            EnableFreelockFromCurrentCamera();
        }

        if (isInCar && activeCarCamera != null && _useFreelock && !WorldMapUI.IsOpen)
        {
            Vector2 look = PlayerInputHandler.Instance.LookDelta;

            if (look.sqrMagnitude > 0.000001f)
            {
                _orbitYaw += look.x * orbitYawSpeed * Time.unscaledDeltaTime;
                _orbitPitch -= look.y * orbitPitchSpeed * Time.unscaledDeltaTime;
                _orbitPitch = Mathf.Clamp(_orbitPitch, orbitPitchLimits.x, orbitPitchLimits.y);
                _freelockJustLatched = false;
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _orbitDist = Mathf.Clamp(
                    _orbitDist - scroll * orbitZoomSpeed,
                    orbitMinDistance,
                    orbitMaxDistance
                );

                _freelockJustLatched = false;
            }
        }
    }

    void LateUpdate()
    {
        if (!isInCar || activeCarCamera == null) return;

        if (_useFreelock)
        {
            Vector3 target = carObject.transform.position + Vector3.up * orbitTargetHeight;
            Quaternion rot = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);

            Vector3 desiredPos = target + rot * (Vector3.back * _orbitDist);
            Quaternion desiredRot = Quaternion.LookRotation(target - desiredPos, Vector3.up);

            float k = _freelockJustLatched ? 1f : Time.deltaTime * orbitSmooth;

            activeCarCamera.transform.position = Vector3.Lerp(
                activeCarCamera.transform.position,
                desiredPos,
                k
            );

            activeCarCamera.transform.rotation = Quaternion.Slerp(
                activeCarCamera.transform.rotation,
                desiredRot,
                k
            );

            _freelockJustLatched = false;
        }
        else if (currentCameraTarget != null)
        {
            activeCarCamera.transform.position = Vector3.Lerp(
                activeCarCamera.transform.position,
                currentCameraTarget.position,
                Time.deltaTime * cameraLerpSpeed
            );

            activeCarCamera.transform.rotation = Quaternion.Slerp(
                activeCarCamera.transform.rotation,
                currentCameraTarget.rotation,
                Time.deltaTime * cameraLerpSpeed
            );
        }
    }

    void SwitchToNextCarCamera()
    {
        if (carCameras == null || carCameras.Length == 0)
            return;

        for (int i = 0; i < carCameras.Length; i++)
        {
            currentCameraIndex = (currentCameraIndex + 1) % carCameras.Length;

            if (carCameras[currentCameraIndex] != null)
            {
                currentCameraTarget = carCameras[currentCameraIndex].transform;
                return;
            }
        }

        currentCameraTarget = null;
    }

    private void SetGunUIVisible(bool visible)
    {
        if (gunUI == null)
            gunUI = FindFirstObjectByType<GunUI>(FindObjectsInactive.Include);

        if (gunUiRoot == null && gunUI != null)
            gunUiRoot = gunUI.gameObject;

        if (gunUiRoot == null)
            return;

        if (!visible)
        {
            if (!gunUiStateCached)
            {
                gunUiRootWasActive = gunUiRoot.activeSelf;
                gunUiStateCached = true;
            }

            gunUiRoot.SetActive(false);
            return;
        }

        // After leaving a vehicle, player is on foot again.
        // Force GunUI back on instead of restoring a cached "false" state from load-in-car.
        gunUiRoot.SetActive(true);
        gunUiRootWasActive = true;
        gunUiStateCached = false;

        if (gunUI != null)
            gunUI.enabled = true;

        if (wm != null)
        {
            wm.enabled = true;
            wm.RefreshWeaponHUD();
        }

        InventoryUI.Instance?.RefreshGunUIFromWeaponManager();
    }

    private void ForceRestoreGunUIAfterVehicle()
    {
        if (gunUI == null)
            gunUI = FindFirstObjectByType<GunUI>(FindObjectsInactive.Include);

        if (gunUiRoot == null && gunUI != null)
            gunUiRoot = gunUI.gameObject;

        if (gunUiRoot != null)
            gunUiRoot.SetActive(true);

        if (gunUI != null)
            gunUI.enabled = true;

        gunUiRootWasActive = true;
        gunUiStateCached = false;

        if (wm != null)
        {
            wm.enabled = true;
            wm.RefreshWeaponHUD();
        }

        InventoryUI.Instance?.RefreshGunUIFromWeaponManager();
    }

    IEnumerator EnterCarRoutine()
    {
        if (carDestructible != null && carDestructible.isPermanentlyDestroyed)
            yield break;

        isBusy = true;
        PlayerMovement.IsMovementLocked = true;
        yield return StartCoroutine(ShowLoadingBar(1f));

        SetParked(false);

        var uiManager = Object.FindFirstObjectByType<CarUIManager>();
        var carInfo = carObject != null ? carObject.GetComponent<CarInfo>() : null;
        if (uiManager != null && carInfo != null)
            uiManager.ShowCarName(carInfo.carDisplayName);

        var controller = carObject != null ? carObject.GetComponent<CarControll>() : null;
        if (controller != null)
        {
            controller.isControlled = true;
            controller.enabled = true;
        }

        if (carDestructible != null && playerScriptRef != null)
            carDestructible.AssignPlayerRef(playerScriptRef);

        isInCar = true;

        if (carObject != null)
            ActiveVehicleTransform = carObject.transform;

        if (playerMovement != null)
            playerMovement.IsInVehicle = true;

        VehicleFacade vehicleFacade = carObject != null
            ? carObject.GetComponent<VehicleFacade>()
            : GetComponent<VehicleFacade>();

        if (vehicleFacade != null)
        {
            QuickSaveSystem.Instance?.SetCurrentVehicle(vehicleFacade, true);
        }
        else
        {
            QuickSaveSystem.Instance?.SetCurrentVehicle(
                carObject != null ? carObject.transform : transform,
                true
            );
        }

        if (playerVisualRoot != null)
            playerVisualRoot.SetActive(false);

        if (playerCC != null)
            playerCC.enabled = false;

        if (playerObject != null && seatPosition != null)
        {
            playerObject.transform.SetPositionAndRotation(
                seatPosition.position,
                seatPosition.rotation
            );
        }

        if (carCamera != null) carCamera.SetActive(true);
        if (playerCamera != null) playerCamera.SetActive(false);
        if (PlayerGUI != null) PlayerGUI.SetActive(true);

        if (hudController != null)
        {
            hudController.OnPlayerEnteredVehicle(controller);
        }
        else
        {
            SetGunUIVisible(false);

            if (wm != null)
                wm.enabled = false;
        }

        if (carCameras != null && carCameras.Length > 0)
        {
            currentCameraIndex = 0;

            for (int i = 0; i < carCameras.Length; i++)
            {
                if (carCameras[i] != null)
                    carCameras[i].SetActive(i == 0);
            }

            currentCameraTarget = carCameras[0] != null ? carCameras[0].transform : null;
        }

        if (carRaceUiRoot != null)
            carRaceUiRoot.SetActive(false);

        _useFreelock = false;
        ResetFreelockDefaults();

        if (carObject != null)
            MinimapTargetProvider.Instance?.SetVehicleTarget(carObject.transform);

        isBusy = false;

        ActiveCarInteraction = this;

        OnEnterCar?.Invoke();
        OnAnyPlayerEnteredCar?.Invoke(this);
    }

    IEnumerator ExitCarRoutine()
    {
        isBusy = true;
        yield return StartCoroutine(ShowLoadingBar(0.25f));

        if (playerObject != null && !playerObject.activeSelf)
            playerObject.SetActive(true);

        MinimapTargetProvider.Instance?.ClearVehicleTarget();

        if (raceManager != null && raceManager.raceActive && !raceManager.raceFinished)
        {
            raceManager.ResetRace();
        }

        var controller = carObject != null ? carObject.GetComponent<CarControll>() : null;
        if (controller != null)
        {
            controller.isControlled = false;
            controller.enabled = false;
        }

        isInCar = false;

        if (carObject != null && ActiveVehicleTransform == carObject.transform)
            ActiveVehicleTransform = null;

        VehicleFacade vehicleFacade = carObject != null
            ? carObject.GetComponent<VehicleFacade>()
            : GetComponent<VehicleFacade>();

        if (vehicleFacade != null)
        {
            QuickSaveSystem.Instance?.SetCurrentVehicle(vehicleFacade, false);
        }
        else
        {
            QuickSaveSystem.Instance?.SetCurrentVehicle(
                carObject != null ? carObject.transform : transform,
                false
            );
        }

        if (playerObject != null)
        {
            if (exitPoint != null)
            {
                playerObject.transform.SetPositionAndRotation(
                    exitPoint.position,
                    Quaternion.Euler(0f, exitPoint.eulerAngles.y, 0f)
                );
            }
            else if (carObject != null)
            {
                Vector3 exitPos = carObject.transform.position + carObject.transform.right * 2f;
                playerObject.transform.SetPositionAndRotation(
                    exitPos,
                    Quaternion.Euler(0f, carObject.transform.eulerAngles.y, 0f)
                );
            }
        }

        if (playerVisualRoot != null)
            playerVisualRoot.SetActive(true);

        if (playerCC != null)
            playerCC.enabled = true;

        if (playerMovement != null)
            playerMovement.IsInVehicle = false;

        PlayerMovement.IsMovementLocked = false;

        if (carCamera != null) carCamera.SetActive(false);
        if (playerCamera != null) playerCamera.SetActive(true);
        if (PlayerGUI != null) PlayerGUI.SetActive(true);

        if (hudController != null)
        {
            hudController.OnPlayerExitedVehicle();
        }
        else
        {
            if (wm != null)
            {
                wm.enabled = true;
                wm.RefreshWeaponHUD();
            }

            ForceRestoreGunUIAfterVehicle();
        }

        if (carCameras != null)
        {
            foreach (var cam in carCameras)
            {
                if (cam != null) cam.SetActive(false);
            }
        }

        SetParked(true);

        _useFreelock = false;
        currentCameraIndex = 0;
        currentCameraTarget = (carCameras != null && carCameras.Length > 0 && carCameras[0] != null)
            ? carCameras[0].transform
            : null;

        ResetFreelockDefaults();
        if (hudController == null)
        {
            TearDownHud();
            ForceHideCarHud();
        }

        if (carDestructible != null)
            carDestructible.AssignPlayerRef(null);

        isBusy = false;

        if (ActiveCarInteraction == this)
            ActiveCarInteraction = null;

        OnExitCar?.Invoke();
        OnAnyPlayerExitedCar?.Invoke(this);
    }

    public void RestorePlayerInsideCarFromLoad(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if (carDestructible != null && carDestructible.isPermanentlyDestroyed)
            return;

        StopAllCoroutines();

        isBusy = false;

        SetParked(false);

        var controller = carObject != null ? carObject.GetComponent<CarControll>() : null;

        if (controller != null)
        {
            controller.isControlled = true;
            controller.enabled = true;
        }

        if (carRb != null)
        {
            carRb.isKinematic = false;
            carRb.useGravity = true;
            carRb.linearVelocity = linearVelocity;
            carRb.angularVelocity = angularVelocity;
            carRb.WakeUp();
        }

        if (carDestructible != null && playerScriptRef != null)
            carDestructible.AssignPlayerRef(playerScriptRef);

        isInCar = true;

        if (carObject != null)
            ActiveVehicleTransform = carObject.transform;

        ActiveCarInteraction = this;

        if (playerObject != null)
            playerObject.SetActive(false);

        if (playerMovement != null)
            playerMovement.IsInVehicle = true;

        QuickSaveSystem.Instance?.SetCurrentVehicle(
            carObject != null ? carObject.transform : transform,
            true
        );

        if (playerVisualRoot != null)
            playerVisualRoot.SetActive(false);

        if (playerCC != null)
            playerCC.enabled = false;

        if (playerObject != null && seatPosition != null)
        {
            playerObject.transform.SetPositionAndRotation(
                seatPosition.position,
                seatPosition.rotation
            );
        }

        if (carCamera != null)
            carCamera.SetActive(true);

        if (playerCamera != null)
            playerCamera.SetActive(false);

        if (PlayerGUI != null)
            PlayerGUI.SetActive(true);

        if (hudController != null)
        {
            hudController.OnPlayerRestoredInsideVehicle(controller);
        }
        else
        {
            SetGunUIVisible(false);

            if (wm != null)
                wm.enabled = false;
        }

        if (carCameras != null && carCameras.Length > 0)
        {
            currentCameraIndex = 0;

            for (int i = 0; i < carCameras.Length; i++)
            {
                if (carCameras[i] != null)
                    carCameras[i].SetActive(i == 0);
            }

            currentCameraTarget = carCameras[0] != null ? carCameras[0].transform : null;
        }

        if (carRaceUiRoot != null)
            carRaceUiRoot.SetActive(false);

        _useFreelock = false;
        ResetFreelockDefaults();

        if (carObject != null)
            MinimapTargetProvider.Instance?.SetVehicleTarget(carObject.transform);

        if (hudController == null)
        {
            ForceHideCarHud();
            SetupHud(controller);
        }

        OnEnterCar?.Invoke();
        OnAnyPlayerEnteredCar?.Invoke(this);
    }

    void EnableFreelockFromCurrentCamera()
    {
        if (activeCarCamera == null || carObject == null) return;

        Vector3 target = carObject.transform.position + Vector3.up * orbitTargetHeight;
        Vector3 offset = activeCarCamera.transform.position - target;

        _orbitDist = Mathf.Clamp(offset.magnitude, orbitMinDistance, orbitMaxDistance);

        Quaternion look = Quaternion.LookRotation(
            target - activeCarCamera.transform.position,
            Vector3.up
        );

        Vector3 e = look.eulerAngles;
        _orbitYaw = e.y;
        _orbitPitch = (e.x > 180f) ? e.x - 360f : e.x;

        _useFreelock = true;
        _freelockJustLatched = true;
    }

    void SetParked(bool parked)
    {
        if (carRb == null) return;

        if (parked)
        {
            carRb.isKinematic = true;
            carRb.useGravity = false;

            if (useParkingMass)
            {
                carRb.mass = parkedMass;
                carRb.linearDamping = parkedDrag;
                carRb.angularDamping = parkedAngularDrag;
            }

            carRb.constraints = RigidbodyConstraints.FreezeAll;
            carRb.Sleep();

            if (ignorePlayerCollisionWhenParked && _carCols != null && _playerCols != null)
            {
                foreach (var c in _carCols)
                {
                    if (!c || c.isTrigger) continue;

                    foreach (var p in _playerCols)
                    {
                        if (!p || p.isTrigger) continue;
                        Physics.IgnoreCollision(c, p, true);
                    }
                }
            }
        }
        else
        {
            carRb.isKinematic = false;
            carRb.useGravity = true;

            carRb.mass = _defMass;
            carRb.linearDamping = _defDrag;
            carRb.angularDamping = _defAngDrag;
            carRb.constraints = _defConstraints;
            carRb.WakeUp();

            if (ignorePlayerCollisionWhenParked && _carCols != null && _playerCols != null)
            {
                foreach (var c in _carCols)
                {
                    if (!c || c.isTrigger) continue;

                    foreach (var p in _playerCols)
                    {
                        if (!p || p.isTrigger) continue;
                        Physics.IgnoreCollision(c, p, false);
                    }
                }
            }
        }
    }

    void SetupHud(CarControll controller)
    {
        if (speedometerPrefab == null && legacySpeedometerInScene != null)
        {
            _activeHud = legacySpeedometerInScene.GetComponent<SpeedometerUI>();
            _hudWasInstantiated = false;
        }
        else if (speedometerPrefab != null)
        {
            Transform parent = hudParent ? hudParent : (PlayerGUI ? PlayerGUI.transform : null);
            _activeHud = Instantiate(speedometerPrefab, parent);
            _hudWasInstantiated = true;
        }

        if (_activeHud != null)
        {
            _activeHud.carController = controller;
            _activeHud.nitroSystem = carObject != null ? carObject.GetComponent<NitroSystem>() : null;

            if (_activeHud.speedometerRoot != null)
                _activeHud.speedometerRoot.SetActive(true);
        }
    }

    void TearDownHud()
    {
        if (_activeHud == null) return;

        if (_hudWasInstantiated)
            Destroy(_activeHud.gameObject);
        else if (_activeHud.speedometerRoot != null)
            _activeHud.speedometerRoot.SetActive(false);

        _activeHud = null;
        _hudWasInstantiated = false;
    }

    private void ForceHideCarHud()
    {
        if (_activeHud != null)
        {
            if (_activeHud.speedometerRoot != null)
                _activeHud.speedometerRoot.SetActive(false);

            if (_hudWasInstantiated)
                Destroy(_activeHud.gameObject);

            _activeHud = null;
            _hudWasInstantiated = false;
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

    void ResetFreelockDefaults()
    {
        if (carObject != null)
            _orbitYaw = carObject.transform.eulerAngles.y;
        else
            _orbitYaw = 0f;

        _orbitPitch = Mathf.Clamp(15f, orbitPitchLimits.x, orbitPitchLimits.y);
        _orbitDist = Mathf.Clamp((orbitMinDistance + orbitMaxDistance) * 0.5f, orbitMinDistance, orbitMaxDistance);
        _useFreelock = false;
        _freelockJustLatched = false;
    }

    IEnumerator ShowLoadingBar(float duration)
    {
        if (loadingBarRoot == null || loadingBarFill == null)
            yield break;

        loadingBarRoot.SetActive(true);
        loadingBarFill.fillAmount = 0f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            loadingBarFill.fillAmount = Mathf.Clamp01(t / duration);
            yield return null;
        }

        loadingBarRoot.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            isPlayerNearby = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            isPlayerNearby = false;
    }

    public VehicleCameraSnapshot GetCameraSnapshot()
    {
        return new VehicleCameraSnapshot
        {
            cameraIndex = currentCameraIndex,

            useFreelock = _useFreelock,
            orbitYaw = _orbitYaw,
            orbitPitch = _orbitPitch,
            orbitDistance = _orbitDist
        };
    }

    public void ApplyCameraSnapshot(VehicleCameraSnapshot snapshot)
    {
        if (carCameras == null || carCameras.Length == 0)
            return;

        currentCameraIndex = Mathf.Clamp(snapshot.cameraIndex, 0, carCameras.Length - 1);

        for (int i = 0; i < carCameras.Length; i++)
        {
            if (carCameras[i] != null)
                carCameras[i].SetActive(i == currentCameraIndex);
        }

        currentCameraTarget = carCameras[currentCameraIndex] != null
            ? carCameras[currentCameraIndex].transform
            : null;

        _useFreelock = snapshot.useFreelock;
        _orbitYaw = snapshot.orbitYaw;
        _orbitPitch = snapshot.orbitPitch;
        _orbitDist = Mathf.Clamp(snapshot.orbitDistance, orbitMinDistance, orbitMaxDistance);
        _freelockJustLatched = true;
    }
}