using UnityEngine;

public class PlatformMiniGamePainter : MonoBehaviour
{
    private PlatformMiniGameGrid grid;

    public void Init(PlatformMiniGameGrid miniGameGrid)
    {
        grid = miniGameGrid;
    }

    public PlatformTile SetStartTile()
    {
        if (grid == null)
            return null;

        PlatformTile startTile = grid.GetRandomTile();

        if (startTile)
            ApplyArenaIdlePatternAroundStart(startTile);

        return startTile;
    }

    public void ApplyArenaIdlePatternAroundStart(PlatformTile startTile)
    {
        if (grid == null || startTile == null)
            return;

        foreach (var tile in PlatformMiniGameGrid.Tiles)
        {
            if (!tile) continue;

            int dx = Mathf.Abs(tile.GridX - startTile.GridX);
            int dy = Mathf.Abs(tile.GridY - startTile.GridY);

            int dist = Mathf.Max(dx, dy);

            if (tile == startTile)
            {
                tile.SetStart();
            }
            else if (dist == 1)
            {
                tile.SetPath();
            }
            else if (dist == 2)
            {
                tile.SetSafe();
            }
            else if (dist == 3)
            {
                tile.SetWarning();
            }
            else if ((tile.GridX + tile.GridY) % 4 == 0)
            {
                tile.SetPath();
            }
            else if ((tile.GridX - tile.GridY) % 6 == 0)
            {
                tile.SetSafe();
            }
            else
            {
                int pattern = Mathf.Abs(tile.GridX * 31 + tile.GridY * 17) % 4;

                switch (pattern)
                {
                    case 0:
                        tile.SetPath();
                        break;

                    case 1:
                        tile.SetSafe();
                        break;

                    case 2:
                        tile.SetWarning();
                        break;

                    default:
                        tile.SetDecor();
                        break;
                }
            }
        }
    }
}