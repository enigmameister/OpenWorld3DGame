using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCMissionListUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Header")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private Button closeButton;

    [Header("List")]
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private MissionListEntryUI entryPrefab;

    [Header("Player Lock")]
    [SerializeField] private bool lockPlayerWhenOpen = true;

    [Header("Window Position")]
    [SerializeField] private RectTransform windowRoot;
    [SerializeField] private bool resetPositionOnOpen = true;

    private Vector2 startAnchoredPosition;
    private bool startPositionCached;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private readonly List<MissionListEntryUI> spawnedEntries = new();

    private NPCMissionGiver currentGiver;
    private NPCDialogueGraphInteractor currentInteractor;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (windowRoot == null && root != null)
            windowRoot = root.GetComponent<RectTransform>();

        if (windowRoot != null)
        {
            startAnchoredPosition = windowRoot.anchoredPosition;
            startPositionCached = true;
        }

        CloseImmediate();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }
    }

    public void Open(NPCMissionGiver giver, NPCDialogueGraphInteractor interactor)
    {
        if (giver == null)
            return;

        if (!HasVisibleMissions(giver))
        {
            if (debugLogs)
                Debug.Log($"[NPCMissionListUI] No visible missions for {giver.NpcName}");

            return;
        }

        currentGiver = giver;
        currentInteractor = interactor;

        IsOpen = true;

        if (root != null)
            root.SetActive(true);

        if (resetPositionOnOpen && startPositionCached && windowRoot != null)
            windowRoot.anchoredPosition = startAnchoredPosition;

        if (headerText != null)
            headerText.text = string.IsNullOrWhiteSpace(giver.NpcName) ? "NPC NAME" : giver.NpcName;

        BuildList();

        if (lockPlayerWhenOpen)
            LockPlayer();

        if (debugLogs)
            Debug.Log($"[NPCMissionListUI] Opened mission list for {giver.NpcName}");
    }

    public void Close()
    {
        Close(unlockPlayer: true);
    }

    public void Close(bool unlockPlayer)
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        if (root != null)
            root.SetActive(false);

        ClearList();

        if (unlockPlayer)
            UnlockPlayer();

        currentGiver = null;
        currentInteractor = null;
    }

    public void CloseImmediate()
    {
        IsOpen = false;

        if (root != null)
            root.SetActive(false);

        ClearList();

        currentGiver = null;
        currentInteractor = null;
    }

    private void BuildList()
    {
        ClearList();

        if (currentGiver == null || currentGiver.Missions == null)
            return;

        int visibleIndex = 0;

        for (int i = 0; i < currentGiver.Missions.Length; i++)
        {
            MissionDefinition mission = currentGiver.Missions[i];

            if (mission == null)
                continue;

            MissionRuntimeState state = GetMissionState(mission);

            if (ShouldHideMission(mission, state))
                continue;

            MissionListEntryUI entry = Instantiate(entryPrefab, listRoot);
            entry.gameObject.SetActive(true);

            entry.Setup(mission, this, visibleIndex, state);

            spawnedEntries.Add(entry);
            visibleIndex++;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);
    }

    private void ClearList()
    {
        for (int i = 0; i < spawnedEntries.Count; i++)
        {
            if (spawnedEntries[i] != null)
                Destroy(spawnedEntries[i].gameObject);
        }

        spawnedEntries.Clear();
    }

    private MissionRuntimeState GetMissionState(MissionDefinition mission)
    {
        if (MissionCoordinator.Instance == null || mission == null)
            return MissionRuntimeState.NotStarted;

        return MissionCoordinator.Instance.GetMissionState(mission.missionId);
    }

    private bool ShouldHideMission(MissionDefinition mission, MissionRuntimeState state)
    {
        if (mission == null)
            return true;

        if (state == MissionRuntimeState.RewardClaimed &&
            !mission.repeatable &&
            mission.hideAfterCompleted)
        {
            return true;
        }

        return false;
    }

    public void SelectMission(MissionDefinition mission)
    {
        if (mission == null)
            return;

        DialogueGraph graph = ResolveGraph(mission);

        if (graph == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[NPCMissionListUI] No graph for mission: {mission.title}");

            return;
        }

        NPCDialogueGraphInteractor interactor = currentInteractor;
        NPCMissionGiver giver = currentGiver;

        Close(unlockPlayer: false);

        if (interactor != null)
            interactor.OpenDialogueGraphDirect(graph, giver != null ? giver.NpcName : "NPC");
    }

    private DialogueGraph ResolveGraph(MissionDefinition mission)
    {
        if (MissionCoordinator.Instance == null)
            return mission != null ? mission.offerGraph : null;

        return MissionCoordinator.Instance.ResolveDialogueGraph(mission);
    }

    private void LockPlayer()
    {
        MouseLook.IsLookLocked = true;
        PlayerMovement.IsMovementLocked = true;
        PlayerInputHandler.SetGameplayBlocked(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void UnlockPlayer()
    {
        MouseLook.IsLookLocked = false;
        PlayerMovement.IsMovementLocked = false;
        PlayerInputHandler.SetGameplayBlocked(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public bool HasVisibleMissions(NPCMissionGiver giver)
    {
        if (giver == null || giver.Missions == null)
            return false;

        for (int i = 0; i < giver.Missions.Length; i++)
        {
            MissionDefinition mission = giver.Missions[i];

            if (mission == null)
                continue;

            MissionRuntimeState state = GetMissionState(mission);

            if (!ShouldHideMission(mission, state))
                return true;
        }

        return false;
    }
}