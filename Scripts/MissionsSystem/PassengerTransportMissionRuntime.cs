using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PassengerTransportMissionRuntime : MonoBehaviour, IMissionRuntime
{
    private enum PassengerState
    {
        NotStarted,
        NeedCar,
        DriveToPassenger,
        WaitingForPassengerPickup,
        PassengerWalkingToCar,
        DrivingToDestination,
        PlayerLeftCarDuringRide,
        ArrivedAtDestination,
        PassengerWalkingAway,
        ReadyToClaimAtGiver,
        RewardClaimed
    }

    [Header("Definition")]
    [SerializeField] private PassengerTransportMissionDefinition definition;

    [Header("Mission NPCs")]
    [SerializeField] private NPCMissionGiver giverNpc;
    [SerializeField] private NPCMissionGiver passengerNpcNameSource;
    [SerializeField] private MissionPassengerNpc passengerNpc;

    [Header("Dialogue Event Keys")]
    [SerializeField] private string acceptEventKey = "Mission_PassengerTransport_Accept";
    [SerializeField] private string declineEventKey = "Mission_PassengerTransport_Decline";
    [SerializeField] private string claimRewardEventKey = "Mission_PassengerTransport_ClaimReward";

    [Header("Targets")]
    [SerializeField] private Transform passengerPickupTarget;
    [SerializeField] private Transform destinationTarget;
    [SerializeField] private Transform passengerExitPoint;
    [SerializeField] private Transform passengerWalkTarget;
    [SerializeField] private Transform giverGpsTarget;

    [Header("Car / Pickup")]
    [SerializeField] private float pickupDistance = 8f;
    [SerializeField] private float destinationDistance = 8f;
    [SerializeField] private Transform currentVehicleRoot;
    [SerializeField] private Transform currentVehicleEnterTarget;

    [Header("GPS")]
    [SerializeField] private WorldMapUI worldMapUI;
    [SerializeField] private bool setGpsToPassengerAfterEnteringCar = true;
    [SerializeField] private bool clearGpsOnComplete = true;

    [Header("HUD Tracker")]
    [SerializeField] private MissionTrackerEntryUI trackerEntry;

    [Header("Ride Dialogue")]
    [SerializeField] private PassengerRideDialoguePlayer rideDialoguePlayer;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private PassengerState state = PassengerState.NotStarted;
    private bool showOnScreenTracker = false;
    private bool playerInVehicle = false;
    private bool passengerBoarded = false;
    private bool warnedAfterExit = false;
    private bool arrivalSequenceRunning = false;
    private bool rewardPendingDetached = false;

    public string MissionId =>
        definition != null ? definition.missionId : "";

    public MissionDefinition Definition => definition;

    public bool ShowOnScreenTracker => showOnScreenTracker;

    public MissionRuntimeState RuntimeState
    {
        get
        {
            switch (state)
            {
                case PassengerState.NeedCar:
                case PassengerState.DriveToPassenger:
                case PassengerState.WaitingForPassengerPickup:
                case PassengerState.PassengerWalkingToCar:
                case PassengerState.DrivingToDestination:
                case PassengerState.PlayerLeftCarDuringRide:
                case PassengerState.ArrivedAtDestination:
                case PassengerState.PassengerWalkingAway:
                    return MissionRuntimeState.Active;

                case PassengerState.ReadyToClaimAtGiver:
                    return MissionRuntimeState.ReadyToClaim;

                case PassengerState.RewardClaimed:
                    return MissionRuntimeState.RewardClaimed;

                default:
                    return MissionRuntimeState.NotStarted;
            }
        }
    }

    public string GiverName
    {
        get
        {
            if (giverNpc != null && !string.IsNullOrWhiteSpace(giverNpc.NpcName))
                return giverNpc.NpcName;

            return "Mission giver";
        }
    }

    public string PassengerName
    {
        get
        {
            if (definition != null && !definition.usePassengerNpcName)
            {
                return string.IsNullOrWhiteSpace(definition.passengerDisplayName)
                    ? "Passenger"
                    : definition.passengerDisplayName;
            }

            if (passengerNpcNameSource != null && !string.IsNullOrWhiteSpace(passengerNpcNameSource.NpcName))
                return passengerNpcNameSource.NpcName;

            if (definition != null && !string.IsNullOrWhiteSpace(definition.passengerDisplayName))
                return definition.passengerDisplayName;

            return "Passenger";
        }
    }

    private void Awake()
    {
        if (passengerNpc != null)
            passengerNpc.BindRuntime(this);

        if (rideDialoguePlayer == null)
            rideDialoguePlayer = GetComponent<PassengerRideDialoguePlayer>();
    }

    private void OnEnable()
    {
        DialogueMissionEventRouter.OnDialogueEvent += HandleDialogueEvent;

        CarInteraction.OnAnyPlayerEnteredCar += HandlePlayerEnteredCar;
        CarInteraction.OnAnyPlayerExitedCar += HandlePlayerExitedCar;
    }

    private void OnDisable()
    {
        DialogueMissionEventRouter.OnDialogueEvent -= HandleDialogueEvent;

        CarInteraction.OnAnyPlayerEnteredCar -= HandlePlayerEnteredCar;
        CarInteraction.OnAnyPlayerExitedCar -= HandlePlayerExitedCar;
    }

    private void Update()
    {
        if (state == PassengerState.DriveToPassenger ||
            state == PassengerState.WaitingForPassengerPickup)
        {
            TickPassengerPickup();
        }

        if (state == PassengerState.DrivingToDestination)
        {
            TickDestinationArrival();
        }
    }

    private void HandleDialogueEvent(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            return;

        if (eventKey == acceptEventKey)
        {
            AcceptMission();
            return;
        }

        if (eventKey == declineEventKey)
        {
            DeclineMission();
            return;
        }

        if (eventKey == claimRewardEventKey)
        {
            ClaimReward();
            return;
        }
    }

    public void AcceptMission()
    {
        if (definition == null)
            return;

        if (state == PassengerState.ReadyToClaimAtGiver)
        {
            if (CommunicateUI.Instance != null)
            {
                CommunicateUI.Instance.Show(
                    FormatMissionText("You already finished the job. Talk to {GIVER} to collect your reward."),
                    5f
                );
            }

            return;
        }

        if (state != PassengerState.NotStarted)
            return;

        rewardPendingDetached = false;

        state = PassengerState.NeedCar;

        CarInteraction activeCar = CarInteraction.ActiveCarInteraction;

        if (activeCar != null && activeCar.IsPlayerInThisCar)
        {
            NotifyPlayerEnteredVehicle(
                activeCar.VehicleRoot,
                activeCar.VehicleEnterTarget
            );
        }

        showOnScreenTracker = false;
        passengerBoarded = false;
        warnedAfterExit = false;

        if (passengerNpc != null)
            passengerNpc.ResetPassenger();

        ShowCommunicate(definition.acceptTextFormat);

        RefreshUI();
    }

    public void DeclineMission()
    {
        if (debugLogs)
            Debug.Log($"[PassengerTransportMissionRuntime] Declined: {MissionId}");
    }

    public void ClaimReward()
    {
        if (definition == null)
            return;

        if (state != PassengerState.ReadyToClaimAtGiver)
            return;

        GiveReward();

        state = PassengerState.RewardClaimed;
        rewardPendingDetached = false;
        showOnScreenTracker = false;

        ClearMissionGps();

        RefreshUI();
    }

    public void AbandonMission()
    {
        if (state == PassengerState.NotStarted ||
            state == PassengerState.RewardClaimed)
        {
            return;
        }

        // Wolf zosta³ ju¿ odwieziony albo w³aœnie odchodzi do punktu koñcowego.
        // Gracz anuluje œledzenie misji, ale nagroda nadal zostaje do odebrania u Fredo.
        if (state == PassengerState.PassengerWalkingAway ||
            state == PassengerState.ReadyToClaimAtGiver)
        {
            rewardPendingDetached = true;
            showOnScreenTracker = false;

            ClearMissionGps();

            if (rideDialoguePlayer != null)
                rideDialoguePlayer.StopDialogue();

            if (CommunicateUI.Instance != null)
            {
                CommunicateUI.Instance.Show(
                    FormatMissionText("Job finished. Your reward is waiting with {GIVER}."),
                    5f
                );
            }

            RefreshUI();
            return;
        }

        // Normalne porzucenie przed dowiezieniem pasa¿era.
        state = PassengerState.NotStarted;
        rewardPendingDetached = false;
        showOnScreenTracker = false;
        playerInVehicle = false;
        passengerBoarded = false;
        warnedAfterExit = false;
        arrivalSequenceRunning = false;

        if (passengerNpc != null)
            passengerNpc.ResetPassenger();

        if (rideDialoguePlayer != null)
            rideDialoguePlayer.StopDialogue();

        ClearMissionGps();

        if (CommunicateUI.Instance != null)
            CommunicateUI.Instance.Show("Mission abandoned.", 4f);

        RefreshUI();
    }

    public void SetShowOnScreenTracker(bool visible)
    {
        showOnScreenTracker = visible;
        RefreshUI();
    }

    public void NotifyPlayerEnteredVehicle(Transform vehicleRoot, Transform vehicleEnterTarget)
    {
        currentVehicleRoot = vehicleRoot;
        currentVehicleEnterTarget = vehicleEnterTarget;
        playerInVehicle = true;

        if (state == PassengerState.NeedCar)
        {
            state = PassengerState.DriveToPassenger;

            ShowCommunicate(definition.enteredCarTextFormat);

            if (setGpsToPassengerAfterEnteringCar)
                SetGpsToPassenger();

            RefreshUI();
            return;
        }

        if (state == PassengerState.PlayerLeftCarDuringRide && passengerBoarded)
        {
            state = PassengerState.DrivingToDestination;
            warnedAfterExit = false;

            if (rideDialoguePlayer != null)
                rideDialoguePlayer.Resume();

            ShowCommunicate(definition.playerReturnedToCarTextFormat);

            SetGpsToDestination();

            RefreshUI();
        }
    }

    public void NotifyPlayerExitedVehicle(Transform vehicleRoot)
    {
        playerInVehicle = false;

        if (state != PassengerState.DrivingToDestination)
            return;

        state = PassengerState.PlayerLeftCarDuringRide;

        if (rideDialoguePlayer != null)
        {
            rideDialoguePlayer.PauseAndHide();

            if (!warnedAfterExit)
            {
                rideDialoguePlayer.ShowTemporaryLine(
                    PassengerName,
                    FormatMissionText(definition.passengerLineWhenPlayerLeavesCar),
                    4f
                );
            }
        }

        warnedAfterExit = true;

        ShowCommunicate(definition.playerLeftCarTextFormat);

        RefreshUI();
    }

    public void NotifyPassengerBoarded()
    {
        if (state != PassengerState.PassengerWalkingToCar)
            return;

        passengerBoarded = true;
        state = PassengerState.DrivingToDestination;

        SetGpsToDestination();

        ShowCommunicate(definition.passengerBoardedTextFormat);

        if (rideDialoguePlayer != null)
            rideDialoguePlayer.StartDialogue(definition, this);

        RefreshUI();
    }

    public void NotifyPassengerWalkAwayFinished()
    {
        if (state != PassengerState.PassengerWalkingAway)
            return;

        if (definition.rewardAtDestination)
        {
            GiveReward();

            state = PassengerState.RewardClaimed;
            showOnScreenTracker = false;

            ClearMissionGps();
        }
        else
        {
            state = PassengerState.ReadyToClaimAtGiver;

            if (!rewardPendingDetached)
            {
                SetGpsToGiver();
                ShowCommunicate(definition.returnTextFormat);
            }
        }

        RefreshUI();
    }

    private void TickPassengerPickup()
    {
        if (!playerInVehicle)
            return;

        if (currentVehicleRoot == null)
            return;

        Transform pickup = passengerPickupTarget != null
            ? passengerPickupTarget
            : passengerNpc != null ? passengerNpc.transform : null;

        if (pickup == null)
            return;

        float dist = Vector3.Distance(currentVehicleRoot.position, pickup.position);

        if (dist > pickupDistance)
            return;

        state = PassengerState.PassengerWalkingToCar;

        if (passengerNpc != null)
        {
            Transform boardTarget = currentVehicleEnterTarget != null
                ? currentVehicleEnterTarget
                : currentVehicleRoot;

            passengerNpc.WalkToCarAndBoard(boardTarget);
        }
        else
        {
            NotifyPassengerBoarded();
        }

        RefreshUI();
    }

    private void TickDestinationArrival()
    {

        if (!playerInVehicle)
            return;

        if (currentVehicleRoot == null || destinationTarget == null)
            return;

        if (arrivalSequenceRunning)
            return;

        float dist = Vector3.Distance(currentVehicleRoot.position, destinationTarget.position);

        if (dist > destinationDistance)
            return;

        StartCoroutine(CoDestinationArrival());
    }

    private IEnumerator CoDestinationArrival()
    {
        arrivalSequenceRunning = true;

        state = PassengerState.ArrivedAtDestination;
        RefreshUI();

        if (rideDialoguePlayer != null)
        {
            // Zatrzymaj tylko g³ówn¹ sekwencjê rozmowy,
            // ale nie zamykaj UI przed koñcow¹ lini¹ Wolfa.
            rideDialoguePlayer.StopRoutineOnly();

            yield return rideDialoguePlayer.ShowTemporaryLineAndWait(
                PassengerName,
                FormatMissionText(definition.arrivedTextFormat),
                2.5f
            );

            rideDialoguePlayer.StopDialogue();
        }

        // Tutaj dopiero Wolf wysiada.
        state = PassengerState.PassengerWalkingAway;

        if (passengerNpc != null)
        {
            Transform exitPoint = passengerExitPoint != null
                ? passengerExitPoint
                : currentVehicleEnterTarget;

            passengerNpc.ExitCarAndWalkAway(exitPoint, passengerWalkTarget);
        }
        else
        {
            NotifyPassengerWalkAwayFinished();
        }

        RefreshUI();

        arrivalSequenceRunning = false;
    }

    private void GiveReward()
    {
        if (definition == null)
            return;

        bool gaveAnything = false;

        if (definition.rewardMoney > 0)
        {
            PlayerStats playerStats = FindFirstObjectByType<PlayerStats>();

            if (playerStats != null)
            {
                playerStats.AddMoneySmooth(definition.rewardMoney);
                gaveAnything = true;
            }
        }

        if (definition.rewardItem != null)
        {
            InventoryItemInstance rewardInstance = new InventoryItemInstance(definition.rewardItem);
            rewardInstance.count = Mathf.Max(1, definition.rewardItemCount);

            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance.TryGiveMissionRewardItemOrDrop(rewardInstance);
                gaveAnything = true;
            }
        }

        if (gaveAnything)
            ShowCommunicate(definition.rewardTextAfterClaimFormat);
        else
            ShowCommunicate(definition.noRewardTextAfterClaimFormat);
    }

    public ObjectiveEntryData BuildObjectiveEntry()
    {
        if (definition == null)
            return null;

        if (state == PassengerState.NotStarted ||
            state == PassengerState.RewardClaimed ||
            rewardPendingDetached)
        {
            return null;
        }

        ObjectiveStatus status = RuntimeState == MissionRuntimeState.ReadyToClaim
            ? ObjectiveStatus.Finished
            : ObjectiveStatus.InProgress;

        bool canAbandon =
            state != PassengerState.NotStarted &&
            state != PassengerState.RewardClaimed;

        string missionName = FormatMissionText(
            string.IsNullOrWhiteSpace(definition.title)
                ? "Passenger Transport"
                : definition.title
        );

        string description = string.IsNullOrWhiteSpace(definition.description)
            ? "Transport the passenger to the destination."
            : FormatMissionText(definition.description);

        return new ObjectiveEntryData(
            MissionId,
            missionName,
            GetObjectiveText(),
            description,
            status,
            canAbandon,
            AbandonMission,
            showOnScreenTracker,
            SetShowOnScreenTracker
        );
    }

    private string GetObjectiveText()
    {
        if (definition == null)
            return "Passenger transport";

        switch (state)
        {
            case PassengerState.NeedCar:
                return FormatMissionText(definition.needCarObjectiveFormat);

            case PassengerState.DriveToPassenger:
                return FormatMissionText(definition.driveToPassengerObjectiveFormat);

            case PassengerState.WaitingForPassengerPickup:
                return FormatMissionText(definition.waitForPassengerObjectiveFormat);

            case PassengerState.PassengerWalkingToCar:
                return FormatMissionText(definition.waitForPassengerObjectiveFormat);

            case PassengerState.DrivingToDestination:
                return FormatMissionText(definition.driveToDestinationObjectiveFormat);

            case PassengerState.PlayerLeftCarDuringRide:
                return FormatMissionText(definition.playerLeftCarObjectiveFormat);

            case PassengerState.PassengerWalkingAway:
                return FormatMissionText(definition.passengerWalkingAwayObjectiveFormat);

            case PassengerState.ReadyToClaimAtGiver:
                return FormatMissionText(definition.returnObjectiveFormat);

            default:
                return "Passenger transport";
        }
    }

    private void RefreshUI()
    {
        bool visible =
            showOnScreenTracker &&
            state != PassengerState.NotStarted &&
            state != PassengerState.RewardClaimed;

        if (trackerEntry == null)
            return;

        trackerEntry.SetVisible(visible);

        if (!visible)
            return;

        trackerEntry.SetTitle(
            FormatMissionText(
                string.IsNullOrWhiteSpace(definition.title)
                    ? "PASSENGER TRANSPORT"
                    : definition.title
            )
        );

        trackerEntry.SetSimpleText(GetObjectiveText());
        trackerEntry.SetRuntimeStatus(RuntimeState);
    }

    private void SetGpsToPassenger()
    {
        if (worldMapUI == null)
            worldMapUI = FindFirstObjectByType<WorldMapUI>();

        if (worldMapUI == null)
            return;

        Transform target = passengerPickupTarget != null
            ? passengerPickupTarget
            : passengerNpc != null ? passengerNpc.transform : null;

        if (target == null)
            return;

        worldMapUI.FollowGpsToTransform(
            target,
            FormatMissionText(definition.pickupGpsLabelFormat)
        );
    }

    private void SetGpsToDestination()
    {
        if (worldMapUI == null)
            worldMapUI = FindFirstObjectByType<WorldMapUI>();

        if (worldMapUI == null || destinationTarget == null)
            return;

        worldMapUI.FollowGpsToTransform(
            destinationTarget,
            FormatMissionText(definition.destinationGpsLabelFormat)
        );
    }

    private void SetGpsToGiver()
    {
        if (worldMapUI == null)
            worldMapUI = FindFirstObjectByType<WorldMapUI>();

        if (worldMapUI == null || giverGpsTarget == null)
            return;

        worldMapUI.FollowGpsToTransform(
            giverGpsTarget,
            FormatMissionText(definition.returnGpsLabelFormat)
        );
    }

    private void ClearMissionGps()
    {
        if (!clearGpsOnComplete)
            return;

        if (worldMapUI == null)
            worldMapUI = FindFirstObjectByType<WorldMapUI>();

        if (worldMapUI == null)
            return;

        worldMapUI.ClearGpsPublic();
    }

    private void ShowCommunicate(string template)
    {
        if (definition == null)
            return;

        if (CommunicateUI.Instance != null)
            CommunicateUI.Instance.Show(FormatMissionText(template), 5f);
    }

    public string FormatMissionText(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return "";

        int reward = definition != null ? definition.rewardMoney : 0;

        return template
            .Replace("{PASSENGER}", PassengerName)
            .Replace("{GIVER}", GiverName)
            .Replace("{REWARD}", reward.ToString());
    }

    public string ResolveSpeakerName(PassengerRideDialogueLine line)
    {
        if (line == null)
            return "";

        switch (line.speaker)
        {
            case PassengerDialogueSpeaker.Passenger:
                return PassengerName;

            case PassengerDialogueSpeaker.Player:
                return "Player";

            case PassengerDialogueSpeaker.Giver:
                return GiverName;

            case PassengerDialogueSpeaker.Custom:
                return string.IsNullOrWhiteSpace(line.customSpeakerName)
                    ? "Unknown"
                    : FormatMissionText(line.customSpeakerName);

            default:
                return "";
        }
    }

    private void HandlePlayerEnteredCar(CarInteraction car)
    {
        if (car == null)
            return;

        NotifyPlayerEnteredVehicle(
            car.VehicleRoot,
            car.VehicleEnterTarget
        );
    }

    private void HandlePlayerExitedCar(CarInteraction car)
    {
        if (car == null)
            return;

        NotifyPlayerExitedVehicle(car.VehicleRoot);
    }

    public void NotifyPassengerBoardingCancelled()
    {
        if (state != PassengerState.PassengerWalkingToCar)
            return;

        state = PassengerState.WaitingForPassengerPickup;

        if (CommunicateUI.Instance != null)
            CommunicateUI.Instance.Show(
                FormatMissionText("Stop close to {PASSENGER} and wait until they get in."),
                4f
            );

        RefreshUI();
    }
}