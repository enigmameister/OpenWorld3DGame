using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArkanoidMiniGame : MonoBehaviour
{
    private enum ArkanoidPowerupType
    {
        PenetrationGold,
        BigPaddle,
        MultiBall,
        SmallPaddle,
        SpeedBoost
    }

    private class ArkanoidBrick
    {
        public Vector2Int Position;
        public int HitsLeft;
        public bool IsHard;
    }

    private class ArkanoidBall
    {
        public Vector2Int Position;
        public int Dx;
        public int Dy;
        public float StepInterval;
        public float Timer;
    }

    private class FallingPowerup
    {
        public ArkanoidPowerupType Type;
        public Vector2Int Position;
        public float Timer;
    }

    [Header("Stages")]
    [SerializeField] private int arkanoidStages = 3;
    [SerializeField] private float stageStartPause = 0.5f;

    [Header("Ball")]
    [SerializeField] private float baseBallStepInterval = 0.3f;
    [SerializeField] private float minBallStepInterval = 0.09f;
    [SerializeField] private float ballSpeedupPerBounce = 0.965f;
    [SerializeField] private float temporarySpeedMultiplier = 0.7f;
    [SerializeField] private int initialBallDx = 1;
    [SerializeField] private int initialBallDy = 1;

    [Header("Paddle")]
    [SerializeField] private int normalPaddleHalfWidth = 2; // 1x5
    [SerializeField] private int bigPaddleHalfWidth = 3;    // 1x7
    [SerializeField] private int smallPaddleHalfWidth = 1;  // 1x3
    [SerializeField] private int paddleHalfDepth = 0;
    [SerializeField] private int paddleYOffsetFromWall = 1;

    [Header("Bricks")]
    [SerializeField] private int stage1BrickRows = 3;
    [SerializeField] private int stage2BrickRows = 4;
    [SerializeField] private int stage3BrickRows = 6;
    [SerializeField] private int brickTopPadding = 2;
    [SerializeField] private int normalBrickSidePadding = 2;
    [SerializeField] private bool fullWidthHardBrickWall = true;

    [Header("Hard Bricks")]
    [SerializeField] private int hardBrickHits = 3;

    [Header("Powerups")]
    [SerializeField] private float powerupDropChance = 0.18f;
    [SerializeField] private float powerupFallStepInterval = 0.22f;
    [SerializeField] private float powerupDuration = 30f;

    private int currentArkanoidStage;

    private int minX;
    private int maxX;
    private int minY;
    private int maxY;

    private Vector2Int paddleCenter;
    private int currentPaddleHalfWidth;

    private int? preferredNextPaddleX;

    private PlatformTile lastGroundedPlayerTile;

    private float penetrationUntil;
    private float bigPaddleUntil;
    private float smallPaddleUntil;
    private float speedBoostUntil;

    private readonly Dictionary<Vector2Int, ArkanoidBrick> bricks = new();
    private readonly List<ArkanoidBall> balls = new();
    private readonly List<FallingPowerup> fallingPowerups = new();

    public IEnumerator Run(
        PlatformMiniGameGrid grid,
        Func<bool> isGameRunning,
        Func<PlatformTile> getCurrentPlayerTile,
        Func<int> getLivesLeft,
        Action loseGame,
        Action<string, int, int, int> showGameScreen,
        Action<float> updateGameTimes,
        Func<string, int, int, float, string, IEnumerator> showStageWinScreen,
        Func<string> getNextSequenceName,
        Action hideWindow,
        bool skipFirstStageStart,
        Action<bool> setSequenceActive)
    {
        bool skipStartOnlyOnce = skipFirstStageStart;

        CacheBounds(grid);

        for (int stage = currentArkanoidStage; stage < arkanoidStages; stage++)
        {
            currentArkanoidStage = stage;

            if (!isGameRunning())
                yield break;

            PlatformTile playerTileBeforeStage = getCurrentPlayerTile != null
                ? getCurrentPlayerTile()
                : null;

            SetupStage(grid, stage, playerTileBeforeStage);

            if (!skipStartOnlyOnce)
            {
                yield return WaitForStageStart(
                    grid,
                    isGameRunning,
                    getCurrentPlayerTile,
                    setSequenceActive
                );
            }
            else
            {
                yield return WaitUntilPlayerIsOnPaddle(
                    isGameRunning,
                    getCurrentPlayerTile
                );
            }

            skipStartOnlyOnce = false;

            if (!isGameRunning())
                yield break;

            showGameScreen?.Invoke(
                "Arkanoid",
                stage + 1,
                arkanoidStages,
                getLivesLeft != null ? getLivesLeft() : 0
            );

            setSequenceActive?.Invoke(true);

            float stageStartTime = Time.time;

            PaintBoard(grid, waitingForStart: false);

            while (isGameRunning())
            {
                float stageElapsed = Time.time - stageStartTime;
                updateGameTimes?.Invoke(stageElapsed);

                UpdatePowerupTimers();

                PlatformTile currentPlayerTile = getCurrentPlayerTile != null
                    ? getCurrentPlayerTile()
                    : null;

                if (currentPlayerTile != null)
                {
                    lastGroundedPlayerTile = currentPlayerTile;

                    if (!IsPaddleTile(currentPlayerTile.GridX, currentPlayerTile.GridY))
                    {
                        currentPlayerTile.SetPlayerFail();

                        setSequenceActive?.Invoke(false);
                        loseGame?.Invoke();
                        yield break;
                    }

                    else
                    {         
                        UpdatePaddleFromPlayer(currentPlayerTile);
                    }
                }

                UpdateFallingPowerups(Time.deltaTime);

                if (bricks.Count == 0)
                    break;

                bool lostAllBalls = UpdateBalls(stage, Time.deltaTime);

                PaintBoard(grid, waitingForStart: false);

                if (lostAllBalls)
                {
                    PlatformTile failTile = grid.GetTile(paddleCenter.x, minY);

                    if (failTile)
                        failTile.SetPlayerFail();

                    setSequenceActive?.Invoke(false);
                    loseGame?.Invoke();
                    yield break;
                }

                yield return null;
            }

            setSequenceActive?.Invoke(false);

            if (!isGameRunning())
                yield break;

            PlatformTile playerTileAfterStage = getCurrentPlayerTile != null
                ? getCurrentPlayerTile()
                : null;

            if (playerTileAfterStage != null)
            {
                preferredNextPaddleX = Mathf.Clamp(
                    playerTileAfterStage.GridX,
                    minX + normalPaddleHalfWidth,
                    maxX - normalPaddleHalfWidth
                );
            }
            else
            {
                preferredNextPaddleX = paddleCenter.x;
            }

            grid.SetAllSafe();

            float clearTime = Time.time - stageStartTime;

            string nextName = stage + 1 < arkanoidStages
                ? "ARKANOID"
                : getNextSequenceName != null
                    ? getNextSequenceName()
                    : "FINISH";

            if (showStageWinScreen != null)
            {
                yield return showStageWinScreen(
                    "Arkanoid",
                    stage + 1,
                    arkanoidStages,
                    clearTime,
                    nextName
                );
            }

            currentArkanoidStage = stage + 1;

            hideWindow?.Invoke();

            yield return new WaitForSeconds(stageStartPause);
        }

        currentArkanoidStage = 0;
        grid.SetAllSafe();
    }

    private IEnumerator WaitUntilPlayerIsOnPaddle(
    Func<bool> isGameRunning,
    Func<PlatformTile> getCurrentPlayerTile)
    {
        float timer = 0f;
        float maxWait = 0.5f;

        while (isGameRunning() && timer < maxWait)
        {
            PlatformTile current = getCurrentPlayerTile != null
                ? getCurrentPlayerTile()
                : null;

            if (current != null && IsPaddleTile(current.GridX, current.GridY))
            {
                lastGroundedPlayerTile = current;
                UpdatePaddleFromPlayer(current);
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForStageStart(
        PlatformMiniGameGrid grid,
        Func<bool> isGameRunning,
        Func<PlatformTile> getCurrentPlayerTile,
        Action<bool> setSequenceActive)
    {
        setSequenceActive?.Invoke(false);

        PaintBoard(grid, waitingForStart: true);

        while (isGameRunning())
        {
            PlatformTile current = getCurrentPlayerTile != null
                ? getCurrentPlayerTile()
                : null;

            if (current != null && IsPaddleTile(current.GridX, current.GridY))
            {
                lastGroundedPlayerTile = current;
                UpdatePaddleFromPlayer(current);
                yield break;
            }

            yield return null;
        }
    }

    private void CacheBounds(PlatformMiniGameGrid grid)
    {
        minX = grid.MinX;
        maxX = grid.MaxX;
        minY = grid.MinY;
        maxY = grid.MaxY;
    }

    private void SetupStage(
        PlatformMiniGameGrid grid,
        int stage,
        PlatformTile currentPlayerTile)
    {
        int centerX = (minX + maxX) / 2;
        int startX = centerX;

        if (preferredNextPaddleX.HasValue)
            startX = preferredNextPaddleX.Value;
        else if (currentPlayerTile != null)
            startX = currentPlayerTile.GridX;

        currentPaddleHalfWidth = normalPaddleHalfWidth;

        startX = Mathf.Clamp(
            startX,
            minX + currentPaddleHalfWidth,
            maxX - currentPaddleHalfWidth
        );

        paddleCenter = new Vector2Int(
            startX,
            minY + paddleYOffsetFromWall
        );

        ResetTemporaryPowerups();

        lastGroundedPlayerTile = null;

        bricks.Clear();
        balls.Clear();
        fallingPowerups.Clear();

        BuildBrickWall(stage);
        SpawnSingleBall(startX);

        PaintBoard(grid, waitingForStart: true);
    }

    private void SpawnSingleBall(int x)
    {
        balls.Add(new ArkanoidBall
        {
            Position = new Vector2Int(x, paddleCenter.y + 2),
            Dx = initialBallDx >= 0 ? 1 : -1,
            Dy = initialBallDy >= 0 ? 1 : -1,
            StepInterval = baseBallStepInterval,
            Timer = 0f
        });
    }

    private void BuildBrickWall(int stage)
    {
        int rows = GetBrickRowsForStage(stage);
        int topY = maxY - brickTopPadding;
        int frontRowY = topY - rows + 1;

        for (int row = 0; row < rows; row++)
        {
            int y = topY - row;

            bool hardBrickRow =
                stage >= 1 &&
                y == frontRowY;

            int startX;
            int endX;

            if (hardBrickRow && fullWidthHardBrickWall)
            {
                startX = minX;
                endX = maxX;
            }
            else
            {
                startX = minX + normalBrickSidePadding;
                endX = maxX - normalBrickSidePadding;
            }

            for (int x = startX; x <= endX; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);

                bricks[pos] = new ArkanoidBrick
                {
                    Position = pos,
                    IsHard = hardBrickRow,
                    HitsLeft = hardBrickRow ? hardBrickHits : 1
                };
            }
        }
    }

    private int GetBrickRowsForStage(int stage)
    {
        return stage switch
        {
            0 => stage1BrickRows,
            1 => stage2BrickRows,
            _ => stage3BrickRows
        };
    }

    private void UpdatePaddleFromPlayer(PlatformTile currentPlayerTile)
    {
        int clampedX = Mathf.Clamp(
            currentPlayerTile.GridX,
            minX + currentPaddleHalfWidth,
            maxX - currentPaddleHalfWidth
        );

        paddleCenter = new Vector2Int(
            clampedX,
            minY + paddleYOffsetFromWall
        );
    }

    private bool UpdateBalls(int stage, float deltaTime)
    {
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            ArkanoidBall ball = balls[i];

            ball.Timer += deltaTime;

            float effectiveStepInterval = GetEffectiveBallStepInterval(ball);

            if (ball.Timer < effectiveStepInterval)
                continue;

            ball.Timer = 0f;

            bool lost = AdvanceBall(ball, stage);

            if (lost)
                balls.RemoveAt(i);
        }

        return balls.Count == 0;
    }

    private float GetEffectiveBallStepInterval(ArkanoidBall ball)
    {
        if (IsSpeedBoostActive())
        {
            return Mathf.Max(
                minBallStepInterval,
                ball.StepInterval * temporarySpeedMultiplier
            );
        }

        return ball.StepInterval;
    }

    private bool AdvanceBall(ArkanoidBall ball, int stage)
    {
        bool speedupAllowed = stage >= 1;
        bool penetration = IsPenetrationActive();

        // 1. Ruch poziomy.
        Vector2Int nextX = new Vector2Int(ball.Position.x + ball.Dx, ball.Position.y);

        if (nextX.x < minX || nextX.x > maxX)
        {
            ball.Dx *= -1;
            ApplyBounceSpeedup(ball, speedupAllowed);
            nextX = new Vector2Int(ball.Position.x + ball.Dx, ball.Position.y);
        }

        if (bricks.ContainsKey(nextX))
        {
            HitBrick(nextX, penetration);

            if (!penetration)
            {
                ball.Dx *= -1;
                ApplyBounceSpeedup(ball, speedupAllowed);
                nextX = new Vector2Int(ball.Position.x + ball.Dx, ball.Position.y);
            }
        }

        ball.Position = nextX;

        // 2. Ruch pionowy.
        Vector2Int nextY = new Vector2Int(ball.Position.x, ball.Position.y + ball.Dy);

        if (nextY.y > maxY)
        {
            ball.Dy = -1;
            ApplyBounceSpeedup(ball, speedupAllowed);
            nextY = new Vector2Int(ball.Position.x, ball.Position.y + ball.Dy);
        }

        if (bricks.ContainsKey(nextY))
        {
            HitBrick(nextY, penetration);

            if (!penetration)
            {
                ball.Dy *= -1;
                ApplyBounceSpeedup(ball, speedupAllowed);
                nextY = new Vector2Int(ball.Position.x, ball.Position.y + ball.Dy);
            }
        }

        // 3. Odbicie od platformy.
        if (ball.Dy < 0 && IsPaddleTile(nextY.x, nextY.y))
        {
            BounceFromPaddle(ball, nextY.x);
            ApplyBounceSpeedup(ball, speedupAllowed);

            nextY = new Vector2Int(ball.Position.x + ball.Dx, ball.Position.y + ball.Dy);
        }

        // 4. Pi³ka spad³a za platformê.
        if (nextY.y <= minY)
        {
            ball.Position = nextY;
            return true;
        }

        ball.Position = nextY;
        return false;
    }

    private void HitBrick(Vector2Int brickPos, bool penetration)
    {
        if (!bricks.TryGetValue(brickPos, out ArkanoidBrick brick))
            return;

        if (penetration)
            brick.HitsLeft = 0;
        else
            brick.HitsLeft--;

        if (brick.HitsLeft <= 0)
        {
            bricks.Remove(brickPos);
            TrySpawnPowerup(brickPos);
        }
    }

    private void TrySpawnPowerup(Vector2Int spawnPos)
    {
        if (UnityEngine.Random.value > powerupDropChance)
            return;

        ArkanoidPowerupType type = GetRandomPowerupType();

        fallingPowerups.Add(new FallingPowerup
        {
            Type = type,
            Position = spawnPos,
            Timer = 0f
        });
    }

    private ArkanoidPowerupType GetRandomPowerupType()
    {
        int value = UnityEngine.Random.Range(0, 5);

        return value switch
        {
            0 => ArkanoidPowerupType.PenetrationGold,
            1 => ArkanoidPowerupType.BigPaddle,
            2 => ArkanoidPowerupType.MultiBall,
            3 => ArkanoidPowerupType.SmallPaddle,
            _ => ArkanoidPowerupType.SpeedBoost
        };
    }

    private void UpdateFallingPowerups(float deltaTime)
    {
        for (int i = fallingPowerups.Count - 1; i >= 0; i--)
        {
            FallingPowerup powerup = fallingPowerups[i];

            powerup.Timer += deltaTime;

            if (powerup.Timer < powerupFallStepInterval)
                continue;

            powerup.Timer = 0f;
            powerup.Position += Vector2Int.down;

            if (powerup.Position.y < minY)
            {
                fallingPowerups.RemoveAt(i);
                continue;
            }

            if (powerup.Position.y == paddleCenter.y &&
                IsPaddleTile(powerup.Position.x, powerup.Position.y))
            {
                ActivatePowerup(powerup.Type);
                fallingPowerups.RemoveAt(i);
            }
        }
    }

    private void ActivatePowerup(ArkanoidPowerupType type)
    {
        float until = Time.time + powerupDuration;

        switch (type)
        {
            case ArkanoidPowerupType.PenetrationGold:
                penetrationUntil = until;
                break;

            case ArkanoidPowerupType.BigPaddle:
                bigPaddleUntil = until;
                smallPaddleUntil = 0f;
                break;

            case ArkanoidPowerupType.MultiBall:
                SpawnMultiBalls();
                break;

            case ArkanoidPowerupType.SmallPaddle:
                smallPaddleUntil = until;
                bigPaddleUntil = 0f;
                break;

            case ArkanoidPowerupType.SpeedBoost:
                speedBoostUntil = until;
                break;
        }

        UpdatePowerupTimers();
    }

    private void SpawnMultiBalls()
    {
        if (balls.Count >= 3)
            return;

        ArkanoidBall source = balls.Count > 0
            ? balls[0]
            : null;

        Vector2Int spawnPos = source != null
            ? source.Position
            : new Vector2Int(paddleCenter.x, paddleCenter.y + 2);

        float interval = source != null
            ? source.StepInterval
            : baseBallStepInterval;

        balls.Clear();

        balls.Add(new ArkanoidBall
        {
            Position = spawnPos,
            Dx = -1,
            Dy = 1,
            StepInterval = interval,
            Timer = 0f
        });

        balls.Add(new ArkanoidBall
        {
            Position = spawnPos,
            Dx = 1,
            Dy = 1,
            StepInterval = interval,
            Timer = 0f
        });

        balls.Add(new ArkanoidBall
        {
            Position = spawnPos,
            Dx = UnityEngine.Random.value > 0.5f ? 1 : -1,
            Dy = -1,
            StepInterval = interval,
            Timer = 0f
        });
    }

    private void UpdatePowerupTimers()
    {
        float now = Time.time;

        int targetWidth = normalPaddleHalfWidth;

        if (bigPaddleUntil > now)
            targetWidth = bigPaddleHalfWidth;

        if (smallPaddleUntil > now)
            targetWidth = smallPaddleHalfWidth;

        currentPaddleHalfWidth = targetWidth;

        paddleCenter.x = Mathf.Clamp(
            paddleCenter.x,
            minX + currentPaddleHalfWidth,
            maxX - currentPaddleHalfWidth
        );
    }

    private void ResetTemporaryPowerups()
    {
        penetrationUntil = 0f;
        bigPaddleUntil = 0f;
        smallPaddleUntil = 0f;
        speedBoostUntil = 0f;
        currentPaddleHalfWidth = normalPaddleHalfWidth;
    }

    private bool IsPenetrationActive()
    {
        return Time.time < penetrationUntil;
    }

    private bool IsSpeedBoostActive()
    {
        return Time.time < speedBoostUntil;
    }

    private void ApplyBounceSpeedup(ArkanoidBall ball, bool allowed)
    {
        if (!allowed)
            return;

        ball.StepInterval = Mathf.Max(
            minBallStepInterval,
            ball.StepInterval * ballSpeedupPerBounce
        );
    }

    private void BounceFromPaddle(ArkanoidBall ball, int hitX)
    {
        ball.Dy = 1;

        int offset = hitX - paddleCenter.x;

        if (offset < 0)
            ball.Dx = -1;
        else if (offset > 0)
            ball.Dx = 1;
        else if (ball.Dx == 0)
            ball.Dx = UnityEngine.Random.value > 0.5f ? 1 : -1;
    }

    private bool IsPaddleTile(int x, int y)
    {
        return x >= paddleCenter.x - currentPaddleHalfWidth &&
               x <= paddleCenter.x + currentPaddleHalfWidth &&
               y >= paddleCenter.y - paddleHalfDepth &&
               y <= paddleCenter.y + paddleHalfDepth;
    }

    private void PaintBoard(PlatformMiniGameGrid grid, bool waitingForStart)
    {
        foreach (PlatformTile tile in PlatformMiniGameGrid.Tiles)
        {
            if (!tile)
                continue;

            tile.SetStart(); // niebieskie t³o planszy
        }

        foreach (KeyValuePair<Vector2Int, ArkanoidBrick> pair in bricks)
        {
            PlatformTile brickTile = grid.GetTile(pair.Key.x, pair.Key.y);

            if (!brickTile)
                continue;

            if (pair.Value.IsHard)
                brickTile.SetBlocked(); // czarne twarde tile
            else
                brickTile.SetDanger(); // czerwone zwyk³e tile
        }

        DrawFallingPowerups(grid);
        DrawPaddle(grid);
        DrawBalls(grid);

        if (waitingForStart)
        {
            PlatformTile centerTile = grid.GetTile(paddleCenter.x, paddleCenter.y);

            if (centerTile)
                centerTile.SetStart();
        }
    }

    private void DrawPaddle(PlatformMiniGameGrid grid)
    {
        for (int y = paddleCenter.y - paddleHalfDepth; y <= paddleCenter.y + paddleHalfDepth; y++)
        {
            for (int x = paddleCenter.x - currentPaddleHalfWidth; x <= paddleCenter.x + currentPaddleHalfWidth; x++)
            {
                PlatformTile paddleTile = grid.GetTile(x, y);

                if (paddleTile)
                    paddleTile.SetPath(); // cyan platforma
            }
        }
    }

    private void DrawBalls(PlatformMiniGameGrid grid)
    {
        foreach (ArkanoidBall ball in balls)
        {
            PlatformTile ballTile = grid.GetTile(ball.Position.x, ball.Position.y);

            if (ballTile)
                ballTile.SetPlayerFail(); // bia³a pi³ka
        }
    }

    private void DrawFallingPowerups(PlatformMiniGameGrid grid)
    {
        foreach (FallingPowerup powerup in fallingPowerups)
        {
            PlatformTile tile = grid.GetTile(powerup.Position.x, powerup.Position.y);

            if (!tile)
                continue;

            switch (powerup.Type)
            {
                case ArkanoidPowerupType.PenetrationGold:
                    tile.SetFinish(); // gold/yellow
                    break;

                case ArkanoidPowerupType.BigPaddle:
                    tile.SetDecor(); // gray
                    break;

                case ArkanoidPowerupType.MultiBall:
                    tile.SetSafe(); // green
                    break;

                case ArkanoidPowerupType.SmallPaddle:
                    tile.SetWarning(); // orange
                    break;

                case ArkanoidPowerupType.SpeedBoost:
                    tile.SetPowerSpeed(); // purple
                    break;
            }
        }
    }

    public void ResetProgress()
    {
        currentArkanoidStage = 0;

        bricks.Clear();
        balls.Clear();
        fallingPowerups.Clear();

        lastGroundedPlayerTile = null;
        preferredNextPaddleX = null;

        ResetTemporaryPowerups();
    }
}