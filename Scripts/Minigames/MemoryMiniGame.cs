using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MemoryMiniGame : MonoBehaviour
{
    private enum MemoryDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    [Header("Stages")]
    [SerializeField] private int memoryStages = 3;
    [SerializeField] private float stageStartPause = 0.25f;

    [Header("Board")]
    [SerializeField] private int boardRadius = 2; // 5x5
    [SerializeField] private int borderThickness = 1;
    [SerializeField] private float centerReturnPause = 0.25f;

    [Header("Intro Preview")]
    [SerializeField] private float introPreviewDelay = 2.5f;
    [SerializeField] private float introArrowDuration = 1f;

    [Header("Sequence Preview")]
    [SerializeField] private float directionSpriteDuration = 0.5f;
    [SerializeField] private float directionGapDuration = 0.15f;
    [SerializeField] private float tileFlashDuration = 0.5f;

    private bool introPreviewShown;
    private int currentMemoryStage;

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
        Action<bool> setSequenceActive,
        PlatformMiniGameScreenUI screenUI)
    {
        bool skipStartOnlyOnce = skipFirstStageStart;

        for (int stage = currentMemoryStage; stage < memoryStages; stage++)
        {
            currentMemoryStage = stage;

            if (!isGameRunning())
                yield break;

            Vector2Int center = GetCenter(grid);

            if (!skipStartOnlyOnce)
            {
                yield return WaitForCenterStart(
                    grid,
                    center,
                    isGameRunning,
                    getCurrentPlayerTile,
                    setSequenceActive
                );
            }

            skipStartOnlyOnce = false;

            if (!introPreviewShown && stage == 0)
            {
                yield return ShowIntroPreview(
                    grid,
                    center,
                    screenUI,
                    isGameRunning,
                    getCurrentPlayerTile
                );

                introPreviewShown = true;
            }

            if (!isGameRunning())
                yield break;

            int maxSequenceLength = GetMaxSequenceLengthForStage(stage);

            List<MemoryDirection> fullSequence = GenerateSequence(
                center,
                maxSequenceLength,
                grid
            );

            showGameScreen?.Invoke(
                "Memory",
                stage + 1,
                memoryStages,
                getLivesLeft != null ? getLivesLeft() : 0
            );

            float stageStartTime = Time.time;

            for (int round = 1; round <= maxSequenceLength; round++)
            {
                if (!isGameRunning())
                    yield break;

                setSequenceActive?.Invoke(false);

                PaintMemoryBoard(grid, center);
                yield return WaitUntilPlayerOnCenter(
                    grid,
                    center,
                    isGameRunning,
                    getCurrentPlayerTile
                );

                if (!isGameRunning())
                    yield break;

                yield return ShowSequencePreview(
                    grid,
                    center,
                    fullSequence,
                    round,
                    screenUI,
                    isGameRunning
                );

                if (!isGameRunning())
                    yield break;

                screenUI?.ShowNormalGamePanel();

                setSequenceActive?.Invoke(true);

                bool success = true;

                yield return ReplaySequence(
                    grid,
                    center,
                    fullSequence,
                    round,
                    isGameRunning,
                    getCurrentPlayerTile,
                    updateGameTimes,
                    stageStartTime,
                    () => success = false
                );

                setSequenceActive?.Invoke(false);

                if (!isGameRunning())
                    yield break;

                if (!success)
                {
                    PlatformTile failTile = getCurrentPlayerTile != null
                        ? getCurrentPlayerTile()
                        : null;

                    if (failTile)
                        failTile.SetPlayerFail();

                    loseGame?.Invoke();
                    yield break;
                }

                PaintMemoryBoard(grid, center);

                yield return new WaitForSeconds(centerReturnPause);
            }

            setSequenceActive?.Invoke(false);

            if (!isGameRunning())
                yield break;

            grid.SetAllSafe();

            float clearTime = Time.time - stageStartTime;

            string nextName = stage + 1 < memoryStages
                ? "MEMORY"
                : getNextSequenceName != null
                    ? getNextSequenceName()
                    : "FINISH";

            if (showStageWinScreen != null)
            {
                yield return showStageWinScreen(
                    "Memory",
                    stage + 1,
                    memoryStages,
                    clearTime,
                    nextName
                );
            }

            currentMemoryStage = stage + 1;

            hideWindow?.Invoke();

            yield return new WaitForSeconds(stageStartPause);
        }

        currentMemoryStage = 0;
        grid.SetAllSafe();
    }

    private IEnumerator ShowIntroPreview(
      PlatformMiniGameGrid grid,
      Vector2Int center,
      PlatformMiniGameScreenUI screenUI,
      Func<bool> isGameRunning,
      Func<PlatformTile> getCurrentPlayerTile)
    {
        PaintMemoryBoard(grid, center);

        yield return new WaitForSeconds(introPreviewDelay);

        if (!isGameRunning())
            yield break;

        List<MemoryDirection> possible = GetPossibleDirections(center, grid);

        if (possible.Count == 0)
            yield break;

        MemoryDirection tutorialDirection = possible[
            UnityEngine.Random.Range(0, possible.Count)
        ];

        Vector2Int tutorialPos = center + DirectionToVector(tutorialDirection);

        PaintMemoryIntroTarget(grid, center, tutorialPos);

        if (screenUI != null)
        {
            yield return screenUI.ShowMemoryDirectionSprite(
                DirectionToString(tutorialDirection),
                introArrowDuration
            );
        }

        PaintMemoryIntroTarget(grid, center, tutorialPos);

        while (isGameRunning())
        {
            PlatformTile currentTile = getCurrentPlayerTile != null
                ? getCurrentPlayerTile()
                : null;

            if (currentTile != null &&
                currentTile.GridX == tutorialPos.x &&
                currentTile.GridY == tutorialPos.y)
            {
                break;
            }

            yield return null;
        }

        PaintMemoryBoard(grid, center);
    }

    private void PaintMemoryIntroTarget(
    PlatformMiniGameGrid grid,
    Vector2Int center,
    Vector2Int target)
    {
        PaintMemoryBoard(grid, center);

        PlatformTile targetTile = grid.GetTile(target.x, target.y);

        if (targetTile)
            targetTile.SetPlayerFail(); // bia³y tile instrukcyjny
    }

    private IEnumerator WaitForCenterStart(
        PlatformMiniGameGrid grid,
        Vector2Int center,
        Func<bool> isGameRunning,
        Func<PlatformTile> getCurrentPlayerTile,
        Action<bool> setSequenceActive)
    {
        setSequenceActive?.Invoke(false);

        PaintMemoryBoard(grid, center);

        PlatformTile startTile = grid.GetTile(center.x, center.y);

        if (startTile)
            startTile.SetStart();

        while (isGameRunning())
        {
            PlatformTile current = getCurrentPlayerTile != null
                ? getCurrentPlayerTile()
                : null;

            if (current != null &&
                current.GridX == center.x &&
                current.GridY == center.y)
            {
                break;
            }

            yield return null;
        }
    }

    private IEnumerator ShowSequencePreview(
        PlatformMiniGameGrid grid,
        Vector2Int center,
        List<MemoryDirection> sequence,
        int count,
        PlatformMiniGameScreenUI screenUI,
        Func<bool> isGameRunning)
    {
        Vector2Int cursor = center;

        for (int i = 0; i < count; i++)
        {
            if (!isGameRunning())
                yield break;

            MemoryDirection direction = sequence[i];
            cursor += DirectionToVector(direction);

            PaintMemoryBoard(grid, center);

            PlatformTile flashTile = grid.GetTile(cursor.x, cursor.y);

            if (flashTile)
                flashTile.SetPlayerFail(); // bia³y tile sekwencji

            if (screenUI != null)
                yield return screenUI.ShowMemoryDirectionSprite(
                    DirectionToString(direction),
                    directionSpriteDuration
                );
            else
                yield return new WaitForSeconds(tileFlashDuration);

            PaintMemoryBoard(grid, center);

            yield return new WaitForSeconds(directionGapDuration);
        }
    }

    private IEnumerator ReplaySequence(
        PlatformMiniGameGrid grid,
        Vector2Int center,
        List<MemoryDirection> sequence,
        int count,
        Func<bool> isGameRunning,
        Func<PlatformTile> getCurrentPlayerTile,
        Action<float> updateGameTimes,
        float stageStartTime,
        Action fail)
    {
        Vector2Int currentPos = center;
        PlatformTile lastTile = getCurrentPlayerTile != null
            ? getCurrentPlayerTile()
            : null;

        for (int i = 0; i < count; i++)
        {
            if (!isGameRunning())
                yield break;

            Vector2Int expectedPos = currentPos + DirectionToVector(sequence[i]);

            PaintMemoryReplayStep(
                grid,
                center,
                currentPos,
                expectedPos
            );

            bool stepResolved = false;

            while (!stepResolved && isGameRunning())
            {
                float stageElapsed = Time.time - stageStartTime;
                updateGameTimes?.Invoke(stageElapsed);

                PlatformTile currentTile = getCurrentPlayerTile != null
                    ? getCurrentPlayerTile()
                    : null;

                if (currentTile != null && currentTile != lastTile)
                {
                    Vector2Int enteredPos = new Vector2Int(
                        currentTile.GridX,
                        currentTile.GridY
                    );

                    if (enteredPos != expectedPos)
                    {
                        currentTile.SetPlayerFail();
                        fail?.Invoke();
                        yield break;
                    }

                    currentPos = expectedPos;
                    lastTile = currentTile;
                    stepResolved = true;
                }

                yield return null;
            }
        }

        PaintMemoryBoard(grid, center);
    }

    private IEnumerator WaitUntilPlayerOnCenter(
        PlatformMiniGameGrid grid,
        Vector2Int center,
        Func<bool> isGameRunning,
        Func<PlatformTile> getCurrentPlayerTile)
    {
        PaintMemoryBoard(grid, center);

        while (isGameRunning())
        {
            PlatformTile currentTile = getCurrentPlayerTile != null
                ? getCurrentPlayerTile()
                : null;

            if (currentTile != null &&
                currentTile.GridX == center.x &&
                currentTile.GridY == center.y)
            {
                yield break;
            }

            yield return null;
        }
    }

    private List<MemoryDirection> GenerateSequence(
        Vector2Int center,
        int length,
        PlatformMiniGameGrid grid)
    {
        List<MemoryDirection> result = new();

        Vector2Int cursor = center;

        for (int i = 0; i < length; i++)
        {
            List<MemoryDirection> possible = GetPossibleDirections(
                cursor,
                grid
            );

            if (possible.Count == 0)
                break;

            MemoryDirection chosen = possible[
                UnityEngine.Random.Range(0, possible.Count)
            ];

            result.Add(chosen);
            cursor += DirectionToVector(chosen);
        }

        return result;
    }

    private List<MemoryDirection> GetPossibleDirections(
        Vector2Int from,
        PlatformMiniGameGrid grid)
    {
        List<MemoryDirection> possible = new();

        TryAddDirection(possible, MemoryDirection.Up, from, grid);
        TryAddDirection(possible, MemoryDirection.Down, from, grid);
        TryAddDirection(possible, MemoryDirection.Left, from, grid);
        TryAddDirection(possible, MemoryDirection.Right, from, grid);

        return possible;
    }

    private void TryAddDirection(
        List<MemoryDirection> list,
        MemoryDirection direction,
        Vector2Int from,
        PlatformMiniGameGrid grid)
    {
        Vector2Int next = from + DirectionToVector(direction);

        if (!IsInsideMemoryBoard(grid, next))
            return;

        list.Add(direction);
    }

    private void PaintMemoryBoard(
       PlatformMiniGameGrid grid,
       Vector2Int center)
    {
        grid.ResetTiles();

        foreach (PlatformTile tile in PlatformMiniGameGrid.Tiles)
        {
            if (!tile)
                continue;

            Vector2Int pos = new Vector2Int(tile.GridX, tile.GridY);

            if (pos == center)
            {
                tile.SetStart(); // niebieski œrodek
            }
            else if (IsInsideMemoryBoard(grid, pos))
            {
                tile.SetDecor(); // szare pole gry 5x5
            }
            else if (IsMemoryBorder(grid, pos))
            {
                tile.SetBlocked(); // fioletowe obramowanie
            }
            else
            {
                tile.SetDanger(); // reszta planszy czerwona
            }
        }
    }

    private bool IsMemoryBorder(
    PlatformMiniGameGrid grid,
    Vector2Int pos)
    {
        Vector2Int center = GetCenter(grid);

        int dx = Mathf.Abs(pos.x - center.x);
        int dy = Mathf.Abs(pos.y - center.y);

        int outerRadius = boardRadius + borderThickness;

        bool insideOuter = dx <= outerRadius && dy <= outerRadius;
        bool outsideBoard = dx > boardRadius || dy > boardRadius;

        return insideOuter && outsideBoard;
    }

    private void PaintMemoryReplayStep(
       PlatformMiniGameGrid grid,
       Vector2Int center,
       Vector2Int currentPos,
       Vector2Int expectedPos)
    {
        PaintMemoryBoard(grid, center);

        PlatformTile currentTile = grid.GetTile(currentPos.x, currentPos.y);
        if (currentTile)
        {
            if (currentPos == center)
                currentTile.SetStart();
            else
                currentTile.SetPath();
        }

        PlatformTile expectedTile = grid.GetTile(expectedPos.x, expectedPos.y);
        if (expectedTile)
            expectedTile.SetPlayerFail(); // bia³y tile oczekiwanego ruchu
    }

    private bool IsInsideMemoryBoard(
        PlatformMiniGameGrid grid,
        Vector2Int pos)
    {
        Vector2Int center = GetCenter(grid);

        return Mathf.Abs(pos.x - center.x) <= boardRadius &&
               Mathf.Abs(pos.y - center.y) <= boardRadius;
    }

    private Vector2Int GetCenter(PlatformMiniGameGrid grid)
    {
        return new Vector2Int(
            (grid.MinX + grid.MaxX) / 2,
            (grid.MinY + grid.MaxY) / 2
        );
    }

    private int GetMaxSequenceLengthForStage(int stage)
    {
        return stage switch
        {
            0 => 3,
            1 => 7,
            _ => 11
        };
    }

    private Vector2Int DirectionToVector(MemoryDirection direction)
    {
        return direction switch
        {
            MemoryDirection.Up => new Vector2Int(0, 1),
            MemoryDirection.Down => new Vector2Int(0, -1),
            MemoryDirection.Left => new Vector2Int(-1, 0),
            MemoryDirection.Right => new Vector2Int(1, 0),
            _ => Vector2Int.zero
        };
    }

    private string DirectionToString(MemoryDirection direction)
    {
        return direction switch
        {
            MemoryDirection.Up => "UP",
            MemoryDirection.Down => "DOWN",
            MemoryDirection.Left => "LEFT",
            MemoryDirection.Right => "RIGHT",
            _ => ""
        };
    }
    public void ResetProgress()
    {
        currentMemoryStage = 0;
        introPreviewShown = false;
    }
}