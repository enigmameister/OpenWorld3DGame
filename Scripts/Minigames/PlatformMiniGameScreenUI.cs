using System.Collections;
using TMPro;
using UnityEngine;

public class PlatformMiniGameScreenUI : MonoBehaviour
{
    [Header("Screens")]
    [SerializeField] private GameObject planeIdle;
    [SerializeField] private GameObject planeWelcome;
    [SerializeField] private GameObject planeGame;
    [SerializeField] private GameObject planeWinStage;
    [SerializeField] private GameObject planeWinMiniGame;
    [SerializeField] private GameObject planeDied;
    [SerializeField] private GameObject planeGameOver;

    [Header("Game Panel Roots")]
    [SerializeField] private GameObject gamePanelRoot;

    [Header("Welcome Screen Objects")]
    [SerializeField] private GameObject welcomeMessageRoot;
    [SerializeField] private GameObject welcomePrepareRoot;
    [SerializeField] private GameObject welcomeCountdownRoot;

    [Header("Welcome Countdown Sizes")]
    [SerializeField] private float welcomeCountdownNumberSize = 70f;
    [SerializeField] private float welcomeCountdownStartSize = 30f;

    [Header("Welcome Countdown")]
    [SerializeField] private TextMeshPro welcomeCountdownText;

    [Header("Welcome Message")]
    [SerializeField] private TextMeshPro welcomeText;
    [SerializeField] private float welcomeTypeSpeed = 0.015f;

    [Header("Welcome Prepare")]
    [SerializeField] private TextMeshPro prepareNextGameText;
    [SerializeField] private TextMeshPro prepareDifficultyText;

    [Header("Game Screen")]
    [SerializeField] private TextMeshPro currentGameText;
    [SerializeField] private TextMeshPro stageText;
    [SerializeField] private TextMeshPro livesText;
    [SerializeField] private TextMeshPro difficultyText;
    [SerializeField] private TextMeshPro currentTimeText;
    [SerializeField] private TextMeshPro totalTimeText;

    [Header("Memory Preview Panel")]
    [SerializeField] private GameObject memoryRoot;
    [SerializeField] private TextMeshPro memoryMessageText;
    [SerializeField] private SpriteRenderer memorySequenceRenderer;

    [Header("Memory Direction Sprites")]
    [SerializeField] private Sprite memoryArrowUp;
    [SerializeField] private Sprite memoryArrowDown;
    [SerializeField] private Sprite memoryArrowLeft;
    [SerializeField] private Sprite memoryArrowRight;

    [Header("Current Game Arrows")]
    [SerializeField] private GameObject snakeCurrentArrow;
    [SerializeField] private GameObject bridgeCurrentArrow;
    [SerializeField] private GameObject trafficCurrentArrow;
    [SerializeField] private GameObject mazeCurrentArrow;
    [SerializeField] private GameObject memoryCurrentArrow;
    [SerializeField] private GameObject arkanoidCurrentArrow;
    [SerializeField] private GameObject avoidingCurrentArrow;

    [Header("Win Stage Screen")]
    [SerializeField] private TextMeshPro stageWinHeaderText;
    [SerializeField] private TextMeshPro stageWinTimeText;
    [SerializeField] private TextMeshPro stageWinLivesText;

    [Header("Win Mini Game Screen")]
    [SerializeField] private TextMeshPro miniGameWinHeaderText;
    [SerializeField] private TextMeshPro miniGameWinTimeText;
    [SerializeField] private TextMeshPro miniGameWinLivesText;
    [SerializeField] private TextMeshPro miniGameNextText;

    [Header("Died Screen")]
    [SerializeField] private TextMeshPro diedLivesText;

    [Header("Game Over Screen")]
    [SerializeField] private TextMeshPro gameOverFinishGamesText;
    [SerializeField] private TextMeshPro gameOverPerfectGamesText;
    [SerializeField] private TextMeshPro gameOverDifficultyText;
    [SerializeField] private TextMeshPro gameOverTotalTimeText;

    [Header("Timing")]
    [SerializeField] private float stageWinDuration = 3f;
    [SerializeField] private float gameOverDuration = 4f;

    private Coroutine welcomeTypeRoutine;

    public void ShowIdle()
    {
        ShowOnly(planeIdle);
        HideAllWelcomeParts();
        HideAllCurrentGameArrows();
        HideMemoryPanel();
    }

