using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class MissionPassengerNpc : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Visibility")]
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Collider[] collidersToDisableWhenHidden;

    [Header("Boarding")]
    [SerializeField] private float arriveDistance = 1.2f;
    [SerializeField] private float boardingDelay = 0.25f;

    [Header("Boarding Live Check")]
    [SerializeField] private bool disableCollidersWhileBoarding = true;
    [SerializeField] private float boardDistance = 2.4f;
    [SerializeField] private float cancelBoardingDistance = 8.0f;
    [SerializeField] private float repathToCarInterval = 0.15f;

    private Collider[] cachedColliders;

    [Header("NavMesh Teleport")]
    [SerializeField] private bool snapTeleportToNavMesh = true;
    [SerializeField] private float navMeshSnapDistance = 2.0f;
    [SerializeField] private bool useAgentWarp = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private PassengerTransportMissionRuntime runtime;
    private Coroutine actionRoutine;
    private Vector3 startPosition;
    private Quaternion startRotation;

    public bool IsHidden { get; private set; }
    public bool IsWalkingToCar { get; private set; }
    public bool IsWalkingAway { get; private set; }

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        if (cachedColliders == null || cachedColliders.Length == 0)
            cachedColliders = GetComponentsInChildren<Collider>(true);

        if (collidersToDisableWhenHidden == null || collidersToDisableWhenHidden.Length == 0)
            collidersToDisableWhenHidden = GetComponentsInChildren<Collider>(true);

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    public void BindRuntime(PassengerTransportMissionRuntime missionRuntime)
    {
        runtime = missionRuntime;
    }

    public void ResetPassenger()
    {
        StopAction();

        transform.position = startPosition;
        transform.rotation = startRotation;

        ShowPassenger();
        SetPassengerCollidersEnabled(true);

        IsWalkingToCar = false;
        IsWalkingAway = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }
    }

    public void WalkToCarAndBoard(Transform carEnterTarget)
    {
        if (carEnterTarget == null)
            return;

        StopAction();
        ShowPassenger();

        actionRoutine = StartCoroutine(CoWalkToCarAndBoard(carEnterTarget));
    }

    public void ExitCarAndWalkAway(Transform exitPoint, Transform walkTarget)
    {
        StopAction();

        Vector3 exitPosition = transform.position;
        Quaternion exitRotation = transform.rotation;

        if (exitPoint != null)
        {
            exitPosition = exitPoint.position;
            exitRotation = exitPoint.rotation;
        }

        ShowPassenger();
        TeleportPassenger(exitPosition, exitRotation);

        actionRoutine = StartCoroutine(CoWalkAway(walkTarget));
    }

    private void TeleportPassenger(Vector3 targetPosition, Quaternion targetRotation)
    {
        Vector3 finalPosition = targetPosition;

        if (snapTeleportToNavMesh)
        {
            if (NavMesh.SamplePosition(
                    targetPosition,
                    out NavMeshHit hit,
                    navMeshSnapDistance,
                    NavMesh.AllAreas))
            {
                finalPosition = hit.position;
            }
            else if (debugLogs)
            {
                Debug.LogWarning(
                    $"[MissionPassengerNpc] No NavMesh near exit point '{targetPosition}'. " +
                    $"Using raw transform position. NPC may not be able to walk."
                );
            }
        }

        if (agent != null && agent.enabled)
        {
            agent.ResetPath();
            agent.isStopped = true;

            bool warped = false;

            if (useAgentWarp)
                warped = agent.Warp(finalPosition);

            if (!warped)
                transform.position = finalPosition;

            agent.nextPosition = finalPosition;
            agent.isStopped = false;
            agent.ResetPath();
        }
        else
        {
            transform.position = finalPosition;
        }

        transform.rotation = targetRotation;
    }

    private IEnumerator CoWalkToCarAndBoard(Transform carEnterTarget)
    {
        IsWalkingToCar = true;
        IsWalkingAway = false;

        if (disableCollidersWhileBoarding)
            SetPassengerCollidersEnabled(false);

        if (carEnterTarget == null)
        {
            CancelBoarding();
            yield break;
        }

        float nextRepathTime = 0f;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }

        while (true)
        {
            if (carEnterTarget == null)
            {
                CancelBoarding();
                yield break;
            }

            float liveDistanceToCar = Vector3.Distance(transform.position, carEnterTarget.position);

            // Auto odjecha³o za daleko zanim NPC zd¹¿y³ wejœæ.
            if (liveDistanceToCar > cancelBoardingDistance)
            {
                if (debugLogs)
                    Debug.Log($"[MissionPassengerNpc] Boarding cancelled. Car moved away: {name}");

                CancelBoarding();
                yield break;
            }

            // NPC jest faktycznie przy aktualnym triggerze wejœcia auta.
            if (liveDistanceToCar <= boardDistance)
                break;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (Time.time >= nextRepathTime)
                {
                    nextRepathTime = Time.time + repathToCarInterval;

                    Vector3 targetPos = carEnterTarget.position;

                    if (NavMesh.SamplePosition(
                            carEnterTarget.position,
                            out NavMeshHit hit,
                            navMeshSnapDistance,
                            NavMesh.AllAreas))
                    {
                        targetPos = hit.position;
                    }

                    agent.isStopped = false;
                    agent.SetDestination(targetPos);
                }
            }

            yield return null;
        }

        yield return new WaitForSeconds(boardingDelay);

        // Ostatni check po opóŸnieniu, bo auto mog³o ruszyæ w czasie delay.
        if (carEnterTarget == null ||
            Vector3.Distance(transform.position, carEnterTarget.position) > boardDistance + 0.5f)
        {
            CancelBoarding();
            yield break;
        }

        HidePassenger();

        IsWalkingToCar = false;

        if (runtime != null)
            runtime.NotifyPassengerBoarded();

        if (debugLogs)
            Debug.Log($"[MissionPassengerNpc] Boarded car: {name}");

        actionRoutine = null;
    }

    private void CancelBoarding()
    {
        IsWalkingToCar = false;
        IsWalkingAway = false;

        if (disableCollidersWhileBoarding)
            SetPassengerCollidersEnabled(true);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }

        actionRoutine = null;

        if (runtime != null)
            runtime.NotifyPassengerBoardingCancelled();
    }

    private IEnumerator CoWalkAway(Transform walkTarget)
    {
        IsWalkingAway = true;
        IsWalkingToCar = false;

        if (walkTarget == null)
        {
            HidePassenger();
            IsWalkingAway = false;

            if (runtime != null)
                runtime.NotifyPassengerWalkAwayFinished();

            actionRoutine = null;
            yield break;
        }

        Vector3 walkPosition = walkTarget.position;

        if (NavMesh.SamplePosition(
                walkTarget.position,
                out NavMeshHit walkHit,
                navMeshSnapDistance,
                NavMesh.AllAreas))
        {
            walkPosition = walkHit.position;
        }
        else if (debugLogs)
        {
            Debug.LogWarning(
                $"[MissionPassengerNpc] Walk target is not near NavMesh: {walkTarget.name}"
            );
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
            agent.SetDestination(walkPosition);
        }

        while (true)
        {
            float dist = Vector3.Distance(transform.position, walkPosition);

            if (dist <= arriveDistance)
                break;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (!agent.pathPending && agent.remainingDistance <= arriveDistance)
                    break;
            }

            yield return null;
        }

        HidePassenger();

        IsWalkingAway = false;

        if (runtime != null)
            runtime.NotifyPassengerWalkAwayFinished();

        if (debugLogs)
            Debug.Log($"[MissionPassengerNpc] Walk away finished: {name}");

        actionRoutine = null;
    }

    private void StopAction()
    {
        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }
    }

    private void HidePassenger()
    {
        IsHidden = true;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].enabled = false;
        }

        if (collidersToDisableWhenHidden != null)
        {
            for (int i = 0; i < collidersToDisableWhenHidden.Length; i++)
                if (collidersToDisableWhenHidden[i] != null)
                    collidersToDisableWhenHidden[i].enabled = false;
        }

        if (animator != null)
            animator.enabled = false;
    }

    private void ShowPassenger()
    {
        IsHidden = false;

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].enabled = true;
        }

        if (collidersToDisableWhenHidden != null)
        {
            for (int i = 0; i < collidersToDisableWhenHidden.Length; i++)
                if (collidersToDisableWhenHidden[i] != null)
                    collidersToDisableWhenHidden[i].enabled = true;
        }

        if (animator != null)
            animator.enabled = true;
    }

    private void SetPassengerCollidersEnabled(bool enabled)
    {
        if (cachedColliders == null)
            return;

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = enabled;
        }
    }
}