using UnityEngine;

public static class NPCPlayerTargetUtility
{
    public static Transform GetTargetTransform(Transform fallbackPlayer)
    {
        if (CarInteraction.ActiveVehicleTransform != null)
            return CarInteraction.ActiveVehicleTransform;

        return fallbackPlayer;
    }

    public static Vector3 GetTargetPosition(Transform fallbackPlayer)
    {
        Transform target = GetTargetTransform(fallbackPlayer);

        if (target != null)
            return target.position;

        return Vector3.zero;
    }

    public static bool IsPlayerInVehicle()
    {
        return CarInteraction.ActiveVehicleTransform != null;
    }
}