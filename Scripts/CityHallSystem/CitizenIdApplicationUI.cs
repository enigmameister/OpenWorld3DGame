using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CitizenIdApplicationUI : MonoBehaviour
{
    private enum Section
    {
        Name = 0,
        Variant = 1,
        Save = 2,
        Cancel = 3,
        Back = 4
    }

    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Name")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private GameObject nameSelected;

    [Header("Variants")]
    [SerializeField] private CitizenIdVariantDatabase variantDatabase;

    [SerializeField] private RectTransform variantPickerContainer;
    [SerializeField] private VariantSlotView variantSlotPrefab;

    [SerializeField] private GameObject variantSelected;

    private readonly List<VariantSlotView> variantSlots = new();

    [Header("Save")]
    [SerializeField] private Button saveButton;
    [SerializeField] private GameObject saveSelected;
    [SerializeField] private GameObject saveBlocked;
    [SerializeField] private TMP_Text saveText;

    [Header("Cancel")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private GameObject cancelSelected;

    [Header("Back")]
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject backSelected;

    [Header("Processing")]
    [SerializeField] private TMP_Text processingText;
    [SerializeField] private TMP_Text successText;
    [SerializeField, Min(0f)] private float processingDuration = 5f;
    [SerializeField, Min(0.05f)] private float dotsInterval = 0.35f;

    [Header("Validation")]
    [SerializeField, Range(1, 24)] private int minNameLength = 3;
    [SerializeField, Range(3, 24)] private int maxNameLength = 24;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    public bool IsOpen { get; private set; }

    private Section currentSection = Section.Name;

    private int hoveredVariantIndex;
    private int selectedVariantIndex = -1;

    private bool processing;
    private bool completed;

    private Coroutine processingCoroutine;

    private CityHallDialogueUI dialogueOwner;

    private bool HasValidName =>
        TryNormalizeAndValidateName(
            nameInput != null ? nameInput.text : string.Empty,
            out _
        );

    private bool HasSelectedVariant =>
        selectedVariantIndex >= 0 &&
        variantDatabase != null &&
        selectedVariantIndex < variantDatabase.Count;

    private bool CanSave =>
        !processing &&
        !completed &&
        HasValidName &&
        HasSelectedVariant;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (canvasGroup == null && root != null)
            canvasGroup = root.GetComponent<CanvasGroup>();

        BuildVariantSlots();

        if (nameInput != null)
        {
            nameInput.characterLimit = maxNameLength;
            nameInput.onValueChanged.AddListener(
                HandleNameValueChanged
            );
        }

        saveButton?.onClick.AddListener(TryStartProcessing);
        cancelButton?.onClick.AddListener(CancelForm);
        backButton?.onClick.AddListener(CloseAndReturn);

        CloseImmediate();
    }

    private void OnDestroy()
    {
        if (nameInput != null)
        {
            nameInput.onValueChanged.RemoveListener(
                HandleNameValueChanged
            );
        }
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (EscapePressedThisFrame())
        {
            if (!processing)
                CloseAndReturn();

            return;
        }

        if (processing)
            return;

        if (nameInput != null && nameInput.isFocused)
        {
            if (DownPressedThisFrame() || EnterPressedThisFrame())
            {
                ConfirmNameInput();
                return;
            }

            if (UpPressedThisFrame())
            {
                nameInput.DeactivateInputField();
                currentSection = Section.Back;
                RefreshAllVisuals();
                return;
            }

            return;
        }

        if (UpPressedThisFrame())
            MoveSection(-1);

        if (DownPressedThisFrame())
            MoveSection(1);

        if (currentSection == Section.Variant)
        {
            if (LeftPressedThisFrame())
                MoveVariant(-1);

            if (RightPressedThisFrame())
                MoveVariant(1);
        }

        if (EnterPressedThisFrame())
            ActivateCurrentSection();
    }

    // =========================================================
    // OPEN / CLOSE
    // =========================================================

    public void Open(CityHallDialogueUI owner)
    {
        dialogueOwner = owner;

        ResetForm();

        IsOpen = true;

        if (root != null)
            root.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        PlayerMovement.IsMovementLocked = true;
        MouseLook.IsLookLocked = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        currentSection = Section.Name;

        RefreshAllVisuals();

        if (debugLogs)
            Debug.Log("[CITIZEN ID UI] Opened.");
    }

    public void CloseAndReturn()
        {
            if (processing)
            return;

        CloseInternal();

        if (dialogueOwner != null)
            dialogueOwner.ResumeAfterCitizenIdApplication();

        dialogueOwner = null;
    }

    private void CloseImmediate()
    {
        IsOpen = false;

        if (root != null)
            root.SetActive(false);
    }

    private void CloseInternal()
    {
        StopProcessingCoroutine();

        IsOpen = false;

        if (nameInput != null)
        {
            nameInput.DeactivateInputField();
            nameInput.text = string.Empty;
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (root != null)
            root.SetActive(false);

        ResetForm();

        if (debugLogs)
            Debug.Log("[CITIZEN ID UI] Closed and reset.");
    }

    // =========================================================
    // NAVIGATION
    // =========================================================

    private void MoveSection(int direction)
    {
        int sectionCount =
            System.Enum.GetValues(typeof(Section)).Length;

        int next =
            ((int)currentSection + direction + sectionCount) %
            sectionCount;

        currentSection = (Section)next;

        RefreshSectionVisuals();
    }

    private void ActivateCurrentSection()
    {
        switch (currentSection)
        {
            case Section.Name:
                BeginNameInput();
                break;

            case Section.Variant:
                ConfirmHoveredVariant();
                break;

            case Section.Save:
                TryStartProcessing();
                break;

            case Section.Cancel:
                CancelForm();
                break;

            case Section.Back:
                CloseAndReturn();
                break;
        }
    }

    private void BeginNameInput()
    {
        if (nameInput == null)
            return;

        nameInput.ActivateInputField();
        nameInput.Select();
        nameInput.caretPosition = nameInput.text.Length;
    }

    private void ConfirmNameInput()
    {
        if (nameInput == null)
            return;

        nameInput.DeactivateInputField();

        if (!TryNormalizeAndValidateName(
                nameInput.text,
                out string normalized))
        {
            currentSection = Section.Name;
            RefreshAllVisuals();
            return;
        }

        nameInput.SetTextWithoutNotify(normalized);

        currentSection = HasSelectedVariant
            ? Section.Save
            : Section.Variant;

        RefreshAllVisuals();
    }

    // =========================================================
    // VARIANTS
    // =========================================================

    private void MoveVariant(int direction)
    {
        int count = GetAvailableVariantCount();

        if (count <= 0)
            return;

        hoveredVariantIndex =
            (hoveredVariantIndex + direction + count) % count;

        RefreshVariantVisuals();
    }

    private void ConfirmHoveredVariant()
    {
        int count = GetAvailableVariantCount();

        if (count <= 0)
            return;

        selectedVariantIndex =
            Mathf.Clamp(hoveredVariantIndex, 0, count - 1);

        if (HasValidName)
            currentSection = Section.Save;
        else
            currentSection = Section.Name;

        RefreshAllVisuals();
    }

    private int GetAvailableVariantCount()
    {
        int generatedCount = variantSlots.Count;

        int databaseCount =
            variantDatabase != null
                ? variantDatabase.Count
                : 0;

        return Mathf.Min(generatedCount, databaseCount);
    }

    private void RefreshVariantVisuals()
    {
        int count = variantSlots.Count;

        for (int i = 0; i < count; i++)
        {
            VariantSlotView slot = variantSlots[i];

            if (slot == null)
                continue;

            bool isHovered =
                currentSection == Section.Variant &&
                i == hoveredVariantIndex;

            bool isSelected =
                i == selectedVariantIndex;

            // Strza³ka oznacza aktualnie nawigowany wariant.
            slot.SetHover(isHovered);

            // Border oznacza wariant zatwierdzony Enterem.
            slot.SetSelected(
                isSelected,
                highlighted: isSelected
            );

            if (variantDatabase != null &&
                i < variantDatabase.Count)
            {
                slot.SetPreviewColor(
                    variantDatabase.Get(i)
                );
            }
        }
    }

    // =========================================================
    // SAVE / PROCESS
    // =========================================================

    private void TryStartProcessing()
    {
        if (processing || completed)
            return;

        if (!HasValidName)
        {
            currentSection = Section.Name;
            RefreshAllVisuals();
            return;
        }

        if (!HasSelectedVariant)
        {
            currentSection = Section.Variant;
            RefreshAllVisuals();
            return;
        }

        if (!TryNormalizeAndValidateName(
                nameInput.text,
                out string normalizedName))
        {
            currentSection = Section.Name;
            RefreshAllVisuals();
            return;
        }

        CitizenIdApplicationService service =
            CitizenIdApplicationService.Instance;

        if (service == null)
        {
            service = FindFirstObjectByType<CitizenIdApplicationService>(
                FindObjectsInactive.Include
            );
        }

        if (service == null)
        {
            Debug.LogWarning(
                "[CITIZEN ID UI] CitizenIdApplicationService missing."
            );

            return;
        }

        bool submitted = service.TrySubmit(
            normalizedName,
            selectedVariantIndex,
            out string failureReason
        );

        if (!submitted)
        {
            Debug.LogWarning(
                $"[CITIZEN ID UI] Submit failed: {failureReason}"
            );

            return;
        }

        processingCoroutine =
            StartCoroutine(ProcessingRoutine());
    }

    private IEnumerator ProcessingRoutine()
    {
        processing = true;

        if (nameInput != null)
        {
            nameInput.DeactivateInputField();
            nameInput.interactable = false;
        }

        SetNavigationEnabled(false);

        if (successText != null)
            successText.gameObject.SetActive(false);

        if (processingText != null)
            processingText.gameObject.SetActive(true);

        RefreshAllVisuals();

        float elapsed = 0f;
        float nextDotsUpdate = 0f;
        int dots = 1;

        while (elapsed < processingDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            nextDotsUpdate -= Time.unscaledDeltaTime;

            if (nextDotsUpdate <= 0f)
            {
                nextDotsUpdate = dotsInterval;

                if (processingText != null)
                {
                    processingText.text =
                        "PROCESSING" + new string('.', dots);
                }

                dots++;

                if (dots > 3)
                    dots = 1;
            }

            yield return null;
        }

        processing = false;
        completed = true;
        processingCoroutine = null;

        if (processingText != null)
            processingText.gameObject.SetActive(false);

        if (successText != null)
        {
            successText.text = "SUCCESSFUL";
            successText.gameObject.SetActive(true);
        }

        currentSection = Section.Back;

        SetNavigationEnabled(true);

        // Po sukcesie pozostawiamy aktywny tylko BACK.
        if (nameInput != null)
            nameInput.interactable = false;

        RefreshAllVisuals();

        CityHallVisitRegistry visits =
            CityHallVisitRegistry.Instance;

        visits?.TryCompleteVisit(
            CityHallVisitType.CitizenId
        );

        if (debugLogs)
        {
            Debug.Log(
                "[CITIZEN ID UI] Application process completed."
            );
        }
    }

    // =========================================================
    // CANCEL
    // =========================================================

    private void CancelForm()
    {
        if (processing)
            return;

        ResetForm();

        currentSection = Section.Name;
        RefreshAllVisuals();
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private bool TryNormalizeAndValidateName(
        string raw,
        out string normalized)
    {
        normalized = NormalizeSpaces(raw);

        if (normalized.Length < minNameLength ||
            normalized.Length > maxNameLength)
        {
            return false;
        }

        bool containsLetter = false;

        for (int i = 0; i < normalized.Length; i++)
        {
            char character = normalized[i];

            if (char.IsLetter(character))
            {
                containsLetter = true;
                continue;
            }

            if (character == ' ')
                continue;

            return false;
        }

        return containsLetter;
    }

    private string NormalizeSpaces(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder builder = new StringBuilder();

        bool previousWasSpace = false;

        string trimmed = value.Trim();

        for (int i = 0; i < trimmed.Length; i++)
        {
            char character = trimmed[i];

            if (char.IsWhiteSpace(character))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasSpace = false;
        }

        return builder.ToString();
    }

    private void HandleNameValueChanged(string value)
    {
        if (value.Length > maxNameLength)
        {
            nameInput.SetTextWithoutNotify(
                value.Substring(0, maxNameLength)
            );
        }

        RefreshSaveState();
    }

    // =========================================================
    // VISUALS
    // =========================================================

    private void RefreshAllVisuals()
    {
        RefreshSectionVisuals();
        RefreshVariantVisuals();
        RefreshSaveState();
    }

    private void RefreshSectionVisuals()
    {
        SetActiveSafe(
            nameSelected,
            currentSection == Section.Name
        );

        SetActiveSafe(
            variantSelected,
            currentSection == Section.Variant
        );

        SetActiveSafe(
            saveSelected,
            currentSection == Section.Save
        );

        SetActiveSafe(
            cancelSelected,
            currentSection == Section.Cancel
        );

        SetActiveSafe(
            backSelected,
            currentSection == Section.Back
        );

        RefreshVariantVisuals();
    }

    private void RefreshSaveState()
    {
        bool canSave = CanSave;

        if (saveButton != null)
            saveButton.interactable = canSave;

        if (saveBlocked != null)
            saveBlocked.SetActive(!canSave);

        if (saveText != null)
        {
            Color color = saveText.color;
            color.a = canSave ? 1f : 0.45f;
            saveText.color = color;
        }
    }

    private void SetNavigationEnabled(bool enabled)
    {
        if (canvasGroup != null)
        {
            canvasGroup.interactable = enabled;
            canvasGroup.blocksRaycasts = enabled;
        }

        // CanvasGroup blokuje mysz, ale Update nadal dzia³a.
        // Flaga processing blokuje klawiaturê w Update().
    }

    private static void SetActiveSafe(
        GameObject target,
        bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    private void ResetForm()
    {
        StopProcessingCoroutine();

        processing = false;
        completed = false;

        currentSection = Section.Name;

        hoveredVariantIndex = 0;
        selectedVariantIndex = -1;

        if (nameInput != null)
        {
            nameInput.DeactivateInputField();
            nameInput.interactable = true;
            nameInput.SetTextWithoutNotify(string.Empty);
        }

        if (processingText != null)
        {
            processingText.text = string.Empty;
            processingText.gameObject.SetActive(false);
        }

        if (successText != null)
        {
            successText.text = string.Empty;
            successText.gameObject.SetActive(false);
        }

        SetNavigationEnabled(true);
        RefreshAllVisuals();
    }

    private void StopProcessingCoroutine()
    {
        if (processingCoroutine == null)
            return;

        StopCoroutine(processingCoroutine);
        processingCoroutine = null;
    }

    // =========================================================
    // INPUT
    // =========================================================

    private bool UpPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.upArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.UpArrow);
#endif
    }

    private bool DownPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.downArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.DownArrow);
#endif
    }

    private bool LeftPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.leftArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.LeftArrow);
