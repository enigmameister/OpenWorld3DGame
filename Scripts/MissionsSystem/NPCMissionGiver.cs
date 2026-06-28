using UnityEngine;

public class NPCMissionGiver : MonoBehaviour
{
    [Header("NPC")]
    [SerializeField] private string npcName = "NPC NAME";

    [Header("Missions")]
    [SerializeField] private MissionDefinition[] missions;

    public string NpcName => npcName;
    public MissionDefinition[] Missions => missions;

    public bool HasAnyMission()
    {
        return missions != null && missions.Length > 0;
    }
}