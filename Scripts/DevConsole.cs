using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public class DevConsole : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private CanvasGroup consoleCanvasGroup;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.15f;

    private Coroutine _fadeRoutine;
    private bool _isTransitioning;

    public GameObject consolePanel;
    public TMP_InputField inputField;
    public TextMeshProUGUI logText;

    private bool isConsoleOpen = false;
    public static bool IsOpen { get; private set; }

    // mapa komend
    private Dictionary<string, Action<string[]>> _commands;

    // HISTORIA
    private readonly List<string> history = new List<string>();
    private int historyIndex = -1; // -1 = poza historią (pusty wiersz)

    private bool _prevMoveLocked;
    private bool _prevLookLocked;
    private CursorLockMode _prevCursorLock;
    private bool _prevCursorVisible;
    private bool _cachedPrev;

    void Awake()
    {
        if (consoleCanvasGroup != null)
        {
            consoleCanvasGroup.alpha = 0f;
            consoleCanvasGroup.interactable = false;
            consoleCanvasGroup.blocksRaycasts = false;
        }
        consolePanel.SetActive(false);

        _commands = new Dictionary<string, Action<string[]>>(StringComparer.OrdinalIgnoreCase)
        {
            // dostępne ZAWSZE:
            ["FRAUD"] = args => {
                // tylko wielkie litery odblokowują
                if (args.Length == 1 && args[0] == "FRAUD")
                {
                    CheatState.UnlockCheats();
                    PrintResult("Cheating Available");
                }
                // wpisany w innej formie – nic nie rób (wymóg)
            },

            // pauza / wznowienie: działają nawet bez FRAUD
            ["PAUSE"] = args => { Time.timeScale = 0f; PrintResult("Game Paused"); },
            ["RESUME"] = args => { Time.timeScale = 1f; PrintResult("Game Resumed"); },
        };

        // komendy wymagające FRAUD:
        _commands["RESET"] = RequireFraud(args => { CheatState.ResetAll(); ApplyBoltToMovement(); PrintResult("Cheats reset"); });
        _commands["SAIYAN"] = RequireFraud(args => { CheatState.Invincible = true; PrintResult("Invincibility ON"); });
        _commands["CO2"] = RequireFraud(args => { CheatState.InfiniteStamina = true; PrintResult("Infinite Stamina ON"); });
        _commands["ELWRAY"] = RequireFraud(args => { CheatState.InfiniteAmmo = true; PrintResult("Infinite Ammo ON"); });
        _commands["ALLIANCE"] = RequireFraud(args => { CheatState.Alliance = true; PrintResult("Alliance ON"); });

        _commands["BOLT"] = RequireFraud(args =>
        {
            if (args.Length >= 2 && float.TryParse(args[1], System.Globalization.NumberStyles.Float,
                                                   System.Globalization.CultureInfo.InvariantCulture, out float mul))
            {
                mul = Mathf.Max(0.01f, mul);
                CheatState.PlayerSpeedMultiplier = mul;
                ApplyBoltToMovement();
                PrintResult($"Speed x{mul:0.###}");
            }
            else PrintResult("Usage: BOLT <number>");
        });

        _commands["SETTIME"] = RequireFraud(args =>
        {
            if (args.Length >= 2 && int.TryParse(args[1], out int hour))
            {
                hour = Mathf.Clamp(hour, 0, 23);
                var cycle = Object.FindFirstObjectByType<DayNightCycle>(FindObjectsInactive.Include);

                if (cycle != null)
                {
                    // patrz patch w DayNightCycle poniżej
                    cycle.SetHour(hour);
                    PrintResult($"Time set to {hour:00}:00");
                }
                else PrintResult("DayNightCycle not found");
            }
            else PrintResult("Usage: SETTIME <0..23>");
        });

        _commands["GOLDRUSH"] = RequireFraud(args =>
        {
            if (args.Length < 2 || !int.TryParse(args[1], out int amount))
            {
                PrintResult("Usage: GOLDRUSH <amount>");
                return;
            }

            var stats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Include);
            if (stats != null)
            {
                stats.AddMoney(amount);
                PrintResult($"Added {amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}$ → Total: {stats.money.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}$");
            }
            else
            {
                PrintResult("PlayerStats not found");
            }
        });

        // ==== ALWAYS AVAILABLE ====
        _commands["CLEAR"] = args => ClearConsole();
        _commands["CLEAN"] = args => ClearConsole();

        _commands["TIME"] = args =>
        {
            var gts = GameTimeSystem.Instance;
            if (gts == null)
            {
                PrintResult("GameTimeSystem not found");
                return;
            }

            // np. "12:34 | 01-07-2050"
            var dt = gts.CurrentTime;
            PrintResult($"{dt:HH:mm} | {dt:dd-MM-yyyy}");
        };

    }

    // helper: owija akcję i pilnuje aktywacji FRAUD
    private Action<string[]> RequireFraud(Action<string[]> inner) => (args) =>
    {
        if (!CheatState.CheatsUnlocked) { /* nic – komenda wymaga FRAUD */ return; }
        inner(args);
    };

    private bool IsExternalUiRequestingLock()
    {
        // 1) BankDialogueUI (masz publiczne IsOpen)
        var dlg = Object.FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);
        if (dlg != null && dlg.IsOpen) return true;

        // 2) AccountOperationsUI (masz publiczne IsOpen)
        var acc = Object.FindFirstObjectByType<AccountOperationsUI>(FindObjectsInactive.Include);
        if (acc != null && acc.IsOpen) return true;

        // 3) CreditCardOperationsUI – brak IsOpen → sprawdź CanvasGroup lub aktywność
        var cc = Object.FindFirstObjectByType<CreditCardOperationsUI>(FindObjectsInactive.Include);
        if (cc != null && IsUiVisible(cc.gameObject)) return true;

        // 4) BankAccountCreateUI – IsOpen prywatne → też sprawdź wizualnie
        var create = Object.FindFirstObjectByType<BankAccountCreateUI>(FindObjectsInactive.Include);
        if (create != null && IsUiVisible(create.gameObject)) return true;

        return false;
    }

    private bool IsUiVisible(GameObject go)
    {
        if (go == null) return false;
        if (!go.activeInHierarchy) return false;

        // jeśli gdzieś jest CanvasGroup, to użyj go jako "prawdy" czy UI działa
        var cg = go.GetComponentInChildren<CanvasGroup>(true);
        if (cg != null)
            return cg.alpha > 0.01f && cg.blocksRaycasts && cg.interactable;

        // fallback: sam activeInHierarchy
        return true;
    }


    private void PrintResult(string msg) => logText.text += "\n" + msg;

    void ApplyBoltToMovement()
    {
        var pm = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
    }

    void Update()
    {
        if (CarRaceManager.IsRaceLoading)
            return;

        // Toggle przez Input System (~)
        if (PlayerInputHandler.Instance?.ToggleConsolePressed ?? false)
        {
            SetConsoleOpen(!isConsoleOpen);
        }

        if (!isConsoleOpen) return;

        // ↑ / ↓ – nawigacja po historii
        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            if (history.Count > 0)
            {
                if (historyIndex == -1) historyIndex = history.Count - 1; // przejście z “pustego”
                else historyIndex = Mathf.Max(0, historyIndex - 1);

                inputField.text = history[historyIndex];
                // ustaw kursor na końcu
                inputField.caretPosition = inputField.text.Length;
                inputField.selectionAnchorPosition = inputField.caretPosition;
                inputField.selectionFocusPosition = inputField.caretPosition;
                inputField.ActivateInputField();
            }
        }
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            if (history.Count > 0)
            {
                if (historyIndex == -1) { /* nic – już jesteśmy na “pustym” */ }
                else
                {
                    historyIndex++;
                    if (historyIndex >= history.Count)
                    {
                        historyIndex = -1;      // wróć do “pustego”
                        inputField.text = "";
                    }
                    else
                    {
                        inputField.text = history[historyIndex];
                    }
                    inputField.caretPosition = inputField.text.Length;
                    inputField.selectionAnchorPosition = inputField.caretPosition;
                    inputField.selectionFocusPosition = inputField.caretPosition;
                    inputField.ActivateInputField();
                }
            }
        }

        // Submit komendy Enterem
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            string cmd = inputField.text;
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                // dopisz do historii (bez dublowania dwóch identycznych pod rząd)
                if (history.Count == 0 || history[^1] != cmd)
                    history.Add(cmd);
            }

            ExecuteCommand(cmd);
            inputField.text = "";
            inputField.ActivateInputField();
            historyIndex = -1; // po wykonaniu wróć na “pusty”
        }
    }


    private void SetConsoleOpen(bool open)
    {
        if (_isTransitioning) return;

        isConsoleOpen = open;
        IsOpen = open;

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        if (open)
        {
            CachePrevLockState();

            consolePanel.SetActive(true);
            ApplyConsoleLocks(true);

            _fadeRoutine = StartCoroutine(CoFade(open: true));
        }
        else
        {
            _fadeRoutine = StartCoroutine(CoFade(open: false));
        }
    }

    private IEnumerator CoFade(bool open)
    {
        _isTransitioning = true;

        if (consoleCanvasGroup == null)
        {
            if (!open)
            {
                consolePanel.SetActive(false);
                RestoreLocksAfterConsoleClosed();
            }
            _isTransitioning = false;
            yield break;
        }

        float start = consoleCanvasGroup.alpha;
        float end = open ? 1f : 0f;

        consoleCanvasGroup.interactable = open;
        consoleCanvasGroup.blocksRaycasts = open;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            consoleCanvasGroup.alpha = Mathf.Lerp(start, end, k);
            yield return null;
        }

        consoleCanvasGroup.alpha = end;

        if (!open)
        {
            consolePanel.SetActive(false);
            RestoreLocksAfterConsoleClosed();
        }
        else
        {
            if (inputField)
            {
                inputField.text = "";
                inputField.ActivateInputField();
            }
            historyIndex = -1;
        }

        _isTransitioning = false;
    }



    private void CachePrevLockState()
    {
        _prevMoveLocked = PlayerMovement.IsMovementLocked;
        _prevLookLocked = MouseLook.IsLookLocked;
        _prevCursorLock = Cursor.lockState;
        _prevCursorVisible = Cursor.visible;
        _cachedPrev = true;
    }

    private void ApplyConsoleLocks(bool open)
    {
        // Konsola zawsze wymaga kursora
        PlayerMovement.IsMovementLocked = open;
        MouseLook.IsLookLocked = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }

    private void RestoreLocksAfterConsoleClosed()
    {
        // jeśli inne UI wciąż chce lock — nie ruszaj
        if (IsExternalUiRequestingLock())
            return;

        if (_cachedPrev)
        {
            PlayerMovement.IsMovementLocked = _prevMoveLocked;
            MouseLook.IsLookLocked = _prevLookLocked;
            Cursor.lockState = _prevCursorLock;
            Cursor.visible = _prevCursorVisible;
            _cachedPrev = false;
        }
        else
        {
            PlayerMovement.IsMovementLocked = false;
            MouseLook.IsLookLocked = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OpenConsoleInstant()
    {
        consolePanel.SetActive(true);
        if (consoleCanvasGroup) consoleCanvasGroup.alpha = 1f;
        ApplyConsoleLocks(true);
    }

    private void CloseConsoleInstant()
    {
        consolePanel.SetActive(false);
        if (consoleCanvasGroup) consoleCanvasGroup.alpha = 0f;
        RestoreLocksAfterConsoleClosed();
    }

    void ExecuteCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        // proste tokenizowanie: pierwszy token = komenda, reszta = args
        var parts = command.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var cmdRaw = parts[0];
        var cmd = NormalizeCommandToken(cmdRaw);

        if (_commands.TryGetValue(cmd, out var handler))

        {
            handler(parts);
        }
        else
        {
            PrintResult($"{cmd}");
        }
    }

    private static string NormalizeCommandToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "";

        token = token.Trim().ToUpperInvariant();

        // leet -> litery
        token = token
            .Replace('3', 'E')
            .Replace('4', 'A')
            .Replace('1', 'I')
            .Replace('5', 'S')
            .Replace('8', 'B');

        // opcjonalnie: usuń znaki nie-alfanumeryczne (zostawiamy tylko litery/cyfry)
        // jeśli chcesz pozwolić na myślniki itp, usuń ten fragment
        var chars = new System.Text.StringBuilder(token.Length);
        for (int i = 0; i < token.Length; i++)
        {
            char c = token[i];
            if (char.IsLetterOrDigit(c)) chars.Append(c);
        }
        return chars.ToString();
    }

    private void ClearConsole()
    {
        // log
        if (logText) logText.text = "";

        // historia komend (↑/↓)
        history.Clear();
        historyIndex = -1;

        // input
        if (inputField)
        {
            inputField.text = "";
            inputField.ActivateInputField();
        }

        // mały feedback
        if (logText) logText.text = "Console cleared";
    }

}
