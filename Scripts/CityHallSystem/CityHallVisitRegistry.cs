using System;
using UnityEngine;

public class CityHallVisitRegistry : MonoBehaviour
{
    public static CityHallVisitRegistry Instance { get; private set; }

    [Header("Runtime")]
    [SerializeField] private CityHallVisitType activeVisitType = CityHallVisitType.None;
    [SerializeField] private CityHallVisitState activeVisitState = CityHallVisitState.None;
    [SerializeField] private long registeredAtGameMinute;

    [Header("Rules")]
    [SerializeField] private bool allowOnlyOneActiveVisit = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    public CityHallVisitType ActiveVisitType => activeVisitType;
    public CityHallVisitState ActiveVisitState => activeVisitState;
    public long RegisteredAtGameMinute => registeredAtGameMinute;

    public bool HasAnyActiveVisit =>
        activeVisitType != CityHallVisitType.None &&
        (activeVisitState == CityHallVisitState.Registered ||
         activeVisitState == CityHallVisitState.InProgress);

    public event Action<CityHallVisitType> VisitRegistered;
    public event Action<CityHallVisitType> VisitStarted;
    public event Action<CityHallVisitType> VisitCompleted;
    public event Action<CityHallVisitType> VisitCancelled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool TryRegisterVisit(
        CityHallVisitType visitType,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (visitType == CityHallVisitType.None)
        {
            failureReason = "INVALID_VISIT_TYPE";
            return false;
        }

        if (allowOnlyOneActiveVisit && HasAnyActiveVisit)
        {
            if (activeVisitType == visitType)
                failureReason = "VISIT_ALREADY_REGISTERED";
            else
                failureReason = "OTHER_VISIT_ALREADY_ACTIVE";

            return false;
        }

        activeVisitType = visitType;
        activeVisitState = CityHallVisitState.Registered;
        registeredAtGameMinute = GetCurrentGameMinute();

        if (debugLogs)
        {
            Debug.Log(
                $"[CITY HALL] Visit registered: {activeVisitType}, " +
                $"time={registeredAtGameMinute}"
            );
        }

        VisitRegistered?.Invoke(activeVisitType);
        return true;
    }

    public bool HasRegisteredVisit(CityHallVisitType visitType)
    {
        return activeVisitType == visitType &&
               activeVisitState == CityHallVisitState.Registered;
    }

    public bool HasUsableVisit(CityHallVisitType visitType)
    {
        return activeVisitType == visitType &&
               (activeVisitState == CityHallVisitState.Registered ||
                activeVisitState == CityHallVisitState.InProgress);
    }

    public bool HasVisit(
        CityHallVisitType visitType,
        CityHallVisitState requiredState)
    {
        return activeVisitType == visitType &&
               activeVisitState == requiredState;
    }

    public bool TryBeginVisit(CityHallVisitType visitType)
    {
        if (activeVisitType != visitType)
            return false;

        // Wizyta zosta³a ju¿ wczeœniej sprawdzona.
        if (activeVisitState == CityHallVisitState.InProgress)
            return true;

        if (activeVisitState != CityHallVisitState.Registered)
            return false;

        activeVisitState = CityHallVisitState.InProgress;

        if (debugLogs)
            Debug.Log($"[CITY HALL] Visit started: {activeVisitType}");

        VisitStarted?.Invoke(activeVisitType);
        return true;
    }

    public bool TryCompleteVisit(CityHallVisitType visitType)
    {
        if (activeVisitType != visitType)
            return false;

        if (activeVisitState != CityHallVisitState.Registered &&
            activeVisitState != CityHallVisitState.InProgress)
        {
            return false;
        }

        activeVisitState = CityHallVisitState.Completed;

        if (debugLogs)
            Debug.Log($"[CITY HALL] Visit completed: {activeVisitType}");

        VisitCompleted?.Invoke(activeVisitType);
        return true;
    }

    public bool CancelActiveVisit()
    {
        if (!HasAnyActiveVisit)
            return false;

        CityHallVisitType cancelledType = activeVisitType;

        activeVisitState = CityHallVisitState.Cancelled;

        if (debugLogs)
            Debug.Log($"[CITY HALL] Visit cancelled: {cancelledType}");

        VisitCancelled?.Invoke(cancelledType);
        return true;
    }

    public void ClearVisit()
    {
        activeVisitType = CityHallVisitType.None;
        activeVisitState = CityHallVisitState.None;
        registeredAtGameMinute = 0;

        if (debugLogs)
            Debug.Log("[CITY HALL] Visit state cleared.");
    }

    public string GetActiveVisitDisplayName()
    {
        return activeVisitType switch
        {
            CityHallVisitType.CitizenId => "CITIZEN ID",
            CityHallVisitType.DrivingLicense => "DRIVING LICENSE",
            CityHallVisitType.LostAndFound => "LOST & FOUND",
            CityHallVisitType.FilePickup => "FILE PICKUP",
            _ => "NONE"
        };
    }

    private long GetCurrentGameMinute()
    {
        if (GameTimeSystem.Instance != null)
            return GameTimeSystem.Instance.TotalMinutesSinceStart;

        return Mathf.FloorToInt(Time.timeSinceLevelLoad / 60f);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Register Citizen ID")]
    private void DebugRegisterCitizenId()
    {
        TryRegisterVisit(
            CityHallVisitType.CitizenId,
            out string failureReason
        );

        if (!string.IsNullOrWhiteSpace(failureReason))
            Debug.Log($"[CITY HALL] Register failed: {failureReason}");
    }

    [ContextMenu("Debug/Clear Visit")]
    private void DebugClearVisit()
    {
        ClearVisit();
    }
#endif
}