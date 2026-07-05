using System;
using UnityEngine;

public enum VehicleMode
{
    Parked,
    PlayerControlled,
    AIControlled,
    RaceLocked,
    Cutscene,
    Destroyed
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CarControll))]
public class VehicleFacade : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private CarControll controller;
    [SerializeField] private CarInteraction interaction;
    [SerializeField] private NitroSystem nitro;
    [SerializeField] private VehicleDestructible damage;

    [Header("Optional")]
    [SerializeField] private AICarController aiController;
    [SerializeField] private VehicleNpcImpactDetector npcImpactDetector;
    [SerializeField] private CarInfo carInfo;

    [Header("Runtime")]
    [SerializeField] private VehicleMode mode = VehicleMode.Parked;

    public Rigidbody Rigidbody => rb;
    public CarControll Controller => controller;
    public CarInteraction Interaction => interaction;
    public NitroSystem Nitro => nitro;
    public VehicleDestructible Damage => damage;
    public AICarController AIController => aiController;
    public VehicleNpcImpactDetector NpcImpactDetector => npcImpactDetector;
    public CarInfo Info => carInfo;

    public VehicleMode Mode => mode;

    public bool IsPlayerInside => interaction != null && interaction.IsPlayerInThisCar;
    public bool IsDestroyed => damage != null && damage.isPermanentlyDestroyed;
    public bool IsPlayerControlled => mode == VehicleMode.PlayerControlled;
    public bool IsAIControlled => mode == VehicleMode.AIControlled;
    public bool IsRaceLocked => mode == VehicleMode.RaceLocked;

    public Transform VehicleTransform => transform;

    public string DisplayName
    {
        get
        {
            if (carInfo != null && !string.IsNullOrWhiteSpace(carInfo.carDisplayName))
                return carInfo.carDisplayName;

            return gameObject.name;
        }
    }

    public event Action<VehicleMode, VehicleMode> OnModeChanged;

    private void Awake()
    {
        CacheReferences();
        DetectInitialMode();
    }

    private void CacheReferences()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (controller == null)
            controller = GetComponent<CarControll>();

        if (interaction == null)
            interaction = GetComponent<CarInteraction>();

        if (nitro == null)
            nitro = GetComponent<NitroSystem>();

        if (damage == null)
            damage = GetComponent<VehicleDestructible>();

        if (aiController == null)
            aiController = GetComponent<AICarController>();

        if (npcImpactDetector == null)
            npcImpactDetector = GetComponentInChildren<VehicleNpcImpactDetector>(true);

        if (carInfo == null)
            carInfo = GetComponent<CarInfo>();
    }

    private void OnEnable()
    {
        if (interaction != null)
        {
            interaction.OnEnterCar += HandlePlayerEntered;
            interaction.OnExitCar += HandlePlayerExited;
        }
    }

    private void OnDisable()
    {
        if (interaction != null)
        {
            interaction.OnEnterCar -= HandlePlayerEntered;
            interaction.OnExitCar -= HandlePlayerExited;
        }
    }

    private void DetectInitialMode()
    {
        if (IsDestroyed)
        {
            SetMode(VehicleMode.Destroyed);
            return;
        }

        if (interaction != null && interaction.IsPlayerInThisCar)
        {
            SetMode(VehicleMode.PlayerControlled);
            return;
        }

        if (controller != null && controller.useExternalInput)
        {
            SetMode(VehicleMode.AIControlled);
            return;
        }

        SetMode(VehicleMode.Parked);
    }

    private void HandlePlayerEntered()
    {
        SetMode(VehicleMode.PlayerControlled);
    }

    private void HandlePlayerExited()
    {
        if (IsDestroyed)
            SetMode(VehicleMode.Destroyed);
        else
            SetMode(VehicleMode.Parked);
    }

    public void SetMode(VehicleMode newMode)
    {
        if (mode == newMode)
            return;

        VehicleMode oldMode = mode;
        mode = newMode;

        OnModeChanged?.Invoke(oldMode, newMode);
    }

    public void SetRaceLocked(bool locked)
    {
        if (controller != null)
            controller.raceStartLock = locked;

        if (nitro != null)
            nitro.nitroLocked = locked;

        SetMode(locked ? VehicleMode.RaceLocked : VehicleMode.PlayerControlled);
    }

    public void SetPlayerControlled(bool controlled)
    {
        if (controller != null)
        {
            controller.enabled = controlled;
            controller.isControlled = controlled;
            controller.useExternalInput = false;
        }

        SetMode(controlled ? VehicleMode.PlayerControlled : VehicleMode.Parked);
    }

    public void SetAIControlled(bool controlled)
    {
        if (controller != null)
        {
            controller.enabled = controlled;
            controller.isControlled = controlled;
            controller.useExternalInput = controlled;
        }

        if (aiController != null)
            aiController.enabled = controlled;

        if (nitro != null)
            nitro.useExternalNitroInput = controlled;

        SetMode(controlled ? VehicleMode.AIControlled : VehicleMode.Parked);
    }

    public void StopVehicleMotion()
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
    }

    public int GetSpeedKPH()
    {
        if (controller != null)
            return controller.currentSpeedKPH;

        if (rb != null)
            return Mathf.RoundToInt(rb.linearVelocity.magnitude * 3.6f);

        return 0;
    }

    public int GetDisplaySpeedKPH()
    {
        if (controller != null)
            return controller.GetDisplaySpeedKPH();

        return GetSpeedKPH();
    }

    public float GetNitro01()
    {
        if (nitro == null)
            return 0f;

        return nitro.GetNitroNormalized();
    }

    [System.Serializable]
    public struct VehicleRuntimeSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;

        public Vector3 linearVelocity;
        public Vector3 angularVelocity;

        public VehicleMode mode;

        public bool playerInside;

        public float nitro;
        public int currentGear;
        public int currentSpeedKPH;
        public bool isReversing;

        public VehicleCameraSnapshot camera;
    }

    [System.Serializable]
    public struct VehicleSaveSnapshot
    {
        public VehicleRuntimeSnapshot runtime;

        public bool hasDamageSnapshot;
        public VehicleDestructible.VehicleDamageSnapshot damageSnapshot;

        public bool isPermanentlyDestroyed;
    }

    public VehicleRuntimeSnapshot CaptureRuntimeSnapshot()
    {
        return new VehicleRuntimeSnapshot
        {
            position = transform.position,
            rotation = transform.rotation,

            linearVelocity = rb != null ? rb.linearVelocity : Vector3.zero,
            angularVelocity = rb != null ? rb.angularVelocity : Vector3.zero,

            mode = mode,

            playerInside = IsPlayerInside,

            nitro = nitro != null ? nitro.currentNitro : 0f,

            currentGear = controller != null ? controller.currentGear : 1,
            currentSpeedKPH = controller != null ? controller.currentSpeedKPH : 0,
            isReversing = controller != null && controller.isReversing,

            camera = interaction != null
                ? interaction.GetCameraSnapshot()
                : default
        };
    }

    public void ApplyRuntimeSnapshot(VehicleRuntimeSnapshot snapshot)
    {
        transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);

        if (rb != null)
        {
            rb.linearVelocity = snapshot.linearVelocity;
            rb.angularVelocity = snapshot.angularVelocity;
            rb.WakeUp();
        }

        if (nitro != null)
            nitro.currentNitro = Mathf.Clamp(snapshot.nitro, 0f, nitro.maxNitro);

        if (controller != null)
        {
            controller.currentGear = Mathf.Max(1, snapshot.currentGear);
            controller.currentSpeedKPH = Mathf.Max(0, snapshot.currentSpeedKPH);
            controller.isReversing = snapshot.isReversing;
        }

        SetMode(snapshot.mode);
    }

    public VehicleSaveSnapshot CaptureSaveSnapshot()
    {
        VehicleSaveSnapshot snapshot = new VehicleSaveSnapshot
        {
            runtime = CaptureRuntimeSnapshot(),
            hasDamageSnapshot = damage != null,
            damageSnapshot = damage != null ? damage.GetSnapshot() : default,
            isPermanentlyDestroyed = damage != null && damage.isPermanentlyDestroyed
        };

        return snapshot;
    }

    public void ApplySaveSnapshot(VehicleSaveSnapshot snapshot)
    {
        ApplyRuntimeSnapshot(snapshot.runtime);

        if (snapshot.hasDamageSnapshot && damage != null)
            damage.ApplySnapshot(snapshot.damageSnapshot);

        if (damage != null && damage.isPermanentlyDestroyed)
            SetMode(VehicleMode.Destroyed);
    }

    public void RestorePlayerInsideFromLoad(VehicleSaveSnapshot snapshot)
    {
        if (damage != null && damage.isPermanentlyDestroyed)
            return;

        ApplySaveSnapshot(snapshot);

        if (interaction != null)
        {
            interaction.RestorePlayerInsideCarFromLoad(
                snapshot.runtime.linearVelocity,
                snapshot.runtime.angularVelocity
            );

            interaction.ApplyCameraSnapshot(snapshot.runtime.camera);

            SetMode(VehicleMode.PlayerControlled);
        }
    }   

    public void RestoreVehicleOnlyFromLoad(VehicleSaveSnapshot snapshot)
    {
        ApplySaveSnapshot(snapshot);

        if (snapshot.runtime.playerInside)
            return;

        if (controller != null)
        {
            controller.isControlled = false;
            controller.enabled = false;
        }

        SetMode(IsDestroyed ? VehicleMode.Destroyed : VehicleMode.Parked);
    }

}