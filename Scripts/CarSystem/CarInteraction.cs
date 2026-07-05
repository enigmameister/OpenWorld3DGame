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
    public GameObject playerObject;
    public GameObject carObject;
    public GameObject PlayerGUI;
    public Transform exitPoint;

    [Header("UI (interaction)")]
    public GameObject loadingBarRoot;
    public Image loadingBarFill;

    [Header("Player – Hidden objects")]
    [SerializeField] private GameObject playerVisualRoot;
    [SerializeField] private CharacterController playerCC;

    private bool isPlayerNearby;
    private bool isInCar;
    private bool isBusy;

    private VehicleDestructible carDestructible;
    private PlayerStats playerScriptRef;
    private PlayerMovement playerMovement;
    private Rigidbody carRb;

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

        if (PlayerGUI != null) PlayerGUI.SetActive(true);

        if (hudController != null)
            hudController.OnPlayerEnteredVehicle(controller);

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

        if (PlayerGUI != null) PlayerGUI.SetActive(true);

        if (hudController != null)
            hudController.OnPlayerExitedVehicle();

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
            cameraController.OnPlayerRestoredInsideVehicle();

        if (PlayerGUI != null)
            PlayerGUI.SetActive(true);

        if (hudController != null)
            hudController.OnPlayerRestoredInsideVehicle(controller); 

        if (carRaceUiRoot != null)
            carRaceUiRoot.SetActive(false);

        if (carObject != null)
            MinimapTargetProvider.Instance?.SetVehicleTarget(carObject.transform);

        OnEnterCar?.Invoke();
        OnAnyPlayerEnteredCar?.Invoke(this);
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