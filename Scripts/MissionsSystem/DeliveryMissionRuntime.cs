using UnityEngine;

public class DeliveryMissionRuntime : MonoBehaviour, IMissionRuntime
{
    private enum DeliveryState
    {
        NotStarted,
        CarryingPackage,
        DeliveredToReceiver,
        ReadyToClaimAtGiver,
        RewardClaimed
    }

    [Header("Definition")]
    [SerializeField] private DeliveryMissionDefinition definition;

    [Header("HUD Tracker")]
    [SerializeField] private MissionTrackerEntryUI trackerEntry;

    [Header("Dialogue Event Keys")]
    [SerializeField] private string acceptEventKey = "Mission_FredoDelivery_Accept";
    [SerializeField] private string declineEventKey = "Mission_FredoDelivery_Decline";
    [SerializeField] private string deliverEventKey = "Mission_FredoDelivery_DeliverToRalph";
    [SerializeField] private string claimRewardEventKey = "Mission_FredoDelivery_ClaimReward";

    [Header("GPS")]
    [SerializeField] private WorldMapUI worldMapUI;
    [SerializeField] private Transform giverGpsTarget;
    [SerializeField] private Transform receiverGpsTarget;
    [SerializeField] private bool setGpsOnAccept = true;
    [SerializeField] private bool setGpsBackToGiverAfterDelivery = true;
    [SerializeField] private bool clearGpsOnComplete = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private DeliveryState state = DeliveryState.NotStarted;
    private bool showOnScreenTracker = false;
    private bool packageGivenOnce = false;

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
                case DeliveryState.CarryingPackage:
                case DeliveryState.DeliveredToReceiver:
                    return MissionRuntimeState.Active;

                case DeliveryState.ReadyToClaimAtGiver:
                    return MissionRuntimeState.ReadyToClaim;

                case DeliveryState.RewardClaimed:
                    return MissionRuntimeState.RewardClaimed;

