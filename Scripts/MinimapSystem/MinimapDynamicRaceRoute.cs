using System.Collections.Generic;
using UnityEngine;

public class MinimapDynamicRaceRoute : MonoBehaviour
{
    [Header("Lines")]
    public LineRenderer outlineLine;
    public LineRenderer coreLine;

    [Header("Normal Road Network")]
    public MinimapRoadNetworkRenderer roadNetworkRenderer;

    [Header("Free Roam Road Colors")]
    public Color freeRoadOutlineColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    public Color freeRoadCoreColor = Color.white;

    [Header("Race Background Road Colors")]
    public Color raceBackgroundOutlineColor = Color.black;
    public Color raceBackgroundCoreColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Header("Active Race Route Colors")]
    public Color raceRouteOutlineColor = Color.black;
    public Color raceRouteCoreColor = Color.white;

    [Header("Widths")]
    public float outlineWidth = 22f;
    public float coreWidth = 16f;

    [Header("Height")]
    public float outlineY = 4f;
    public float coreY = 6f;

    void Awake()
    {
        if (roadNetworkRenderer == null)
            roadNetworkRenderer = FindFirstObjectByType<MinimapRoadNetworkRenderer>();

        SetupLine(outlineLine);
        SetupLine(coreLine);

        HideRoute();
    }

    void SetupLine(LineRenderer lr)
    {
        if (lr == null) return;

        lr.useWorldSpace = true;
        lr.numCornerVertices = 16;
        lr.numCapVertices = 8;
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.View;
        lr.enabled = false;
    }

    public void ShowRoute(List<Vector3> points)
    {
        if (points == null || points.Count < 2)
        {
            HideRoute();
            return;
        }

        // Przyciemnij zwyk³e drogi.
        if (roadNetworkRenderer != null)
        {
            roadNetworkRenderer.SetGeneratedRoadColors(
                raceBackgroundOutlineColor,
                raceBackgroundCoreColor
            );
        }

        // Aktywna trasa wyœcigu.
        ApplyLineStyle(outlineLine, outlineWidth, raceRouteOutlineColor);
        ApplyLineStyle(coreLine, coreWidth, raceRouteCoreColor);

        ApplyPoints(outlineLine, points, outlineY);
        ApplyPoints(coreLine, points, coreY);
    }

    public void HideRoute()
    {
        ClearLine(outlineLine);
        ClearLine(coreLine);

        // Przywróæ free roam.
        if (roadNetworkRenderer != null)
        {
            roadNetworkRenderer.SetGeneratedRoadColors(
                freeRoadOutlineColor,
                freeRoadCoreColor
            );
        }
    }

    void ApplyLineStyle(LineRenderer lr, float width, Color color)
    {
        if (lr == null) return;

        lr.widthMultiplier = width;
        lr.startColor = color;
        lr.endColor = color;

        if (lr.sharedMaterial != null)
            lr.material = new Material(lr.sharedMaterial);

        if (lr.material != null)
        {
            if (lr.material.HasProperty("_BaseColor"))
                lr.material.SetColor("_BaseColor", color);

            if (lr.material.HasProperty("_Color"))
                lr.material.SetColor("_Color", color);
        }
    }

    void ApplyPoints(LineRenderer lr, List<Vector3> points, float y)
    {
        if (lr == null) return;

        lr.positionCount = points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i];
            p.y += y;
            lr.SetPosition(i, p);
        }

        lr.enabled = true;
    }

    public void ShowRaceRoute(RaceRoute route)
    {
        if (roadNetworkRenderer == null)
            roadNetworkRenderer = FindFirstObjectByType<MinimapRoadNetworkRenderer>();

        if (roadNetworkRenderer == null || route == null)
        {
            HideRoute();
            return;
        }

        List<Vector3> points = roadNetworkRenderer.BuildRoutePoints(route);
        ShowRoute(points);
    }

    void ClearLine(LineRenderer lr)
    {
        if (lr == null) return;

        lr.positionCount = 0;
        lr.enabled = false;
    }
}