    public void ShowWelcomeMessage()
    {
        ShowOnly(planeWelcome);
        ShowOnlyWelcomePart(welcomeMessageRoot);
        HideAllCurrentGameArrows();
        HideMemoryPanel();

        if (welcomeTypeRoutine != null)
            StopCoroutine(welcomeTypeRoutine);

        if (welcomeText)
            welcomeTypeRoutine = StartCoroutine(TypeText(welcomeText));
    }

    public void ShowWelcomePrepare(string nextGameName, string difficulty)
    {
        ShowOnly(planeWelcome);
        ShowOnlyWelcomePart(welcomePrepareRoot);
        HideAllCurrentGameArrows();
        HideMemoryPanel();

        StopWelcomeTyping();

        if (prepareNextGameText)
        {
            prepareNextGameText.text = string.IsNullOrWhiteSpace(nextGameName)
                ? "UNKNOWN"
                : nextGameName.ToUpper();
        }

        if (prepareDifficultyText)
        {
            prepareDifficultyText.text = string.IsNullOrWhiteSpace(difficulty)
                ? "EASY"
                : difficulty.ToUpper();
        }
    }

    public void ShowWelcomeCountdown(string value)
    {
        ShowOnly(planeWelcome);
        ShowOnlyWelcomePart(welcomeCountdownRoot);
        HideAllCurrentGameArrows();
        HideMemoryPanel();

        StopWelcomeTyping();

        if (welcomeCountdownText)
        {
            welcomeCountdownText.gameObject.SetActive(true);
            welcomeCountdownText.text = value;

            if (string.Equals(value, "START", System.StringComparison.OrdinalIgnoreCase))
                welcomeCountdownText.fontSize = welcomeCountdownStartSize;
            else
                welcomeCountdownText.fontSize = welcomeCountdownNumberSize;
        }
    }

    public void ShowWelcomeCountdownNumber(int number)
    {
        ShowWelcomeCountdown(number.ToString());
    }

    public void ShowWelcomeStart()
    {
        ShowWelcomeCountdown("START");
    }

    public void HideWelcomeCountdown()
    {
        if (welcomeCountdownText)
            welcomeCountdownText.gameObject.SetActive(false);
    }

    public void ShowWelcomeWarning(string message)
    {
        ShowOnly(planeWelcome);
        HideAllCurrentGameArrows();
        HideMemoryPanel();

        StopWelcomeTyping();
    }

    public void ShowGame(
        string gameName,
        int stage,
        int totalStages,
        int livesLeft,
        int maxLives,
        string difficulty = "EASY")
    {
        ShowOnly(planeGame);
        HideAllWelcomeParts();

        if (gamePanelRoot)
            gamePanelRoot.SetActive(true);

        HideMemoryPanel();

        SetCurrentGameArrow(gameName);

        if (currentGameText)
            currentGameText.text = gameName.ToUpper();

        if (stageText)
            stageText.text = $"{stage}";

        if (livesText)
            livesText.text = BuildLivesText(livesLeft, maxLives);

        if (difficultyText)
            difficultyText.text = difficulty.ToUpper();

        UpdateTimes(0f, 0f);
    }

    public void UpdateGameInfo(
        string gameName,
        int stage,
        int totalStages,
        int livesLeft,
        int maxLives,
        string difficulty = "EASY")
    {
        SetCurrentGameArrow(gameName);

        if (currentGameText)
            currentGameText.text = gameName.ToUpper();

        if (stageText)
            stageText.text = $"{stage}";

        if (livesText)
            livesText.text = BuildLivesText(livesLeft, maxLives);

        if (difficultyText)
            difficultyText.text = difficulty.ToUpper();
    }

    public void UpdateTimes(float currentStageTime, float totalTime)
    {
        if (currentTimeText)
            currentTimeText.text = FormatTime(currentStageTime);

        if (totalTimeText)
            totalTimeText.text = FormatTime(totalTime);
    }

    public IEnumerator ShowStageWin(
        string gameName,
        int stage,
        int totalStages,
        float stageTime,
        int livesLeft,
        int maxLives)
    {
        ShowOnly(planeWinStage);
        HideAllWelcomeParts();
        HideAllCurrentGameArrows();
        HideMemoryPanel();

        if (stageWinHeaderText)
        {
            stageWinHeaderText.text =
                $"SUCCESSFUL! YOU PASSED\n{gameName.ToUpper()} STAGE {stage}/{totalStages}";
        }

        if (stageWinTimeText)
            stageWinTimeText.text = FormatTime(stageTime);

        if (stageWinLivesText)
            stageWinLivesText.text = BuildLivesText(livesLeft, maxLives);

        yield return new WaitForSeconds(stageWinDuration);
    }

