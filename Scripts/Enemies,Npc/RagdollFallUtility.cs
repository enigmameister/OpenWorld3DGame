using UnityEngine;

public static class RagdollFallUtility
{
    public static void Enable(Transform root, ref Rigidbody rb, ref Collider col, bool gentleImpulse)
    {
        if (root == null || rb == null || col == null) return;

        col.enabled = true;
        col.isTrigger = false;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.None;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp();
    }
}