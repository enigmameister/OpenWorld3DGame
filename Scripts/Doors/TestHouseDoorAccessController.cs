using TMPro;
using UnityEngine;

public class TestHouseDoorAccessController : MonoBehaviour
{
    [Header("Access")]
    [SerializeField] private bool accessUnlocked = false;

    [Header("Animator")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string openBoolName = "IsKey";

    [Header("Auto Close")]
    [SerializeField] private float autoCloseDelay = 5f;

    [Header("Visuals")]
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private GameObject unlockedVisual;
    [SerializeField] private TMP_Text statusText;

    private bool isOpen;
    private float closeTimer;

    public bool AccessUnlocked => accessUnlocked;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        RefreshVisuals();
    }

    private void Update()
    {
        if (!isOpen)
            return;

        closeTimer -= Time.deltaTime;

        if (closeTimer <= 0f)
            CloseDoor();
    }

    public void UnlockAccess()
    {
        accessUnlocked = true;
        RefreshVisuals();
    }

    public void TryUsePanel()
    {
        if (!accessUnlocked)
        {
            ShowLockedInfo();
            return;
        }

        OpenDoor();
    }

    public void OpenDoor()
    {
        if (!accessUnlocked)
            return;

        isOpen = true;
        closeTimer = autoCloseDelay;

        if (doorAnimator != null)
            doorAnimator.SetBool(openBoolName, true);

        RefreshVisuals();
    }

    public void CloseDoor()
    {
        isOpen = false;
        closeTimer = 0f;

        if (doorAnimator != null)
            doorAnimator.SetBool(openBoolName, false);

        RefreshVisuals();
    }

    private void ShowLockedInfo()
    {
        if (statusText != null)
            statusText.text = "LOCKED\nKEY NEEDED";

        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (lockedVisual != null)
            lockedVisual.SetActive(!accessUnlocked);

        if (unlockedVisual != null)
            unlockedVisual.SetActive(accessUnlocked);

        if (statusText != null)
        {
            if (!accessUnlocked)
                statusText.text = "LOCKED\nKEY NEEDED";
            else if (isOpen)
                statusText.text = "OPEN";
            else
                statusText.text = "ACCESS\nGRANTED";
        }
    }
}