using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class FilePickupSequence : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CityHallDialogueUI dialogueUI;

    [Header("Movement Points")]
    [Tooltip("Miejsce, przy którym NPC szuka dokumentów.")]
    [SerializeField] private Transform cabinetPoint;

    [Tooltip("Domyœlne stanowisko NPC za lad¹.")]
    [SerializeField] private Transform fallbackDeskPoint;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float movementTimeout = 12f;
    [SerializeField, Min(0f)] private float searchDuration = 3f;
    [SerializeField, Min(0.01f)] private float arrivalTolerance = 0.15f;

    [Header("Rotation")]
    [SerializeField, Min(1f)] private float rotationSpeed = 360f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private CityHallEmployee servicingClerk;
    private NavMeshAgent activeAgent;
    private Transform activeDeskPoint;

    private Coroutine sequenceCoroutine;

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private bool originalAgentStopped;
    private bool originalUpdateRotation;
    private bool originalAutoBraking;

    public bool IsRunning => sequenceCoroutine != null;

    private void Awake()
    {
        if (dialogueUI == null)
        {
            dialogueUI =
                FindFirstObjectByType<CityHallDialogueUI>(
                    FindObjectsInactive.Include
                );
        }
    }

    public void SetServicingClerk(
        CityHallEmployee employee,
        Transform deskPoint = null)
    {
        if (IsRunning)
            return;

        servicingClerk = employee;
        activeDeskPoint = deskPoint != null
            ? deskPoint
            : fallbackDeskPoint;

        activeAgent = servicingClerk != null
            ? servicingClerk.GetComponent<NavMeshAgent>()
            : null;

        if (debugLogs && servicingClerk != null)
        {
            Debug.Log(
                $"[FILE PICKUP] Servicing clerk selected: " +
                $"{servicingClerk.EmployeeName}",
                this
            );
        }
    }

    public bool BeginSearch()
    {
        if (IsRunning)
            return false;

        if (!ValidateReferences())
            return false;

        sequenceCoroutine =
            StartCoroutine(SearchRoutine());

        return true;
    }

    private IEnumerator SearchRoutine()
    {
        CaptureOriginalState();

        activeAgent.isStopped = false;
        activeAgent.updateRotation = true;
        activeAgent.autoBraking = true;

        if (debugLogs)
        {
            Debug.Log(
                $"[FILE PICKUP] Clerk moving to cabinet: " +
                $"{servicingClerk.EmployeeName}",
                this
            );
        }

        bool reachedCabinet = false;

        yield return MoveToPointRoutine(
            cabinetPoint,
            result => reachedCabinet = result
        );

        if (!reachedCabinet)
        {
            AbortSequence(
                "[FILE PICKUP] Clerk could not reach cabinet."
            );

            yield break;
        }

        SnapToPoint(cabinetPoint);

        activeAgent.updateRotation = false;

        yield return RotateTowardsPointRoutine(
            cabinetPoint
        );

        if (searchDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                searchDuration
            );
        }

        if (!CanContinue())
        {
            AbortSequence(
                "[FILE PICKUP] Dialogue ended during document search."
            );

            yield break;
        }

        activeAgent.updateRotation = true;
        activeAgent.isStopped = false;

        bool reachedDesk = false;

        yield return MoveToPointRoutine(
            activeDeskPoint,
            result => reachedDesk = result
        );

        if (!reachedDesk)
        {
            AbortSequence(
                "[FILE PICKUP] Clerk could not return to desk."
            );

            yield break;
        }

        activeAgent.updateRotation = false;

        SnapToPoint(activeDeskPoint);
        RestoreExactDeskRotation();

        RestoreAgentState();

        sequenceCoroutine = null;

        dialogueUI?.ResumeFilePickupAfterSearch();

    }

    private IEnumerator MoveToPointRoutine(
        Transform destination,
        System.Action<bool> onFinished)
    {
        if (destination == null ||
            activeAgent == null ||
            !activeAgent.enabled ||
            !activeAgent.isOnNavMesh)
        {
            onFinished?.Invoke(false);
            yield break;
        }

        activeAgent.isStopped = false;

        bool destinationAccepted =
            activeAgent.SetDestination(
                destination.position
            );

        if (!destinationAccepted)
        {
            onFinished?.Invoke(false);
            yield break;
        }

        float timer = 0f;

        while (timer < movementTimeout)
        {
            if (!CanContinue())
            {
                onFinished?.Invoke(false);
                yield break;
            }

            timer += Time.unscaledDeltaTime;

            if (!activeAgent.pathPending)
            {
                float allowedDistance =
                    Mathf.Max(
                        activeAgent.stoppingDistance,
                        arrivalTolerance
                    );

                if (activeAgent.remainingDistance <= allowedDistance)
                {
                    if (!activeAgent.hasPath ||
                        activeAgent.velocity.sqrMagnitude <= 0.01f)
                    {
                        activeAgent.isStopped = true;
                        onFinished?.Invoke(true);
                        yield break;
                    }
                }
            }

            yield return null;
        }

        onFinished?.Invoke(false);
    }

    private IEnumerator RotateTowardsPointRoutine(
        Transform point)
    {
        if (servicingClerk == null ||
            point == null)
        {
            yield break;
        }

        Quaternion targetRotation =
            point.rotation;

        while (Quaternion.Angle(
                   servicingClerk.transform.rotation,
                   targetRotation) > 0.5f)
        {
            if (!CanContinue())
                yield break;

            servicingClerk.transform.rotation =
                Quaternion.RotateTowards(
                    servicingClerk.transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.unscaledDeltaTime
                );

            yield return null;
        }

        servicingClerk.transform.rotation =
            targetRotation;
    }

    private void SnapToPoint(Transform point)
    {
        if (servicingClerk == null ||
            point == null)
        {
            return;
        }

        if (activeAgent != null &&
            activeAgent.enabled &&
            activeAgent.isOnNavMesh)
        {
            activeAgent.Warp(point.position);
            activeAgent.ResetPath();
            activeAgent.isStopped = true;
        }
        else
        {
            servicingClerk.transform.position =
                point.position;
        }
    }

    private void RestoreExactDeskRotation()
    {
        if (servicingClerk == null)
            return;

        Quaternion targetRotation =
            activeDeskPoint != null
                ? activeDeskPoint.rotation
                : originalRotation;

        servicingClerk.transform.rotation =
            targetRotation;
    }

    private void CaptureOriginalState()
    {
        originalPosition =
            servicingClerk.transform.position;

        originalRotation =
            servicingClerk.transform.rotation;

        originalAgentStopped =
            activeAgent.isStopped;

        originalUpdateRotation =
            activeAgent.updateRotation;

        originalAutoBraking =
            activeAgent.autoBraking;
    }

    private void RestoreAgentState()
    {
        if (activeAgent == null ||
            !activeAgent.enabled ||
            !activeAgent.isOnNavMesh)
        {
            return;
        }

        activeAgent.ResetPath();
        activeAgent.updateRotation =
            originalUpdateRotation;

        activeAgent.autoBraking =
            originalAutoBraking;

        activeAgent.isStopped =
            originalAgentStopped;
    }

    private void AbortSequence(string reason)
    {
        if (debugLogs)
            Debug.LogWarning(reason, this);

        if (servicingClerk != null)
        {
            Transform returnPoint =
                activeDeskPoint != null
                    ? activeDeskPoint
                    : null;

            if (returnPoint != null)
            {
                SnapToPoint(returnPoint);

                servicingClerk.transform.rotation =
                    returnPoint.rotation;
            }
            else
            {
                if (activeAgent != null &&
                    activeAgent.enabled &&
                    activeAgent.isOnNavMesh)
                {
                    activeAgent.Warp(originalPosition);
                    activeAgent.ResetPath();
                }
                else
                {
                    servicingClerk.transform.position =
                        originalPosition;
                }

                servicingClerk.transform.rotation =
                    originalRotation;
            }
        }

        RestoreAgentState();

        sequenceCoroutine = null;
    }

    private bool CanContinue()
    {
        return dialogueUI != null &&
               dialogueUI.IsOpen &&
               servicingClerk != null &&
               activeAgent != null &&
               activeAgent.enabled &&
               activeAgent.isOnNavMesh;
    }

    private bool ValidateReferences()
    {
        if (servicingClerk == null)
        {
            Debug.LogWarning(
                "[FILE PICKUP] Servicing clerk is missing.",
                this
            );

            return false;
        }

        if (activeAgent == null)
        {
            Debug.LogWarning(
                "[FILE PICKUP] Clerk NavMeshAgent is missing.",
                this
            );

            return false;
        }

        if (!activeAgent.enabled ||
            !activeAgent.isOnNavMesh)
        {
            Debug.LogWarning(
                "[FILE PICKUP] Clerk is not on NavMesh.",
                this
            );

            return false;
        }

        if (cabinetPoint == null)
        {
            Debug.LogWarning(
                "[FILE PICKUP] Cabinet Point is missing.",
                this
            );

            return false;
        }

        if (activeDeskPoint == null)
        {
            Debug.LogWarning(
                "[FILE PICKUP] Desk Point is missing.",
                this
            );

            return false;
        }

        if (dialogueUI == null)
        {
            Debug.LogWarning(
                "[FILE PICKUP] CityHallDialogueUI is missing.",
                this
            );

            return false;
        }

        return true;
    }

    private void OnDisable()
    {
        if (sequenceCoroutine == null)
            return;

        StopCoroutine(sequenceCoroutine);
        sequenceCoroutine = null;

        AbortSequence(
            "[FILE PICKUP] Sequence interrupted because object was disabled."
        );
    }
}