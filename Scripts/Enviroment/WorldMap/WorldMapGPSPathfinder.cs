using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class WorldMapGPSPathfinder : MonoBehaviour
{
    [System.Serializable]
    private class RoadConnection
    {
        public RoadNode from;
        public RoadNode to;
        public RoadSegment segment;
    }
    public class RoadPointResult
    {
        public RoadSegment segment;
        public Vector3 point;
        public RoadNode startNode;
        public RoadNode endNode;
        public float t;
        public bool valid;
    }

    [Header("Road Data")]
    public RoadNode[] nodes;
    public RoadSegment[] segments;

    [Header("Spline Path")]
    [Min(4)] public int samplesPerSegment = 80;

    private readonly Dictionary<RoadNode, List<RoadConnection>> graph = new();

    void Awake()
    {
        BuildGraph();
    }

    [ContextMenu("Build GPS Graph")]
    public void BuildGraph()
    {
        graph.Clear();

        if (nodes == null || nodes.Length == 0)
            nodes = FindObjectsByType<RoadNode>(FindObjectsSortMode.None);

        if (segments == null || segments.Length == 0)
            segments = FindObjectsByType<RoadSegment>(FindObjectsSortMode.None);

        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] == null)
                continue;

            if (!graph.ContainsKey(nodes[i]))
                graph.Add(nodes[i], new List<RoadConnection>());
        }

        for (int i = 0; i < segments.Length; i++)
        {
            RoadSegment segment = segments[i];

            if (segment == null || segment.startNode == null || segment.endNode == null)
                continue;

            AddConnection(segment.startNode, segment.endNode, segment);
            AddConnection(segment.endNode, segment.startNode, segment);
        }
    }

    void AddConnection(RoadNode from, RoadNode to, RoadSegment segment)
    {
        if (from == null || to == null || segment == null)
            return;

        if (!graph.ContainsKey(from))
            graph.Add(from, new List<RoadConnection>());

        graph[from].Add(new RoadConnection
        {
            from = from,
            to = to,
            segment = segment
        });
    }

    public List<Vector3> FindPath(Vector3 startWorld, Vector3 targetWorld)
    {
        if (graph.Count == 0)
            BuildGraph();

        RoadPointResult startPoint = FindClosestPointOnRoad(startWorld);
        RoadPointResult targetPoint = FindClosestPointOnRoad(targetWorld);

        if (startPoint == null || !startPoint.valid || targetPoint == null || !targetPoint.valid)
            return new List<Vector3>();

        // jeœli start i cel s¹ na tym samym segmencie, jedŸ po tym samym splinie
        if (startPoint.segment == targetPoint.segment)
        {
            List<Vector3> sameSegmentPath = SampleSegmentBetween(
                startPoint.segment,
                startPoint.t,
                targetPoint.t,
                samplesPerSegment
            );

            if (sameSegmentPath.Count >= 2)
                return sameSegmentPath;
        }

        RoadNode[] startCandidates =
        {
        startPoint.startNode,
        startPoint.endNode
    };

        RoadNode[] targetCandidates =
        {
        targetPoint.startNode,
        targetPoint.endNode
    };

        List<Vector3> bestPath = new();
        float bestCost = float.MaxValue;

        for (int s = 0; s < startCandidates.Length; s++)
        {
            RoadNode startNode = startCandidates[s];

            if (startNode == null)
                continue;

            for (int t = 0; t < targetCandidates.Length; t++)
            {
                RoadNode targetNode = targetCandidates[t];

                if (targetNode == null)
                    continue;

                List<RoadNode> nodePath = AStar(startNode, targetNode);

                if (nodePath == null || nodePath.Count == 0)
                    continue;

                List<Vector3> finalPath = new();

                // 1. od pozycji gracza/auta do wybranego node — PO SPLINIE
                List<Vector3> startPartial = SampleSegmentBetween(
                    startPoint.segment,
                    startPoint.t,
                    GetRawTForNode(startPoint.segment, startNode),
                    Mathf.Max(8, samplesPerSegment / 3)
                );

                AppendPath(finalPath, startPartial);

                // 2. trasa miêdzy node — PO SEGMENTACH/SPLINE
                if (nodePath.Count >= 2)
                {
                    List<Vector3> middlePath = BuildWorldPathFromNodes(nodePath);
                    AppendPath(finalPath, middlePath);
                }

                // 3. od koñcowego node do punktu eventu — PO SPLINIE
                List<Vector3> targetPartial = SampleSegmentBetween(
                    targetPoint.segment,
                    GetRawTForNode(targetPoint.segment, targetNode),
                    targetPoint.t,
                    Mathf.Max(8, samplesPerSegment / 3)
                );

                AppendPath(finalPath, targetPartial);

                if (finalPath.Count < 2)
                    continue;

                float cost = GetPathLength(finalPath);

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestPath = finalPath;
                }
            }
        }

        return bestPath;
    }

    void AppendPath(List<Vector3> target, List<Vector3> source)
    {
        if (target == null || source == null || source.Count == 0)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            if (target.Count > 0 && Vector3.Distance(target[target.Count - 1], source[i]) < 0.25f)
                continue;

            target.Add(source[i]);
        }
    }

    float GetPathLength(List<Vector3> points)
    {
        if (points == null || points.Count < 2)
            return 0f;

        float length = 0f;

        for (int i = 1; i < points.Count; i++)
            length += Vector3.Distance(points[i - 1], points[i]);

        return length;
    }

    float GetRawTForNode(RoadSegment segment, RoadNode node)
    {
        if (segment == null || node == null)
            return 0f;

        SplineContainer container = segment.GetComponent<SplineContainer>();

        if (container == null || container.Splines == null || container.Splines.Count == 0)
        {
            if (node == segment.startNode)
                return 0f;

            return 1f;
        }

        int splineIndex = Mathf.Clamp(segment.splineIndex, 0, container.Splines.Count - 1);
        Spline spline = container.Splines[splineIndex];

        Vector3 rawStart = container.transform.TransformPoint(spline.EvaluatePosition(0f));
        Vector3 rawEnd = container.transform.TransformPoint(spline.EvaluatePosition(1f));

        float distToRawStart = Vector3.Distance(node.transform.position, rawStart);
        float distToRawEnd = Vector3.Distance(node.transform.position, rawEnd);

        return distToRawStart <= distToRawEnd ? 0f : 1f;
    }

    List<Vector3> SampleSegmentBetween(RoadSegment segment, float fromT, float toT, int samples)
    {
        List<Vector3> points = new();

        if (segment == null)
            return points;

        samples = Mathf.Max(2, samples);

        SplineContainer container = segment.GetComponent<SplineContainer>();

        if (container == null || container.Splines == null || container.Splines.Count == 0)
        {
            Vector3 a = segment.startNode != null ? segment.startNode.transform.position : Vector3.zero;
            Vector3 b = segment.endNode != null ? segment.endNode.transform.position : Vector3.zero;

            for (int i = 0; i <= samples; i++)
            {
                float k = i / (float)samples;
                float t = Mathf.Lerp(fromT, toT, k);
                points.Add(Vector3.Lerp(a, b, t));
            }

            return points;
        }

        int splineIndex = Mathf.Clamp(segment.splineIndex, 0, container.Splines.Count - 1);
        Spline spline = container.Splines[splineIndex];

        for (int i = 0; i <= samples; i++)
        {
            float k = i / (float)samples;
            float t = Mathf.Lerp(fromT, toT, k);

            Vector3 localPos = spline.EvaluatePosition(t);
            Vector3 worldPos = container.transform.TransformPoint(localPos);

            points.Add(worldPos);
        }

        return points;
    }

    RoadNode FindNearestNode(Vector3 position)
    {
        RoadNode best = null;
        float bestSqr = float.MaxValue;

        foreach (var pair in graph)
        {
            RoadNode node = pair.Key;

            if (node == null)
                continue;

            float sqr = (node.transform.position - position).sqrMagnitude;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = node;
            }
        }

        return best;
    }

    List<RoadNode> AStar(RoadNode start, RoadNode goal)
    {
        List<RoadNode> open = new();
        HashSet<RoadNode> closed = new();

        Dictionary<RoadNode, RoadNode> cameFrom = new();
        Dictionary<RoadNode, float> gScore = new();
        Dictionary<RoadNode, float> fScore = new();

        open.Add(start);
        gScore[start] = 0f;
        fScore[start] = Heuristic(start, goal);

        while (open.Count > 0)
        {
            RoadNode current = GetLowestFScore(open, fScore);

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            open.Remove(current);
            closed.Add(current);

            if (!graph.ContainsKey(current))
                continue;

            List<RoadConnection> connections = graph[current];

            for (int i = 0; i < connections.Count; i++)
            {
                RoadConnection connection = connections[i];

                if (connection == null || connection.to == null)
                    continue;

                RoadNode neighbor = connection.to;

                if (closed.Contains(neighbor))
                    continue;

                float tentativeG = gScore[current] + GetConnectionCost(connection);

                if (!open.Contains(neighbor))
                {
                    open.Add(neighbor);
                }
                else if (gScore.ContainsKey(neighbor) && tentativeG >= gScore[neighbor])
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                fScore[neighbor] = tentativeG + Heuristic(neighbor, goal);
            }
        }

        return new List<RoadNode>();
    }

    float GetConnectionCost(RoadConnection connection)
    {
        if (connection == null || connection.from == null || connection.to == null)
            return 999999f;

        if (connection.segment == null)
            return Vector3.Distance(connection.from.transform.position, connection.to.transform.position);

        return EstimateSegmentLength(connection.segment);
    }

    float EstimateSegmentLength(RoadSegment segment)
    {
        if (segment == null)
            return 999999f;

        SplineContainer container = segment.GetComponent<SplineContainer>();

        if (container == null || container.Splines == null || container.Splines.Count == 0)
        {
            if (segment.startNode != null && segment.endNode != null)
                return Vector3.Distance(segment.startNode.transform.position, segment.endNode.transform.position);

            return 999999f;
        }

        int splineIndex = Mathf.Clamp(segment.splineIndex, 0, container.Splines.Count - 1);
        Spline spline = container.Splines[splineIndex];

        Vector3 prev = container.transform.TransformPoint(spline.EvaluatePosition(0f));
        float length = 0f;

        int samples = Mathf.Max(8, samplesPerSegment / 2);

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 current = container.transform.TransformPoint(spline.EvaluatePosition(t));

            length += Vector3.Distance(prev, current);
            prev = current;
        }

        return length;
    }

    RoadNode GetLowestFScore(List<RoadNode> open, Dictionary<RoadNode, float> fScore)
    {
        RoadNode best = open[0];
        float bestScore = fScore.ContainsKey(best) ? fScore[best] : float.MaxValue;

        for (int i = 1; i < open.Count; i++)
        {
            RoadNode node = open[i];
            float score = fScore.ContainsKey(node) ? fScore[node] : float.MaxValue;

            if (score < bestScore)
            {
                best = node;
                bestScore = score;
            }
        }

        return best;
    }

    List<RoadNode> ReconstructPath(Dictionary<RoadNode, RoadNode> cameFrom, RoadNode current)
    {
        List<RoadNode> path = new();
        path.Add(current);

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    float Heuristic(RoadNode a, RoadNode b)
    {
        if (a == null || b == null)
            return 999999f;

        return Vector3.Distance(a.transform.position, b.transform.position);
    }

    List<Vector3> BuildWorldPathFromNodes(List<RoadNode> nodePath)
    {
        List<Vector3> result = new();

        if (nodePath == null || nodePath.Count < 2)
            return result;

        for (int i = 0; i < nodePath.Count - 1; i++)
        {
            RoadNode from = nodePath[i];
            RoadNode to = nodePath[i + 1];

            RoadConnection connection = GetConnection(from, to);

            if (connection == null || connection.segment == null)
                continue;

            List<Vector3> segmentPoints = GetSegmentPoints(connection.segment, from, to, samplesPerSegment);

            if (segmentPoints == null || segmentPoints.Count < 2)
                continue;

            if (result.Count > 0)
                segmentPoints.RemoveAt(0);

            result.AddRange(segmentPoints);
        }

        return result;
    }

    RoadConnection GetConnection(RoadNode from, RoadNode to)
    {
        if (from == null || to == null)
            return null;

        if (!graph.ContainsKey(from))
            return null;

        List<RoadConnection> connections = graph[from];

        for (int i = 0; i < connections.Count; i++)
        {
            RoadConnection connection = connections[i];

            if (connection != null && connection.to == to)
                return connection;
        }

        return null;
    }

    List<Vector3> GetSegmentPoints(RoadSegment segment, RoadNode from, RoadNode to, int samples)
    {
        List<Vector3> points = new();

        if (segment == null)
            return points;

        SplineContainer container = segment.GetComponent<SplineContainer>();

        if (container == null || container.Splines == null || container.Splines.Count == 0)
        {
            if (from != null)
                points.Add(from.transform.position);

            if (to != null)
                points.Add(to.transform.position);

            return points;
        }

        int splineIndex = Mathf.Clamp(segment.splineIndex, 0, container.Splines.Count - 1);
        Spline spline = container.Splines[splineIndex];

        samples = Mathf.Max(4, samples);

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;

            Vector3 localPos = spline.EvaluatePosition(t);
            Vector3 worldPos = container.transform.TransformPoint(localPos);

            points.Add(worldPos);
        }

        if (from != null && points.Count >= 2)
        {
            float distFromFirst = Vector3.Distance(from.transform.position, points[0]);
            float distFromLast = Vector3.Distance(from.transform.position, points[points.Count - 1]);

            if (distFromLast < distFromFirst)
                points.Reverse();
        }

        return points;
    }

    public RoadPointResult FindClosestPointOnRoad(Vector3 worldPos)
    {
        RoadPointResult best = new RoadPointResult();
        float bestSqr = float.MaxValue;

        if (segments == null || segments.Length == 0)
            segments = FindObjectsByType<RoadSegment>(FindObjectsSortMode.None);

        for (int i = 0; i < segments.Length; i++)
        {
            RoadSegment segment = segments[i];

            if (segment == null || segment.startNode == null || segment.endNode == null)
                continue;

            SplineContainer container = segment.GetComponent<SplineContainer>();

            if (container == null || container.Splines == null || container.Splines.Count == 0)
            {
                Vector3 a = segment.startNode.transform.position;
                Vector3 b = segment.endNode.transform.position;

                Vector3 p = ClosestPointOnLine(a, b, worldPos);
                float sqr = (worldPos - p).sqrMagnitude;

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best.segment = segment;
                    best.point = p;
                    best.startNode = segment.startNode;
                    best.endNode = segment.endNode;
                    best.t = 0f;
                    best.valid = true;
                }

                continue;
            }

            int splineIndex = Mathf.Clamp(segment.splineIndex, 0, container.Splines.Count - 1);
            Spline spline = container.Splines[splineIndex];

            int samples = Mathf.Max(8, samplesPerSegment);

            Vector3 prev = container.transform.TransformPoint(spline.EvaluatePosition(0f));
            float prevT = 0f;

            for (int s = 1; s <= samples; s++)
            {
                float t = s / (float)samples;
                Vector3 current = container.transform.TransformPoint(spline.EvaluatePosition(t));

                Vector3 p = ClosestPointOnLine(prev, current, worldPos);
                float sqr = (worldPos - p).sqrMagnitude;

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best.segment = segment;
                    best.point = p;
                    best.startNode = segment.startNode;
                    best.endNode = segment.endNode;
                    best.t = Mathf.Lerp(prevT, t, GetSegmentLerp01(prev, current, p));
                    best.valid = true;
                }

                prev = current;
                prevT = t;
            }
        }

        return best;
    }

    Vector3 ClosestPointOnLine(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude);
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    float GetSegmentLerp01(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        return Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
    }
}