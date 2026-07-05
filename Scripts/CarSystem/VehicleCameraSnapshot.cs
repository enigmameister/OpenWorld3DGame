using System;
using UnityEngine;

[Serializable]
public struct VehicleCameraSnapshot
{
    public int cameraIndex;

    public bool useFreelock;
    public float orbitYaw;
    public float orbitPitch;
    public float orbitDistance;
}