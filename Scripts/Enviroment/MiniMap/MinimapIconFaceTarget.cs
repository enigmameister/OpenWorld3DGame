using UnityEngine;

public class MinimapIconFaceTarget : MonoBehaviour
{
    [Header("Rotation")]
    public bool faceTarget = true;
    public float rotationOffset = 0f; // np. 0, 90, -90, 180 zale¿nie od sprite
    public bool smoothRotation = true;
    public float rotateSpeed = 12f;

    void LateUpdate()
    {
        if (!faceTarget)
            return;

        Transform target = MinimapTargetProvider.Instance != null
            ? MinimapTargetProvider.Instance.CurrentTarget
            : null;

        if (target == null)
            return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        Quaternion targetRot = Quaternion.Euler(90f, angle + rotationOffset, 0f);

        transform.rotation = smoothRotation
            ? Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotateSpeed)
            : targetRot;
    }
}