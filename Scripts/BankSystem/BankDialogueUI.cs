using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class BankDialogueUI : MonoBehaviour
{
    [Header("Shared Dialogue Window")]
    [SerializeField] private DialogueWindowUI window;

    [Header("Dialogue Graphs")]
    public DialogueGraphRegistry registry;
    public string defaultGraphKey = "Start";

    [Header("Hint")]
    [SerializeField] private CanvasGroup useIdHint;
    [SerializeField] private TMP_Text useIdHintText;
    [SerializeField] private float hintBlinkSpeed = 4f;
    [SerializeField] private float hintMinAlpha = 0.25f;
    [SerializeField] private float hintMaxAlpha = 1f;

    private Coroutine useIdHintRoutine;
    private bool _closeKeepLock;

    private DialogueGraph _sessionGraph;
    private string _sessionNpcName;
    public static int SuppressEscapeFrames = 0;

    private enum InteractConfirmMode
    {
        None,
        ShowId,
        PayAccountFee
    }

    private InteractConfirmMode _confirmMode = InteractConfirmMode.None;

    // --- ID confirm flow ---

    private string pendingNextToken; // np. "graph:Account/ACCOUNT_CHECK"

    [Header("Dialogue Pacing")]
    public float npcPostDelay = 0.25f;
    public float playerPostDelay = 0.6f;

    // ===== runtime =====
    public bool IsOpen { get; private set; }

    private DialogueGraph _graph;
    private DialogueNode _node;
    private string _npcName = "NPC";

    private Coroutine _postDelay;
    private bool _waitingForChoice;
    private bool _currentTypingIsPlayer;

    private string _currentLineFull = "";

    // return-token (zostawiłem, bo masz w kodzie)
    private string _returnAfterIdToken;

    private bool _sessionIdVerified = false;
    private string _sessionCitizenId = null;
    public event System.Action DialogueClosed;

    void Awake()
    {
        EnsureWindow();

        if (window != null)
            window.CloseWindowImmediate();

        ShowUseIdHint(false);


    }

    void Update()
    {
        if (SuppressEscapeFrames > 0)
        {
            SuppressEscapeFrames--;
            return;
        }

        if (!IsOpen)
            return;

        if (BankUiState.AnyUiOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            var accOps = FindFirstObjectByType<AccountOperationsUI>(FindObjectsInactive.Include);
            if (accOps != null && accOps.IsOpen)
                return;
        }

        bool interactThisFrame =
            (PlayerInputHandler.Instance != null && PlayerInputHandler.Instance.InteractPressedThisFrame) ||
            Input.GetKeyDown(KeyCode.E);

        bool windowTyping = window != null && window.IsTyping;

        if (_confirmMode != InteractConfirmMode.None && !windowTyping && interactThisFrame)
        {
            var mode = _confirmMode;
            _confirmMode = InteractConfirmMode.None;
            ShowUseIdHint(false);

            if (mode == InteractConfirmMode.ShowId)
            {
                var bank = BankSystem.Instance;
                string cid = GetCitizenId();

                if (bank == null || string.IsNullOrWhiteSpace(cid) || !bank.HasCitizenId(cid))
                {
                    if (TryResolveNext("graph:Account/NO_ID", out var gNo, out var nNo))
                    {
                        _graph = gNo;
                        GoToNode(nNo);
                    }
                    else
                    {
                        Close();
                    }

                    return;
                }

                _sessionIdVerified = true;
                _sessionCitizenId = cid;

                Debug.Log($"[BANK SESJA] ID ZWERYFIKOWANE (SHOW_ID confirm): citizenId={cid}");

                if (!TryResolveNext("graph:Account/ACCOUNT_CHECK", out var nextGraph, out var nextNode))
                {
                    Close();
                    return;
                }

                _graph = nextGraph;
                GoToNode(nextNode);
                return;
            }

            if (mode == InteractConfirmMode.PayAccountFee)
            {
                StartCoroutine(CoCreateAccountFlowAfterPayment());
                return;
            }
        }

        if (IsAnyBankUiOpen())
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_graph != null)
            {
                if (_node != null && string.Equals(_node.id, _graph.startNodeId, System.StringComparison.OrdinalIgnoreCase))
                {
                    Close();
                    return;
                }

                var start = _graph.GetNode(_graph.startNodeId);
                if (start == null && _graph.nodes.Count > 0)
                    start = _graph.nodes[0];

                if (start != null)
                {
                    ClearHistory();
                    GoToNode(start);
                    return;
                }
            }

            Close();
        }
    }

    // === Public API ===

    public void OpenDialogueFromRegistry(string graphKey, string npcDisplayName)
    {
        if (registry == null) return;

        var g = registry.Get(graphKey);
        if (g == null) return;

        OpenDialogue(g, npcDisplayName);
    }
    public void OpenDialogue(DialogueGraph graph, string npcDisplayName)
    {
        if (graph == null)
            return;

        if (!EnsureWindow())
            return;

        _graph = graph;
        _npcName = string.IsNullOrWhiteSpace(npcDisplayName) ? "NPC" : npcDisplayName;

        _sessionGraph = graph;
        _sessionNpcName = _npcName;

        IsOpen = true;

        window.OpenWindow(clearHistory: true, lockPlayer: true);

        var start = _graph.GetNode(_graph.startNodeId);
        if (start == null && _graph.nodes.Count > 0)
            start = _graph.nodes[0];

        GoToNode(start);
    }

    public void Close()
    {
        Close(unlockPlayer: true);
    }

    public void Close(bool unlockPlayer)
    {
        if (!IsOpen)
            return;

        StopTyping();
        HideOptions();
        ShowUseIdHint(false);

        IsOpen = false;
        _graph = null;
        _node = null;
        _waitingForChoice = false;

        if (window != null)
            window.CloseWindow(unlockPlayer);

        DialogueClosed?.Invoke();
    }

    private void UnlockPlayerAndCursor()
    {
        // Cursor + look
        MouseLook.IsLookLocked = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Movement lock flag (Twoje)
        PlayerMovement.IsMovementLocked = false;

        // Upewnij się, że komponent ruchu działa
        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm) pm.enabled = true;

        // Input handler (Twoje)
        var ih = FindFirstObjectByType<PlayerInputHandler>();
        if (ih) ih.enabled = true;

        // Nowy Input System: PlayerInput potrafi zostać w złej mapie lub nieaktywny
        var pi = FindFirstObjectByType<UnityEngine.InputSystem.PlayerInput>();
        if (pi)
        {
            pi.enabled = true;
            pi.ActivateInput();

            // Jeśli masz mapę "Player" (najczęściej), to wymuś powrót
            // (jeśli nie masz takiej mapy, usuń tę linijkę)
            if (!string.IsNullOrEmpty(pi.currentActionMap?.name) && pi.currentActionMap.name != "Player")
                pi.SwitchCurrentActionMap("Player");
        }

        // Broń
        var wm = FindFirstObjectByType<WeaponManager>();
        if (wm) wm.enabled = true;

        Debug.Log($"[BANK UNLOCK] MoveLocked={PlayerMovement.IsMovementLocked}  LookLocked={MouseLook.IsLookLocked}");
    }


    private void OnDisable()
    {
        if (IsOpen) return;
        // awaryjnie - jakby UI zostało wyłączone bez Close()
        PlayerMovement.IsMovementLocked = false;
        MouseLook.IsLookLocked = false;
    }

    private bool IsAnyBankUiOpen()
    {
        var accOps = FindFirstObjectByType<AccountOperationsUI>(FindObjectsInactive.Include);
        if (accOps != null && accOps.IsOpen) return true;

        var create = FindFirstObjectByType<BankAccountCreateUI>(FindObjectsInactive.Include);
        if (create != null && create.gameObject.activeInHierarchy) return true;

        var ccOps = FindFirstObjectByType<CreditCardOperationsUI>(FindObjectsInactive.Include);
        // jeśli możesz – dodaj tam IsOpen analogicznie
        // if (ccOps != null && ccOps.IsOpen) return true;
        if (ccOps != null && ccOps.gameObject.activeInHierarchy) return true;

        return false;
    }

    // === Flow ===

    private void GoToNode(DialogueNode node)
    {
        _node = node;
        if (_node == null) return;

        HideOptions();
        _waitingForChoice = false;

        TypeLine(_npcName, _node.npcText, () =>
        {
            if (_node.endAfterNpcLine)
            {
                _waitingForChoice = false;

                // AUTO-RUN: jeśli node ma 1 opcję @event, odpal ją bez klikania
                if (_node.options != null && _node.options.Count == 1)
                {
                    var opt0 = _node.options[0];
                    if (string.Equals(opt0.nextNodeId, "@event", System.StringComparison.OrdinalIgnoreCase))
                    {
                        string token = ExecuteDebugEvent(opt0.debugEvent);

                        // @hold = zostajemy w tym stanie, np. czekamy na E
                        if (string.Equals(token, "@hold", System.StringComparison.OrdinalIgnoreCase))
                            return;

                        if (string.IsNullOrWhiteSpace(token))
                        {
                            Close();
                            return;
                        }

                        if (!TryResolveNext(token, out var nextGraph, out var nextNode))
                        {
                            Close();
                            return;
                        }

                        _graph = nextGraph;
                        GoToNode(nextNode);
                        return;
                    }
                }

                return;
            }


            ShowOptions(_node.options);
            _waitingForChoice = true;
        });
    }

    private bool IsSilentBackOption(DialogueOption opt)
    {
        if (opt == null) return false;

        if (!string.IsNullOrWhiteSpace(opt.playerText) &&
            opt.playerText.Trim().Equals("Wroc", System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(opt.debugEvent) &&
            opt.debugEvent.Trim().Equals("BACK", System.StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private void OnOptionClicked(int index)
    {
        if (!IsOpen || !_waitingForChoice || _node == null || _node.options == null) return;
        if (index < 0 || index >= _node.options.Count) return;

        var opt = _node.options[index];
        _waitingForChoice = false;
        HideOptions();

        // SILENT BACK: bez kwestii gracza
        if (IsSilentBackOption(opt))
        {
            string token = opt.nextNodeId;

            if (string.Equals(token, "@event", System.StringComparison.OrdinalIgnoreCase))
                token = ExecuteDebugEvent(opt.debugEvent);

            if (string.Equals(token, "@hold", System.StringComparison.OrdinalIgnoreCase))
                return;

            if (string.IsNullOrWhiteSpace(token)) { Close(); return; }

            if (!TryResolveNext(token, out var nextGraph, out var nextNode))
            {
                Close();
                return;
            }

            _graph = nextGraph;
            GoToNode(nextNode);
            return;
        }

        // Normalnie: gracz mówi i dopiero przejście
        TypeLine("PLAYER", opt.playerText, () =>
        {
            string token = opt.nextNodeId;

            if (string.Equals(token, "@event", System.StringComparison.OrdinalIgnoreCase))
                token = ExecuteDebugEvent(opt.debugEvent);

            if (string.Equals(token, "@hold", System.StringComparison.OrdinalIgnoreCase))
                return;

            if (string.IsNullOrWhiteSpace(token)) { CloseByToken(); return; }

            if (!TryResolveNext(token, out var nextGraph, out var nextNode))
            {
                CloseByToken();
                return;
            }

            _graph = nextGraph;
            GoToNode(nextNode);
        });
    }

    // === Typing + log ===

    private void TypeLine(string speaker, string line, System.Action onDone)
    {
        if (!EnsureWindow())
            return;

        StopTyping();
        HideOptions();

        _currentTypingIsPlayer = string.Equals(speaker, "PLAYER", System.StringComparison.OrdinalIgnoreCase);
        _currentLineFull = line ?? "";

        window.TypeLine(speaker, _currentLineFull, _currentTypingIsPlayer, () =>
        {
            float pause = _currentTypingIsPlayer ? playerPostDelay : npcPostDelay;

            if (_postDelay != null)
                StopCoroutine(_postDelay);

            _postDelay = StartCoroutine(CoPostDelayThenInvoke(onDone, pause));
        });
    }

    private IEnumerator CoPostDelayThenInvoke(System.Action onDone, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        onDone?.Invoke();
        _postDelay = null;
    }

    private void StopTyping()
    {


        if (_postDelay != null)
        {
            StopCoroutine(_postDelay);
            _postDelay = null;
        }
    }

    // ===== History =====

    private void ClearHistory()
    {
        if (window != null)
            window.ClearHistory();
    }

    // ===== Options UI =====

    private void ShowOptions(List<DialogueOption> options)
    {
        if (!EnsureWindow())
            return;

        if (options == null)
            options = new List<DialogueOption>();

        List<string> optionTexts = new List<string>();

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == null)
            {
                optionTexts.Add("");
                continue;
            }

            optionTexts.Add(options[i].playerText);
        }

        window.ShowOptions(optionTexts, OnOptionClicked);
    }

    private void HideOptions()
    {
        if (window != null)
            window.HideOptions();
    }


    // ===== Graph resolve / events (Twoje) =====

    private bool TryResolveNext(string nextToken, out DialogueGraph nextGraph, out DialogueNode nextNode)
    {
        nextGraph = null;
        nextNode = null;

        if (string.IsNullOrWhiteSpace(nextToken)) return false;
        nextToken = nextToken.Trim();
        nextToken = ConsumeReturnTokenIfAny(nextToken);

        if (string.Equals(nextToken, "@end", System.StringComparison.OrdinalIgnoreCase))
            return false;

        if (nextToken.StartsWith("graph:", System.StringComparison.OrdinalIgnoreCase))
        {
            if (registry == null) return false;

            var rest = nextToken.Substring("graph:".Length).Trim();

            string graphKey = rest;
            string nodeId = null;

            int slash = rest.IndexOf('/');
            if (slash >= 0)
            {
                graphKey = rest.Substring(0, slash).Trim();
                nodeId = rest.Substring(slash + 1).Trim();
            }

            var g = registry.Get(graphKey);
            if (g == null) return false;

            if (string.IsNullOrWhiteSpace(nodeId))
                nodeId = g.startNodeId;

            var n = g.GetNode(nodeId);
            if (n == null) return false;

            nextGraph = g;
            nextNode = n;
            return true;
        }

        if (_graph == null) return false;

        var local = _graph.GetNode(nextToken);
        if (local == null) return false;

        nextGraph = _graph;
        nextNode = local;
        return true;
    }

    private string ConsumeReturnTokenIfAny(string token)
    {
        int idx = token.IndexOf("|ret=", System.StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return token;

        string main = token.Substring(0, idx).Trim();
        string ret = token.Substring(idx + 5).Trim();

        if (!string.IsNullOrWhiteSpace(ret))
            _returnAfterIdToken = ret;

        return main;
    }

    private string GetCitizenId()
    {
        var ps = FindFirstObjectByType<PlayerStats>();
        return ps ? ps.citizenId : null;
    }

    private string ExecuteDebugEvent(string evt)
    {
        if (string.IsNullOrWhiteSpace(evt)) return "@end";
        evt = evt.Trim();

        var bank = BankSystem.Instance;

        // Lazy cache (FindFirstObjectByType tylko gdy potrzebne)
        PlayerStats psCache = null;
        PlayerStats PS() => psCache != null ? psCache : (psCache = FindFirstObjectByType<PlayerStats>());

        string cidCache = null;
        string CID() => cidCache ??= GetCitizenId(); // u Ciebie GetCitizenId też robi Find..., więc też lazy

        switch (evt)
        {
            // =========================
            // ASYNC / DELAYED
            // =========================
            case "ACCOUNT_CHECK_ACCOUNT":
                StartCoroutine(DelayedAccountCheck());
                return "@hold";

            // =========================
            // ENTRY POINTS
            // =========================
            case "ACCOUNT_ENTRY":
                {
                    var ps = PS();
                    if (ps == null || string.IsNullOrWhiteSpace(ps.citizenId))
                        return "graph:Account/NO_ID";

                    // Jeśli ID w sesji niezweryfikowane -> pokaż SHOW_ID (hint + E)
                    if (!_sessionIdVerified || _sessionCitizenId != ps.citizenId)
                    {
                        Debug.Log("[BANK SESJA] ACCOUNT_ENTRY: brak weryfikacji -> SHOW_ID");
                        return "graph:Account/SHOW_ID";
                    }

                    // ID zweryfikowane -> sprawdź konto
                    if (bank == null) return "@end";

                    if (!bank.TryGetAccountForCitizen(ps.citizenId, out var acc) || acc == null)
                        return "graph:Account/NO_ACCOUNT";

                    // ✅ Konto istnieje -> od razu otwórz AccountOperationsUI (bez menu "stan konta")
                    var ui = FindFirstObjectByType<AccountOperationsUI>(FindObjectsInactive.Include);
                    if (ui == null)
                    {
                        Debug.LogWarning("[BANK] AccountOperationsUI not found in scene.");
                        return "graph:Start/start";
                    }

                    _closeKeepLock = true;

                    // ✅ zamknij dialog natychmiast (ale bez oddania sterowania, bo UI przejmuje)
                    Close(unlockPlayer: false);

                    // ✅ otwórz UI
                    ui.OpenForAccount(acc.accountId);

                    // ✅ zatrzymaj flow w grafie – dialog już zamknięty, niech nic nie domyka
                    return "@hold";
                }

            // =========================
            // ID CONFIRM + CHECK
            // =========================
            case "WAIT_FOR_ID_CONFIRM":
                {
                    _confirmMode = InteractConfirmMode.ShowId;
                    ShowUseIdHint(true, $"Naciśnij [{GetInteractBindingDisplay()}], aby użyć karty ID");
                    return "@hold";
                }

            case "CHECK_ID":
                {
                    if (bank == null) return "graph:ID_GATE/NO_ID";

                    // jeśli już sprawdzony w tej sesji → NIE sprawdzaj ponownie
                    if (_sessionIdVerified && !string.IsNullOrWhiteSpace(_sessionCitizenId))
                    {
                        Debug.Log($"[BANK SESJA] CHECK_ID: już zweryfikowane wcześniej: {_sessionCitizenId}");

                        if (!string.IsNullOrWhiteSpace(_returnAfterIdToken))
                        {
                            var ret = _returnAfterIdToken;
                            _returnAfterIdToken = null;
                            return ret;
                        }

                        return "graph:Start/start";
                    }

                    var cid = CID();
                    if (string.IsNullOrWhiteSpace(cid) || !bank.HasCitizenId(cid))
                    {
                        Debug.Log("[BANK SESJA] CHECK_ID: brak CitizenID -> NO_ID");
                        return "graph:ID_GATE/NO_ID";
                    }

                    _sessionIdVerified = true;
                    _sessionCitizenId = cid;

                    Debug.Log($"[BANK SESJA] CHECK_ID: ID ZWERYFIKOWANE: citizenId={cid}");

                    if (!string.IsNullOrWhiteSpace(_returnAfterIdToken))
                    {
                        var ret = _returnAfterIdToken;
                        _returnAfterIdToken = null;
                        return ret;
                    }

                    return "graph:Start/start";
                }

            // =========================
            // ACCOUNT EXISTENCE CHECKS
            // =========================
            case "ACCOUNT_HAS_ACCOUNT":
            case "ACCOUNT_ID_GATE":
            case "ACCOUNT_ID_CHECK":
                {
                    if (bank == null) return "@end";

                    var cid = CID();
                    if (string.IsNullOrWhiteSpace(cid) || !bank.HasCitizenId(cid))
                        return "graph:Account/NO_ID";

                    return bank.TryGetAccountForCitizen(cid, out _)
                        ? "graph:Account/OPERATIONS_MENU"
                        : "graph:Account/NO_ACCOUNT";
                }

            // =========================
            // FEE CONFIRM + CREATE FLOW
            // =========================
            case "WAIT_FOR_FEE_CONFIRM":
                {
                    var ps = PS();
                    if (ps != null && ps.money < 10)
                        return "graph:Account/FEE_FAIL";

                    _confirmMode = InteractConfirmMode.PayAccountFee;
                    ShowUseIdHint(true, $"Naciśnij [{GetInteractBindingDisplay()}], aby potwierdzić");
                    return "@hold";
                }

            // (opcjonalnie: jeśli kiedyś chcesz mieć osobny event na płatność)
            case "PAY_ACCOUNT_FEE":
                {
                    var ps = PS();
                    if (ps == null) return "graph:Start/start";
                    if (ps.money < 10) return "graph:Account/FEE_FAIL";

                    StartCoroutine(CoCreateAccountFlowAfterPayment());
                    return "@hold";
                }

            case "CREATE_ACCOUNT":
                {
                    if (bank == null) return "@end";

                    var ps = PS();
                    if (ps == null || string.IsNullOrWhiteSpace(ps.citizenId))
                        return "graph:Account/NO_ID";

                    var createUi = FindFirstObjectByType<BankAccountCreateUI>(FindObjectsInactive.Include);
                    if (createUi == null)
                    {
                        Debug.LogWarning("[BANK] BankAccountCreateUI not found in scene.");
                        return "@end";
                    }

                    Close(unlockPlayer: false);

                    createUi.Open(ps.citizenId, (createdAccountId) =>
                    {
                        if (createdAccountId <= 0) return;

                        var opsUi = FindFirstObjectByType<AccountOperationsUI>(FindObjectsInactive.Include);
                        if (opsUi != null)
                            opsUi.OpenForAccount(createdAccountId);
                        else
                            Debug.LogWarning("[BANK] AccountOperationsUI not found in scene.");
                    });

                    return "@hold";
                }

            // =========================
            // CARD FLOW (ID + account check)
            // =========================
            case "CARD_ID_GATE":
            case "CHECK_ID_FOR_CARD":
                {
                    if (bank == null) return "@end";

                    var cid = CID();
                    if (string.IsNullOrWhiteSpace(cid) || !bank.HasCitizenId(cid))
                        return "graph:Card/NO_ID";

                    return "graph:Card/CHECK_ACCOUNT";
                }

            case "CARD_CHECK_ACCOUNT":
            case "CHECK_HAS_ACCOUNT_FOR_CARD":
                {
                    if (bank == null) return "@end";

                    var cid = CID();
                    if (string.IsNullOrWhiteSpace(cid) || !bank.TryGetAccountForCitizen(cid, out _))
                        return "graph:Card/NO_ACCOUNT";

                    return "graph:Card/SELECT_CARD";
                }

            case "OPEN_CARD_PICKER":
                return "graph:Card/OPERATIONS_MENU";

            // =========================
            // OPEN OPERATIONS UI
            // =========================
            case "OPEN_ACCOUNT_OPERATIONS":
                {
                    if (bank == null) return "graph:Start/start";

                    var cid = CID();
                    if (string.IsNullOrWhiteSpace(cid))
                        return "graph:Account/NO_ID";

                    if (!bank.TryGetAccountForCitizen(cid, out var acc) || acc == null)
                        return "graph:Account/NO_ACCOUNT";

                    var ui = FindFirstObjectByType<AccountOperationsUI>(FindObjectsInactive.Include);
                    if (ui == null)
                    {
                        Debug.LogWarning("[BANK] AccountOperationsUI not found in scene.");
                        return "graph:Start/start";
                    }

                    _closeKeepLock = true;
                    Close(unlockPlayer: false);
                    ui.OpenForAccount(acc.accountId);
                    return "@hold";

                }
        }

        return "@end";
    }

    private IEnumerator DelayedAccountCheck()
    {
        yield return new WaitForSeconds(5f);

        var bank = BankSystem.Instance;
        string cid = GetCitizenId();

        if (bank == null || !bank.HasCitizenId(cid))
        {
            GoToResolved("graph:Account/NO_ID");
            yield break;
        }

        if (!bank.TryGetAccountForCitizen(cid, out var acc) || acc == null)
        {
            GoToResolved("graph:Account/NO_ACCOUNT");
            yield break;
        }

        // ✅ Konto istnieje -> od razu otwórz AccountOperationsUI (bez OPERATIONS_MENU)
        var ui = FindFirstObjectByType<AccountOperationsUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            Debug.LogWarning("[BANK] AccountOperationsUI not found in scene.");
            GoToResolved("graph:Start/start");
            yield break;
        }

        _closeKeepLock = true;
        ui.OpenForAccount(acc.accountId);

        // zamknij dialog (bez unlock)
        CloseByToken();
    }

    private void GoToResolved(string token)
    {
        if (TryResolveNext(token, out var g, out var n))
        {
            _graph = g;
            GoToNode(n);
        }
        else Close();
    }

    private void ShowUseIdHint(bool show, string message = null)
    {
        if (useIdHintRoutine != null) { StopCoroutine(useIdHintRoutine); useIdHintRoutine = null; }
        if (useIdHint == null) return;

        if (!show)
        {
            useIdHint.alpha = 0f;
            useIdHint.interactable = false;
            useIdHint.blocksRaycasts = false;
            useIdHint.gameObject.SetActive(false);
            return;
        }

        useIdHint.gameObject.SetActive(true);
        useIdHint.interactable = false;
        useIdHint.blocksRaycasts = false;

        if (useIdHintText)
            useIdHintText.text = string.IsNullOrWhiteSpace(message) ? "" : message;

        useIdHintRoutine = StartCoroutine(BlinkUseIdHint());
    }

    private IEnumerator BlinkUseIdHint()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * hintBlinkSpeed;
            useIdHint.alpha = Mathf.Lerp(hintMinAlpha, hintMaxAlpha, (Mathf.Sin(t) + 1f) * 0.5f);
            yield return null;
        }
    }

    private string GetInteractBindingDisplay()
    {
        // fallback
        if (PlayerInputHandler.Instance == null) return "E";

        // akcja Interact z mapy Player
        var action = PlayerInputHandler.Instance.playerMap.FindAction("Interact", throwIfNotFound: false);
        if (action == null) return "E";

        // Najprościej: pierwszy binding (u Ciebie to będzie <Keyboard>/e, dopóki nie zrobisz rebinda)
        // DisplayString ładnie zwróci np. "E" albo "F" albo "Button South"
        return action.GetBindingDisplayString();
    }

    private IEnumerator CoCreateAccountFlowAfterPayment()
    {
        string cid = GetCitizenId();
        Debug.Log($"[BANK] CoCreateAccountFlowAfterPayment START cid={cid}");

        TypeLine(_npcName, "Please wait...", () => { });

        HideOptions();
        _waitingForChoice = false;

        yield return new WaitForSecondsRealtime(10f);

        var ui = FindCreateAccountUI();
        Debug.Log($"[BANK] CreateUI found? {(ui != null)}");

        if (ui == null)
        {
            Debug.LogWarning("[BANK] BankAccountCreateUI NOT FOUND in scene (even inactive).");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(cid))
        {
            Debug.LogWarning("[BANK] CitizenId is empty -> cannot open create account UI.");
            if (TryResolveNext("graph:Account/NO_ID", out var gNo, out var nNo))
            {
                _graph = gNo;
                GoToNode(nNo);
            }
            yield break;
        }

        // Zamknij dialog, ale nie oddawaj sterowania (UI przejmuje)
        Close(unlockPlayer: false);

        Debug.Log("[BANK] Opening BankAccountCreateUI...");
        ui.Open(cid, onConfirm: (createdAccountId) =>
        {
            Debug.Log($"[BANK] CreateUI finished createdAccountId={createdAccountId}");

            // ===== anulowano =====
            if (createdAccountId <= 0)
            {
                if (TryResolveNext("graph:Start/start", out var g0, out var n0))
                {
                    OpenDialogue(g0, _npcName);
                }
                else
                {
                    UnlockPlayerAndCursor();
                }
                return;
            }

            // ===== konto utworzone -> idź do "Sprawdzam konto..." =====
            if (TryResolveNext("graph:Account/ACCOUNT_CHECK", out var gAcc, out var nAcc))
            {
                _graph = gAcc;

                // pokaż dialog (jeśli był zamknięty) bez resetu sesji
                IsOpen = true;

                if (window != null)
                    window.OpenWindow(clearHistory: true, lockPlayer: true);

                ClearHistory();
                GoToNode(nAcc);
                return;
            }

            // fallback jeśli nie ma node'a
            Debug.LogWarning("[BANK] Could not resolve graph:Account/ACCOUNT_CHECK -> unlocking player.");
            UnlockPlayerAndCursor();
        });
    }

    private void CloseByToken()
    {
        if (_closeKeepLock)
        {
            _closeKeepLock = false;
            Close(unlockPlayer: false);
        }
        else
        {
            Close();
        }
    }

    private BankAccountCreateUI FindCreateAccountUI()
    {
        // 1) Najpierw normalnie (w tym inactive)
        var ui = FindFirstObjectByType<BankAccountCreateUI>(FindObjectsInactive.Include);
        if (ui != null)
            return ui;

        // 2) Fallback: znajduje nawet inactive i w nietypowych przypadkach
        // Uwaga: zwróci też prefab assety, więc filtrujemy po scene.IsValid()
        var all = Resources.FindObjectsOfTypeAll<BankAccountCreateUI>();
        foreach (var cand in all)
        {
            if (cand == null) continue;
            if (!cand.gameObject.scene.IsValid()) continue; // odfiltruj prefab z Project
            return cand;
        }

        return null;
    }

    public void ReturnToStartSameSession()
    {
        var g = _sessionGraph;
        if (g == null) return;

        StopTyping();
        HideOptions();
        ShowUseIdHint(false);
        _confirmMode = InteractConfirmMode.None;

        IsOpen = true;

        if (window != null)
            window.OpenWindow(clearHistory: true, lockPlayer: true);

        ClearHistory();

        var start = g.GetNode(g.startNodeId);
        if (start == null && g.nodes.Count > 0) start = g.nodes[0];
        if (start != null)
        {
            _graph = g;      // aktywny graph roboczy
            _npcName = string.IsNullOrWhiteSpace(_sessionNpcName) ? _npcName : _sessionNpcName;
            GoToNode(start);
        }
    }


    public void ResetSession()
    {
        Debug.Log("[BANK SESJA] RESET SESJI (opuszczenie strefy NPC)");

        _sessionIdVerified = false;
        _sessionCitizenId = null;
        _returnAfterIdToken = null;
        _confirmMode = InteractConfirmMode.None;
        ShowUseIdHint(false);
    }

    public void OpenSingleNpcLine(string npcName, string line, float autoCloseAfter = 2.25f)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (!EnsureWindow())
            return;

        IsOpen = true;

        _npcName = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName;
        _graph = null;
        _node = null;

        window.OpenWindow(clearHistory: true, lockPlayer: true);
        window.HideOptions();

        TypeLine(_npcName, line, () =>
        {
            StartCoroutine(CoAutoCloseSingleLine(autoCloseAfter));
        });
    }

    private IEnumerator CoAutoCloseSingleLine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        Close();
    }

    private bool EnsureWindow()
    {
        if (window == null)
            window = FindFirstObjectByType<DialogueWindowUI>(FindObjectsInactive.Include);

        if (window == null)
        {
            Debug.LogWarning("[BankDialogueUI] DialogueWindowUI missing.");
            return false;
        }

        return true;
    }
}
