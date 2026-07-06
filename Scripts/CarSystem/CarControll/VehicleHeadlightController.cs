using UnityEngine;

[DisallowMultipleComponent]
public class VehicleHeadlightController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject vehicleObject;
    [SerializeField] private CarInteraction carInteraction;
    [SerializeField] private LightController lightController;

    [Header("Low Beams")]
    [SerializeField] private Light[] lowBeams;
    [SerializeField] private float lowRange = 150f;
    [SerializeField] private float lowIntensity = 250f;
    [SerializeField] private float lowInnerAngle = 60f;
    [SerializeField] private float lowOuterAngle = 85f;

    [Header("High Beams")]
    [SerializeField] private Light[] highBeams;
    [SerializeField] private float highRange = 300f;
    [SerializeField] private float highIntensity = 500f;
    [SerializeField] private float highInnerAngle = 55f;
    [SerializeField] private float highOuterAngle = 70f;

    [Header("Controls")]
    [SerializeField] private KeyCode toggleHighBeamKey = KeyCode.L;
    [SerializeField] private bool highBeamEnabled;

    [Header("Runtime")]
    [SerializeField] private bool playerInside;
    [SerializeField] private bool nightLightsAllowed;

    private void Awake()
    {
        ResolveRefs();
        ApplySettings();
        RefreshLights();
    }

    private void OnEnable()
    {
        ResolveRefs();

        if (carInteraction != null)
        {
            carInteraction.OnEnterCar += HandlePlayerEntered;
            carInteraction.OnExitCar += HandlePlayerExited;
        }

        LightController.OnGlobalVehicleLightsChanged += HandleGlobalVehicleLightsChanged;
    }

    private void OnDisable()
    {
        if (carInteraction != null)
        {
            carInteraction.OnEnterCar -= HandlePlayerEntered;
            carInteraction.OnExitCar -= HandlePlayerExited;
        }

        LightController.OnGlobalVehicleLightsChanged -= HandleGlobalVehicleLightsChanged;
    }

    private void Start()
    {
        ResolveRefs();

        nightLightsAllowed = lightController == null || lightController.ShouldLightsBeOnNow();

        playerInside = carInteraction != null && carInteraction.IsPlayerInThisCar;

        RefreshLights();
    }

    private void Update()
    {
        if (!playerInside)
            return;

        if (Input.GetKeyDown(toggleHighBeamKey))
        {
            highBeamEnabled = !highBeamEnabled;
            RefreshLights();
        }
    }

    private void ResolveRefs()
    {
        if (vehicleObject == null)
        {
            VehicleFacade facade = GetComponentInParent<VehicleFacade>();

            if (facade != null)
                vehicleObject = facade.gameObject;
            else
                vehicleObject = transform.root.gameObject;
        }

        if (carInteraction == null && vehicleObject != null)
            carInteraction = vehicleObject.GetComponentInChildren<CarInteraction>(true);

        if (lightController == null)
            lightController = FindFirstObjectByType<LightController>(FindObjectsInactive.Include);
    }

    private void HandlePlayerEntered()
    {
        playerInside = true;

        if (lightController != null)
            nightLightsAllowed = lightController.ShouldLightsBeOnNow();

        RefreshLights();
    }

    private void HandlePlayerExited()
    {
        playerInside = false;
        highBeamEnabled = false;

        RefreshLights();
    }

    private void HandleGlobalVehicleLightsChanged(bool lightsOn)
    {
        nightLightsAllowed = lightsOn;
        RefreshLights();
    }

    private void ApplySettings()
    {
        ApplyGroup(lowBeams, lowRange, lowIntensity, lowInnerAngle, lowOuterAngle);
        ApplyGroup(highBeams, highRange, highIntensity, highInnerAngle, highOuterAngle);
    }

    private void ApplyGroup(Light[] lights, float range, float intensity, float innerAngle, float outerAngle)
    {
        if (lights == null)
            return;

        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];

            if (l == null)
                continue;

            l.type = LightType.Spot;
            l.range = range;
            l.intensity = intensity;
            l.innerSpotAngle = innerAngle;
            l.spotAngle = outerAngle;
            l.shadows = LightShadows.None;
        }
    }

    private void RefreshLights()
    {
        bool lowOn = playerInside && nightLightsAllowed;
        bool highOn = lowOn && highBeamEnabled;

        SetGroup(lowBeams, lowOn);
        SetGroup(highBeams, highOn);
    }

    private void SetGroup(Light[] lights, bool state)
    {
        if (lights == null)
            return;

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].enabled = state;
        }
    }

    public VehicleHeadlightSnapshot GetSnapshot()
    {
        return new VehicleHeadlightSnapshot
        {
            highBeamEnabled = highBeamEnabled,
            nightLightsAllowed = nightLightsAllowed
        };
    }

    public void ApplySnapshot(VehicleHeadlightSnapshot snapshot)
    {
        ResolveRefs();

        highBeamEnabled = snapshot.highBeamEnabled;

        if (lightController != null)
            nightLightsAllowed = lightController.ShouldLightsBeOnNow();
        else
            nightLightsAllowed = snapshot.nightLightsAllowed;

        playerInside = carInteraction != null && carInteraction.IsPlayerInThisCar;

        RefreshLights();
    }
}