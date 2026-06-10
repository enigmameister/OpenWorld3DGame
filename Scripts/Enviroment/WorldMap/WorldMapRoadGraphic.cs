using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasRenderer))]
public class WorldMapRoadGraphic : MaskableGraphic
{
    [Header("Roads")]
    public List<List<Vector2>> paths = new();

    [Header("Style")]
    public float lineWidth = 6f;

    public void SetPaths(List<List<Vector2>> newPaths)
    {
        paths = newPaths;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (paths == null)
            return;

        for (int p = 0; p < paths.Count; p++)
        {
            List<Vector2> path = paths[p];

            if (path == null || path.Count < 2)
                continue;

            for (int i = 0; i < path.Count - 1; i++)
                AddLine(vh, path[i], path[i + 1], lineWidth, color);
        }
    }

    void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color col)
    {
        Vector2 dir = b - a;

        if (dir.sqrMagnitude < 0.001f)
            return;

        dir.Normalize();

        Vector2 normal = new Vector2(-dir.y, dir.x) * (width * 0.5f);

        int index = vh.currentVertCount;

        vh.AddVert(a - normal, col, Vector2.zero);
        vh.AddVert(a + normal, col, Vector2.zero);
        vh.AddVert(b + normal, col, Vector2.zero);
        vh.AddVert(b - normal, col, Vector2.zero);

        vh.AddTriangle(index + 0, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index + 0);
    }
}