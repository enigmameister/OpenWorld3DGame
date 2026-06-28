using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformMiniGameManager : MonoBehaviour
{
    public enum PlatformSequenceType
    {
        Bridge,
        Traffic,
        Snake,
        Maze,
        Memory,
        Arkanoid
    }

    private enum MiniGameEntryState
    {
        Idle,
        Welcome,
        Preparing,
        ReadyToStart,
        Playing,
        Died,
        GameOver
    }

    public static PlatformMiniGameManager Instance { get; private set; }

    [Header("Core Modules")]
    [SerializeField] private PlatformMiniGameGrid miniGameGrid;
    [SerializeField] private PlatformMiniGameUI miniGameUI;
    [SerializeField] private PlatformMiniGamePainter miniGamePainter;
    [SerializeField] private PlatformMiniGameScreenUI screenUI;

    [Header("Mini Games")]
    [SerializeField] private BridgeMiniGame bridgeMiniGame;
    [SerializeField] private TrafficMiniGame trafficMiniGame;
    [SerializeField] private SnakeMiniGame snakeMiniGame;
    [SerializeField] private MazeMiniGame mazeMiniGame;
    [SerializeField] private MemoryMiniGame memoryMiniGame;
    [SerializeField] private ArkanoidMiniGame arkanoidMiniGame;

    [Header("Sequences")]
    [SerializeField]
    private PlatformSequenceType[] sequences =
    {
        PlatformSequenceType.Bridge,
        PlatformSequenceType.Traffic,
        PlatformSequenceType.Snake,
        PlatformSequenceType.Maze
    };

    [Header("Intro / Welcome")]
    [SerializeField] private string defaultDifficultyName = "EASY";
    [SerializeField] private float prepareAnimationDuration = 5f;
    [SerializeField] private float startTextDuration = 1f;
    [SerializeField] private string winMessage = "Wygrana! Ukoñczy³eœ minigrê.";

    [Header("Global Intro Countdown")]
    [SerializeField] private int globalCountdownFrom = 5;

    [Header("Timing")]
    [SerializeField] private float sequencePause = 1.25f;

    [Header("Player")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float minigameJumpHeight = 2f;

    [Header("Lives")]
    [SerializeField] private int maxLives = 3;

    [Header("Bridge")]
    [SerializeField] private int edgeDepth = 2;
    [SerializeField] private int bridgeOptionsPerRow = 4;
    [SerializeField] private float pathReactionTime = 5f;

    [Header("Traffic")]
    [SerializeField] private float trafficTick = 0.35f;

    [Header("Snake")]
    [SerializeField] private int snakeWaves = 5;
    [SerializeField] private float snakeStepDelay = 0.06f;
    [SerializeField] private int snakeLineWidth = 2;
    [SerializeField] private float snakeWavePause = 1.0f;
    [SerializeField] private float snakeSpeedMultiplierPerWave = 0.85f;
    [SerializeField] private int snakeLength = 8;

    [Header("Maze")]
    [SerializeField] private float mazeTileCollapseDelay = 5f;
    private PlatformTile failTileToShow;
    [SerializeField] private float mazeTileCenterPadding = 0f;
    private List<PlatformTile> Tiles => PlatformMiniGameGrid.Tiles;

    private int minX => miniGameGrid.MinX;
    private int maxX => miniGameGrid.MaxX;
    private int minY => miniGameGrid.MinY;
    private int maxY => miniGameGrid.MaxY;

    private readonly Dictionary<PlatformTile, Coroutine> decayCoroutines = new();

    private MiniGameEntryState entryState = MiniGameEntryState.Idle;

    private Coroutine gameRoutine;
    private Coroutine introPrepareRoutine;
    private Coroutine preCountdownAnimationRoutine;

    private PlatformTile currentPlayerTile;
    private PlatformTile lastPlayerTile;
    private PlatformTile introStartTile;
    private PlatformTile retryStartTile;

    private Vector2Int introStartGridPos;
    private Vector2Int pendingSequenceStart;

    private int currentSequenceIndex;
    private int livesLeft;

    private float savedJumpHeight;
    private float totalActiveGameTime;
    private float activeStageStartTime;
    private float currentStageElapsed;

    private bool gameRunning;
    private bool fullGameOver;
    private bool finishReached;
    private bool waitingForReset;
    private bool waitingForSequenceStart;
    private bool countdownRunning;
    private bool sequenceActive;
    private bool retryInProgress;
    private bool waitingForRetryAreaExit;
    private bool waitingForRetryStartTile;
    private bool idlePatternGenerated;
    private bool freezeBoardOnRetry;
    private bool mazeActive;
    private bool hasPendingSequenceStart;
    private bool activeStageTimerRunning;
    private bool preCountdownAnimationRunning;
    private bool usePreparedStartForCurrentSequence;
    private bool waitingForNextSequenceStartTile;
    private PlatformTile nextSequenceStartTile;

    private void Awake()
    {
        Instance = this;

        if (!miniGameGrid) miniGameGrid = GetComponent<PlatformMiniGameGrid>();

        if (!miniGameUI) miniGameUI = GetComponent<PlatformMiniGameUI>();

        if (!miniGamePainter) miniGamePainter = GetComponent<PlatformMiniGamePainter>();

        if (!screenUI) screenUI = GetComponent<PlatformMiniGameScreenUI>();

        // Minigames //

        if (!bridgeMiniGame) bridgeMiniGame = GetComponent<BridgeMiniGame>();

        if (!trafficMiniGame) trafficMiniGame = GetComponent<TrafficMiniGame>();

        if (!snakeMiniGame) snakeMiniGame = GetComponent<SnakeMiniGame>();

        if (!mazeMiniGame) mazeMiniGame = GetComponent<MazeMiniGame>();

        if (!memoryMiniGame) memoryMiniGame = GetComponent<MemoryMiniGame>();

        if (!arkanoidMiniGame) arkanoidMiniGame = GetComponent<ArkanoidMiniGame>();

        miniGameUI.Init(miniGameGrid);
        miniGamePainter.Init(miniGameGrid);
    }

    private void Update()
    {
        if (!activeStageTimerRunning)
            return;

        currentStageElapsed = Time.time - activeStageStartTime;

        screenUI?.UpdateTimes(
            currentStageElapsed,
            totalActiveGameTime + currentStageElapsed
        );
    }

    private void Start()
    {
        BuildGrid();
        ResetTiles();

        entryState = MiniGameEntryState.Idle;
        screenUI?.ShowIdle();
    }

    public void OnPlayerEnteredTile(PlatformTile tile)
    {
        if (tile == null)
            return;

        currentPlayerTile = tile;
        lastPlayerTile = tile;

        if (waitingForRetryStartTile && tile.IsStart)
        {
            waitingForRetryStartTile = false;
            retryInProgress = false;

            currentPlayerTile = tile;
            lastPlayerTile = tile;

            pendingSequenceStart = new Vector2Int(tile.GridX, tile.GridY);
            hasPendingSequenceStart = true;
            usePreparedStartForCurrentSequence = true;

            entryState = MiniGameEntryState.Playing;
            gameRunning = true;
            waitingForReset = false;

 
            gameRoutine = StartCoroutine(GameFlow(false));
            return;
        }

        if (waitingForNextSequenceStartTile && tile.IsStart)
        {
            waitingForNextSequenceStartTile = false;

            currentPlayerTile = tile;
            lastPlayerTile = tile;

            pendingSequenceStart = new Vector2Int(tile.GridX, tile.GridY);
            hasPendingSequenceStart = true;
            usePreparedStartForCurrentSequence = true;

            entryState = MiniGameEntryState.Playing;
            gameRunning = true;
            waitingForReset = false;

    

            gameRoutine = StartCoroutine(GameFlow(false));
            return;
        }

        if (entryState == MiniGameEntryState.Welcome && tile.IsStart)
        {
            StartIntroPreparation();
            return;
        }

        if (entryState == MiniGameEntryState.ReadyToStart && tile.IsStart)
        {
            StartActualMiniGame();
            return;
        }

        if (countdownRunning)
            return;

        if (waitingForSequenceStart && tile.IsStart)
        {
            waitingForSequenceStart = false;
            return;
        }

        if (!gameRunning)
            return;

        if (!sequenceActive)
            return;

        if (tile.IsDanger)
        {
            if (IsCurrentSequence(PlatformSequenceType.Maze))
            {
                // W Maze nie ufamy triggerowi s¹siedniego tile.
                // Skucha tylko jeœli œrodek gracza jest faktycznie na czerwonym/czarnym tile.
                PlatformTile centerTile = GetPlayerCenterTile();

                if (centerTile != null)
                {
                    currentPlayerTile = centerTile;

                    if (centerTile.IsDanger)
                    {
                        MarkFailTile(centerTile);
                        LoseGame();
                    }
                }

                return;
            }

            HandleDangerFail();
            return;
        }

        if (tile.CurrentState == PlatformTileState.Path && CurrentSequenceUsesPathDecay())
        {
            StartPathDecay(tile);
        }

        if (tile.IsFinish)
            finishReached = true;
    }

    public void OnPlayerExitedTile(PlatformTile tile)
    {
        if (countdownRunning)
            return;

        if (gameRunning &&
            tile.CurrentState == PlatformTileState.Path &&
            CurrentSequenceUsesPathDecay())
        {
            tile.SetDanger();
        }

        if (mazeActive && gameRunning && tile.CurrentState == PlatformTileState.Safe)
        {
            if (tile != retryStartTile &&
                tile != introStartTile &&
                tile != nextSequenceStartTile)
            {
                StartMazeTileCollapse(tile);
            }
        }

        if (currentPlayerTile == tile)
            currentPlayerTile = null;

        foreach (var t in Tiles)
        {
            if (t != null && t.IsPlayerOnTile)
            {
                currentPlayerTile = t;
                break;
            }
        }
    }

    private void StartPathDecay(PlatformTile tile)
    {
        if (decayCoroutines.ContainsKey(tile))
            return;

        decayCoroutines[tile] = StartCoroutine(CoPathDecay(tile));
    }

    private IEnumerator CoPathDecay(PlatformTile tile)
    {
        yield return new WaitForSeconds(0.15f);

        if (tile != null && tile.CurrentState == PlatformTileState.Path)
            tile.SetWarning();

        yield return new WaitForSeconds(pathReactionTime);

        if (tile != null && tile.CurrentState == PlatformTileState.Warning)
        {
            tile.SetDanger();

            if (tile.IsPlayerOnTile)
                LoseGame();
        }

        decayCoroutines.Remove(tile);
    }
    private void StartIntroPreparation()
    {
        if (entryState != MiniGameEntryState.Welcome)
            return;

        if (introPrepareRoutine != null)
            StopCoroutine(introPrepareRoutine);

        introPrepareRoutine = StartCoroutine(IntroPreparationRoutine());
    }

    private IEnumerator IntroPreparationRoutine()
    {
        entryState = MiniGameEntryState.Preparing;

        string nextGameName = GetCurrentSequenceName();

        screenUI?.ShowWelcomePrepare(
            nextGameName,
            defaultDifficultyName
        );

        StartPreCountdownTileAnimation();

        yield return new WaitForSeconds(prepareAnimationDuration);

        yield return StopPreCountdownTileAnimation();

        for (int i = globalCountdownFrom; i >= 0; i--)
        {
            screenUI?.ShowWelcomeCountdown(i.ToString());

            yield return miniGameUI.CountdownRoutineStep(
                i,
                currentPlayerTile
            );
        }

        screenUI?.ShowWelcomeStart();

        ResetTiles();

        if (introStartTile != null)
        {
            miniGamePainter.ApplyArenaIdlePatternAroundStart(introStartTile);
        }
        else
        {
            CreateIntroStartTileForCurrentSequence();
        }

        entryState = MiniGameEntryState.ReadyToStart;

        yield return new WaitForSeconds(startTextDuration);

        introPrepareRoutine = null;
    }

    private void StartActualMiniGame()
    {
        if (entryState != MiniGameEntryState.ReadyToStart)
            return;

        entryState = MiniGameEntryState.Playing;

        livesLeft = maxLives;
        currentSequenceIndex = 0;
        fullGameOver = false;
        retryInProgress = false;
        sequenceActive = false;
        finishReached = false;
        waitingForRetryAreaExit = false;
        totalActiveGameTime = 0f;
        ResetActiveStageTimer();

        trafficMiniGame?.ResetProgress();
        bridgeMiniGame?.ResetProgress();
        mazeMiniGame?.ResetProgress();
        snakeMiniGame?.ResetProgress();
        memoryMiniGame?.ResetProgress();
        arkanoidMiniGame?.ResetProgress();

        if (gameRoutine != null)
            StopCoroutine(gameRoutine);

        if (playerMovement)
        {
            savedJumpHeight = playerMovement.jumpHeight;
            playerMovement.jumpHeight = minigameJumpHeight;
        }

  

        if (introStartTile != null)
        {
            introStartGridPos = new Vector2Int(
                introStartTile.GridX,
                introStartTile.GridY
            );

            pendingSequenceStart = introStartGridPos;
            hasPendingSequenceStart = true;

            currentPlayerTile = introStartTile;
            lastPlayerTile = introStartTile;

            usePreparedStartForCurrentSequence = true;
        }
        else
        {
            hasPendingSequenceStart = false;
            usePreparedStartForCurrentSequence = false;
        }

        gameRoutine = StartCoroutine(GameFlow(false));
    }

    private IEnumerator GameFlow(bool withIntroAndCountdown)
    {
        gameRunning = true;
        waitingForReset = false;

        ResetTiles();

        if (withIntroAndCountdown)
        {
            StartPreCountdownTileAnimation();

            yield return new WaitForSeconds(2.5f);

            yield return StopPreCountdownTileAnimation();

            yield return CountdownRoutine();

        }

        for (; currentSequenceIndex < sequences.Length; currentSequenceIndex++)
        {
            finishReached = false;

            bool sequenceHandlesOwnStart =
                sequences[currentSequenceIndex] == PlatformSequenceType.Traffic ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Bridge ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Snake ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Maze ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Memory ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Arkanoid;

            bool consumePreparedStart = usePreparedStartForCurrentSequence;

            if (!sequenceHandlesOwnStart)
            {
                if (!consumePreparedStart)
                {
                    yield return PrepareSequenceStart(sequences[currentSequenceIndex]);

                    if (!gameRunning)
                        yield break;
                }

                ShowGameScreen(
                    sequences[currentSequenceIndex].ToString(),
                    currentSequenceIndex + 1,
                    sequences.Length,
                    livesLeft
                );
            }

            float levelStartTime = Time.time;

            bool sequenceControlsActivity =
                sequences[currentSequenceIndex] == PlatformSequenceType.Traffic ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Bridge ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Snake ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Maze ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Memory ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Arkanoid;

            if (!sequenceControlsActivity)
                sequenceActive = true;

            switch (sequences[currentSequenceIndex])
            {
                case PlatformSequenceType.Bridge:
                    yield return BridgeSequence(consumePreparedStart);
                    break;

                case PlatformSequenceType.Traffic:
                    yield return TrafficSequence(consumePreparedStart);
                    break;

                case PlatformSequenceType.Snake:
                    yield return SnakeSequence(consumePreparedStart);
                    break;

                case PlatformSequenceType.Maze:
                    yield return MazeSequence(consumePreparedStart);
                    break;

                case PlatformSequenceType.Memory:
                    yield return MemorySequence(consumePreparedStart);
                    break;

                case PlatformSequenceType.Arkanoid:
                    yield return ArkanoidSequence(consumePreparedStart);
                    break;
            }

            if (consumePreparedStart)
                usePreparedStartForCurrentSequence = false;

            if (!sequenceControlsActivity)
                sequenceActive = false;

            if (!gameRunning)
                yield break;

            SetAllSafe();

            float clearTime = Time.time - levelStartTime;

            bool sequenceWasStageControlled =
                sequences[currentSequenceIndex] == PlatformSequenceType.Traffic ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Bridge ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Snake ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Maze ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Memory ||
                sequences[currentSequenceIndex] == PlatformSequenceType.Arkanoid;

            if (!sequenceWasStageControlled)
            {
                yield return ShowMiniGameWinScreen(
                    sequences[currentSequenceIndex].ToString(),
                    clearTime,
                    GetNextSequenceName()
                );

                HideWindow();

                currentSequenceIndex++;

                if (currentSequenceIndex >= sequences.Length)
                {
                    WinGame();
                    yield break;
                }

                PrepareNextSequenceAfterWin();

                yield break;
            }
            else
            {
                yield return new WaitForSeconds(sequencePause);
            }
        }

        WinGame();
    }

    private IEnumerator PrepareSequenceStart(PlatformSequenceType sequence)
    {
        ResetTiles();

        PlatformTile startTile = null;
        hasPendingSequenceStart = false;

        if (sequence == PlatformSequenceType.Bridge)
        {
            PaintEdgesOnly();

            int startEdgeY = minY + Random.Range(0, edgeDepth);
            int startEdgeX = Random.Range(minX, maxX + 1);

            startTile = GetTile(startEdgeX, startEdgeY);
        }
        else if (sequence == PlatformSequenceType.Maze)
        {
            pendingSequenceStart = GetRandomEdgePosition();
            hasPendingSequenceStart = true;

            startTile = GetTile(pendingSequenceStart.x, pendingSequenceStart.y);
        }
        else
        {
            int x = Random.Range(minX + 2, maxX - 1);
            int y = Random.Range(minY + 2, maxY - 1);

            startTile = GetTile(x, y);
        }

        if (startTile == null)
            yield break;

        startTile.SetStart();

        waitingForSequenceStart = true;

        while (waitingForSequenceStart && gameRunning)
            yield return null;
    }

    private IEnumerator BridgeSequence(bool skipFirstStageStart)
    {
        yield return bridgeMiniGame.Run(
            miniGameGrid,
            edgeDepth,
            bridgeOptionsPerRow,
            () => finishReached,
            () => finishReached = false,
            () => gameRunning,
            () => currentPlayerTile,
            () => livesLeft,
            CheckPlayerDanger,
            ShowGameScreen,
            UpdateGameScreenTimes,
            ShowStageWinScreen,
            HideWindow,
            skipFirstStageStart,
            value => sequenceActive = value
        );
    }

    private IEnumerator TrafficSequence(bool skipFirstStageStart)
    {
        yield return trafficMiniGame.Run(
            miniGameGrid,
            edgeDepth,
            trafficTick,
            () => finishReached,
            () => finishReached = false,
            () => gameRunning,
            () => currentPlayerTile,
            () => livesLeft,
            CheckPlayerDanger,
            ShowGameScreen,
            UpdateGameScreenTimes,
            ShowStageWinScreen,
            HideWindow,
            skipFirstStageStart,
            value => sequenceActive = value
        );
    }

    private IEnumerator SnakeSequence(bool skipFirstStageStart)
    {
        yield return snakeMiniGame.Run(
            miniGameGrid,
            snakeWaves,
            snakeStepDelay,
            snakeLineWidth,
            snakeWavePause,
            snakeSpeedMultiplierPerWave,
            snakeLength,
            () => finishReached,
            () => finishReached = false,
            () => gameRunning,
            () => currentPlayerTile,
            () => livesLeft,
            CheckPlayerDanger,
            ShowGameScreen,
            UpdateGameScreenTimes,
            ShowStageWinScreen,
            HideWindow,
            skipFirstStageStart,
            value => sequenceActive = value
        );
    }

    private IEnumerator MazeSequence(bool skipFirstStageStart)
    {
        yield return mazeMiniGame.Run(
            miniGameGrid,
            edgeDepth,
            hasPendingSequenceStart,
            pendingSequenceStart,
            () => finishReached,
            () => finishReached = false,
            () => gameRunning,
            GetCurrentPlayerTileForMaze,
            () => livesLeft,
            CheckPlayerDanger,
            LoseGameFromMaze,
            ShowGameScreen,
            UpdateGameScreenTimes,
            ShowStageWinScreen,
            GetNextSequenceName,
            HideWindow,
            skipFirstStageStart,
            value => mazeActive = value
        );
    }

    private IEnumerator MemorySequence(bool skipFirstStageStart)
    {
        yield return memoryMiniGame.Run(
            miniGameGrid,
            () => gameRunning,
            () => currentPlayerTile,
            () => livesLeft,
            LoseGame,
            ShowGameScreen,
            UpdateGameScreenTimes,
            ShowStageWinScreen,
            GetNextSequenceName,
            HideWindow,
            skipFirstStageStart,
            value => sequenceActive = value,
            screenUI
        );
    }

    private IEnumerator ArkanoidSequence(bool skipFirstStageStart)
    {
        yield return arkanoidMiniGame.Run(
            miniGameGrid,
            () => gameRunning,
            () => currentPlayerTile,
            () => livesLeft,
            LoseGame,
            ShowGameScreen,
            UpdateGameScreenTimes,
            ShowStageWinScreen,
            GetNextSequenceName,
            HideWindow,
            skipFirstStageStart,
            value => sequenceActive = value
        );
    }

    private Vector2Int GetRandomEdgePosition()
    {
        return mazeMiniGame.GetRandomEdgePosition(miniGameGrid);
    }

    private IEnumerator WaitForFinish()
    {
        while (!finishReached && gameRunning)
        {
            CheckPlayerDanger();
            yield return null;
        }
    }

    private void LoseGame()
    {
        if (!gameRunning) return;

        StopAndCommitActiveStageTimer();

        StopPreCountdownTileAnimationImmediate();
        StopAllPathDecays();

        sequenceActive = false;
        mazeActive = false;
        gameRunning = false;
        entryState = MiniGameEntryState.Died;

        if (gameRoutine != null)
        {
            StopCoroutine(gameRoutine);
            gameRoutine = null;
        }

        livesLeft--;

        if (livesLeft > 0)
        {
            retryInProgress = true;
            StartCoroutine(RetryRoutine());
        }
        else
        {
            retryInProgress = false;
            fullGameOver = true;
            waitingForReset = true;
            StartCoroutine(FinalLoseRoutine());
        }
    }

    private IEnumerator RetryRoutine()
    {
        if (!freezeBoardOnRetry)
        {
            SetAllDanger();
        }
        else
        {
            PlatformTile failTile = failTileToShow != null
                ? failTileToShow
                : currentPlayerTile != null
                    ? currentPlayerTile
                    : lastPlayerTile;

            if (failTile != null && failTile.CurrentState != PlatformTileState.PlayerFail)
                failTile.SetPlayerFail();
        }

        screenUI?.ShowDied(livesLeft, maxLives);

        HideWindow();

        waitingForRetryAreaExit = true;

        while (waitingForRetryAreaExit)
            yield return null;

        // Gracz opuœci³ Area po œmierci.
        // Teraz nie startujemy gry od razu.
        // Przygotowujemy kolorow¹ planszê + blue tile do retry.
        freezeBoardOnRetry = false;

        ResetTiles();

        retryStartTile = CreateRetryStartTileForCurrentSequence();

        if (retryStartTile != null)
        {
            miniGamePainter.ApplyArenaIdlePatternAroundStart(retryStartTile);
        }

        screenUI?.ShowGame(
            GetCurrentSequenceName(),
            GetCurrentStageForScreen(),
            GetCurrentTotalStagesForScreen(),
            livesLeft,
            maxLives,
            defaultDifficultyName
        );

        waitingForRetryStartTile = true;
        retryInProgress = true;
        gameRunning = false;
        waitingForReset = false;
        failTileToShow = null;
    }

    private IEnumerator FinalLoseRoutine()
    {
        SetAllDanger();
        RestorePlayerJump();

        if (screenUI != null)
        {
            yield return screenUI.ShowGameOver(
                finishedGames: currentSequenceIndex,
                perfectGames: 0,
                difficulty: "EASY",
                totalTime: totalActiveGameTime
            );
        }
    }

    private void ResetFullGame()
    {
        RestorePlayerJump();
        StopAllPathDecays();
        StopPreCountdownTileAnimationImmediate();
  

        if (introPrepareRoutine != null)
        {
            StopCoroutine(introPrepareRoutine);
            introPrepareRoutine = null;
        }

        entryState = MiniGameEntryState.Idle;
        introStartTile = null;

        fullGameOver = false;
        waitingForReset = false;
        waitingForSequenceStart = false;
        countdownRunning = false;
        sequenceActive = false;
        retryInProgress = false;
        gameRunning = false;

        waitingForRetryAreaExit = false;
        finishReached = false;
        totalActiveGameTime = 0f;
        ResetActiveStageTimer();
        freezeBoardOnRetry = false;
        mazeActive = false;
        idlePatternGenerated = false;

        currentSequenceIndex = 0;
        livesLeft = maxLives;

        currentPlayerTile = null;
        lastPlayerTile = null;

        trafficMiniGame?.ResetProgress();
        bridgeMiniGame?.ResetProgress();
        mazeMiniGame?.ResetProgress();
        snakeMiniGame?.ResetProgress();
        memoryMiniGame?.ResetProgress();
        arkanoidMiniGame?.ResetProgress();

        usePreparedStartForCurrentSequence = false;
        introStartGridPos = default;
        hasPendingSequenceStart = false;
        introStartTile = null;
        waitingForRetryStartTile = false;
        retryStartTile = null;
        failTileToShow = null;

        waitingForNextSequenceStartTile = false;
        nextSequenceStartTile = null;

        if (gameRoutine != null)
        {
            StopCoroutine(gameRoutine);
            gameRoutine = null;
        }

        HideWindow();
        ResetTiles();
        screenUI?.ShowIdle();
    }

    private void WinGame()
    {
        RestorePlayerJump();
        sequenceActive = false;
        gameRunning = false;
        waitingForReset = true;
        entryState = MiniGameEntryState.GameOver;
        StartCoroutine(WinRoutine());
    }

    private void RestorePlayerJump()
    {
        if (playerMovement)
            playerMovement.jumpHeight = savedJumpHeight;
    }

    private IEnumerator WinRoutine()
    {
        ResetTiles();
        yield return ShowDialog(winMessage);
    }

    private void CheckPlayerDanger()
    {
        if (IsCurrentSequence(PlatformSequenceType.Maze))
        {
            PlatformTile centerTile = GetPlayerCenterTile();

            if (centerTile == null)
                return;

            currentPlayerTile = centerTile;
            lastPlayerTile = centerTile;

            if (centerTile.IsFinish)
            {
                finishReached = true;
                return;
            }

            if (centerTile.IsDanger)
            {
                MarkFailTile(centerTile);
                LoseGame();
            }

            return;
        }

        if (currentPlayerTile != null && currentPlayerTile.IsDanger)
            HandleDangerFail();
    }

    public void OnPlayerEnteredArea()
    {
        if (retryInProgress)
            return;

        if (gameRunning)
            return;

        if (waitingForReset || fullGameOver)
            return;

        if (entryState == MiniGameEntryState.Welcome ||
            entryState == MiniGameEntryState.Preparing ||
            entryState == MiniGameEntryState.ReadyToStart)
            return;

        if (idlePatternGenerated)
            return;

        ResetTiles();

        CreateIntroStartTileForCurrentSequence();

        screenUI?.ShowWelcomeMessage();

        entryState = MiniGameEntryState.Welcome;
        idlePatternGenerated = true;
    }

    public void OnPlayerExitedArea()
    {
        if (sequenceActive)
        {
            HandleAreaExitFail();
            return;
        }

        if (waitingForRetryAreaExit)
        {
            waitingForRetryAreaExit = false;
            return;
        }

        // WA¯NE:
        // Przed w³aœciw¹ gr¹ wyjœcie z Area nie resetuje ju¿ ekranu,
        // planszy, prepare ani countdownu.
        // Gracz mo¿e odejœæ i wróciæ, a stan ma zostaæ.
        if (entryState == MiniGameEntryState.Welcome ||
            entryState == MiniGameEntryState.Preparing ||
            entryState == MiniGameEntryState.ReadyToStart)
        {
            return;
        }

        if (waitingForReset || fullGameOver)
        {
            ResetFullGame();
        }
    }

    private void BuildGrid()
    {
        miniGameGrid.BuildGrid();
    }

    private PlatformTile GetTile(int x, int y)
    {
        return miniGameGrid.GetTile(x, y);
    }

    private void ResetTiles()
    {
        miniGameGrid.ResetTiles();
    }

    private void SetAllDanger()
    {
        miniGameGrid.SetAllDanger();
    }

    private void SetAllSafe()
    {
        miniGameGrid.SetAllSafe();
    }

    private void PaintEdgesOnly()
    {
        miniGameGrid.PaintEdgesOnly(edgeDepth);
    }

    private bool IsCurrentSequence(PlatformSequenceType type)
    {
        return currentSequenceIndex >= 0 &&
               currentSequenceIndex < sequences.Length &&
               sequences[currentSequenceIndex] == type;
    }

    private IEnumerator ShowDialog(string message)
    {
        yield break;
    }

    private void HideWindow()
    {
    }

    private IEnumerator CountdownRoutine()
    {
        countdownRunning = true;

        PlatformTile playerTileDuringCountdown = currentPlayerTile;

        for (int i = globalCountdownFrom; i >= 0; i--)
        {
            screenUI?.ShowWelcomeCountdown(i.ToString());

            yield return miniGameUI.CountdownRoutineStep(
                i,
                playerTileDuringCountdown
            );
        }

        screenUI?.ShowWelcomeStart();

        countdownRunning = false;
    }

    private void HandleDangerFail()
    {
        if (IsCurrentSequence(PlatformSequenceType.Snake) ||
            IsCurrentSequence(PlatformSequenceType.Maze) ||
            IsCurrentSequence(PlatformSequenceType.Memory))
        {
            MarkFailTile(currentPlayerTile);
        }

        LoseGame();
    }

    private void HandleAreaExitFail()
    {
        if (!sequenceActive)
            return;

        if (IsCurrentSequence(PlatformSequenceType.Snake) ||
            IsCurrentSequence(PlatformSequenceType.Maze))
        {
            MarkFailTile(currentPlayerTile != null ? currentPlayerTile : lastPlayerTile);
        }

        LoseGame();
    }

    private void StopAllPathDecays()
    {
        foreach (var pair in decayCoroutines)
        {
            if (pair.Value != null)
                StopCoroutine(pair.Value);
        }

        decayCoroutines.Clear();
    }

    private void StartPreCountdownTileAnimation()
    {
        StopPreCountdownTileAnimationImmediate();

        preCountdownAnimationRunning = true;
        countdownRunning = true;

        preCountdownAnimationRoutine = StartCoroutine(
            miniGameUI.PlayPreCountdownTileAnimationLoop(
                () => preCountdownAnimationRunning
            )
        );
    }

    private IEnumerator StopPreCountdownTileAnimation()
    {
        preCountdownAnimationRunning = false;

        if (preCountdownAnimationRoutine != null)
        {
            yield return preCountdownAnimationRoutine;
            preCountdownAnimationRoutine = null;
        }

        countdownRunning = false;
    }

    private void StopPreCountdownTileAnimationImmediate()
    {
        preCountdownAnimationRunning = false;

        if (preCountdownAnimationRoutine != null)
        {
            StopCoroutine(preCountdownAnimationRoutine);
            preCountdownAnimationRoutine = null;
        }

        countdownRunning = false;
    }

    private bool CurrentSequenceUsesPathDecay()
    {
        return !IsCurrentSequence(PlatformSequenceType.Bridge) &&
               !IsCurrentSequence(PlatformSequenceType.Snake) &&
               !IsCurrentSequence(PlatformSequenceType.Maze) &&
               !IsCurrentSequence(PlatformSequenceType.Memory) &&
               !IsCurrentSequence(PlatformSequenceType.Arkanoid);
    }

    private void ShowGameScreen(
        string gameName,
        int stage,
        int totalStages,
        int lives)
    {
        screenUI?.ShowGame(
            gameName,
            stage,
            totalStages,
            lives,
            maxLives
        );

        StartActiveStageTimer();
    }

    private void UpdateGameScreenTimes(float currentStageTime)
    {
        screenUI?.UpdateTimes(
            currentStageTime,
            totalActiveGameTime + currentStageTime
        );
    }

    private IEnumerator ShowStageWinScreen(
        string gameName,
        int stage,
        int totalStages,
        float stageTime,
        string nextName)
    {
        float finalStageTime = StopAndCommitActiveStageTimer();

        if (finalStageTime <= 0.01f)
            finalStageTime = stageTime;

        if (screenUI == null)
            yield break;

        if (stage < totalStages)
        {
            yield return screenUI.ShowStageWin(
                gameName,
                stage,
                totalStages,
                finalStageTime,
                livesLeft,
                maxLives
            );
        }
        else
        {
            string realNextName = nextName;

            if (string.IsNullOrWhiteSpace(realNextName) ||
                realNextName.Equals("NEXT MINIGAME", System.StringComparison.OrdinalIgnoreCase))
            {
                realNextName = GetNextSequenceName();
            }

            yield return screenUI.ShowMiniGameWin(
                gameName,
                finalStageTime,
                livesLeft,
                maxLives,
                realNextName
            );
        }
    }

    private string GetNextSequenceName()
    {
        int nextIndex = currentSequenceIndex + 1;

        if (nextIndex >= sequences.Length)
            return "FINISH";

        return sequences[nextIndex].ToString();
    }
    private IEnumerator ShowMiniGameWinScreen(
    string gameName,
    float miniGameTime,
    string nextName)
    {
        totalActiveGameTime += miniGameTime;

        if (screenUI != null)
        {
            yield return screenUI.ShowMiniGameWin(
                gameName,
                miniGameTime,
                livesLeft,
                maxLives,
                nextName
            );
        }
    }

    private void StartActiveStageTimer()
    {
        currentStageElapsed = 0f;
        activeStageStartTime = Time.time;
        activeStageTimerRunning = true;

        screenUI?.UpdateTimes(
            0f,
            totalActiveGameTime
        );
    }

    private float StopAndCommitActiveStageTimer()
    {
        if (!activeStageTimerRunning)
            return currentStageElapsed;

        currentStageElapsed = Time.time - activeStageStartTime;
        activeStageTimerRunning = false;

        totalActiveGameTime += currentStageElapsed;

        screenUI?.UpdateTimes(
            currentStageElapsed,
            totalActiveGameTime
        );

        return currentStageElapsed;
    }

    private void ResetActiveStageTimer()
    {
        activeStageTimerRunning = false;
        activeStageStartTime = 0f;
        currentStageElapsed = 0f;
    }

    private string GetCurrentSequenceName()
    {
        if (currentSequenceIndex < 0 || currentSequenceIndex >= sequences.Length)
            return "UNKNOWN";

        return sequences[currentSequenceIndex].ToString();
    }

    private int GetCurrentStageForScreen()
    {
        // Na razie ogólny stage/sekwencja.
        // Dla Bridge/Traffic potem mo¿emy zwracaæ ich wewnêtrzny stage.
        return currentSequenceIndex + 1;
    }

    private int GetCurrentTotalStagesForScreen()
    {
        return sequences != null ? sequences.Length : 0;
    }

    private PlatformTile CreateIntroStartTileForCurrentSequence()
    {
        introStartTile = CreateStartTileForSequence(sequences[currentSequenceIndex]);

        if (introStartTile != null)
            miniGamePainter.ApplyArenaIdlePatternAroundStart(introStartTile);

        return introStartTile;
    }

    private PlatformTile CreateRetryStartTileForCurrentSequence()
    {
        if (currentSequenceIndex < 0 || currentSequenceIndex >= sequences.Length)
            return miniGamePainter.SetStartTile();

        retryStartTile = CreateStartTileForSequence(sequences[currentSequenceIndex]);
        return retryStartTile;
    }

    private void PrepareNextSequenceAfterWin()
    {
        gameRunning = false;
        sequenceActive = false;
        waitingForSequenceStart = false;
        finishReached = false;

        ResetActiveStageTimer();
        ResetTiles();

        nextSequenceStartTile = CreateStartTileForSequence(sequences[currentSequenceIndex]);

        if (nextSequenceStartTile != null)
        {
            miniGamePainter.ApplyArenaIdlePatternAroundStart(nextSequenceStartTile);
        }

        screenUI?.ShowGame(
            GetCurrentSequenceName(),
            GetCurrentStageForScreen(),
            GetCurrentTotalStagesForScreen(),
            livesLeft,
            maxLives,
            defaultDifficultyName
        );

        waitingForNextSequenceStartTile = true;
        entryState = MiniGameEntryState.ReadyToStart;
    }

    private PlatformTile CreateStartTileForSequence(PlatformSequenceType sequence)
    {
        switch (sequence)
        {
            case PlatformSequenceType.Bridge:
                {
                    int startY = minY + Random.Range(0, edgeDepth);
                    int startX = Random.Range(minX + 1, maxX);

                    Vector2Int pos = new Vector2Int(startX, startY);

                    pendingSequenceStart = pos;
                    hasPendingSequenceStart = true;

                    return GetTile(pos.x, pos.y);
                }

            case PlatformSequenceType.Traffic:
                {
                    Vector2Int pos = trafficMiniGame != null
                        ? trafficMiniGame.GetCurrentStageStartPosition(miniGameGrid, edgeDepth)
                        : new Vector2Int(
                            Random.Range(minX + 1, maxX),
                            minY + Random.Range(0, edgeDepth)
                        );

                    pendingSequenceStart = pos;
                    hasPendingSequenceStart = true;

                    return GetTile(pos.x, pos.y);
                }

            case PlatformSequenceType.Snake:
                {
                    int x = Random.Range(minX + 2, maxX - 1);
                    int y = Random.Range(minY + 2, maxY - 1);

                    Vector2Int pos = new Vector2Int(x, y);

                    pendingSequenceStart = pos;
                    hasPendingSequenceStart = true;

                    return GetTile(pos.x, pos.y);
                }

            case PlatformSequenceType.Memory:
                {
                    int centerX = (minX + maxX) / 2;
                    int centerY = (minY + maxY) / 2;

                    Vector2Int pos = new Vector2Int(centerX, centerY);

                    pendingSequenceStart = pos;
                    hasPendingSequenceStart = true;

                    return GetTile(pos.x, pos.y);
                }

            case PlatformSequenceType.Arkanoid:
                {
                    int centerX = (minX + maxX) / 2;

                    // Platforma Arkanoid jest jedn¹ kratkê od dolnej œciany.
                    // Dla gridu od 0 oznacza to y = minY + 1.
                    int startY = minY + 1;

                    Vector2Int pos = new Vector2Int(centerX, startY);

                    pendingSequenceStart = pos;
                    hasPendingSequenceStart = true;

                    return GetTile(pos.x, pos.y);
                }

            case PlatformSequenceType.Maze:
            default:
                {
                    Vector2Int pos = GetRandomEdgePosition();

                    pendingSequenceStart = pos;
                    hasPendingSequenceStart = true;

                    return GetTile(pos.x, pos.y);
                }
        }
    }
    private void StartMazeTileCollapse(PlatformTile tile)
    {
        if (tile == null)
            return;

        if (decayCoroutines.ContainsKey(tile))
            return;

        decayCoroutines[tile] = StartCoroutine(CoMazeTileCollapse(tile));
    }

    private IEnumerator CoMazeTileCollapse(PlatformTile tile)
    {
        if (tile == null)
            yield break;

        if (tile.CurrentState != PlatformTileState.Safe)
            yield break;

        tile.SetWarning();

        yield return new WaitForSeconds(mazeTileCollapseDelay);

        if (tile != null && tile.CurrentState == PlatformTileState.Warning)
        {
            tile.SetDanger();

            // Nie zabijamy tutaj gracza.
            // Collapse tylko zmienia kafel na czerwony.
            // Skucha ma byæ wykryta przez CheckPlayerDanger(),
            // czyli tylko gdy aktualny tile gracza jest Danger/Pendulum.
        }

        if (tile != null)
            decayCoroutines.Remove(tile);
    }

    private void MarkFailTile(PlatformTile tile)
    {
        failTileToShow = tile != null
            ? tile
            : currentPlayerTile != null
                ? currentPlayerTile
                : lastPlayerTile;

        if (failTileToShow != null)
            failTileToShow.SetPlayerFail();

        freezeBoardOnRetry = true;
    }
    private void LoseGameFromMaze()
    {
        PlatformTile centerTile = GetPlayerCenterTile();

        MarkFailTile(centerTile != null ? centerTile : lastPlayerTile);
        LoseGame();
    }

    private PlatformTile GetPlayerCenterTile()
    {
        Transform playerTransform = playerMovement != null
            ? playerMovement.transform
            : null;

        if (playerTransform == null)
            return currentPlayerTile;

        Vector3 playerPosition = playerTransform.position;

        CharacterController characterController =
            playerMovement.GetComponent<CharacterController>();

        if (characterController != null)
            playerPosition = characterController.bounds.center;

        foreach (PlatformTile tile in Tiles)
        {
            if (tile == null)
                continue;

            if (tile.ContainsWorldXZ(playerPosition, mazeTileCenterPadding))
                return tile;
        }

        return null;
    }

    private PlatformTile GetCurrentPlayerTileForMaze()
    {
        PlatformTile centerTile = GetPlayerCenterTile();

        if (centerTile != null)
        {
            currentPlayerTile = centerTile;
            lastPlayerTile = centerTile;

            if (centerTile.IsFinish)
                finishReached = true;

            return centerTile;
        }

        return currentPlayerTile;
    }
}