    public IEnumerator ShowMiniGameWin(
        string gameName,
        float miniGameTime,
        int livesLeft,
        int maxLives,
        string nextName)
    {
        ShowOnly(planeWinMiniGame);
        HideAllWelcomeParts();
        HideAllCurrentGameArrows();
        HideMemoryPanel();

        if (miniGameWinHeaderText)
        {
            miniGameWinHeaderText.text =
                $"SUCCESSFUL! YOU PASSED\n{gameName.ToUpper()} MINIGAME,\nPREPARE FOR ANOTHER ONE !";
        }

        if (miniGameWinTimeText)
            miniGameWinTimeText.text = FormatTime(miniGameTime);

        if (miniGameWinLivesText)
            miniGameWinLivesText.text = BuildLivesText(livesLeft, maxLives);

        if (miniGameNextText)
            miniGameNextText.text = string.IsNullOrWhiteSpace(nextName)
                ? "FINISH"
                : nextName.ToUpper();

        yield return new WaitForSeconds(stageWinDuration);
    }

    public void ShowDied(int livesLeft, int maxLives)
    {
        ShowOnly(planeDied);
        HideAllWelcomeParts();
        HideAllCurrentGameArrows();
        HideMemoryPanel();

        if (diedLivesText)
            diedLivesText.text = BuildLivesText(livesLeft, maxLives);
    }

    public IEnumerator ShowGameOver(
        int finishedGames,
        int perfectGames,
        string difficulty,
        float totalTime)
    {
        ShowOnly(planeGameOver);
        HideAllWelcomeParts();
        HideAllCurrentGameArrows();
        HideMemoryPanel();

        if (gameOverFinishGamesText)
            gameOverFinishGamesText.text = $"{finishedGames}";

        if (gameOverPerfectGamesText)
            gameOverPerfectGamesText.text = $"{perfectGames}";

        if (gameOverDifficultyText)
            gameOverDifficultyText.text = difficulty.ToUpper();

        if (gameOverTotalTimeText)
            gameOverTotalTimeText.text = FormatTimeLong(totalTime);

        yield return new WaitForSeconds(gameOverDuration);
    }

    // =========================
    // MEMORY PANEL API
    // =========================

    public void ShowNormalGamePanel()
    {
        ShowOnly(planeGame);
        HideAllWelcomeParts();

        if (gamePanelRoot)
            gamePanelRoot.SetActive(true);

        HideMemoryPanel();
    }

    public void ShowMemoryPanel()
    {
        ShowOnly(planeGame);
        HideAllWelcomeParts();

        if (gamePanelRoot)
            gamePanelRoot.SetActive(false);

        if (memoryRoot)
            memoryRoot.SetActive(true);
    }

    public void HideMemoryPanel()
    {
        if (memoryRoot)
            memoryRoot.SetActive(false);

        if (memoryMessageText)
            memoryMessageText.text = "";

        HideMemorySequenceSprite();
    }

    public IEnumerator ShowMemoryDirectionSprite(string direction, float duration)
    {
        ShowMemoryPanel();

        if (memoryMessageText)
            memoryMessageText.text = "";

        if (!memorySequenceRenderer)
            yield break;

        memorySequenceRenderer.sprite = GetMemoryDirectionSprite(direction);
        memorySequenceRenderer.enabled = memorySequenceRenderer.sprite != null;

        yield return new WaitForSeconds(duration);

        HideMemorySequenceSprite();
    }

    public void HideMemorySequenceSprite()
    {
        if (!memorySequenceRenderer)
            return;

        memorySequenceRenderer.sprite = null;
        memorySequenceRenderer.enabled = false;
    }

    private Sprite GetMemoryDirectionSprite(string direction)
    {
        if (string.IsNullOrWhiteSpace(direction))
            return null;

        switch (direction.ToUpperInvariant())
        {
            case "UP":
                return memoryArrowUp;

            case "DOWN":
                return memoryArrowDown;

            case "LEFT":
                return memoryArrowLeft;

            case "RIGHT":
                return memoryArrowRight;

            default:
                return null;
        }
    }

