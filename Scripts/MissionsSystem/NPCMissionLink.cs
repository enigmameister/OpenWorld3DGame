using System;
using UnityEngine;

[Serializable]
public class NPCMissionLink
{
    public MissionDefinition definition;
    public MissionNpcRole role = MissionNpcRole.Giver;
}