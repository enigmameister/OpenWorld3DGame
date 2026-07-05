using UnityEngine;
using UnityEngine.AI;

public class NPCReactive : MonoBehaviour
{
    // =========================================================
    // INTERACTION / BARK
    // =========================================================

    [Header("Interaction / Bark")]
    [SerializeField] private float interactRadius = 2.5f;

    [Tooltip("If TRUE, regular NPC bark is triggered automatically when player enters NPC trigger.")]
    [SerializeField] private bool barkOnTriggerEnter = true;

    [Tooltip("If TRUE, special NPCs use interact key instead of automatic bark.")]
    [SerializeField] private bool useKeyInteraction = false;

    [Tooltip("If TRUE, NPC can repeat bark lines while player remains nearby.")]
    [SerializeField] private bool repeatBarkWhileNearby = false;

    [SerializeField] private float barkCooldown = 3.0f;
    [SerializeField] private Vector2 repeatBarkIntervalRange = new Vector2(4f, 8f);

    [Header("Interaction Tracking")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float lingerTime = 2f;
    [SerializeField] private float interactFaceDuration = 0.6f;

    // =========================================================
    // ROTATION / FACING
    // =========================================================

    [Header("Rotation Speed")]
    [SerializeField] private float interactTurnSpeed = 720f;
    [SerializeField] private float idleTurnSpeed = 240f;
    [SerializeField] private float returnTurnSpeed = 180f;

    [Header("Smooth Rotation")]
    [SerializeField] private bool useSmoothTurn = true;
    [SerializeField] private float interactSmoothTime = 0.12f;
    [SerializeField] private float idleSmoothTime = 0.18f;
    [SerializeField] private float returnSmoothTime = 0.25f;
    [SerializeField] private float maxTurnSpeed = 720f;

    // =========================================================
    // VISUAL FEEDBACK
    // =========================================================

    [Header("Interaction Color")]
    [SerializeField] private Color interactColor = Color.black;
    [SerializeField] private Transform bodyRoot;

    // =========================================================
    // BARK UI
    // =========================================================

    [Header("NPC Bark UI")]
    [SerializeField] private string npcDisplayName = "NPC";

    [TextArea]
    [SerializeField] private string[] barkLines = { "Hi", "What's up?", "Watch out!" };

    // =========================================================
    // AIM / AGGRO DETECTION
    // =========================================================

    [Header("Aim Detection")]
    [SerializeField] private float aimAngleThreshold = 15f;

    [Header("Aiming Sensitivity")]
    [SerializeField] private bool requireADSForAggro = true;
    [SerializeField, Range(2f, 25f)] private float strictAimAngle = 8f;

    [Header("Instant Aggro Reaction")]
    [SerializeField] private bool instantAggroOnAim = true;
    [SerializeField] private float aimMaxDistance = 50f;
    [SerializeField] private float quickDrawAggroWindow = 0.25f;
    [SerializeField] private float minAimHoldTime = 0.10f;

    [Header("Line Of Sight")]
    [SerializeField] private bool autoSuggestLosMask = true;
    [SerializeField] private LayerMask losObstaclesMask;

    [Header("Witness Reaction")]
    [SerializeField] private float witnessRadius = 25f;

    // =========================================================
    // OPTIMIZATION
    // =========================================================

    [Header("Optimization")]
    [SerializeField] private float activeGunRefreshInterval = 0.15f;

    // =========================================================
    // CACHED REFERENCES
    // =========================================================

    private Transform player;
    private Camera playerCam;
    private WeaponManager weaponManager;
    private NPCController npcController;
    private NPCMelee npcMelee;
    private NpcBarkUI barkUI;
    private NavMeshAgent agent;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private Renderer[] bodyRenderers;
    private MaterialPropertyBlock mpb;

    private Color defaultColor = Color.white;
    private Quaternion originalRotation;

    private bool sessionActive;
    private bool playerInsideTrigger;

    private bool agentStoppedBeforeInteraction;
    private bool agentWasSuspendedForInteraction;

    private bool lastHands = true;
    private int lastSlot = -1;

    private float turnVelocity;
    private float lastSeenTime = -999f;
    private float interactFaceUntil = -1f;
    private float lastSwitchTime = -999f;
    private float aimOnMeSince = -1f;
    private float nextBarkTime;
    private float nextNearbyBarkTime;
    private float nextActiveGunRefreshTime;

    private Gun cachedActivePlayerGun;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    private bool InteractPressedThisFrame =>
        PlayerInputHandler.Instance != null &&
        PlayerInputHandler.Instance.InteractPressed;

    // =========================================================
    // UNITY
    // =========================================================

    private void OnEnable()
    {
        NPCController.OnNPCDied += OnNpcDiedGlobal;
    }

    private void OnDisable()
    {
        NPCController.OnNPCDied -= OnNpcDiedGlobal;
        ResumeAgentAfterInteraction();
    }

    private void Awake()
    {
        npcController = GetComponent<NPCController>();
        npcMelee = GetComponent<NPCMelee>();
        agent = GetComponent<NavMeshAgent>();

        mpb = new MaterialPropertyBlock();
        originalRotation = transform.rotation;

        RefreshBodyRenderers();
    }

    private void Start()
    {
        ResolveSceneRefs();
        ResolveCamera();

        if (weaponManager != null)
        {
            lastHands = weaponManager.IsUsingHandsOnly();
            lastSlot = weaponManager.GetCurrentWeaponIndex();
        }

        if (npcController != null)
        {
            defaultColor = npcController.DefaultColor;
            npcController.RecomputeBaseColor();
        }
        else
        {
            defaultColor = ReadDefaultRendererColor();
            ApplyBodyColor(defaultColor);
        }

        EnsureLosMask();
        ScheduleNextNearbyBark();
    }

    private void Update()
    {
        if (DevConsole.IsOpen)
            return;

        ResolveMissingRuntimeRefs();

        if (player == null)
            return;

        if (IsInteractionBlockedByNpcState())
        {
            StopInteractionSession(restoreColor: true);
            return;
        }

        TrackWeaponSwitch();

        if (TryReactToPlayerAim())
            return;

        HandleKeyInteraction();
        HandleRepeatBark();
        UpdateInteractionSession();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        interactRadius = Mathf.Max(0.1f, interactRadius);
        detectionRadius = Mathf.Max(interactRadius, detectionRadius);
        lingerTime = Mathf.Max(0f, lingerTime);
        interactFaceDuration = Mathf.Max(0f, interactFaceDuration);
        barkCooldown = Mathf.Max(0f, barkCooldown);
        aimMaxDistance = Mathf.Max(1f, aimMaxDistance);
        activeGunRefreshInterval = Mathf.Max(0.02f, activeGunRefreshInterval);

        if (repeatBarkIntervalRange.x < 0f)
            repeatBarkIntervalRange.x = 0f;

        if (repeatBarkIntervalRange.y < repeatBarkIntervalRange.x)
            repeatBarkIntervalRange.y = repeatBarkIntervalRange.x;
    }
#endif

    // =========================================================
    // TRIGGERS
    // =========================================================

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInsideTrigger = true;

        if (!barkOnTriggerEnter)
            return;

        if (!CanAutoBarkNow())
            return;

        StartInteraction();
        ScheduleBarkCooldowns();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInsideTrigger = false;
    }

