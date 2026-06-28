// NPCDeathUtility.cs  (statyczny helper – NIE jako komponent)
using UnityEngine;
using UnityEngine.AI;

public static class NPCDeathUtility
{
    public static void DieLikeNPCController(MonoBehaviour owner, NavMeshAgent agent, Transform root,
                                            ref Rigidbody rb, ref Collider col,
                                            Animator anim = null, bool gentleImpulse = false)
    {
        // 1) zatrzymaj AI
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }
        if (anim) anim.enabled = false;

        // 2) w³¹cz upadek – dok³adnie to samo co w NPCController
        RagdollFallUtility.Enable(root, ref rb, ref col, gentleImpulse);

        // 3) bezpieczeñstwo: upewnij siê, ¿e grawitacja naprawdê jest ON
        if (rb != null)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.WakeUp();
        }
    }
}
