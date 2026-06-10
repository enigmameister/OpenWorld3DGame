using UnityEngine;
using System.Collections.Generic;

public class LightController : MonoBehaviour
{
    [Header("Czas")]
    public DayNightCycle dayNightCycle;

    [Header("Reflektory samochodów")]
    public List<GameObject> vehicleHeadlights = new List<GameObject>();

    [Header("Latarnie uliczne")]
    public List<GameObject> streetLights = new List<GameObject>();

    [Header("Godziny aktywacji świateł")]
    public int nightStartHour = 20;
    public int nightEndHour = 6;

    private bool lightsOn = false;

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

        foreach (var lightObj in vehicleHeadlights)
        {
            if (lightObj != null)
            {
                foreach (var light in lightObj.GetComponentsInChildren<Light>(true))
                    light.enabled = state;
            }
        }

        foreach (var streetLight in streetLights)
        {
            if (streetLight == null) continue;

            foreach (var light in streetLight.GetComponentsInChildren<Light>(true))
                light.enabled = state;
        }

        Debug.Log($"💡 Światła {(state ? "WŁĄCZONE" : "WYŁĄCZONE")} (godzina {dayNightCycle.CurrentHour}:00)");
    }

    public bool ShouldLightsBeOnNow()
    {
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

        // Od razu ustaw aktywność świateł zgodnie z aktualnym stanem dnia/nocy
        int hour = dayNightCycle.CurrentHour;
        bool shouldBeOn = hour >= nightStartHour || hour < nightEndHour;

        foreach (var light in headlightsRoot.GetComponentsInChildren<Light>(true))
        {
            light.enabled = shouldBeOn;
        }
    }

}