    // =========================================================
    // INTERACTION
    // =========================================================

    private void HandleKeyInteraction()
    {
        if (!useKeyInteraction)
            return;

        if (!CanInteract())
            return;

        if (Time.time < nextBarkTime)
            return;

        float distToPlayer = Vector3.Distance(player.position, transform.position);

        if (distToPlayer > interactRadius)
            return;

        if (!InteractPressedThisFrame)
            return;

        StartInteraction();
        ScheduleBarkCooldowns();
    }

    private void HandleRepeatBark()
    {
        if (useKeyInteraction)
            return;

        if (!repeatBarkWhileNearby)
            return;

        if (!playerInsideTrigger)
            return;

        if (!CanInteract())
            return;

        if (Time.time < nextNearbyBarkTime)
            return;

        StartInteraction();
        ScheduleBarkCooldowns();
    }

    private bool CanInteract()
    {
        if (npcMelee != null)
        {
            if (npcMelee.IsDead)
                return false;

            if (npcMelee.IsAggro)
                return false;
        }

        if (npcController == null)
            return true;

        if (npcController.IsDead)
            return false;

        if (npcController.IsProvoked)
            return false;

        if (npcController.IsInteractionLocked)
            return false;

        if (npcController.IsScaredVisible)
            return false;

        NPCController.NPCReactionType type = npcController.GetReactionType();

        if (type == NPCController.NPCReactionType.Aggressive)
            return false;

        return true;
    }