                default:
                    return MissionRuntimeState.NotStarted;
            }
        }
    }

    private void OnEnable()
    {
        DialogueMissionEventRouter.OnDialogueEvent += HandleDialogueEvent;
    }

    private void OnDisable()
    {
        DialogueMissionEventRouter.OnDialogueEvent -= HandleDialogueEvent;
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

        if (eventKey == deliverEventKey)
        {
            TryDeliverToReceiver();
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

        if (state != DeliveryState.NotStarted)
            return;

        state = DeliveryState.CarryingPackage;
        showOnScreenTracker = false;

        GivePackageOnce();

        SetGpsToReceiver();

        if (CommunicateUI.Instance != null)
            CommunicateUI.Instance.Show(definition.acceptCommunicateText, 5f);

        RefreshUI();
    }

    public void DeclineMission()
    {
        if (debugLogs)
            Debug.Log($"[DeliveryMissionRuntime] Declined: {MissionId}");
    }

    public void ClaimReward()
    {
        if (definition == null)
            return;

        if (definition.rewardAtReceiver)
            return;

        if (state != DeliveryState.ReadyToClaimAtGiver)
            return;

        GiveReward();

        state = DeliveryState.RewardClaimed;
        showOnScreenTracker = false;

        ClearMissionGps();

        RefreshUI();
    }

    public void AbandonMission()
    {
        if (state == DeliveryState.NotStarted ||
            state == DeliveryState.RewardClaimed)
        {
            return;
        }

        RemovePackageFromInventory();

        showOnScreenTracker = false;
        packageGivenOnce = false;
        state = DeliveryState.NotStarted;

        ClearMissionGps();

        RefreshUI();
    }

    public void SetShowOnScreenTracker(bool visible)
    {
        showOnScreenTracker = visible;
        RefreshUI();
    }

    private void GivePackageOnce()
    {
        if (definition == null || definition.packageItem == null)
            return;

        if (packageGivenOnce)
            return;

        if (InventoryUI.Instance == null)
            return;

        InventoryItemInstance package = new InventoryItemInstance(definition.packageItem);
        package.count = 1;

        bool added = InventoryUI.Instance.TryAddItem(package);

        if (!added)
        {
            state = DeliveryState.NotStarted;

            if (CommunicateUI.Instance != null)
                CommunicateUI.Instance.Show("Inventory is full.", 5f);

            return;
        }

        packageGivenOnce = true;
    }

    private void TryDeliverToReceiver()
    {
        if (definition == null)
            return;

        if (state != DeliveryState.CarryingPackage)
            return;

        if (!HasPackageInInventory())
        {
            if (CommunicateUI.Instance != null)
                CommunicateUI.Instance.Show("You do not have the package.", 5f);

            RefreshUI();
            return;
        }

        RemovePackageFromInventory();

        if (definition.rewardAtReceiver)
        {
            GiveReward();

            state = DeliveryState.RewardClaimed;
            showOnScreenTracker = false;

            ClearMissionGps();

            if (CommunicateUI.Instance != null)
                CommunicateUI.Instance.Show(definition.deliveredCommunicateText, 5f);
        }
        else
        {
            state = DeliveryState.ReadyToClaimAtGiver;

            SetGpsToGiver();

            if (CommunicateUI.Instance != null)
                CommunicateUI.Instance.Show(definition.returnCommunicateText, 5f);
        }

        RefreshUI();

        if (debugLogs)
            Debug.Log($"[DeliveryMissionRuntime] Delivered package: {MissionId}");
    }

    private bool HasPackageInInventory()
    {
        if (definition == null || definition.packageItem == null)
            return false;

        if (InventoryUI.Instance == null)
            return false;

        return InventoryUI.Instance.GetTotalCountForData(definition.packageItem) > 0;
    }

    private void RemovePackageFromInventory()
    {
        if (definition == null || definition.packageItem == null)
            return;

        if (InventoryUI.Instance == null)
            return;

        InventoryItemInstance item =
            InventoryUI.Instance.GetFirstInstanceForData(definition.packageItem);

        while (item != null)
        {
            InventoryUI.Instance.RemoveItem(item, item.count <= 0 ? 1 : item.count);
            item = InventoryUI.Instance.GetFirstInstanceForData(definition.packageItem);
        }
    }

    private void GiveReward()
    {
        PlayerStats playerStats = FindFirstObjectByType<PlayerStats>();

        if (playerStats != null)
            playerStats.AddMoneySmooth(definition.rewardMoney);
    }

    public ObjectiveEntryData BuildObjectiveEntry()
    {
        if (definition == null)
            return null;

        if (state == DeliveryState.NotStarted ||
            state == DeliveryState.RewardClaimed)
        {
            return null;
        }

        ObjectiveStatus status = ObjectiveStatus.InProgress;

        if (state == DeliveryState.ReadyToClaimAtGiver)
            status = ObjectiveStatus.Finished;

        string objective = GetObjectiveText();

        bool canAbandon =
            state == DeliveryState.CarryingPackage ||
            state == DeliveryState.DeliveredToReceiver ||
            state == DeliveryState.ReadyToClaimAtGiver;

        return new ObjectiveEntryData(
            MissionId,
            string.IsNullOrWhiteSpace(definition.title) ? "Delivery" : definition.title,
            objective,
            string.IsNullOrWhiteSpace(definition.description)
                ? "Deliver the package."
                : definition.description,
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
            return "Delivery";

        if (state == DeliveryState.CarryingPackage)
        {
            if (HasPackageInInventory())
                return string.IsNullOrWhiteSpace(definition.objectiveText)
                    ? "Deliver the package to Ralph."
                    : definition.objectiveText;

            return "Find the lost package.";
        }

        if (state == DeliveryState.ReadyToClaimAtGiver)
            return "Return to Fredo for your reward.";

        return string.IsNullOrWhiteSpace(definition.objectiveText)
            ? "Delivery"
            : definition.objectiveText;
    }

    public DialogueGraph ResolveReceiverGraph()
    {
        if (definition == null)
            return null;

        if (state == DeliveryState.CarryingPackage)
        {
            if (HasPackageInInventory())
                return definition.receiverWaitingGraph;

            return definition.receiverMissingPackageGraph != null
                ? definition.receiverMissingPackageGraph
                : definition.receiverWaitingGraph;
        }

        if (state == DeliveryState.RewardClaimed ||
            state == DeliveryState.ReadyToClaimAtGiver)
        {
            return definition.receiverDeliveredGraph != null
                ? definition.receiverDeliveredGraph
                : definition.completedGraph;
        }

        return definition.receiverMissingPackageGraph;
    }

    private void RefreshUI()
    {
        bool visible =
            showOnScreenTracker &&
            (state == DeliveryState.CarryingPackage ||
             state == DeliveryState.ReadyToClaimAtGiver);

        if (trackerEntry != null)
        {
            trackerEntry.SetVisible(visible);

            if (visible)
            {
                trackerEntry.SetTitle(
                    string.IsNullOrWhiteSpace(definition.title)
                        ? "DELIVERY"
                        : definition.title
                );

                string trackerText = GetTrackerText();

                trackerEntry.SetSimpleText(trackerText);
                trackerEntry.SetRuntimeStatus(RuntimeState);
            }
        }
    }

    private string GetTrackerText()
    {
        if (definition == null)
            return "Delivery";

        if (state == DeliveryState.CarryingPackage)
        {
            if (HasPackageInInventory())
            {
                return string.IsNullOrWhiteSpace(definition.objectiveText)
                    ? "Deliver Fredo's package to Ralph."
                    : definition.objectiveText;
            }

            return "Package missing";
        }

        if (state == DeliveryState.ReadyToClaimAtGiver)
            return "Return to Fredo for reward";

        if (state == DeliveryState.RewardClaimed)
            return "Delivery complete";

        return string.IsNullOrWhiteSpace(definition.objectiveText)
            ? "Delivery"
            : definition.objectiveText;
    }

    private void SetGpsToReceiver()
    {
        if (!setGpsOnAccept)
            return;

        if (worldMapUI == null)
            worldMapUI = FindFirstObjectByType<WorldMapUI>();

        if (worldMapUI == null || receiverGpsTarget == null)
            return;

        worldMapUI.FollowGpsToTransform(receiverGpsTarget, "Deliver package to Ralph");
    }

    private void SetGpsToGiver()
    {
        if (!setGpsBackToGiverAfterDelivery)
            return;

        if (worldMapUI == null)
            worldMapUI = FindFirstObjectByType<WorldMapUI>();

        if (worldMapUI == null || giverGpsTarget == null)
            return;

        worldMapUI.FollowGpsToTransform(giverGpsTarget, "Return to Fredo");
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
}