    // =========================
    // INTERNAL HELPERS
    // =========================

    private IEnumerator TypeText(TextMeshPro text)
    {
        text.ForceMeshUpdate();

        int total = text.textInfo.characterCount;
        text.maxVisibleCharacters = 0;

        for (int i = 0; i <= total; i++)
        {
            text.maxVisibleCharacters = i;
            yield return new WaitForSeconds(welcomeTypeSpeed);
        }
    }

    private void StopWelcomeTyping()
    {
        if (welcomeTypeRoutine != null)
        {
            StopCoroutine(welcomeTypeRoutine);
            welcomeTypeRoutine = null;
        }

        if (welcomeText)
            welcomeText.maxVisibleCharacters = welcomeText.text.Length;
    }

    private void ShowOnly(GameObject target)
    {
        StopWelcomeTyping();

        if (planeIdle) planeIdle.SetActive(false);
        if (planeWelcome) planeWelcome.SetActive(false);
        if (planeGame) planeGame.SetActive(false);
        if (planeWinStage) planeWinStage.SetActive(false);
        if (planeWinMiniGame) planeWinMiniGame.SetActive(false);
        if (planeDied) planeDied.SetActive(false);
        if (planeGameOver) planeGameOver.SetActive(false);

        if (target)
            target.SetActive(true);

        if (target != planeGame)
        {
            if (gamePanelRoot)
                gamePanelRoot.SetActive(false);

            HideMemoryPanel();
        }
    }

    private void HideAllWelcomeParts()
    {
        if (welcomeMessageRoot) welcomeMessageRoot.SetActive(false);
        if (welcomePrepareRoot) welcomePrepareRoot.SetActive(false);
        if (welcomeCountdownRoot) welcomeCountdownRoot.SetActive(false);
    }

    private void ShowOnlyWelcomePart(GameObject target)
    {
        HideAllWelcomeParts();

        if (target)
            target.SetActive(true);
    }

    private void HideAllCurrentGameArrows()
    {
        if (snakeCurrentArrow) snakeCurrentArrow.SetActive(false);
        if (bridgeCurrentArrow) bridgeCurrentArrow.SetActive(false);
        if (trafficCurrentArrow) trafficCurrentArrow.SetActive(false);
        if (mazeCurrentArrow) mazeCurrentArrow.SetActive(false);
        if (memoryCurrentArrow) memoryCurrentArrow.SetActive(false);
        if (arkanoidCurrentArrow) arkanoidCurrentArrow.SetActive(false);
        if (avoidingCurrentArrow) avoidingCurrentArrow.SetActive(false);
    }

    private void SetCurrentGameArrow(string gameName)
    {
        HideAllCurrentGameArrows();

        if (string.IsNullOrWhiteSpace(gameName))
            return;

        string key = gameName.ToLowerInvariant().Replace(" ", "");

        switch (key)
        {
            case "snake":
                if (snakeCurrentArrow) snakeCurrentArrow.SetActive(true);
                break;

            case "bridge":
                if (bridgeCurrentArrow) bridgeCurrentArrow.SetActive(true);
                break;

            case "traffic":
                if (trafficCurrentArrow) trafficCurrentArrow.SetActive(true);
                break;

            case "maze":
                if (mazeCurrentArrow) mazeCurrentArrow.SetActive(true);
                break;

            case "memory":
                if (memoryCurrentArrow) memoryCurrentArrow.SetActive(true);
                break;

            case "arkanoid":
            case "arcanoid":
                if (arkanoidCurrentArrow) arkanoidCurrentArrow.SetActive(true);
                break;

            case "avoiding":
                if (avoidingCurrentArrow) avoidingCurrentArrow.SetActive(true);
                break;
        }
    }

    private string BuildLivesText(int livesLeft, int maxLives)
    {
        string result = "";

        for (int i = 0; i < maxLives; i++)
        {
            if (i < livesLeft)
                result += "<color=#ff3030>●</color> ";
            else
                result += "<color=#ffffff>●</color> ";
        }

        return result.TrimEnd();
    }

    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(seconds);
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;

        return $"{minutes:00}:{secs:00}";
    }

    private string FormatTimeLong(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(seconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int secs = totalSeconds % 60;

        return $"{hours:00}:{minutes:00}:{secs:00}";
    }
}