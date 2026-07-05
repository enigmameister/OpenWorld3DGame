using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CarInteraction : MonoBehaviour
{
    [Header("Vehicle Modules")]
    [SerializeField] private VehicleHudController hudController;
    [SerializeField] private VehicleParkingController parkingController;
    [SerializeField] private VehicleCameraController cameraController;
    [SerializeField] private VehicleOccupantController occupantController;
    [SerializeField] private VehicleStateBridge stateBridge;
    [SerializeField] private VehicleDriveController driveController;

    [Header("References")]
    public Transform seatPosition;
    public GameObject playerObject;
    public GameObject carObject;
    public GameObject PlayerGUI;
    public Transform exitPoint;

    [Header("UI (interaction)")]
    public GameObject loadingBarRoot;
    public Image loadingBarFill;

    private bool isPlayerNearby;
    private bool isInCar;
    private bool isBusy;

    private VehicleDestructible carDestructible;    
    private Rigidbody carRb;

    public event System.Action OnEnterCar;
    public event System.Action OnExitCar;

    public static event System.Action<CarInteraction> OnAnyPlayerEnteredCar
    {
        add => VehicleStateBridge.OnAnyPlayerEnteredCar += value;
        remove => VehicleStateBridge.OnAnyPlayerEnteredCar -= value;
    }

    public static event System.Action<CarInteraction> OnAnyPlayerExitedCar
    {
        add => VehicleStateBridge.OnAnyPlayerExitedCar += value;
        remove => VehicleStateBridge.OnAnyPlayerExitedCar -= value;
    }

    public static Transform ActiveVehicleTransform => VehicleStateBridge.ActiveVehicleTransform;
    public static CarInteraction ActiveCarInteraction => VehicleStateBridge.ActiveCarInteraction;

    public bool IsPlayerInThisCar => isInCar;
    public Transform VehicleRoot =>
        carObject != null ? carObject.transform : transform;

    public Transform VehicleEnterTarget =>
        transform;

    public Transform VehicleExitPoint =>
        exitPoint != null ? exitPoint : transform;

    private PlayerStats PlayerStatsRef =>
    occupantController != null ? occupantController.PlayerStats : null;

    private PlayerMovement PlayerMovementRef =>
        occupantController != null ? occupantController.PlayerMovement : null;

    [Header("Race UI")]
    [SerializeField] private GameObject carRaceUiRoot;
    [SerializeField] private CarRaceManager raceManager;

    void Start()
    {
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
            cameraController.SetContext(carObject != null ? carObject : gameObject);
        }
        if (parkingController == null)
        {
            parkingController = GetComponent<VehicleParkingController>();

            if (parkingController == null)
                Debug.LogWarning($"[CarInteraction] Missing VehicleParkingController on {name}");
        }

        if (parkingController != null)
            parkingController.SetContext(carObject != null ? carObject : gameObject, playerObject);

        if (occupantController == null)
        {
            occupantController = GetComponent<VehicleOccupantController>();

            if (occupantController == null)
                Debug.LogWarning($"[CarInteraction] Missing VehicleOccupantController on {name}");
        }

        if (occupantController != null)
        {
            occupantController.SetContext(
                carObject != null ? carObject : gameObject,
                playerObject,
                seatPosition,
                exitPoint
            );
        }

        if (stateBridge == null)
        {
            stateBridge = GetComponent<VehicleStateBridge>();

            if (stateBridge == null)
                Debug.LogWarning($"[CarInteraction] Missing VehicleStateBridge on {name}");
        }

        if (stateBridge != null)
        {
            stateBridge.SetContext(
                carObject != null ? carObject : gameObject,
                this
            );
        }

        if (driveController == null)
        {
            driveController = GetComponent<VehicleDriveController>();

            if (driveController == null)
                Debug.LogWarning($"[CarInteraction] Missing VehicleDriveController on {name}");
        }

        if (driveController != null)
            driveController.SetContext(carObject != null ? carObject : gameObject);

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

        if (parkingController != null)
            parkingController.SetParked(true);
    }

    void Update()
    {
        if (PlayerStatsRef != null && PlayerStatsRef.IsDead) return;
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

    IEnumerator EnterCarRoutine()
    {
        if (driveController != null && driveController.IsPermanentlyDestroyed())
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

        CarControll controller = null;

        if (driveController != null)
            controller = driveController.EnablePlayerControl(PlayerStatsRef);

        isInCar = true;

        if (occupantController != null)
            occupantController.EnterVehicle();

        if (cameraController != null)
            cameraController.OnPlayerEnteredVehicle();

        if (PlayerGUI != null) PlayerGUI.SetActive(true);

        if (hudController != null)
            hudController.OnPlayerEnteredVehicle(controller);

        if (carRaceUiRoot != null)
            carRaceUiRoot.SetActive(false);

        isBusy = false;

        if (stateBridge != null)
            stateBridge.NotifyPlayerEntered();

        OnEnterCar?.Invoke();
    }

    IEnumerator ExitCarRoutine()
    {
        isBusy = true;
        yield return StartCoroutine(ShowLoadingBar(0.25f));

        if (raceManager != null && raceManager.raceActive && !raceManager.raceFinished)
        {
            raceManager.ResetRace();
        }

        if (driveController != null)
            driveController.DisablePlayerControl();

        isInCar = false;

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

        if (occupantController != null)
            occupantController.ExitVehicle();

        PlayerMovement.IsMovementLocked = false;

        if (cameraController != null)
            cameraController.OnPlayerExitedVehicle();

        if (PlayerGUI != null) PlayerGUI.SetActive(true);

        if (hudController != null)
            hudController.OnPlayerExitedVehicle();

        if (parkingController != null)
            parkingController.SetParked(true);

        isBusy = false;

        if (stateBridge != null)
            stateBridge.NotifyPlayerExited();

        OnExitCar?.Invoke();
    }

    public void RestorePlayerInsideCarFromLoad(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if (driveController != null && driveController.IsPermanentlyDestroyed())
            return;

        StopAllCoroutines();

        isBusy = false;

        if (parkingController != null)
            parkingController.SetParked(false);

        CarControll controller = null;

        if (driveController != null)
            controller = driveController.EnablePlayerControl(PlayerStatsRef);

        if (carRb != null)
        {
            carRb.isKinematic = false;
            carRb.useGravity = true;
            carRb.linearVelocity = linearVelocity;
            carRb.angularVelocity = angularVelocity;
            carRb.WakeUp();
        }

        isInCar = true;

        VehicleFacade vehicleFacade = carObject != null
            ? carObject.GetComponent<VehicleFacade>()
            : GetComponent<VehicleFacade>();

        if (occupantController != null)
            occupantController.RestoreInsideVehicleFromLoad();

        if (cameraController != null)
            cameraController.OnPlayerRestoredInsideVehicle();

        if (PlayerGUI != null)
            PlayerGUI.SetActive(true);

        if (hudController != null)
            hudController.OnPlayerRestoredInsideVehicle(controller); 

        if (carRaceUiRoot != null)
            carRaceUiRoot.SetActive(false);

        if (stateBridge != null)
            stateBridge.NotifyPlayerRestoredInsideFromLoad();

        OnEnterCar?.Invoke();
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

        return default;
    }

    public void ApplyCameraSnapshot(VehicleCameraSnapshot snapshot)
    {
        if (cameraController != null)
            cameraController.ApplySnapshot(snapshot);
    }
}