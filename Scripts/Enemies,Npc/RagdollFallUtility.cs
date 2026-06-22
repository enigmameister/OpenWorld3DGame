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

        // ¯eby kapsu³a nie sta³a idealnie pionowo po œmierci.
        Vector3 fallDir = -root.forward;

        if (fallDir.sqrMagnitude < 0.001f)
            fallDir = Random.insideUnitSphere;

        fallDir.y = 0f;

        if (fallDir.sqrMagnitude < 0.001f)
            fallDir = Vector3.right;

        fallDir.Normalize();

        rb.WakeUp();

        if (gentleImpulse)
        {
            rb.AddForce(fallDir * 0.85f + Vector3.down * 0.15f, ForceMode.Impulse);
            rb.AddTorque(Vector3.Cross(Vector3.up, fallDir) * 2.2f, ForceMode.Impulse);
        }
        else
        {
            rb.AddForce(fallDir * 1.25f + Vector3.down * 0.2f, ForceMode.Impulse);
            rb.AddTorque(Vector3.Cross(Vector3.up, fallDir) * 3.0f, ForceMode.Impulse);
        }
    }
}