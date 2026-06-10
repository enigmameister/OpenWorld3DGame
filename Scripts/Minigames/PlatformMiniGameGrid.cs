using System.Collections.Generic;
using UnityEngine;

public class PlatformMiniGameGrid : MonoBehaviour
{
    public static readonly List<PlatformTile> Tiles = new();

    [Header("Auto Grid")]
    [SerializeField] private float tileSpacingTolerance = 0.35f;

    private readonly Dictionary<Vector2Int, PlatformTile> grid = new();

    public int MinX { get; private set; }
    public int MaxX { get; private set; }
    public int MinY { get; private set; }
    public int MaxY { get; private set; }

    public bool IsBuilt { get; private set; }

    public static void Register(PlatformTile tile)
    {
        if (tile != null && !Tiles.Contains(tile))
            Tiles.Add(tile);
    }

    public static void Unregister(PlatformTile tile)
    {
        Tiles.Remove(tile);
    }

    public void BuildGrid()
    {
        grid.Clear();

        List<PlatformTile> validTiles = new();

        foreach (var tile in Tiles)
        {
            if (tile != null)
                validTiles.Add(tile);
        }

        if (validTiles.Count == 0)
        {
            IsBuilt = false;
            return;
        }

        validTiles.Sort((a, b) =>
        {
            int zCompare = a.transform.position.z.CompareTo(b.transform.position.z);

            if (Mathf.Abs(a.transform.position.z - b.transform.position.z) > tileSpacingTolerance)
                return zCompare;

            return a.transform.position.x.CompareTo(b.transform.position.x);
        });

        List<float> rows = new();

        foreach (var tile in validTiles)
        {
            float z = tile.transform.position.z;

            bool foundRow = false;

            for (int i = 0; i < rows.Count; i++)
            {
                if (Mathf.Abs(rows[i] - z) <= tileSpacingTolerance)
                {
                    foundRow = true;
                    break;
                }
            }

            if (!foundRow)
                rows.Add(z);
        }

        rows.Sort();

        MinX = 0;
        MinY = 0;
        MaxX = 0;
        MaxY = rows.Count - 1;

        for (int y = 0; y < rows.Count; y++)
        {
            List<PlatformTile> rowTiles = new();

            foreach (var tile in validTiles)
            {
                if (Mathf.Abs(tile.transform.position.z - rows[y]) <= tileSpacingTolerance)
                    rowTiles.Add(tile);
            }

            rowTiles.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            for (int x = 0; x < rowTiles.Count; x++)
            {
                PlatformTile tile = rowTiles[x];

                tile.GridX = x;
                tile.GridY = y;

                grid[new Vector2Int(x, y)] = tile;

                if (x > MaxX)
                    MaxX = x;
            }
        }

        IsBuilt = true;
    }

    public PlatformTile GetTile(int x, int y)
    {
        if (!IsBuilt)
            BuildGrid();

        grid.TryGetValue(new Vector2Int(x, y), out PlatformTile tile);
        return tile;
    }

    public void ResetTiles()
    {
        foreach (var tile in Tiles)
        {
            if (tile != null)
                tile.SetNormal();
        }
    }

    public void ClearDangerOnly()
    {
        foreach (var tile in Tiles)
        {
            if (tile != null && tile.IsDanger)
                tile.SetNormal();
        }
    }

    public void SetAllDanger()
    {
        foreach (var tile in Tiles)
        {
            if (tile != null)
                tile.SetDanger();
        }
    }

    public void SetAllSafe()
    {
        foreach (var tile in Tiles)
        {
            if (tile != null)
                tile.SetSafe();
        }
    }
    public void SetAllPathBlue()
    {
        foreach (var tile in Tiles)
        {
            if (tile != null)
                tile.SetPath();
        }
    }

    public void PaintEdgesOnly(int edgeDepth)
    {
        foreach (var tile in Tiles)
        {
            if (!tile) continue;

            bool startEdge = tile.GridY < MinY + edgeDepth;
            bool finishEdge = tile.GridY > MaxY - edgeDepth;

            if (startEdge || finishEdge)
                tile.SetSafe();
            else
                tile.SetNormal();
        }
    }

    public PlatformTile GetRandomTile()
    {
        if (Tiles.Count == 0)
            return null;

        return Tiles[Random.Range(0, Tiles.Count)];
    }

    public int GetRandomBlockX(int blockSize)
    {
        return Random.Range(MinX + 1, MaxX - blockSize);
    }

    public void SetTileBlock(int startX, int startY, int size, PlatformTileState state)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                PlatformTile tile = GetTile(startX + x, startY + y);
                if (!tile) continue;

                switch (state)
                {
                    case PlatformTileState.Path:
                        tile.SetPath();
                        break;

                    case PlatformTileState.Finish:
                        tile.SetFinish();
                        break;

                    case PlatformTileState.Safe:
                        tile.SetSafe();
                        break;

                    case PlatformTileState.Danger:
                        tile.SetDanger();
                        break;
                }
            }
        }
    }
}