#endif
    }

    private bool RightPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.rightArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.RightArrow);
#endif
    }

    private bool EnterPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) ||
               Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    private bool EscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private void BuildVariantSlots()
    {
        variantSlots.Clear();

        if (variantPickerContainer == null)
        {
            Debug.LogWarning(
                "[CITIZEN ID UI] Variant Picker Container is missing."
            );

            return;
        }

        if (variantSlotPrefab == null)
        {
            Debug.LogWarning(
                "[CITIZEN ID UI] Variant Slot Prefab is missing."
            );

            return;
        }

        if (variantDatabase == null || variantDatabase.Count <= 0)
        {
            Debug.LogWarning(
                "[CITIZEN ID UI] Variant database is empty."
            );

            return;
        }

        // Usuñ wszystkie stare, rêcznie ustawione lub poprzednio wygenerowane sloty.
        for (int i = variantPickerContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = variantPickerContainer.GetChild(i);

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        for (int i = 0; i < variantDatabase.Count; i++)
        {
            VariantSlotView newSlot = Instantiate(
                variantSlotPrefab,
                variantPickerContainer
            );

            newSlot.name = $"CitizenIdVariant_{i:00}";

            newSlot.SetPreviewColor(
                variantDatabase.Get(i)
            );

            newSlot.SetHover(false);
            newSlot.SetSelected(false);

            variantSlots.Add(newSlot);
        }

        hoveredVariantIndex = 0;
        selectedVariantIndex = -1;

        if (debugLogs)
        {
            Debug.Log(
                $"[CITIZEN ID UI] Generated {variantSlots.Count} variant slots."
            );
        }
    }
}