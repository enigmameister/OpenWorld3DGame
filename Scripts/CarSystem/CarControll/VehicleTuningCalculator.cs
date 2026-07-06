using UnityEngine;

public static class VehicleTuningCalculator
{
    public static VehicleTuningProfile CreateRuntimeProfile(
        VehicleTuningProfile baseProfile,
        VehicleUpgradeState upgrades)
    {
        if (baseProfile == null)
            return null;

        if (upgrades == null)
            upgrades = new VehicleUpgradeState();

        upgrades.Clamp();

        VehicleTuningProfile runtime = ScriptableObject.CreateInstance<VehicleTuningProfile>();
        CopyProfile(baseProfile, runtime);

        ApplyEngine(runtime, upgrades.engineLevel);
        ApplyTransmission(runtime, upgrades.transmissionLevel);
        ApplyNitro(runtime, upgrades.nitroLevel);
        ApplyHandling(runtime, upgrades.handlingLevel);
        ApplySuspension(runtime, upgrades.suspensionLevel);

        return runtime;
    }

    private static void CopyProfile(VehicleTuningProfile source, VehicleTuningProfile target)
    {
        target.acceleration = source.acceleration;
        target.reverseAcceleration = source.reverseAcceleration;
        target.brakeForce = source.brakeForce;
        target.coastDrag = source.coastDrag;

        target.maxSpeedKPH = source.maxSpeedKPH;
        target.maxReverseKPH = source.maxReverseKPH;

        target.highSpeedAccelFactor = source.highSpeedAccelFactor;
        target.launchBoostSpeed = source.launchBoostSpeed;
        target.launchMinThrottle = source.launchMinThrottle;

        target.steerAtLow = source.steerAtLow;
        target.steerAtHigh = source.steerAtHigh;
        target.steerResponse = source.steerResponse;
        target.maxVisualSteerAngle = source.maxVisualSteerAngle;

        target.lateralGrip = source.lateralGrip;
        target.yawStability = source.yawStability;
        target.extraDownforce = source.extraDownforce;

        target.highSpeedGripAssist = source.highSpeedGripAssist;
        target.lateralVelocityKill = source.lateralVelocityKill;
        target.steeringSpeedLossReducer = source.steeringSpeedLossReducer;
        target.rollStability = source.rollStability;
        target.pitchStability = source.pitchStability;

        target.driftSideGrip = source.driftSideGrip;
        target.normalSideGrip = source.normalSideGrip;
        target.driftYawAssist = source.driftYawAssist;
        target.driftMinSpeedKmh = source.driftMinSpeedKmh;

        target.airControl = source.airControl;
        target.airDownforce = source.airDownforce;

        target.nitroForwardForce = source.nitroForwardForce;
        target.nitroAccelerationMultiplier = source.nitroAccelerationMultiplier;
        target.nitroMaxSpeedBonus = source.nitroMaxSpeedBonus;

        target.maxGear = source.maxGear;

        if (source.gearSpeedLimits != null)
            target.gearSpeedLimits = (float[])source.gearSpeedLimits.Clone();

        target.shiftUpBuffer = source.shiftUpBuffer;
        target.shiftDownBuffer = source.shiftDownBuffer;
        target.shiftCooldown = source.shiftCooldown;
        target.shiftSpeedDrop = source.shiftSpeedDrop;
        target.downshiftBufferKPH = source.downshiftBufferKPH;

        target.maxRPM = source.maxRPM;
        target.rpmSmoothing = source.rpmSmoothing;
    }

    private static void ApplyEngine(VehicleTuningProfile profile, int level)
    {
        switch (level)
        {
            case 1:
                profile.acceleration *= 1.08f;
                profile.maxSpeedKPH += 15f;
                profile.maxRPM += 300f;
                break;

            case 2:
                profile.acceleration *= 1.18f;
                profile.maxSpeedKPH += 35f;
                profile.maxRPM += 650f;
                break;

            case 3:
                profile.acceleration *= 1.32f;
                profile.maxSpeedKPH += 65f;
                profile.maxRPM += 1000f;
                break;
        }
    }

    private static void ApplyTransmission(VehicleTuningProfile profile, int level)
    {
        if (level <= 0)
            return;

        float gearStretch;
        float shiftCooldownMul;
        float speedDropBonus;

        switch (level)
        {
            case 1:
                gearStretch = 1.04f;
                shiftCooldownMul = 0.92f;
                speedDropBonus = 0.01f;
                break;

            case 2:
                gearStretch = 1.08f;
                shiftCooldownMul = 0.84f;
                speedDropBonus = 0.02f;
                break;

            default:
                gearStretch = 1.13f;
                shiftCooldownMul = 0.72f;
                speedDropBonus = 0.03f;
                break;
        }

        if (profile.gearSpeedLimits != null)
        {
            for (int i = 0; i < profile.gearSpeedLimits.Length; i++)
                profile.gearSpeedLimits[i] *= gearStretch;
        }

        profile.shiftCooldown *= shiftCooldownMul;
        profile.shiftSpeedDrop = Mathf.Clamp01(profile.shiftSpeedDrop + speedDropBonus);
    }

    private static void ApplyNitro(VehicleTuningProfile profile, int level)
    {
        switch (level)
        {
            case 1:
                profile.nitroForwardForce *= 1.12f;
                profile.nitroAccelerationMultiplier += 0.03f;
                profile.nitroMaxSpeedBonus += 10f;
                break;

            case 2:
                profile.nitroForwardForce *= 1.28f;
                profile.nitroAccelerationMultiplier += 0.07f;
                profile.nitroMaxSpeedBonus += 22f;
                break;

            case 3:
                profile.nitroForwardForce *= 1.48f;
                profile.nitroAccelerationMultiplier += 0.12f;
                profile.nitroMaxSpeedBonus += 38f;
                break;
        }
    }

    private static void ApplyHandling(VehicleTuningProfile profile, int level)
    {
        switch (level)
        {
            case 1:
                profile.lateralGrip *= 1.08f;
                profile.highSpeedGripAssist *= 1.06f;
                profile.steerAtHigh += 0.75f;
                break;

            case 2:
                profile.lateralGrip *= 1.18f;
                profile.highSpeedGripAssist *= 1.12f;
                profile.steerAtHigh += 1.5f;
                profile.lateralVelocityKill *= 1.08f;
                break;

            case 3:
                profile.lateralGrip *= 1.32f;
                profile.highSpeedGripAssist *= 1.22f;
                profile.steerAtHigh += 2.5f;
                profile.lateralVelocityKill *= 1.16f;
                break;
        }
    }

    private static void ApplySuspension(VehicleTuningProfile profile, int level)
    {
        switch (level)
        {
            case 1:
                profile.extraDownforce *= 1.08f;
                profile.yawStability *= 1.05f;
                profile.rollStability *= 0.92f;
                profile.pitchStability *= 0.94f;
                break;

            case 2:
                profile.extraDownforce *= 1.18f;
                profile.yawStability *= 1.12f;
                profile.rollStability *= 0.84f;
                profile.pitchStability *= 0.88f;
                break;

            case 3:
                profile.extraDownforce *= 1.32f;
                profile.yawStability *= 1.2f;
                profile.rollStability *= 0.75f;
                profile.pitchStability *= 0.8f;
                break;
        }
    }
}