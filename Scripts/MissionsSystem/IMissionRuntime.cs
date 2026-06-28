public interface IMissionRuntime
{
    string MissionId { get; }
    MissionDefinition Definition { get; }

    MissionRuntimeState RuntimeState { get; }

    bool ShowOnScreenTracker { get; }

    ObjectiveEntryData BuildObjectiveEntry();

    void AcceptMission();
    void DeclineMission();
    void ClaimReward();
    void AbandonMission();

    void SetShowOnScreenTracker(bool visible);
}