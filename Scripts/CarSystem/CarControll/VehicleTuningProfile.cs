using UnityEngine;

[CreateAssetMenu(menuName = "Vehicle/Tuning Profile", fileName = "VehicleTuningProfile")]
public class VehicleTuningProfile : ScriptableObject
{
    [Header("Engine")]
    public float acceleration = 35f;
    public float reverseAcceleration = 12f;
    public float brakeForce = 12f;
    public float coastDrag = 0.7f;

    [Header("Speed")]
    public float maxSpeedKPH = 180f;
    public float maxReverseKPH = 28f;

    [Header("Top Speed Pacing")]
    [Range(0.05f, 1f)] public float highSpeedAccelFactor = 0.28f;
    public float launchBoostSpeed = 1.2f;
    [Range(0f, 1f)] public float launchMinThrottle = 0.2f;

    [Header("Steering")]
    public float steerAtLow = 24f;
    public float steerAtHigh = 10f;
    public float steerResponse = 3.5f;
    public float maxVisualSteerAngle = 22f;

    [Header("Grip / Stability")]
    public float lateralGrip = 3.2f;
    public float yawStability = 1.3f;
    public float extraDownforce = 12f;

    [Header("Arcade Handling")]
    public float highSpeedGripAssist = 1.8f;
    public float lateralVelocityKill = 2.8f;
    public float steeringSpeedLossReducer = 0.35f;
    public float rollStability = 0.25f;
    public float pitchStability = 0.45f;

    [Header("Drift")]
    public float driftSideGrip = 0.65f;
    public float normalSideGrip = 3.5f;
    public float driftYawAssist = 1.4f;
    public float driftMinSpeedKmh = 45f;

    [Header("Air")]
    public float airControl = 0.12f;
    public float airDownforce = 14f;

    [Header("Nitro Physics")]
    public float nitroForwardForce = 1200f;
    public float nitroAccelerationMultiplier = 1.05f;
    public float nitroMaxSpeedBonus = 25f;

    [Header("Gearbox")]
    public int maxGear = 6;
    public float[] gearSpeedLimits = { 20f, 40f, 65f, 95f, 130f, 180f };
    public float shiftUpBuffer = 0f;
    public float shiftDownBuffer = 25f;
    public float shiftCooldown = 0.35f;
    [Range(0.85f, 1f)] public float shiftSpeedDrop = 0.96f;
    public float downshiftBufferKPH = 25f;

    [Header("RPM")]
    public float maxRPM = 6500f;
    [Range(0.01f, 0.5f)] public float rpmSmoothing = 0.12f;
}