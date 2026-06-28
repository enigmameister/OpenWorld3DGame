using System.Collections.Generic;
using UnityEngine.Splines;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RaceRouteArrowGenerator : MonoBehaviour
{
    [Header("Route Source")]
    public RaceRoute raceRoute;

    [Header("Arrow Prefab")]
    public GameObject arrowPrefab;

    [Header("Placement")]
    public Transform arrowsParent;
    public float arrowSideOffset = 8f;
    public float arrowForwardOffset = 0f;
    public float arrowHeightOffset = 1f;
    public float minTurnAngle = 25f;
    public int arrowsPerTurn = 3;
    public float arrowSpacing = 5f;

    [Header("Rotation")]
    public bool faceAlongRoute = true;
    public Vector3 rotationOffsetEuler = Vector3.zero;
    public bool placeOnIntersectionCenter = true;

    [Header("Spline Sampling")]
    public bool useSplinePoints = true;
    public int samplesPerSegment = 64;
    public int directionProbePoints = 8;

    [Header("Direction Probe Distance")]
    public float directionProbeInDistance = 12f;
    public float directionProbeOutDistance = 12f;

    [Header("Arrow Spline")]
    public SplineContainer arrowSplinePrefab;
    public int splineSamples = 12;

    [Header("Arrow Spline Fit")]
    public float arrowSplineScale = 0.55f;
    public float arrowStartOffset = 2f;
    public bool reverseArrowDirection = false;

    [Header("Dynamic Arrow Arc")]
    public float arrowArcStartDistance = 10f;
    public float arrowArcEndDistance = 22f;
    public float arrowArcControlDistance = 12f;
    public float arrowArcSideOffset = 8f;

    [Header("Double Arrow Wall")]
    public bool doubleArrowWall = false;
    public float doubleWallSpacing = 10f;

    [Header("Arrow Preview")]
    public bool drawArrowPreview = true;
    public Color previewArcColor = Color.yellow;
    public Color previewDirectionColor = Color.red;
    public float previewDirectionLength = 4f;

    private List<List<Vector3>> previewArcs = new();
    private List<List<Vector3>> previewDirs = new();

    [Header("Debug")]
    public bool debugRoutePoints = true;
    public bool drawDebugGizmos = true;

    private List<Vector3> debugPoints = new();

    [Header("Debug / Direction Fix")]
    public bool invertTurnDirection = false;

    [Header("Cleanup")]
    public string generatedRootName = "Generated_Route_Arrows";

    [ContextMenu("GENERATE ROUTE ARROWS")]

    public void GenerateArrows()
    {
        if (raceRoute == null)
            raceRoute = GetComponent<RaceRoute>();

        if (raceRoute == null || raceRoute.segments == null || raceRoute.segments.Count < 2)
        {
            Debug.LogWarning("Brakuje RaceRoute albo minimum 2 segmentów.");
            return;
        }

        if (arrowPrefab == null)
        {
            Debug.LogWarning("Brakuje arrowPrefab.");
            return;
        }

        if (arrowSplinePrefab == null)
        {
            Debug.LogWarning("Brakuje arrowSplinePrefab.");
            return;
        }

        List<Vector3> points = BuildPointsFromSegments();

        if (points.Count < 3)
        {
            Debug.LogWarning("Za mało punktów trasy. Sprawdź Start Node / End Node w RoadSegment.");
            return;
        }

        debugPoints = points;

        ClearArrows();

        GameObject root = new GameObject(generatedRootName);
        root.transform.SetParent(arrowsParent != null ? arrowsParent : transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(root, "Generate Route Arrows Root");
#endif

        for (int s = 1; s < raceRoute.segments.Count; s++)
        {
            RoadSegment prevSegment = raceRoute.GetSegment(s - 1);
            RoadSegment nextSegment = raceRoute.GetSegment(s);

            RoadNode node = raceRoute.GetExitNode(s - 1);

            if (node == null || node != raceRoute.GetEntryNode(s))
            {
                Debug.LogWarning($"[RouteArrows] Segmenty nie łączą się kierunkowo: {prevSegment?.name} -> {nextSegment?.name}", this);
                continue;
            }

            if (!node.isIntersection)
                continue;

            List<Vector3> prevPoints = GetSegmentPoints(prevSegment);
            List<Vector3> nextPoints = GetSegmentPoints(nextSegment);

            OrientSegmentPointsToNode(prevPoints, node, true);
            OrientSegmentPointsToNode(nextPoints, node, false);

            if (prevPoints.Count < 2 || nextPoints.Count < 2)
                continue;

            Vector3 current = node.transform.position;

            Vector3 prev = GetPointAlongSegmentFromNode(
                prevPoints,
                node,
                directionProbeInDistance,
                true
            );

            Vector3 next = GetPointAlongSegmentFromNode(
                nextPoints,
                node,
                directionProbeOutDistance,
                false
            );

            Vector3 dirIn = current - prev;
            Vector3 dirOut = next - current;

            dirIn.y = 0f;
            dirOut.y = 0f;

            if (dirIn.sqrMagnitude < 0.01f || dirOut.sqrMagnitude < 0.01f)
                continue;

            dirIn.Normalize();
            dirOut.Normalize();

            float signedAngle = Vector3.SignedAngle(dirIn, dirOut, Vector3.up);

            if (invertTurnDirection)
                signedAngle = -signedAngle;

            Debug.Log($"ARROW NODE: {node.name} | angle={signedAngle}");

            if (Mathf.Abs(signedAngle) < minTurnAngle)
                continue;

            bool turnRight = signedAngle > 0f;

            var ov = GetOverride(node);

            float startDistance = ov != null && ov.startDistance >= 0f ? ov.startDistance : arrowArcStartDistance;
            float endDistance = ov != null && ov.endDistance >= 0f ? ov.endDistance : arrowArcEndDistance;
            float controlDistance = ov != null && ov.controlDistance >= 0f ? ov.controlDistance : arrowArcControlDistance;
            float sideOffsetValue = ov != null && ov.sideOffset >= 0f ? ov.sideOffset : arrowArcSideOffset;
            float heightOffsetValue = ov != null && ov.heightOffset >= 0f ? ov.heightOffset : arrowHeightOffset;

            Vector3 extraPosOffset = ov != null ? ov.positionOffset : Vector3.zero;
            Vector3 extraRotOffset = ov != null ? ov.rotationOffsetEuler : Vector3.zero;
            bool invertSide = ov != null && ov.invertSide;
            bool reverseDir = reverseArrowDirection || (ov != null && ov.reverseDirection);

            Vector3 sideDir = Vector3.Cross(Vector3.up, dirIn).normalized;

            // dla prawego skrętu strzałki po lewej stronie wjazdu
            // dla lewego skrętu po prawej stronie wjazdu
            float sideSign = turnRight ? -1f : 1f;
            if (invertSide)
                sideSign *= -1f;

            Vector3 baseSideOffset = sideDir * sideOffsetValue * sideSign;

            int wallCount = (ov != null && ov.doubleWall) ? 2 : 1;
            float spacing = ov != null ? ov.doubleWallSpacing : 10f;

            for (int wall = 0; wall < wallCount; wall++)
            {
                float wallOffset = 0f;

                if (wallCount == 2)
                    wallOffset = wall == 0 ? -spacing * 0.5f : spacing * 0.5f;

                Vector3 sideOffset = baseSideOffset + sideDir * wallOffset;

                Vector3 p0 = current - dirIn * startDistance + sideOffset + extraPosOffset;
                Vector3 p2 = current + dirOut * endDistance + sideOffset + extraPosOffset;

                Vector3 bisector = (-dirIn + dirOut).normalized;
                if (bisector.sqrMagnitude < 0.01f)
                    bisector = dirOut;

                Vector3 p1 = current + bisector * controlDistance + sideOffset + extraPosOffset;

                for (int i = 0; i <= splineSamples; i++)
                {
                    float t = splineSamples <= 0 ? 0f : i / (float)splineSamples;

                    Vector3 worldPos = QuadraticBezier(p0, p1, p2, t);

                    float tPrev = Mathf.Clamp01(t - 0.01f);
                    float tNext = Mathf.Clamp01(t + 0.01f);

                    Vector3 pPrev = QuadraticBezier(p0, p1, p2, tPrev);
                    Vector3 pNext = QuadraticBezier(p0, p1, p2, tNext);

                    Vector3 worldTangent = (pNext - pPrev).normalized;

                    if (reverseDir)
                        worldTangent = -worldTangent;

                    if (worldTangent.sqrMagnitude < 0.001f)
                        continue;

                    Quaternion arrowRot = Quaternion.LookRotation(worldTangent, Vector3.up);
                    arrowRot *= Quaternion.Euler(rotationOffsetEuler + extraRotOffset);

                    GameObject arrow = Instantiate(
                        arrowPrefab,
                        worldPos + Vector3.up * heightOffsetValue,
                        arrowRot,
                        root.transform
                    );

#if UNITY_EDITOR
                    Undo.RegisterCreatedObjectUndo(arrow, "Generate Route Arrow");
#endif
                }
            }

            Debug.Log("Wygenerowano strzałki po splinie tylko na skrzyżowaniach trasy.");
        }
    }

    public void ShowRaceArrows()
    {
        GenerateArrows();
    }

    public void HideRaceArrows()
    {
        ClearArrows();
    }

    RoadNode GetSharedNode(RoadSegment a, RoadSegment b)
    {
        if (a == null || b == null)
            return null;

        if (a.startNode == b.startNode || a.startNode == b.endNode)
            return a.startNode;

        if (a.endNode == b.startNode || a.endNode == b.endNode)
            return a.endNode;

        return null;
    }

    void OrientSegmentPointsToNode(List<Vector3> points, RoadNode node, bool nodeShouldBeLast)
    {
        if (points == null || points.Count < 2 || node == null)
            return;

        Vector3 nodePos = node.transform.position;

        float distToFirst = Vector3.Distance(points[0], nodePos);
        float distToLast = Vector3.Distance(points[points.Count - 1], nodePos);

        bool nodeIsCloserToFirst = distToFirst < distToLast;

        if (nodeShouldBeLast && nodeIsCloserToFirst)
            points.Reverse();

        if (!nodeShouldBeLast && !nodeIsCloserToFirst)
            points.Reverse();
    }
    List<Vector3> BuildPointsFromSegments()
    {
        List<Vector3> points = new();

        if (raceRoute == null || raceRoute.segments == null || raceRoute.segments.Count == 0)
            return points;

        for (int i = 0; i < raceRoute.segments.Count; i++)
        {
            RoadSegment segment = raceRoute.GetSegment(i);

            if (segment == null || segment.startNode == null || segment.endNode == null)
                continue;

            List<Vector3> segmentPoints = GetSegmentPointsByRouteIndex(i);

            if (segmentPoints.Count < 2)
                continue;

            if (points.Count > 0 && Vector3.Distance(points[points.Count - 1], segmentPoints[0]) < 0.5f)
                segmentPoints.RemoveAt(0);

            points.AddRange(segmentPoints);
        }

        return points;
    }

    List<Vector3> GetSegmentPointsByRouteIndex(int index)
    {
        RoadSegment segment = raceRoute.GetSegment(index);
        List<Vector3> points = GetSegmentPoints(segment);

        if (raceRoute.segments[index].reverse)
            points.Reverse();

        return points;
    }

    List<Vector3> GetSegmentPoints(RoadSegment segment)
    {
        List<Vector3> result = new();

        SplineContainer container = segment.GetComponent<SplineContainer>();

        if (!useSplinePoints || container == null || container.Splines == null || container.Splines.Count == 0)
        {
            result.Add(segment.startNode.transform.position);
            result.Add(segment.endNode.transform.position);
            return result;
        }

        Spline spline = GetSelectedSplineForSegment(segment, container);

        if (spline == null || spline.Count < 2)
        {
            result.Add(segment.startNode.transform.position);
            result.Add(segment.endNode.transform.position);
            return result;
        }

        for (int i = 0; i <= samplesPerSegment; i++)
        {
            float t = i / (float)samplesPerSegment;

            Vector3 localPos = spline.EvaluatePosition(t);
            Vector3 worldPos = container.transform.TransformPoint(localPos);

            result.Add(worldPos);
        }

        float distStartToFirst = Vector3.Distance(segment.startNode.transform.position, result[0]);
        float distStartToLast = Vector3.Distance(segment.startNode.transform.position, result[result.Count - 1]);

        if (distStartToLast < distStartToFirst)
            result.Reverse();

        return result;
    }

    Spline GetSelectedSplineForSegment(RoadSegment segment, SplineContainer container)
    {
        if (container == null || container.Splines == null || container.Splines.Count == 0)
            return null;

        int index = Mathf.Clamp(segment.splineIndex, 0, container.Splines.Count - 1);
        return container.Splines[index];
    }

    [ContextMenu("CLEAR ROUTE ARROWS")]
    public void ClearArrows()
    {
        Transform parent = arrowsParent != null ? arrowsParent : transform;
        Transform oldRoot = parent.Find(generatedRootName);

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

    Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }
    void RebuildArrowPreview()
    {
        previewArcs.Clear();
        previewDirs.Clear();

        if (raceRoute == null)
            raceRoute = GetComponent<RaceRoute>();

        if (raceRoute == null || raceRoute.segments == null || raceRoute.segments.Count < 2)
            return;

        for (int s = 1; s < raceRoute.segments.Count; s++)
        {
            RoadSegment prevSegment = raceRoute.GetSegment(s - 1);
            RoadSegment nextSegment = raceRoute.GetSegment(s);

            RoadNode node = raceRoute.GetExitNode(s - 1);

            if (node == null || node != raceRoute.GetEntryNode(s))
                continue;

            if (!node.isIntersection)
                continue;

            List<Vector3> prevPoints = GetSegmentPointsByRouteIndex(s - 1);
            List<Vector3> nextPoints = GetSegmentPointsByRouteIndex(s);

            OrientSegmentPointsToNode(prevPoints, node, true);
            OrientSegmentPointsToNode(nextPoints, node, false);

            if (prevPoints.Count < 2 || nextPoints.Count < 2)
                continue;

            Vector3 current = node.transform.position;

            Vector3 prev = GetPointAlongSegmentFromNode(
                prevPoints,
                node,
                directionProbeInDistance,
                true
            );

            Vector3 next = GetPointAlongSegmentFromNode(
                nextPoints,
                node,
                directionProbeOutDistance,
                false
            );

            Vector3 dirIn = current - prev;
            Vector3 dirOut = next - current;

            dirIn.y = 0f;
            dirOut.y = 0f;

            if (dirIn.sqrMagnitude < 0.01f || dirOut.sqrMagnitude < 0.01f)
                continue;

            dirIn.Normalize();
            dirOut.Normalize();

            float signedAngle = Vector3.SignedAngle(dirIn, dirOut, Vector3.up);

            if (invertTurnDirection)
                signedAngle = -signedAngle;

            if (Mathf.Abs(signedAngle) < minTurnAngle)
                continue;

            bool turnRight = signedAngle > 0f;

            var ov = GetOverride(node);

            float startDistance = ov != null && ov.startDistance >= 0f ? ov.startDistance : arrowArcStartDistance;
            float endDistance = ov != null && ov.endDistance >= 0f ? ov.endDistance : arrowArcEndDistance;
            float controlDistance = ov != null && ov.controlDistance >= 0f ? ov.controlDistance : arrowArcControlDistance;
            float sideOffsetValue = ov != null && ov.sideOffset >= 0f ? ov.sideOffset : arrowArcSideOffset;
            float heightOffsetValue = ov != null && ov.heightOffset >= 0f ? ov.heightOffset : arrowHeightOffset;

            Vector3 extraPosOffset = ov != null ? ov.positionOffset : Vector3.zero;
            bool invertSide = ov != null && ov.invertSide;
            bool reverseDir = reverseArrowDirection || (ov != null && ov.reverseDirection);

            Vector3 sideDir = Vector3.Cross(Vector3.up, dirIn).normalized;

            float sideSign = turnRight ? -1f : 1f;
            if (invertSide)
                sideSign *= -1f;

            Vector3 baseSideOffset = sideDir * sideOffsetValue * sideSign;

            int wallCount = (ov != null && ov.doubleWall) ? 2 : 1;
            float spacing = ov != null ? ov.doubleWallSpacing : 10f;

            for (int wall = 0; wall < wallCount; wall++)
            {
                float wallOffset = 0f;

                if (wallCount == 2)
                    wallOffset = wall == 0 ? -spacing * 0.5f : spacing * 0.5f;

                Vector3 sideOffset = baseSideOffset + sideDir * wallOffset;

                Vector3 p0 = current - dirIn * startDistance + sideOffset + extraPosOffset;
                Vector3 p2 = current + dirOut * endDistance + sideOffset + extraPosOffset;

                Vector3 bisector = (-dirIn + dirOut).normalized;
                if (bisector.sqrMagnitude < 0.01f)
                    bisector = dirOut;

                Vector3 p1 = current + bisector * controlDistance + sideOffset + extraPosOffset;

                List<Vector3> arcPoints = new();
                List<Vector3> arcDirs = new();

                for (int i = 0; i <= splineSamples; i++)
                {
                    float t = splineSamples <= 0 ? 0f : i / (float)splineSamples;

                    Vector3 pos = QuadraticBezier(p0, p1, p2, t);

                    float tPrev = Mathf.Clamp01(t - 0.01f);
                    float tNext = Mathf.Clamp01(t + 0.01f);

                    Vector3 pPrev = QuadraticBezier(p0, p1, p2, tPrev);
                    Vector3 pNext = QuadraticBezier(p0, p1, p2, tNext);

                    Vector3 dir = (pNext - pPrev).normalized;

                    if (reverseDir)
                        dir = -dir;

                    arcPoints.Add(pos + Vector3.up * heightOffsetValue);
                    arcDirs.Add(dir);
                }

                previewArcs.Add(arcPoints);
                previewDirs.Add(arcDirs);
            }
        }
    }

    Vector3 GetPointAlongSegmentFromNode(List<Vector3> points, RoadNode node, float distance, bool nodeIsAtEnd)
    {
        if (points == null || points.Count < 2 || node == null)
            return node != null ? node.transform.position : Vector3.zero;

        Vector3 nodePos = node.transform.position;

        int index = nodeIsAtEnd ? points.Count - 1 : 0;
        Vector3 current = nodePos;
        float remaining = distance;

        if (nodeIsAtEnd)
        {
            for (int i = index; i > 0; i--)
            {
                Vector3 a = current;
                Vector3 b = points[i - 1];

                float d = Vector3.Distance(a, b);

                if (d >= remaining)
                    return Vector3.Lerp(a, b, remaining / d);

                remaining -= d;
                current = b;
            }
        }
        else
        {
            for (int i = index; i < points.Count - 1; i++)
            {
                Vector3 a = current;
                Vector3 b = points[i + 1];

                float d = Vector3.Distance(a, b);

                if (d >= remaining)
                    return Vector3.Lerp(a, b, remaining / d);

                remaining -= d;
                current = b;
            }
        }

        return current;
    }
    bool AreSegmentsConnectedInRouteDirection(RoadSegment a, RoadSegment b)
    {
        if (a == null || b == null)
            return false;

        return a.endNode == b.startNode;
    }

    void OnDrawGizmosSelected()
    {
        if (drawArrowPreview)
        {
            RebuildArrowPreview();

            for (int a = 0; a < previewArcs.Count; a++)
            {
                List<Vector3> arc = previewArcs[a];
                List<Vector3> dirs = previewDirs[a];

                Gizmos.color = previewArcColor;

                for (int i = 0; i < arc.Count - 1; i++)
                    Gizmos.DrawLine(arc[i], arc[i + 1]);

                Gizmos.color = previewDirectionColor;

                for (int i = 0; i < arc.Count; i++)
                {
                    Gizmos.DrawSphere(arc[i], 0.6f);
                    Gizmos.DrawRay(arc[i], dirs[i] * previewDirectionLength);
                }
            }
        }

        if (!drawDebugGizmos || debugPoints == null || debugPoints.Count < 2)
            return;

        Gizmos.color = Color.green;

        for (int i = 0; i < debugPoints.Count; i++)
        {
            Gizmos.DrawSphere(debugPoints[i] + Vector3.up * 2f, 2f);

#if UNITY_EDITOR
            Handles.Label(debugPoints[i] + Vector3.up * 5f, $"P{i}");
#endif
        }

        Gizmos.color = Color.cyan;

        for (int i = 0; i < debugPoints.Count - 1; i++)
        {
            Vector3 a = debugPoints[i] + Vector3.up * 2f;
            Vector3 b = debugPoints[i + 1] + Vector3.up * 2f;

            Gizmos.DrawLine(a, b);

#if UNITY_EDITOR
            Vector3 mid = (a + b) * 0.5f;
            Handles.Label(mid + Vector3.up * 2f, $"{i} -> {i + 1}");
#endif
        }
    }

    [System.Serializable]
    public class IntersectionArrowOverride
    {
        public RoadNode node;
        public bool enabled = true;

        public Vector3 positionOffset = Vector3.zero;
        public Vector3 rotationOffsetEuler = Vector3.zero;

        public float startDistance = -1f;
        public float endDistance = -1f;
        public float controlDistance = -1f;
        public float sideOffset = -1f;
        public float heightOffset = -1f;

        public bool invertSide = false;
        public bool reverseDirection = false;

        [Header("Double Wall")]
        public bool doubleWall = false;
        public float doubleWallSpacing = 10f;
    }

    [Header("Per Intersection Overrides")]
    public List<IntersectionArrowOverride> intersectionOverrides = new();

    IntersectionArrowOverride GetOverride(RoadNode node)
    {
        if (node == null || intersectionOverrides == null)
            return null;

        foreach (var ov in intersectionOverrides)
        {
            if (ov != null && ov.enabled && ov.node == node)
                return ov;
        }

        return null;
    }


}