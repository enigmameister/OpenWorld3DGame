using UnityEngine;

public class RaceEventUIController : MonoBehaviour
{
    public static CarRaceManager ActiveFinishRaceManager;

    public void OnPlayPressed()
    {
        if (CarRaceManager.ActiveRaceManager == null) return;
        CarRaceManager.ActiveRaceManager.ConfirmRaceStart();
    }

    public void OnCancelPressed()
    {
        if (CarRaceManager.ActiveRaceManager == null) return;
        CarRaceManager.ActiveRaceManager.CancelRaceEventPanel();
    }

    public void OnRestartPressed()
    {
        if (ActiveFinishRaceManager == null)
            return;

        if (ActiveFinishRaceManager.IsRacePaused)
            ActiveFinishRaceManager.OnPauseRestartRace();
        else
            ActiveFinishRaceManager.OnFinishRestart();
    }

    public void OnPauseContinuePressed()
    {
        if (ActiveFinishRaceManager == null) return;
        ActiveFinishRaceManager.CloseRacePauseFromButton();
    }

    public void OnLeaveRacePressed()
    {
        if (ActiveFinishRaceManager == null) return;
        ActiveFinishRaceManager.OnPauseLeaveRace();
    }
}