using System.Collections.Generic;
using UnityEngine;

public class GpsHudArrowController : MonoBehaviour
{
    [Header("UI")]
    public RectTransform arrowRect;
    public CanvasGroup canvasGroup;

    [Header("Target")]
    public Transform playerTransform;

    [Header("Path")]
    public float lookAheadDistance = 25f;

    [Header("Rotation")]
    public float rotationSmooth = 12f;
    public float spriteRotationOffset = 0f; // jeœli sprite jest obrócony bokiem, ustaw np. 90 albo -90

    private List<Vector3> currentPath;

    public void SetPath(List<Vector3> path)
    {
        currentPath = path;

        if (canvasGroup != null)
            canvasGroup.alpha = path != null && path.Count >= 2 ? 1f : 0f;
    }

    public void Clear()
    {
        currentPath = null;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    void Update()
    {
        Transform source = GetCurrentSource();

        if (source == null)
            return;

        if (currentPath == null || currentPath.Count < 2)
            return;

        Vector3 targetPoint = GetLookAheadPoint(source.position);

        Vector3 worldDir = targetPoint - source.position;
        worldDir.y = 0f;

        if (worldDir.sqrMagnitude < 0.01f)
            return;

        worldDir.Normalize();

        Vector3 forward = source.forward;
        forward.y = 0f;
        forward.Normalize();

        float signedAngle = Vector3.SignedAngle(forward, worldDir, Vector3.up);

        float targetZ = -signedAngle + spriteRotationOffset;

        if (arrowRect != null)
        {
            Quaternion targetRot = Quaternion.Euler(0f, 0f, targetZ);

            arrowRect.localRotation = Quaternion.Lerp(
                arrowRect.localRotation,
                targetRot,
                Time.unscaledDeltaTime * rotationSmooth
            );
        }
    }

    Transform GetCurrentSource()
    {
        if (CarInteraction.ActiveVehicleTransform != null)
            return CarInteraction.ActiveVehicleTransform;

        return playerTransform;
    }

    Vector3 GetLookAheadPoint(Vector3 sourcePos)
    {
        float bestDist = float.MaxValue;
        int bestIndex = 0;

        for (int i = 0; i < currentPath.Count; i++)
        {
            float dist = Vector3.Distance(sourcePos, currentPath[i]);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }

        float accumulated = 0f;

        for (int i = bestIndex; i < currentPath.Count - 1; i++)
        {
            float seg = Vector3.Distance(currentPath[i], currentPath[i + 1]);
            accumulated += seg;

            if (accumulated >= lookAheadDistance)
                return currentPath[i + 1];
        }

        return currentPath[currentPath.Count - 1];
    }
}