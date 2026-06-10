using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BridgeMiniGame : MonoBehaviour
{
    [Header("Stages")]
    [SerializeField] private int bridgeStages = 3;
    [SerializeField] private float stageStartPause = 0.25f;

    [Header("Moving Platforms")]
    [SerializeField] private float movingPlatformTick = 0.45f;
    [SerializeField] private int movingPlatformStep = 1;

    [Header("Bridge Safety")]
    [SerializeField] private int maxHorizontalJumpTiles = 1;
    [SerializeField] private int stage2ExtraOptions = 2;

    private int currentBridgeStage;
    private struct MovingPlatform
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public int direction;
        public float tick;
        public float timer;
    }

    public IEnumerator Run(
        PlatformMiniGameGrid grid,
        int edgeDepth,
        int bridgeOptionsPerRow,
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

        for (int stage = currentBridgeStage; stage < bridgeStages; stage++)
        {
            currentBridgeStage = stage;

            if (!isGameRunning())
                yield break;

            resetFinish?.Invoke();

            if (!skipStartOnlyOnce)
            {
                yield return WaitForStageStart(
                    grid,
                    edgeDepth,
                    getCurrentPlayerTile,
                    isGameRunning,
                    setSequenceActive
                );
            }

            skipStartOnlyOnce = false;

            showGameScreen?.Invoke(
                "Bridge",
                stage + 1,
                bridgeStages,
                getLivesLeft != null ? getLivesLeft() : 0
            );

            if (!isGameRunning())
                yield break;

            float stageStartTime = Time.time;

            setSequenceActive?.Invoke(true);

            if (stage == 0)
            {
                // Stage 1: statyczne platformy 2x2
                GenerateStaticBridge(
                    grid,
                    edgeDepth,
                    bridgeOptionsPerRow,
                    blockWidth: 2,
                    blockHeight: 2
                );

                yield return WaitForFinish(
                    isFinished,
                    isGameRunning,
                    checkPlayerDanger,
                    stageStartTime,
                    updateGameTimes
                );
            }
            else if (stage == 1)
            {
                // Stage 2: mniejsze platformy 1x2 albo 2x1
                GenerateStage2MixedBridge(
                    grid,
                    edgeDepth,
                    bridgeOptionsPerRow + stage2ExtraOptions
                );

                yield return WaitForFinish(
                    isFinished,
                    isGameRunning,
                    checkPlayerDanger,
                    stageStartTime,
                    updateGameTimes
                );
            }
            else
            {
                // Stage 3: ruchome platformy 2x2
                yield return RunMovingBridgeStage(
                    grid,
                    edgeDepth,
                    bridgeOptionsPerRow,
                    isFinished,
                    isGameRunning,
                    checkPlayerDanger,
                    stageStartTime,
                    updateGameTimes
                );
            }

            setSequenceActive?.Invoke(false);

            if (!isGameRunning())
                yield break;

            grid.SetAllSafe();

            float clearTime = Time.time - stageStartTime;

            string nextName = stage + 1 < bridgeStages
                ? "BRIDGE"
                : "NEXT MINIGAME";

            if (showStageWinScreen != null)
            {
                yield return showStageWinScreen(
                    "Bridge",
                    stage + 1,
                    bridgeStages,
                    clearTime,
                    nextName
                );
            }
            currentBridgeStage = stage + 1;

            hideWindow?.Invoke();

            yield return new WaitForSeconds(stageStartPause);
        }

        currentBridgeStage = 0;
        grid.SetAllSafe();
    }

    private void GenerateStage2MixedBridge(
    PlatformMiniGameGrid grid,
    int edgeDepth,
    int bridgeOptionsPerRow)
    {
        grid.ResetTiles();

        PaintBridgeBase(grid, edgeDepth);

        int gapRows = 1;
        int maxBlockHeight = 2;
        int stepY = maxBlockHeight + gapRows;

        int anchorX = UnityEngine.Random.Range(grid.MinX + 1, grid.MaxX - 1);

        for (int y = grid.MinY + edgeDepth + 1;
             y <= grid.MaxY - edgeDepth - maxBlockHeight;
             y += stepY)
        {
            List<int> usedX = new();

            // Gwarantowana platforma g³ównej trasy.
            bool anchorHorizontal = UnityEngine.Random.value > 0.5f;

            int anchorWidth = anchorHorizontal ? 2 : 1;
            int anchorHeight = anchorHorizontal ? 1 : 2;

            int minX = grid.MinX + 1;
            int maxX = grid.MaxX - anchorWidth;

            anchorX = GetNextBridgeAnchorX(
                anchorX,
                minX,
                maxX,
                anchorWidth
            );

            usedX.Add(anchorX);

            SetTileBlock(
                grid,
                anchorX,
                y,
                anchorWidth,
                anchorHeight,
                PlatformTileState.Path
            );

            // Dodatkowe losowe platformy 1x2 albo 2x1.
            for (int i = 1; i < bridgeOptionsPerRow; i++)
            {
                bool horizontal = UnityEngine.Random.value > 0.5f;

                int blockWidth = horizontal ? 2 : 1;
                int blockHeight = horizontal ? 1 : 2;

                int x = GetRandomBlockX(grid, blockWidth);

                int guard = 0;
                while (IsTooCloseToUsedBlocks(x, usedX, blockWidth) && guard < 40)
                {
                    x = GetRandomBlockX(grid, blockWidth);
                    guard++;
                }

                usedX.Add(x);

                SetTileBlock(
                    grid,
                    x,
                    y,
                    blockWidth,
                    blockHeight,
                    PlatformTileState.Path
                );
            }
        }

        SetRandomFinishNearX(grid, anchorX);
    }

    private IEnumerator WaitForStageStart(
        PlatformMiniGameGrid grid,
        int edgeDepth,
        Func<PlatformTile> getCurrentPlayerTile,
        Func<bool> isGameRunning,
        Action<bool> setSequenceActive)
    {
        setSequenceActive?.Invoke(false);

        PaintBridgeWaitingArea(grid, edgeDepth);

        int startY = grid.MinY + UnityEngine.Random.Range(0, edgeDepth);
        int startX = UnityEngine.Random.Range(grid.MinX + 1, grid.MaxX);

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
    }

    private IEnumerator WaitForFinish(
        Func<bool> isFinished,
        Func<bool> isGameRunning,
        Action checkPlayerDanger,
        float stageStartTime,
        Action<float> updateGameTimes)
    {
        while (!isFinished() && isGameRunning())
        {
            float stageElapsed = Time.time - stageStartTime;

            updateGameTimes?.Invoke(stageElapsed);
            checkPlayerDanger?.Invoke();

            yield return null;
        }
    }

    private void PaintBridgeWaitingArea(
        PlatformMiniGameGrid grid,
        int edgeDepth)
    {
        grid.ResetTiles();

        for (int y = grid.MinY; y <= grid.MaxY; y++)
        {
            bool startEdge = y < grid.MinY + edgeDepth;
            bool finishEdge = y > grid.MaxY - edgeDepth;

            for (int x = grid.MinX; x <= grid.MaxX; x++)
            {
                PlatformTile tile = grid.GetTile(x, y);
                if (!tile) continue;

                if (startEdge || finishEdge)
                    tile.SetSafe();
                else
                    tile.SetNormal();
            }
        }
    }

    private void PaintBridgeBase(
        PlatformMiniGameGrid grid,
        int edgeDepth)
    {
        for (int y = grid.MinY; y <= grid.MaxY; y++)
        {
            bool startEdge = y < grid.MinY + edgeDepth;
            bool finishEdge = y > grid.MaxY - edgeDepth;

            for (int x = grid.MinX; x <= grid.MaxX; x++)
            {
                PlatformTile tile = grid.GetTile(x, y);
                if (!tile) continue;

                if (startEdge || finishEdge)
                    tile.SetSafe();
                else
                    tile.SetDanger();
            }
        }
    }

    private void GenerateStaticBridge(
       PlatformMiniGameGrid grid,
       int edgeDepth,
       int bridgeOptionsPerRow,
       int blockWidth,
       int blockHeight)
    {
        grid.ResetTiles();

        PaintBridgeBase(grid, edgeDepth);

        int gapRows = 1;
        int stepY = blockHeight + gapRows;

        int minX = grid.MinX + 1;
        int maxX = grid.MaxX - blockWidth;

        int anchorX = UnityEngine.Random.Range(minX, maxX + 1);

        for (int y = grid.MinY + edgeDepth + 1;
             y <= grid.MaxY - edgeDepth - blockHeight;
             y += stepY)
        {
            List<int> usedX = new();

            // 1) Gwarantowana platforma na trasie g³ównej.
            anchorX = GetNextBridgeAnchorX(
                anchorX,
                minX,
                maxX,
                blockWidth
            );

            usedX.Add(anchorX);
            SetTileBlock(
                grid,
                anchorX,
                y,
                blockWidth,
                blockHeight,
                PlatformTileState.Path
            );

            // 2) Dodatkowe losowe platformy.
            for (int i = 1; i < bridgeOptionsPerRow; i++)
            {
                int x = GetRandomBlockX(grid, blockWidth);

                int guard = 0;
                while (IsTooCloseToUsedBlocks(x, usedX, blockWidth) && guard < 40)
                {
                    x = GetRandomBlockX(grid, blockWidth);
                    guard++;
                }

                usedX.Add(x);

                SetTileBlock(
                    grid,
                    x,
                    y,
                    blockWidth,
                    blockHeight,
                    PlatformTileState.Path
                );
            }
        }

        SetRandomFinishNearX(grid, anchorX);
    }

    private IEnumerator RunMovingBridgeStage(
        PlatformMiniGameGrid grid,
        int edgeDepth,
        int bridgeOptionsPerRow,
        Func<bool> isFinished,
        Func<bool> isGameRunning,
        Action checkPlayerDanger,
        float stageStartTime,
        Action<float> updateGameTimes)
    {
        List<MovingPlatform> platforms = CreateMovingPlatforms(
            grid,
            edgeDepth,
            bridgeOptionsPerRow
        );

        PlatformTile finishTile = SetRandomFinish(grid);

        while (!isFinished() && isGameRunning())
        {
            MovePlatforms(grid, platforms);

            PaintBridgeBase(grid, edgeDepth);
            DrawMovingPlatforms(grid, platforms);

            if (finishTile)
                finishTile.SetFinish();

            float stageElapsed = Time.time - stageStartTime;
            updateGameTimes?.Invoke(stageElapsed);

            checkPlayerDanger?.Invoke();

            yield return null;
        }
    }

    private List<MovingPlatform> CreateMovingPlatforms(
        PlatformMiniGameGrid grid,
        int edgeDepth,
        int bridgeOptionsPerRow)
    {
        List<MovingPlatform> platforms = new();

        int blockWidth = 2;
        int blockHeight = 2;

        int gapRows = 1;
        int stepY = blockHeight + gapRows;

        for (int y = grid.MinY + edgeDepth + 1;
             y <= grid.MaxY - edgeDepth - blockHeight;
             y += stepY)
        {
            List<int> usedX = new();

            for (int i = 0; i < bridgeOptionsPerRow; i++)
            {
                int x = GetRandomBlockX(grid, blockWidth);

                int guard = 0;
                while (usedX.Contains(x) && guard < 30)
                {
                    x = GetRandomBlockX(grid, blockWidth);
                    guard++;
                }

                usedX.Add(x);

                MovingPlatform platform = new MovingPlatform
                {
                    x = x,
                    y = y,
                    width = blockWidth,
                    height = blockHeight,
                    direction = UnityEngine.Random.value > 0.5f ? 1 : -1,
                    tick = movingPlatformTick * UnityEngine.Random.Range(1.0f, 1.8f),
                    timer = UnityEngine.Random.Range(0f, movingPlatformTick)
                };

                platforms.Add(platform);
            }
        }

        return platforms;
    }

    private void MovePlatforms(
       PlatformMiniGameGrid grid,
       List<MovingPlatform> platforms)
    {
        for (int i = 0; i < platforms.Count; i++)
        {
            MovingPlatform platform = platforms[i];

            platform.timer += Time.deltaTime;

            if (platform.timer < platform.tick)
            {
                platforms[i] = platform;
                continue;
            }

            platform.timer = 0f;

            platform.x += platform.direction * movingPlatformStep;

            int minMoveX = grid.MinX + 1;
            int maxMoveX = grid.MaxX - platform.width;

            if (platform.x <= minMoveX)
            {
                platform.x = minMoveX;
                platform.direction = 1;
            }
            else if (platform.x >= maxMoveX)
            {
                platform.x = maxMoveX;
                platform.direction = -1;
            }

            platforms[i] = platform;
        }
    }

    private void DrawMovingPlatforms(
        PlatformMiniGameGrid grid,
        List<MovingPlatform> platforms)
    {
        for (int i = 0; i < platforms.Count; i++)
        {
            MovingPlatform platform = platforms[i];

            SetTileBlock(
                grid,
                platform.x,
                platform.y,
                platform.width,
                platform.height,
                PlatformTileState.Path
            );
        }
    }

    private PlatformTile SetRandomFinish(PlatformMiniGameGrid grid)
    {
        int finishX = GetRandomBlockX(grid, 1);
        int finishY = grid.MaxY;

        PlatformTile finish = grid.GetTile(finishX, finishY);

        if (finish)
            finish.SetFinish();

        return finish;
    }

    private void SetTileBlock(
        PlatformMiniGameGrid grid,
        int startX,
        int startY,
        int width,
        int height,
        PlatformTileState state)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                PlatformTile tile = grid.GetTile(startX + x, startY + y);
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

    private int GetRandomBlockX(PlatformMiniGameGrid grid, int blockWidth)
    {
        return UnityEngine.Random.Range(
            grid.MinX + 1,
            grid.MaxX - blockWidth + 1
        );
    }

    public void ResetProgress()
    {
        currentBridgeStage = 0;
    }

    private int GetNextBridgeAnchorX(
    int previousX,
    int minX,
    int maxX,
    int blockWidth)
    {
        int maxStep = Mathf.Max(1, maxHorizontalJumpTiles + blockWidth);

        int minNextX = Mathf.Max(minX, previousX - maxStep);
        int maxNextX = Mathf.Min(maxX, previousX + maxStep);

        return UnityEngine.Random.Range(minNextX, maxNextX + 1);
    }

    private bool IsTooCloseToUsedBlocks(
        int x,
        List<int> usedX,
        int blockWidth)
    {
        foreach (int used in usedX)
        {
            if (Mathf.Abs(x - used) <= blockWidth)
                return true;
        }

        return false;
    }

    private PlatformTile SetRandomFinishNearX(
    PlatformMiniGameGrid grid,
    int preferredX)
    {
        int minX = grid.MinX + 1;
        int maxX = grid.MaxX - 1;

        int finishX = Mathf.Clamp(
            preferredX + UnityEngine.Random.Range(-1, 2),
            minX,
            maxX
        );

        int finishY = grid.MaxY;

        PlatformTile finish = grid.GetTile(finishX, finishY);

        if (finish)
            finish.SetFinish();

        return finish;
    }
}