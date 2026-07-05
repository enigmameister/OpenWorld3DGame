using UnityEngine;

[DisallowMultipleComponent]
public class VehicleStateBridge : MonoBehaviour
{
    [Header("Vehicle")]
    [SerializeField] private GameObject vehicleObject;
    [SerializeField] private VehicleFacade vehicleFacade;
    [SerializeField] private CarInteraction carInteraction;

    public static Transform ActiveVehicleTransform { get; private set; }
    public static CarInteraction ActiveCarInteraction { get; private set; }

    public static event System.Action<CarInteraction> OnAnyPlayerEnteredCar;
    public static event System.Action<CarInteraction> OnAnyPlayerExitedCar;

    public void SetContext(GameObject newVehicleObject, CarInteraction interaction)
    {
        if (newVehicleObject != null)
            vehicleObject = newVehicleObject;

        if (interaction != null)
            carInteraction = interaction;

        ResolveRefs();
    }

    private void Awake()
    {
        ResolveRefs();
    }

    private void ResolveRefs()
    {
        if (vehicleObject == null)
            vehicleObject = gameObject;

        if (carInteraction == null)
            carInteraction = GetComponent<CarInteraction>();

        if (vehicleFacade == null && vehicleObject != null)
            vehicleFacade = vehicleObject.GetComponent<VehicleFacade>();

        if (vehicleFacade == null)
            vehicleFacade = GetComponent<VehicleFacade>();
    }

    public void NotifyPlayerEntered()
    {
        ResolveRefs();

        Transform vehicleTransform = vehicleObject != null
            ? vehicleObject.transform
            : transform;

        ActiveVehicleTransform = vehicleTransform;
        ActiveCarInteraction = carInteraction;

        SetQuickSaveVehicle(true);

        if (vehicleObject != null)
            MinimapTargetProvider.Instance?.SetVehicleTarget(vehicleObject.transform);

        OnAnyPlayerEnteredCar?.Invoke(carInteraction);
    }

    public void NotifyPlayerExited()
    {
        ResolveRefs();

        if (vehicleObject != null && ActiveVehicleTransform == vehicleObject.transform)
            ActiveVehicleTransform = null;

        if (ActiveCarInteraction == carInteraction)
            ActiveCarInteraction = null;

        SetQuickSaveVehicle(false);

        MinimapTargetProvider.Instance?.ClearVehicleTarget();

        OnAnyPlayerExitedCar?.Invoke(carInteraction);
    }

    public void NotifyPlayerRestoredInsideFromLoad()
    {
        ResolveRefs();

        Transform vehicleTransform = vehicleObject != null
            ? vehicleObject.transform
            : transform;

        ActiveVehicleTransform = vehicleTransform;
        ActiveCarInteraction = carInteraction;

        SetQuickSaveVehicle(true);

        if (vehicleObject != null)
            MinimapTargetProvider.Instance?.SetVehicleTarget(vehicleObject.transform);

        OnAnyPlayerEnteredCar?.Invoke(carInteraction);
    }

    private void SetQuickSaveVehicle(bool playerInside)
    {
        if (QuickSaveSystem.Instance == null)
            return;

        if (vehicleFacade != null)
        {
            QuickSaveSystem.Instance.SetCurrentVehicle(vehicleFacade, playerInside);
            return;
        }

        Transform vehicleTransform = vehicleObject != null
            ? vehicleObject.transform
            : transform;

        QuickSaveSystem.Instance.SetCurrentVehicle(vehicleTransform, playerInside);
    }
}