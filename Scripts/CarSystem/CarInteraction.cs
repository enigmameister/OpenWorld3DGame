using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CarInteraction : MonoBehaviour
{
    [Header("Vehicle Modules")]
    [SerializeField] private VehicleHudController hudController;
    [SerializeField] private VehicleParkingController parkingController;
    [SerializeField] private VehicleCameraController cameraController;

    [Header("References")]
    public Transform seatPosition;
    public GameObject carCamera;
    public GameObject playerCamera;
    public GameObject playerObject;
    public GameObject carObject;
    public GameObject PlayerGUI;
    public Transform exitPoint;

    [Header("UI (interaction)")]
    public GameObject loadingBarRoot;
    public Image loadingBarFill;

    [Header("Car Cams")]
    public GameObject[] carCameras;
    public Camera activeCarCamera;
    public float cameraLerpSpeed = 5f;

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

    private VehicleDestructible carDestructible;
    private PlayerStats playerScriptRef;
    private PlayerMovement playerMovement;

    private int currentCameraIndex = 0;
    private Transform currentCameraTarget;

    private Rigidbody carRb;

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

        if (loadingBarRoot != null)
            loadingBarRoot.SetActive(false);

        if (hudController == null)
            hudController = GetComponent<VehicleHudController>();

        if (hudController != null)
            hudController.SetContext(carObject != null ? carObject : gameObject);

        if (cameraController == null)
        {
            cameraController = GetComponent<VehicleCameraController>();


            if (cameraController == null)
                Debug.LogWarning($"[CarInteraction] Missing VehicleCameraController on {name}");
        }

        if (cameraController != null)
        {
            cameraController.SetContext(
                carObject != null ? carObject : gameObject,
                carCamera,
                playerCamera,
                carCameras,
                activeCarCamera
            );
        }
        if (parkingController == null)
        {
            parkingController = GetComponent<VehicleParkingController>();

            if (parkingController == null)
                Debug.LogWarning($"[CarInteraction] Missing VehicleParkingController on {name}");
        }

        if (parkingController != null)
            parkingController.SetContext(carObject != null ? carObject : gameObject, playerObject);

        if (carObject != null)
        {
            carDestructible = carObject.GetComponent<VehicleDestructible>();
            carRb = carObject.GetComponent<Rigidbody>();

            if (carRb != null)
            {
                carRb.isKinematic = false;
                carRb.useGravity = true;
                carRb.constraints &= ~(RigidbodyConstraints.FreezePositionX |
                                       RigidbodyConstraints.FreezePositionY |
                                       RigidbodyConstraints.FreezePositionZ);
            }
        }

        if (playerObject != null)
        {
            playerScriptRef = playerObject.GetComponent<PlayerStats>();
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
        if (parkingController != null)
            parkingController.SetParked(true);
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
            if (cameraController != null)
                cameraController.SwitchToNextCamera();
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

    IEnumerator EnterCarRoutine()
    {
        if (carDestructible != null && carDestructible.isPermanentlyDestroyed)
            yield break;

        isBusy = true;
        PlayerMovement.IsMovementLocked = true;
        yield return StartCoroutine(ShowLoadingBar(1f));

        if (parkingController != null)
            parkingController.SetParked(false);

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

        if (cameraController != null)
            cameraController.OnPlayerEnteredVehicle();
        else
        {
            if (carCamera != null) carCamera.SetActive(true);
            if (playerCamera != null) playerCamera.SetActive(false);
        }

        if (PlayerGUI != null) PlayerGUI.SetActive(true);

        if (hudController != null)
            hudController.OnPlayerEnteredVehicle(controller);

        if (cameraController == null && carCameras != null && carCameras.Length > 0)
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

        if (cameraController != null)
            cameraController.OnPlayerExitedVehicle();
        else
        {
            if (carCamera != null) carCamera.SetActive(false);
            if (playerCamera != null) playerCamera.SetActive(true);
        }

        if (PlayerGUI != null) PlayerGUI.SetActive(true);

        if (hudController != null)
            hudController.OnPlayerExitedVehicle();

        if (cameraController == null)
        {
            if (carCameras != null)
            {
                foreach (var cam in carCameras)
                {
                    if (cam != null) cam.SetActive(false);
                }
            }

            _useFreelock = false;
            currentCameraIndex = 0;
            currentCameraTarget = (carCameras != null && carCameras.Length > 0 && carCameras[0] != null)
                ? carCameras[0].transform
                : null;

            ResetFreelockDefaults();
        }

        if (parkingController != null)
            parkingController.SetParked(true);

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

        if (parkingController != null)
            parkingController.SetParked(false);

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

        if (cameraController != null)
            cameraController.OnPlayerRestoredInsideVehicle();
        else
        {
            if (carCamera != null)
                carCamera.SetActive(true);

            if (playerCamera != null)
                playerCamera.SetActive(false);
        }

        if (PlayerGUI != null)
            PlayerGUI.SetActive(true);

        if (hudController != null)
            hudController.OnPlayerRestoredInsideVehicle(controller);

        if (cameraController == null && carCameras != null && carCameras.Length > 0)
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
        if (cameraController != null)
            return cameraController.GetSnapshot();

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
        if (cameraController != null)
        {
            cameraController.ApplySnapshot(snapshot);
            return;
        }

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