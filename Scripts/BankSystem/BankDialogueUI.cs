using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BankDialogueUI : MonoBehaviour
{
    [Header("Root")]
    public CanvasGroup root;

    [Header("Wheel Root (container na kółko + buttony A/B/C/D)")]
    public GameObject wheelRoot;

    [Header("Text")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI bodyText; // (nieużywane w tej wersji, może zostać)
    public TextMeshProUGUI historyText;
    public ScrollRect historyScrollRect;

    [Header("Current Line (Text_1)")]
    public TextMeshProUGUI currentLineText;
    public ScrollRect currentLineScroll; // ScrollRect na CurrentLineViewport (Content = CurrentLineContent)

    [Header("Typing")]
    public float charsPerSecond = 40f;

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

    [Header("Typing Colors")]
    public Color unspokenColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    public Color npcSpokenColor = new Color(0.2f, 1f, 0.2f, 1f);
    public Color playerSpokenColor = new Color(1f, 0.9f, 0.2f, 1f);

    [Header("History")]
    public int maxHistoryLines = 4;

    [Header("Auto Follow / Teleprompter")]
    public bool followToBottomOnOverflow = true;
    public float followSmooth = 12f;     // szybkość “dociągania” scrolla
    public int keepContextLines = 1;     // ile linijek zostawić u góry

    [Header("Dynamic Options")]
    public RectTransform optionsRoot;
    public Button optionButtonPrefab;
    public float ringRadius1 = 170f;
    public float ringRadius2 = 260f;
    public float ringRadiusStep = 90f;
    public float startAngleDeg = 90f;
    public bool clockwise = true;
    public int slotsPerRing = 8;
    public float optionMinWidth = 220f;
    public float optionMaxWidth = 420f;
    public float optionPaddingX = 40f;
    public float optionPaddingY = 22f;
    public float centerClearance = 90f;

    // ===== runtime =====
    public bool IsOpen { get; private set; }

    private readonly Queue<string> _history = new();
    private string _historyJoined = "";

    private readonly List<Button> _spawnedOptionButtons = new();

    private DialogueGraph _graph;
    private DialogueNode _node;
    private string _npcName = "NPC";

    private Coroutine _typing;
    private Coroutine _postDelay;
    private bool _isTyping;
    private bool _waitingForChoice;
    private bool _currentTypingIsPlayer;

    private string _currentLineFull = "";
    private System.Action _pendingOnDone;
    private string _pendingSpeaker;

    // manual scroll state
    private bool _userScrolledCurrentLine = false;
    private float _userScrollCooldown = 0f;

    // auto follow target (1 = top, 0 = bottom)
    private float _currentLineTargetNorm = 1f;

    // teleprompter: która linia TMP jest aktualnie u góry
    private int _topLineIndex = 0;

    // return-token (zostawiłem, bo masz w kodzie)
    private string _returnAfterIdToken;

    // ======= ANGLES =======
    private static readonly float[] SLOT_ANGLES_4 = { 180f, 0f, 90f, 270f };
    private static readonly float[] SLOT_ANGLES_8 = { 180f, 0f, 90f, 270f, 135f, 45f, 225f, 315f };

    private bool _sessionIdVerified = false;
    private string _sessionCitizenId = null;
    public event System.Action DialogueClosed;


    void Awake()
    {
        HideImmediate();
        HideOptions();

        if (historyText) historyText.richText = true;
        if (currentLineText) currentLineText.richText = true;

        if (bodyText)
        {
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Overflow;
        }
    }

    void Update()
    {
        if (SuppressEscapeFrames > 0)
        {
            SuppressEscapeFrames--;
            return; // ignoruj ESC i resztę inputu
        }
        if (!IsOpen) return;

        if (BankUiState.AnyUiOpen)
            return;

        // na początku Update, po if(!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            var accOps = FindFirstObjectByType<AccountOperationsUI>(FindObjectsInactive.Include);
            if (accOps != null && accOps.IsOpen)
                return;
        }

        // Confirm ID with E (only when we are waiting)
        bool interactThisFrame =
            (PlayerInputHandler.Instance != null && PlayerInputHandler.Instance.InteractPressedThisFrame) ||
            Input.GetKeyDown(KeyCode.E); // fallback jakby handler był wyłączony


        if (_confirmMode != InteractConfirmMode.None && !_isTyping && interactThisFrame)
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
                    // brak ID -> pokaż NO_ID
                    if (TryResolveNext("graph:Account/NO_ID", out var gNo, out var nNo))
                    {
                        _graph = gNo;
                        GoToNode(nNo);
                    }
                    else Close();
                    return;
                }

                // ✅ TU utrwalamy sesję
                _sessionIdVerified = true;
                _sessionCitizenId = cid;

                Debug.Log($"[BANK SESJA] ID ZWERYFIKOWANE (SHOW_ID confirm): citizenId={cid}");

                // idź do sprawdzania konta
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
                // Tutaj TYLKO zgoda gracza na rozpoczęcie procedury.
                // Gotówkę pobierzemy dopiero w BankAccountCreateUI po finalnym CONFIRM.
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
                // jeśli jesteśmy na starcie -> ESC = definitywne wyjście
                if (_node != null && string.Equals(_node.id, _graph.startNodeId, System.StringComparison.OrdinalIgnoreCase))
                {
                    Close(); // unlockPlayer = true
                    return;
                }

                // w przeciwnym wypadku -> wróć do startu bez zamykania
                var start = _graph.GetNode(_graph.startNodeId);
                if (start == null && _graph.nodes.Count > 0) start = _graph.nodes[0];

                if (start != null)
                {
                    ClearHistory();
                    GoToNode(start);
                    return;
                }
            }

            Close(); // fallback
            return;
        }

        // Skip typing (ale NIE gdy klikamy UI button)
        if (_isTyping)
        {
            bool leftClick = Input.GetMouseButtonDown(0);
            if (leftClick && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                leftClick = false;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || leftClick)
                SkipTyping();
        }

        // Manual scroll (wheel) - tylko gdy kursor nad viewportem
        if (currentLineScroll && currentLineScroll.viewport)
        {
            float wheel = Input.mouseScrollDelta.y;

            if (Mathf.Abs(wheel) > 0.01f && IsPointerOverRect(currentLineScroll.viewport))
            {
                _userScrolledCurrentLine = true;
                _userScrollCooldown = 1.0f;

                const float wheelStep = 0.12f;
                currentLineScroll.verticalNormalizedPosition =
                    Mathf.Clamp01(currentLineScroll.verticalNormalizedPosition + wheel * wheelStep);

                // IMPORTANT: zapamiętaj nowy target (żeby po cooldownie nie “odbijało”)
                _currentLineTargetNorm = currentLineScroll.verticalNormalizedPosition;
            }

            if (_userScrolledCurrentLine)
            {
                _userScrollCooldown -= Time.unscaledDeltaTime;
                if (_userScrollCooldown <= 0f)
                    _userScrolledCurrentLine = false;
            }
        }

        // Auto follow do targetu (płynnie) tylko gdy user nie scrolluje
        if (!currentLineScroll) return;
        if (_userScrolledCurrentLine) return;

        float cur = currentLineScroll.verticalNormalizedPosition;
        float t = 1f - Mathf.Exp(-followSmooth * Time.unscaledDeltaTime);
        float next = Mathf.Lerp(cur, _currentLineTargetNorm, t);
        currentLineScroll.verticalNormalizedPosition = next;
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
        if (graph == null) return;

        if (bodyText) bodyText.text = "";

        // ---- BLOKADA GRACZA / KURSORA ----
        MouseLook.IsLookLocked = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        PlayerMovement.IsMovementLocked = true;


        var wm = FindFirstObjectByType<WeaponManager>();
        if (wm) wm.enabled = false;
        // ----------------------------------

        _graph = graph;
        _npcName = string.IsNullOrWhiteSpace(npcDisplayName) ? "NPC" : npcDisplayName;
        _sessionGraph = graph;
        _sessionNpcName = _npcName; // po tym jak ustawisz _npcName

        IsOpen = true;
        ShowRoot(true);

        var start = _graph.GetNode(_graph.startNodeId);
        if (start == null && _graph.nodes.Count > 0) start = _graph.nodes[0];

        GoToNode(start);
    }

    public void Close()
    {
        Close(unlockPlayer: true);
    }

    public void Close(bool unlockPlayer)
    {
        if (!IsOpen) return; // ✅ guard przed double-close

        StopTyping();
        HideOptions();

        IsOpen = false;
        _graph = null;
        _node = null;

        ShowRoot(false);

        // ✅ Bezpiecznik: NIGDY nie zostawiaj movement locked po zamknięciu dialogu.
        // (look/cursor zostawiamy zależnie od unlockPlayer)
        PlayerMovement.IsMovementLocked = false;

        if (unlockPlayer)
            UnlockPlayerAndCursor();

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
        StopTyping();
        HideOptions();

        _pendingOnDone = onDone;
        _pendingSpeaker = speaker;

        _currentTypingIsPlayer = (speaker == "PLAYER");
        _currentLineFull = line ?? "";

        if (nameText) nameText.text = speaker + ":";

        Color spokenColor = _currentTypingIsPlayer ? playerSpokenColor : npcSpokenColor;

        // HISTORY: tylko zatwierdzona historia
        if (historyText) historyText.text = _historyJoined;
        ForceHistoryScrollToBottom();

        // CURRENT LINE: reset + snap top-left
        if (currentLineText)
        {
            currentLineText.text = BuildGreyAndReveal(_currentLineFull, 0, spokenColor);

            _topLineIndex = 0;
            _currentLineTargetNorm = 1f;
            _userScrolledCurrentLine = false;

            if (currentLineScroll)
                SnapCurrentLineToTopNextFrame();
        }

        _typing = StartCoroutine(CoType(
            _currentLineFull,
            onUpdate: (revealedCount) =>
            {
                if (!currentLineText) return;

                currentLineText.text = BuildGreyAndReveal(_currentLineFull, revealedCount, spokenColor);
                UpdateCurrentLineFollowTarget(revealedCount);
            },
            onDone: () =>
            {
                PushHistoryLine(speaker, _currentLineFull);

                _isTyping = false;
                _typing = null;

                var cb = _pendingOnDone;
                _pendingOnDone = null;
                _pendingSpeaker = null;

                cb?.Invoke();
            }
        ));
    }

    private IEnumerator CoType(string text, System.Action<int> onUpdate, System.Action onDone)
    {
        _isTyping = true;

        float delay = (charsPerSecond <= 0f) ? 0f : 1f / charsPerSecond;
        int len = text?.Length ?? 0;

        for (int i = 0; i < len; i++)
        {
            onUpdate?.Invoke(i + 1);
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            else yield return null;
        }

        onUpdate?.Invoke(len);

        float pause = _currentTypingIsPlayer ? playerPostDelay : npcPostDelay;
        if (pause > 0f) yield return new WaitForSecondsRealtime(pause);

        onDone?.Invoke();
    }

    private void SkipTyping()
    {
        if (!_isTyping) return;

        StopTyping();

        Color spokenColor = _currentTypingIsPlayer ? playerSpokenColor : npcSpokenColor;

        if (currentLineText)
        {
            currentLineText.text = BuildGreyAndReveal(_currentLineFull, _currentLineFull.Length, spokenColor);
            UpdateCurrentLineFollowTarget(_currentLineFull.Length);
        }

        PushHistoryLine(_pendingSpeaker ?? "NPC", _currentLineFull);

        _isTyping = false;

        if (_postDelay != null) StopCoroutine(_postDelay);
        _postDelay = StartCoroutine(CoPostDelayThenInvoke());
    }

    private IEnumerator CoPostDelayThenInvoke()
    {
        float pause = _currentTypingIsPlayer ? playerPostDelay : npcPostDelay;
        if (pause > 0f) yield return new WaitForSecondsRealtime(pause);

        var cb = _pendingOnDone;
        _pendingOnDone = null;
        _pendingSpeaker = null;

        cb?.Invoke();
        _postDelay = null;
    }

    private void StopTyping()
    {
        if (_typing != null) { StopCoroutine(_typing); _typing = null; }
        _isTyping = false;

        if (_postDelay != null) { StopCoroutine(_postDelay); _postDelay = null; }
    }

    // ===== History =====

    private void PushHistoryLine(string speaker, string line)
    {
        string s = $"{speaker}: {line}";

        _history.Enqueue(s);
        while (_history.Count > Mathf.Max(1, maxHistoryLines))
            _history.Dequeue();

        _historyJoined = string.Join("\n", _history);

        if (historyText) historyText.text = _historyJoined;
        ForceHistoryScrollToBottom();
    }

    private void ForceHistoryScrollToBottom()
    {
        if (!historyScrollRect) return;
        Canvas.ForceUpdateCanvases();
        historyScrollRect.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
    }

    private void ClearHistory()
    {
        _history.Clear();
        _historyJoined = "";
        if (historyText) historyText.text = "";
        if (currentLineText) currentLineText.text = "";
    }

    // ===== Current line scroll logic (teleprompter) =====

    private void SnapCurrentLineToTopNextFrame()
    {
        if (!currentLineScroll || !currentLineText) return;
        StartCoroutine(CoSnapCurrentLineToTop());
    }

    private IEnumerator CoSnapCurrentLineToTop()
    {
        yield return null; // poczekaj 1 frame

        currentLineText.ForceMeshUpdate();

        Canvas.ForceUpdateCanvases();
        if (currentLineScroll.content)
            LayoutRebuilder.ForceRebuildLayoutImmediate(currentLineScroll.content);
        Canvas.ForceUpdateCanvases();

        currentLineScroll.verticalNormalizedPosition = 1f;
        _currentLineTargetNorm = 1f;
        Canvas.ForceUpdateCanvases();
    }

    private void UpdateCurrentLineFollowTarget(int revealedCount)
    {
        if (!followToBottomOnOverflow) return;
        if (!currentLineScroll || !currentLineText) return;
        if (_userScrolledCurrentLine) return;

        currentLineText.ForceMeshUpdate();
        var ti = currentLineText.textInfo;
        if (ti == null || ti.lineCount <= 0)
        {
            _currentLineTargetNorm = 1f;
            _topLineIndex = 0;
            return;
        }

        if (revealedCount <= 0)
        {
            _currentLineTargetNorm = 1f;
            _topLineIndex = 0;
            return;
        }

        int charIndex = Mathf.Clamp(revealedCount - 1, 0, ti.characterCount - 1);
        int lastLine = ti.characterInfo[charIndex].lineNumber;

        float viewH = ((RectTransform)currentLineScroll.viewport).rect.height;

        // policz ile linii mieści się w viewport od aktualnego _topLineIndex
        int visibleLines = 0;
        float sum = 0f;

        for (int i = _topLineIndex; i < ti.lineCount; i++)
        {
            var li = ti.lineInfo[i];
            float lineH = (li.ascender - li.descender);
            if (lineH <= 0.01f) lineH = currentLineText.fontSize;

            if (visibleLines == 0 || sum + lineH <= viewH + 0.5f)
            {
                sum += lineH;
                visibleLines++;
            }
            else break;
        }

        visibleLines = Mathf.Max(1, visibleLines);

        int bottomIndexAllowed = _topLineIndex + visibleLines - 1;

        if (lastLine > bottomIndexAllowed)
        {
            int context = Mathf.Clamp(keepContextLines, 0, visibleLines - 1);
            _topLineIndex = lastLine - (visibleLines - 1 - context);
            _topLineIndex = Mathf.Clamp(_topLineIndex, 0, Mathf.Max(0, ti.lineCount - 1));
        }

        // mapuj topLineIndex na normalized scroll
        RebuildCurrentLineLayout();

        float contentH = currentLineScroll.content.rect.height;
        float maxScroll = Mathf.Max(0f, contentH - viewH);

        if (maxScroll <= 0.001f)
        {
            _currentLineTargetNorm = 1f;
            return;
        }

        float yTop0 = ti.lineInfo[0].ascender;
        float yTopLine = ti.lineInfo[_topLineIndex].ascender;

        float offset = Mathf.Max(0f, yTop0 - yTopLine);
        offset = Mathf.Clamp(offset, 0f, maxScroll);

        _currentLineTargetNorm = 1f - (offset / maxScroll);
    }

    private void RebuildCurrentLineLayout()
    {
        if (!currentLineScroll) return;

        if (currentLineText) currentLineText.ForceMeshUpdate();

        Canvas.ForceUpdateCanvases();
        if (currentLineScroll.content)
            LayoutRebuilder.ForceRebuildLayoutImmediate(currentLineScroll.content);
        Canvas.ForceUpdateCanvases();
    }

    // ===== Options UI =====

    private void ShowOptions(List<DialogueOption> options)
    {
        if (wheelRoot) wheelRoot.SetActive(true);

        if (options == null) options = new List<DialogueOption>();

        if (optionsRoot == null && wheelRoot != null)
            optionsRoot = wheelRoot.GetComponent<RectTransform>();

        if (optionsRoot == null || optionButtonPrefab == null)
        {
            Debug.LogWarning("[DIALOGUE] optionsRoot lub optionButtonPrefab nie są ustawione.");
            return;
        }

        ClearSpawnedOptions();

        float GetRingRadius(int ringIndex)
        {
            if (ringIndex == 0) return ringRadius1;
            if (ringIndex == 1) return ringRadius2;
            return ringRadius2 + (ringIndex - 1) * ringRadiusStep;
        }

        for (int i = 0; i < options.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(options[i].playerText))
                continue;

            int ring = i / slotsPerRing;
            int slot = i % slotsPerRing;

            float angle;
            if (options.Count <= 4 && slot < SLOT_ANGLES_4.Length) angle = SLOT_ANGLES_4[slot];
            else if (options.Count <= 8 && slot < SLOT_ANGLES_8.Length) angle = SLOT_ANGLES_8[slot];
            else
            {
                float step = 360f / slotsPerRing;
                angle = startAngleDeg + slot * step * (clockwise ? -1f : 1f);
            }

            var btn = Instantiate(optionButtonPrefab, optionsRoot);
            _spawnedOptionButtons.Add(btn);

            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp)
            {
                tmp.text = options[i].playerText;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.overflowMode = TextOverflowModes.Overflow;

                float maxTextW = optionMaxWidth - optionPaddingX;
                float minTextW = optionMinWidth - optionPaddingX;

                Vector2 pref = tmp.GetPreferredValues(tmp.text, maxTextW, 0f);

                float textW = Mathf.Clamp(pref.x, minTextW, maxTextW);
                float bubbleW = textW + optionPaddingX;
                float bubbleH = pref.y + optionPaddingY;

                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleW);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleH);

                var tmpRT = tmp.rectTransform;
                tmpRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textW);
                tmpRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, pref.y);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            float rad = angle * Mathf.Deg2Rad;
            float baseRadius = GetRingRadius(ring);

            float halfW = rt.rect.width * 0.5f;
            float halfH = rt.rect.height * 0.5f;
            float halfDiag = Mathf.Sqrt(halfW * halfW + halfH * halfH);
            float radius = Mathf.Max(baseRadius, centerClearance + halfDiag);

            rt.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            Vector2 pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            rt.anchoredPosition = pos;

            Vector2 dir = pos.sqrMagnitude > 0.0001f ? pos.normalized : Vector2.up;
            PushOutUntilNoOverlap(rt, dir, radius);

            int capturedIndex = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnOptionClicked(capturedIndex));

            btn.gameObject.SetActive(true);
        }
    }

    private void HideOptions()
    {
        if (wheelRoot) wheelRoot.SetActive(false);
        ClearSpawnedOptions();
    }

    private void ClearSpawnedOptions()
    {
        for (int i = 0; i < _spawnedOptionButtons.Count; i++)
        {
            if (_spawnedOptionButtons[i] != null)
                Destroy(_spawnedOptionButtons[i].gameObject);
        }
        _spawnedOptionButtons.Clear();
    }

    // ===== Root =====

    private void ShowRoot(bool v)
    {
        if (!root) { gameObject.SetActive(v); return; }

        root.alpha = v ? 1f : 0f;
        root.interactable = v;
        root.blocksRaycasts = v;

        // 🔥 klucz: gdy chowasz dialog, wyłącz GO żeby Update nie pracował i nie łapał inputu
        root.gameObject.SetActive(v);
    }

    private void HideImmediate() => ShowRoot(false);

    // ===== Helpers =====

    private bool IsPointerOverRect(RectTransform rt)
    {
        if (!rt) return false;

        var canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, cam);
    }

    private static string ColorToHex(Color c) => ColorUtility.ToHtmlStringRGBA(c);

    private static string EscapeTmp(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private string BuildGreyAndReveal(string fullText, int revealedChars, Color revealColor)
    {
        string safe = EscapeTmp(fullText ?? "");
        revealedChars = Mathf.Clamp(revealedChars, 0, safe.Length);

        string a = safe.Substring(0, revealedChars);
        string b = safe.Substring(revealedChars);

        string revealHex = ColorToHex(revealColor);
        string greyHex = ColorToHex(unspokenColor);

        return $"<color=#{revealHex}>{a}</color><color=#{greyHex}>{b}</color>";
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


    private static bool OverlapsInRootSpace(RectTransform a, RectTransform b, RectTransform root, float padding = 6f)
    {
        Vector3[] ca = new Vector3[4];
        Vector3[] cb = new Vector3[4];
        a.GetWorldCorners(ca);
        b.GetWorldCorners(cb);

        for (int i = 0; i < 4; i++)
        {
            ca[i] = root.InverseTransformPoint(ca[i]);
            cb[i] = root.InverseTransformPoint(cb[i]);
        }

        Rect ra = new Rect(ca[0].x, ca[0].y, ca[2].x - ca[0].x, ca[2].y - ca[0].y);
        Rect rb = new Rect(cb[0].x, cb[0].y, cb[2].x - cb[0].x, cb[2].y - cb[0].y);

        ra.xMin -= padding; ra.xMax += padding;
        ra.yMin -= padding; ra.yMax += padding;

        return ra.Overlaps(rb);
    }

    private void PushOutUntilNoOverlap(RectTransform rt, Vector2 dirNorm, float startRadius, float stepRadius = 18f, int maxIters = 20)
    {
        if (!optionsRoot) return;

        float r = startRadius;

        for (int it = 0; it < maxIters; it++)
        {
            bool hit = false;
            for (int i = 0; i < _spawnedOptionButtons.Count; i++)
            {
                var otherBtn = _spawnedOptionButtons[i];
                if (!otherBtn) continue;

                var other = otherBtn.GetComponent<RectTransform>();
                if (!other || other == rt) continue;

                if (OverlapsInRootSpace(rt, other, optionsRoot, 8f))
                {
                    hit = true;
                    break;
                }
            }

            if (!hit) return;

            r += stepRadius;
            rt.anchoredPosition = dirNorm * r;
            Canvas.ForceUpdateCanvases();
        }
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
                ShowRoot(true);

                MouseLook.IsLookLocked = true;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;

                PlayerMovement.IsMovementLocked = true;

                var wm = FindFirstObjectByType<WeaponManager>();
                if (wm) wm.enabled = false;

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
        ShowRoot(true);

        // utrzymaj locki jak masz
        MouseLook.IsLookLocked = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        PlayerMovement.IsMovementLocked = true;

        var wm = FindFirstObjectByType<WeaponManager>();
        if (wm) wm.enabled = false;

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
        if (string.IsNullOrWhiteSpace(line)) return;

        MouseLook.IsLookLocked = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        PlayerMovement.IsMovementLocked = true;

        var wm = FindFirstObjectByType<WeaponManager>();
        if (wm) wm.enabled = false;

        IsOpen = true;
        ShowRoot(true);
        HideOptions();

        _npcName = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName;
        _graph = null;
        _node = null;

        ClearHistory();

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
}
