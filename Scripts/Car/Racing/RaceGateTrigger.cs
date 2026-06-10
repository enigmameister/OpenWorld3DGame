using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RaceGateTrigger : MonoBehaviour
{
    public CarRaceManager raceManager;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (raceManager == null)
            return;

        CarControll car = ExtractCar(other);

        if (car == null)
            return;

        if (raceManager.racePhase != CarRaceManager.RacePhase.Racing)
            return;

        raceManager.OnCarEnteredGate(this, car);
    }

    CarControll ExtractCar(Collider other)
    {
        if (other == null)
            return null;

        if (other.attachedRigidbody != null)
        {
            CarControll car = other.attachedRigidbody.GetComponent<CarControll>();
            if (car != null)
                return car;
        }

        return other.GetComponentInParent<CarControll>();
    }
}