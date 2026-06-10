using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Splines;
using Unity.Mathematics;
public class CarRaceManager : MonoBehaviour
{
    public enum RaceMode
    {
        Circuit,
        Sprint,
        TimeChallenge,
        SpeedTrap,
        Elimination
    }

    public enum RacePhase
    {
        Idle,
        WaitingForStart,
        Transition,
        Countdown,
        Racing,
        Finished
    }

    public enum RaceFinishResult
    {
        Complete,
        Win,
        Lose
    }

    [Header("Race Event Definition")]
    public RaceEventDefinition raceEventDefinition;

    // =========================================================
    // EVENT DATA - ładowane z RaceEventDefinition, NIE z Inspectora
    // =========================================================

    private RaceMode raceMode = RaceMode.Circuit;

    private Transform raceStartPoint;
    private int totalLaps = 1;

    private RaceGateTrigger finishGate;
    private readonly List<RaceGateTrigger> splitGates = new();

    private readonly List<SpeedTrapTrigger> speedTraps = new();
    private GameObject speedTrapVisualRoot;

    private readonly List<RaceGateTrigger> timeChallengeGates = new();
    private float timeChallengeStartTime = 20f;

    private RaceRoute raceRoute;
    private RaceRouteArrowGenerator routeArrowGenerator;
    private RaceRouteArrowGenerator activeRouteArrowGenerator;

    private string raceRouteName = "ROUTE";
    private float raceLengthKm = 1f;
    private int raceRewardCash = 0;
    private Sprite racePreviewSprite;

    private string raceRewardId = "";
    private string raceBestTimeId = "";

    // =========================================================
    // SYSTEM / GLOBAL SETTINGS
    // =========================================================

    [Header("Pre-Race Setup")]
    public bool alignToStartPointRotation = true;

    [Header("State")]
    public RacePhase racePhase = RacePhase.Idle;
    public bool raceActive;
    public bool raceFinished;

    [Header("Race")]
    public float gateCooldown = 0.1f;

    [Header("Input")]
    public KeyCode cancelRaceKey = KeyCode.Escape;
    public KeyCode respawnRaceKey = KeyCode.R;

    // =========================================================
    // RESPAWN
    // =========================================================

    [Header("Race Respawn")]
    public float respawnInvincibleDuration = 3f;
    public float respawnForwardOffset = 2f;
    public float respawnUpOffset = 0.5f;
    public LayerMask respawnIgnoreCollisionMask;
    public float respawnIgnoreCollisionRadius = 8f;

    private Transform lastRaceRespawnPoint;
    private Coroutine respawnProtectionRoutine;

    [Header("Dynamic Respawn")]
    public float respawnSaveInterval = 0.5f;
    public float minSpeedToSave = 10f;
    public float maxRollAngle = 35f;

    private Vector3 lastSafePosition;
    private Quaternion lastSafeRotation;
    private float respawnSaveTimer;

    [Header("Spline Respawn")]
    public bool useSplineRespawn = true;
    public float respawnBackDistanceOnSpline = 12f;

    [Header("Spline Respawn Ground Snap")]
    public LayerMask respawnGroundMask = ~0;
    public float respawnRaycastUp = 20f;
    public float respawnRaycastDown = 60f;
    public float respawnGroundOffset = 0.7f;

    // =========================================================
    // UI
    // =========================================================

    [Header("Speed Trap UI")]
    public GameObject speedTrapSummaryRoot;
    public TextMeshProUGUI speedTrapSummaryText;

    [Header("Minimap SpeedTrap Icons")]
    [SerializeField] private MinimapSpeedTrapIconManager minimapSpeedTrapIconManager;

    [Header("SpeedTrap Finish Drain")]
    public int speedTrapFinishPenaltyPerSecond = 10;
    private float speedTrapDrainAccumulator = 0f;

    [Header("UI Race")]
    public GameObject raceRoot;
    public TextMeshProUGUI timerValueText;
    public TextMeshProUGUI lapValueText;
    public TextMeshProUGUI lapRecordText;
    public TextMeshProUGUI cancelHintText;

    [Header("UI Panels")]
    public GameObject speedometerHudRoot;

    [Header("Sprint UI")]
    public GameObject sprintProgressRoot;
    public TextMeshProUGUI sprintProgressText;
    public Slider sprintProgressSlider;

    [Header("Mode UI Roots")]
    public GameObject timeRoot;
    public GameObject lapRoot;
    public GameObject lapTimesRoot;
    public GameObject cancelRaceRoot;

    [Header("Player UI To Hide")]
    public GameObject playerGuiRoot;
    public GameObject gunUiRoot;

    [Header("Lap Times List")]
    public Transform lapTimesContainer;
    public GameObject lapTimeEntryPrefab;

    [Header("Delta Colors")]
    public Color betterLapColor = Color.green;
    public Color worseLapColor = Color.red;
    public Color neutralColor = Color.white;

    [Header("Center Text Animation")]
    public float lapDeltaFadeIn = 0.15f;
    public float lapDeltaHold = 2.3f;
    public float lapDeltaFadeOut = 0.45f;

    [Header("Enter Race UI")]
    public GameObject enterRaceRoot;
    public TextMeshProUGUI enterRaceText;

    [Header("Race Pause Panel")]
    public GameObject racePauseRoot;

    private bool racePauseOpen = false;
    private float _previousTimeScale = 1f;

    // =========================================================
    // PROGRESS / ROUTE CACHE
    // =========================================================

    [Header("Route Progress")]
    [Range(20, 400)] public int splineSamples = 150;
    [Range(5, 80)] public int splineSearchWindow = 20;
    public bool routeSplinePointsAreWorld = true;

    private readonly List<Vector3> sprintSplinePoints = new();
    private readonly List<float> sprintSplineDistances = new();
    private readonly List<float> sprintSampleTs = new();

    private float sprintSplineTotalLength = 0f;
    private float sprintProgress01 = 0f;

    private int speedTrapTotalKmh = 0;
    private int speedTrapPassedCount = 0;

    [Header("Race World Visibility")]
    public GameObject raceVisualRoot;
    public List<GameObject> objectsToShowOnlyForThisRace = new();
    public List<GameObject> objectsToHideDuringThisRace = new();

    private static readonly List<CarRaceManager> allRaceManagers = new();

    private float routeProgressStartDistance = 0f;
    private float routeProgressEndDistance = 0f;
    private float routeProgressLength = 0f;

    [Header("Minimap Dynamic Route")]
    [SerializeField] private MinimapDynamicRaceRoute minimapDynamicRoute;

    // =========================================================
    // TRANSITION / COUNTDOWN / LOADING
    // =========================================================

    [Header("Transition UI")]
    public CanvasGroup fadeCanvasGroup;
    public GameObject fadeRoot;
    public GameObject countdownRoot;
    public TextMeshProUGUI countdownText;

    [Header("Countdown")]
    public int countdownFrom = 5;
    public float fadeDuration = 0.35f;
    public float countdownStepDuration = 1f;

    private Vector3 countdownBaseScale;

    [Header("Loading Screen UI")]
    public GameObject raceLoadingRoot;
    public CanvasGroup raceLoadingCanvasGroup;
    public TextMeshProUGUI loadingText;
    public TextMeshProUGUI tipText;
    public Slider loadingSlider;
    public TextMeshProUGUI sliderPercentText;

    [Header("Loading Screen")]
    public float loadingDuration = 5f;
    public bool waitForEnterAfterLoading = true;
    public string loadingBaseText = "LOADING";
    public string continueText = "PRESS ENTER TO CONTINUE";

    // =========================================================
    // EVENT PANEL / REWARD / SAVE
    // =========================================================

    [Header("Race Event Panel")]
    public GameObject raceEventPanelRoot;
    public TextMeshProUGUI raceTypeValueText;
    public TextMeshProUGUI raceLengthValueText;
    public TextMeshProUGUI raceRewardValueText;
    public TextMeshProUGUI raceRouteValueText;
    public TextMeshProUGUI raceBestTimeValueText;
    public Image racePreviewImage;

    [Header("One Time Reward")]
    public bool rewardOnlyOnce = true;
    public GameObject raceRewardRoot;
    public GameObject finishRewardRoot;
    public string claimedRewardText = "-";
    public string defaultBestTime = "00:00:00";

    private bool newRecordThisRace = false;

    // =========================================================
    // FINISH
    // =========================================================

    [Header("Finish Panel")]
    public GameObject raceFinalPanelRoot;
    public TextMeshProUGUI finishHeaderText;
    public TextMeshProUGUI finishPlaceText;
    public TextMeshProUGUI finishTimeValueText;
    public TextMeshProUGUI newRecordText;
    public TextMeshProUGUI finishRewardValueText;
    public GameObject finishPlaceRoot;

    [Header("Finish Cinematic")]
    public bool useFinishCinematic = true;
    public Camera finishCinematicCamera;
    public float finishCinematicDuration = 2f;
    [Range(0.05f, 1f)] public float finishSlowMotionScale = 0.35f;

    [Header("Finish Camera Presets")]
    public string finishCameraRootName = "FinishRaceCams";
    public float finishCameraPositionLerp = 8f;
    public float finishCameraRotationLerp = 8f;

    private Camera mainGameplayCamera;
    private bool finishCameraActive = false;
    private Transform currentFinishCameraPoint;
    private readonly List<Transform> cachedFinishCameraPoints = new();

    [Header("UI To Hide During Finish")]
    public GameObject minimapRoot;

    // =========================================================
    // TIME CHALLENGE SETTINGS
    // =========================================================

    [Header("Time Challenge")]
    public float timeChallengeBonusTime = 8f;
    public TextMeshProUGUI timeChallengeGateText;

    [Header("Time Challenge - Balance")]
    public bool firstTimeChallengeGateIsStart = true;
    public float secondsPer100Meters = 4.5f;
    public float minGateTime = 6f;
    public float maxGateTime = 25f;
    public float remainingTimeBonusMultiplier = 1f;

    [Header("Time Challenge - Warning")]
    public float hurryUpTime = 20f;
    public float nextBoothTextDuration = 3f;

    private int currentTimeChallengeGateIndex = 0;
    private float timeChallengeTimeLeft = 0f;
    private Coroutine nextBoothRoutine;

    // =========================================================
    // AI / STANDINGS
    // =========================================================

    [Header("Minimap AI Icons")]
    [SerializeField] private MinimapAIIconManager minimapAIIconManager;

    [Header("AI Opponents")]
    public bool useAIInRace = false;
    public GameObject[] aiCarPrefabs;
    [Min(0)] public int aiCount = 1;
    public Transform[] aiStartPoints;

    private readonly List<CarControll> activeAICars = new();
    private readonly List<CarControll> finishOrder = new();

    [Header("Race Standings UI")]
    public GameObject standingsRoot;
    public Transform standingsContainer;
    public GameObject standingsEntryPrefab;
    public string playerDriverName = "PLAYER";

    [Header("Standings Distance Flash")]
    public bool standingsShowDistanceFlash = true;
    public float standingsDistanceInterval = 15f;
    public float standingsDistanceDuration = 3f;

    private float standingsDistanceTimer = 0f;
    private bool standingsShowingDistance = false;

    private readonly List<StandingsEntryUI> standingsEntries = new();
    private readonly List<RacerRuntimeInfo> racers = new();

    // =========================================================
    // RUNTIME CAR / RACE STATE
    // =========================================================

    public GameObject raceStartVisual;
    public bool IsRaceRunning => raceActive && !raceFinished;
    public bool IsRacePaused => racePauseOpen;
    public bool IsRaceEventPanelOpen => raceEventPanelOpen;

    public CarControll ActiveCar => activeCar;

    private CarControll activeCar;
    private VehicleDestructible activeCarDestructible;
    private NitroSystem activeNitro;

    private bool preRaceLock;
    private Coroutine preRaceRoutine;

    private float raceStartTime;
    private float lapStartTime;
    private float lastGateTime = -999f;
    private float finishTime;

    private int currentLap = 0;
    private int currentSplitIndex = 0;
    private float lastSectorStartTime = 0f;

    private readonly List<float> lapTimes = new();
    private readonly List<GameObject> spawnedLapEntries = new();
    private readonly List<float> bestSplitTimes = new();

    private float bestLapTime = -1f;
    private float previousLapTime = -1f;
    private int eliminationLapToResolve = 1;

    private Coroutine lapRecordRoutine;
    private Coroutine finishRoutine;
    private RaceFinishResult pendingFinishResult = RaceFinishResult.Complete;

    private bool raceEventPanelOpen = false;
    private bool blockRaceEventSubmitUntilRelease = false;

    public static CarRaceManager ActiveRaceManager;
    public static CarRaceManager CurrentPanelManager;
    public static bool IsRaceStarting = false;
    public static bool IsRaceLoading = false;

