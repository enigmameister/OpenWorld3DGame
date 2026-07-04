using UnityEngine;

public class BankTransferMissionRuntime : MonoBehaviour, IMissionRuntime
{
    private enum BankMissionState
    {
        NotStarted,
        WaitingForAccount,
        WaitingForTransfer,
        ReadyToClaim,
        RewardClaimed
    }

    [Header("Definition")]
    [SerializeField] private BankTransferMissionDefinition definition;

    [Header("HUD Tracker")]
    [SerializeField] private MissionTrackerEntryUI trackerEntry;

    [Header("Dialogue Event Keys")]
    [SerializeField] private string acceptEventKey = "Mission_FredoBankTransfer_Accept";
    [SerializeField] private string declineEventKey = "Mission_FredoBankTransfer_Decline";
    [SerializeField] private string claimRewardEventKey = "Mission_FredoBankTransfer_ClaimReward";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private BankMissionState state = BankMissionState.NotStarted;
    private bool showOnScreenTracker = false;

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
                case BankMissionState.WaitingForAccount:
                case BankMissionState.WaitingForTransfer:
                    return MissionRuntimeState.Active;

                case BankMissionState.ReadyToClaim:
                    return MissionRuntimeState.ReadyToClaim;

                case BankMissionState.RewardClaimed:
                    return MissionRuntimeState.RewardClaimed;

