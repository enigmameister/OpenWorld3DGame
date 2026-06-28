using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionTrackerEntryUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Header")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleText;
    [SerializeField] private TMP_Text headerText;

    [Header("Info")]
    [SerializeField] private GameObject infoRoot;
    [SerializeField] private TMP_Text armedKilledText;
    [SerializeField] private TMP_Text innocentKilledText;
    [SerializeField] private TMP_Text resultText;

    [Header("Colors")]
    [SerializeField] private Color inProgressColor = Color.white;
    [SerializeField] private Color readyToClaimColor = new Color(1f, 0.8f, 0.15f);
    [SerializeField] private Color completedColor = Color.green;

    [Header("State")]
    [SerializeField] private bool expanded = true;

    [SerializeField] private LayoutElement layoutElement;
    [SerializeField] private float collapsedHeight = 28f;
    [SerializeField] private float expandedHeight = 92f;
    [SerializeField] private RectTransform rebuildRoot;
    private void Awake()
    {
        if (root == null) root = gameObject;

        if (toggleButton != null) toggleButton.onClick.AddListener(ToggleExpanded);

        ApplyExpandedState();
        ForceRebuild();
    }

    private void OnDestroy()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(ToggleExpanded);
    }

    public void SetVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
        else
            gameObject.SetActive(visible);
    }

    public void SetTitle(string title)
    {
        if (headerText != null)
            headerText.text = string.IsNullOrWhiteSpace(title) ? "MISSION" : title;
    }

    public void SetProgress(int armedKilled, int innocentKilled, int result, int requiredScore)
    {
        if (armedKilledText != null)
            armedKilledText.text = $"{armedKilled}/{requiredScore}";

        if (innocentKilledText != null)
            innocentKilledText.text = innocentKilled.ToString();

        if (resultText != null)
            resultText.text = $"{result}/{requiredScore}";
    }

    public void SetStatus(KillArmedNPCMission.MissionState state)
    {
        if (headerText == null)
            return;

        switch (state)
        {
            case KillArmedNPCMission.MissionState.Active:
                headerText.color = inProgressColor;
                break;

            case KillArmedNPCMission.MissionState.ReadyToClaim:
                headerText.color = readyToClaimColor;
                break;

            case KillArmedNPCMission.MissionState.RewardClaimed:
                headerText.color = completedColor;
                break;

            default:
                headerText.color = inProgressColor;
                break;
        }
    }

    private void ToggleExpanded()
    {
        expanded = !expanded;
        ApplyExpandedState();
    }

    private void ApplyExpandedState()
    {
        if (infoRoot != null)
            infoRoot.SetActive(expanded);

        if (toggleText != null)
            toggleText.text = expanded ? "-" : "+";

        if (layoutElement != null)
            layoutElement.preferredHeight = expanded ? expandedHeight : collapsedHeight;

        ForceRebuild();
    }

    private void ForceRebuild()
    {
        Canvas.ForceUpdateCanvases();

        if (rebuildRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rebuildRoot);

        RectTransform parent = transform.parent as RectTransform;

        if (parent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);

        RectTransform self = transform as RectTransform;

        if (self != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(self);

        Canvas.ForceUpdateCanvases();
    }
}