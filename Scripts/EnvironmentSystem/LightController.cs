using UnityEngine;
using System.Collections.Generic;

public class LightController : MonoBehaviour
{
    [Header("Time")]
    public DayNightCycle dayNightCycle;

    [Header("Car Headlights")]
    public List<GameObject> vehicleHeadlights = new List<GameObject>();

    [Header("Streetlights")]
    public List<GameObject> streetLights = new List<GameObject>();

    [Header("Lights time range")]
    public int nightStartHour = 20;
    public int nightEndHour = 6;

    private bool lightsOn = false;

    public static event System.Action<bool> OnGlobalVehicleLightsChanged;

    void Start()
    {
        var allLamps = GameObject.FindGameObjectsWithTag("StreetLamp");

        foreach (var lamp in allLamps)
            RegisterStreetLight(lamp);

        SetLights(ShouldLightsBeOnNow());
    }

    void Update()
    {
        if (dayNightCycle == null) return;

        int hour = dayNightCycle.CurrentHour;
        bool shouldBeOn = hour >= nightStartHour || hour < nightEndHour;

        if (shouldBeOn != lightsOn)
        {
            SetLights(shouldBeOn);
        }
    }

    void SetLights(bool state)
    {
        lightsOn = state;

        foreach (var streetLight in streetLights)
        {
            if (streetLight == null)
                continue;

            foreach (var light in streetLight.GetComponentsInChildren<Light>(true))
                light.enabled = state;
        }

        OnGlobalVehicleLightsChanged?.Invoke(state);

      //  Debug.Log($"💡 Światła {(state ? "WŁĄCZONE" : "WYŁĄCZONE")} (godzina {dayNightCycle.CurrentHour}:00)");
    }

    public bool ShouldLightsBeOnNow()
    {
        if (dayNightCycle == null)
            return false;

        int hour = dayNightCycle.CurrentHour;

        return hour >= nightStartHour || hour < nightEndHour;
    }

    public void RegisterVehicleHeadlights(GameObject headlights)
    {
        if (!vehicleHeadlights.Contains(headlights))
            vehicleHeadlights.Add(headlights);
    }

    public void RegisterStreetLight(GameObject lamp)
    {
        if (!streetLights.Contains(lamp))
            streetLights.Add(lamp);
    }

    public void RegisterAndEnableVehicleHeadlights(GameObject headlightsRoot)
    {
        if (!vehicleHeadlights.Contains(headlightsRoot))
            vehicleHeadlights.Add(headlightsRoot);

        int hour = dayNightCycle.CurrentHour;
        bool shouldBeOn = hour >= nightStartHour || hour < nightEndHour;

        foreach (var light in headlightsRoot.GetComponentsInChildren<Light>(true))
        {
            light.enabled = shouldBeOn;
        }
    }

}
