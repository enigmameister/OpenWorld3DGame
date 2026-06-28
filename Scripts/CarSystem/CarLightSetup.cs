using UnityEngine;

public class CarLightSetup : MonoBehaviour
{
    public GameObject headlightsRoot;
    private LightController lightController;
    private CarInteraction carInteraction;

    private bool lightsRegistered = false;

    void Start()
    {
        // Wyłącz światła na starcie gry
        if (headlightsRoot != null)
        {
            foreach (var light in headlightsRoot.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
            }
        }

        lightController = Object.FindFirstObjectByType<LightController>();
        carInteraction = GetComponent<CarInteraction>();

        if (carInteraction != null)
        {
            carInteraction.OnEnterCar += ActivateHeadlights;
            carInteraction.OnExitCar += DeactivateHeadlights;
        }
    }


    void ActivateHeadlights()
    {
        Debug.Log("Włączam światła po wejściu do auta.");

        if (!lightsRegistered && lightController != null && headlightsRoot != null)
        {
            lightController.RegisterVehicleHeadlights(headlightsRoot);
            lightsRegistered = true;
        }

        if (lightController != null && headlightsRoot != null)
        {
            bool shouldBeOn = lightController.ShouldLightsBeOnNow();
            foreach (var light in headlightsRoot.GetComponentsInChildren<Light>(true))
                light.enabled = shouldBeOn;
        }
    }

    void DeactivateHeadlights() 
    {
        if (headlightsRoot != null)
        {
            foreach (var light in headlightsRoot.GetComponentsInChildren<Light>(true))
                light.enabled = false;

            lightsRegistered = false;
        }
    }

}