    private bool CanAutoBarkNow()
    {
        if (!CanInteract())
            return false;

        return Time.time >= nextBarkTime;
    }

    private void StartInteraction()
    {
        if (!CanInteract())
            return;

        ResolveMissingRuntimeRefs();

        if (player == null)
            return;

        sessionActive = true;
        turnVelocity = 0f;

        SuspendAgentForInteraction();

        interactFaceUntil = Time.time + interactFaceDuration;
        lastSeenTime = Time.time;

        ApplyBodyColor(interactColor);
        RotateTowardsDeg(player.position, interactTurnSpeed);

        ShowBark();
    }

    private void ShowBark()
    {
        string line = GetRandomBark();

        if (barkUI == null && NPCSceneRefs.Instance != null)
            barkUI = NPCSceneRefs.Instance.BarkUI;

        if (barkUI != null)
        {
            barkUI.ShowBark(
                string.IsNullOrWhiteSpace(npcDisplayName) ? gameObject.name : npcDisplayName,
                line,
                2.25f
            );
        }
        else
        {
            Debug.Log(line);
        }
    }

    private string GetRandomBark()
    {
        if (barkLines == null || barkLines.Length == 0)
            return "Cześć.";

        return barkLines[Random.Range(0, barkLines.Length)];
    }

    private void ScheduleBarkCooldowns()
    {
        nextBarkTime = Time.time + barkCooldown;
        ScheduleNextNearbyBark();
    }

    private void ScheduleNextNearbyBark()
    {
        float min = Mathf.Max(0f, repeatBarkIntervalRange.x);
        float max = Mathf.Max(min, repeatBarkIntervalRange.y);

        nextNearbyBarkTime = Time.time + Random.Range(min, max);
    }

    // =========================================================
    // INTERACTION SESSION / ROTATION
    // =========================================================

    private void UpdateInteractionSession()
    {
        if (!sessionActive)
            return;

        if (player == null)
        {
            StopInteractionSession(restoreColor: true);
            return;
        }

        float dist = Vector3.Distance(player.position, transform.position);

        if (dist <= detectionRadius)
            lastSeenTime = Time.time;

        bool burst = Time.time <= interactFaceUntil;
        bool linger = Time.time - lastSeenTime <= lingerTime;

        if (burst || linger)
        {
            RotateTowardsDeg(player.position, burst ? interactTurnSpeed : idleTurnSpeed);
            return;
        }

        ReturnToDefaultRotationDeg(returnTurnSpeed);

        if (Quaternion.Angle(transform.rotation, originalRotation) <= 1.0f)
            StopInteractionSession(restoreColor: true);
    }

    private void StopInteractionSession(bool restoreColor)
    {
        if (!sessionActive && !agentWasSuspendedForInteraction)
            return;

        sessionActive = false;
        turnVelocity = 0f;

        if (restoreColor)
            ApplyBodyColor(defaultColor);

        ResumeAgentAfterInteraction();
    }

