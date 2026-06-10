using System.Collections.Generic;
using UnityEngine;

public class MinimapGPSDashedRoute : MonoBehaviour
{
    [Header("Prefab")]
    public LineRenderer dashPrefab;

    [Header("Visual")]
    public float routeWidth = 12f;
    public float heightOffset = 1.2f;

    [Header("Dash")]
    public float dashLength = 8f;
    public float gapLength = 6f;

    private readonly List<LineRenderer> spawned = new();

    public void ShowRoute(List<Vector3> points)
    {
        Clear();

        if (points == null || points.Count < 2 || dashPrefab == null)
            return;

        for (int i = 0; i < points.Count - 1; i++)
            SpawnDashes(points[i], points[i + 1]);
    }

    public void HideRoute()
    {
        Clear();
    }

    void SpawnDashes(Vector3 a, Vector3 b)
    {
        Vector3 dir = b - a;
        float length = dir.magnitude;

        if (length < 0.01f)
            return;

        dir.Normalize();

        float distance = 0f;

        while (distance < length)
        {
            float end = Mathf.Min(distance + dashLength, length);

            Vector3 p0 = a + dir * distance;
            Vector3 p1 = a + dir * end;

            p0.y += heightOffset;
            p1.y += heightOffset;

            LineRenderer dash = Instantiate(dashPrefab, transform);
            dash.useWorldSpace = true;
            dash.widthMultiplier = routeWidth;
            dash.positionCount = 2;
            dash.SetPosition(0, p0);
            dash.SetPosition(1, p1);
            dash.gameObject.SetActive(true);

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