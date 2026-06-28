using UnityEngine;

public class MinimapCameraFollow : MonoBehaviour
{
    public float height = 120f;
    public bool rotateWithTarget = true;
    public bool smoothFollow = true;
    public float followSpeed = 12f;
    public float rotateSpeed = 12f;

    void LateUpdate()
    {
        Transform target = MinimapTargetProvider.Instance != null
            ? MinimapTargetProvider.Instance.CurrentTarget
            : null;

        if (target == null)
            return;

        Vector3 targetPosition = new Vector3(
            target.position.x,
            target.position.y + height,
            target.position.z
        );

        transform.position = smoothFollow
            ? Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed)
            : targetPosition;

        Quaternion targetRotation = rotateWithTarget
            ? Quaternion.Euler(90f, target.eulerAngles.y, 0f)
            : Quaternion.Euler(90f, 0f, 0f);

        transform.rotation = smoothFollow
            ? Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed)
            : targetRotation;
    }
}