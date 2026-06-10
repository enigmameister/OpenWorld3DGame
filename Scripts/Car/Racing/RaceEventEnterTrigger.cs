using UnityEngine;

public class RaceEventEnterTrigger : MonoBehaviour
{
    public CarRaceManager raceManager;
    public KeyCode openKey = KeyCode.Return;

    [Header("Safety Check")]
    public float maxOpenDistance = 8f;

    private CarControll currentCar;
    private Collider triggerCollider;

    void Awake()
    {
        triggerCollider = GetComponent<Collider>();
    }

    void OnTriggerEnter(Collider other)
    {
        TrySetCurrentCar(other);
    }

    void OnTriggerStay(Collider other)
    {
        if (currentCar != null)
            return;

        TrySetCurrentCar(other);
    }

    void TrySetCurrentCar(Collider other)
    {
        CarControll car = other.GetComponentInParent<CarControll>();
        if (car == null) return;
        if (raceManager == null) return;

        currentCar = car;
        raceManager.ShowEnterRaceUI(true);
    }

    void OnTriggerExit(Collider other)
    {
        CarControll car = other.GetComponentInParent<CarControll>();
        if (car == null) return;
        if (car != currentCar) return;

        currentCar = null;

        if (raceManager != null)
            raceManager.ShowEnterRaceUI(false);
    }

    void Update()
    {
        if (raceManager == null) return;
        if (currentCar == null) return;

        if (CarRaceManager.IsRaceStarting) return;
        if (CarRaceManager.IsRaceLoading) return;
        if (CarRaceManager.CurrentPanelManager != null) return;
        if (CarRaceManager.ActiveRaceManager != null) return;

        if (raceManager.racePhase != CarRaceManager.RacePhase.Idle)
            return;

        if (!IsCarStillInside())
        {
            currentCar = null;
            raceManager.ShowEnterRaceUI(false);
            return;
        }

        if (Input.GetKeyDown(openKey))
            raceManager.OpenRaceEventPanel(currentCar);
    }

    bool IsCarStillInside()
    {
        if (currentCar == null)
            return false;

        float distance = Vector3.Distance(transform.position, currentCar.transform.position);
        if (distance > maxOpenDistance)
            return false;

        if (triggerCollider == null)
            return true;

        Vector3 closest = triggerCollider.ClosestPoint(currentCar.transform.position);
        float closestDistance = Vector3.Distance(closest, currentCar.transform.position);

        return closestDistance < 1.5f;
    }
}