    void Start()
    {
        ApplyRaceEventDefinition();

        if (raceEventPanelRoot != null)
            raceEventPanelRoot.SetActive(false);

        if (raceLoadingRoot != null)
            raceLoadingRoot.SetActive(false);

        if (raceLoadingCanvasGroup != null)
            raceLoadingCanvasGroup.alpha = 1f;

        if (sliderPercentText != null)
            sliderPercentText.text = "0%";

        if (loadingSlider != null)
            loadingSlider.value = 0f;

        if (raceRoot != null)
            raceRoot.SetActive(false);

        if (fadeRoot != null)
            fadeRoot.SetActive(false);

        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        if (countdownText != null)
            countdownBaseScale = countdownText.transform.localScale;

        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 0f;

        if (raceFinalPanelRoot != null)
            raceFinalPanelRoot.SetActive(false);

        if (racePauseRoot != null)
            racePauseRoot.SetActive(false);

        if (finishGate != null)
            finishGate.raceManager = this;

        for (int i = 0; i < splitGates.Count; i++)
        {
            if (splitGates[i] != null)
                splitGates[i].raceManager = this;
        }

        for (int i = 0; i < timeChallengeGates.Count; i++)
        {
            if (timeChallengeGates[i] != null)
                timeChallengeGates[i].raceManager = this;
        }

        for (int i = 0; i < speedTraps.Count; i++)
        {
            if (speedTraps[i] != null)
                speedTraps[i].raceManager = this;
        }

        if (speedTrapVisualRoot != null)
            speedTrapVisualRoot.SetActive(false);

        mainGameplayCamera = Camera.main;

        if (finishCinematicCamera != null)
            finishCinematicCamera.enabled = false;

        // PlayerPrefs.DeleteKey(RewardSaveKey);
        // PlayerPrefs.DeleteKey(BestTimeSaveKey);
        // PlayerPrefs.Save();
        EnsureBestSplitListSize();
        ClearLapEntries();
        RefreshIdleUi();
        RefreshModeUi();
        UpdateSprintProgressUI(0f);
    }

    void Update()
    {
        if (CurrentPanelManager != null && CurrentPanelManager != this && raceEventPanelOpen)
            return;

        if (raceEventPanelOpen)
        {
            if (blockRaceEventSubmitUntilRelease)
            {
                if (!Input.GetKey(KeyCode.Return))
                    blockRaceEventSubmitUntilRelease = false;

                if (Input.GetKeyDown(KeyCode.Escape))
                    CancelRaceEventPanel();

                return;
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                ConfirmRaceStart();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelRaceEventPanel();
                return;
            }

            return;
        }

        if (racePhase == RacePhase.Racing && raceActive && !raceFinished)
        {
            UpdateStandings();

            if (raceMode == RaceMode.TimeChallenge)
            {
                timeChallengeTimeLeft -= Time.deltaTime;

                if (timeChallengeTimeLeft <= 0f)
                {
                    timeChallengeTimeLeft = 0f;

                    if (timerValueText != null)
                        timerValueText.text = FormatCountdownTime(timeChallengeTimeLeft);

                    FailTimeChallengeRace();
                    return;
                }

                if (timerValueText != null)
                    timerValueText.text = FormatCountdownTime(timeChallengeTimeLeft);

                UpdateTimeChallengeWarningText();
            }
            else
            {
                float elapsed = Time.time - raceStartTime;

                if (timerValueText != null)
                    timerValueText.text = FormatRaceTime(elapsed);

                if (raceMode == RaceMode.SpeedTrap)
                    UpdateSpeedTrapFinishDrain();
            }

            if (cancelHintText != null && cancelRaceRoot == null)
            {
                cancelHintText.gameObject.SetActive(true);
                cancelHintText.text = $"PRESS \"{cancelRaceKey.ToString().ToUpper()}\" FOR CANCEL RACE";
            }

            if ((raceMode == RaceMode.Sprint || raceMode == RaceMode.TimeChallenge || raceMode == RaceMode.SpeedTrap) && activeCar != null)
                UpdateSprintProgressFromSpline();

            UpdateDynamicRespawnPoint();
        }

        if (racePhase == RacePhase.Finished && raceFinalPanelRoot != null && raceFinalPanelRoot.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                OnFinishRestart();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                OnFinishContinue();
                return;
            }
        }

