using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrafficMiniGame : MonoBehaviour
{
    [Header("Stages")]
    [SerializeField] private int trafficStages = 3;
    [SerializeField] private float stageStartPause = 0.15f;

    [Header("Difficulty")]
    [SerializeField] private float speedMultiplierPerStage = 0.8f;
    [SerializeField] private int startingSafeStripes = 3;

    [Header("Obstacles")]
    [SerializeField] private int normalObstacleWidth = 4;
    [SerializeField] private int hardObstacleWidth = 1;
    [SerializeField] private int minObstacleGap = 5;
    [SerializeField] private int maxObstacleGap = 9;

    [Header("Stage 3 Aggression")]
    [SerializeField] private int aggressiveNearLaneDistance = 2;
    [SerializeField] private float aggressiveTickMultiplier = 0.75f;

    private int lastFinishX = -1;
    private int currentTrafficStage;

    private struct LaneData
    {
        public int y;
        public int direction;
        public float tick;
        public int phase;
        public int obstacleWidth;
        public int gap;
        public bool hardLane;
    }

    private bool StageStartsFromBottom(int stage)
    {
        // Stage 1: bottom -> top
        // Stage 2: top -> bottom
        // Stage 3: bottom -> top
        return stage % 2 == 0;
    }

    public IEnumerator Run(
        PlatformMiniGameGrid grid,
        int edgeDepth,
        float baseTrafficTick,
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

        for (int stage = currentTrafficStage; stage < trafficStages; stage++)
        {
            if (!isGameRunning())
                yield break;

            currentTrafficStage = stage;

            resetFinish?.Invoke();

            if (!skipStartOnlyOnce)
            {
                yield return WaitForStageStart(
                    grid,
                    edgeDepth,
                    stage,
                    getCurrentPlayerTile,
                    isGameRunning,
                    setSequenceActive
                );
            }

            skipStartOnlyOnce = false;

            showGameScreen?.Invoke(
                "Traffic",
                stage + 1,
                trafficStages,
                getLivesLeft != null ? getLivesLeft() : 0
            );

            if (!isGameRunning())
                yield break;

            float stageStartTime = Time.time;

            yield return RunStage(
                grid,
                edgeDepth,
                stage,
                baseTrafficTick,
                isFinished,
                isGameRunning,
                getCurrentPlayerTile,
                checkPlayerDanger,
                stageStartTime,
                updateGameTimes,
                setSequenceActive
            );

            setSequenceActive?.Invoke(false);

            if (!isGameRunning())
                yield break;

            grid.SetAllSafe();

            float clearTime = Time.time - stageStartTime;

            string nextName = stage + 1 < trafficStages
                ? "TRAFFIC"
                : "NEXT MINIGAME";

            if (showStageWinScreen != null)
            {
                yield return showStageWinScreen(
                    "Traffic",
                    stage + 1,
                    trafficStages,
                    clearTime,
                    nextName
                );
            }

            currentTrafficStage = stage + 1;

            hideWindow?.Invoke();

            yield return new WaitForSeconds(stageStartPause);
        }

        grid.SetAllSafe();
        currentTrafficStage = 0;
    }

    private IEnumerator WaitForStageStart(
       PlatformMiniGameGrid grid,
       int edgeDepth,
       int stage,
       Func<PlatformTile> getCurrentPlayerTile,
       Func<bool> isGameRunning,
       Action<bool> setSequenceActive)
    {
        setSequenceActive?.Invoke(false);

        PaintStageWaitingArea(grid, edgeDepth);

        bool startFromBottom = StageStartsFromBottom(stage);

        int startY = startFromBottom
            ? grid.MinY + UnityEngine.Random.Range(0, edgeDepth)
            : grid.MaxY - UnityEngine.Random.Range(0, edgeDepth);

        int startX = stage > 0 && lastFinishX >= grid.MinX + 1 && lastFinishX < grid.MaxX
            ? lastFinishX
            : UnityEngine.Random.Range(grid.MinX + 1, grid.MaxX);

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

    private IEnumerator RunStage(
        PlatformMiniGameGrid grid,
        int edgeDepth,
        int stage,
        float baseTrafficTick,
        Func<bool> isFinished,
        Func<bool> isGameRunning,
        Func<PlatformTile> getCurrentPlayerTile,
        Action checkPlayerDanger,
        float stageStartTime,
        Action<float> updateGameTimes,
        Action<bool> setSequenceActive)
    {
        setSequenceActive?.Invoke(true);

        int roadStartY = grid.MinY + edgeDepth;
        int roadEndY = grid.MaxY - edgeDepth;

        bool startFromBottom = StageStartsFromBottom(stage);

        int finishY = startFromBottom
            ? grid.MaxY
            : grid.MinY;

        HashSet<int> safeRows = BuildSafeRowsForStage(
            roadStartY,
            roadEndY,
            stage
        );

        List<LaneData> lanes = BuildLanes(
            grid,
            roadStartY,
            roadEndY,
            safeRows,
            stage,
            baseTrafficTick
        );

        PlatformTile finishTile = grid.GetTile(
            UnityEngine.Random.Range(grid.MinX + 1, grid.MaxX),
            finishY
        );

        if (finishTile)
            finishTile.SetFinish();

        float elapsed = 0f;

        while (!isFinished() && isGameRunning())
        {
            elapsed += Time.deltaTime;

            PaintTrafficBase(grid, edgeDepth, safeRows);

            PlatformTile playerTile = getCurrentPlayerTile != null
                ? getCurrentPlayerTile()
                : null;

            DrawTrafficObstacles(grid, lanes, elapsed, stage, playerTile);

            if (finishTile)
            {
                finishTile.SetFinish();
                lastFinishX = finishTile.GridX;
            }

            float stageElapsed = Time.time - stageStartTime;
            updateGameTimes?.Invoke(stageElapsed);

            checkPlayerDanger?.Invoke();

            yield return null;
        }

        setSequenceActive?.Invoke(false);
    }

    private void PaintStageWaitingArea(
        PlatformMiniGameGrid grid,
        int edgeDepth)
    {
        grid.ResetTiles();

        for (int y = grid.MinY; y <= grid.MaxY; y++)
        {
            for (int x = grid.MinX; x <= grid.MaxX; x++)
            {
                PlatformTile tile = grid.GetTile(x, y);
                if (!tile) continue;

                bool edge = y < grid.MinY + edgeDepth ||
                            y > grid.MaxY - edgeDepth;

                if (edge)
                    tile.SetSafe();
                else
                    tile.SetNormal();
            }
        }
    }

    private void PaintTrafficBase(
        PlatformMiniGameGrid grid,
        int edgeDepth,
        HashSet<int> safeRows)
    {
        for (int y = grid.MinY; y <= grid.MaxY; y++)
        {
            for (int x = grid.MinX; x <= grid.MaxX; x++)
            {
                PlatformTile tile = grid.GetTile(x, y);
                if (!tile) continue;

                bool edge = y < grid.MinY + edgeDepth ||
                            y > grid.MaxY - edgeDepth;

                if (edge || safeRows.Contains(y))
                    tile.SetSafe();
                else
                    tile.SetNormal();
            }
        }
    }

    private HashSet<int> BuildSafeRows(
      int roadStartY,
      int roadEndY,
      int safeStripes)
    {
        HashSet<int> rows = new();

        if (safeStripes <= 0)
            return rows;

        int roadHeight = Mathf.Max(1, roadEndY - roadStartY + 1);

        for (int i = 1; i <= safeStripes; i++)
        {
            float t = i / (float)(safeStripes + 1);
            int y = roadStartY + Mathf.RoundToInt((roadHeight - 1) * t);

            rows.Add(y);
        }

        return rows;
    }

    private List<LaneData> BuildLanes(
        PlatformMiniGameGrid grid,
        int roadStartY,
        int roadEndY,
        HashSet<int> safeRows,
        int stage,
        float baseTrafficTick)
    {
        List<LaneData> lanes = new();

        float stageSpeed = Mathf.Pow(speedMultiplierPerStage, stage);

        for (int y = roadStartY; y <= roadEndY; y++)
        {
            if (safeRows.Contains(y))
                continue;

            bool hardLane = y >= roadEndY - 1;

            LaneData lane = new LaneData
            {
                y = y,
                direction = UnityEngine.Random.value > 0.5f ? 1 : -1,
                tick = Mathf.Max(
                    0.045f,
                    baseTrafficTick *
                    stageSpeed *
                    UnityEngine.Random.Range(0.75f, 1.25f) *
                    (hardLane ? 0.75f : 1f)
                ),
                phase = UnityEngine.Random.Range(0, 100),
                obstacleWidth = hardLane ? hardObstacleWidth : normalObstacleWidth,
                gap = stage >= 2
    ? UnityEngine.Random.Range(Mathf.Max(2, minObstacleGap - 2), Mathf.Max(3, maxObstacleGap - 3))
    : UnityEngine.Random.Range(minObstacleGap, maxObstacleGap + 1),
                hardLane = hardLane
            };

            lanes.Add(lane);
        }

        return lanes;
    }

    private void DrawTrafficObstacles(
        PlatformMiniGameGrid grid,
        List<LaneData> lanes,
        float elapsed,
        int stage,
        PlatformTile playerTile)
    {
        for (int i = 0; i < lanes.Count; i++)
        {
            LaneData lane = lanes[i];

            float effectiveTick = lane.tick;

            if (stage >= 2 && playerTile != null)
            {
                int distanceToPlayerLane = Mathf.Abs(playerTile.GridY - lane.y);

                if (distanceToPlayerLane <= aggressiveNearLaneDistance)
                {
                    effectiveTick *= aggressiveTickMultiplier;
                }
            }

            int tick = Mathf.FloorToInt(elapsed / effectiveTick);

            int offset = tick * lane.direction + lane.phase;

            int patternSize = lane.obstacleWidth + lane.gap;

            for (int x = grid.MinX; x <= grid.MaxX; x++)
            {
                int local = Mod(x + offset, patternSize);

                bool obstacle = local < lane.obstacleWidth;

                if (!obstacle)
                    continue;

                PlatformTile tile = grid.GetTile(x, lane.y);

                if (tile)
                    tile.SetDanger();
            }

            if (lane.hardLane)
                DrawHardLaneNoise(grid, lane, tick);
        }
    }

    private void DrawHardLaneNoise(
        PlatformMiniGameGrid grid,
        LaneData lane,
        int tick)
    {
        int noiseSeed = lane.phase + tick * 13;

        for (int x = grid.MinX + 1; x < grid.MaxX; x++)
        {
            int value = Mathf.Abs((x * 37 + noiseSeed * 17) % 23);

            if (value == 0 || value == 3)
            {
                PlatformTile tile = grid.GetTile(x, lane.y);

                if (tile)
                    tile.SetDanger();
            }
        }
    }

    private int Mod(int value, int mod)
    {
        return ((value % mod) + mod) % mod;
    }

    public void ResetProgress()
    {
        currentTrafficStage = 0;
        lastFinishX = -1;
    }

    private HashSet<int> BuildSafeRowsForStage(
    int roadStartY,
    int roadEndY,
    int stage)
    {
        // Stage 1 - bez zmian, obecne pasy bezpieczeñstwa.
        if (stage == 0)
        {
            return BuildSafeRows(
                roadStartY,
                roadEndY,
                startingSafeStripes
            );
        }

        // Stage 2 - jedna bezpieczna strefa 1x20 na œrodku.
        if (stage == 1)
        {
            HashSet<int> rows = new();
            int middleY = Mathf.RoundToInt((roadStartY + roadEndY) * 0.5f);
            rows.Add(middleY);
            return rows;
        }

        // Stage 3 - brak bezpiecznych stref.
        return new HashSet<int>();
    }

    public Vector2Int GetCurrentStageStartPosition(
    PlatformMiniGameGrid grid,
    int edgeDepth)
    {
        bool startFromBottom = StageStartsFromBottom(currentTrafficStage);

        int startY = startFromBottom
            ? grid.MinY + UnityEngine.Random.Range(0, edgeDepth)
            : grid.MaxY - UnityEngine.Random.Range(0, edgeDepth);

        int startX = currentTrafficStage > 0 &&
                     lastFinishX >= grid.MinX + 1 &&
                     lastFinishX < grid.MaxX
            ? lastFinishX
            : UnityEngine.Random.Range(grid.MinX + 1, grid.MaxX);

        return new Vector2Int(startX, startY);
    }
}