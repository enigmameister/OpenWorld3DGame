using UnityEngine;
using UnityEngine.UI;

public class MinimapIconFollowTarget : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("UI")]
    public RectTransform iconRect;
    public Image iconImage;

    [Header("Minimap Camera Mapping")]
    public Camera minimapCamera;
    public RectTransform radarRect;

    [Header("Out Of Range Direction")]
    public bool rotateToDirectionWhenClamped = true;
    public float clampedRotationOffsetZ = -90f;

    [Header("Clamp")]
    public bool clampToRadarCircle = true;
    [Range(0.1f, 1f)] public float clampRadiusMultiplier = 0.88f;

    [Header("Rotation")]
    public bool rotateWithTarget = true;
    public float rotationOffsetZ = 180f;

    void Awake()
    {
        if (iconRect == null)
            iconRect = GetComponent<RectTransform>();

        if (iconImage == null)
            iconImage = GetComponent<Image>();
    }

    void LateUpdate()
    {
        if (target == null || iconRect == null || minimapCamera == null || radarRect == null)
            return;

        Vector3 viewport = minimapCamera.WorldToViewportPoint(target.position);

        if (viewport.z < 0f)
            return;

        Vector2 radarSize = radarRect.rect.size;

        Vector2 pos = new Vector2(
            (viewport.x - 0.5f) * radarSize.x,
            (viewport.y - 0.5f) * radarSize.y
        );

        bool isClamped = false;

        if (clampToRadarCircle)
        {
            float radius = Mathf.Min(radarSize.x, radarSize.y) * 0.5f * clampRadiusMultiplier;

            if (pos.magnitude > radius)
            {
                pos = pos.normalized * radius;
                isClamped = true;
            }
        }

        iconRect.anchoredPosition = pos;

        if (rotateWithTarget)
        {
            float zRot = -target.eulerAngles.y + minimapCamera.transform.eulerAngles.y + rotationOffsetZ;
            iconRect.localRotation = Quaternion.Euler(0f, 0f, zRot);
        }
        else if (rotateToDirectionWhenClamped && isClamped)
        {
            float angle = Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg;
            iconRect.localRotation = Quaternion.Euler(0f, 0f, angle + clampedRotationOffsetZ);
        }
        else
        {
            iconRect.localRotation = Quaternion.identity;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetColor(Color color)
    {
        if (iconImage != null)
            iconImage.color = color;
    }
}