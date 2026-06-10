using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MinimapRoadNetworkRenderer : MonoBehaviour
{
    [Header("Search")]
    public bool findInactiveObjects = false;

    [Header("Generated Root")]
    public string generatedRootName = "Generated_Minimap_Roads";

    [Header("Visual")]
    public Material outlineMaterial;
    public Material coreMaterial;

    public Color outlineColor = Color.black;
    public Color coreColor = Color.gray;

    [Header("Widths")]
    public float outlineWidth = 18f;
    public float coreWidth = 12f;

    [Header("Height")]
    public float outlineY = 0.10f;
    public float coreY = 0.15f;

    [Header("Spline Sampling")]
    [Range(8, 256)]
    public int samplesPerSegment = 64;

    [Header("Rendering")]
    public int cornerVertices = 12;
    public int capVertices = 4;


    [ContextMenu("GENERATE MINIMAP ROADS")]
    public void GenerateRoads()
    {
        ClearGenerated();

        GameObject root = new GameObject(generatedRootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(root, "Generate Minimap Roads");
#endif

        RoadSegment[] segments = FindObjectsByType<RoadSegment>(
            findInactiveObjects
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (RoadSegment segment in segments)
        {
            if (segment == null)
                continue;

            CreateRoadRenderer(segment, root.transform);
        }

        Debug.Log($"[MinimapRoads] Generated: {segments.Length} road segments.");
    }

    void CreateRoadRenderer(RoadSegment segment, Transform parent)
    {
        List<Vector3> points = GetSegmentPoints(segment);

        if (points.Count < 2)
            return;

        GameObject segmentRoot = new GameObject(segment.name);
        segmentRoot.transform.SetParent(parent);

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(segmentRoot, "Create Segment Root");
#endif

        CreateLineObject(
            "Outline",
            segmentRoot.transform,
            points,
            outlineWidth,
            outlineColor,
            outlineMaterial,
            outlineY
        );

        CreateLineObject(
            "Core",
            segmentRoot.transform,
            points,
            coreWidth,
            coreColor,
            coreMaterial,
            coreY
        );
    }

    void CreateLineObject(
        string objectName,
        Transform parent,
        List<Vector3> points,
        float width,
        Color color,
        Material material,
        float y
    )
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(parent);

        LineRenderer lr = obj.AddComponent<LineRenderer>();

        lr.useWorldSpace = true;
        lr.loop = false;

        lr.widthMultiplier = width;

        lr.startColor = color;
        lr.endColor = color;

        lr.numCornerVertices = cornerVertices;
        lr.numCapVertices = capVertices;

        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.View;

        if (material != null)
            lr.material = material;

        lr.positionCount = points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i];

            p.y += y;

            lr.SetPosition(i, p);
        }
    }

    List<Vector3> GetSegmentPoints(RoadSegment segment)
    {
        List<Vector3> result = new();

        if (segment == null)
            return result;

        SplineContainer container = segment.GetComponent<SplineContainer>();

        if (container == null || container.Splines == null || container.Splines.Count == 0)
        {
            if (segment.startNode != null)
                result.Add(segment.startNode.transform.position);

            if (segment.endNode != null)
                result.Add(segment.endNode.transform.position);

            return result;
        }

        for (int s = 0; s < container.Splines.Count; s++)
        {
            Spline spline = container.Splines[s];

            if (spline == null || spline.Count < 2)
                continue;

            for (int i = 0; i <= samplesPerSegment; i++)
            {
                float t = i / (float)samplesPerSegment;

                Vector3 localPos = spline.EvaluatePosition(t);
                Vector3 worldPos = container.transform.TransformPoint(localPos);

                if (result.Count > 0 && Vector3.Distance(result[result.Count - 1], worldPos) < 0.05f)
                    continue;

                result.Add(worldPos);
            }
        }

        return result;
    }

    public void SetGeneratedRoadColors(Color outline, Color core)
    {
        Transform root = transform.Find(generatedRootName);
        if (root == null)
            return;

        LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>(true);

        foreach (LineRenderer lr in lines)
        {
            if (lr == null)
                continue;

            bool isOutline = lr.gameObject.name.Contains("Outline");
            Color c = isOutline ? outline : core;

            ApplyLineColor(lr, c);
        }
    }

    void ApplyLineColor(LineRenderer lr, Color color)
    {
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

    public List<Vector3> BuildRoutePoints(RaceRoute route)
    {
        List<Vector3> result = new();

        if (route == null || route.Count <= 0)
            return result;

        for (int i = 0; i < route.Count; i++)
        {
            RoadSegment segment = route.GetSegment(i);
            if (segment == null)
                continue;

            List<Vector3> points = GetSegmentPoints(segment);

            if (points.Count < 2)
                continue;

            if (route.segments[i].reverse)
                points.Reverse();

            if (result.Count > 0)
            {
                float gapToStart = Vector3.Distance(result[result.Count - 1], points[0]);
                float gapToEnd = Vector3.Distance(result[result.Count - 1], points[points.Count - 1]);

                if (gapToEnd < gapToStart)
                    points.Reverse();

                if (Vector3.Distance(result[result.Count - 1], points[0]) < 1.5f)
                    points.RemoveAt(0);
            }

            result.AddRange(points);
        }

        if (route.loop && result.Count > 2)
        {
            float gap = Vector3.Distance(result[result.Count - 1], result[0]);

            if (gap < 10f)
                result.Add(result[0]);
        }

        return result;
    }

    [ContextMenu("CLEAR GENERATED")]
    public void ClearGenerated()
    {
        Transform oldRoot = transform.Find(generatedRootName);

        if (oldRoot == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.DestroyObjectImmediate(oldRoot.gameObject);
        else
            Destroy(oldRoot.gameObject);
#else
        Destroy(oldRoot.gameObject);
#endif
    }
}