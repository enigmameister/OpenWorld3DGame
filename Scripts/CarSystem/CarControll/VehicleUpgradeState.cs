using UnityEngine;

[System.Serializable]
public class VehicleUpgradeState
{
    [Header("Upgrade Levels")]
    [Range(0, 3)] public int engineLevel = 0;
    [Range(0, 3)] public int transmissionLevel = 0;
    [Range(0, 3)] public int nitroLevel = 0;
    [Range(0, 3)] public int handlingLevel = 0;
    [Range(0, 3)] public int suspensionLevel = 0;

    public void Clamp()
    {
        engineLevel = Mathf.Clamp(engineLevel, 0, 3);
        transmissionLevel = Mathf.Clamp(transmissionLevel, 0, 3);
        nitroLevel = Mathf.Clamp(nitroLevel, 0, 3);
        handlingLevel = Mathf.Clamp(handlingLevel, 0, 3);
        suspensionLevel = Mathf.Clamp(suspensionLevel, 0, 3);
    }
}