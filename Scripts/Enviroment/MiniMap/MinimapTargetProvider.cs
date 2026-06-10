using UnityEngine;

public class MinimapTargetProvider : MonoBehaviour
{
    public static MinimapTargetProvider Instance { get; private set; }

    [Header("Default Target")]
    public Transform playerTarget;

    public Transform CurrentTarget { get; private set; }

    void Awake()
    {
        Instance = this;
        CurrentTarget = playerTarget;
    }

    public void SetPlayerTarget(Transform player)
    {
        playerTarget = player;

        if (CurrentTarget == null)
            CurrentTarget = playerTarget;
    }

    public void SetVehicleTarget(Transform vehicle)
    {
        if (vehicle != null)
            CurrentTarget = vehicle;
    }

    public void ClearVehicleTarget()
    {
        CurrentTarget = playerTarget;
    }
}