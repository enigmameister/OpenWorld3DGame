using System.Collections.Generic;
using UnityEngine;

public class RaceRoute : MonoBehaviour
{
    [System.Serializable]
    public class RouteSegmentEntry
    {
        public RoadSegment segment;
        public bool reverse;
    }

    [Header("Route Segments In Order")]
    public List<RouteSegmentEntry> segments = new();

    [Header("Route Settings")]
    public bool loop;

    public int Count => segments != null ? segments.Count : 0;

    public RoadSegment GetSegment(int index)
    {
        if (segments == null || index < 0 || index >= segments.Count)
            return null;

        return segments[index].segment;
    }

    public RoadNode GetEntryNode(int index)
    {
        if (segments == null || index < 0 || index >= segments.Count)
            return null;

        RouteSegmentEntry entry = segments[index];
        if (entry == null || entry.segment == null)
            return null;

        return entry.reverse ? entry.segment.endNode : entry.segment.startNode;
    }

    public RoadNode GetExitNode(int index)
    {
        if (segments == null || index < 0 || index >= segments.Count)
            return null;

        RouteSegmentEntry entry = segments[index];
        if (entry == null || entry.segment == null)
            return null;

        return entry.reverse ? entry.segment.startNode : entry.segment.endNode;
    }

    [ContextMenu("VALIDATE ROUTE")]
    void ValidateRoute()
    {
        for (int i = 1; i < segments.Count; i++)
        {
            var prev = GetExitNode(i - 1);
            var next = GetEntryNode(i);

            if (prev != next)
            {
                Debug.LogWarning($"Route broken between {i - 1} and {i}", this);
            }
        }
    }
}