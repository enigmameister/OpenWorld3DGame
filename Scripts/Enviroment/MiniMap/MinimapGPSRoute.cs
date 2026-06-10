using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class MinimapGPSRoute : MonoBehaviour
{
    private LineRenderer line;

    [Header("Visual")]
    public float routeWidth = 12f;
    public float heightOffset = 1.2f;

    void Awake()
    {
        line = GetComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.widthMultiplier = routeWidth;
        line.enabled = false;
        line.positionCount = 0;
    }

    public void ShowRoute(List<Vector3> points)
    {
        if (line == null)
            line = GetComponent<LineRenderer>();

        if (points == null || points.Count < 2)
        {
            HideRoute();
            return;
        }

        line.useWorldSpace = true;
        line.widthMultiplier = routeWidth;
        line.positionCount = points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i];

            // wa¿ne: NIE zerujemy Y
            // u¿ywamy wysokoœci ze spline + ma³y offset nad drog¹
            p.y += heightOffset;

            line.SetPosition(i, p);
        }

        line.enabled = true;
    }

    public void HideRoute()
    {
        if (line == null)
            line = GetComponent<LineRenderer>();

        line.positionCount = 0;
        line.enabled = false;
    }
}