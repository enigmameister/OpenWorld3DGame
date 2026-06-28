using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class WorldMapRoadRenderer : MonoBehaviour
{
    [Header("Build")]
    public bool buildOnStart = true;

    [Header("Refs")]
    public RectTransform mapRect;
    public WorldMapRoadGraphic roadOutlineGraphic;
    public WorldMapRoadGraphic roadCoreGraphic;

    [Header("Road Source")]
    public RoadSegment[] roadSegments;

    [Header("Intersections")]
    public WorldMapRoadGraphic intersectionsGraphic;
    public RoadNode[] roadNodes;
    public Color intersectionColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    public float intersectionSize = 14f;

    [Header("Sampling")]
    [Range(8, 300)] public int samplesPerSpline = 96;
    [Tooltip("Minimalny dystans miêdzy punktami na mapie. Wiêksza wartoœæ = mniej punktów = lepszy FPS.")]
    public float minUiPointDistance = 1.5f;

    [Header("Fit")]
    public float mapPadding = 35f;

    [Header("Style")]
    public Color roadColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    public Color roadOutlineColor = Color.black;
    public float roadWidth = 6f;
    public float roadOutlineWidth = 10f;

    [Header("World Bounds")]
    public bool autoCalculateBounds = true;
    public Vector2 worldMin;
    public Vector2 worldMax;

    private readonly List<List<Vector2>> uiPaths = new();
    private readonly List<List<Vector3>> cachedWorldPaths = new();

    void Awake()
    {
        if (mapRect == null)
            mapRect = GetComponent<RectTransform>();
    }

    IEnumerator Start()
    {
        if (!buildOnStart)
            yield break;

        yield return null;
        Build();
    }

    [ContextMenu("Build World Map Roads")]
    public void Build()
    {
        uiPaths.Clear();
        cachedWorldPaths.Clear();

        if (mapRect == null)
            mapRect = GetComponent<RectTransform>();

        if (mapRect == null)
            return;

        if (roadSegments == null || roadSegments.Length == 0)
            roadSegments = FindObjectsByType<RoadSegment>(FindObjectsSortMode.None);

        CacheWorldPaths();

        if (autoCalculateBounds)
            CalculateWorldBoundsFromCache();

        BuildUiPathsFromCache();
        BuildIntersections();
        ApplyToGraphics();
    }

    void CacheWorldPaths()
    {
        cachedWorldPaths.Clear();

        if (roadSegments == null)
            return;

        for (int i = 0; i < roadSegments.Length; i++)
        {
            RoadSegment segment = roadSegments[i];

            if (segment == null)
                continue;

            List<List<Vector3>> paths = GetSegmentWorldPaths(segment);

            for (int p = 0; p < paths.Count; p++)
            {
                if (paths[p] != null && paths[p].Count >= 2)
                    cachedWorldPaths.Add(paths[p]);
            }
        }
    }

    void BuildUiPathsFromCache()
    {
        uiPaths.Clear();

        for (int i = 0; i < cachedWorldPaths.Count; i++)
        {
            List<Vector3> worldPath = cachedWorldPaths[i];

            if (worldPath == null || worldPath.Count < 2)
                continue;

            List<Vector2> uiPath = new();
            Vector2 lastAdded = Vector2.zero;
            bool hasLast = false;

            for (int j = 0; j < worldPath.Count; j++)
            {
                Vector2 uiPos = WorldToMapPosition(worldPath[j]);

                if (hasLast && Vector2.Distance(lastAdded, uiPos) < minUiPointDistance && j < worldPath.Count - 1)
                    continue;

                uiPath.Add(uiPos);
                lastAdded = uiPos;
                hasLast = true;
            }

            if (uiPath.Count >= 2)
                uiPaths.Add(uiPath);
        }
    }

    void ApplyToGraphics()
    {
        if (roadOutlineGraphic != null)
        {
            roadOutlineGraphic.raycastTarget = false;
            roadOutlineGraphic.color = roadOutlineColor;
            roadOutlineGraphic.lineWidth = roadOutlineWidth;
            roadOutlineGraphic.SetPaths(uiPaths);
        }

        if (roadCoreGraphic != null)
        {
            roadCoreGraphic.raycastTarget = false;
            roadCoreGraphic.color = roadColor;
            roadCoreGraphic.lineWidth = roadWidth;
            roadCoreGraphic.SetPaths(uiPaths);
        }
    }

    void BuildIntersections()
    {
        if (intersectionsGraphic == null)
            return;

        if (roadNodes == null || roadNodes.Length == 0)
            roadNodes = FindObjectsByType<RoadNode>(FindObjectsSortMode.None);

        List<List<Vector2>> paths = new();

        for (int i = 0; i < roadNodes.Length; i++)
        {
            RoadNode node = roadNodes[i];

            if (node == null || !node.isIntersection)
                continue;

            Vector2 center = WorldToMapPosition(node.transform.position);

            float r = intersectionSize * 0.5f;
            int points = 12;

            List<Vector2> circle = new();

            for (int p = 0; p <= points; p++)
            {
                float angle = (p / (float)points) * Mathf.PI * 2f;

                circle.Add(center + new Vector2(
                    Mathf.Cos(angle) * r,
                    Mathf.Sin(angle) * r
                ));
            }

            paths.Add(circle);
        }

        intersectionsGraphic.raycastTarget = false;
        intersectionsGraphic.color = intersectionColor;
        intersectionsGraphic.lineWidth = intersectionSize;
        intersectionsGraphic.SetPaths(paths);
    }

    List<List<Vector3>> GetSegmentWorldPaths(RoadSegment segment)
    {
        List<List<Vector3>> result = new();

        SplineContainer container = segment.GetComponent<SplineContainer>();

        if (container == null || container.Splines == null || container.Splines.Count == 0)
        {
            List<Vector3> fallback = new();

            if (segment.startNode != null)
                fallback.Add(segment.startNode.transform.position);

            if (segment.endNode != null)
                fallback.Add(segment.endNode.transform.position);

            if (fallback.Count >= 2)
                result.Add(fallback);

            return result;
        }

        int sampleCount = Mathf.Max(8, samplesPerSpline);

        for (int s = 0; s < container.Splines.Count; s++)
        {
            Spline spline = container.Splines[s];
            List<Vector3> points = new();

            for (int i = 0; i <= sampleCount; i++)
            {
                float t = i / (float)sampleCount;

                Vector3 localPos = spline.EvaluatePosition(t);
                Vector3 worldPos = container.transform.TransformPoint(localPos);

                points.Add(worldPos);
            }

            if (points.Count >= 2)
                result.Add(points);
        }

        return result;
    }

    void CalculateWorldBoundsFromCache()
    {
        bool hasPoint = false;

        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;

        for (int i = 0; i < cachedWorldPaths.Count; i++)
        {
            List<Vector3> path = cachedWorldPaths[i];

            if (path == null)
                continue;

            for (int j = 0; j < path.Count; j++)
            {
                Vector3 world = path[j];
                Vector2 point = new Vector2(world.x, world.z);

                if (!hasPoint)
                {
                    min = point;
                    max = point;
                    hasPoint = true;
                }
                else
                {
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }
        }

        if (!hasPoint)
            return;

        worldMin = min;
        worldMax = max;
    }

    Vector2 WorldToMapPosition(Vector3 worldPos)
    {
        if (mapRect == null)
            return Vector2.zero;

        Vector2 size = mapRect.rect.size;

        float usableW = size.x - mapPadding * 2f;
        float usableH = size.y - mapPadding * 2f;

        float worldW = Mathf.Max(1f, worldMax.x - worldMin.x);
        float worldH = Mathf.Max(1f, worldMax.y - worldMin.y);

        float scale = Mathf.Min(usableW / worldW, usableH / worldH);

        Vector2 worldCenter = (worldMin + worldMax) * 0.5f;

        float x = (worldPos.x - worldCenter.x) * scale;
        float y = (worldPos.z - worldCenter.y) * scale;

        return new Vector2(x, y);
    }

    public Vector2 WorldToMapPositionPublic(Vector3 worldPos)
    {
        return WorldToMapPosition(worldPos);
    }

    public Vector3 MapToWorldPositionPublic(Vector2 mapPos, float y = 0f)
    {
        Vector2 size = mapRect.rect.size;

        float usableW = size.x - mapPadding * 2f;
        float usableH = size.y - mapPadding * 2f;

        float worldW = Mathf.Max(1f, worldMax.x - worldMin.x);
        float worldH = Mathf.Max(1f, worldMax.y - worldMin.y);

        float scale = Mathf.Min(usableW / worldW, usableH / worldH);

        Vector2 worldCenter = (worldMin + worldMax) * 0.5f;

        float worldX = (mapPos.x / scale) + worldCenter.x;
        float worldZ = (mapPos.y / scale) + worldCenter.y;

        return new Vector3(worldX, y, worldZ);
    }
}