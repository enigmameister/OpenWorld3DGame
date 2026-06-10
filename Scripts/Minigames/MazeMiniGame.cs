using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeMiniGame : MonoBehaviour
{
    [Header("Stages")]
    [SerializeField] private int mazeStages = 3;
    [SerializeField] private float stageStartPause = 0.25f;

    [Header("Stage 3 Pendulum")]
    [SerializeField] private float pendulumDegreesPerSecond = 30f;
    [SerializeField] private int pendulumArmLength = 6;

    private int currentMazeStage;

    private class MazeData
    {
        public Vector2Int start;
        public Vector2Int entryCell;
        public Vector2Int finish;

        public HashSet<Vector2Int> passable = new();
        public HashSet<Vector2Int> startZone = new();
        public List<Vector2Int> solutionPath = new();
        public List<Vector2Int> activePendulumTiles = new();

        public List<Vector2Int> finishBlockers = new();
        public List<Vector2Int> decorTargets = new();
        public HashSet<Vector2Int> activatedDecorTargets = new();

        public Dictionary<Vector2Int, PlatformTileState> baseStates = new();
    }

    public IEnumerator Run(
       PlatformMiniGameGrid grid,
       int edgeDepth,
       bool hasPendingSequenceStart,
       Vector2Int pendingSequenceStart,
       Func<bool> isFinished,
       Action resetFinish,
       Func<bool> isGameRunning,
       Func<PlatformTile> getCurrentPlayerTile,
       Func<int> getLivesLeft,
       Action checkPlayerDanger,
       Action loseGame,
       Action<string, int, int, int> showGameScreen,
       Action<float> updateGameTimes,
       Func<string, int, int, float, string, IEnumerator> showStageWinScreen,
       Func<string> getNextSequenceName,
       Action hideWindow,
       bool skipFirstStageStart,
       Action<bool> setMazeActive)
    {
        bool skipStartOnlyOnce = skipFirstStageStart;

        for (int stage = currentMazeStage; stage < mazeStages; stage++)
        {
            currentMazeStage = stage;

            if (!isGameRunning())
                yield break;

            resetFinish?.Invoke();

            if (!skipStartOnlyOnce)
            {
                yield return WaitForStageStart(
                    grid,
                    edgeDepth,
                    getCurrentPlayerTile,
                    isGameRunning
                );
            }

            skipStartOnlyOnce = false;

            showGameScreen?.Invoke(
                "Maze",
                stage + 1,
                mazeStages,
                getLivesLeft != null ? getLivesLeft() : 0
            );

            if (!isGameRunning())
                yield break;

            Vector2Int start = ResolveStartPosition(
                grid,
                stage,
                hasPendingSequenceStart,
                pendingSequenceStart,
                getCurrentPlayerTile
            );

            MazeData maze = BuildMazeStage(grid, start, stage);

            float stageStartTime = Time.time;
            float pendulumAngle = 90f;

            setMazeActive?.Invoke(true);

            while (!isFinished() && isGameRunning())
            {
                float stageElapsed = Time.time - stageStartTime;
                updateGameTimes?.Invoke(stageElapsed);

                PlatformTile currentTile = getCurrentPlayerTile();

                if (stage == 1 && currentTile != null)
                {
                    HandleStage2DecorActivation(grid, maze, currentTile);
                }

                if (stage == 2)
                {
                    pendulumAngle -= pendulumDegreesPerSecond * Time.deltaTime;
                    UpdateStage3Pendulum(grid, maze, pendulumAngle);
                }

                if (currentTile != null && currentTile.IsDanger)
                {
                    loseGame?.Invoke();
                    yield break;
                }

                checkPlayerDanger?.Invoke();

                yield return null;
            }

            if (stage == 2)
            {
                RestoreDynamicPendulumTiles(grid, maze);
                maze.activePendulumTiles.Clear();
            }

            setMazeActive?.Invoke(false);

            if (!isGameRunning())
                yield break;

            grid.SetAllSafe();

            float clearTime = Time.time - stageStartTime;

            string nextName = stage + 1 < mazeStages
                ? "MAZE"
                : getNextSequenceName != null
                    ? getNextSequenceName()
                    : "FINISH";

            if (showStageWinScreen != null)
            {
                yield return showStageWinScreen(
                    "Maze",
                    stage + 1,
                    mazeStages,
                    clearTime,
                    nextName
                );
            }

            currentMazeStage = stage + 1;

            hideWindow?.Invoke();

            yield return new WaitForSeconds(stageStartPause);
        }

        currentMazeStage = 0;
        grid.SetAllSafe();
    }

    private IEnumerator WaitForStageStart(
        PlatformMiniGameGrid grid,
        int edgeDepth,
        Func<PlatformTile> getCurrentPlayerTile,
        Func<bool> isGameRunning)
    {
        grid.ResetTiles();

        Vector2Int start = GetRandomEdgePosition(grid);

        PlatformTile startTile = grid.GetTile(start.x, start.y);

        if (startTile)
            startTile.SetStart();

        while (isGameRunning())
        {
            PlatformTile current = getCurrentPlayerTile();

            if (current != null && current.IsStart)
                break;

            yield return null;
        }
    }

    private Vector2Int ResolveStartPosition(
        PlatformMiniGameGrid grid,
        int stage,
        bool hasPendingSequenceStart,
        Vector2Int pendingSequenceStart,
        Func<PlatformTile> getCurrentPlayerTile)
    {
        if (stage == 0 && hasPendingSequenceStart)
            return pendingSequenceStart;

        PlatformTile current = getCurrentPlayerTile();

        if (current != null)
            return new Vector2Int(current.GridX, current.GridY);

        return GetRandomEdgePosition(grid);
    }

    private MazeData BuildMazeStage(
      PlatformMiniGameGrid grid,
      Vector2Int start,
      int stage)
    {
        MazeData maze = new MazeData();

        maze.start = ClampToGrid(grid, start);
        maze.entryCell = ClampToInteriorCell(grid, maze.start);

        PaintAllDanger(grid);

        GeneratePerfectMaze(grid, maze);

        ConnectStartZoneToEntry(grid, maze);

        maze.finish = GetRandomFarFinishPosition(
            grid,
            maze,
            stage
        );

        EnsureFinishReachable(grid, maze);

        maze.solutionPath = FindPath(
            maze.entryCell,
            maze.finish,
            maze.passable
        );

        SealFinishToSingleEntrance(grid, maze);

        maze.solutionPath = FindPath(
            maze.entryCell,
            maze.finish,
            maze.passable
        );

        if (stage == 2)
        {
            if (PathUsesPendulumCenter(grid, maze.solutionPath))
            {
                RemovePendulumCenterFromMaze(grid, maze);

                maze.solutionPath = FindPath(
                    maze.entryCell,
                    maze.finish,
                    maze.passable
                );

                if (maze.solutionPath == null || maze.solutionPath.Count == 0)
                {
                    maze.finish = GetRandomFarFinishPosition(grid, maze, stage);

                    maze.solutionPath = FindPath(
                        maze.entryCell,
                        maze.finish,
                        maze.passable
                    );
                }
            }
        }

        PaintMaze(grid, maze);
        ForceSingleOuterWall(grid, maze);
        FixDoubleOuterWalls(grid, maze);

        PlatformTile finishTile = grid.GetTile(maze.finish.x, maze.finish.y);
        if (finishTile)
            finishTile.SetFinish();

        if (stage == 1)
        {
            ConfigureStage2LocksAndDecor(grid, maze);
        }
        else if (stage == 2)
        {
            SaveBaseStates(grid, maze);
        }

        return maze;
    }

    private bool PathUsesPendulumCenter(
    PlatformMiniGameGrid grid,
    List<Vector2Int> path)
    {
        if (path == null)
            return false;

        int centerX = (grid.MinX + grid.MaxX) / 2;
        int centerY = (grid.MinY + grid.MaxY) / 2;

        HashSet<Vector2Int> center = new()
    {
        new Vector2Int(centerX, centerY),
        new Vector2Int(centerX + 1, centerY),
        new Vector2Int(centerX, centerY + 1),
        new Vector2Int(centerX + 1, centerY + 1)
    };

        foreach (Vector2Int pos in path)
        {
            if (center.Contains(pos))
                return true;
        }

        return false;
    }

    private void RemovePendulumCenterFromMaze(
        PlatformMiniGameGrid grid,
        MazeData maze)
    {
        int centerX = (grid.MinX + grid.MaxX) / 2;
        int centerY = (grid.MinY + grid.MaxY) / 2;

        Vector2Int[] centerTiles =
        {
        new Vector2Int(centerX, centerY),
        new Vector2Int(centerX + 1, centerY),
        new Vector2Int(centerX, centerY + 1),
        new Vector2Int(centerX + 1, centerY + 1)
    };

        foreach (Vector2Int pos in centerTiles)
        {
            maze.passable.Remove(pos);

            PlatformTile tile = grid.GetTile(pos.x, pos.y);
            if (tile)
                tile.SetDanger();
        }
    }

    private void FixDoubleOuterWalls(
    PlatformMiniGameGrid grid,
    MazeData maze)
    {
        OpenInnerWallIfTouchesPath(
            grid,
            maze,
            innerX: grid.MinX + 1,
            innerY: null,
            checkOffset: new Vector2Int(1, 0)
        );

        OpenInnerWallIfTouchesPath(
            grid,
            maze,
            innerX: grid.MaxX - 1,
            innerY: null,
            checkOffset: new Vector2Int(-1, 0)
        );

        OpenInnerWallIfTouchesPath(
            grid,
            maze,
            innerX: null,
            innerY: grid.MinY + 1,
            checkOffset: new Vector2Int(0, 1)
        );

        OpenInnerWallIfTouchesPath(
            grid,
            maze,
            innerX: null,
            innerY: grid.MaxY - 1,
            checkOffset: new Vector2Int(0, -1)
        );
    }

    private void OpenInnerWallIfTouchesPath(
    PlatformMiniGameGrid grid,
    MazeData maze,
    int? innerX,
    int? innerY,
    Vector2Int checkOffset)
    {
        for (int x = grid.MinX + 1; x <= grid.MaxX - 1; x++)
        {
            for (int y = grid.MinY + 1; y <= grid.MaxY - 1; y++)
            {
                if (innerX.HasValue && x != innerX.Value)
                    continue;

                if (innerY.HasValue && y != innerY.Value)
                    continue;

                Vector2Int pos = new Vector2Int(x, y);

                if (maze.passable.Contains(pos))
                    continue;

                if (maze.startZone.Contains(pos))
                    continue;

                if (pos == maze.finish)
                    continue;

                Vector2Int neighbour = pos + checkOffset;

                if (!maze.passable.Contains(neighbour))
                    continue;

                maze.passable.Add(pos);

                PlatformTile tile = grid.GetTile(pos.x, pos.y);
                if (tile)
                    tile.SetSafe();
            }
        }
    }
    private Vector2Int GetNearestPassableToTarget(
    Vector2Int target,
    HashSet<Vector2Int> passable)
    {
        Vector2Int best = target;
        float bestDistance = float.MaxValue;

        foreach (Vector2Int pos in passable)
        {
            float distance = Vector2Int.Distance(pos, target);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pos;
            }
        }

        return best;
    }

    private void SealFinishToSingleEntrance(
        PlatformMiniGameGrid grid,
        MazeData maze)
    {
        if (maze.solutionPath == null || maze.solutionPath.Count < 2)
            return;

        Vector2Int entrance = maze.solutionPath[^2];

        foreach (Vector2Int neighbour in GetNeighbours(maze.finish))
        {
            if (neighbour == entrance)
                continue;

            if (!maze.passable.Contains(neighbour))
                continue;

            if (maze.startZone.Contains(neighbour))
                continue;

            maze.passable.Remove(neighbour);

            PlatformTile tile = grid.GetTile(neighbour.x, neighbour.y);
            if (tile)
                tile.SetDanger();
        }
    }

    private void EnsureFinishReachable(
        PlatformMiniGameGrid grid,
        MazeData maze)
    {
        if (maze.passable.Contains(maze.finish))
            return;

        Vector2Int walker = maze.entryCell;

        int guard = 0;

        while (walker != maze.finish && guard < 500)
        {
            guard++;

            maze.passable.Add(walker);

            if (walker.x < maze.finish.x)
                walker.x++;
            else if (walker.x > maze.finish.x)
                walker.x--;
            else if (walker.y < maze.finish.y)
                walker.y++;
            else if (walker.y > maze.finish.y)
                walker.y--;
        }

        maze.passable.Add(maze.finish);
    }
    private void PaintAllDanger(PlatformMiniGameGrid grid)
    {
        foreach (var tile in PlatformMiniGameGrid.Tiles)
        {
            if (tile != null)
                tile.SetDanger();
        }
    }

    private void GeneratePerfectMaze(
      PlatformMiniGameGrid grid,
      MazeData maze)
    {
        List<int> cellXs = BuildMazeCellCoordinates(grid.MinX, grid.MaxX);
        List<int> cellYs = BuildMazeCellCoordinates(grid.MinY, grid.MaxY);

        if (cellXs.Count == 0 || cellYs.Count == 0)
            return;

        Vector2Int startCell = FindNearestMazeCell(
            maze.entryCell,
            cellXs,
            cellYs
        );

        maze.entryCell = startCell;

        Stack<Vector2Int> stack = new();
        HashSet<Vector2Int> visited = new();

        visited.Add(startCell);
        stack.Push(startCell);
        maze.passable.Add(startCell);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();

            List<Vector2Int> candidates = GetUnvisitedMazeNeighbours(
                current,
                cellXs,
                cellYs,
                visited
            );

            if (candidates.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            CarveLineBetweenCells(maze, current, chosen);

            visited.Add(chosen);
            stack.Push(chosen);
        }
    }

    private List<int> BuildMazeCellCoordinates(int min, int max)
    {
        List<int> result = new();

        int innerMin = min + 1;
        int innerMax = max - 1;

        for (int value = innerMin; value <= innerMax; value += 2)
        {
            result.Add(value);
        }

        return result;
    }

    private Vector2Int FindNearestMazeCell(
        Vector2Int source,
        List<int> cellXs,
        List<int> cellYs)
    {
        int bestX = cellXs[0];
        int bestY = cellYs[0];
        float bestDistance = float.MaxValue;

        for (int y = 0; y < cellYs.Count; y++)
        {
            for (int x = 0; x < cellXs.Count; x++)
            {
                Vector2Int candidate = new Vector2Int(cellXs[x], cellYs[y]);
                float distance = Vector2Int.Distance(source, candidate);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestX = candidate.x;
                    bestY = candidate.y;
                }
            }
        }

        return new Vector2Int(bestX, bestY);
    }

    private List<Vector2Int> GetUnvisitedMazeNeighbours(
        Vector2Int current,
        List<int> cellXs,
        List<int> cellYs,
        HashSet<Vector2Int> visited)
    {
        List<Vector2Int> result = new();

        int xIndex = cellXs.IndexOf(current.x);
        int yIndex = cellYs.IndexOf(current.y);

        TryAddMazeNeighbour(cellXs, cellYs, visited, result, xIndex - 1, yIndex);
        TryAddMazeNeighbour(cellXs, cellYs, visited, result, xIndex + 1, yIndex);
        TryAddMazeNeighbour(cellXs, cellYs, visited, result, xIndex, yIndex - 1);
        TryAddMazeNeighbour(cellXs, cellYs, visited, result, xIndex, yIndex + 1);

        return result;
    }

    private void TryAddMazeNeighbour(
        List<int> cellXs,
        List<int> cellYs,
        HashSet<Vector2Int> visited,
        List<Vector2Int> result,
        int xIndex,
        int yIndex)
    {
        if (xIndex < 0 || xIndex >= cellXs.Count)
            return;

        if (yIndex < 0 || yIndex >= cellYs.Count)
            return;

        Vector2Int candidate = new Vector2Int(cellXs[xIndex], cellYs[yIndex]);

        if (visited.Contains(candidate))
            return;

        result.Add(candidate);
    }

    private void CarveLineBetweenCells(
        MazeData maze,
        Vector2Int from,
        Vector2Int to)
    {
        Vector2Int pos = from;

        maze.passable.Add(pos);

        int dx = Math.Sign(to.x - from.x);
        int dy = Math.Sign(to.y - from.y);

        int guard = 0;

        while (pos != to && guard < 20)
        {
            guard++;

            if (pos.x != to.x)
                pos.x += dx;
            else if (pos.y != to.y)
                pos.y += dy;

            maze.passable.Add(pos);
        }
    }

    private void ConnectStartZoneToEntry(
        PlatformMiniGameGrid grid,
        MazeData maze)
    {
        Vector2Int startPos = new Vector2Int(
            Mathf.Clamp(maze.start.x, grid.MinX + 1, grid.MaxX - 2),
            Mathf.Clamp(maze.start.y, grid.MinY + 1, grid.MaxY - 2)
        );

        maze.start = startPos;

        for (int y = 0; y <= 1; y++)
        {
            for (int x = 0; x <= 1; x++)
            {
                Vector2Int pos = new Vector2Int(startPos.x + x, startPos.y + y);

                maze.startZone.Add(pos);
                maze.passable.Add(pos);
            }
        }

        Vector2Int walker = maze.start;

        int guard = 0;

        while (walker != maze.entryCell && guard < 200)
        {
            guard++;

            maze.passable.Add(walker);

            if (walker.x < maze.entryCell.x)
                walker.x++;
            else if (walker.x > maze.entryCell.x)
                walker.x--;
            else if (walker.y < maze.entryCell.y)
                walker.y++;
            else if (walker.y > maze.entryCell.y)
                walker.y--;
        }

        maze.passable.Add(maze.entryCell);
    }

    private void PaintMaze(
        PlatformMiniGameGrid grid,
        MazeData maze)
    {
        foreach (Vector2Int pos in maze.passable)
        {
            PlatformTile tile = grid.GetTile(pos.x, pos.y);

            if (tile)
                tile.SetSafe();
        }

        foreach (Vector2Int pos in maze.startZone)
        {
            PlatformTile tile = grid.GetTile(pos.x, pos.y);

            if (tile)
                tile.SetPath();
        }
    }
    private List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int finish,
        HashSet<Vector2Int> passable)
    {
        Queue<Vector2Int> queue = new();
        Dictionary<Vector2Int, Vector2Int> parent = new();

        queue.Enqueue(start);
        parent[start] = start;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == finish)
                break;

            foreach (Vector2Int next in GetNeighbours(current))
            {
                if (!passable.Contains(next))
                    continue;

                if (parent.ContainsKey(next))
                    continue;

                parent[next] = current;
                queue.Enqueue(next);
            }
        }

        List<Vector2Int> path = new();

        if (!parent.ContainsKey(finish))
            return path;

        Vector2Int walker = finish;

        while (walker != start)
        {
            path.Add(walker);
            walker = parent[walker];
        }

        path.Add(start);
        path.Reverse();

        return path;
    }

    private IEnumerable<Vector2Int> GetNeighbours(Vector2Int pos)
    {
        yield return new Vector2Int(pos.x + 1, pos.y);
        yield return new Vector2Int(pos.x - 1, pos.y);
        yield return new Vector2Int(pos.x, pos.y + 1);
        yield return new Vector2Int(pos.x, pos.y - 1);
    }

    private Vector2Int ClampToGrid(
        PlatformMiniGameGrid grid,
        Vector2Int pos)
    {
        return new Vector2Int(
            Mathf.Clamp(pos.x, grid.MinX, grid.MaxX),
            Mathf.Clamp(pos.y, grid.MinY, grid.MaxY)
        );
    }

    private Vector2Int ClampToInteriorCell(
    PlatformMiniGameGrid grid,
    Vector2Int pos)
    {
        int x = Mathf.Clamp(pos.x, grid.MinX + 1, grid.MaxX - 1);
        int y = Mathf.Clamp(pos.y, grid.MinY + 1, grid.MaxY - 1);

        int localX = x - (grid.MinX + 1);
        int localY = y - (grid.MinY + 1);

        if (localX % 2 != 0)
            x += x < grid.MaxX - 2 ? 1 : -1;

        if (localY % 2 != 0)
            y += y < grid.MaxY - 2 ? 1 : -1;

        return new Vector2Int(x, y);
    }

    private void Shuffle(List<Vector2Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
        }
    }

    public Vector2Int GetRandomEdgePosition(PlatformMiniGameGrid grid)
    {
        int side = UnityEngine.Random.Range(0, 4);

        int minStartX = grid.MinX + 1;
        int maxStartX = grid.MaxX - 2;

        int minStartY = grid.MinY + 1;
        int maxStartY = grid.MaxY - 2;

        return side switch
        {
            0 => new Vector2Int(UnityEngine.Random.Range(minStartX, maxStartX + 1), minStartY),
            1 => new Vector2Int(UnityEngine.Random.Range(minStartX, maxStartX + 1), maxStartY),
            2 => new Vector2Int(minStartX, UnityEngine.Random.Range(minStartY, maxStartY + 1)),
            _ => new Vector2Int(maxStartX, UnityEngine.Random.Range(minStartY, maxStartY + 1))
        };
    }

    public void ResetProgress()
    {
        currentMazeStage = 0;
    }

    private void ConfigureStage2LocksAndDecor(
    PlatformMiniGameGrid grid,
    MazeData maze)
    {
        if (maze.solutionPath.Count < 8)
            return;

        // 3 czarne tile przed finishem
        for (int i = 4; i >= 2; i--)
        {
            int index = maze.solutionPath.Count - i;

            if (index < 0 || index >= maze.solutionPath.Count)
                continue;

            Vector2Int pos = maze.solutionPath[index];

            if (pos == maze.finish)
                continue;
            
            maze.finishBlockers.Add(pos);

            PlatformTile tile = grid.GetTile(pos.x, pos.y);
            if (tile)
                tile.SetBlocked();
        }

        SealBlockerCorridorSides(grid, maze);

        // Kandydaci na szare decor tile:
        // dostêpne koñce œlepych odnóg przed blokad¹.
        // Gracz musi zaryzykowaæ wejœcie po klucz,
        // a po zebraniu klucza maze odœwie¿a zapadaj¹ce siê pola.

        List<Vector2Int> candidates = new();
        HashSet<Vector2Int> solution = new HashSet<Vector2Int>(maze.solutionPath);
        HashSet<Vector2Int> reachableBeforeBlockers = GetReachableBeforeBlockers(maze);

        foreach (Vector2Int pos in reachableBeforeBlockers)
        {
            if (maze.startZone.Contains(pos))
                continue;

            if (pos == maze.finish)
                continue;

            if (maze.finishBlockers.Contains(pos))
                continue;

            if (solution.Contains(pos))
                continue;

            if (GetPassableNeighbourCount(pos, maze.passable) > 1)
                continue;

            candidates.Add(pos);
        }

        if (candidates.Count < 3)
        {
            foreach (Vector2Int pos in reachableBeforeBlockers)
            {
                if (candidates.Contains(pos))
                    continue;

                if (maze.startZone.Contains(pos))
                    continue;

                if (pos == maze.finish)
                    continue;

                if (maze.finishBlockers.Contains(pos))
                    continue;

                if (solution.Contains(pos))
                    continue;

                candidates.Add(pos);

                if (candidates.Count >= 3)
                    break;
            }
        }

        Shuffle(candidates);

        int count = Mathf.Min(3, candidates.Count);

        for (int i = 0; i < count; i++)
        {
            Vector2Int pos = candidates[i];
            maze.decorTargets.Add(pos);

            PlatformTile tile = grid.GetTile(pos.x, pos.y);
            if (tile)
                tile.SetDecor();
        }

        SaveBaseStates(grid, maze);
    }

    private void SealBlockerCorridorSides(
    PlatformMiniGameGrid grid,
    MazeData maze)
    {
        HashSet<Vector2Int> solution = new HashSet<Vector2Int>(maze.solutionPath);

        foreach (Vector2Int blocker in maze.finishBlockers)
        {
            foreach (Vector2Int neighbour in GetNeighbours(blocker))
            {
                if (!maze.passable.Contains(neighbour))
                    continue;

                // zostawiamy tylko s¹siadów nale¿¹cych do g³ównej œcie¿ki,
                // ¿eby blocker by³ czêœci¹ jedynego korytarza
                if (solution.Contains(neighbour))
                    continue;

                if (maze.startZone.Contains(neighbour))
                    continue;

                if (neighbour == maze.finish)
                    continue;

                maze.passable.Remove(neighbour);

                PlatformTile tile = grid.GetTile(neighbour.x, neighbour.y);
                if (tile)
                    tile.SetDanger();
            }
        }
    }
    private int GetPassableNeighbourCount(
    Vector2Int pos,
    HashSet<Vector2Int> passable)
    {
        int count = 0;

        foreach (Vector2Int next in GetNeighbours(pos))
        {
            if (passable.Contains(next))
                count++;
        }

        return count;
    }

    private void HandleStage2DecorActivation(
    PlatformMiniGameGrid grid,
    MazeData maze,
    PlatformTile currentPlayerTile)
    {
        Vector2Int pos = new Vector2Int(currentPlayerTile.GridX, currentPlayerTile.GridY);

        if (!maze.decorTargets.Contains(pos))
            return;

        if (maze.activatedDecorTargets.Contains(pos))
            return;

        maze.activatedDecorTargets.Add(pos);

        currentPlayerTile.SetSafe();

        // zdejmij 1 czarny blocker
        if (maze.finishBlockers.Count > 0)
        {
            Vector2Int blockerPos = maze.finishBlockers[0];
            maze.finishBlockers.RemoveAt(0);

            PlatformTile blockerTile = grid.GetTile(blockerPos.x, blockerPos.y);
            if (blockerTile)
                blockerTile.SetSafe();
        }

        RefreshStage2MazeAfterDecor(grid, maze);
    }

    private void UpdateStage3Pendulum(
    PlatformMiniGameGrid grid,
    MazeData maze,
    float angleDeg)
    {
        RestoreDynamicPendulumTiles(grid, maze);

        maze.activePendulumTiles.Clear();

        // œrodek 2x2
        int centerX1 = (grid.MinX + grid.MaxX) / 2;
        int centerY1 = (grid.MinY + grid.MaxY) / 2;
        int centerX2 = centerX1 + 1;
        int centerY2 = centerY1 + 1;

        Vector2 pivot = new Vector2(centerX1 + 0.5f, centerY1 + 0.5f);

        MarkPendulumTile(grid, maze, new Vector2Int(centerX1, centerY1));
        MarkPendulumTile(grid, maze, new Vector2Int(centerX2, centerY1));
        MarkPendulumTile(grid, maze, new Vector2Int(centerX1, centerY2));
        MarkPendulumTile(grid, maze, new Vector2Int(centerX2, centerY2));

        float radians = angleDeg * Mathf.Deg2Rad;

        Vector2 direction = new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians)
        );

        Vector2 endA = pivot + direction * pendulumArmLength;
        Vector2 endB = pivot - direction * pendulumArmLength;

        foreach (Vector2Int pos in RasterizeLine(pivot, endA))
        {
            MarkPendulumTile(grid, maze, pos);
        }

        foreach (Vector2Int pos in RasterizeLine(pivot, endB))
        {
            MarkPendulumTile(grid, maze, pos);
        }
    }

    private void MarkPendulumTile(
        PlatformMiniGameGrid grid,
        MazeData maze,
        Vector2Int pos)
    {
        PlatformTile tile = grid.GetTile(pos.x, pos.y);
        if (!tile)
            return;

        if (pos == maze.finish)
            return;

        tile.SetPendulum();

        if (!maze.activePendulumTiles.Contains(pos))
            maze.activePendulumTiles.Add(pos);
    }

    private void RestoreDynamicPendulumTiles(
        PlatformMiniGameGrid grid,
        MazeData maze)
    {
        foreach (Vector2Int pos in maze.activePendulumTiles)
        {
            if (!maze.baseStates.TryGetValue(pos, out PlatformTileState state))
                continue;

            PlatformTile tile = grid.GetTile(pos.x, pos.y);
            if (!tile)
                continue;

            RestoreTileState(tile, state);
        }
    }

    private void SaveBaseStates(
    PlatformMiniGameGrid grid,
    MazeData maze)
    {
        maze.baseStates.Clear();

        foreach (PlatformTile tile in PlatformMiniGameGrid.Tiles)
        {
            if (tile == null)
                continue;

            Vector2Int pos = new Vector2Int(tile.GridX, tile.GridY);
            maze.baseStates[pos] = tile.CurrentState;
        }
    }

    private void RestoreTileState(
        PlatformTile tile,
        PlatformTileState state)
    {
        switch (state)
        {
            case PlatformTileState.Start: tile.SetStart(); break;
            case PlatformTileState.Danger: tile.SetDanger(); break;
            case PlatformTileState.Path: tile.SetPath(); break;
            case PlatformTileState.Finish: tile.SetFinish(); break;
            case PlatformTileState.Warning: tile.SetWarning(); break;
            case PlatformTileState.Decor: tile.SetDecor(); break;
            case PlatformTileState.Blocked: tile.SetBlocked(); break;
            case PlatformTileState.Pendulum: tile.SetPendulum(); break;
            case PlatformTileState.PlayerFail: tile.SetPlayerFail(); break;
            case PlatformTileState.Safe:
                if (tile.CurrentState != PlatformTileState.Warning &&
                    tile.CurrentState != PlatformTileState.Danger)
                {
                    tile.SetSafe();
                }
                break;
            default: tile.SetNormal(); break;
        }
    }

    private List<Vector2Int> RasterizeLine(
    Vector2 start,
    Vector2 end)
    {
        List<Vector2Int> result = new();

        int x0 = Mathf.RoundToInt(start.x);
        int y0 = Mathf.RoundToInt(start.y);
        int x1 = Mathf.RoundToInt(end.x);
        int y1 = Mathf.RoundToInt(end.y);

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            result.Add(new Vector2Int(x0, y0));

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return result;
    }

    private void ForceSingleOuterWall(
       PlatformMiniGameGrid grid,
       MazeData maze)
    {
        foreach (PlatformTile tile in PlatformMiniGameGrid.Tiles)
        {
            if (tile == null)
                continue;

            bool outerWall =
                tile.GridX == grid.MinX ||
                tile.GridX == grid.MaxX ||
                tile.GridY == grid.MinY ||
                tile.GridY == grid.MaxY;

            if (!outerWall)
                continue;

            Vector2Int pos = new Vector2Int(tile.GridX, tile.GridY);

            tile.SetDanger();
            maze.passable.Remove(pos);
            maze.startZone.Remove(pos);
        }
    }

    private void RefreshStage2MazeAfterDecor(
       PlatformMiniGameGrid grid,
       MazeData maze)
    {
        foreach (Vector2Int pos in maze.passable)
        {
            PlatformTile tile = grid.GetTile(pos.x, pos.y);
            if (!tile)
                continue;

            if (maze.startZone.Contains(pos))
            {
                tile.SetPath();
                continue;
            }

            if (pos == maze.finish)
            {
                tile.SetFinish();
                continue;
            }

            if (maze.finishBlockers.Contains(pos))
            {
                tile.SetBlocked();
                continue;
            }

            if (maze.decorTargets.Contains(pos) &&
                !maze.activatedDecorTargets.Contains(pos))
            {
                tile.SetDecor();
                continue;
            }

            tile.SetSafe();
        }

        foreach (Vector2Int pos in maze.activatedDecorTargets)
        {
            PlatformTile tile = grid.GetTile(pos.x, pos.y);
            if (tile)
                tile.SetSafe();
        }

        SaveBaseStates(grid, maze);
    }

    private HashSet<Vector2Int> GetReachableBeforeBlockers(
    MazeData maze)
    {
        HashSet<Vector2Int> reachable = new();
        Queue<Vector2Int> queue = new();

        queue.Enqueue(maze.entryCell);
        reachable.Add(maze.entryCell);

        HashSet<Vector2Int> blockers = new HashSet<Vector2Int>(maze.finishBlockers);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int next in GetNeighbours(current))
            {
                if (!maze.passable.Contains(next))
                    continue;

                if (blockers.Contains(next))
                    continue;

                if (reachable.Contains(next))
                    continue;

                reachable.Add(next);
                queue.Enqueue(next);
            }
        }

        return reachable;
    }

    private Vector2Int GetRandomFarFinishPosition(
    PlatformMiniGameGrid grid,
    MazeData maze,
    int stage)
    {
        List<Vector2Int> candidates = new();

        int centerX = (grid.MinX + grid.MaxX) / 2;
        int centerY = (grid.MinY + grid.MaxY) / 2;

        HashSet<Vector2Int> forbidden = new();

        if (stage == 2)
        {
            forbidden.Add(new Vector2Int(centerX, centerY));
            forbidden.Add(new Vector2Int(centerX + 1, centerY));
            forbidden.Add(new Vector2Int(centerX, centerY + 1));
            forbidden.Add(new Vector2Int(centerX + 1, centerY + 1));
        }

        foreach (Vector2Int pos in maze.passable)
        {
            if (maze.startZone.Contains(pos))
                continue;

            if (forbidden.Contains(pos))
                continue;

            float distanceFromStart = Vector2Int.Distance(pos, maze.start);

            if (distanceFromStart < 8f)
                continue;

            candidates.Add(pos);
        }

        if (candidates.Count == 0)
            return GetNearestPassableToTarget(
                new Vector2Int(centerX, centerY),
                maze.passable
            );

        Shuffle(candidates);

        return candidates[0];
    }
}