using UnityEngine;

public class NPCMissionGiver : MonoBehaviour
{
    [Header("NPC")]
    [SerializeField] private string npcName = "NPC NAME";

    [Header("Missions")]
    [SerializeField] private NPCMissionEntry[] missions;

    public string NpcName => npcName;
    public NPCMissionEntry[] Missions => missions;

    public bool HasAnyMission()
    {
        return missions != null && missions.Length > 0;
    }
}