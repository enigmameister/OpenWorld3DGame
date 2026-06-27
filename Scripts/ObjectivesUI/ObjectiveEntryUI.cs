using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveEntryUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject objectiveInfoRoot;
    [SerializeField] private LayoutElement layoutElement;

    [Header("Header")]
    [SerializeField] private TMP_Text missionNameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleButtonText;
    [SerializeField] private Button infoButton;

    [Header("Info")]
    [SerializeField] private TMP_Text objectiveText;

    [Header("Heights")]
    [SerializeField] private float collapsedHeight = 36f;
    [SerializeField] private float expandedHeight = 76f;

    [Header("Colors")]
    [SerializeField] private Color inProgressColor = Color.white;
    [SerializeField] private Color finishedColor = new Color(1f, 0.55f, 0f);
    [SerializeField] private Color failedColor = Color.red;

    private bool expanded;
    private RectTransform rebuildRoot;
    private ObjectivesUI owner;
    private ObjectiveEntryData currentData;

    private void Awake()
    {
        if (layoutElement == null)
            layoutElement = GetComponent<LayoutElement>();

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleExpanded);

        if (infoButton != null)
            infoButton.onClick.AddListener(OnInfoClicked);

        expanded = false;
        ApplyExpandedState();
    }

    private void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(ToggleExpanded);

        if (infoButton != null)
            infoButton.onClick.RemoveListener(OnInfoClicked);
    }

    public void Setup(ObjectiveEntryData data, RectTransform layoutRebuildRoot, ObjectivesUI uiOwner)
    {
        currentData = data;
        rebuildRoot = layoutRebuildRoot;
        owner = uiOwner;

        if (data == null)
            return;

        if (missionNameText != null)
            missionNameText.text = data.missionName;

        if (objectiveText != null)
            objectiveText.text = data.objectiveText;

        if (statusText != null)
        {
            statusText.text = GetStatusText(data.status);
            statusText.color = GetStatusColor(data.status);
        }

        expanded = false;
        ApplyExpandedState();
    }

    private void ToggleExpanded()
    {
        expanded = !expanded;
        ApplyExpandedState();

        if (owner != null)
            owner.RefreshLayoutAndScrollbarDelayed();
    }

    private void ApplyExpandedState()
    {
        if (objectiveInfoRoot != null)
            objectiveInfoRoot.SetActive(expanded);

        if (toggleButtonText != null)
            toggleButtonText.text = expanded ? "-" : "+";

        if (layoutElement != null)
            layoutElement.preferredHeight = expanded ? expandedHeight : collapsedHeight;

        ForceRebuild();
    }

    private string GetStatusText(ObjectiveStatus status)
    {
        switch (status)
        {
            case ObjectiveStatus.Finished:
                return "Finished";

            case ObjectiveStatus.Failed:
                return "Failed";

            default:
                return "In progress";
        }
    }

    private Color GetStatusColor(ObjectiveStatus status)
    {
        switch (status)
        {
            case ObjectiveStatus.Finished:
                return finishedColor;

            case ObjectiveStatus.Failed:
                return failedColor;

            default:
                return inProgressColor;
        }
    }

    private void OnInfoClicked()
    {
        if (owner != null && currentData != null)
            owner.OpenDetails(currentData);
    }

    private void ForceRebuild()
    {
        Canvas.ForceUpdateCanvases();

        if (rebuildRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rebuildRoot);

        RectTransform self = transform as RectTransform;

        if (self != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(self);

        Canvas.ForceUpdateCanvases();
    }
}