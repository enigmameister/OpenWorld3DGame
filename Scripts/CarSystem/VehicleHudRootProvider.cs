using UnityEngine;

[DisallowMultipleComponent]
public class VehicleHudRootProvider : MonoBehaviour
{
    public static VehicleHudRootProvider Instance { get; private set; }

    [Header("HUD Parents")]
    [SerializeField] private Transform speedometerHudParent;

    public Transform SpeedometerHudParent =>
        speedometerHudParent != null ? speedometerHudParent : transform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[VehicleHudRootProvider] Duplicate provider found on {name}. Using first instance: {Instance.name}");
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}