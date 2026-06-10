using UnityEngine;

public class WorldMapPlayerIcon : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform iconRect;
    public WorldMapRoadRenderer mapRenderer;

    [Header("Targets")]
    public Transform playerTarget;

    [Header("Rotation")]
    public float rotationOffsetZ = 0f;

    void Awake()
    {
        if (iconRect == null)
            iconRect = GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        if (iconRect == null || mapRenderer == null)
            return;

        Transform target = GetActiveTarget();

        if (target == null)
            return;

        iconRect.anchoredPosition = mapRenderer.WorldToMapPositionPublic(target.position);

        float yRot = target.eulerAngles.y;
        iconRect.localRotation = Quaternion.Euler(0f, 0f, -yRot + rotationOffsetZ);
    }

    Transform GetActiveTarget()
    {
        CarControll[] cars = FindObjectsByType<CarControll>(FindObjectsSortMode.None);

        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] != null && cars[i].isControlled)
                return cars[i].transform;
        }

        return playerTarget;
    }
}