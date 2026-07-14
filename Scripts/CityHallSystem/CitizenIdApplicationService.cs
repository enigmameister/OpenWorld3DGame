using System;
using UnityEngine;
using System.IO;
public enum CitizenIdApplicationStatus
{
    None = 0, // Default 

    WaitingForPhoto = 1, // Saved Form before Photo
    PhotoCompleted = 2, // Phone done
    Processing = 3, 
    ReadyForPickup = 4, // Ready for take it next day
    Issued = 5
}

[Serializable]
public class CitizenIdApplicationRecord
{
    public string holderName;
    public int variantIndex;

    public CitizenIdApplicationStatus status;

    public long submittedAtMinute;
    public long readyAtMinute;
    public string photoFilePath;

    public bool HasPhoto =>
    !string.IsNullOrWhiteSpace(photoFilePath);
}

public class CitizenIdApplicationService : MonoBehaviour
{
    public static CitizenIdApplicationService Instance { get; private set; }

    [Header("Runtime")]
    [SerializeField] private CitizenIdApplicationRecord currentApplication;

    [Header("Actual Photo Capture")]
    [SerializeField] private CitizenIdPhotoCapture photoCapture;

    [Header("Testing")]
    [SerializeField] private bool makeReadyImmediately;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    public CitizenIdApplicationRecord CurrentApplication =>
        currentApplication;

    public bool HasApplication =>
        currentApplication != null &&
        currentApplication.status != CitizenIdApplicationStatus.None;

    public bool IsReadyForPickup
    {
        get
        {
            RefreshStatus();

            return currentApplication != null &&
                   currentApplication.status ==
                   CitizenIdApplicationStatus.ReadyForPickup;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (photoCapture == null)
        {
            photoCapture =
                GetComponentInChildren<CitizenIdPhotoCapture>(true);
        }

        if (photoCapture == null)
        {
            photoCapture =
                FindFirstObjectByType<CitizenIdPhotoCapture>(
                    FindObjectsInactive.Include
                );
        }

        Instance = this;
    }

    private void Update()
    {
        if (currentApplication == null)
            return;

        if (currentApplication.status ==
            CitizenIdApplicationStatus.Processing)
        {
            RefreshStatus();
        }
    }

    public bool TrySubmit(
        string holderName,
        int variantIndex,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (HasApplication)
        {
            failureReason = "APPLICATION_ALREADY_EXISTS";
            return false;
        }

        if (string.IsNullOrWhiteSpace(holderName))
        {
            failureReason = "INVALID_NAME";
            return false;
        }

        long now = GetGameMinutesNow();

        currentApplication = new CitizenIdApplicationRecord
        {
            holderName = holderName.Trim(),
            variantIndex = Mathf.Max(0, variantIndex),
            status = CitizenIdApplicationStatus.WaitingForPhoto,
            submittedAtMinute = now,
            readyAtMinute = 0
        };

        if (debugLogs)
        {
            Debug.Log(
                $"[CITIZEN ID] Form saved: " +
                $"name={currentApplication.holderName}, " +
                $"variant={currentApplication.variantIndex}, " +
                $"status={currentApplication.status}"
            );
        }

        return true;
    }

    public void RefreshStatus()
    {
        if (currentApplication == null)
            return;

        if (currentApplication.status !=
            CitizenIdApplicationStatus.Processing)
        {
            return;
        }

        if (GetGameMinutesNow() <
            currentApplication.readyAtMinute)
        {
            return;
        }

        currentApplication.status =
            CitizenIdApplicationStatus.ReadyForPickup;

        if (debugLogs)
        {
            Debug.Log(
                "[CITIZEN ID] Document is ready for pickup."
            );
        }
    }
    public void MarkIssued()
    {
        if (currentApplication == null)
            return;

        currentApplication.status =
            CitizenIdApplicationStatus.Issued;
    }

    private long CalculateNextDayAtSeven(long currentMinute)
    {
        long currentDay = currentMinute / 1440L;
        long nextDayStart = (currentDay + 1L) * 1440L;

        return nextDayStart + 7L * 60L;
    }

    private long GetGameMinutesNow()
    {
        if (GameTimeSystem.Instance != null)
            return GameTimeSystem.Instance.TotalMinutesSinceStart;

        return Mathf.FloorToInt(
            Time.timeSinceLevelLoad / 60f
        );
    }

    public CitizenIdApplicationStatus Status =>
    currentApplication != null
        ? currentApplication.status
        : CitizenIdApplicationStatus.None;

    public bool IsWaitingForPhoto =>
        currentApplication != null &&
        currentApplication.status == CitizenIdApplicationStatus.WaitingForPhoto;

    public bool HasCompletedPhoto =>
        currentApplication != null &&
        currentApplication.status == CitizenIdApplicationStatus.PhotoCompleted;

    public bool IsProcessing =>
        currentApplication != null &&
        currentApplication.status == CitizenIdApplicationStatus.Processing;

    public bool TryCompletePhoto()
    {
        if (currentApplication == null)
            return false;

        if (currentApplication.status !=
            CitizenIdApplicationStatus.WaitingForPhoto)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                currentApplication.photoFilePath))
        {
            if (debugLogs)
            {
                Debug.LogWarning(
                    "[CITIZEN ID] Cannot complete photo stage: " +
                    "captured photo is missing."
                );
            }

            return false;
        }

        currentApplication.status =
            CitizenIdApplicationStatus.PhotoCompleted;

        if (debugLogs)
        {
            Debug.Log(
                "[CITIZEN ID] Photo completed. " +
                "Return to the clerk."
            );
        }

        return true;
    }

    public bool TryFinalizeApplication()
    {
        if (currentApplication == null)
            return false;

        if (currentApplication.status !=
            CitizenIdApplicationStatus.PhotoCompleted)
        {
            return false;
        }

        long now = GetGameMinutesNow();

        currentApplication.status =
            CitizenIdApplicationStatus.Processing;

        currentApplication.readyAtMinute =
            makeReadyImmediately
                ? now
                : CalculateNextDayAtSeven(now);

        if (debugLogs)
        {
            Debug.Log(
                $"[CITIZEN ID] Application finalized. " +
                $"Ready at {currentApplication.readyAtMinute}."
            );
        }

        return true;
    }

    public bool TryAttachPhoto(string photoFilePath)
    {
        if (currentApplication == null)
            return false;

        if (currentApplication.status !=
            CitizenIdApplicationStatus.WaitingForPhoto)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(photoFilePath))
            return false;

        if (!File.Exists(photoFilePath))
        {
            Debug.LogWarning(
                $"[CITIZEN ID] Photo file does not exist: " +
                $"{photoFilePath}"
            );

            return false;
        }

        currentApplication.photoFilePath = photoFilePath;

        if (debugLogs)
        {
            Debug.Log(
                $"[CITIZEN ID] Photo attached: " +
                $"{currentApplication.photoFilePath}"
            );
        }

        return true;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Clear Application")]
    private void DebugClearApplication()
    {
        currentApplication = null;
    }
#endif
}