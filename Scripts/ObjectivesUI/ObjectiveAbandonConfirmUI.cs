using UnityEngine;
using UnityEngine.UI;

public class ObjectiveAbandonConfirmUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private ObjectiveDetailsUI detailsUI;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (yesButton != null)
            yesButton.onClick.AddListener(Confirm);

        if (noButton != null)
            noButton.onClick.AddListener(Cancel);

        CloseImmediate();
    }

    private void OnDestroy()
    {
        if (yesButton != null)
            yesButton.onClick.RemoveListener(Confirm);

        if (noButton != null)
            noButton.onClick.RemoveListener(Cancel);
    }

    public void Open(ObjectiveDetailsUI owner)
    {
        detailsUI = owner;

        if (root != null)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
    }

    public void CloseImmediate()
    {
        if (root != null)
            root.SetActive(false);

        detailsUI = null;
    }

    private void Confirm()
    {
        if (detailsUI != null)
            detailsUI.ConfirmAbandonMission();

        CloseImmediate();
    }

    private void Cancel()
    {
        CloseImmediate();
    }
}