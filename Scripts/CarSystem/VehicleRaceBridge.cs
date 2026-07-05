using UnityEngine;

[DisallowMultipleComponent]
public class VehicleRaceBridge : MonoBehaviour
{
    [Header("Race")]
    [SerializeField] private CarRaceManager raceManager;
    [SerializeField] private GameObject carRaceUiRoot;

    public bool BlocksVehicleExit =>
        CarRaceManager.AnyRaceBusy;

    private void Awake()
    {
        ResolveRefs();
    }

    public void SetContext(CarRaceManager newRaceManager = null, GameObject newCarRaceUiRoot = null)
    {
        if (newRaceManager != null)
            raceManager = newRaceManager;

        if (newCarRaceUiRoot != null)
            carRaceUiRoot = newCarRaceUiRoot;

        ResolveRefs();
    }

    private void ResolveRefs()
    {
        if (raceManager == null)
            raceManager = FindFirstObjectByType<CarRaceManager>(FindObjectsInactive.Include);
    }

    public void OnPlayerEnteredVehicle()
    {
        HideCarRaceUI();
    }

    public void OnPlayerExitedVehicle()
    {
        ResetActiveRaceIfNeeded();
    }

    public void OnPlayerRestoredInsideVehicle()
    {
        HideCarRaceUI();
    }

    public void HideCarRaceUI()
    {
        if (carRaceUiRoot != null)
            carRaceUiRoot.SetActive(false);
    }

    private void ResetActiveRaceIfNeeded()
    {
        ResolveRefs();

        if (raceManager != null && raceManager.raceActive && !raceManager.raceFinished)
            raceManager.ResetRace();
    }
}