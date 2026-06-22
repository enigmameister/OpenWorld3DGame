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
    [SerializeField] private float currentLinePaddingBottom = 30f;
    [SerializeField] private float currentLineMinExtraHeight = 5f;
    [SerializeField] private float manualScrollStep = 0.12f;

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

    private readonly List<Button> spawnedButtons = new();
    private readonly Queue<string> historyLines = new();

    private Coroutine typingCoroutine;

    private string fullCurrentText = "";
    private bool isTyping;
    private bool currentLineIsPlayer;
    private Action pendingTypeDone;

    private float currentLineTargetNorm = 1f;
    private int topLineIndex = 0;
    private bool userLockedCurrentLineScroll = false;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (currentLineText != null)
            currentLineText.richText = true;

        if (historyText != null)
            historyText.richText = true;

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

        HandleManualCurrentLineScroll();
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
    }

    public void CloseWindow(bool unlockPlayer = true)
    {
        if (!IsOpen)
            return;

        StopTyping();

        IsOpen = false;

        HideOptions();

        if (root != null)
            root.SetActive(false);

        if (unlockPlayer)
            UnlockPlayer();

        Closed?.Invoke();
    }

    public void CloseWindowImmediate()
    {
        StopTyping();

        IsOpen = false;

        HideOptions();

        if (root != null)
            root.SetActive(false);
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

            Button button = Instantiate(optionButtonPrefab, optionsRoot);
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

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = text;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelected?.Invoke(capturedIndex));

            spawnedButtons.Add(button);
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

        float delay = 1f / Mathf.Max(1f, charactersPerSecond);
        Color spokenColor = GetSpokenColor(isPlayerLine);

        for (int i = 0; i <= text.Length; i++)
        {
            if (currentLineText != null)
            {
                currentLineText.text = BuildColoredReveal(text, i, spokenColor);
                RefreshCurrentLineContentSize();
                UpdateCurrentLineFollowTarget(i);
            }

            yield return new WaitForSecondsRealtime(delay);
        }

        isTyping = false;
        typingCoroutine = null;

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

        if (currentLineScroll.content == null)
            return;

        if (currentLineText == null)
            return;

        RectTransform textRt = currentLineText.rectTransform;
        RectTransform content = currentLineScroll.content;

        RectTransform viewport = currentLineScroll.viewport != null
            ? currentLineScroll.viewport
            : currentLineScroll.GetComponent<RectTransform>();

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

        float preferredHeight = currentLineText.GetPreferredValues(
            currentLineText.text,
            contentWidth,
            0f
        ).y;

        preferredHeight += currentLinePaddingBottom;

        float finalHeight = Mathf.Max(
            viewportHeight + currentLineMinExtraHeight,
            preferredHeight
        );

        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);
        textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);

        Canvas.ForceUpdateCanvases();
    }

    private void UpdateCurrentLineFollowTarget(int revealedCount)
    {
        if (!autoScrollCurrentLine)
            return;

        if (userLockedCurrentLineScroll)
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

        int charIndex = Mathf.Clamp(revealedCount - 1, 0, textInfo.characterCount - 1);
        int currentLineIndex = textInfo.characterInfo[charIndex].lineNumber;

        RectTransform viewport = currentLineScroll.viewport != null
            ? currentLineScroll.viewport
            : currentLineScroll.GetComponent<RectTransform>();

        if (viewport == null)
            return;

        float viewportHeight = viewport.rect.height;

        int visibleLines = 0;
        float usedHeight = 0f;

        for (int i = topLineIndex; i < textInfo.lineCount; i++)
        {
            TMP_LineInfo lineInfo = textInfo.lineInfo[i];

            float lineHeight = lineInfo.ascender - lineInfo.descender;

            if (lineHeight <= 0.01f)
                lineHeight = currentLineText.fontSize;

            if (visibleLines == 0 || usedHeight + lineHeight <= viewportHeight)
            {
                usedHeight += lineHeight;
                visibleLines++;
            }
            else
            {
                break;
            }
        }

        visibleLines = Mathf.Max(1, visibleLines);

        int bottomVisibleLine = topLineIndex + visibleLines - 1;

        if (currentLineIndex > bottomVisibleLine)
        {
            topLineIndex = currentLineIndex - visibleLines + 1;
            topLineIndex = Mathf.Clamp(topLineIndex, 0, Mathf.Max(0, textInfo.lineCount - 1));
        }

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

        float firstLineTop = textInfo.lineInfo[0].ascender;
        float targetLineTop = textInfo.lineInfo[topLineIndex].ascender;

        float offsetFromTop = Mathf.Max(0f, firstLineTop - targetLineTop);
        offsetFromTop = Mathf.Clamp(offsetFromTop, 0f, maxScroll);

        currentLineTargetNorm = 1f - (offsetFromTop / maxScroll);
    }

    private void UpdateCurrentLineSmoothScroll()
    {
        if (!autoScrollCurrentLine || currentLineScroll == null)
            return;

        if (userLockedCurrentLineScroll)
            return;

        float current = currentLineScroll.verticalNormalizedPosition;
        float t = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);

        currentLineScroll.verticalNormalizedPosition =
            Mathf.Lerp(current, currentLineTargetNorm, t);
    }

    private void HandleManualCurrentLineScroll()
    {
        if (currentLineScroll == null)
            return;

        if (currentLineScroll.viewport == null)
            return;

        float wheel = Input.mouseScrollDelta.y;

        if (Mathf.Abs(wheel) < 0.01f)
            return;

        if (!IsPointerOverRect(currentLineScroll.viewport))
            return;

        RefreshCurrentLineContentSize();

        userLockedCurrentLineScroll = true;

        currentLineScroll.verticalNormalizedPosition =
            Mathf.Clamp01(currentLineScroll.verticalNormalizedPosition + wheel * manualScrollStep);

        currentLineTargetNorm = currentLineScroll.verticalNormalizedPosition;
    }

    private bool IsPointerOverRect(RectTransform rect)
    {
        if (rect == null)
            return false;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(
            rect,
            Input.mousePosition,
            cam
        );
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
        for (int i = spawnedButtons.Count - 1; i >= 0; i--)
        {
            if (spawnedButtons[i] != null)
                Destroy(spawnedButtons[i].gameObject);
        }

        spawnedButtons.Clear();
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

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (disableWeaponManagerDuringDialogue)
        {
            WeaponManager wm = FindFirstObjectByType<WeaponManager>();
            if (wm != null)
                wm.enabled = false;
        }
    }

    private void UnlockPlayer()
    {
        MouseLook.IsLookLocked = false;
        PlayerMovement.IsMovementLocked = false;
        PlayerInputHandler.SetGameplayBlocked(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        WeaponManager wm = FindFirstObjectByType<WeaponManager>();
        if (wm != null)
            wm.enabled = true;
    }
}