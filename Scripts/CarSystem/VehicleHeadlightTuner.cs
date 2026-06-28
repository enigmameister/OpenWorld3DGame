using UnityEngine;

public class VehicleHeadlightTuner : MonoBehaviour
{
    public Light[] lowBeams;
    public float lowRange = 70f;
    public float lowIntensity = 7000f;
    public float lowInnerAngle = 18f;
    public float lowOuterAngle = 45f;

    public Light[] highBeams;
    public float highRange = 140f;
    public float highIntensity = 18000f;
    public float highInnerAngle = 10f;
    public float highOuterAngle = 25f;

    [Header("Controls")]
    public KeyCode toggleHighBeamKey = KeyCode.L;
    public bool highBeamEnabled;

    void Start()
    {
        ApplySettings();
        SetHighBeam(highBeamEnabled);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleHighBeamKey))
        {
            highBeamEnabled = !highBeamEnabled;
            SetHighBeam(highBeamEnabled);
        }
    }

    void ApplySettings()
    {
        ApplyGroup(lowBeams, lowRange, lowIntensity, lowInnerAngle, lowOuterAngle);
        ApplyGroup(highBeams, highRange, highIntensity, highInnerAngle, highOuterAngle);
    }

    void ApplyGroup(Light[] lights, float range, float intensity, float innerAngle, float outerAngle)
    {
        if (lights == null) return;

        foreach (var l in lights)
        {
            if (l == null) continue;

            l.type = LightType.Spot;
            l.range = range;
            l.intensity = intensity;
            l.innerSpotAngle = innerAngle;
            l.spotAngle = outerAngle;
            l.shadows = LightShadows.None;
        }
    }

    void SetHighBeam(bool state)
    {
        if (highBeams == null) return;

        foreach (var l in highBeams)
        {
            if (l != null)
                l.enabled = state;
        }
    }
}