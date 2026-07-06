using UnityEngine;

[DisallowMultipleComponent]
public class VehicleDriveController : MonoBehaviour
{
    [Header("Vehicle")]
    [SerializeField] private GameObject vehicleObject;

    [Header("Runtime refs")]
    [SerializeField] private CarControll carController;
    [SerializeField] private VehicleDestructible vehicleDestructible;

    public CarControll CarController => carController;
    public VehicleDestructible VehicleDestructible => vehicleDestructible;

    public bool IsPlayerControlled =>
        carController != null && carController.isControlled;

    private void Awake()
    {
        ResolveRefs();
    }

    public void SetContext(GameObject newVehicleObject)
    {
        if (newVehicleObject != null)
            vehicleObject = newVehicleObject;

        ResolveRefs();
    }

    private void ResolveRefs()
    {
        if (vehicleObject == null)
            vehicleObject = gameObject;

        if (carController == null && vehicleObject != null)
            carController = vehicleObject.GetComponent<CarControll>();

        if (carController == null)
            carController = GetComponent<CarControll>();

        if (vehicleDestructible == null && vehicleObject != null)
            vehicleDestructible = vehicleObject.GetComponent<VehicleDestructible>();

        if (vehicleDestructible == null)
            vehicleDestructible = GetComponent<VehicleDestructible>();
    }

    public CarControll EnablePlayerControl(PlayerStats playerStats)
    {
        ResolveRefs();

        if (vehicleDestructible != null && vehicleDestructible.isPermanentlyDestroyed)
            return null;

        if (carController != null)
        {
            carController.isControlled = true;
            carController.enabled = true;
        }

        if (vehicleDestructible != null && playerStats != null)
            vehicleDestructible.AssignPlayerRef(playerStats);

        return carController;
    }

    public void DisablePlayerControl()
    {
        ResolveRefs();

        if (carController != null)
        {
            carController.isControlled = false;
            carController.enabled = false;
        }

        if (vehicleDestructible != null)
            vehicleDestructible.AssignPlayerRef(null);
    }

    public bool IsPermanentlyDestroyed()
    {
        ResolveRefs();

        return vehicleDestructible != null &&
               vehicleDestructible.isPermanentlyDestroyed;
    }

    public void DisableParkedControlIfNotExternal()
    {
        ResolveRefs();

        if (carController == null)
            return;

        // Jeœli auto jest sterowane przez AI, nie wy³¹czamy.
        if (carController.useExternalInput)
            return;

        carController.isControlled = false;
        carController.enabled = false;

        if (vehicleDestructible != null)
            vehicleDestructible.AssignPlayerRef(null);
    }
}