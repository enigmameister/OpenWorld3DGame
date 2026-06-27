using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveDetailsUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button abandonButton;

    [Header("Scroll")]
    [SerializeField] private ScrollRect descriptionScrollRect;
    [SerializeField] private Scrollbar verticalScrollbar;

    [Header("Confirm")]
    [SerializeField] private ObjectiveAbandonConfirmUI abandonConfirmUI;

    [Header("Refresh")]
    [SerializeField] private ObjectivesUI objectivesUI;

    private ObjectiveEntryData currentData;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (abandonButton != null)
            abandonButton.onClick.AddListener(AbandonMission);

        CloseImmediate();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        if (abandonButton != null)
            abandonButton.onClick.RemoveListener(AbandonMission);
    }

    public void Open(ObjectiveEntryData data)
    {
        currentData = data;

        if (root != null)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        if (titleText != null)
            titleText.text = data != null ? data.missionName : "Mission";

        if (descriptionText != null)
            descriptionText.text = data != null ? data.descriptionText : "";

        if (abandonButton != null)
            abandonButton.gameObject.SetActive(data != null && data.canAbandon);

        RefreshScrollDelayed();
    }

    public void Close()
    {
        if (abandonConfirmUI != null)
            abandonConfirmUI.CloseImmediate();

        if (root != null)
            root.SetActive(false);

        currentData = null;

        if (objectivesUI != null)
            objectivesUI.SetMainListVisible(true);
    }

    public void CloseImmediate()
    {
        if (abandonConfirmUI != null)
            abandonConfirmUI.CloseImmediate();

        if (root != null)
            root.SetActive(false);

        currentData = null;

        if (objectivesUI != null)
            objectivesUI.SetMainListVisible(true);
    }

    private void AbandonMission()
    {
        if (currentData == null)
            return;

        if (!currentData.canAbandon)
            return;

        if (abandonConfirmUI != null)
        {
            abandonConfirmUI.Open(this);
            return;
        }

        ConfirmAbandonMission();
    }

    public void ConfirmAbandonMission()
    {
        if (currentData == null)
            return;

        if (!currentData.canAbandon)
            return;

        currentData.onAbandon?.Invoke();

        Close();

        if (objectivesUI != null)
            objectivesUI.Rebuild();
    }

    private void RefreshScrollDelayed()
    {
        if (!gameObject.activeInHierarchy)
            return;

        StopCoroutine(nameof(CoRefreshScroll));
        StartCoroutine(nameof(CoRefreshScroll));
    }

    private System.Collections.IEnumerator CoRefreshScroll()
    {
        yield return null;
        yield return null;

        Canvas.ForceUpdateCanvases();

        if (descriptionScrollRect != null)
            descriptionScrollRect.verticalNormalizedPosition = 1f;

        RefreshScrollbarVisibility();
    }

    private void RefreshScrollbarVisibility()
    {
        if (descriptionScrollRect == null ||
            descriptionScrollRect.content == null ||
            descriptionScrollRect.viewport == null ||
            verticalScrollbar == null)
        {
            return;
        }

        float contentHeight = descriptionScrollRect.content.rect.height;
        float viewportHeight = descriptionScrollRect.viewport.rect.height;

        bool shouldShow = contentHeight > viewportHeight + 1f;

        verticalScrollbar.gameObject.SetActive(shouldShow);
    }
}