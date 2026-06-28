using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DialogueWindowUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Main Text")]
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text currentLineText;

    [Header("Scroll / Current Line")]
    [SerializeField] private ScrollRect currentLineScroll;
    [SerializeField] private bool autoScrollCurrentLine = true;
    [SerializeField] private float currentLinePaddingBottom = 0f;

    [Header("Scroll / Current Line - Bar")]
    [SerializeField] private Scrollbar currentLineScrollbar;

    private bool currentLineScrollbarHeld = false;
    private Coroutine resumeAutoScrollCoroutine;
    private int currentRevealedCount = 0;

    [Header("History Optional")]
    [SerializeField] private TMP_Text historyText;
    [SerializeField] private ScrollRect historyScroll;
    [SerializeField] private int maxHistoryLines = 4;
    [SerializeField] private Color historyColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("Typewriter")]
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private float charactersPerSecond = 42f;
    [SerializeField] private bool allowSkipTypewriter = true;

    [Header("Typing Colors")]
    [SerializeField] private Color unspokenColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color npcSpokenColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color playerSpokenColor = new Color(1f, 0.9f, 0.2f, 1f);

    [Header("Wheel / Options")]
    [SerializeField] private GameObject wheelRoot;
    [SerializeField] private RectTransform optionsRoot;
    [SerializeField] private Button optionButtonPrefab;

    [Header("Option Placement")]
    [SerializeField] private float optionHorizontalDistance = 180f;
    [SerializeField] private float optionVerticalDistance = 95f;
    [SerializeField] private float optionRingRadius = 190f;

    [Header("Player Lock")]
    [SerializeField] private bool lockPlayerDuringDialogue = true;
    [SerializeField] private bool disableWeaponManagerDuringDialogue = true;

    public bool IsOpen { get; private set; }
    public bool IsTyping => isTyping;

    public event Action Closed;

    private readonly Queue<string> historyLines = new();

    private Coroutine typingCoroutine;

    private string fullCurrentText = "";
    private bool isTyping;
    private bool currentLineIsPlayer;
    private Action pendingTypeDone;

    private float currentLineTargetNorm = 1f;
    private int topLineIndex = 0;
    private bool userLockedCurrentLineScroll = false;

    private const int LayoutRefreshEveryChars = 4;

    private RectTransform cachedCurrentLineTextRect;
    private RectTransform cachedCurrentLineContentRect;
    private RectTransform cachedCurrentLineViewportRect;

    private WeaponManager cachedWeaponManager;

    private readonly List<Button> pooledButtons = new();
    private readonly List<TMP_Text> pooledButtonLabels = new();

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (currentLineText != null)
        {
            currentLineText.richText = true;
            cachedCurrentLineTextRect = currentLineText.rectTransform;
        }

        if (currentLineScroll != null)
        {
            cachedCurrentLineContentRect = currentLineScroll.content;

            cachedCurrentLineViewportRect = currentLineScroll.viewport != null
                ? currentLineScroll.viewport
                : currentLineScroll.GetComponent<RectTransform>();
        }

        if (historyText != null)
            historyText.richText = true;

        cachedWeaponManager = FindFirstObjectByType<WeaponManager>();

        ConfigureCurrentLineScrollbar();
        CloseWindowImmediate();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (isTyping && allowSkipTypewriter)
        {
            bool clickSkip = Input.GetMouseButtonDown(0);

            if (clickSkip && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                clickSkip = false;

            bool skipPressed =
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                clickSkip ||
                (PlayerInputHandler.Instance != null && PlayerInputHandler.Instance.InteractPressed);

            if (skipPressed)
                FinishTypewriterInstant();
        }

        UpdateCurrentLineSmoothScroll();
    }

    public void OpenWindow(bool clearHistory = true, bool lockPlayer = true)
    {
        IsOpen = true;

        if (root != null)
            root.SetActive(true);

        HideOptions();

        if (clearHistory)
            ClearHistory();

        if (lockPlayer)
            LockPlayer();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void CloseWindow(bool unlockPlayer = true)
    {
        bool wasOpen = IsOpen;

        StopTyping();

        IsOpen = false;

        HideOptions();

        if (root != null)
            root.SetActive(false);

        if (unlockPlayer)
            UnlockPlayer();

        if (wasOpen)
            Closed?.Invoke();
    }

    public void CloseWindowImmediate(bool unlockPlayer = false)
    {
        StopTyping();

        IsOpen = false;

        HideOptions();

        if (root != null)
            root.SetActive(false);

        if (unlockPlayer)
            UnlockPlayer();
    }

    public void ClearHistory()
    {
        historyLines.Clear();

        if (historyText != null)
            historyText.text = "";

        if (currentLineText != null)
            currentLineText.text = "";
    }

    public void TypeLine(string speaker, string line, bool isPlayerLine, Action onDone)
    {
        StopTyping();

        fullCurrentText = line ?? "";
        currentLineIsPlayer = isPlayerLine;
        pendingTypeDone = onDone;
        currentRevealedCount = 0;

        userLockedCurrentLineScroll = false;
        currentLineScrollbarHeld = false;

        if (resumeAutoScrollCoroutine != null)
        {
            StopCoroutine(resumeAutoScrollCoroutine);
            resumeAutoScrollCoroutine = null;
        }

        if (speakerNameText != null)
            speakerNameText.text = string.IsNullOrWhiteSpace(speaker) ? "" : speaker + ":";

        Color spokenColor = GetSpokenColor(isPlayerLine);

        if (currentLineText != null)
        {
            currentLineText.text = BuildColoredReveal(fullCurrentText, 0, spokenColor);
            ResetCurrentLineScroll();
        }

        if (!useTypewriter)
        {
            if (currentLineText != null)
            {
                currentLineText.text = BuildColoredReveal(
                    fullCurrentText,
                    fullCurrentText.Length,
                    spokenColor
                );

                RefreshCurrentLineContentSize();
                UpdateCurrentLineFollowTarget(fullCurrentText.Length);
            }

            PushHistoryLine(speaker, fullCurrentText);
            pendingTypeDone = null;
            onDone?.Invoke();
            return;
        }

        typingCoroutine = StartCoroutine(TypewriterRoutine(speaker, fullCurrentText, isPlayerLine));
    }

    public void ShowOptions(List<string> optionTexts, Action<int> onSelected)
    {
        HideOptions();

        if (optionTexts == null || optionTexts.Count <= 0)
            return;

        if (wheelRoot != null)
        {
            wheelRoot.SetActive(true);
            SetChildrenActiveRecursive(wheelRoot.transform, true);
        }

        if (optionsRoot == null)
        {
            Debug.LogWarning("[DialogueWindowUI] OptionsRoot missing.");
            return;
        }

        if (optionButtonPrefab == null)
        {
            Debug.LogWarning("[DialogueWindowUI] OptionButtonPrefab missing.");
            return;
        }

        optionsRoot.gameObject.SetActive(true);

        int visibleIndex = 0;
        int visibleCount = 0;

        for (int i = 0; i < optionTexts.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(optionTexts[i]))
                visibleCount++;
        }

        for (int i = 0; i < optionTexts.Count; i++)
        {
            string text = optionTexts[i];

            if (string.IsNullOrWhiteSpace(text))
                continue;

            int capturedIndex = i;

            Button button = GetOptionButtonFromPool(visibleIndex);
            button.gameObject.SetActive(true);

            RectTransform rt = button.GetComponent<RectTransform>();

            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = GetOptionPosition(visibleIndex, visibleCount);
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }

            TMP_Text label = pooledButtonLabels[visibleIndex];

            if (label != null)
            {
                label.text = text;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelected?.Invoke(capturedIndex));


            visibleIndex++;
        }
    }

    public void HideOptions()
    {
        if (wheelRoot != null)
            wheelRoot.SetActive(false);

        if (optionsRoot != null)
            optionsRoot.gameObject.SetActive(false);

        ClearSpawnedOptions();
    }

    public void FinishTypewriterInstant()
    {
        if (!isTyping)
            return;

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
        currentRevealedCount = fullCurrentText.Length;

        if (currentLineText != null)
        {
            currentLineText.text = BuildColoredReveal(
                fullCurrentText,
                fullCurrentText.Length,
                GetSpokenColor(currentLineIsPlayer)
            );

            RefreshCurrentLineContentSize();
            UpdateCurrentLineFollowTarget(fullCurrentText.Length);
        }

        PushHistoryLine(currentLineIsPlayer ? "PLAYER" : GetCurrentSpeakerWithoutColon(), fullCurrentText);

        Action done = pendingTypeDone;
        pendingTypeDone = null;

        done?.Invoke();
    }

    private IEnumerator TypewriterRoutine(string speaker, string text, bool isPlayerLine)
    {
        isTyping = true;

        int len = text?.Length ?? 0;
        Color spokenColor = GetSpokenColor(isPlayerLine);

        float charsPerFrameAccumulator = 0f;
        int revealed = 0;

        while (revealed < len)
        {
            charsPerFrameAccumulator += charactersPerSecond * Time.unscaledDeltaTime;

            int nextReveal = Mathf.Clamp(
                Mathf.FloorToInt(charsPerFrameAccumulator),
                0,
                len
            );

            if (nextReveal > revealed)
            {
                revealed = nextReveal;
                currentRevealedCount = revealed;

                if (currentLineText != null)
                {
                    currentLineText.text = BuildColoredReveal(text, revealed, spokenColor);

                    bool shouldRefreshLayout =
                        revealed >= len ||
                        revealed % LayoutRefreshEveryChars == 0;

                    if (shouldRefreshLayout)
                    {
                        RefreshCurrentLineContentSize();
                        UpdateCurrentLineFollowTarget(revealed);
                    }
                }
            }

            yield return null;
        }

        if (currentLineText != null)
        {
            currentLineText.text = BuildColoredReveal(text, len, spokenColor);
            RefreshCurrentLineContentSize();
            UpdateCurrentLineFollowTarget(len);
        }

        isTyping = false;
        typingCoroutine = null;
        currentRevealedCount = len;

        PushHistoryLine(speaker, text);

        Action done = pendingTypeDone;
        pendingTypeDone = null;

        done?.Invoke();
    }

    private void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
        pendingTypeDone = null;
    }

    private void PushHistoryLine(string speaker, string line)
    {
        if (historyText == null)
            return;

        string color = ColorUtility.ToHtmlStringRGBA(historyColor);
        string safeSpeaker = EscapeRichText(speaker);
        string safeLine = EscapeRichText(line);

        string formatted = $"<color=#{color}>{safeSpeaker}: {safeLine}</color>";

        historyLines.Enqueue(formatted);

        while (historyLines.Count > Mathf.Max(1, maxHistoryLines))
            historyLines.Dequeue();

        RefreshHistory();
    }

    private void RefreshHistory()
    {
        if (historyText == null)
            return;

        StringBuilder sb = new StringBuilder();

        foreach (string line in historyLines)
            sb.AppendLine(line);

        historyText.text = sb.ToString();

        if (historyScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            historyScroll.verticalNormalizedPosition = 0f;
        }
    }

    private void ResetCurrentLineScroll()
    {
        if (currentLineScroll == null)
            return;

        topLineIndex = 0;
        currentLineTargetNorm = 1f;
        userLockedCurrentLineScroll = false;

        RefreshCurrentLineContentSize();

        currentLineScroll.verticalNormalizedPosition = 1f;
    }

    private void RefreshCurrentLineContentSize()
    {
        if (currentLineScroll == null)
            return;

        if (currentLineText == null)
            return;

        RectTransform textRt = cachedCurrentLineTextRect;
        RectTransform content = cachedCurrentLineContentRect;
        RectTransform viewport = cachedCurrentLineViewportRect;

        if (textRt == null || content == null)
            return;

        float viewportHeight = viewport != null ? viewport.rect.height : 100f;

        float contentWidth = content.rect.width;

        if (contentWidth <= 1f && viewport != null)
            contentWidth = viewport.rect.width;

        textRt.anchorMin = new Vector2(0f, 1f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0f, 1f);
        textRt.anchoredPosition = Vector2.zero;

        textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);

        currentLineText.ForceMeshUpdate();

        TMP_TextInfo textInfo = currentLineText.textInfo;

        float preferredHeightByTMP = currentLineText.GetPreferredValues(
            currentLineText.text,
            contentWidth,
            0f
        ).y;

        float preferredHeightByLines = preferredHeightByTMP;

        float lineHeight = GetCurrentLineHeight(textInfo);

        if (textInfo != null && textInfo.lineCount > 0 && lineHeight > 0.01f)
        {
            preferredHeightByLines = textInfo.lineCount * lineHeight;
        }

        float preferredHeight = Mathf.Max(preferredHeightByTMP, preferredHeightByLines);

        preferredHeight += Mathf.Max(0f, currentLinePaddingBottom);

        float finalHeight = Mathf.Max(viewportHeight, preferredHeight);

        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);
        textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);

        Canvas.ForceUpdateCanvases();
    }

    private float GetCurrentLineHeight(TMP_TextInfo textInfo)
    {
        if (currentLineText == null)
            return 16f;

        if (textInfo == null || textInfo.lineCount <= 0)
            return Mathf.Max(1f, currentLineText.fontSize);

        TMP_LineInfo line = textInfo.lineInfo[0];

        float height = line.ascender - line.descender;

        if (height <= 0.01f)
            height = currentLineText.fontSize * 1.2f;

        return Mathf.Max(1f, height);
    }

    private void UpdateCurrentLineFollowTarget(int revealedCount)
    {
        if (!autoScrollCurrentLine)
            return;

        if (userLockedCurrentLineScroll || currentLineScrollbarHeld)
            return;

        if (currentLineScroll == null || currentLineText == null)
            return;

        currentLineText.ForceMeshUpdate();

        TMP_TextInfo textInfo = currentLineText.textInfo;

        if (textInfo == null || textInfo.lineCount <= 0)
        {
            currentLineTargetNorm = 1f;
            return;
        }

        if (revealedCount <= 0 || textInfo.characterCount <= 0)
        {
            topLineIndex = 0;
            currentLineTargetNorm = 1f;
            return;
        }

        RectTransform viewport = cachedCurrentLineViewportRect;

        if (viewport == null)
            return;

        float viewportHeight = viewport.rect.height;
        float lineHeight = GetCurrentLineHeight(textInfo);

        int visibleLines = Mathf.Max(1, Mathf.FloorToInt(viewportHeight / Mathf.Max(1f, lineHeight)));

        int charIndex = Mathf.Clamp(revealedCount - 1, 0, textInfo.characterCount - 1);
        int currentLineIndex = textInfo.characterInfo[charIndex].lineNumber;

        int maxTopLineIndex = Mathf.Max(0, textInfo.lineCount - visibleLines);

        if (currentLineIndex >= topLineIndex + visibleLines)
        {
            topLineIndex = currentLineIndex - visibleLines + 1;
        }

        topLineIndex = Mathf.Clamp(topLineIndex, 0, maxTopLineIndex);

        RefreshCurrentLineContentSize();

        RectTransform content = currentLineScroll.content;

        if (content == null)
            return;

        float contentHeight = content.rect.height;
        float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);

        if (maxScroll <= 0.001f)
        {
            currentLineTargetNorm = 1f;
            return;
        }

        if (topLineIndex >= maxTopLineIndex)
        {
            currentLineTargetNorm = 0f;
            return;
        }

        float offsetFromTop = topLineIndex * lineHeight;
        offsetFromTop = Mathf.Clamp(offsetFromTop, 0f, maxScroll);

        currentLineTargetNorm = 1f - (offsetFromTop / maxScroll);
    }

    private void UpdateCurrentLineSmoothScroll()
    {
        if (!autoScrollCurrentLine || currentLineScroll == null)
            return;

        if (userLockedCurrentLineScroll || currentLineScrollbarHeld)
            return;

        float current = currentLineScroll.verticalNormalizedPosition;
        float t = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);

        currentLineScroll.verticalNormalizedPosition =
            Mathf.Lerp(current, currentLineTargetNorm, t);
    }

    private Vector2 GetOptionPosition(int index, int count)
    {
        if (count == 1)
            return new Vector2(0f, -optionVerticalDistance);

        if (count == 2)
        {
            return index == 0
                ? new Vector2(-optionHorizontalDistance, 0f)
                : new Vector2(optionHorizontalDistance, 0f);
        }

        if (count == 3)
        {
            if (index == 0) return new Vector2(-optionHorizontalDistance, 0f);
            if (index == 1) return new Vector2(optionHorizontalDistance, 0f);
            return new Vector2(0f, -optionVerticalDistance);
        }

        if (count == 4)
        {
            if (index == 0) return new Vector2(-optionHorizontalDistance, 0f);
            if (index == 1) return new Vector2(optionHorizontalDistance, 0f);
            if (index == 2) return new Vector2(0f, optionVerticalDistance);
            return new Vector2(0f, -optionVerticalDistance);
        }

        float angle = 90f - index * (360f / Mathf.Max(1, count));
        float rad = angle * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * optionRingRadius;
    }

    private void ClearSpawnedOptions()
    {
        for (int i = 0; i < pooledButtons.Count; i++)
        {
            if (pooledButtons[i] == null)
                continue;

            pooledButtons[i].onClick.RemoveAllListeners();
            pooledButtons[i].gameObject.SetActive(false);
        }
    }

    private Color GetSpokenColor(bool isPlayerLine)
    {
        return isPlayerLine ? playerSpokenColor : npcSpokenColor;
    }

    private string BuildColoredReveal(string text, int revealedCount, Color spokenColor)
    {
        text ??= "";
        revealedCount = Mathf.Clamp(revealedCount, 0, text.Length);

        string spoken = EscapeRichText(text.Substring(0, revealedCount));
        string unspoken = EscapeRichText(text.Substring(revealedCount));

        return
            $"<color=#{ColorUtility.ToHtmlStringRGBA(spokenColor)}>{spoken}</color>" +
            $"<color=#{ColorUtility.ToHtmlStringRGBA(unspokenColor)}>{unspoken}</color>";
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private string GetCurrentSpeakerWithoutColon()
    {
        if (speakerNameText == null)
            return "NPC";

        string value = speakerNameText.text;

        if (string.IsNullOrWhiteSpace(value))
            return "NPC";

        return value.Replace(":", "").Trim();
    }

    private void SetChildrenActiveRecursive(Transform rootTransform, bool active)
    {
        if (rootTransform == null)
            return;

        for (int i = 0; i < rootTransform.childCount; i++)
        {
            Transform child = rootTransform.GetChild(i);

            if (child == null)
                continue;

            child.gameObject.SetActive(active);
            SetChildrenActiveRecursive(child, active);
        }
    }

    private void LockPlayer()
    {
        if (!lockPlayerDuringDialogue)
            return;

        MouseLook.IsLookLocked = true;
        PlayerMovement.IsMovementLocked = true;
        PlayerInputHandler.SetGameplayBlocked(true);

        if (disableWeaponManagerDuringDialogue)
        {
            if (cachedWeaponManager == null)
                cachedWeaponManager = FindFirstObjectByType<WeaponManager>();

            if (cachedWeaponManager != null)
                cachedWeaponManager.enabled = false;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void UnlockPlayer()
    {
        MouseLook.IsLookLocked = false;
        PlayerMovement.IsMovementLocked = false;
        PlayerInputHandler.SetGameplayBlocked(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (cachedWeaponManager == null)
            cachedWeaponManager = FindFirstObjectByType<WeaponManager>();

        if (cachedWeaponManager != null)
            cachedWeaponManager.enabled = true;
    }

    private Button GetOptionButtonFromPool(int index)
    {
        while (pooledButtons.Count <= index)
        {
            Button button = Instantiate(optionButtonPrefab, optionsRoot);
            button.gameObject.SetActive(false);

            pooledButtons.Add(button);
            pooledButtonLabels.Add(button.GetComponentInChildren<TMP_Text>(true));
        }

        return pooledButtons[index];
    }

    private void ConfigureCurrentLineScrollbar()
    {
        if (currentLineScroll == null)
            return;

        if (currentLineScrollbar == null)
            currentLineScrollbar = currentLineScroll.verticalScrollbar;

        if (currentLineScrollbar == null)
            return;

        currentLineScroll.verticalScrollbar = currentLineScrollbar;

        EventTrigger trigger = currentLineScrollbar.GetComponent<EventTrigger>();

        if (trigger == null)
            trigger = currentLineScrollbar.gameObject.AddComponent<EventTrigger>();

        trigger.triggers ??= new List<EventTrigger.Entry>();

        AddScrollbarEvent(trigger, EventTriggerType.PointerDown, OnCurrentLineScrollbarDown);
        AddScrollbarEvent(trigger, EventTriggerType.PointerUp, OnCurrentLineScrollbarUp);
    }

    private void AddScrollbarEvent(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = type
        };

        entry.callback.AddListener(data => callback?.Invoke(data));
        trigger.triggers.Add(entry);
    }

    private void OnCurrentLineScrollbarDown(BaseEventData data)
    {
        currentLineScrollbarHeld = true;

        if (resumeAutoScrollCoroutine != null)
        {
            StopCoroutine(resumeAutoScrollCoroutine);
            resumeAutoScrollCoroutine = null;
        }
    }

    private void OnCurrentLineScrollbarUp(BaseEventData data)
    {
        currentLineScrollbarHeld = false;
        userLockedCurrentLineScroll = false;

        if (resumeAutoScrollCoroutine != null)
        {
            StopCoroutine(resumeAutoScrollCoroutine);
            resumeAutoScrollCoroutine = null;
        }

        // Po puszczeniu scrollbara wracamy do automatycznego œledzenia aktualnej linii.
        RefreshCurrentLineContentSize();

        int reveal = Mathf.Clamp(currentRevealedCount, 0, fullCurrentText.Length);
        UpdateCurrentLineFollowTarget(reveal);
    }
}