    private void RotateTowardsDeg(Vector3 worldPos, float speedDeg)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);

        if (!useSmoothTurn)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                speedDeg * Time.deltaTime
            );

            return;
        }

        float currentY = transform.eulerAngles.y;
        float targetY = targetRot.eulerAngles.y;

        float smoothTime = Time.time <= interactFaceUntil
            ? interactSmoothTime
            : idleSmoothTime;

        float newY = Mathf.SmoothDampAngle(
            currentY,
            targetY,
            ref turnVelocity,
            smoothTime,
            maxTurnSpeed,
            Time.deltaTime
        );

        transform.rotation = Quaternion.Euler(0f, newY, 0f);
    }

    private void ReturnToDefaultRotationDeg(float speedDeg)
    {
        if (!useSmoothTurn)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                originalRotation,
                speedDeg * Time.deltaTime
            );

            return;
        }

        float currentY = transform.eulerAngles.y;
        float targetY = originalRotation.eulerAngles.y;

        float newY = Mathf.SmoothDampAngle(
            currentY,
            targetY,
            ref turnVelocity,
            returnSmoothTime,
            maxTurnSpeed,
            Time.deltaTime
        );

        transform.rotation = Quaternion.Euler(0f, newY, 0f);
    }

    // =========================================================
    // AGGRO ON AIM
    // =========================================================

    private bool TryReactToPlayerAim()
    {
        if (npcController == null)
            return false;

        if (npcController.IsDead || npcController.IsProvoked)
            return false;

        if (!ShouldAggroOnAim())
            return false;

        if (!PlayerIsAimingAtMe())
            return false;

        npcController.ForceReactToAggression();
        StopInteractionSession(restoreColor: true);

        return true;
    }

    private bool ShouldAggroOnAim()
    {
        if (npcController == null)
            return false;

        NPCController.NPCReactionType type = npcController.GetReactionType();

        return type == NPCController.NPCReactionType.Aggressive ||
               type == NPCController.NPCReactionType.Fighter;
    }

    private bool PlayerIsAimingAtMe()
    {
        if (playerCam == null || weaponManager == null)
            return false;

        if (weaponManager.IsUsingHandsOnly())
        {
            aimOnMeSince = -1f;
            return false;
        }

        int slot = weaponManager.GetCurrentWeaponIndex();

        if (slot < 0 || slot > 3)
        {
            aimOnMeSince = -1f;
            return false;
        }

        bool adsHeld = PlayerInputHandler.Instance?.FireAltHeld ?? false;
        bool fireHeld = PlayerInputHandler.Instance?.FireHeld ?? false;

        bool isFighter =
            npcController != null &&
            npcController.GetReactionType() == NPCController.NPCReactionType.Fighter;

        bool scoped = false;

        if (slot == 1 || slot == 2)
        {
            Gun activeGun = GetCachedActivePlayerGun();
            scoped = activeGun != null && activeGun.IsScoped();
        }

        bool aimingInput = slot switch
        {
            0 => adsHeld,
            1 => adsHeld || scoped,
            2 => adsHeld || scoped,
            3 => fireHeld || adsHeld,
            _ => false
        };

        bool inQuickDrawWindow = Time.time - lastSwitchTime <= quickDrawAggroWindow;

        if (!isFighter && requireADSForAggro && !aimingInput && !inQuickDrawWindow)
        {
            aimOnMeSince = -1f;
            return false;
        }

        Vector3 camPos = playerCam.transform.position;
        Vector3 aimPoint = transform.position + Vector3.up * 1.4f;
        Vector3 toTarget = aimPoint - camPos;

        if (toTarget.sqrMagnitude <= 0.001f)
        {
            aimOnMeSince = -1f;
            return false;
        }

        float angle = Vector3.Angle(playerCam.transform.forward, toTarget.normalized);
        float angleGate = Mathf.Min(aimAngleThreshold, strictAimAngle);

        if (angle > angleGate)
        {
            aimOnMeSince = -1f;
            return false;
        }

        if (!CameraRayHitsThisNpc())
        {
            aimOnMeSince = -1f;
            return false;
        }

        if (!HasLineOfSightToAimPoint(aimPoint))
        {
            aimOnMeSince = -1f;
            return false;
        }

        if (instantAggroOnAim)
            return true;

        if (aimOnMeSince < 0f)
            aimOnMeSince = Time.time;

        return Time.time - aimOnMeSince >= minAimHoldTime;
    }

    private bool CameraRayHitsThisNpc()
    {
        Vector3 origin = playerCam.transform.position;
        Vector3 direction = playerCam.transform.forward;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, aimMaxDistance, ~0, QueryTriggerInteraction.Ignore))
            return false;

        return hit.collider != null &&
               hit.collider.transform != null &&
               hit.collider.transform.IsChildOf(transform);
    }

    private bool HasLineOfSightToAimPoint(Vector3 aimPoint)
    {
        if (playerCam == null)
            return false;

        Vector3 origin = playerCam.transform.position;
        Vector3 dir = aimPoint - origin;

        float dist = dir.magnitude;

        if (dist <= 0.001f)
            return false;

        int npcMask = LayerMask.GetMask("NPC");
        int mask = losObstaclesMask.value & ~npcMask;

        if (mask == 0)
            return true;

        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
                return true;

            return false;
        }

        return true;
    }

    private Gun GetCachedActivePlayerGun()
    {
        if (player == null)
            return null;

        if (Time.time < nextActiveGunRefreshTime)
            return cachedActivePlayerGun;

        nextActiveGunRefreshTime = Time.time + activeGunRefreshInterval;
        cachedActivePlayerGun = null;

        Gun[] guns = player.GetComponentsInChildren<Gun>(true);

        for (int i = 0; i < guns.Length; i++)
        {
            Gun gun = guns[i];

            if (gun == null)
                continue;

            if (!gun.gameObject.activeInHierarchy)
                continue;

            if (gun.isControlledByNPC)
                continue;

            cachedActivePlayerGun = gun;
            break;
        }

        return cachedActivePlayerGun;
    }

    private void TrackWeaponSwitch()
    {
        if (weaponManager == null)
            return;

        bool handsNow = weaponManager.IsUsingHandsOnly();
        int slotNow = weaponManager.GetCurrentWeaponIndex();

        if (lastHands && !handsNow && slotNow >= 0 && slotNow <= 3)
            lastSwitchTime = Time.time;

        lastHands = handsNow;
        lastSlot = slotNow;
    }

    // =========================================================
    // WITNESS REACTION
    // =========================================================

    private void OnNpcDiedGlobal(NPCController deadNpc, string attackerName)
    {
        if (deadNpc == null)
            return;

        if (deadNpc == npcController)
            return;

        if (npcController == null || npcController.IsDead)
            return;

        if (npcController.GetReactionType() == NPCController.NPCReactionType.Coward)
        {
            StopInteractionSession(restoreColor: true);
            return;
        }

        float dist = Vector3.Distance(transform.position, deadNpc.transform.position);

        if (dist > witnessRadius)
            return;

        Vector3 witnessEye = transform.position + Vector3.up * 1.6f;
        Vector3 eventPoint = deadNpc.transform.position + Vector3.up;
        Vector3 dir = eventPoint - witnessEye;

        float eventDistance = dir.magnitude;

        if (eventDistance <= 0.01f)
            return;

        int npcMask = LayerMask.GetMask("NPC");
        int mask = losObstaclesMask.value & ~npcMask;

        if (Physics.Raycast(witnessEye, dir.normalized, out RaycastHit hit, eventDistance, mask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && !hit.collider.transform.IsChildOf(deadNpc.transform))
                return;
        }

        npcController.ForceReactToAggression();
        StopInteractionSession(restoreColor: true);
    }

    // =========================================================
    // VISUALS
    // =========================================================

    private void RefreshBodyRenderers()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>(true);

        if (all == null || all.Length == 0)
        {
            bodyRenderers = System.Array.Empty<Renderer>();
            return;
        }

        int count = 0;

        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];

            if (r == null)
                continue;

            if (bodyRoot != null && !r.transform.IsChildOf(bodyRoot))
                continue;

            count++;
        }

        bodyRenderers = new Renderer[count];

        int index = 0;

        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];

            if (r == null)
                continue;

            if (bodyRoot != null && !r.transform.IsChildOf(bodyRoot))
                continue;

            bodyRenderers[index] = r;
            index++;
        }
    }

    private Color ReadDefaultRendererColor()
    {
        if (bodyRenderers == null || bodyRenderers.Length == 0)
            return Color.white;

        Renderer renderer = bodyRenderers[0];

        if (renderer == null)
            return Color.white;

        Material mat = renderer.sharedMaterial;

        if (mat == null)
            return Color.white;

        if (mat.HasProperty(BaseColorID))
            return mat.GetColor(BaseColorID);

        if (mat.HasProperty(ColorID))
            return mat.GetColor(ColorID);

        return mat.color;
    }

    private void ApplyBodyColor(Color color)
    {
        if (mpb == null)
            mpb = new MaterialPropertyBlock();

        if (bodyRenderers == null || bodyRenderers.Length == 0)
            return;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            Renderer renderer = bodyRenderers[i];

            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorID, color);
            mpb.SetColor(ColorID, color);
            renderer.SetPropertyBlock(mpb);
        }
    }

    // =========================================================
    // AGENT
    // =========================================================

    private void SuspendAgentForInteraction()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (agentWasSuspendedForInteraction)
            return;

        agentStoppedBeforeInteraction = agent.isStopped;

        agent.isStopped = true;
        agent.ResetPath();

        agentWasSuspendedForInteraction = true;
    }

    private void ResumeAgentAfterInteraction()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (!agentWasSuspendedForInteraction)
            return;

        agent.isStopped = agentStoppedBeforeInteraction;
        agentWasSuspendedForInteraction = false;
    }

    // =========================================================
    // REFS / STATE
    // =========================================================

    private void ResolveSceneRefs()
    {
        NPCSceneRefs refs = NPCSceneRefs.Instance;

        if (refs != null)
        {
            if (player == null)
                player = refs.Player;

            if (weaponManager == null)
                weaponManager = refs.WeaponManager;

            if (barkUI == null)
                barkUI = refs.BarkUI;
        }

        if (player == null)
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");

            if (playerGo != null)
                player = playerGo.transform;
        }
    }

    private void ResolveMissingRuntimeRefs()
    {
        if (player == null || weaponManager == null || barkUI == null)
            ResolveSceneRefs();

        if (playerCam == null)
            ResolveCamera();
    }

    private void ResolveCamera()
    {
        if (playerCam == null)
            playerCam = Camera.main;
    }

    private bool IsInteractionBlockedByNpcState()
    {
        if (npcMelee != null)
        {
            if (npcMelee.IsDead || npcMelee.IsAggro)
                return true;
        }

        if (npcController == null)
            return false;

        return npcController.IsDead ||
               npcController.IsProvoked ||
               npcController.IsInteractionLocked ||
               npcController.IsScaredVisible;
    }

    private void EnsureLosMask()
    {
        if (!autoSuggestLosMask)
            return;

        if (losObstaclesMask.value != 0 && losObstaclesMask.value != ~0)
            return;

        losObstaclesMask = SuggestObstacleMask();
    }

    private LayerMask SuggestObstacleMask()
    {
        int mask = LayerMask.GetMask("Default", "Obstacle", "Car", "Environment", "Building");

        if (mask == 0)
            mask = LayerMask.GetMask("Default", "Obstacle", "Car");

        return mask == 0 ? 0 : mask;
    }

    // =========================================================
    // PROFILE
    // =========================================================

    public void ApplyProfile(NPCProfile profile)
    {
        if (profile == null)
            return;

        npcDisplayName = string.IsNullOrWhiteSpace(profile.displayName)
            ? gameObject.name
            : profile.displayName;

        if (!profile.allowReactiveInteraction)
        {
            barkOnTriggerEnter = false;
            useKeyInteraction = false;
            repeatBarkWhileNearby = false;
        }

        if (profile.archetype == NPCProfile.NPCArchetype.Story ||
            profile.archetype == NPCProfile.NPCArchetype.BankEmployee)
        {
            useKeyInteraction = true;
            barkOnTriggerEnter = false;
            repeatBarkWhileNearby = false;
        }

        if (profile.archetype == NPCProfile.NPCArchetype.Civilian)
        {
            useKeyInteraction = false;
            barkOnTriggerEnter = true;
        }
    }
}