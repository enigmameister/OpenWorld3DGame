using System.Collections.Generic;
using UnityEngine;

public class RaceEventDefinition : MonoBehaviour
{
    [Header("Core")]
    public CarRaceManager.RaceMode raceMode;
    public RaceRoute raceRoute;
    public RaceRouteArrowGenerator routeArrowGenerator;

    [Header("Start / Finish")]
    public Transform raceStartPoint;
    public RaceGateTrigger finishGate;

    [Header("Circuit / Elimination")]
    [Min(1)] public int totalLaps = 3;
    public List<RaceGateTrigger> splitGates = new();

    [Header("Speed Trap")]
    public List<SpeedTrapTrigger> speedTraps = new();
    public GameObject speedTrapVisualRoot;

    [Header("Time Challenge")]
    public List<RaceGateTrigger> timeChallengeGates = new();
    public float timeChallengeStartTime = 20f;

    [Header("Display")]
    public string raceDisplayName = "RACE";
    public string raceRouteName = "ROUTE";
    public float raceLengthKm = 1.2f;
    public int raceRewardCash = 1000;
    public Sprite racePreviewSprite;

    [Header("Save IDs")]
    public string raceRewardId = "";
    public string raceBestTimeId = "";

    [Header("World Map Icon")]
    public Sprite worldMapIconSprite;
    public Color worldMapIconColor = Color.white;
    public MinimapWorldMarker minimapIconSource;
    public Transform worldMapMarkerPoint;
}