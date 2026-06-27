using TMPro;
using UnityEngine;

public class KillArmedNPCMission : MonoBehaviour
{
    public static KillArmedNPCMission Instance { get; private set; }

    public enum MissionState
    {
        NotStarted,
        Active,
        ReadyToClaim,
        RewardClaimed
    }
    [Header("Mission Tracker Entry")]
    [SerializeField] private MissionTrackerEntryUI trackerEntry;

    [Header("Mission")]
    [SerializeField] private int requiredScore = 30;
    [SerializeField] private MissionState state = MissionState.NotStarted;

    [Header("Runtime")]
    [SerializeField] private int armedNpcScore = 0;
    [SerializeField] private int innocentNpcKilled = 0;

    [Header("Reward")]
    [SerializeField] private TestHouseDoorAccessController doorAccess;

    [Header("Mission UI")]
    [SerializeField] private GameObject missionRoot;
    [SerializeField] private TMP_Text armedNpcValueText;
    [SerializeField] private TMP_Text innocentNpcValueText;
    [SerializeField] private TMP_Text resultValueText;
    [SerializeField] private TMP_Text headerText;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    public MissionState State => state;
    public bool IsActive => state == MissionState.Active;
    public bool IsReadyToClaim => state == MissionState.ReadyToClaim;
    public bool HasReward => state == MissionState.RewardClaimed;

    public int ArmedNpcScore => armedNpcScore;
    public int InnocentNpcKilled => innocentNpcKilled;
    public int ResultScore => armedNpcScore;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[KillArmedNPCMission] Duplicate found. Destroying this instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RefreshUI();
    }

    private void OnEnable()
    {
        NPCController.OnNPCDied += OnNpcDied;
        DialogueMissionEventRouter.OnDialogueEvent += HandleDialogueEvent;
        NPCMelee.OnMeleeNPCDied += OnMeleeNpcDied;
    }

    private void OnDisable()
    {
        NPCController.OnNPCDied -= OnNpcDied;
        DialogueMissionEventRouter.OnDialogueEvent -= HandleDialogueEvent;
        NPCMelee.OnMeleeNPCDied -= OnMeleeNpcDied;
    }

    private void HandleDialogueEvent(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            return;

        switch (eventKey)
        {
            case "Mission_TestHouse_Accept":
                AcceptMission();
                break;

            case "Mission_TestHouse_Decline":
                DeclineMission();
                break;

            case "Mission_TestHouse_ClaimReward":
                ClaimReward();
                break;
        }
    }

    public void AcceptMission()
    {
        if (state != MissionState.NotStarted)
            return;

        state = MissionState.Active;
        armedNpcScore = 0;
        innocentNpcKilled = 0;

        if (debugLogs)
            Debug.Log("[KillArmedNPCMission] Mission accepted.");

        RefreshUI();
    }

    public void DeclineMission()
    {
        if (debugLogs)
            Debug.Log("[KillArmedNPCMission] Mission declined.");

        RefreshUI();
    }

    public void ClaimReward()
    {
        if (state != MissionState.ReadyToClaim)
        {
            if (debugLogs)
                Debug.Log("[KillArmedNPCMission] Reward not ready yet.");

            return;
        }

        state = MissionState.RewardClaimed;

        if (doorAccess != null)
            doorAccess.UnlockAccess();

        RefreshUI();
    }

    private void OnNpcDied(NPCController deadNpc, string attackerName)
    {
        if (deadNpc == null)
            return;

        if (state != MissionState.Active)
            return;

        NPCController.NPCReactionType type = deadNpc.GetReactionType();

        if (type == NPCController.NPCReactionType.Aggressive ||
            type == NPCController.NPCReactionType.Fighter)
        {
            armedNpcScore += 1;
        }
        else if (type == NPCController.NPCReactionType.Coward)
        {
            innocentNpcKilled += 1;
            armedNpcScore -= 1;
        }

        if (armedNpcScore >= requiredScore)
        {
            armedNpcScore = requiredScore;
            state = MissionState.ReadyToClaim;

            if (debugLogs)
                Debug.Log("[KillArmedNPCMission] Mission completed. Return to Story NPC.");
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        bool showMission =
            state == MissionState.Active ||
            state == MissionState.ReadyToClaim;

        if (missionRoot != null)
            missionRoot.SetActive(showMission);

        if (headerText != null)
        {
            if (state == MissionState.Active)
                headerText.text = "ELIMINATE ARMED NPCS";
            else if (state == MissionState.ReadyToClaim)
                headerText.text = "RETURN TO STORY NPC";
            else if (state == MissionState.RewardClaimed)
                headerText.text = "MISSION COMPLETE";
        }

        if (armedNpcValueText != null)
            armedNpcValueText.text = armedNpcScore.ToString();

        if (innocentNpcValueText != null)
            innocentNpcValueText.text = innocentNpcKilled.ToString();

        if (resultValueText != null)
        {
            if (state == MissionState.ReadyToClaim)
                resultValueText.text = "READY";
            else if (state == MissionState.RewardClaimed)
                resultValueText.text = "PASS";
            else
                resultValueText.text = $"{armedNpcScore}/{requiredScore}";
        }

        bool showTracker =
            state == MissionState.Active ||
            state == MissionState.ReadyToClaim;

        if (trackerEntry != null)
        {
            trackerEntry.SetVisible(showTracker);

            if (showTracker)
            {
                trackerEntry.SetTitle("ELIMINATE ARMED NPCS");
                trackerEntry.SetProgress(armedNpcScore, innocentNpcKilled, armedNpcScore, requiredScore);
                trackerEntry.SetStatus(state);
            }
        }
    }

    private void OnMeleeNpcDied(NPCMelee deadNpc, string attackerName)
    {
        if (deadNpc == null)
            return;

        if (state != MissionState.Active)
            return;

        armedNpcScore += 1;

        if (armedNpcScore >= requiredScore)
        {
            armedNpcScore = requiredScore;
            state = MissionState.ReadyToClaim;

            if (debugLogs)
                Debug.Log("[KillArmedNPCMission] Mission completed. Return to Story NPC.");
        }

        RefreshUI();
    }
}