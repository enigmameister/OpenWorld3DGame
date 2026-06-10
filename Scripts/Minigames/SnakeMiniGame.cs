using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnakeMiniGame : MonoBehaviour
{
    [Header("Stages")]
    [SerializeField] private int snakeStages = 3;
    [SerializeField] private float stageStartPause = 0.25f;

    [Header("Stage 1")]
    [SerializeField] private int[] stage1Widths = { 1, 2, 3 };

    [Header("Stage 2")]
    [SerializeField] private int[] stage2Widths = { 5, 7, 9 };

    [Header("Stage 3 Perimeter")]
    [SerializeField] private int stage3Attacks = 3;
    [SerializeField] private int minPerimeterStepsBeforeAttack = 24;
    [SerializeField] private int maxPerimeterStepsBeforeAttack = 48;
    [SerializeField] private float perimeterStepDelayMultiplier = 1.15f;

    private int currentSnakeStage;
    private int lastPlayerY;
    private bool hasLastPlayerY;

    public IEnumerator Run(
        PlatformMiniGameGrid grid,
        int snakeWaves,
        float snakeStepDelay,
        int snakeLineWidth,
        float snakeWavePause,
        float snakeSpeedMultiplierPerWave,
        int snakeLength,
        Func<bool> isFinished,
        Action resetFinish,
        Func<bool> isGameRunning,
        Func<PlatformTile> getCurrentPlayerTile,
        Func<int> getLivesLeft,
        Action checkPlayerDanger,
        Action<string, int, int, int> showGameScreen,
        Action<float> updateGameTimes,
        Func<string, int, int, float, string, IEnumerator> showStageWinScreen,
        Action hideWindow,
        bool skipFirstStageStart,
        Action<bool> setSequenceActive)
    {
        bool skipStartOnlyOnce = skipFirstStageStart;

        for (int stage = currentSnakeStage; stage < snakeStages; stage++)
        {
            currentSnakeStage = stage;

            if (!isGameRunning())
                yield break;

            resetFinish?.Invoke();

            if (!skipStartOnlyOnce)
            {
                yield return WaitForStageStart(
                    grid,
                    getCurrentPlayerTile,
                    isGameRunning,
                    setSequenceActive
                );
            }

            skipStartOnlyOnce = false;

            showGameScreen?.Invoke(
                "Snake",
                stage + 1,
                snakeStages,
                getLivesLeft != null ? getLivesLeft() : 0
            );

            if (!isGameRunning())
                yield break;

            float stageStartTime = Time.time;

            setSequenceActive?.Invoke(true);

            hasLastPlayerY = false;

            if (stage == 0)
            {
                yield return RunStraightAttackStage(
                    grid,
                    stage1Widths,
                    useSafeCore: false,
                    snakeStepDelay,
                    snakeWavePause,
                    snakeSpeedMultiplierPerWave,
                    snakeLength,
                    isGameRunning,
                    getCurrentPlayerTile,
                    checkPlayerDanger,
                    stageStartTime,
                    updateGameTimes
                );
            }
            else if (stage == 1)
            {
                yield return RunStraightAttackStage(
                    grid,
                    stage2Widths,
                    useSafeCore: true,
                    snakeStepDelay,
                    snakeWavePause,
                    snakeSpeedMultiplierPerWave,
                    snakeLength,
                    isGameRunning,
                    getCurrentPlayerTile,
                    checkPlayerDanger,
                    stageStartTime,
                    updateGameTimes
                );
            }
            else
            {
                yield return RunPerimeterStage(
                    grid,
                    stage3Attacks,
                    snakeStepDelay,
                    snakeWavePause,
                    isGameRunning,
                    getCurrentPlayerTile,
                    checkPlayerDanger,
                    stageStartTime,
                    updateGameTimes
                );
            }

            if (!isGameRunning())
                yield break;

            PlatformTile finishTile = SetFinishNearPlayer(
                grid,
                getCurrentPlayerTile
            );

            while (!isFinished() && isGameRunning())
            {
                if (stage != 2)
                    PaintSnakeArena(grid);

                if (finishTile)
                    finishTile.SetFinish();

                float stageElapsed = Time.time - stageStartTime;
                updateGameTimes?.Invoke(stageElapsed);

                checkPlayerDanger?.Invoke();

                yield return null;
            }

            setSequenceActive?.Invoke(false);

            if (!isGameRunning())
                yield break;

            grid.SetAllSafe();

            float clearTime = Time.time - stageStartTime;

            string nextName = stage + 1 < snakeStages
                ? "SNAKE"
                : "NEXT MINIGAME";

            if (showStageWinScreen != null)
            {
                yield return showStageWinScreen(
                    "Snake",
                    stage + 1,
                    snakeStages,
                    clearTime,
                    nextName
                );
            }

            currentSnakeStage = stage + 1;

            hideWindow?.Invoke();

            yield return new WaitForSeconds(stageStartPause);
        }

        currentSnakeStage = 0;
        grid.SetAllSafe();
    }

    private IEnumerator WaitForStageStart(
        PlatformMiniGameGrid grid,
        Func<PlatformTile> getCurrentPlayerTile,
        Func<bool> isGameRunning,
        Action<bool> setSequenceActive)
    {
        setSequenceActive?.Invoke(false);

        PaintSnakeArena(grid);

        int startX = UnityEngine.Random.Range(grid.MinX + 2, grid.MaxX - 1);
        int startY = UnityEngine.Random.Range(grid.MinY + 2, grid.MaxY - 1);

        PlatformTile startTile = grid.GetTile(startX, startY);

        if (startTile)
            startTile.SetStart();

        while (isGameRunning())
        {
            PlatformTile current = getCurrentPlayerTile();

            if (current != null && current.IsStart)
                break;

            yield return null;
        }

        setSequenceActive?.Invoke(true);
    }

    private IEnumerator RunStraightAttackStage(
        PlatformMiniGameGrid grid,
        int[] widths,
        bool useSafeCore,
        float snakeStepDelay,
        float snakeWavePause,
        float snakeSpeedMultiplierPerWave,
        int snakeLength,
        Func<bool> isGameRunning,
        Func<PlatformTile> getCurrentPlayerTile,
        Action checkPlayerDanger,
        float stageStartTime,
        Action<float> updateGameTimes)
    {
        for (int attack = 0; attack < widths.Length; attack++)
        {
            if (!isGameRunning())
                yield break;

            int currentWidth = Mathf.Max(1, widths[attack]);
            int currentLength = snakeLength + attack * 2;

            float attackDelay = snakeStepDelay *
                                Mathf.Pow(snakeSpeedMultiplierPerWave, attack);

            PlatformTile playerTile = getCurrentPlayerTile();

            int targetCenterY = GetSmartTargetY(
                grid,
                playerTile,
                currentWidth,
                predictionRows: attack + 1
            );

            bool attackFromLeft = ShouldAttackFromLeft(grid, playerTile);

            int startX = attackFromLeft
                ? grid.MinX - currentLength
                : grid.MaxX + currentLength;

            int endX = attackFromLeft
                ? grid.MaxX + currentLength
                : grid.MinX - currentLength;

            int step = attackFromLeft ? 1 : -1;

            for (int x = startX; attackFromLeft ? x <= endX : x >= endX; x += step)
            {
                if (!isGameRunning())
                    yield break;

                PaintSnakeArena(grid);

                DrawSnakeBody(
                    grid,
                    x,
                    step,
                    currentLength,
                    currentWidth,
                    targetCenterY,
                    useSafeCore
                );

                float stageElapsed = Time.time - stageStartTime;
                updateGameTimes?.Invoke(stageElapsed);

                checkPlayerDanger?.Invoke();

                yield return new WaitForSeconds(attackDelay);
            }

            PaintSnakeArena(grid);

            yield return new WaitForSeconds(snakeWavePause);
        }
    }

    private IEnumerator RunPerimeterStage(
        PlatformMiniGameGrid grid,
        int attacks,
        float snakeStepDelay,
        float snakeWavePause,
        Func<bool> isGameRunning,
        Func<PlatformTile> getCurrentPlayerTile,
        Action checkPlayerDanger,
        float stageStartTime,
        Action<float> updateGameTimes)
    {
        List<Vector2Int> perimeter = BuildInnerPerimeterPath(grid);

        if (perimeter.Count == 0)
            yield break;

        PaintSnakeArena(grid);

        int perimeterIndex = UnityEngine.Random.Range(0, perimeter.Count);

        for (int attack = 0; attack < attacks; attack++)
        {
            int stalkSteps = UnityEngine.Random.Range(
                minPerimeterStepsBeforeAttack,
                maxPerimeterStepsBeforeAttack + 1
            );

            for (int i = 0; i < stalkSteps; i++)
            {
                if (!isGameRunning())
                    yield break;

                Vector2Int snakePos = perimeter[perimeterIndex];
                SetDangerTile(grid, snakePos);

                perimeterIndex = (perimeterIndex + 1) % perimeter.Count;

                float stageElapsed = Time.time - stageStartTime;
                updateGameTimes?.Invoke(stageElapsed);

                checkPlayerDanger?.Invoke();

                yield return new WaitForSeconds(
                    snakeStepDelay * perimeterStepDelayMultiplier
                );
            }

            if (!isGameRunning())
                yield break;

            Vector2Int attackStart = perimeter[perimeterIndex];
            PlatformTile playerTile = getCurrentPlayerTile();

            Vector2Int attackEnd = GetPerimeterAttackEnd(
                grid,
                attackStart,
                playerTile
            );

            List<Vector2Int> attackPath = BuildStraightAttackPath(
                attackStart,
                attackEnd
            );

            foreach (Vector2Int pos in attackPath)
            {
                if (!isGameRunning())
                    yield break;

                SetDangerTile(grid, pos);

                float stageElapsed = Time.time - stageStartTime;
                updateGameTimes?.Invoke(stageElapsed);

                checkPlayerDanger?.Invoke();

                yield return new WaitForSeconds(snakeStepDelay);
            }

            perimeterIndex = GetClosestPerimeterIndex(perimeter, attackEnd);

            yield return new WaitForSeconds(snakeWavePause);
        }
    }

    private void DrawSnakeBody(
        PlatformMiniGameGrid grid,
        int headX,
        int step,
        int length,
        int width,
        int centerY,
        bool useSafeCore)
    {
        int halfDown = width / 2;
        int halfUp = width - halfDown - 1;

        for (int i = 0; i < length; i++)
        {
            int bodyX = headX - i * step;

            for (int offsetY = -halfDown; offsetY <= halfUp; offsetY++)
            {
                int y = centerY + offsetY;

                PlatformTile bodyTile = grid.GetTile(bodyX, y);

                if (!bodyTile)
                    continue;

                if (IsEdgeTile(grid, bodyTile))
                {
                    bodyTile.SetDanger();
                    continue;
                }

                if (useSafeCore && offsetY == 0)
                    bodyTile.SetSafe();
                else
                    bodyTile.SetDanger();
            }
        }
    }

    private int GetSmartTargetY(
        PlatformMiniGameGrid grid,
        PlatformTile playerTile,
        int attackWidth,
        int predictionRows)
    {
        int halfDown = attackWidth / 2;
        int halfUp = attackWidth - halfDown - 1;

        int minCenterY = grid.MinY + 1 + halfDown;
        int maxCenterY = grid.MaxY - 1 - halfUp;

        if (playerTile == null)
            return Mathf.Clamp((grid.MinY + grid.MaxY) / 2, minCenterY, maxCenterY);

        int currentY = playerTile.GridY;
        int predictedY = currentY;

        if (hasLastPlayerY)
        {
            int deltaY = currentY - lastPlayerY;
            predictedY = currentY + deltaY * predictionRows;
        }

        lastPlayerY = currentY;
        hasLastPlayerY = true;

        return Mathf.Clamp(predictedY, minCenterY, maxCenterY);
    }

    private bool ShouldAttackFromLeft(
        PlatformMiniGameGrid grid,
        PlatformTile playerTile)
    {
        if (playerTile == null)
            return UnityEngine.Random.value > 0.5f;

        int centerX = (grid.MinX + grid.MaxX) / 2;

        return playerTile.GridX <= centerX;
    }

    private PlatformTile SetFinishNearPlayer(
        PlatformMiniGameGrid grid,
        Func<PlatformTile> getCurrentPlayerTile)
    {
        PlatformTile playerTile = getCurrentPlayerTile != null
            ? getCurrentPlayerTile()
            : null;

        int finishY = playerTile != null
            ? playerTile.GridY
            : (grid.MinY + grid.MaxY) / 2;

        finishY = Mathf.Clamp(finishY, grid.MinY + 1, grid.MaxY - 1);

        int finishX = grid.MaxX - 1;

        PlatformTile finishTile = grid.GetTile(finishX, finishY);

        if (finishTile)
            finishTile.SetFinish();

        return finishTile;
    }

    private List<Vector2Int> BuildInnerPerimeterPath(
        PlatformMiniGameGrid grid)
    {
        List<Vector2Int> path = new();

        int minX = grid.MinX + 1;
        int maxX = grid.MaxX - 1;
        int minY = grid.MinY + 1;
        int maxY = grid.MaxY - 1;

        for (int x = minX; x <= maxX; x++)
            path.Add(new Vector2Int(x, minY));

        for (int y = minY + 1; y <= maxY; y++)
            path.Add(new Vector2Int(maxX, y));

        for (int x = maxX - 1; x >= minX; x--)
            path.Add(new Vector2Int(x, maxY));

        for (int y = maxY - 1; y > minY; y--)
            path.Add(new Vector2Int(minX, y));

        return path;
    }

    private Vector2Int GetPerimeterAttackEnd(
        PlatformMiniGameGrid grid,
        Vector2Int start,
        PlatformTile playerTile)
    {
        int minX = grid.MinX + 1;
        int maxX = grid.MaxX - 1;
        int minY = grid.MinY + 1;
        int maxY = grid.MaxY - 1;

        if (start.x == minX)
        {
            int y = playerTile != null ? playerTile.GridY : start.y;
            return new Vector2Int(maxX, Mathf.Clamp(y, minY, maxY));
        }

        if (start.x == maxX)
        {
            int y = playerTile != null ? playerTile.GridY : start.y;
            return new Vector2Int(minX, Mathf.Clamp(y, minY, maxY));
        }

        if (start.y == minY)
        {
            int x = playerTile != null ? playerTile.GridX : start.x;
            return new Vector2Int(Mathf.Clamp(x, minX, maxX), maxY);
        }

        int fallbackX = playerTile != null ? playerTile.GridX : start.x;
        return new Vector2Int(Mathf.Clamp(fallbackX, minX, maxX), minY);
    }

    private List<Vector2Int> BuildStraightAttackPath(
        Vector2Int start,
        Vector2Int end)
    {
        List<Vector2Int> path = new();

        Vector2Int current = start;
        path.Add(current);

        int guard = 0;

        while (current != end && guard < 200)
        {
            guard++;

            if (current.x < end.x)
                current.x++;
            else if (current.x > end.x)
                current.x--;
            else if (current.y < end.y)
                current.y++;
            else if (current.y > end.y)
                current.y--;

            path.Add(current);
        }

        return path;
    }

    private int GetClosestPerimeterIndex(
        List<Vector2Int> perimeter,
        Vector2Int pos)
    {
        int bestIndex = 0;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < perimeter.Count; i++)
        {
            float distance = Vector2Int.Distance(perimeter[i], pos);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void SetDangerTile(
        PlatformMiniGameGrid grid,
        Vector2Int pos)
    {
        PlatformTile tile = grid.GetTile(pos.x, pos.y);

        if (tile)
            tile.SetDanger();
    }

    private void PaintSnakeArena(PlatformMiniGameGrid grid)
    {
        grid.ResetTiles();

        foreach (var tile in PlatformMiniGameGrid.Tiles)
        {
            if (!tile) continue;

            if (IsEdgeTile(grid, tile))
                tile.SetDanger();
            else
                tile.SetSafe();
        }
    }

    private bool IsEdgeTile(
        PlatformMiniGameGrid grid,
        PlatformTile tile)
    {
        return tile.GridX == grid.MinX ||
               tile.GridX == grid.MaxX ||
               tile.GridY == grid.MinY ||
               tile.GridY == grid.MaxY;
    }

    public void ResetProgress()
    {
        currentSnakeStage = 0;
        hasLastPlayerY = false;
    }
}