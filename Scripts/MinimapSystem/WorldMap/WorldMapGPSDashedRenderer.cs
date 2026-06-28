using System.Collections.Generic;
using UnityEngine;

public class WorldMapGPSDashedRenderer : MonoBehaviour
{
    [Header("Refs")]
    public WorldMapRoadRenderer roadRenderer;
    public RectTransform dashPrefab;
    public RectTransform dashParent;

    [Header("Visual")]
    public float dashLength = 10f;
    public float gapLength = 8f;
    public float dashWidth = 5f;

    private readonly List<RectTransform> spawned = new();

    public void ShowPath(List<Vector3> worldPath)
    {
        Clear();

        if (worldPath == null || worldPath.Count < 2 || roadRenderer == null || dashPrefab == null)
            return;

        Transform parent = dashParent != null ? dashParent : transform;

        for (int i = 0; i < worldPath.Count - 1; i++)
        {
            Vector2 a = roadRenderer.WorldToMapPositionPublic(worldPath[i]);
            Vector2 b = roadRenderer.WorldToMapPositionPublic(worldPath[i + 1]);

            SpawnDashesOnSegment(a, b, parent);
        }
    }

    public void HidePath()
    {
        Clear();
    }

    void SpawnDashesOnSegment(Vector2 a, Vector2 b, Transform parent)
    {
        Vector2 dir = b - a;
        float length = dir.magnitude;

        if (length < 0.01f)
            return;

        dir.Normalize();

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float distance = 0f;

        while (distance < length)
        {
            float currentDashLength = Mathf.Min(dashLength, length - distance);
            Vector2 center = a + dir * (distance + currentDashLength * 0.5f);

            RectTransform dash = Instantiate(dashPrefab, parent);
            dash.gameObject.SetActive(true);
            dash.anchoredPosition = center;
            dash.sizeDelta = new Vector2(currentDashLength, dashWidth);
            dash.localRotation = Quaternion.Euler(0f, 0f, angle);

            spawned.Add(dash);

            distance += dashLength + gapLength;
        }
    }

    void Clear()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null)
                Destroy(spawned[i].gameObject);
        }

        spawned.Clear();
    }
}