                default:
                    return MissionRuntimeState.NotStarted;
            }
        }
    }

    private void OnEnable()
    {
        DialogueMissionEventRouter.OnDialogueEvent += HandleDialogueEvent;
        BankSystem.OnTransferCompleted += HandleTransferCompleted;
    }

    private void OnDisable()
    {
        DialogueMissionEventRouter.OnDialogueEvent -= HandleDialogueEvent;
        BankSystem.OnTransferCompleted -= HandleTransferCompleted;
    }

    private void Update()
    {
        if (state == BankMissionState.WaitingForAccount)
        {
            if (PlayerHasBankAccount())
            {
                state = BankMissionState.WaitingForTransfer;
                RefreshUI();
            }
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

        if (state != BankMissionState.NotStarted)
            return;

        EnsureTargetAccountExists();

        state = PlayerHasBankAccount()
            ? BankMissionState.WaitingForTransfer
            : BankMissionState.WaitingForAccount;

        showOnScreenTracker = false;

        if (CommunicateUI.Instance != null)
            CommunicateUI.Instance.Show(definition.acceptText, 5f);

        RefreshUI();

        if (debugLogs)
            Debug.Log($"[BankTransferMissionRuntime] Accepted: {MissionId}");
    }

    public void DeclineMission()
    {
        if (debugLogs)
            Debug.Log($"[BankTransferMissionRuntime] Declined: {MissionId}");
    }
    public void ClaimReward()
    {
        if (definition == null)
            return;

        if (state != BankMissionState.ReadyToClaim)
            return;

        GiveReward();

        state = BankMissionState.RewardClaimed;
        showOnScreenTracker = false;

        RefreshUI();
    }

    public void AbandonMission()
    {
        if (state == BankMissionState.NotStarted ||
            state == BankMissionState.RewardClaimed)
        {
            return;
        }

        state = BankMissionState.NotStarted;
        showOnScreenTracker = false;

        RefreshUI();
    }

    public void SetShowOnScreenTracker(bool visible)
    {
        showOnScreenTracker = visible;
        RefreshUI();
    }

    private void HandleTransferCompleted(int fromAccountId, int toAccountId, int amount)
    {
        if (definition == null)
            return;

        if (state != BankMissionState.WaitingForTransfer)
            return;

        if (toAccountId != definition.targetAccountId)
            return;

        if (amount < definition.requiredAmount)
            return;

        if (!IsPlayerAccount(fromAccountId))
            return;

        state = BankMissionState.ReadyToClaim;

        if (CommunicateUI.Instance != null)
            CommunicateUI.Instance.Show(definition.transferDoneText, 5f);

        RefreshUI();

        if (debugLogs)
            Debug.Log($"[BankTransferMissionRuntime] Transfer completed: {amount} -> {toAccountId}");
    }

    private bool PlayerHasBankAccount()
    {
        if (BankSystem.Instance == null)
            return false;

        PlayerStats playerStats = FindFirstObjectByType<PlayerStats>();

        if (playerStats == null)
            return false;

        if (string.IsNullOrWhiteSpace(playerStats.citizenId))
            return false;

        return BankSystem.Instance.TryGetAccountForCitizen(playerStats.citizenId, out _);
    }

    private bool IsPlayerAccount(int accountId)
    {
        if (BankSystem.Instance == null)
            return false;

        PlayerStats playerStats = FindFirstObjectByType<PlayerStats>();

        if (playerStats == null)
            return false;

        if (string.IsNullOrWhiteSpace(playerStats.citizenId))
            return false;

        if (!BankSystem.Instance.TryGetAccountForCitizen(playerStats.citizenId, out BankAccount account))
            return false;

        return account != null && account.accountId == accountId;
    }

    private void EnsureTargetAccountExists()
    {
        if (definition == null)
            return;

        if (!definition.createTargetAccountIfMissing)
            return;

        if (BankSystem.Instance == null)
            return;

        if (BankSystem.Instance.AccountExists(definition.targetAccountId))
            return;

        BankSystem.Instance.CreateAccount(0, definition.targetAccountId);

        if (debugLogs)
            Debug.Log($"[BankTransferMissionRuntime] Created target account: {definition.targetAccountId}");
    }

    private void GiveReward()
    {
        if (definition == null || definition.rewardItem == null)
            return;

        InventoryItemInstance reward = new InventoryItemInstance(definition.rewardItem);
        reward.count = Mathf.Max(1, definition.rewardItemCount);

        if (InventoryUI.Instance == null)
            return;

        bool addedToInventory = InventoryUI.Instance.TryGiveMissionRewardItemOrDrop(reward);

        if (CommunicateUI.Instance != null)
        {
            if (addedToInventory)
                CommunicateUI.Instance.Show(definition.rewardTextAfterClaim, 5f);
            else
                CommunicateUI.Instance.Show("Inventory full or weapon slot already occupied. Fredo dropped your reward nearby.", 5f);
        }
    }

    public ObjectiveEntryData BuildObjectiveEntry()
    {
        if (definition == null)
            return null;

        if (state == BankMissionState.NotStarted ||
            state == BankMissionState.RewardClaimed)
        {
            return null;
        }

        ObjectiveStatus status = RuntimeState == MissionRuntimeState.ReadyToClaim
            ? ObjectiveStatus.Finished
            : ObjectiveStatus.InProgress;

        bool canAbandon =
            state == BankMissionState.WaitingForAccount ||
            state == BankMissionState.WaitingForTransfer ||
            state == BankMissionState.ReadyToClaim;

        return new ObjectiveEntryData(
            MissionId,
            string.IsNullOrWhiteSpace(definition.title) ? "Bank Transfer" : definition.title,
            GetObjectiveText(),
            string.IsNullOrWhiteSpace(definition.description)
                ? "Complete the requested bank transfer."
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
            return "Bank transfer";

        if (state == BankMissionState.WaitingForAccount)
            return definition.accountMissingText;

        if (state == BankMissionState.WaitingForTransfer)
            return $"Transfer ${definition.requiredAmount} to account {definition.targetAccountId}.";

        if (state == BankMissionState.ReadyToClaim)
            return "Return to Fredo for your reward.";

        return "Bank transfer";
    }

    private void RefreshUI()
    {
        bool visible =
            showOnScreenTracker &&
            (state == BankMissionState.WaitingForAccount ||
             state == BankMissionState.WaitingForTransfer ||
             state == BankMissionState.ReadyToClaim);

        if (trackerEntry == null)
            return;

        trackerEntry.SetVisible(visible);

        if (!visible)
            return;

        trackerEntry.SetTitle(
            string.IsNullOrWhiteSpace(definition.title)
                ? "BANK TRANSFER"
                : definition.title
        );

        trackerEntry.SetSimpleText(GetObjectiveText());
        trackerEntry.SetRuntimeStatus(RuntimeState);
    }
}