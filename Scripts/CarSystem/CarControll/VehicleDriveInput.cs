using UnityEngine;

[System.Serializable]
public struct VehicleDriveInput
{
    [Range(-1f, 1f)] public float steer;
    [Range(-1f, 1f)] public float throttle;
    public bool handbrake;

    public Vector2 MoveVector => new Vector2(steer, throttle);

    public static VehicleDriveInput Zero => new VehicleDriveInput
    {
        steer = 0f,
        throttle = 0f,
        handbrake = false
    };
}