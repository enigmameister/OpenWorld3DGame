using System.Collections.Generic;
using UnityEngine;

public class MissionCoordinator : MonoBehaviour
{
    public static MissionCoordinator Instance { get; private set; }

    [Header("Mission Runtimes")]
    [SerializeField] private MonoBehaviour[] missionRuntimeBehaviours;

    private readonly Dictionary<string, IMissionRuntime> runtimeById = new();

    private void Awake()
    {
        Instance = this;
        RebuildCache();
    }

    public void RebuildCache()
    {
        runtimeById.Clear();

        if (missionRuntimeBehaviours == null)
            return;

        for (int i = 0; i < missionRuntimeBehaviours.Length; i++)
        {
            if (missionRuntimeBehaviours[i] is not IMissionRuntime runtime)
                continue;

            if (string.IsNullOrWhiteSpace(runtime.MissionId))
                continue;

            runtimeById[runtime.MissionId] = runtime;
        }
    }

    public IMissionRuntime GetRuntime(string missionId)
    {
        if (string.IsNullOrWhiteSpace(missionId))
            return null;

        runtimeById.TryGetValue(missionId, out IMissionRuntime runtime);
        return runtime;
    }

    public MissionRuntimeState GetMissionState(string missionId)
    {
        IMissionRuntime runtime = GetRuntime(missionId);
        return runtime != null ? runtime.RuntimeState : MissionRuntimeState.NotStarted;
    }

    public List<ObjectiveEntryData> GetActiveObjectives()
    {
        List<ObjectiveEntryData> result = new();

        foreach (var pair in runtimeById)
        {
            ObjectiveEntryData data = pair.Value.BuildObjectiveEntry();

            if (data != null)
                result.Add(data);
        }

        return result;
    }

    public void AcceptMission(string missionId)
    {
        GetRuntime(missionId)?.AcceptMission();
    }

    public void DeclineMission(string missionId)
    {
        GetRuntime(missionId)?.DeclineMission();
    }

    public void ClaimReward(string missionId)
    {
        GetRuntime(missionId)?.ClaimReward();
    }

    public void AbandonMission(string missionId)
    {
        GetRuntime(missionId)?.AbandonMission();
    }

    public void SetShowOnScreenTracker(string missionId, bool visible)
    {
        GetRuntime(missionId)?.SetShowOnScreenTracker(visible);
    }

    public DialogueGraph ResolveDialogueGraph(MissionDefinition definition)
    {
        if (definition == null)
            return null;

        MissionRuntimeState state = GetMissionState(definition.missionId);

        switch (state)
        {
            case MissionRuntimeState.NotStarted:
                return definition.offerGraph;

            case MissionRuntimeState.Active:
                return definition.activeGraph != null ? definition.activeGraph : definition.offerGraph;

            case MissionRuntimeState.ReadyToClaim:
                return definition.readyToClaimGraph != null
                    ? definition.readyToClaimGraph
                    : definition.activeGraph;

            case MissionRuntimeState.RewardClaimed:
                if (definition.repeatable)
                    return definition.completedGraph != null
                        ? definition.completedGraph
                        : definition.offerGraph;

                return definition.completedGraph;

            default:
                return definition.offerGraph;
        }
    }

    public bool ShouldHideMissionInNpcList(MissionDefinition definition)
    {
        if (definition == null)
            return true;

        MissionRuntimeState state = GetMissionState(definition.missionId);

        if (state == MissionRuntimeState.RewardClaimed &&
            !definition.repeatable &&
            definition.hideAfterCompleted)
        {
            return true;
        }

        return false;
    }
}