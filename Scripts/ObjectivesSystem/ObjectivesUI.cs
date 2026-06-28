using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ObjectivesUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform windowRoot;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;

    [Header("List")]
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private ObjectiveEntryUI entryPrefab;

    [Header("Scroll")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Scrollbar verticalScrollbar;

    [Header("Player Lock")]
    [SerializeField] private bool lockPlayerWhenOpen = true;

    [Header("Window Position")]
    [SerializeField] private bool resetPositionOnOpen = true;

    [Header("Details")]
    [SerializeField] private ObjectiveDetailsUI detailsUI;

    [Header("Main List View")]
    [SerializeField] private GameObject mainScrollViewRoot;
    [SerializeField] private GameObject mainScrollbarRoot;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly List<ObjectiveEntryUI> spawnedEntries = new();
    private readonly Dictionary<string, bool> expandedStates = new();

    private Vector2 startAnchoredPosition;
    private bool startPositionCached;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (windowRoot == null && root != null)
            windowRoot = root.GetComponent<RectTransform>();

        if (windowRoot != null)
        {
            startAnchoredPosition = windowRoot.anchoredPosition;
            startPositionCached = true;
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        CloseImmediate();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    private void Update()
    {
        if (PlayerInputHandler.Instance != null &&
            PlayerInputHandler.Instance.ObjectivesRawPressedThisFrame)
        {
            Toggle();
            return;
        }

        if (!IsOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        IsOpen = true;

        if (root != null)
            root.SetActive(true);

        if (resetPositionOnOpen && startPositionCached && windowRoot != null)
            windowRoot.anchoredPosition = startAnchoredPosition;

        BuildList();

        if (lockPlayerWhenOpen)
            LockPlayer();

        if (debugLogs)
            Debug.Log("[ObjectivesUI] Open");
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        if (detailsUI != null)
            detailsUI.CloseImmediate();

        if (root != null)
            root.SetActive(false);

        ClearList();

        if (lockPlayerWhenOpen)
            UnlockPlayer();
    }

    public void CloseImmediate()
    {
        IsOpen = false;

        if (root != null)
            root.SetActive(false);

        ClearList();
    }

    private void BuildList()
    {
        ClearList();

        List<ObjectiveEntryData> objectives = CollectObjectives();

        for (int i = 0; i < objectives.Count; i++)
        {
            ObjectiveEntryUI entry = Instantiate(entryPrefab, listRoot);
            entry.gameObject.SetActive(true);
            entry.Setup(objectives[i], listRoot, this);

            spawnedEntries.Add(entry);
        }

        RefreshLayoutAndScrollbarDelayed();
    }

    private void ClearList()
    {
        for (int i = 0; i < spawnedEntries.Count; i++)
        {
            if (spawnedEntries[i] != null)
                Destroy(spawnedEntries[i].gameObject);
        }

        spawnedEntries.Clear();
    }

    private List<ObjectiveEntryData> CollectObjectives()
    {
        if (MissionCoordinator.Instance == null)
            return new List<ObjectiveEntryData>();

        return MissionCoordinator.Instance.GetActiveObjectives();
    }
    private void RefreshScrollbarVisibility()
    {
        Canvas.ForceUpdateCanvases();

        if (verticalScrollbar == null || scrollRect == null || scrollRect.content == null)
            return;

        RectTransform viewport = scrollRect.viewport;

        if (viewport == null)
            return;

        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = viewport.rect.height;

        bool shouldShow = contentHeight > viewportHeight + 1f;

        verticalScrollbar.gameObject.SetActive(shouldShow);

        if (!shouldShow)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void ForceRebuild()
    {
        Canvas.ForceUpdateCanvases();

        if (listRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);

        if (scrollRect != null && scrollRect.content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        Canvas.ForceUpdateCanvases();
    }

    private void LockPlayer()
    {
        MouseLook.IsLookLocked = true;
        PlayerMovement.IsMovementLocked = true;
        PlayerInputHandler.SetGameplayBlocked(true);

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
    }

    public void RefreshLayoutAndScrollbarDelayed()
    {
        if (!gameObject.activeInHierarchy)
            return;

        StopCoroutine(nameof(CoRefreshLayoutAndScrollbar));
        StartCoroutine(nameof(CoRefreshLayoutAndScrollbar));
    }

    private System.Collections.IEnumerator CoRefreshLayoutAndScrollbar()
    {
        yield return null;
        yield return null;

        ForceRebuild();
        RefreshScrollbarVisibility();
    }

    public void OpenDetails(ObjectiveEntryData data)
    {
        SetMainListVisible(false);

        if (detailsUI != null)
            detailsUI.Open(data);
    }

    public void Rebuild()
    {
        if (!IsOpen)
            return;

        BuildList();
    }

    public void SetMainListVisible(bool visible)
    {
        if (mainScrollViewRoot != null)
            mainScrollViewRoot.SetActive(visible);

        if (mainScrollbarRoot != null)
            mainScrollbarRoot.SetActive(visible);
    }

    public bool GetObjectiveExpanded(string missionId)
    {
        if (string.IsNullOrWhiteSpace(missionId))
            return false;

        return expandedStates.TryGetValue(missionId, out bool expanded) && expanded;
    }

    public void SetObjectiveExpanded(string missionId, bool expanded)
    {
        if (string.IsNullOrWhiteSpace(missionId))
            return;

        expandedStates[missionId] = expanded;
    }
}