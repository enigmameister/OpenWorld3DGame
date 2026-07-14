using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class CitizenIdPhotoSequence : MonoBehaviour
{
    [Serializable]
    private class ClerkStation
    {
        public CityHallEmployee employee;
        public Transform clerkRoot;
        public NavMeshAgent agent;
        public Transform deskPoint;
    }

    [Header("Clerks")]
    [SerializeField] private ClerkStation[] clerks;
    [SerializeField, Min(1f)] private float movementTimeout = 12f;
    [SerializeField] private Transform photoMakerPoint;

    private ClerkStation activeClerk;
    private bool originalAgentStopped;
    private bool waitingForPlayerPose;
    
    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private PlayerMovement playerMovement;

    [SerializeField] private Transform playerPhotoPoint;

    [Tooltip("Maksymalna odleg³oœæ gracza od œrodka stanowiska.")]
    [SerializeField, Min(0.05f)] private float playerPositionTolerance = 0.35f;

    [Tooltip("Maksymalny k¹t odchylenia od kierunku aparatu.")]
    [SerializeField, Range(1f, 90f)] private float playerFacingTolerance = 20f;

    [Tooltip("Po zaakceptowaniu pozycji wyrównaj gracza dok³adnie do punktu.")]
    [SerializeField] private bool snapPlayerAfterAccepted = true;

    [Header("Photo FX")]
    [SerializeField] private Renderer photoStatusRenderer;
    [SerializeField] private Light flashLight;
    [SerializeField] private AudioSource photoAudio;
    [SerializeField] private AudioClip shutterClip;

    [SerializeField] private Light photoKeyLight;
    [SerializeField] private Light photoFillLight;

    [Header("Photo Status Colors")]
    [SerializeField]
    private Color idleColor =
        new Color(0.05f, 0.35f, 0.55f);

    [SerializeField] private Color preparingColor = Color.red;
    [SerializeField] private Color captureColor = Color.white;
    [SerializeField] private Color completedColor = Color.green;

    [SerializeField, Min(0f)]
    private float returnToIdleDelay = 5f;
    private bool lastActualPhotoCaptureSucceeded;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float arriveTolerance = 0.2f;
    [SerializeField, Min(0f)] private float prepareDelay = 1f;
    [SerializeField, Min(0f)] private float flashDuration = 0.08f;
    [SerializeField, Min(0f)] private float photoHoldDuration = 0.6f;
    [SerializeField, Min(0f)] private float completedHoldDuration = 1f;

    [Header("Screen Flash")]
    [SerializeField] private ScreenFlashUI screenFlash;

    [SerializeField, Range(0f, 1f)]
    private float screenFlashAlpha = 0.85f;

    [SerializeField, Min(0f)]
    private float screenFlashFadeIn = 0.02f;

    [SerializeField, Min(0f)]
    private float screenFlashHold = 0.04f;

    [SerializeField, Min(0f)]
    private float screenFlashFadeOut = 0.35f;

    [Header("Photo Series")]
    [SerializeField, Min(1)] private int photoCount = 3;
    [SerializeField, Min(0f)] private float delayBetweenPhotos = 0.75f;

    [SerializeField] private CityHallDialogueUI dialogueUI;

    [SerializeField]
    private string lookAtCameraLine =
        "Please look at the camera.";

    [SerializeField]
    private string againLine =
        "Again.";

    [SerializeField]
    private string anotherOneLine =
        "Another one.";

    [SerializeField]
    private string completedLine =
        "Okay, thank you.";

    [SerializeField, Min(0f)] private float dialogueLineHoldTime = 0.5f;

    [Header("Rules")]
    [SerializeField] private bool requirePlayerToRemainInZone = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool playerInside;
    private bool sequenceRunning;

    private MaterialPropertyBlock propertyBlock;

    private bool originalAgentStateCaptured;
    private Coroutine statusResetCoroutine;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private WeaponManager cachedWeaponManager;
    private bool weaponManagerWasEnabled;
    private bool originalAgentUpdateRotation;
    private bool originalAgentUpdatePosition;
    private bool lastMovementSucceeded;

    [Header("Actual Photo Capture")]
    [SerializeField] private CitizenIdPhotoCapture photoCapture;

    private void Update()
    {
        if (!waitingForPlayerPose)
            return;

        if (sequenceRunning)
            return;

        if (!playerInside)
            return;

        if (!IsPlayerCorrectlyPositioned())
            return;

        waitingForPlayerPose = false;

        if (snapPlayerAfterAccepted)
            SnapPlayerToPhotoPose();

        StartCoroutine(PhotoSequenceRoutine());
    }

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        cachedWeaponManager = FindFirstObjectByType<WeaponManager>();

        ValidateClerkStations();

        if (player == null)
        {
            PlayerStats stats = FindFirstObjectByType<PlayerStats>();

            if (stats != null)
                player = stats.transform;
        }

        if (dialogueUI == null)
        {
            dialogueUI = FindFirstObjectByType<CityHallDialogueUI>(
                FindObjectsInactive.Include
            );
        }

        if (screenFlash == null)
        {
            screenFlash = FindFirstObjectByType<ScreenFlashUI>(
                FindObjectsInactive.Include
            );
        }

        if (photoCapture == null)
            photoCapture = GetComponentInChildren<CitizenIdPhotoCapture>(true);

        if (photoCapture == null)
        {
            photoCapture =
                FindFirstObjectByType<CitizenIdPhotoCapture>(
                    FindObjectsInactive.Include
                );
        }

        if (playerMovement == null && player != null)
            playerMovement = player.GetComponent<PlayerMovement>();

        if (flashLight != null)
            flashLight.enabled = false;

        if (photoKeyLight != null)
            photoKeyLight.enabled = false;

        if (photoFillLight != null)
            photoFillLight.enabled = false;

        SetPhotoStatusColor(idleColor);
    }

    private void ValidateClerkStations()
    {
        if (clerks == null)
            return;

        for (int i = 0; i < clerks.Length; i++)
        {
            ClerkStation station = clerks[i];

            if (station == null)
                continue;

            if (station.clerkRoot == null && station.employee != null)
                station.clerkRoot = station.employee.transform;

            if (station.agent == null && station.clerkRoot != null)
                station.agent = station.clerkRoot.GetComponent<NavMeshAgent>();
        }
    }

    public void NotifyPlayerEntered()
    {
        playerInside = true;

        if (sequenceRunning)
            return;

        CitizenIdApplicationService service =
            CitizenIdApplicationService.Instance;

        if (service == null || !service.IsWaitingForPhoto)
        {
            if (debugLogs)
            {
                Debug.Log(
                    "[CITIZEN ID PHOTO] Player entered, but no application is waiting for photo."
                );
            }

            return;
        }

        if (!ResolveActiveClerk())
        {
            Debug.LogWarning(
                "[CITIZEN ID PHOTO] No valid clerk station available."
            );

            return;
        }

        waitingForPlayerPose = true;

        if (debugLogs)
        {
            Debug.Log(
                "[CITIZEN ID PHOTO] Player entered photo zone. " +
                "Waiting for correct position and facing."
            );
        }
    }

    public void NotifyPlayerExited()
    {
        playerInside = false;

        if (!sequenceRunning)
            waitingForPlayerPose = false;
    }

    private IEnumerator PhotoSequenceRoutine()
    {
        if (sequenceRunning)
            yield break;

        if (!playerInside)
            yield break;

        if (!ResolveActiveClerk())
            yield break;

        sequenceRunning = true;

        CaptureOriginalAgentState();
        activeClerk.agent.updateRotation = true;
        LockPlayerForPhoto();

        if (debugLogs)
        {
            Debug.Log(
                "[CITIZEN ID PHOTO] Player pose accepted. " +
                "Movement locked, clerk is moving to camera."
            );
        }

        bool movedToCamera = BeginMoveClerk(photoMakerPoint);

        if (!movedToCamera)
        {
            AbortSequence(
                "[CITIZEN ID PHOTO] Clerk could not move to PhotoMakerPoint."
            );

            yield break;
        }

        yield return WaitForClerkArrival(photoMakerPoint);

        if (!lastMovementSucceeded)
        {
            AbortSequence(
                "[CITIZEN ID PHOTO] Clerk did not reach PhotoMakerPoint."
            );

            yield break;
        }

        if (ShouldAbortBecausePlayerLeft())
        {
            AbortSequence(
                "[CITIZEN ID PHOTO] Player left photo zone before picture."
            );

            yield break;
        }


        SetPhotoStatusColor(preparingColor);

        activeClerk.agent.updateRotation = false;
        FaceClerkTowardsPlayer();

        yield return TakePhotoSeriesRoutine();

        if (!lastActualPhotoCaptureSucceeded)
        {
            AbortSequence(
                "[CITIZEN ID PHOTO] Actual photo capture failed."
            );

            yield break;
        }

        if (ShouldAbortBecausePlayerLeft())
        {
            dialogueUI?.CloseTemporaryDialogue();

            AbortSequence(
                "[CITIZEN ID PHOTO] Player left during photo series."
            );

            yield break;
        }

        CitizenIdApplicationService service =
            CitizenIdApplicationService.Instance;

        bool photoCompleted =
            service != null &&
            service.TryCompletePhoto();

        if (!photoCompleted)
        {
            AbortSequence(
                "[CITIZEN ID PHOTO] Could not mark photo as completed."
            );

            yield break;
        }

        ScheduleReturnToIdleColor();

        if (completedHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(
                completedHoldDuration
            );

        NavMeshAgent returningAgent = activeClerk.agent;

        if (returningAgent != null &&
            returningAgent.enabled &&
            returningAgent.isOnNavMesh)
        {
            returningAgent.updateRotation = false;
        }

        yield return RotateClerkTowardsDestination(
            activeClerk.deskPoint,
            360f
        );

        if (returningAgent != null &&
            returningAgent.enabled &&
            returningAgent.isOnNavMesh)
        {
            returningAgent.updateRotation = true;
        }

        bool movedBack = BeginMoveClerk(activeClerk.deskPoint);

        if (movedBack)
            yield return WaitForClerkArrival(activeClerk.deskPoint);

        SnapClerkToDeskPose();

        // Agent dostaje klatkê na synchronizacjê pozycji.
        yield return null;

        // Przy stanowisku automatyczny obrót pozostaje wy³¹czony.
        RestoreClerkAgent(restoreAutomaticRotation: false);

        // Po przywróceniu pozosta³ych ustawieñ wymuszamy ostateczn¹ pozê.
        SnapClerkToDeskPose();

        UnlockPlayerAfterPhoto();

        sequenceRunning = false;
    }

    private IEnumerator RotateClerkTowardsDestination(
    Transform destination,
    float rotationSpeed = 360f)
    {
        if (!ResolveActiveClerk() || destination == null)
            yield break;

        Transform root = activeClerk.clerkRoot;

        Vector3 direction =
            destination.position - root.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            yield break;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized);

        while (Quaternion.Angle(
                   root.rotation,
                   targetRotation) > 1f)
        {
            root.rotation = Quaternion.RotateTowards(
                root.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            yield return null;
        }

        root.rotation = targetRotation;
    }

    private IEnumerator TakePhotoSeriesRoutine()
    {
        string tellerName =
            activeClerk != null &&
            activeClerk.employee != null
                ? activeClerk.employee.EmployeeName
                : "ID TELLER";

        if (dialogueUI != null)
        {
            yield return dialogueUI.ShowTemporaryNpcLine(
                tellerName,
                lookAtCameraLine,
                dialogueLineHoldTime
            );
        }

        if (prepareDelay > 0f)
            yield return new WaitForSecondsRealtime(prepareDelay);

        int count = Mathf.Max(1, photoCount);

        for (int i = 0; i < count; i++)
        {
            if (ShouldAbortBecausePlayerLeft())
                yield break;

            if (i == 1 && dialogueUI != null)
            {
                yield return dialogueUI.ShowTemporaryNpcLine(
                    tellerName,
                    againLine,
                    dialogueLineHoldTime
                );
            }
            else if (i == 2 && dialogueUI != null)
            {
                yield return dialogueUI.ShowTemporaryNpcLine(
                    tellerName,
                    anotherOneLine,
                    dialogueLineHoldTime
                );
            }

            bool isFinalPhoto = i == count - 1;

            yield return TakeSinglePhotoRoutine(isFinalPhoto);

            if (!isFinalPhoto && delayBetweenPhotos > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    delayBetweenPhotos
                );
            }
        }

        if (dialogueUI != null)
        {
            yield return dialogueUI.ShowTemporaryNpcLine(
                tellerName,
                completedLine,
                dialogueLineHoldTime
            );

            dialogueUI.CloseTemporaryDialogue();
        }
    }

    private void CaptureOriginalAgentState()
    {
        if (originalAgentStateCaptured)
            return;

        if (!ResolveActiveClerk())
            return;

        NavMeshAgent agent = activeClerk.agent;

        originalAgentStopped = agent.isStopped;
        originalAgentUpdateRotation = agent.updateRotation;
        originalAgentUpdatePosition = agent.updatePosition;

        originalAgentStateCaptured = true;
    }

    private IEnumerator TakeSinglePhotoRoutine(bool finalPhoto)
    {
        SetPhotoStatusColor(captureColor);

        if (photoAudio != null && shutterClip != null)
            photoAudio.PlayOneShot(shutterClip);

        if (flashLight != null)
            flashLight.enabled = true;

        if (screenFlash != null)
        {
            screenFlash.Flash(
                screenFlashAlpha,
                screenFlashFadeIn,
                screenFlashHold,
                screenFlashFadeOut
            );
        }

        // W³aœciwy dokument fotografujemy podczas dzia³ania œwiat³a.
        if (finalPhoto)
        {
            lastActualPhotoCaptureSucceeded = false;

            if (photoKeyLight != null)
                photoKeyLight.enabled = true;

            if (photoFillLight != null)
                photoFillLight.enabled = true;

            try
            {
                yield return CaptureCitizenIdPhotoRoutine();
            }
            finally
            {
                if (photoKeyLight != null)
                    photoKeyLight.enabled = false;

                if (photoFillLight != null)
                    photoFillLight.enabled = false;
            }

            if (!lastActualPhotoCaptureSucceeded)
                yield break;
        }

        if (flashDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                flashDuration
            );
        }

        if (flashLight != null)
            flashLight.enabled = false;

        if (photoHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                photoHoldDuration
            );
        }

        SetPhotoStatusColor(
            finalPhoto
                ? completedColor
                : preparingColor
        );
    }

    private bool BeginMoveClerk(Transform destination)
    {
        if (!ResolveActiveClerk())
            return false;

        NavMeshAgent agent = activeClerk.agent;

        if (destination == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return false;
        }

        agent.isStopped = false;

        if (agent.hasPath)
            agent.ResetPath();

        return agent.SetDestination(destination.position);
    }

    private IEnumerator WaitForClerkArrival(Transform destination)
    {
        lastMovementSucceeded = false;

        if (!ResolveActiveClerk() || destination == null)
            yield break;

        NavMeshAgent agent = activeClerk.agent;
        float timeoutAt = Time.time + movementTimeout;

        while (agent.enabled && agent.isOnNavMesh)
        {
            if (ShouldAbortBecausePlayerLeft())
                yield break;

            if (Time.time >= timeoutAt)
            {
                if (debugLogs)
                {
                    Debug.LogWarning(
                        $"[CITIZEN ID PHOTO] Movement timeout: {destination.name}"
                    );
                }

                yield break;
            }

            if (!agent.pathPending)
            {
                if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                    yield break;

                if (agent.remainingDistance <=
                    Mathf.Max(agent.stoppingDistance, arriveTolerance))
                {
                    lastMovementSucceeded = true;
                    break;
                }
            }

            yield return null;
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;

            if (agent.hasPath)
                agent.ResetPath();
        }
    }

    private void FaceClerkTowardsPlayer()
    {
        if (!ResolveActiveClerk() || player == null)
            return;

        Transform clerkRoot = activeClerk.clerkRoot;

        Vector3 direction =
            player.position - clerkRoot.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        clerkRoot.rotation =
            Quaternion.LookRotation(direction.normalized);
    }

    private bool ShouldAbortBecausePlayerLeft()
    {
        return requirePlayerToRemainInZone &&
               !playerInside;
    }

    private void AbortSequence(string reason)
    {
        if (debugLogs)
            Debug.LogWarning(reason);

        if (statusResetCoroutine != null)
        {
            StopCoroutine(statusResetCoroutine);
            statusResetCoroutine = null;
        }

        if (flashLight != null)
            flashLight.enabled = false;

        if (photoKeyLight != null)
            photoKeyLight.enabled = false;

        if (photoFillLight != null)
            photoFillLight.enabled = false;

        screenFlash?.StopFlashImmediate();

        SetPhotoStatusColor(idleColor);

        if (ResolveActiveClerk())
        {
            NavMeshAgent agent = activeClerk.agent;

            if (agent != null &&
                agent.enabled &&
                agent.isOnNavMesh &&
                activeClerk.deskPoint != null)
            {
                agent.isStopped = false;

                if (agent.hasPath)
                    agent.ResetPath();

                agent.SetDestination(activeClerk.deskPoint.position);
            }
        }

        if (ResolveActiveClerk() &&
    originalAgentStateCaptured)
        {
            NavMeshAgent agent = activeClerk.agent;

            if (agent != null &&
                agent.enabled &&
                agent.isOnNavMesh)
            {
                agent.updatePosition = originalAgentUpdatePosition;
                agent.updateRotation = true;
            }
        }

        UnlockPlayerAfterPhoto();
        originalAgentStateCaptured = false;
        sequenceRunning = false;
        waitingForPlayerPose = false;
    }

    private void LockPlayerForPhoto()
    {
        PlayerMovement.IsMovementLocked = true;
        MouseLook.IsLookLocked = false;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (cachedWeaponManager == null)
            cachedWeaponManager = FindFirstObjectByType<WeaponManager>();

        if (cachedWeaponManager != null)
        {
            weaponManagerWasEnabled = cachedWeaponManager.enabled;
            cachedWeaponManager.enabled = false;
        }
    }

    private void UnlockPlayerAfterPhoto()
    {
        PlayerMovement.IsMovementLocked = false;
        MouseLook.IsLookLocked = false;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (playerMovement != null)
            playerMovement.enabled = true;

        if (cachedWeaponManager != null)
            cachedWeaponManager.enabled = weaponManagerWasEnabled;
    }

    private void RestoreClerkAgent(bool restoreAutomaticRotation)
    {
        if (!ResolveActiveClerk())
            return;

        NavMeshAgent agent = activeClerk.agent;

        if (!agent.enabled || !agent.isOnNavMesh)
            return;

        agent.velocity = Vector3.zero;

        if (originalAgentStateCaptured)
        {
            agent.isStopped = originalAgentStopped;
            agent.updatePosition = originalAgentUpdatePosition;

            agent.updateRotation = restoreAutomaticRotation
                ? originalAgentUpdateRotation
                : false;
        }

        originalAgentStateCaptured = false;
    }

    private void SetPhotoStatusColor(Color color)
    {
        if (photoStatusRenderer == null)
            return;

        photoStatusRenderer.GetPropertyBlock(propertyBlock);

        Material material =
            photoStatusRenderer.sharedMaterial;

        if (material != null &&
            material.HasProperty(BaseColorId))
        {
            propertyBlock.SetColor(BaseColorId, color);
        }
        else
        {
            propertyBlock.SetColor(ColorId, color);
        }

        photoStatusRenderer.SetPropertyBlock(propertyBlock);
    }

    public void SetServicingClerk(CityHallEmployee employee)
    {
        activeClerk = null;

        if (employee == null || clerks == null)
            return;

        for (int i = 0; i < clerks.Length; i++)
        {
            ClerkStation station = clerks[i];

            if (station == null)
                continue;

            if (station.employee != employee)
                continue;

            activeClerk = station;

            if (debugLogs)
            {
                Debug.Log(
                    $"[CITIZEN ID PHOTO] Servicing clerk selected: " +
                    $"{employee.EmployeeName}"
                );
            }

            return;
        }

        Debug.LogWarning(
            $"[CITIZEN ID PHOTO] No station found for clerk: " +
            $"{employee.EmployeeName}"
        );
    }

    private bool ResolveActiveClerk()
    {
        if (activeClerk != null &&
            activeClerk.clerkRoot != null &&
            activeClerk.agent != null &&
            activeClerk.deskPoint != null)
        {
            return true;
        }

        if (clerks == null)
            return false;

        for (int i = 0; i < clerks.Length; i++)
        {
            ClerkStation station = clerks[i];

            if (station == null ||
                station.clerkRoot == null ||
                station.agent == null ||
                station.deskPoint == null)
            {
                continue;
            }

            activeClerk = station;
            return true;
        }

        return false;
    }
    private void OnDisable()
    {
        waitingForPlayerPose = false;

        if (statusResetCoroutine != null)
        {
            StopCoroutine(statusResetCoroutine);
            statusResetCoroutine = null;
        }

        if (flashLight != null)
            flashLight.enabled = false;

        if (photoKeyLight != null)
            photoKeyLight.enabled = false;

        if (photoFillLight != null)
            photoFillLight.enabled = false;

        screenFlash?.StopFlashImmediate();

        SetPhotoStatusColor(idleColor);

        if (!sequenceRunning)
            return;

        dialogueUI?.CloseTemporaryDialogue();

        UnlockPlayerAfterPhoto();

        originalAgentStateCaptured = false;
        sequenceRunning = false;
    }

    private bool IsPlayerCorrectlyPositioned()
    {
        if (player == null || playerPhotoPoint == null)
            return false;

        Vector3 playerPos = player.position;
        Vector3 targetPos = playerPhotoPoint.position;

        playerPos.y = 0f;
        targetPos.y = 0f;

        float distance = Vector3.Distance(playerPos, targetPos);

        if (distance > playerPositionTolerance)
            return false;

        Vector3 requiredForward = playerPhotoPoint.forward;
        requiredForward.y = 0f;

        Vector3 playerForward = player.forward;
        playerForward.y = 0f;

        if (requiredForward.sqrMagnitude <= 0.001f ||
            playerForward.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        float angle = Vector3.Angle(
            playerForward.normalized,
            requiredForward.normalized
        );

        return angle <= playerFacingTolerance;
    }

    private void SnapPlayerToPhotoPose()
    {
        if (player == null || playerPhotoPoint == null)
            return;

        Vector3 position = player.position;
        position.x = playerPhotoPoint.position.x;
        position.z = playerPhotoPoint.position.z;

        CharacterController controller =
            player.GetComponent<CharacterController>();

        bool controllerWasEnabled =
            controller != null && controller.enabled;

        if (controllerWasEnabled)
            controller.enabled = false;

        player.position = position;

        Vector3 forward = playerPhotoPoint.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.001f)
        {
            player.rotation =
                Quaternion.LookRotation(forward.normalized);
        }

        if (controllerWasEnabled)
            controller.enabled = true;
    }

    private void SnapClerkToDeskPose()
    {
        if (!ResolveActiveClerk())
            return;

        Transform root = activeClerk.clerkRoot;
        Transform desk = activeClerk.deskPoint;
        NavMeshAgent agent = activeClerk.agent;

        if (root == null || desk == null)
            return;

        bool validAgent =
            agent != null &&
            agent.enabled &&
            agent.isOnNavMesh;

        if (validAgent)
        {
            agent.isStopped = true;
            agent.updateRotation = false;

            if (agent.hasPath)
                agent.ResetPath();

            agent.velocity = Vector3.zero;

            bool warped = agent.Warp(desk.position);

            if (!warped)
            {
                Debug.LogWarning(
                    $"[CITIZEN ID PHOTO] Warp failed for {root.name}."
                );

                root.position = desk.position;
                agent.nextPosition = desk.position;
            }
        }
        else
        {
            root.position = desk.position;
        }

        root.eulerAngles = new Vector3(
            0f,
            desk.eulerAngles.y,
            0f
        );

        if (validAgent)
        {
            agent.nextPosition = root.position;
            agent.velocity = Vector3.zero;
            agent.updateRotation = false;
        }

        if (debugLogs)
        {
            Debug.Log(
                $"[CITIZEN ID PHOTO] Desk pose applied: " +
                $"clerk={root.name}, " +
                $"deskY={desk.eulerAngles.y:F1}, " +
                $"rootY={root.eulerAngles.y:F1}"
            );
        }
    }

    private void ScheduleReturnToIdleColor()
    {
        if (statusResetCoroutine != null)
            StopCoroutine(statusResetCoroutine);

        statusResetCoroutine =
            StartCoroutine(ReturnToIdleColorRoutine());
    }

    private IEnumerator ReturnToIdleColorRoutine()
    {
        if (returnToIdleDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                returnToIdleDelay
            );
        }

        SetPhotoStatusColor(idleColor);
        statusResetCoroutine = null;

        if (debugLogs)
        {
            Debug.Log(
                "[CITIZEN ID PHOTO] Camera status returned to idle."
            );
        }
    }

    private IEnumerator CaptureCitizenIdPhotoRoutine()
    {
        lastActualPhotoCaptureSucceeded = false;

        if (photoCapture == null)
        {
            Debug.LogWarning(
                "[CITIZEN ID PHOTO] CitizenIdPhotoCapture missing."
            );

            yield break;
        }

        CitizenIdApplicationService service =
            CitizenIdApplicationService.Instance;

        if (service == null ||
            service.CurrentApplication == null)
        {
            Debug.LogWarning(
                "[CITIZEN ID PHOTO] Application record missing."
            );

            yield break;
        }

        bool completed = false;
        bool succeeded = false;
        string capturedPath = null;
        string failureReason = null;

        string holderName =
            service.CurrentApplication.holderName;

        photoCapture.Capture(
            holderName,
            (texture, filePath) =>
            {
                capturedPath = filePath;
                succeeded = true;
                completed = true;
            },
            reason =>
            {
                failureReason = reason;
                succeeded = false;
                completed = true;
            }
        );

        while (!completed)
            yield return null;

        if (!succeeded)
        {
            Debug.LogWarning(
                $"[CITIZEN ID PHOTO] Capture failed: " +
                $"{failureReason}"
            );

            yield break;
        }

        bool attached =
            service.TryAttachPhoto(capturedPath);

        if (!attached)
        {
            Debug.LogWarning(
                "[CITIZEN ID PHOTO] " +
                "Photo was saved but could not be attached."
            );

            yield break;
        }

        lastActualPhotoCaptureSucceeded = true;

        if (debugLogs)
        {
            Debug.Log(
                $"[CITIZEN ID PHOTO] Actual document photo saved: " +
                $"{capturedPath}"
            );
        }
    }
}