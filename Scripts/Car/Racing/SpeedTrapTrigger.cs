using System.Collections.Generic;
using UnityEngine;

public class SpeedTrapTrigger : MonoBehaviour
{
    public CarRaceManager raceManager;

    private readonly HashSet<CarControll> triggeredCars = new();

    public bool WasTriggered => false; // zostaw tylko jeœli gdzieœ stare UI tego wymaga

    private void OnTriggerEnter(Collider other)
    {
        CarControll car = other.GetComponentInParent<CarControll>();

        if (car == null)
            return;

        if (triggeredCars.Contains(car))
            return;

        triggeredCars.Add(car);

        if (raceManager != null)
            raceManager.OnSpeedTrapPassed(this, car);
    }

    public void ResetTrap()
    {
        triggeredCars.Clear();
    }

    public bool WasTriggeredBy(CarControll car)
    {
        return car != null && triggeredCars.Contains(car);
    }
}