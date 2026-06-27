using System;
using UnityEngine;

[Serializable]
public class NPCMissionEntry
{
    [Header("Mission")]
    public string missionId = "Mission_TestHouse";
    public string displayName = "Eliminate Armed NPCs";

    [Header("Rules")]
    public bool repeatable = false;
    public bool hideAfterRewardClaimed = true;
    public bool rewardOnlyOnce = true;

    [Header("Dialogue Graphs")]
    public DialogueGraph offerGraph;
    public DialogueGraph activeGraph;
    public DialogueGraph readyToClaimGraph;
    public DialogueGraph completedGraph;
}