        if (racePauseOpen)
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                OnPauseRestartRace();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                OnPauseLeaveRace();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseRacePause();
                return;
            }

            return;
        }

        if (Input.GetKeyDown(respawnRaceKey))
        {
            if (racePhase == RacePhase.Racing && raceActive && !raceFinished)
            {
                RespawnActiveCarInRace();
                return;
            }
        }

        if (Input.GetKeyDown(cancelRaceKey))
        {
            // jeśli wyścig jest w toku LUB countdown → pauza
            if ((racePhase == RacePhase.Racing || racePhase == RacePhase.Countdown) && raceActive && !raceFinished)
            {
                OpenRacePause();
                return;
            }

            // tylko w TRANSITION (loading/fade) można anulować
            if (racePhase == RacePhase.Transition)
            {
                CancelRace();
                return;
            }
        }
    }

    void LateUpdate()
    {
        if (!finishCameraActive || finishCinematicCamera == null || activeCar == null)
            return;

        if (currentFinishCameraPoint == null)
            return;

        finishCinematicCamera.transform.position = Vector3.Lerp(
            finishCinematicCamera.transform.position,
            currentFinishCameraPoint.position,
            Time.unscaledDeltaTime * finishCameraPositionLerp
        );

        finishCinematicCamera.transform.rotation = Quaternion.Slerp(
            finishCinematicCamera.transform.rotation,
            currentFinishCameraPoint.rotation,
            Time.unscaledDeltaTime * finishCameraRotationLerp
        );
    }

    public void OnCarEnteredGate(RaceGateTrigger gate, CarControll car)
    {
        if (gate == null || car == null)
            return;

        if (racePhase != RacePhase.Racing || raceFinished)
            return;

        if (Time.time - lastGateTime < gateCooldown && car == activeCar)
            return;

        bool isPlayerCar = car == activeCar;
        bool isAICar = activeAICars.Contains(car);

        if (!isPlayerCar && !isAICar)
            return;

        if (isAICar)
        {
            HandleAIGate(gate, car);
            return;
        }

        lastGateTime = Time.time;

        switch (raceMode)
        {
            case RaceMode.Circuit:
                HandleCircuitGate(gate);
                break;

            case RaceMode.Sprint:
                if (gate == finishGate)
                    FinishSprintRace();
                break;

            case RaceMode.SpeedTrap:
                if (gate == finishGate)
                    FinishSpeedTrapRace();
                break;

            case RaceMode.TimeChallenge:
                HandleTimeChallengeGate(gate);
                break;

            case RaceMode.Elimination:
                HandleCircuitGate(gate);
                break;
        }
    }

    void HandleCircuitGate(RaceGateTrigger gate)
    {
        int splitIndex = splitGates.IndexOf(gate);

        if (splitIndex >= 0)
        {
            HandleSplit(splitIndex);
            return;
        }

        if (gate == finishGate)
        {
            if (currentSplitIndex < splitGates.Count)
                return;

            CompleteLap();
        }
    }

    public void StartRace(CarControll car)
    {
        BeginRaceSequence(car);
    }

    void HandleSplit(int splitIndex)
    {
        if (!IsCircuitLikeRace())
            return;

        if (splitIndex != currentSplitIndex)
            return;

        float splitTime = Time.time - lastSectorStartTime;
        lastSectorStartTime = Time.time;

        float previousBest = bestSplitTimes[splitIndex];
        bool hasPrevious = previousBest > 0f;

        float diff = hasPrevious ? splitTime - previousBest : 0f;
        bool isBest = !hasPrevious || splitTime < previousBest;

        if (isBest)
            bestSplitTimes[splitIndex] = splitTime;

        ShowSplitDelta(splitIndex + 1, splitTime, diff, hasPrevious, isBest);

        currentSplitIndex++;
        lastRaceRespawnPoint = splitGates[splitIndex].transform;

        RacerRuntimeInfo player = racers.Find(r => r.car == activeCar);
        if (player != null)
        {
            player.currentLap = currentLap;
            player.currentSplitIndex = currentSplitIndex;
        }
    }

    bool IsCircuitLikeRace()
    {
        return raceMode == RaceMode.Circuit || raceMode == RaceMode.Elimination;
    }
    void CompleteLap()
    {
        if (!IsCircuitLikeRace())
            return;

        float lapTime = Time.time - lapStartTime;
        lapTimes.Add(lapTime);

        SpawnLapEntry(currentLap, lapTime);

        bool isBest = bestLapTime < 0f || lapTime < bestLapTime;
        float compareBase = previousLapTime > 0f ? previousLapTime : bestLapTime;
        float diff = (compareBase > 0f) ? lapTime - compareBase : 0f;

        if (isBest)
            bestLapTime = lapTime;

        ShowLapDelta(currentLap, lapTime, diff, isBest);

        previousLapTime = lapTime;

        RacerRuntimeInfo playerRacer = racers.Find(r => r.car == activeCar);

        // WAŻNE:
        // Gracz właśnie ukończył okrążenie, więc do sortowania/eliminacji
        // musi być już traktowany jako zawodnik na następnym lapie.
        if (playerRacer != null)
        {
            playerRacer.currentLap = currentLap + 1;
            playerRacer.currentSplitIndex = 0;
        }

        if (raceMode == RaceMode.Elimination)
        {
            TryResolveEliminationLap();

            RacerRuntimeInfo player = racers.Find(r => r.car == activeCar);

            if (player != null && player.eliminated)
            {
                FinishEliminationLose();
                return;
            }

            int aliveCount = racers.FindAll(r => !r.eliminated).Count;

            if (aliveCount <= 1)
            {
                FinishEliminationWin();
                return;
            }

            if (currentLap >= totalLaps)
            {
                int playerPlace = GetPlayerPlace();

                if (playerPlace == 1)
                    FinishEliminationWin();
                else
                    FinishEliminationLose();

                return;
            }
        }

        else
        {
            if (currentLap >= totalLaps)
            {
                FinishCircuitRace();
                return;
            }
        }

        currentLap++;
        currentSplitIndex = 0;

        if (playerRacer != null)
        {
            playerRacer.currentLap = currentLap;
            playerRacer.currentSplitIndex = currentSplitIndex;
        }

        lapStartTime = Time.time;
        lastSectorStartTime = Time.time;

        RefreshLapUi();
    }

    void TryResolveEliminationLap()
    {
        if (raceMode != RaceMode.Elimination)
            return;

        int aliveCount = 0;

        for (int i = 0; i < racers.Count; i++)
        {
            RacerRuntimeInfo r = racers[i];

            if (r == null || r.eliminated || r.car == null)
                continue;

            aliveCount++;

            // czekamy aż KAŻDY aktywny zawodnik przejedzie metę danego okrążenia
            if (r.currentLap <= eliminationLapToResolve)
                return;
        }

        if (aliveCount <= 1)
            return;

        EliminateLastRacerAfterLap();

        eliminationLapToResolve++;
    }

    void EliminateLastRacerAfterLap()
    {
        RacerRuntimeInfo last = null;
        float worstProgress = float.MaxValue;

        for (int i = 0; i < racers.Count; i++)
        {
            RacerRuntimeInfo r = racers[i];

            if (r == null || r.car == null || r.eliminated)
                continue;

            float progress = GetRaceProgressMeters(r.car);

            if (progress < worstProgress)
            {
                worstProgress = progress;
                last = r;
            }
        }

        if (last == null)
            return;

        last.eliminated = true;

        if (last.car != null)
        {
            AICarController ai = last.car.GetComponent<AICarController>();
            if (ai != null)
                ai.enabled = false;

            last.car.SetExternalInput(0f, 0f);
            last.car.isControlled = false;

            Rigidbody rb = last.car.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity *= 0.2f;
                rb.angularVelocity *= 0.2f;
            }
        }

        ShowCenterText($"{last.driverName}\nELIMINATED", worseLapColor);
        UpdateStandings();
    }

    void FinishEliminationLose()
    {
        racePhase = RacePhase.Finished;
        raceFinished = true;
        finishTime = Time.time - raceStartTime;

        if (timerValueText != null)
            timerValueText.text = FormatRaceTime(finishTime);

        if (cancelHintText != null)
            cancelHintText.gameObject.SetActive(false);

        RefreshModeUi();

        pendingFinishResult = RaceFinishResult.Lose;

        if (finishRoutine != null)
            StopCoroutine(finishRoutine);

        finishRoutine = StartCoroutine(FinishSequenceWithPlace(GetPlayerPlace()));
    }

    void FinishEliminationWin()
    {
        racePhase = RacePhase.Finished;
        raceFinished = true;
        finishTime = Time.time - raceStartTime;
        newRecordThisRace = SaveBestTimeIfBetter(finishTime);

        if (timerValueText != null)
            timerValueText.text = FormatRaceTime(finishTime);

        if (cancelHintText != null)
            cancelHintText.gameObject.SetActive(false);

        RefreshModeUi();

        pendingFinishResult = RaceFinishResult.Win;

        if (finishRoutine != null)
            StopCoroutine(finishRoutine);

        finishRoutine = StartCoroutine(FinishSequenceWithPlace(1));
    }

    int GetPlayerPlace()
    {
        UpdateStandings();

        for (int i = 0; i < racers.Count; i++)
        {
            if (racers[i].car == activeCar)
                return i + 1;
        }

        return racers.Count;
    }
    void FinishCircuitRace()
    {
        if (!finishOrder.Contains(activeCar))
            finishOrder.Add(activeCar);

        int playerPlace = finishOrder.IndexOf(activeCar) + 1;
        bool playerWon = playerPlace == 1;

        racePhase = RacePhase.Finished;
        raceFinished = true;
        finishTime = Time.time - raceStartTime;
        newRecordThisRace = SaveBestTimeIfBetter(finishTime);

        if (timerValueText != null)
            timerValueText.text = FormatRaceTime(finishTime);

        if (cancelHintText != null)
            cancelHintText.gameObject.SetActive(false);

        RefreshModeUi();

        RacerRuntimeInfo player = racers.Find(r => r.car == activeCar);
        if (player != null)
            player.finishedPlace = playerPlace;

        if (finishRoutine != null)
            StopCoroutine(finishRoutine);

        pendingFinishResult = playerWon ? RaceFinishResult.Win : RaceFinishResult.Lose;
        finishRoutine = StartCoroutine(FinishSequenceWithPlace(playerPlace));
    }

    IEnumerator FinishSequenceWithPlace(int place)
    {
        HideCurrentRaceArrows();

        if (minimapDynamicRoute != null)
            minimapDynamicRoute.HideRoute();

        if (minimapSpeedTrapIconManager != null)
            minimapSpeedTrapIconManager.ClearIcons();

        if (speedTrapVisualRoot != null)
            speedTrapVisualRoot.SetActive(false);

        if (minimapAIIconManager != null)
            minimapAIIconManager.ClearIcons();

        if (useFinishCinematic && finishCinematicCamera != null)
        {
            StartFinishCinematic();
            yield return new WaitForSecondsRealtime(finishCinematicDuration);
        }

        ShowFinishPanel(pendingFinishResult, place);
    }

    void FinishSprintRace()
    {
        racePhase = RacePhase.Finished;
        raceFinished = true;
        finishTime = Time.time - raceStartTime;
        newRecordThisRace = SaveBestTimeIfBetter(finishTime);

        sprintProgress01 = 1f;
        UpdateSprintProgressUI(1f);

        if (timerValueText != null)
            timerValueText.text = FormatRaceTime(finishTime);

        if (cancelHintText != null)
            cancelHintText.gameObject.SetActive(false);

        RefreshModeUi();

        if (finishRoutine != null)
            StopCoroutine(finishRoutine);

        pendingFinishResult = RaceFinishResult.Complete;
        finishRoutine = StartCoroutine(FinishSequence());
    }

    IEnumerator FinishSequence()
    {
        HideCurrentRaceArrows();

        if (minimapDynamicRoute != null)
            minimapDynamicRoute.HideRoute();

        if (minimapSpeedTrapIconManager != null)
            minimapSpeedTrapIconManager.ClearIcons();

        if (speedTrapVisualRoot != null)
            speedTrapVisualRoot.SetActive(false);

        if (minimapAIIconManager != null)
            minimapAIIconManager.ClearIcons();

        if (useFinishCinematic && finishCinematicCamera != null)
        {
            StartFinishCinematic();
            yield return new WaitForSecondsRealtime(finishCinematicDuration);
        }

        ShowFinishPanel(pendingFinishResult);
    }

    public void CancelRace()
    {
        IsRaceStarting = false;
        IsRaceLoading = false;

        ResetRace();
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeRoot != null) fadeRoot.SetActive(true);
        if (fadeCanvasGroup == null) yield break;

        float t = 0f;
        fadeCanvasGroup.alpha = from;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, k);

            yield return null;
        }

        fadeCanvasGroup.alpha = to;

        if (Mathf.Approximately(to, 0f) && fadeRoot != null)
            fadeRoot.SetActive(false);
    }

    public void ResetRace()
    {
        ClearGlobalRaceState();

        StopRaceCoroutines();
        ResetTransitionUi();
        ClearAICars();
        ClearStandingsUI();

        racers.Clear();

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        racePauseOpen = false;
        finishCameraActive = false;
        currentFinishCameraPoint = null;
        cachedFinishCameraPoints.Clear();

        newRecordThisRace = false;

        HideCurrentRaceArrows();

        if (newRecordText != null)
            newRecordText.gameObject.SetActive(false);

        if (racePauseRoot != null)
            racePauseRoot.SetActive(false);

        if (finishCinematicCamera != null)
            finishCinematicCamera.enabled = false;

        if (mainGameplayCamera != null)
            mainGameplayCamera.enabled = true;

        if (minimapSpeedTrapIconManager != null)
            minimapSpeedTrapIconManager.ClearIcons();

        if (activeCar != null)
            activeCar.isControlled = true;

        if (minimapRoot != null)
            minimapRoot.SetActive(true);

        if (standingsRoot != null)
            standingsRoot.SetActive(false);

        if (minimapDynamicRoute != null)
            minimapDynamicRoute.HideRoute();

        racePhase = RacePhase.Idle;
        preRaceLock = false;
        SetCarPreRaceLocked(false);
        blockRaceEventSubmitUntilRelease = false;
        raceEventPanelOpen = false;

        if (raceEventPanelRoot != null)
            raceEventPanelRoot.SetActive(false);

        if (raceFinalPanelRoot != null)
            raceFinalPanelRoot.SetActive(false);

        if (speedometerHudRoot != null && activeCar != null && activeCar.isControlled)
            speedometerHudRoot.SetActive(true);

        if (RaceEventUIController.ActiveFinishRaceManager == this)
            RaceEventUIController.ActiveFinishRaceManager = null;

        if (activeCarDestructible != null)
            activeCarDestructible.isInvincible = false;

        for (int i = 0; i < speedTraps.Count; i++)
        {
            if (speedTraps[i] != null)
                speedTraps[i].ResetTrap();
        }

        speedTrapTotalKmh = 0;
        speedTrapPassedCount = 0;
        activeCarDestructible = null;
        activeNitro = null;
        activeCar = null;

        raceActive = false;
        raceFinished = false;

        if (raceStartVisual != null)
            raceStartVisual.SetActive(true);

        ShowEnterRaceUI(false);
        SetRaceWorldState(false);

        raceStartTime = 0f;
        lapStartTime = 0f;
        finishTime = 0f;
        lastGateTime = -999f;
        lastSectorStartTime = 0f;

        currentLap = 0;
        currentSplitIndex = 0;

        lapTimes.Clear();
        ClearLapEntries();

        bestLapTime = -1f;
        previousLapTime = -1f;
        EnsureBestSplitListSize();

        sprintSplinePoints.Clear();
        sprintSplineDistances.Clear();
        sprintSampleTs.Clear();
        sprintSplineTotalLength = 0f;
        sprintProgress01 = 0f;

        speedTrapTotalKmh = 0;
        speedTrapPassedCount = 0;

        if (speedTrapSummaryText != null)
            speedTrapSummaryText.text = "0 KM/H";

        if (speedTrapVisualRoot != null)
            speedTrapVisualRoot.SetActive(false);

        if (raceRoot != null)
            raceRoot.SetActive(false);

        if (playerGuiRoot != null)
            playerGuiRoot.SetActive(true);

        if (gunUiRoot != null)
            gunUiRoot.SetActive(true);

        RefreshIdleUi();
        RefreshModeUi();
        UpdateSprintProgressUI(0f);
    }

    void RefreshLapUi()
    {
        if (raceMode == RaceMode.TimeChallenge)
        {
            if (lapValueText != null)
                lapValueText.text = "";
            return;
        }

        if (raceMode == RaceMode.Sprint)
        {
            if (lapValueText != null)
                lapValueText.text = "";
            return;
        }

        if (lapValueText != null)
            lapValueText.text = $"{currentLap}/{totalLaps}";
    }

    void RefreshIdleUi()
    {
        if (timerValueText != null)
            timerValueText.text = "00:00:00";

        if (lapValueText != null)
        {
            if (raceMode == RaceMode.Sprint)
                lapValueText.text = "";
            else
                lapValueText.text = $"0/{totalLaps}";
        }

        if (lapRecordText != null)
        {
            lapRecordText.text = "";
            lapRecordText.color = new Color(neutralColor.r, neutralColor.g, neutralColor.b, 0f);
        }

        if (cancelHintText != null)
            cancelHintText.gameObject.SetActive(false);
    }

    void RefreshModeUi()
    {
        bool sprintLike = raceMode == RaceMode.Sprint || raceMode == RaceMode.TimeChallenge || raceMode == RaceMode.SpeedTrap;
        bool showRaceHud = raceActive || raceFinished || racePhase == RacePhase.Countdown;

        if (timeRoot != null)
            timeRoot.SetActive(showRaceHud);

        if (cancelRaceRoot != null)
            cancelRaceRoot.SetActive(raceActive && !raceFinished);

        if (lapRoot != null)
            lapRoot.SetActive(!sprintLike && showRaceHud);

        if (lapTimesRoot != null)
            lapTimesRoot.SetActive(!sprintLike && showRaceHud);

        if (sprintProgressRoot != null)
            sprintProgressRoot.SetActive(sprintLike && showRaceHud);

        if (speedTrapSummaryRoot != null)
            speedTrapSummaryRoot.SetActive(raceMode == RaceMode.SpeedTrap && showRaceHud);

        if (!showRaceHud)
        {
            if (timeRoot != null) timeRoot.SetActive(false);
            if (cancelRaceRoot != null) cancelRaceRoot.SetActive(false);
            if (lapRoot != null) lapRoot.SetActive(false);
            if (lapTimesRoot != null) lapTimesRoot.SetActive(false);
            if (sprintProgressRoot != null) sprintProgressRoot.SetActive(false);
            if (speedTrapSummaryRoot != null) speedTrapSummaryRoot.SetActive(false);
        }
    }

    void EnsureBestSplitListSize()
    {
        bestSplitTimes.Clear();

        for (int i = 0; i < splitGates.Count; i++)
            bestSplitTimes.Add(-1f);
    }

    void ClearLapEntries()
    {
        for (int i = 0; i < spawnedLapEntries.Count; i++)
        {
            if (spawnedLapEntries[i] != null)
                Destroy(spawnedLapEntries[i]);
        }

        spawnedLapEntries.Clear();
    }

    void SpawnLapEntry(int lapIndex, float lapTime)
    {
        if (lapTimesContainer == null || lapTimeEntryPrefab == null)
            return;

        GameObject entry = Instantiate(lapTimeEntryPrefab, lapTimesContainer);
        spawnedLapEntries.Add(entry);

        TextMeshProUGUI text = entry.GetComponentInChildren<TextMeshProUGUI>(true);

        if (text != null)
            text.text = $"LAP {lapIndex}     {FormatRaceTime(lapTime)}";
    }

    void ShowSplitDelta(int splitNumber, float splitTime, float diff, bool hasPrevious, bool isBest)
    {
        if (!hasPrevious)
        {
            ShowCenterText(
                $"SPLIT\n{FormatRaceTimeCompact(splitTime)}",
                neutralColor
            );
            return;
        }

        Color color = Mathf.Approximately(diff, 0f)
            ? neutralColor
            : diff < 0f ? betterLapColor : worseLapColor;

        ShowCenterText(
            $"SPLIT\n{FormatSignedDelta(diff)}",
            color
        );
    }

    void ShowLapDelta(int lapNumber, float lapTime, float diff, bool isBest)
    {
        string text = $"LAP TIME\n{FormatRaceTime(lapTime)}";
        Color color = neutralColor;

        if (isBest)
        {
            text += "\nNEW BEST LAP";
            color = betterLapColor;
        }
        else if (!Mathf.Approximately(diff, 0f))
        {
            string sign = diff > 0f ? "+" : "";
            text += $"\n{FormatSignedDelta(diff)}";
            color = diff > 0f ? worseLapColor : betterLapColor;
        }

        ShowCenterText(text, color);
    }

    void ShowCenterText(string text, Color color)
    {
        if (lapRecordText == null)
            return;

        if (lapRecordRoutine != null)
            StopCoroutine(lapRecordRoutine);

        lapRecordRoutine = StartCoroutine(AnimateCenterText(text, color));
    }

    IEnumerator AnimateCenterText(string text, Color color)
    {
        lapRecordText.text = text;

        Color c = color;
        c.a = 0f;
        lapRecordText.color = c;

        float t = 0f;
        while (t < lapDeltaFadeIn)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, lapDeltaFadeIn));
            c.a = a;
            lapRecordText.color = c;
            yield return null;
        }

        c.a = 1f;
        lapRecordText.color = c;

        yield return new WaitForSeconds(lapDeltaHold);

        t = 0f;
        while (t < lapDeltaFadeOut)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / Mathf.Max(0.0001f, lapDeltaFadeOut));
            c.a = a;
            lapRecordText.color = c;
            yield return null;
        }

        c.a = 0f;
        lapRecordText.color = c;
    }

    void BuildSprintSplineCache()
    {
        sprintSplinePoints.Clear();
        sprintSplineDistances.Clear();
        sprintSampleTs.Clear();

        sprintSplineTotalLength = 0f;
        sprintProgress01 = 0f;

        if (raceRoute != null && raceRoute.Count > 0)
        {
            BuildRouteCache();
            ConfigureRouteProgressRange();
        }
    }

    void ConfigureRouteProgressRange()
    {
        routeProgressStartDistance = 0f;
        routeProgressEndDistance = sprintSplineTotalLength;
        routeProgressLength = sprintSplineTotalLength;

        if (sprintSplinePoints.Count < 2 || sprintSplineTotalLength <= 0.01f)
            return;

        // Circuit zostawiamy jako pełne okrążenie
        if (raceMode == RaceMode.Circuit)
            return;

        if (raceStartPoint != null)
            routeProgressStartDistance = GetDistanceOnCachedRoute(raceStartPoint.position);

        if (finishGate != null)
            routeProgressEndDistance = GetDistanceOnCachedRoute(finishGate.transform.position);

        routeProgressLength = routeProgressEndDistance - routeProgressStartDistance;

        if (routeProgressLength <= 1f)
        {
            routeProgressStartDistance = 0f;
            routeProgressEndDistance = sprintSplineTotalLength;
            routeProgressLength = sprintSplineTotalLength;
        }
    }

    float GetDistanceOnCachedRoute(Vector3 worldPos)
    {
        int index = FindNearestRoutePointIndex(worldPos);

        if (index < 0 || index >= sprintSplineDistances.Count)
            return 0f;

        return sprintSplineDistances[index];
    }

    void BuildRouteCache()
    {
        Vector3 prev = Vector3.zero;
        bool hasPrev = false;

        int samplesPerSegment = Mathf.Max(8, splineSamples / Mathf.Max(1, raceRoute.Count));

        for (int s = 0; s < raceRoute.segments.Count; s++)
        {
            List<Vector3> points = GetRouteSegmentPointsByIndex(s, samplesPerSegment);

            if (points.Count < 2)
                continue;

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];

                if (!hasPrev)
                {
                    sprintSplinePoints.Add(p);
                    sprintSplineDistances.Add(0f);
                    sprintSampleTs.Add(0f);

                    prev = p;
                    hasPrev = true;
                    continue;
                }

                if (Vector3.Distance(prev, p) < 0.05f)
                    continue;

                sprintSplineTotalLength += Vector3.Distance(prev, p);

                sprintSplinePoints.Add(p);
                sprintSplineDistances.Add(sprintSplineTotalLength);
                sprintSampleTs.Add(0f);

                prev = p;
            }
        }

        if (sprintSplineTotalLength > 1f)
            raceLengthKm = sprintSplineTotalLength / 1000f;
    }

    List<Vector3> GetRouteSegmentPointsByIndex(int index, int samples)
    {
        List<Vector3> result = new();

        RoadSegment segment = raceRoute.GetSegment(index);

        if (segment == null)
            return result;

        SplineContainer container = segment.GetComponent<SplineContainer>();

        if (container == null || container.Splines == null || container.Splines.Count == 0)
        {
            if (segment.startNode != null)
                result.Add(segment.startNode.transform.position);

            if (segment.endNode != null)
                result.Add(segment.endNode.transform.position);

            if (raceRoute.segments[index].reverse)
                result.Reverse();

            return result;
        }

        int splineIndex = Mathf.Clamp(segment.splineIndex, 0, container.Splines.Count - 1);
        Spline spline = container.Splines[splineIndex];

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;

            Vector3 localPos = spline.EvaluatePosition(t);
            Vector3 worldPos = container.transform.TransformPoint(localPos);

            result.Add(worldPos);
        }

        float distStartToFirst = segment.startNode != null
            ? Vector3.Distance(segment.startNode.transform.position, result[0])
            : 0f;

        float distStartToLast = segment.startNode != null
            ? Vector3.Distance(segment.startNode.transform.position, result[result.Count - 1])
            : 0f;

        if (segment.startNode != null && distStartToLast < distStartToFirst)
            result.Reverse();

        if (raceRoute.segments[index].reverse)
            result.Reverse();

        return result;
    }

    void UpdateSprintProgressFromSpline()
    {
        if (activeCar == null || sprintSplinePoints.Count < 2 || sprintSplineTotalLength <= 0.01f)
            return;

        float carDistance = GetDistanceOnCachedRoute(activeCar.transform.position);

        float progress;

        if (raceMode == RaceMode.Circuit)
        {
            progress = Mathf.Clamp01(carDistance / sprintSplineTotalLength);
        }
        else
        {
            progress = Mathf.InverseLerp(
                routeProgressStartDistance,
                routeProgressEndDistance,
                carDistance
            );
        }

        progress = Mathf.Clamp01(progress);

        if (progress < sprintProgress01)
            progress = sprintProgress01;

        if (!raceFinished)
            progress = Mathf.Min(progress, 0.99f);

        sprintProgress01 = progress;
        UpdateSprintProgressUI(sprintProgress01);
    }

    int FindNearestRoutePointIndex(Vector3 worldPos)
    {
        int bestIndex = 0;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < sprintSplinePoints.Count; i++)
        {
            float sqr = (worldPos - sprintSplinePoints[i]).sqrMagnitude;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    void UpdateSprintProgressUI(float progress01)
    {
        progress01 = Mathf.Clamp01(progress01);
        int percent = Mathf.RoundToInt(progress01 * 100f);

        if (sprintProgressText != null)
            sprintProgressText.text = percent + "%";

        if (sprintProgressSlider != null)
            sprintProgressSlider.value = progress01;
    }

    string FormatRaceTime(float timeSeconds)
    {
        int minutes = Mathf.FloorToInt(timeSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeSeconds % 60f);
        int centiseconds = Mathf.FloorToInt((timeSeconds % 1f) * 100f);
        centiseconds = Mathf.Clamp(centiseconds, 0, 99);

        return $"{minutes:00}:{seconds:00}:{centiseconds:00}";
    }


    public void ShowEnterRaceUI(bool show)
    {
        if (enterRaceRoot != null)
            enterRaceRoot.SetActive(show);

        if (show && enterRaceText != null)
            enterRaceText.text = "PRESS ENTER TO OPEN EVENT";
    }

    public void BeginRaceSequence(CarControll car)
    {
        if (car == null) return;
        if (racePhase != RacePhase.Idle) return;

        ActiveRaceManager = this;

        activeCar = car;
        activeCarDestructible = car.GetComponent<VehicleDestructible>();
        activeNitro = car.GetComponent<NitroSystem>();
        ResetHudForNewRace();

        if (activeCarDestructible != null)
            activeCarDestructible.isInvincible = true;

        if (preRaceRoutine != null)
            StopCoroutine(preRaceRoutine);

        preRaceRoutine = StartCoroutine(PreRaceSequence());
    }

    IEnumerator PreRaceSequence()
    {
        racePhase = RacePhase.Transition;
        preRaceLock = true;

        SetCarPreRaceLocked(true);

        yield return StartCoroutine(Fade(0f, 1f, fadeDuration));
        yield return StartCoroutine(ShowRaceLoadingScreen());

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        yield return StartCoroutine(Fade(1f, 0f, fadeDuration));

        racePhase = RacePhase.Countdown;

        if (raceMode == RaceMode.Circuit)
            currentLap = 1;
        else
            currentLap = 0;

        PrepareRaceHudForCountdown();

        if (countdownRoot != null)
            countdownRoot.SetActive(true);

        for (int i = countdownFrom; i >= 1; i--)
        {
            if (countdownText != null)
                countdownText.text = i.ToString();

            StartCoroutine(PulseCountdown());
            yield return new WaitForSeconds(countdownStepDuration);
        }

        if (countdownText != null)
            countdownText.text = "GO";

        StartCoroutine(PulseCountdown(0.35f, 1.6f));

        SetCarPreRaceLocked(false);
        StartRaceInternal();

        yield return new WaitForSeconds(0.6f);

        if (countdownRoot != null)
            countdownRoot.SetActive(false);
    }


    void StartRaceInternal()
    {
        IsRaceStarting = false;
        IsRaceLoading = false;

        raceActive = true;
        raceFinished = false;
        racePhase = RacePhase.Racing;

        lapTimes.Clear();
        ClearLapEntries();

        bestLapTime = -1f;
        previousLapTime = -1f;
        EnsureBestSplitListSize();

        currentLap = IsCircuitLikeRace() ? 1 : 0;
        currentSplitIndex = 0;
        eliminationLapToResolve = 1;

        raceStartTime = Time.time;
        lapStartTime = Time.time;
        lastSectorStartTime = Time.time;
        finishTime = 0f;
        lastGateTime = Time.time;

        lastRaceRespawnPoint = raceStartPoint;

        if (raceStartPoint != null)
        {
            lastSafePosition = raceStartPoint.position;
            lastSafeRotation = raceStartPoint.rotation;
        }

        respawnSaveTimer = 0f;

        if (raceRoot != null)
            raceRoot.SetActive(true);

        if (playerGuiRoot != null)
            playerGuiRoot.SetActive(false);

        if (gunUiRoot != null)
            gunUiRoot.SetActive(false);

        if (cancelHintText != null)
        {
            cancelHintText.gameObject.SetActive(true);
            cancelHintText.text = $"PRESS \"{cancelRaceKey.ToString().ToUpper()}\" FOR PAUSE EVENT";
        }

        if (timerValueText != null)
        {
            timerValueText.text = raceMode == RaceMode.TimeChallenge
                ? FormatCountdownTime(timeChallengeTimeLeft)
                : "00:00:00";
        }

        if (lapRecordText != null)
        {
            lapRecordText.text = "";
            lapRecordText.color = new Color(neutralColor.r, neutralColor.g, neutralColor.b, 0f);
        }

        sprintProgress01 = 0f;

        sprintSplinePoints.Clear();
        sprintSplineDistances.Clear();
        sprintSampleTs.Clear();
        sprintSplineTotalLength = 0f;

        if (raceRoute != null && raceRoute.Count > 0)
            BuildSprintSplineCache();

        if (raceMode == RaceMode.TimeChallenge)
        {
            if (timeChallengeTimeLeft <= 0f)
                timeChallengeTimeLeft = GetTimeToNextTimeChallengeGate();
        }

        if (raceMode == RaceMode.SpeedTrap)
        {
            speedTrapTotalKmh = 0;
            speedTrapPassedCount = 0;
            speedTrapDrainAccumulator = 0f;

            if (speedTrapSummaryText != null)
                speedTrapSummaryText.text = "0 KM/H";
        }

        UpdateSprintProgressUI(0f);
        RefreshLapUi();
        RefreshModeUi();
    }

    void TeleportCarToStart()
    {
        if (activeCar == null || raceStartPoint == null)
            return;

        activeCar.ResetRaceVisualState();

        Rigidbody rb = activeCar.GetComponent<Rigidbody>();

        Vector3 pos = raceStartPoint.position;
        Quaternion rot = alignToStartPointRotation ? raceStartPoint.rotation : activeCar.transform.rotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();

            rb.position = pos;
            rb.rotation = rot;
            activeCar.transform.SetPositionAndRotation(pos, rot);

            Physics.SyncTransforms();

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }
        else
        {
            activeCar.transform.SetPositionAndRotation(pos, rot);
            Physics.SyncTransforms();
        }

        activeCar.ResetRaceVisualState();

        lastSafePosition = pos;
        lastSafeRotation = rot;
    }

    void SetCarPreRaceLocked(bool locked)
    {
        preRaceLock = locked;

        if (activeCar != null)
            activeCar.raceStartLock = locked;

        if (activeNitro != null)
            activeNitro.nitroLocked = locked;

        for (int i = 0; i < activeAICars.Count; i++)
        {
            if (activeAICars[i] != null)
                activeAICars[i].raceStartLock = locked;
        }
    }

    IEnumerator PulseCountdown(float duration = 0.25f, float scaleMultiplier = 1.4f)
    {
        if (countdownText == null)
            yield break;

        float t = 0f;
        Vector3 start = countdownBaseScale * scaleMultiplier;
        Vector3 end = countdownBaseScale;

        countdownText.transform.localScale = start;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;
            countdownText.transform.localScale = Vector3.Lerp(start, end, k);
            yield return null;
        }

        countdownText.transform.localScale = end;
    }

    void RefreshRaceEventPanel()
    {
        string modeText = raceMode switch
        {
            RaceMode.Sprint => "SPRINT",
            RaceMode.Circuit => "CIRCUIT",
            RaceMode.Elimination => "ELIMINATION",
            RaceMode.TimeChallenge => "TIME\nCHALLENGE",
            RaceMode.SpeedTrap => "SPEED\nTRAP",
            _ => "RACE"
        };

        if (raceTypeValueText != null)
            raceTypeValueText.text = modeText;

        if (raceLengthValueText != null)
            raceLengthValueText.text = $"{raceLengthKm:0.00} KM";

        bool rewardClaimed = IsRewardClaimed();

        if (raceRewardRoot != null)
            raceRewardRoot.SetActive(!rewardClaimed);

        if (raceRewardValueText != null)
            raceRewardValueText.text = rewardClaimed ? claimedRewardText : $"{raceRewardCash}$";

        if (raceRouteValueText != null)
            raceRouteValueText.text = raceRouteName;

        if (raceBestTimeValueText != null)
            raceBestTimeValueText.text = GetBestTimeText();

        if (racePreviewImage != null)
        {
            racePreviewImage.gameObject.SetActive(true);

            if (racePreviewSprite != null)
                racePreviewImage.sprite = racePreviewSprite;
        }
    }

    public void ConfirmRaceStart()
    {
        Debug.Log($"CONFIRM PANEL: {gameObject.name} / {raceMode}");

        if (!raceEventPanelOpen) return;
        if (activeCar == null) return;

        IsRaceStarting = true;
        IsRaceLoading = true;

        raceEventPanelOpen = false;
        blockRaceEventSubmitUntilRelease = false;

        if (CurrentPanelManager == this)
            CurrentPanelManager = null;

        if (raceEventPanelRoot != null)
            raceEventPanelRoot.SetActive(false);

        racePhase = RacePhase.Idle;

        SetRaceMenuState(false);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        BeginRaceSequence(activeCar);
    }

    public void CancelRaceEventPanel()
    {
        if (!raceEventPanelOpen) return;

        raceEventPanelOpen = false;
        blockRaceEventSubmitUntilRelease = false;

        if (raceEventPanelRoot != null)
            raceEventPanelRoot.SetActive(false);

        racePhase = RacePhase.Idle;

        SetRaceMenuState(false);
        ShowEnterRaceUI(false);

        if (raceEventPanelRoot != null)
            raceEventPanelRoot.SetActive(false);

        if (ActiveRaceManager == this)
            ActiveRaceManager = null;

        if (CurrentPanelManager == this)
            CurrentPanelManager = null;
    }

    IEnumerator ShowRaceLoadingScreen()
    {
        SetRaceMenuState(true);
        IsRaceLoading = true;

        if (raceLoadingRoot != null)
            raceLoadingRoot.SetActive(true);

        SetRaceWorldState(true);

        if (raceRoute != null && raceRoute.Count > 0)
            BuildSprintSplineCache();

        if (minimapDynamicRoute != null)
            minimapDynamicRoute.ShowRaceRoute(raceRoute);

        PrepareSpeedTrapRaceWorld();
        ShowCurrentRaceArrows();
        ShowEnterRaceUI(false);

        if (raceStartVisual != null)
            raceStartVisual.SetActive(false);

        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(true);
            loadingText.text = loadingBaseText;
            loadingText.alpha = 1f;
        }

        if (tipText != null)
        {
            tipText.gameObject.SetActive(false);
            Color tipColor = tipText.color;
            tipColor.a = 1f;
            tipText.color = tipColor;
        }

        if (loadingSlider != null)
            loadingSlider.value = 0f;

        if (sliderPercentText != null)
            sliderPercentText.text = "0%";

        float t = 0f;
        float dotTimer = 0f;
        int dotCount = 0;

        while (t < loadingDuration)
        {
            t += Time.unscaledDeltaTime;
            dotTimer += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(t / loadingDuration);

            if (loadingSlider != null)
                loadingSlider.value = progress;

            if (sliderPercentText != null)
                sliderPercentText.text = $"{Mathf.RoundToInt(progress * 100f)}%";

            if (loadingText != null && dotTimer >= 0.35f)
            {
                dotTimer = 0f;
                dotCount = (dotCount + 1) % 4;
                loadingText.text = loadingBaseText + new string('.', dotCount);
            }

            yield return null;
        }

        if (loadingSlider != null)
            loadingSlider.value = 1f;

        if (sliderPercentText != null)
            sliderPercentText.text = "100%";

        if (loadingText != null)
            loadingText.gameObject.SetActive(false);

        if (tipText != null)
        {
            tipText.gameObject.SetActive(true);
            tipText.text = continueText;
        }

        if (waitForEnterAfterLoading)
        {
            float pulseTimer = 0f;

            while (!Input.GetKeyDown(KeyCode.Return))
            {
                pulseTimer += Time.unscaledDeltaTime;

                if (tipText != null)
                {
                    Color c = tipText.color;
                    c.a = 0.55f + Mathf.PingPong(pulseTimer * 1.5f, 0.45f);
                    tipText.color = c;
                }

                yield return null;
            }
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (raceMode == RaceMode.TimeChallenge)
        {
            currentTimeChallengeGateIndex = firstTimeChallengeGateIsStart ? 1 : 0;
            timeChallengeTimeLeft = GetTimeToNextTimeChallengeGate();
        }

        if (raceMode == RaceMode.SpeedTrap && speedTrapVisualRoot != null)
            speedTrapVisualRoot.SetActive(true);

        if (activeNitro != null)
            activeNitro.RefillNitro();

        TeleportCarToStart();
        SpawnAICars();
        BuildRacersList();
        BuildStandingsUI();
        PrepareRaceHudForCountdown();

        if (tipText != null)
        {
            tipText.gameObject.SetActive(false);

            Color c = tipText.color;
            c.a = 1f;
            tipText.color = c;
        }

        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(true);
            loadingText.alpha = 1f;
            loadingText.text = loadingBaseText;
        }

        IsRaceLoading = false;
        SetRaceMenuState(false);


        if (raceLoadingRoot != null)
            raceLoadingRoot.SetActive(false);

        if (raceMode == RaceMode.SpeedTrap && speedTrapSummaryText != null)
        {
            speedTrapSummaryText.text = "0 KM/H";
        }
    }

    void SetRaceMenuState(bool opened)
    {
        if (opened)
        {
            _previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;

            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0.02f;

            if (speedometerHudRoot != null)
                speedometerHudRoot.SetActive(false);

            SetCarPreRaceLocked(true);
        }
        else
        {
            Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            if (speedometerHudRoot != null)
                speedometerHudRoot.SetActive(true);

            if (racePhase == RacePhase.Idle)
                SetCarPreRaceLocked(false);
        }
    }

    void ShowFinishPanel(RaceFinishResult result, int place = 1)
    {
        HideCurrentRaceArrows();

        if (minimapDynamicRoute != null)
            minimapDynamicRoute.HideRoute();

        if (minimapAIIconManager != null)
            minimapAIIconManager.ClearIcons();

        RaceEventUIController.ActiveFinishRaceManager = this;

        SetCarPreRaceLocked(true);
        FreezeActiveCarCompletely();

        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0.02f;

        if (minimapSpeedTrapIconManager != null)
            minimapSpeedTrapIconManager.ClearIcons();

        if (speedometerHudRoot != null)
            speedometerHudRoot.SetActive(false);

        if (raceFinalPanelRoot != null)
            raceFinalPanelRoot.SetActive(true);

        if (newRecordText != null)
        {
            newRecordText.gameObject.SetActive(newRecordThisRace);
            newRecordText.text = "NEW RECORD!";
        }

        if (finishHeaderText != null)
        {
            finishHeaderText.text = result switch
            {
                RaceFinishResult.Win => "YOU WIN!",
                RaceFinishResult.Lose => "YOU LOSE!",
                _ => "RACE COMPLETE"
            };
        }

        bool showPlace = result == RaceFinishResult.Win || result == RaceFinishResult.Lose;
        bool rewardAvailableNow = result != RaceFinishResult.Lose && raceRewardCash > 0 && !IsRewardClaimed();

        if (finishPlaceRoot != null)
            finishPlaceRoot.SetActive(showPlace);

        if (finishPlaceText != null && showPlace)
            finishPlaceText.text = place.ToString();

        if (finishTimeValueText != null)
            finishTimeValueText.text = FormatRaceTime(finishTime);

        if (finishRewardRoot != null)
            finishRewardRoot.SetActive(rewardAvailableNow);

        if (finishRewardValueText != null)
            finishRewardValueText.text = rewardAvailableNow ? $"{raceRewardCash} $" : "";

        if (rewardAvailableNow)
        {
            InventoryUI inv = InventoryUI.Instance;
            if (inv != null)
                inv.ApplyMoneyChange(raceRewardCash);
            else
            {
                PlayerStats stats = FindFirstObjectByType<PlayerStats>();
                if (stats != null)
                    stats.AddMoney(raceRewardCash);
            }

            MarkRewardClaimed();
        }
    }

    public void OnFinishRestart()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        EndFinishCinematic();

        if (activeCar != null)
            activeCar.isControlled = true;

        HideCurrentRaceArrows();
        ResetRaceStateKeepActiveCar();
        BeginRaceSequence(activeCar);
    }

    public void OnFinishContinue()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        EndFinishCinematic();

        if (activeCar != null)
            activeCar.isControlled = true;

        if (raceFinalPanelRoot != null)
            raceFinalPanelRoot.SetActive(false);

        HideCurrentRaceArrows();
        ResetRace();
    }

    void ResetRaceStateKeepActiveCar()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SetCarPreRaceLocked(false);
        ClearAICars();
        ClearStandingsUI();


        racers.Clear();
        finishCameraActive = false;
        currentFinishCameraPoint = null;
        cachedFinishCameraPoints.Clear();

        newRecordThisRace = false;

        if (standingsRoot != null)
            standingsRoot.SetActive(false);

        if (newRecordText != null)
            newRecordText.gameObject.SetActive(false);

        if (finishCinematicCamera != null)
            finishCinematicCamera.enabled = false;

        if (mainGameplayCamera != null)
            mainGameplayCamera.enabled = true;

        if (minimapRoot != null)
            minimapRoot.SetActive(true);

        raceActive = false;
        raceFinished = false;
        racePhase = RacePhase.Idle;

        raceStartTime = 0f;
        lapStartTime = 0f;
        finishTime = 0f;
        lastGateTime = -999f;
        lastSectorStartTime = 0f;

        currentLap = 0;
        currentSplitIndex = 0;

        lapTimes.Clear();
        ClearLapEntries();

        bestLapTime = -1f;
        previousLapTime = -1f;
        EnsureBestSplitListSize();

        sprintSplinePoints.Clear();
        sprintSplineDistances.Clear();
        sprintSplineTotalLength = 0f;
        sprintProgress01 = 0f;

        if (raceRoot != null)
            raceRoot.SetActive(false);

        if (raceFinalPanelRoot != null)
            raceFinalPanelRoot.SetActive(false);

        if (speedometerHudRoot != null && activeCar != null && activeCar.isControlled)
            speedometerHudRoot.SetActive(true);

        if (nextBoothRoutine != null)
        {
            StopCoroutine(nextBoothRoutine);
            nextBoothRoutine = null;
        }

        RefreshIdleUi();
        RefreshModeUi();
        UpdateSprintProgressUI(0f);
    }

    void StartFinishCinematic()
    {
        if (activeCar == null)
            return;

        activeCar.isControlled = false;

        if (activeNitro != null)
            activeNitro.nitroLocked = true;

        if (raceRoot != null)
            raceRoot.SetActive(false);

        if (speedometerHudRoot != null)
            speedometerHudRoot.SetActive(false);

        if (minimapRoot != null)
            minimapRoot.SetActive(false);

        CacheFinishCameraPoints();

        currentFinishCameraPoint = null;

        if (cachedFinishCameraPoints.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, cachedFinishCameraPoints.Count);
            currentFinishCameraPoint = cachedFinishCameraPoints[index];
        }

        if (finishCinematicCamera != null)
        {
            if (currentFinishCameraPoint != null)
            {
                finishCinematicCamera.transform.position = currentFinishCameraPoint.position;
                finishCinematicCamera.transform.rotation = currentFinishCameraPoint.rotation;
            }

            finishCinematicCamera.enabled = true;
            finishCameraActive = true;
        }

        if (mainGameplayCamera != null)
            mainGameplayCamera.enabled = false;

        Time.timeScale = finishSlowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    void EndFinishCinematic()
    {
        finishCameraActive = false;
        currentFinishCameraPoint = null;

        if (finishCinematicCamera != null)
            finishCinematicCamera.enabled = false;

        if (mainGameplayCamera != null)
            mainGameplayCamera.enabled = true;

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    void CacheFinishCameraPoints()
    {
        cachedFinishCameraPoints.Clear();

        if (activeCar == null)
            return;

        Transform root = activeCar.transform.Find(finishCameraRootName);
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform point = root.GetChild(i);
            if (point != null)
                cachedFinishCameraPoints.Add(point);
        }
    }

    void FreezeActiveCarCompletely()
    {
        if (activeCar == null)
            return;

        Rigidbody rb = activeCar.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
    }

    void PrepareRaceHudForCountdown()
    {
        if (raceRoot != null)
            raceRoot.SetActive(true);

        if (playerGuiRoot != null)
            playerGuiRoot.SetActive(false);

        if (gunUiRoot != null)
            gunUiRoot.SetActive(false);

        if (timerValueText != null)
        {
            if (raceMode == RaceMode.TimeChallenge)
                timerValueText.text = FormatCountdownTime(timeChallengeTimeLeft);
            else
                timerValueText.text = "00:00:00";
        }

        if (lapRecordText != null)
        {
            lapRecordText.text = "";
            lapRecordText.color = new Color(neutralColor.r, neutralColor.g, neutralColor.b, 0f);
        }

        RefreshLapUi();
        RefreshModeUi();
        UpdateSprintProgressUI(0f);
    }

    void HandleTimeChallengeGate(RaceGateTrigger gate)
    {
        if (currentTimeChallengeGateIndex >= timeChallengeGates.Count)
            return;

        RaceGateTrigger expectedGate = timeChallengeGates[currentTimeChallengeGateIndex];

        if (gate != expectedGate)
            return;

        bool isFinishGate = currentTimeChallengeGateIndex >= timeChallengeGates.Count - 1;

        currentTimeChallengeGateIndex++;
        lastRaceRespawnPoint = gate.transform;

        if (isFinishGate)
        {
            FinishTimeChallengeRace();
            return;
        }

        float bonusTime = timeChallengeTimeLeft * remainingTimeBonusMultiplier;
        float nextGateTime = GetTimeToNextTimeChallengeGate();

        timeChallengeTimeLeft = bonusTime + nextGateTime;

        ShowTimeChallengeBonusText(bonusTime);
    }

    void FinishTimeChallengeRace()
    {
        racePhase = RacePhase.Finished;
        raceFinished = true;
        pendingFinishResult = RaceFinishResult.Complete;

        finishTime = Time.time - raceStartTime;
        newRecordThisRace = SaveBestTimeIfBetter(finishTime);

        sprintProgress01 = 1f;
        UpdateSprintProgressUI(1f);

        if (timerValueText != null)
            timerValueText.text = FormatCountdownTime(timeChallengeTimeLeft);

        if (cancelHintText != null)
            cancelHintText.gameObject.SetActive(false);

        RefreshModeUi();

        if (finishRoutine != null)
            StopCoroutine(finishRoutine);

        finishRoutine = StartCoroutine(FinishSequence());
    }

    void FailTimeChallengeRace()
    {
        racePhase = RacePhase.Finished;
        raceFinished = true;
        pendingFinishResult = RaceFinishResult.Lose;

        finishTime = Time.time - raceStartTime;

        if (nextBoothRoutine != null)
        {
            StopCoroutine(nextBoothRoutine);
            nextBoothRoutine = null;
        }

        ShowCenterText("TIME OVER", worseLapColor);

        if (cancelHintText != null)
            cancelHintText.gameObject.SetActive(false);

        RefreshModeUi();

        if (finishRoutine != null)
            StopCoroutine(finishRoutine);

        finishRoutine = StartCoroutine(FinishSequence());
    }

    float GetTimeToNextTimeChallengeGate()
    {
        if (timeChallengeGates == null || timeChallengeGates.Count == 0)
            return timeChallengeStartTime;

        if (currentTimeChallengeGateIndex >= timeChallengeGates.Count)
            return 0f;

        Transform from = activeCar != null ? activeCar.transform : null;

        if (firstTimeChallengeGateIsStart && currentTimeChallengeGateIndex > 0)
            from = timeChallengeGates[currentTimeChallengeGateIndex - 1].transform;

        Transform to = timeChallengeGates[currentTimeChallengeGateIndex].transform;

        if (from == null || to == null)
            return timeChallengeStartTime;

        float distance = Vector3.Distance(from.position, to.position);
        float time = (distance / 100f) * secondsPer100Meters;

        return Mathf.Clamp(time, minGateTime, maxGateTime);
    }

    IEnumerator ShowNextBoothTextRoutine()
    {
        if (lapRecordText == null)
            yield break;

        float t = 0f;

        while (t < nextBoothTextDuration &&
               raceMode == RaceMode.TimeChallenge &&
               racePhase == RacePhase.Racing &&
               raceActive &&
               !raceFinished)
        {
            t += Time.deltaTime;
            lapRecordText.text = $"NEXT BOOTH:\n{FormatCountdownTime(timeChallengeTimeLeft)}";

            Color c = neutralColor;
            c.a = 1f;
            lapRecordText.color = c;

            yield return null;
        }

        lapRecordText.text = "";
        Color clear = lapRecordText.color;
        clear.a = 0f;
        lapRecordText.color = clear;

        nextBoothRoutine = null;
    }

    string FormatCountdownTime(float timeSeconds)
    {
        timeSeconds = Mathf.Max(0f, timeSeconds);

        int minutes = Mathf.FloorToInt(timeSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeSeconds % 60f);
        int centiseconds = Mathf.FloorToInt((timeSeconds % 1f) * 100f);
        centiseconds = Mathf.Clamp(centiseconds, 0, 99);

        if (minutes > 0)
            return $"{minutes:00}:{seconds:00}:{centiseconds:00}";

        return $"{seconds:00}:{centiseconds:00}";
    }

    void UpdateTimeChallengeWarningText()
    {
        if (raceMode != RaceMode.TimeChallenge)
            return;

        if (lapRecordText == null)
            return;

        if (nextBoothRoutine != null)
            return;

        if (timeChallengeTimeLeft > hurryUpTime)
            return;

        lapRecordText.text = $"HURRY UP!\n{FormatCountdownTime(timeChallengeTimeLeft)}";

        float danger01 = Mathf.InverseLerp(hurryUpTime, 0f, timeChallengeTimeLeft);

        // im mniej czasu, tym szybsze pulsowanie
        float pulseSpeed = Mathf.Lerp(3f, 10f, danger01);

        // im mniej czasu, tym mocniejszy kontrast pulsowania
        float minAlpha = Mathf.Lerp(0.45f, 0.15f, danger01);

        Color c = worseLapColor;
        c.a = minAlpha + Mathf.PingPong(Time.time * pulseSpeed, 1f - minAlpha);
        lapRecordText.color = c;
    }

    void RespawnActiveCarInRace()
    {
        if (activeCar == null)
            return;

        Vector3 spawnPos;
        Quaternion spawnRot;

        if (useSplineRespawn && TryGetSplineRespawnPose(out spawnPos, out spawnRot))
        {
            // używa pozycji z RaceRoute
        }
        else if (lastRaceRespawnPoint != null)
        {
            spawnRot = lastRaceRespawnPoint.rotation;
            spawnPos = lastRaceRespawnPoint.position
                + lastRaceRespawnPoint.forward * respawnForwardOffset
                + Vector3.up * respawnUpOffset;
        }
        else
        {
            spawnRot = lastSafeRotation;
            spawnPos = lastSafePosition + Vector3.up * respawnUpOffset;
        }

        Rigidbody rb = activeCar.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.position = spawnPos;
            rb.rotation = spawnRot;

            rb.Sleep();
            rb.WakeUp();
        }
        else
        {
            activeCar.transform.position = spawnPos;
            activeCar.transform.rotation = spawnRot;
        }

        lastSafePosition = spawnPos;
        lastSafeRotation = spawnRot;
        respawnSaveTimer = 0f;

        activeCar.isControlled = true;

        if (respawnProtectionRoutine != null)
            StopCoroutine(respawnProtectionRoutine);

        respawnProtectionRoutine = StartCoroutine(RespawnProtectionRoutine());
    }

    IEnumerator RespawnProtectionRoutine()
    {
        if (activeCarDestructible != null)
            activeCarDestructible.isInvincible = true;

        Collider[] carColliders = activeCar.GetComponentsInChildren<Collider>(true);
        Collider[] nearbyColliders = Physics.OverlapSphere(
            activeCar.transform.position,
            respawnIgnoreCollisionRadius,
            respawnIgnoreCollisionMask,
            QueryTriggerInteraction.Ignore
        );

        List<(Collider, Collider)> ignoredPairs = new();

        for (int i = 0; i < carColliders.Length; i++)
        {
            Collider carCol = carColliders[i];
            if (carCol == null || carCol.isTrigger)
                continue;

            for (int j = 0; j < nearbyColliders.Length; j++)
            {
                Collider otherCol = nearbyColliders[j];
                if (otherCol == null || otherCol.isTrigger)
                    continue;

                if (otherCol.transform.IsChildOf(activeCar.transform))
                    continue;

                Physics.IgnoreCollision(carCol, otherCol, true);
                ignoredPairs.Add((carCol, otherCol));
            }
        }

        yield return new WaitForSeconds(respawnInvincibleDuration);

        for (int i = 0; i < ignoredPairs.Count; i++)
        {
            if (ignoredPairs[i].Item1 != null && ignoredPairs[i].Item2 != null)
                Physics.IgnoreCollision(ignoredPairs[i].Item1, ignoredPairs[i].Item2, false);
        }

        if (activeCarDestructible != null && raceActive && !raceFinished)
            activeCarDestructible.isInvincible = true;
    }

    void UpdateDynamicRespawnPoint()
    {
        if (activeCar == null)
            return;

        respawnSaveTimer += Time.deltaTime;

        if (respawnSaveTimer < respawnSaveInterval)
            return;

        respawnSaveTimer = 0f;

        Rigidbody rb = activeCar.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        float speed = rb.linearVelocity.magnitude * 3.6f;

        // za wolno = nie zapisujemy (np. zaklinowany)
        if (speed < minSpeedToSave)
            return;

        // auto nie może być wywrócone
        float angle = Vector3.Angle(activeCar.transform.up, Vector3.up);
        if (angle > maxRollAngle)
            return;

        lastSafePosition = activeCar.transform.position;
        lastSafeRotation = activeCar.transform.rotation;
    }

    bool TryGetSplineRespawnPose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (activeCar == null)
            return false;

        if (sprintSplinePoints.Count < 2 || sprintSplineDistances.Count < 2 || sprintSplineTotalLength <= 0.01f)
            BuildSprintSplineCache();

        if (sprintSplinePoints.Count < 2 || sprintSplineDistances.Count < 2 || sprintSplineTotalLength <= 0.01f)
            return false;

        int nearestIndex = FindNearestRoutePointIndex(activeCar.transform.position);

        float nearestDistance = sprintSplineDistances[nearestIndex];
        float targetDistance = Mathf.Max(0f, nearestDistance - respawnBackDistanceOnSpline);

        int bestIndex = 0;
        float bestDiff = float.MaxValue;

        for (int i = 0; i < sprintSplineDistances.Count; i++)
        {
            float diff = Mathf.Abs(sprintSplineDistances[i] - targetDistance);

            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestIndex = i;
            }
        }

        Vector3 point = sprintSplinePoints[bestIndex];

        int nextIndex = Mathf.Min(bestIndex + 1, sprintSplinePoints.Count - 1);
        Vector3 forward = sprintSplinePoints[nextIndex] - point;

        if (forward.sqrMagnitude < 0.001f && bestIndex > 0)
            forward = point - sprintSplinePoints[bestIndex - 1];

        if (forward.sqrMagnitude < 0.001f)
            forward = raceStartPoint != null ? raceStartPoint.forward : transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        forward.Normalize();

        Vector3 rawPosition = point + Vector3.up * respawnUpOffset;

        Vector3 rayOrigin = point + Vector3.up * respawnRaycastUp;
        float rayDistance = respawnRaycastUp + respawnRaycastDown;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, respawnGroundMask, QueryTriggerInteraction.Ignore))
            rawPosition = hit.point + Vector3.up * respawnGroundOffset;

        position = rawPosition;
        rotation = Quaternion.LookRotation(forward, Vector3.up);

        return true;
    }

    void OpenRacePause()
    {
        racePauseOpen = true;
        RaceEventUIController.ActiveFinishRaceManager = this;

        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0.02f;

        SetCarPreRaceLocked(true);

        if (racePauseRoot != null)
            racePauseRoot.SetActive(true);

        if (speedometerHudRoot != null)
            speedometerHudRoot.SetActive(false);
    }

    public void OpenRaceEventPanel(CarControll car)
    {
        ApplyRaceEventDefinition();

        if (car == null) return;
        if (racePhase != RacePhase.Idle) return;

        if (CurrentPanelManager != null && CurrentPanelManager != this)
            CurrentPanelManager.ForceCloseRaceEventPanel();

        CurrentPanelManager = this;
        ActiveRaceManager = this;

        activeCar = car;
        raceEventPanelOpen = true;
        blockRaceEventSubmitUntilRelease = true;
        racePhase = RacePhase.WaitingForStart;

        ShowEnterRaceUI(false);

        if (raceEventPanelRoot != null)
            raceEventPanelRoot.SetActive(true);

        SetRaceMenuState(true);
        RefreshRaceEventPanel();

        Debug.Log($"OPEN PANEL: {gameObject.name} / {raceMode}");
    }

    void CloseRacePause()
    {
        racePauseOpen = false;
        RaceEventUIController.ActiveFinishRaceManager = null;

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        SetCarPreRaceLocked(false);

        if (racePauseRoot != null)
            racePauseRoot.SetActive(false);

        if (speedometerHudRoot != null)
            speedometerHudRoot.SetActive(true);
    }

    public void OnPauseLeaveRace()
    {
        racePauseOpen = false;

        if (racePauseRoot != null)
            racePauseRoot.SetActive(false);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        ClearGlobalRaceState();
        ResetRace();
    }

    public void CloseRacePauseFromButton()
    {
        CloseRacePause();
    }

    private string RewardSaveKey
    {
        get
        {
            string id = string.IsNullOrWhiteSpace(raceRewardId) ? gameObject.name : raceRewardId;
            return $"RaceRewardClaimed_{id}";
        }
    }

    bool IsRewardClaimed()
    {
        return rewardOnlyOnce && PlayerPrefs.GetInt(RewardSaveKey, 0) == 1;
    }

    void MarkRewardClaimed()
    {
        if (!rewardOnlyOnce)
            return;

        PlayerPrefs.SetInt(RewardSaveKey, 1);
        PlayerPrefs.Save();
    }

    string BestTimeSaveKey
    {
        get
        {
            string id = string.IsNullOrWhiteSpace(raceBestTimeId) ? gameObject.name : raceBestTimeId;
            return $"RaceBestTime_{id}";
        }
    }

    float GetSavedBestTime()
    {
        return PlayerPrefs.GetFloat(BestTimeSaveKey, -1f);
    }

    bool SaveBestTimeIfBetter(float newTime)
    {
        float currentBest = GetSavedBestTime();

        if (currentBest < 0f || newTime < currentBest)
        {
            PlayerPrefs.SetFloat(BestTimeSaveKey, newTime);
            PlayerPrefs.Save();
            return true;
        }

        return false;
    }

    string GetBestTimeText()
    {
        float best = GetSavedBestTime();
        return best < 0f ? defaultBestTime : FormatRaceTime(best);
    }

    public void OnSpeedTrapPassed(SpeedTrapTrigger trap, CarControll car)
    {
        if (raceMode != RaceMode.SpeedTrap)
            return;

        if (racePhase != RacePhase.Racing || !raceActive || raceFinished)
            return;

        if (trap == null || car == null)
            return;

        bool isPlayerCar = car == activeCar;
        bool isAICar = activeAICars.Contains(car);

        if (!isPlayerCar && !isAICar)
            return;

        int speed = car.GetDisplaySpeedKPH();

        RacerRuntimeInfo racer = racers.Find(r => r.car == car);
        if (racer != null)
            racer.speedTrapScore += speed;

        if (isPlayerCar)
        {
            speedTrapPassedCount++;
            speedTrapTotalKmh += speed;

            if (speedTrapSummaryText != null)
                speedTrapSummaryText.text = $"{speedTrapTotalKmh} KM/H";

            ShowSpeedTrapResult(speed);
        }

        UpdateStandings();
    }

    void ShowSpeedTrapResult(int speed)
    {
        ShowCenterText($"{speed} KM/H", betterLapColor);
    }

    void FinishSpeedTrapRace()
    {
        racePhase = RacePhase.Finished;
        raceFinished = true;

        finishTime = Time.time - raceStartTime;

        sprintProgress01 = 1f;
        UpdateSprintProgressUI(1f);

        if (timerValueText != null)
            timerValueText.text = FormatRaceTime(finishTime);

        if (cancelHintText != null)
            cancelHintText.gameObject.SetActive(false);

        if (minimapSpeedTrapIconManager != null)
            minimapSpeedTrapIconManager.ClearIcons();

        if (speedTrapVisualRoot != null)
            speedTrapVisualRoot.SetActive(false);

        RefreshModeUi();

        if (finishRoutine != null)
            StopCoroutine(finishRoutine);

        ApplySpeedTrapFinishBonusForPlayer();
        UpdateStandings();

        racers.Sort((a, b) => b.speedTrapScore.CompareTo(a.speedTrapScore));

        int playerPlace = racers.FindIndex(r => r.car == activeCar) + 1;
        bool playerWon = playerPlace == 1;

        pendingFinishResult = playerWon ? RaceFinishResult.Win : RaceFinishResult.Lose;
        finishRoutine = StartCoroutine(FinishSequenceWithPlace(playerPlace));
    }

    public void OnPauseRestartRace()
    {
        CarControll carToRestart = activeCar;

        racePauseOpen = false;

        if (racePauseRoot != null)
            racePauseRoot.SetActive(false);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (carToRestart != null)
        {
            carToRestart.isControlled = true;

            Rigidbody rb = carToRestart.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        ResetRaceStateKeepActiveCar();

        activeCar = carToRestart;

        if (activeCar != null)
        {
            activeCar.isControlled = true;
            BeginRaceSequence(activeCar);
        }
    }

    void StopRoutine(ref Coroutine routine)
    {
        if (routine == null) return;

        StopCoroutine(routine);
        routine = null;
    }

    void ResetTransitionUi()
    {
        IsRaceLoading = false;

        if (raceLoadingRoot != null)
            raceLoadingRoot.SetActive(false);

        if (loadingText != null)
            loadingText.gameObject.SetActive(true);

        if (loadingSlider != null)
            loadingSlider.value = 0f;

        if (sliderPercentText != null)
            sliderPercentText.text = "0%";

        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        if (fadeRoot != null)
            fadeRoot.SetActive(false);

        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 0f;
    }

    void StopRaceCoroutines()
    {
        StopRoutine(ref preRaceRoutine);
        StopRoutine(ref finishRoutine);
        StopRoutine(ref lapRecordRoutine);
        StopRoutine(ref nextBoothRoutine);
        StopRoutine(ref respawnProtectionRoutine);
    }

    void ResetHudForNewRace()
    {
        raceActive = false;
        raceFinished = false;

        raceStartTime = 0f;
        lapStartTime = 0f;
        finishTime = 0f;
        lastGateTime = -999f;

        currentLap = IsCircuitLikeRace() ? 1 : 0;
        currentSplitIndex = 0;

        sprintProgress01 = 0f;
        UpdateSprintProgressUI(0f);

        if (timerValueText != null)
        {
            if (raceMode == RaceMode.TimeChallenge)
                timerValueText.text = FormatCountdownTime(timeChallengeTimeLeft);
            else
                timerValueText.text = "00:00:00";
        }

        if (lapValueText != null)
        {
            if (raceMode == RaceMode.Circuit)
                lapValueText.text = $"{currentLap}/{totalLaps}";
            else
                lapValueText.text = "";
        }

        if (lapRecordText != null)
        {
            lapRecordText.text = "";
            lapRecordText.color = new Color(neutralColor.r, neutralColor.g, neutralColor.b, 0f);
        }

        if (speedTrapSummaryText != null)
            speedTrapSummaryText.text = "0 KM/H";

        RefreshModeUi();
    }

    void ForceCloseRaceEventPanel()
    {
        raceEventPanelOpen = false;
        blockRaceEventSubmitUntilRelease = false;

        if (racePhase == RacePhase.WaitingForStart)
            racePhase = RacePhase.Idle;

        if (raceEventPanelRoot != null)
            raceEventPanelRoot.SetActive(false);

        SetRaceMenuState(false);
        ShowEnterRaceUI(false);

        if (ActiveRaceManager == this)
            ActiveRaceManager = null;

        if (CurrentPanelManager == this)
            CurrentPanelManager = null;
    }

    void ShowTimeChallengeBonusText(float bonusTime)
    {
        if (nextBoothRoutine != null)
            StopCoroutine(nextBoothRoutine);

        nextBoothRoutine = StartCoroutine(ShowTimeChallengeBonusTextRoutine(bonusTime));
    }

    IEnumerator ShowTimeChallengeBonusTextRoutine(float bonusTime)
    {
        if (lapRecordText == null)
            yield break;

        lapRecordText.text = $"BONUS TIME:\n+{FormatCountdownTime(bonusTime)}";
        Color c = betterLapColor;
        c.a = 1f;
        lapRecordText.color = c;

        yield return new WaitForSeconds(2.5f);

        nextBoothRoutine = StartCoroutine(ShowNextBoothTextRoutine());
    }

    void HandleAIGate(RaceGateTrigger gate, CarControll aiCar)
    {
        if (aiCar == null || gate == null)
            return;

        RacerRuntimeInfo racer = racers.Find(r => r.car == aiCar);
        if (racer == null)
            return;

        if (raceMode == RaceMode.Sprint || raceMode == RaceMode.SpeedTrap || raceMode == RaceMode.TimeChallenge)
        {
            if (raceMode == RaceMode.SpeedTrap)
            {
                if (gate == finishGate)
                    RegisterSpeedTrapAIFinish(aiCar);

                return;
            }

            if (gate == finishGate)
                RegisterAIFinish(aiCar);

            return;
        }

        if (!IsCircuitLikeRace())
            return;

        int splitIndex = splitGates.IndexOf(gate);

        if (splitIndex >= 0)
        {
            if (splitIndex == racer.currentSplitIndex)
                racer.currentSplitIndex++;

            return;
        }

        if (gate == finishGate)
        {
            if (racer.currentSplitIndex < splitGates.Count)
                return;

            if (racer.currentLap >= totalLaps)
            {
                RegisterAIFinish(aiCar);
                return;
            }

            racer.currentLap++;
            racer.currentSplitIndex = 0;

            if (raceMode == RaceMode.Elimination)
                TryResolveEliminationLap();
        }
    }

    void RegisterSpeedTrapAIFinish(CarControll aiCar)
    {
        RacerRuntimeInfo racer = racers.Find(r => r.car == aiCar);

        if (racer == null)
            return;

        if (racer.speedTrapFinished)
            return;

        racer.speedTrapFinished = true;
        racer.speedTrapFinishTime = Time.time;

        RegisterAIFinish(aiCar);
    }

    void SpawnAICars()
    {
        ClearAICars();

        AIRacerInfo.ResetUsedNames();

        if (!useAIInRace)
            return;

        if (aiCarPrefabs == null || aiCarPrefabs.Length == 0)
            return;

        if (aiStartPoints == null || aiStartPoints.Length == 0)
            return;

        List<GameObject> prefabPool = new List<GameObject>(aiCarPrefabs);

        int spawnCount = Mathf.Min(aiCount, aiStartPoints.Length, prefabPool.Count);

        for (int i = 0; i < spawnCount; i++)
        {
            Transform start = aiStartPoints[i];
            if (start == null)
                continue;

            int randomIndex = UnityEngine.Random.Range(0, prefabPool.Count);
            GameObject selectedPrefab = prefabPool[randomIndex];
            prefabPool.RemoveAt(randomIndex);

            GameObject obj = Instantiate(selectedPrefab, start.position, start.rotation);

            AIRacerInfo racerInfo = obj.GetComponent<AIRacerInfo>();
            if (racerInfo != null)
                racerInfo.AssignRandomName();

            if (minimapAIIconManager != null)
                minimapAIIconManager.RegisterAI(obj.transform, i);

            CarControll car = obj.GetComponent<CarControll>();
            AICarController ai = obj.GetComponent<AICarController>();
            Rigidbody rb = obj.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
                rb.WakeUp();
            }

            if (car != null)
            {
                car.isControlled = true;
                car.useExternalInput = true;
                car.raceStartLock = true;
                car.ResetRaceVisualState();

                activeAICars.Add(car);
            }

            if (ai != null)
            {
                ai.enabled = true;
                ai.SetRacePath(GetRaceRoutePointsForAI());
            }
        }


    }

    void ClearAICars()
    {
        for (int i = 0; i < activeAICars.Count; i++)
        {
            if (activeAICars[i] != null)
                Destroy(activeAICars[i].gameObject);
        }

        activeAICars.Clear();
        finishOrder.Clear();

        if (minimapAIIconManager != null)
            minimapAIIconManager.ClearIcons();
    }

    public List<Vector3> GetRaceRoutePointsForAI()
    {
        if (sprintSplinePoints.Count < 2)
            BuildSprintSplineCache();

        return new List<Vector3>(sprintSplinePoints);
    }

    void BuildRacersList()
    {
        racers.Clear();

        if (activeCar != null)
        {
            racers.Add(new RacerRuntimeInfo
            {
                car = activeCar,
                driverName = playerDriverName,
                isPlayer = true,
                finishedPlace = 0,
                currentLap = IsCircuitLikeRace() ? currentLap : 1,
                currentSplitIndex = 0,
                eliminated = false
            });
        }

        for (int i = 0; i < activeAICars.Count; i++)
        {
            CarControll aiCar = activeAICars[i];
            if (aiCar == null) continue;

            AIRacerInfo info = aiCar.GetComponent<AIRacerInfo>();

            racers.Add(new RacerRuntimeInfo
            {
                car = aiCar,
                driverName = info != null ? info.driverName : $"AI {i + 1}",
                isPlayer = false,
                finishedPlace = 0,
                currentLap = 1,
                currentSplitIndex = 0,
                eliminated = false
            });
        }
    }

    void BuildStandingsUI()
    {
        ClearStandingsUI();

        standingsDistanceTimer = 0f;
        standingsShowingDistance = false;

        if (standingsRoot != null)
            standingsRoot.SetActive(true);

        if (standingsContainer == null || standingsEntryPrefab == null)
            return;

        for (int i = 0; i < racers.Count; i++)
        {
            GameObject entryObj = Instantiate(standingsEntryPrefab, standingsContainer);
            StandingsEntryUI entry = entryObj.GetComponent<StandingsEntryUI>();

            if (entry != null)
            {
                entry.Set(i + 1, racers[i].driverName, racers[i].isPlayer);
                standingsEntries.Add(entry);
            }
        }
    }

    void ClearStandingsUI()
    {
        for (int i = standingsEntries.Count - 1; i >= 0; i--)
        {
            if (standingsEntries[i] != null)
                Destroy(standingsEntries[i].gameObject);
        }

        standingsEntries.Clear();
    }

    void UpdateStandings()
    {
        if (racers.Count == 0 || standingsEntries.Count == 0)
            return;

        UpdateStandingsDistanceFlashTimer();

        racers.Sort((a, b) =>
        {
            if (a.eliminated && !b.eliminated)
                return 1;

            if (!a.eliminated && b.eliminated)
                return -1;

            if (raceMode == RaceMode.SpeedTrap)
                return b.speedTrapScore.CompareTo(a.speedTrapScore);

            if (a.finishedPlace > 0 && b.finishedPlace > 0)
                return a.finishedPlace.CompareTo(b.finishedPlace);

            if (a.finishedPlace > 0)
                return -1;

            if (b.finishedPlace > 0)
                return 1;

            float pa = GetRaceProgress01(a.car);
            float pb = GetRaceProgress01(b.car);

            return pb.CompareTo(pa);
        });

        for (int i = 0; i < racers.Count && i < standingsEntries.Count; i++)
        {
            string text = racers[i].driverName;

            if (racers[i].eliminated)
                text = $"{racers[i].driverName} OUT";
            else if (raceMode == RaceMode.SpeedTrap)
                text = $"{racers[i].driverName}  {racers[i].speedTrapScore} KM/H";
            else if (standingsShowingDistance)
                text = GetDistanceTextToPlayer(racers[i]);

            standingsEntries[i].Set(i + 1, text, racers[i].isPlayer);
        }
    }

    void UpdateStandingsDistanceFlashTimer()
    {
        if (!standingsShowDistanceFlash)
        {
            standingsShowingDistance = false;
            standingsDistanceTimer = 0f;
            return;
        }

        standingsDistanceTimer += Time.deltaTime;

        if (!standingsShowingDistance && standingsDistanceTimer >= standingsDistanceInterval)
        {
            standingsShowingDistance = true;
            standingsDistanceTimer = 0f;
        }
        else if (standingsShowingDistance && standingsDistanceTimer >= standingsDistanceDuration)
        {
            standingsShowingDistance = false;
            standingsDistanceTimer = 0f;
        }
    }

    string GetDistanceTextToPlayer(RacerRuntimeInfo racer)
    {
        if (racer == null || racer.car == null || activeCar == null)
            return "";

        if (racer.isPlayer)
            return "YOU";

        float playerProgress = GetRaceProgressMeters(activeCar);
        float racerProgress = GetRaceProgressMeters(racer.car);

        float diff = playerProgress - racerProgress;
        int meters = Mathf.RoundToInt(Mathf.Abs(diff));

        if (meters <= 1)
            return "0m";

        return diff >= 0f ? $"+{meters}m" : $"-{meters}m";
    }

    float GetRaceProgressMeters(CarControll car)
    {
        if (car == null || sprintSplinePoints.Count < 2)
            return 0f;

        int index = FindNearestRoutePointIndex(car.transform.position);

        float offRouteDistance = Vector3.Distance(
            car.transform.position,
            sprintSplinePoints[index]
        );

        if (offRouteDistance > 35f)
            return 0f;

        return sprintSplineDistances[index];
    }

    float GetRaceProgress01(CarControll car)
    {
        if (car == null || sprintSplinePoints.Count < 2 || sprintSplineTotalLength <= 0.01f)
            return 0f;

        int nearestIndex = FindNearestRoutePointIndex(car.transform.position);
        float progress = sprintSplineDistances[nearestIndex] / sprintSplineTotalLength;

        if (IsCircuitLikeRace())
        {
            RacerRuntimeInfo racer = racers.Find(r => r.car == car);
            int lap = racer != null ? racer.currentLap : 1;

            return Mathf.Clamp01((lap - 1 + progress) / Mathf.Max(1, totalLaps));
        }

        return Mathf.Clamp01(progress);
    }

    public List<Vector3> GetRaceRoutePointsForMinimap()
    {
        return BuildMinimapRoutePointsFromRaceRoute();
    }

    List<Vector3> BuildMinimapRoutePointsFromRaceRoute()
    {
        List<Vector3> result = new();

        if (raceRoute == null || raceRoute.Count <= 0)
            return result;

        int samplesPerSegment = Mathf.Max(16, splineSamples / Mathf.Max(1, raceRoute.Count));

        for (int i = 0; i < raceRoute.Count; i++)
        {
            List<Vector3> segmentPoints = GetRouteSegmentPointsByIndex(i, samplesPerSegment);

            if (segmentPoints == null || segmentPoints.Count < 2)
                continue;

            // usuń pierwszy punkt jeśli pokrywa się z poprzednim końcem
            if (result.Count > 0)
            {
                float gap = Vector3.Distance(result[result.Count - 1], segmentPoints[0]);

                if (gap < 3f)
                    segmentPoints.RemoveAt(0);
                else if (gap > 25f)
                {
                    Debug.LogWarning(
                        $"[MinimapRoute] Duży skok między segmentami w {raceRoute.name}: " +
                        $"{i - 1} -> {i}, gap={gap:0.0}m. Sprawdź reverse/splineIndex/kolejność segmentów.",
                        raceRoute
                    );
                }
            }

            result.AddRange(segmentPoints);
        }

        // Circuit / loop: domknij tylko jeśli koniec jest blisko początku
        if (raceRoute.loop && result.Count > 2)
        {
            float closingGap = Vector3.Distance(result[result.Count - 1], result[0]);

            if (closingGap < 25f)
                result.Add(result[0]);
            else
            {
                Debug.LogWarning(
                    $"[MinimapRoute] Loop nie został domknięty, bo gap={closingGap:0.0}m. " +
                    $"Sprawdź ostatni i pierwszy segment RaceRoute.",
                    raceRoute
                );
            }
        }

        return result;
    }

    void RegisterAIFinish(CarControll aiCar)
    {
        if (aiCar == null)
            return;

        if (finishOrder.Contains(aiCar))
            return;

        finishOrder.Add(aiCar);

        RacerRuntimeInfo racer = racers.Find(r => r.car == aiCar);
        if (racer != null)
            racer.finishedPlace = finishOrder.Count;

        AICarController ai = aiCar.GetComponent<AICarController>();
        if (ai != null)
            ai.enabled = false;

        aiCar.isControlled = false;
        aiCar.SetExternalInput(0f, 0f);

        Rigidbody rb = aiCar.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity *= 0.25f;
            rb.angularVelocity *= 0.25f;
        }

        UpdateStandings();
    }

    void OnEnable()
    {
        if (!allRaceManagers.Contains(this))
            allRaceManagers.Add(this);
    }

    void OnDisable()
    {
        allRaceManagers.Remove(this);
    }

    void SetRaceWorldState(bool racing)
    {
        for (int i = 0; i < allRaceManagers.Count; i++)
        {
            CarRaceManager manager = allRaceManagers[i];
            if (manager == null)
                continue;

            bool isThisRace = manager == this;

            if (manager.raceVisualRoot != null)
                manager.raceVisualRoot.SetActive(!racing || isThisRace);

            if (manager.enterRaceRoot != null)
                manager.enterRaceRoot.SetActive(false);

            if (manager.raceStartVisual != null)
                manager.raceStartVisual.SetActive(!racing);
        }

        for (int i = 0; i < objectsToShowOnlyForThisRace.Count; i++)
        {
            if (objectsToShowOnlyForThisRace[i] != null)
                objectsToShowOnlyForThisRace[i].SetActive(racing);
        }

        for (int i = 0; i < objectsToHideDuringThisRace.Count; i++)
        {
            if (objectsToHideDuringThisRace[i] != null)
                objectsToHideDuringThisRace[i].SetActive(!racing);
        }
    }

    void ApplySpeedTrapFinishBonusForPlayer()
    {
        RacerRuntimeInfo player = racers.Find(r => r.car == activeCar);

        if (player == null)
            return;

        player.speedTrapFinished = true;
        player.speedTrapFinishTime = Time.time;

        int bestAIScore = 0;
        RacerRuntimeInfo bestAI = null;

        for (int i = 0; i < racers.Count; i++)
        {
            RacerRuntimeInfo r = racers[i];

            if (r == null || r.isPlayer)
                continue;

            if (r.speedTrapScore > bestAIScore)
            {
                bestAIScore = r.speedTrapScore;
                bestAI = r;
            }
        }

        if (bestAI == null)
            return;

        int diff = player.speedTrapScore - bestAI.speedTrapScore;

        if (diff > 0)
        {
            player.speedTrapScore += diff;
            bestAI.speedTrapScore = Mathf.Max(0, bestAI.speedTrapScore - diff);
        }

        speedTrapTotalKmh = player.speedTrapScore;

        if (speedTrapSummaryText != null)
            speedTrapSummaryText.text = $"{speedTrapTotalKmh} KM/H";
    }

    void UpdateSpeedTrapFinishDrain()
    {
        if (raceMode != RaceMode.SpeedTrap)
            return;

        RacerRuntimeInfo player = racers.Find(r => r.car == activeCar);

        if (player == null || player.speedTrapFinished)
            return;

        speedTrapDrainAccumulator += speedTrapFinishPenaltyPerSecond * Time.deltaTime;

        int drain = Mathf.FloorToInt(speedTrapDrainAccumulator);

        if (drain <= 0)
            return;

        speedTrapDrainAccumulator -= drain;

        for (int i = 0; i < racers.Count; i++)
        {
            RacerRuntimeInfo r = racers[i];

            if (r == null || r.isPlayer)
                continue;

            if (!r.speedTrapFinished)
                continue;

            r.speedTrapScore = Mathf.Max(0, r.speedTrapScore - drain);
        }

        UpdateStandings();
    }

    [ContextMenu("APPLY RACE EVENT DEFINITION")]
    void ApplyRaceEventDefinition()
    {
        if (raceEventDefinition == null)
            return;

        raceMode = raceEventDefinition.raceMode;

        raceRoute = raceEventDefinition.raceRoute;
        routeArrowGenerator = raceEventDefinition.routeArrowGenerator;

        raceStartPoint = raceEventDefinition.raceStartPoint;
        finishGate = raceEventDefinition.finishGate;

        totalLaps = raceEventDefinition.totalLaps;

        splitGates.Clear();
        splitGates.AddRange(raceEventDefinition.splitGates);

        speedTraps.Clear();
        speedTraps.AddRange(raceEventDefinition.speedTraps);
        speedTrapVisualRoot = raceEventDefinition.speedTrapVisualRoot;

        for (int i = 0; i < speedTraps.Count; i++)
        {
            if (speedTraps[i] != null)
            {
                speedTraps[i].raceManager = this;
                speedTraps[i].ResetTrap();
            }
        }

        timeChallengeGates.Clear();
        timeChallengeGates.AddRange(raceEventDefinition.timeChallengeGates);
        timeChallengeStartTime = raceEventDefinition.timeChallengeStartTime;

        raceRouteName = raceEventDefinition.raceRouteName;
        raceLengthKm = raceEventDefinition.raceLengthKm;
        raceRewardCash = raceEventDefinition.raceRewardCash;
        racePreviewSprite = raceEventDefinition.racePreviewSprite;

        raceRewardId = raceEventDefinition.raceRewardId;
        raceBestTimeId = raceEventDefinition.raceBestTimeId;

        if (routeArrowGenerator != null)
            routeArrowGenerator.raceRoute = raceRoute;
    }

    void ShowCurrentRaceArrows()
    {
        if (activeRouteArrowGenerator != null && activeRouteArrowGenerator != routeArrowGenerator)
            activeRouteArrowGenerator.HideRaceArrows();

        activeRouteArrowGenerator = routeArrowGenerator;

        if (activeRouteArrowGenerator == null)
            return;

        activeRouteArrowGenerator.HideRaceArrows();
        activeRouteArrowGenerator.raceRoute = raceRoute;
        activeRouteArrowGenerator.ShowRaceArrows();
    }

    void HideCurrentRaceArrows()
    {
        if (activeRouteArrowGenerator != null)
            activeRouteArrowGenerator.HideRaceArrows();

        if (routeArrowGenerator != null && routeArrowGenerator != activeRouteArrowGenerator)
            routeArrowGenerator.HideRaceArrows();

        activeRouteArrowGenerator = null;
    }

    string FormatRaceTimeCompact(float timeSeconds)
    {
        timeSeconds = Mathf.Max(0f, timeSeconds);

        int minutes = Mathf.FloorToInt(timeSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeSeconds % 60f);
        int centiseconds = Mathf.FloorToInt((timeSeconds % 1f) * 100f);
        centiseconds = Mathf.Clamp(centiseconds, 0, 99);

        if (minutes > 0)
            return $"{minutes:00}:{seconds:00}:{centiseconds:00}";

        return $"{seconds:00}:{centiseconds:00}";
    }
    string FormatSignedDelta(float delta)
    {
        string sign = delta < 0f ? "-" : "+";
        return sign + FormatRaceTimeCompact(Mathf.Abs(delta));
    }

    void PrepareSpeedTrapRaceWorld()
    {
        if (raceMode != RaceMode.SpeedTrap)
            return;

        if (speedTrapVisualRoot != null)
            speedTrapVisualRoot.SetActive(true);

        if (minimapSpeedTrapIconManager != null)
        {
            minimapSpeedTrapIconManager.ClearIcons();

            for (int i = 0; i < speedTraps.Count; i++)
            {
                if (speedTraps[i] != null)
                    minimapSpeedTrapIconManager.RegisterSpeedTrap(speedTraps[i].transform);
            }
        }

        for (int i = 0; i < speedTraps.Count; i++)
        {
            if (speedTraps[i] == null)
                continue;

            speedTraps[i].raceManager = this;
            speedTraps[i].ResetTrap();
            speedTraps[i].gameObject.SetActive(true);
        }

        if (speedTrapSummaryText != null)
            speedTrapSummaryText.text = "0 KM/H";
    }

    void ClearGlobalRaceState()
    {
        IsRaceStarting = false;
        IsRaceLoading = false;

        if (ActiveRaceManager == this)
            ActiveRaceManager = null;

        if (CurrentPanelManager == this)
            CurrentPanelManager = null;

        if (RaceEventUIController.ActiveFinishRaceManager == this)
            RaceEventUIController.ActiveFinishRaceManager = null;
    }

    public static bool AnyRaceBusy =>
        IsRaceStarting ||
        IsRaceLoading ||
        (CurrentPanelManager != null && CurrentPanelManager.racePhase != RacePhase.Idle) ||
        (ActiveRaceManager != null && ActiveRaceManager.racePhase != RacePhase.Idle);
}

[System.Serializable]
public class RacerRuntimeInfo
{
    public CarControll car;
    public string driverName;
    public bool isPlayer;
    public int finishedPlace;

    public int currentLap = 1;
    public int currentSplitIndex = 0;

    public int speedTrapScore = 0;
    public bool speedTrapFinished = false;
    public float speedTrapFinishTime = -1f;

    public bool eliminated = false;
}