using System;

public enum ObjectiveStatus
{
    InProgress,
    Finished,
    Failed
}

public class ObjectiveEntryData
{
    public string missionId;
    public string missionName;
    public string objectiveText;
    public string descriptionText;
    public ObjectiveStatus status;
    public bool canAbandon;
    public Action onAbandon;

    public ObjectiveEntryData(
        string missionId,
        string missionName,
        string objectiveText,
        string descriptionText,
        ObjectiveStatus status,
        bool canAbandon,
        Action onAbandon)
    {
        this.missionId = missionId;
        this.missionName = missionName;
        this.objectiveText = objectiveText;
        this.descriptionText = descriptionText;
        this.status = status;
        this.canAbandon = canAbandon;
        this.onAbandon = onAbandon;
    }
}