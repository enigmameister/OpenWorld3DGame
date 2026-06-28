using TMPro;
using UnityEngine;

public class WorldMapEventInfoPanel : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;

    [Header("Values")]
    public TextMeshProUGUI typeValue;
    public TextMeshProUGUI routeValue;
    public TextMeshProUGUI distanceValue;
    public TextMeshProUGUI lapsValue;
    public TextMeshProUGUI lapRecordValue;
    public TextMeshProUGUI totalRecordValue;
    public TextMeshProUGUI rewardValue;

    [Header("Optional Rows")]
    public GameObject tollBoothsHeader;
    public GameObject tollBoothsValueRoot;
    public GameObject speedtrapsHeader;
    public GameObject speedtrapsValueRoot;

    void Awake()
    {
        Hide();
    }

    public void Show(RaceEventDefinition def)
    {
        if (def == null)
            return;

        if (root != null)
            root.SetActive(true);

        if (typeValue != null)
            typeValue.text = def.raceMode.ToString().ToUpper();

        if (routeValue != null)
            routeValue.text = def.raceRouteName.ToUpper();

        if (distanceValue != null)
            distanceValue.text = $"{def.raceLengthKm:0.##}KM";

        if (lapsValue != null)
            lapsValue.text = def.raceMode == CarRaceManager.RaceMode.Circuit ||
                             def.raceMode == CarRaceManager.RaceMode.Elimination
                ? def.totalLaps.ToString()
                : "-";

        if (lapRecordValue != null)
            lapRecordValue.text = "00:00:00";

        if (totalRecordValue != null)
            totalRecordValue.text = "00:00:00";

        if (rewardValue != null)
            rewardValue.text = $"{def.raceRewardCash}$";

        bool isTimeChallenge = def.raceMode == CarRaceManager.RaceMode.TimeChallenge;
        bool isSpeedTrap = def.raceMode == CarRaceManager.RaceMode.SpeedTrap;

        if (tollBoothsHeader != null)
            tollBoothsHeader.SetActive(isTimeChallenge);

        if (tollBoothsValueRoot != null)
            tollBoothsValueRoot.SetActive(isTimeChallenge);

        if (speedtrapsHeader != null)
            speedtrapsHeader.SetActive(isSpeedTrap);

        if (speedtrapsValueRoot != null)
            speedtrapsValueRoot.SetActive(isSpeedTrap);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }
}