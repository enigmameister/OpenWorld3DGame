using UnityEngine;

[CreateAssetMenu(menuName = "Missions/Mission Definition")]
public class MissionDefinition : ScriptableObject
{
    public string missionId;
    public string title;

    [TextArea(4, 10)]
    public string description;

    public string objectiveText;
    public string rewardText;

    public bool repeatable;
    public bool giveRewardOnlyOnce = true;
    public bool hideAfterCompleted = true;

    public DialogueGraph offerGraph;
    public DialogueGraph activeGraph;
    public DialogueGraph readyToClaimGraph;
    public DialogueGraph completedGraph;
}