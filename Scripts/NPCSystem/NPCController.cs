using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class NPCController : MonoBehaviour, IDamageable
{
    public enum NPCReactionType
    {
        Coward,
        Aggressive,
        Fighter
    }

    public enum FighterVariant
    {
        Blue,
        Black
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public bool IsDead => isDead;
    public bool IsProvoked => isProvoked;
    public bool IsInteractionLocked => interactionDisabledForever || isFleeing;
    public bool IsScaredVisible => scaredIcon != null && scaredIcon.activeSelf;
    public Color DefaultColor => defaultColor;

    public NPCReactionType GetReactionType() => reactionType;
    public FighterVariant GetFighterVariant() => fighterVariant;

    public bool IsAggressiveBlackVariant() =>
        reactionType == NPCReactionType.Aggressive && fighterVariant == FighterVariant.Black;

    public static event System.Action<Vector3> OnCowardReportedLastKnownPos;
    public static System.Action<NPCController, string> OnNPCDied;

    // =========================================================
    // IDENTITY / CORE
    // =========================================================

    [Header("Core")]
    [SerializeField] private NPCCore core;

    [Header("Behavior Type")]
    [SerializeField] private NPCReactionType reactionType = NPCReactionType.Coward;

    [Header("Fighter Variant")]
    [SerializeField] private FighterVariant fighterVariant = FighterVariant.Blue;

    // =========================================================
    // HEALTH
    // =========================================================

    [Header("Health")]
    [SerializeField] private float maxHP = 100f;

    private float currentHP;
    private bool isDead;
    private bool deathSequenceStarted;
    private string lastAttacker = "Unknown";

    // =========================================================
    // AI SETTINGS
    // =========================================================

    [Header("AI Settings")]
    [SerializeField] private float interactRange = 2.5f;
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float engageDistance = 8f;
    [SerializeField] private float fleeDistance = 20f;
    [SerializeField] private float reactionDuration = 30f;

    [Header("Coward Flee / Interaction Lock")]
    [SerializeField] private bool propagatePanicToWitnesses = true;
    [SerializeField] private float fleeDuration = 6.0f;
    [SerializeField] private float fleeFarDistance = 18.0f;
    [SerializeField] private LayerMask npcMask;
    [SerializeField] private Collider[] interactionColliders;
    [SerializeField] private Collider[] physicalColliders;

    [Header("Fighter Coward Incidents")]
    [SerializeField] private float patrolInvestigateTime = 6f;
    [SerializeField] private float investigateArriveTolerance = 1.2f;

    [Header("Aggro Memory")]
    [SerializeField] private float losePlayerDistance = 35f;
    [SerializeField] private float searchLastSeenTime = 4f;

    private bool isProvoked;
    private bool isFleeing;
    private bool interactionDisabledForever;
    private bool inVictoryState;
    private bool _defenseMode;

    private float reactionEndTime;
    private float cowardFleeSafeUntil = -999f;
    private Vector3 lastKnownAttackerPos;

    private Vector3 lastSeenPlayerPosition;
    private float lastSeenPlayerTime = -999f;
    private bool searchingLastSeenPosition;

    // =========================================================
    // MOVEMENT / NAVIGATION
    // =========================================================

    [Header("Vertical Awareness")]
    [SerializeField] private float verticalAimTolerance = 2.5f;
    [SerializeField] private float campUnderPlayerRadius = 4.0f;
    [SerializeField] private float campRepathInterval = 0.8f;
    [SerializeField] private float maxApproachSpeedWhenCamping = 6.5f;

    [Header("Combat Positioning")]
    [SerializeField] private float faceAngleThreshold = 15f;
    [SerializeField] private float weaponDrawTime = 0.05f;
    [SerializeField] private float desiredShootingDistance = 12f;
    [SerializeField] private float retreatBuffer = 0.75f;

    private NavMeshAgent agent;
    private Transform player;
    private PlayerStats playerStats;

    private Quaternion startRotation;
    private Coroutine attackCoroutine;
    private float _nextRepath;

    private Vector3 _lastPlayerPos;
    private bool _playerPosInitialized;

    [Header("Combat Repath Optimization")]
    [SerializeField] private float chaseRepathInterval = 0.18f;
    [SerializeField] private float retreatRepathInterval = 0.22f;
    [SerializeField] private float minCombatDestinationMoveDelta = 0.75f;

    private float nextCombatDestinationUpdateTime;
    private Vector3 lastCombatDestination;
    private bool hasLastCombatDestination;

    // =========================================================
    // PERCEPTION / VISION / HEARING
    // =========================================================

    [Header("Vision")]
    [SerializeField] private float viewDistance = 30f;
    [SerializeField, Range(10f, 180f)] private float viewAngle = 110f;
    [SerializeField] private LayerMask losObstaclesMask = ~0;

    [Header("Shot Hearing")]
    [SerializeField] private float shotHearRadius = 18f;
    [SerializeField] private float shotLOSProbeHeight = 1.6f;
    [SerializeField] private float investigateFromShotTime = 4.0f;

    [Header("Shot Reaction")]
    [SerializeField] private float reactShotMaxDistance = 30f;
    [SerializeField] private float nearMissThreshold = 2.5f;
    [SerializeField] private LayerMask shotHearingObstaclesMask = ~0;

    private bool investigatingShot;
    private float investigateShotUntil;
    private Vector3 lastShotPoint;

    [Header("Rear Awareness / Sprint Noise")]
    [SerializeField] private bool useRearSprintAwareness = true;

    [Tooltip("NPC reacts when the player sprints with a weapon behind or near their back.")]
    [SerializeField] private bool rearAwarenessRequiresHeldWeapon = true;

    [Tooltip("NPC reacts when the player moves fast behind their back within this radius.")]
    [SerializeField] private float rearAwarenessRadius = 5.0f;

    [Tooltip("Minimum horizontal player speed required to trigger rear awareness.")]
    [SerializeField] private float rearAwarenessMinSpeed = 3.2f;

    [Tooltip("Player must be behind or rear-side of the NPC by at least this angle.")]
    [SerializeField, Range(60f, 179f)] private float rearAwarenessMinBackAngle = 90f;

    [Tooltip("How often this NPC can react to sprint noise from behind.")]
    [SerializeField] private float rearAwarenessCooldown = 0.35f;

    [Tooltip("If TRUE, walls block rear awareness.")]
    [SerializeField] private bool rearAwarenessRequiresLineOfSight = true;

    [Tooltip("How long the NPC keeps looking at the player after hearing sprint noise.")]
    [SerializeField] private float rearAwarenessLookHoldTime = 1.0f;

    [Tooltip("How long rear awareness suppresses automatic aggro from normal vision.")]
    [SerializeField] private float rearAwarenessAutoAggroSuppressTime = 1.0f;

    [SerializeField] private LayerMask rearAwarenessObstacleMask = ~0;

    [Tooltip("How long the NPC stops after hearing sprint noise.")]
    [SerializeField] private float rearAwarenessStopDuration = 1.0f;

    private float rearAwarenessStopUntil;
    private bool rearAwarenessPausedAgent;
    private float nextRearAwarenessTime;
    private float rearAwarenessLookUntil;
    private float rearAwarenessSuppressAutoAggroUntil;
    private Vector3 rearAwarenessLookTarget;

    private CharacterController playerCharacterController;
    private Rigidbody playerRigidbody;
    private PlayerMovement playerMovement;
    private WeaponManager playerWeaponManager;
    private Vector3 lastRearAwarenessPlayerPos;
    private bool rearAwarenessPlayerPosInitialized;

    // =========================================================
    // NPC SHOOTING
    // =========================================================

    [Header("NPC Shooting")]
    [SerializeField] private int shotsPerBurst = 2;
    [SerializeField] private float fireCooldown = 1.5f;
    [SerializeField] private float aimDelay = 0.15f;
    [SerializeField] private float minShootingDistance = 5f;

    [Header("Friendly Fire")]
    [SerializeField] private bool avoidSameTypeFriendlyFire = true;

    [Tooltip("Small radius used to detect friendly NPCs in the shooting line.")]
    [SerializeField] private float friendlyFireProbeRadius = 0.18f;

    private static readonly RaycastHit[] FriendlyFireHits = new RaycastHit[16];

    // =========================================================
    // WEAPONS / LOOT
    // =========================================================

    [Header("NPC Weapons")]
    [SerializeField] private bool useWeaponSystem = true;
    [SerializeField] private NPCGun[] availableWeapons;
    [SerializeField] private Transform weaponsListRoot;
    [SerializeField] private string assignedWeaponName;

    [Header("Drop / Loot")]
    [SerializeField] private bool allowWeaponDrop = true;
    [Range(0f, 100f)]
    [SerializeField] private float weaponDropChance = 50f;
    [SerializeField] private Transform weaponDropPoint;

    [Header("Weapon Pickup Prefabs")]
    [SerializeField] private GameObject GlockPickup;
    [SerializeField] private GameObject M4A1Pickup;
    [SerializeField] private GameObject AK97Pickup;
    [SerializeField] private GameObject SPAS12Pickup;

    private NPCGun equippedGun;

    // =========================================================
    // VISUALS / HIT FEEDBACK
    // =========================================================

    [Header("Body Colors / Hit Flash")]
    [SerializeField] private Renderer[] bodyRenderers;
    [SerializeField] private float hitFlashDuration = 0.5f;

    [Header("Hit FX")]
    [SerializeField] private GameObject bloodFxPrefab;
    [SerializeField] private float bloodFxScale = 1f;
    [SerializeField] private float bloodFxLifetime = 2f;
    [SerializeField] private AudioClip hurtSfx;
    [SerializeField] private AudioSource audioSource;

    private Color defaultColor;
    private MaterialPropertyBlock _mpb;
    private Coroutine flashCoroutine;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    // =========================================================
    // ALERT ICONS
    // =========================================================

    [Header("Alert Icons")]
    [SerializeField] private GameObject alertIcon;
    [SerializeField] private SpriteRenderer alertSprite;
    [SerializeField] private GameObject scaredIcon;
    [SerializeField] private SpriteRenderer scaredSpriteRenderer;

    // =========================================================
    // RAGDOLL / PHYSICS
    // =========================================================

    private Rigidbody rootRb;
    private Collider rootCol;

    private bool pendingBackstabFall;
    private bool pendingMeleeFall;
    private Vector3 pendingMeleeAttackerPos;

    private bool pendingVehicleFall;
    private Vector3 pendingVehicleVelocity;
    private float pendingVehicleSpeedKmh;

    // =========================================================
    // CACHED COMPONENTS
    // =========================================================

    private Animator cachedAnimator;
    private NPCReactive cachedReactive;
    private Billboard cachedBillboard;

    // =========================================================
    // OPTIMIZATION
    // =========================================================

    [Header("Optimization")]
    [SerializeField] private float visionRefreshInterval = 0.12f;

    private static readonly Collider[] PanicOverlapBuffer = new Collider[32];
    private float nextVisionCheckTime;
    private bool cachedPlayerInFrontAndVisible;

    private static float panicPropagationRadius = 15f;

    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rootRb = GetComponent<Rigidbody>();
        cachedAnimator = GetComponentInChildren<Animator>(true);
        cachedReactive = GetComponent<NPCReactive>();
        cachedBillboard = GetComponent<Billboard>();

        if (core == null)
            core = GetComponent<NPCCore>();

        var cols = GetComponents<Collider>();
        rootCol = null;

        if (weaponsListRoot == null)
        {
            Transform body = transform.Find("Body");
            if (body != null)
                weaponsListRoot = body.Find("WeaponsList");
        }

        foreach (var c in cols)
        {
            if (c != null && !c.isTrigger)
            {
                rootCol = c;
                break;
            }
        }

        if ((rootCol == null || rootCol.isTrigger) && physicalColliders != null)
        {
            for (int i = 0; i < physicalColliders.Length; i++)
            {
                var c = physicalColliders[i];
                if (c != null && !c.isTrigger)
                {
                    rootCol = c;
                    break;
                }
            }
        }

        startRotation = transform.rotation;

        ResolvePlayerRefs();

        RefreshBodyRenderers();
        _mpb = new MaterialPropertyBlock();

        RecomputeBaseColor();
        currentHP = maxHP;

        if (!alertSprite && alertIcon) alertSprite = alertIcon.GetComponent<SpriteRenderer>();
        HideAllIcons();

        if (scaredIcon && !scaredSpriteRenderer)
            scaredSpriteRenderer = scaredIcon.GetComponent<SpriteRenderer>();
        if (scaredIcon) scaredIcon.SetActive(false);

        if (rootRb != null)
        {
            rootRb.isKinematic = true;
            rootRb.useGravity = false;
            rootRb.interpolation = RigidbodyInterpolation.Interpolate;
            rootRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    private void Start()
    {
        if (useWeaponSystem)
            AssignRandomWeapon();

        RefreshBodyRenderers();
        ApplyBodyColor(defaultColor);

        InvokeRepeating(nameof(PickNewDestination), 5f, 10f);

        if (reactionType == NPCReactionType.Aggressive && fighterVariant == FighterVariant.Black)
        {
            DisableInteractionOnly();
            HolsterWeapon(true);
            HideAllIcons();
        }
    }

    private void Update()
    {
        if (CheatState.Alliance)
        {
            if (isProvoked)
            {
                isProvoked = false;
                HolsterWeapon(true);
            }

            HideAllIcons();
            return;
        }

        if (isDead)
            return;

        if (!TryResolvePlayerRefs())
            return;

        if (inVictoryState)
            return;

        TickRearSprintAwareness();
        TickRearAwarenessLookAtPlayer();

        if (reactionType == NPCReactionType.Fighter)
        {
            if (_defenseMode && !isProvoked && CanAutoAggroFromVision() && PlayerInFrontAndVisible())
                StartAggression(byHit: false);
        }

        if (reactionType == NPCReactionType.Aggressive && !isProvoked)
        {
            if (CanAutoAggroFromVision() && PlayerInFrontAndVisible())
                StartAggression(byHit: false);
        }

        if (isProvoked &&
            reactionType != NPCReactionType.Fighter &&
            reactionType != NPCReactionType.Aggressive)
        {
            Vector3 targetPos = GetCurrentTargetPosition();
            float maxResetDistance = 25f;
            float sqrDistance = (transform.position - targetPos).sqrMagnitude;

            if (Time.time > reactionEndTime || sqrDistance > maxResetDistance * maxResetDistance)
            {
                isProvoked = false;

                if (agent != null && agent.enabled && agent.isOnNavMesh)
                    agent.isStopped = false;

                if (UsesLocalColorFeedback())
                    ApplyBodyColor(defaultColor);

                HolsterWeapon(true);

                if (attackCoroutine != null)
                {
                    StopCoroutine(attackCoroutine);
                    attackCoroutine = null;
                }

                HideAllIcons();
            }
        }

        if (isProvoked &&
        reactionType == NPCReactionType.Aggressive &&
        player != null &&
        !isDead)
        {
            HandleAggressiveMemory();
        }

        HandleMoveFacing();

        if (!isDead && investigatingShot)
        {
            FacePositionXZ(lastShotPoint);

            if (Time.time > investigateShotUntil)
            {
                investigatingShot = false;
            }
            else if (agent && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= investigateArriveTolerance)
            {
                investigateShotUntil = Mathf.Min(investigateShotUntil, Time.time + 1.25f);
            }
        }
    }

    private bool CanAutoAggroFromVision()
    {
        return Time.time >= rearAwarenessSuppressAutoAggroUntil;
    }

    private void OnEnable()
    {
        PlayerStats.OnPlayerDied += HandlePlayerDied;

        OnCowardReportedLastKnownPos += HandleCowardReported;

        Gun.OnPlayerShot += OnPlayerShotHeard;
    }
    private void OnDisable()
    {
        PlayerStats.OnPlayerDied -= HandlePlayerDied;

        OnCowardReportedLastKnownPos -= HandleCowardReported;

        Gun.OnPlayerShot -= OnPlayerShotHeard;
    }

    public void ForceReactToAggression()
    {
        if (isDead || deathSequenceStarted || inVictoryState)
            return;

        StartAggression(byHit: false);
    }

    public void RecomputeBaseColor()
    {
        defaultColor = ChooseBaseColor();
        ApplyBodyColor(defaultColor);
    }

    public void ApplyProfile(NPCProfile profile)
    {
        if (profile == null) return;

        reactionType = profile.reactionType;
        fighterVariant = profile.fighterVariant;

        maxHP = Mathf.Max(1f, profile.maxHP);
        currentHP = maxHP;

        useWeaponSystem = profile.useWeaponSystem;
        allowWeaponDrop = profile.allowWeaponDrop;
        weaponDropChance = profile.weaponDropChance;

        if (profile.availableWeapons != null && profile.availableWeapons.Length > 0)
            availableWeapons = profile.availableWeapons;

        RecomputeBaseColor();

        if (!useWeaponSystem)
        {
            HideAllNpcWeapons();
        }
    }

    public void SetReactionType(NPCReactionType type)
    {
        reactionType = type;
        RecomputeBaseColor();
    }

    static float DistancePointToRay(Vector3 point, Vector3 rayOrigin, Vector3 rayDirNormalized)
    {
        Vector3 toPoint = point - rayOrigin;
        float t = Vector3.Dot(toPoint, rayDirNormalized);
        if (t <= 0f) return toPoint.magnitude;
        Vector3 proj = rayOrigin + rayDirNormalized * t;
        return Vector3.Distance(point, proj);
    }
    private void OnPlayerShotHeard(Vector3 shotOrigin, Vector3 shotDir, Vector3 impactPoint)
    {
        if (isDead || CheatState.Alliance) return;

        float dOrigin = Vector3.Distance(transform.position, shotOrigin);
        float dImpact = Vector3.Distance(transform.position, impactPoint);
        float dMin = Mathf.Min(dOrigin, dImpact);
        if (dMin > reactShotMaxDistance) return;

        float miss = DistancePointToRay(transform.position + Vector3.up * shotLOSProbeHeight, shotOrigin, shotDir.normalized);
        float distToImpact = Vector3.Distance(transform.position, impactPoint);
        bool nearMiss = miss <= nearMissThreshold;
        bool closeImpact = distToImpact <= shotHearRadius;
        if (!nearMiss && !closeImpact) return;

        Vector3 eye = transform.position + Vector3.up * shotLOSProbeHeight;
        Vector3 toCheck = (nearMiss ? (shotOrigin - eye) : (impactPoint - eye));
        var maskNoNPC = shotHearingObstaclesMask & ~LayerMask.GetMask("NPC");

        if (Physics.Raycast(eye, toCheck.normalized, out RaycastHit block, toCheck.magnitude,
                            maskNoNPC, QueryTriggerInteraction.Ignore))
        {
            if (!block.collider.transform.IsChildOf(transform)) return;
        }

        Vector3 focus = nearMiss ? shotOrigin : impactPoint;
        FacePositionXZ(focus);

        if (reactionType == NPCReactionType.Coward)
        {
            propagatePanicToWitnesses = false;
            DisableInteractionAndCollisionForever();
            StartCowardFlee();

            ShowScared(new Color(1f, 0.85f, 0.2f)); 
            return;
        }

        if (!isProvoked) StartAggression(byHit: false);
        reactionEndTime = Time.time + reactionDuration;

        investigatingShot = true;
        lastShotPoint = impactPoint;
        investigateShotUntil = Time.time + investigateFromShotTime;

        if (agent && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = 6.5f * GetWeaponSpeedMulForNPC();
            agent.speed = Mathf.Max(agent.speed, maxApproachSpeedWhenCamping * GetWeaponSpeedMulForNPC());
            agent.SetDestination(lastShotPoint);
        }
        if (alertIcon)
        {
            SetAlertColor(Color.red);
            alertIcon.SetActive(true);
        }

    }

    private void ShowAlert(Color c)
    {
        if (scaredIcon) scaredIcon.SetActive(false);
        if (alertIcon)
        {
            if (alertSprite) alertSprite.color = c;
            alertIcon.SetActive(true);
        }
    }

    private void ShowScared(Color? tint = null)
    {
        HideAllIcons();

        if (scaredIcon)
        {
            if (scaredSpriteRenderer && tint.HasValue)
                scaredSpriteRenderer.color = tint.Value;
            scaredIcon.SetActive(true);
        }
    }

    private void HideAllIcons()
    {
        if (alertIcon) alertIcon.SetActive(false);
        if (scaredIcon) scaredIcon.SetActive(false);
    }

    private void SetAlertColor(Color c)
    {
        if (alertSprite) alertSprite.color = c;
    }

    private bool PlayerInFrontAndVisibleRaw()
    {
        if (CheatState.Alliance) return false;

        if (player == null) return false;

        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Vector3 targetPos = GetCurrentTargetPosition();
        Vector3 to = (targetPos + Vector3.up * 1.4f) - eye;

        float dist = to.magnitude;
        if (dist > viewDistance) return false;

        Vector3 flatTo = to; flatTo.y = 0f;
        if (flatTo.sqrMagnitude < 0.0001f) return false;

        float angle = Vector3.Angle(transform.forward, flatTo.normalized);
        if (angle > viewAngle * 0.5f) return false;

        if (Physics.Raycast(eye, to.normalized, out RaycastHit hit, dist, losObstaclesMask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    private void TickRearSprintAwareness()
    {
        if (!CanUseRearSprintAwareness())
            return;

        if (Time.time < nextRearAwarenessTime)
            return;

        Vector3 targetPos = GetCurrentTargetPosition();
        Vector3 toPlayer = targetPos - transform.position;
        toPlayer.y = 0f;

        float radiusSqr = rearAwarenessRadius * rearAwarenessRadius;

        if (toPlayer.sqrMagnitude > radiusSqr)
            return;

        if (toPlayer.sqrMagnitude < 0.001f)
            return;

        float playerSpeed = GetPlayerHorizontalSpeed();

        if (playerSpeed < rearAwarenessMinSpeed)
            return;

        float angle = Vector3.Angle(transform.forward, toPlayer.normalized);

        if (angle < rearAwarenessMinBackAngle)
            return;

        if (rearAwarenessRequiresLineOfSight && IsRearAwarenessBlocked(targetPos))
            return;

        nextRearAwarenessTime = Time.time + rearAwarenessCooldown;

        BeginRearAwarenessLook(targetPos);

        if (debugLogs)
        {
            Debug.Log(
                $"[NPC] Rear awareness look -> {name}, " +
                $"speed={playerSpeed:0.00}, angle={angle:0.0}, dist={Mathf.Sqrt(toPlayer.sqrMagnitude):0.00}"
            );
        }
    }

    private void TickRearAwarenessLookAtPlayer()
    {
        if (Time.time >= rearAwarenessLookUntil)
        {
            ReleaseRearAwarenessStopIfNeeded();
            return;
        }

        if (isDead || deathSequenceStarted || inVictoryState)
        {
            ReleaseRearAwarenessStopIfNeeded();
            return;
        }

        if (isProvoked || isFleeing)
        {
            ReleaseRearAwarenessStopIfNeeded();
            return;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;

        Vector3 targetPos = player != null
            ? GetCurrentTargetPosition()
            : rearAwarenessLookTarget;

        rearAwarenessLookTarget = targetPos;
        FacePositionXZ(targetPos);
    }


    private bool CanUseRearSprintAwareness()
    {
        if (!useRearSprintAwareness)
            return false;

        if (isDead || deathSequenceStarted || inVictoryState)
            return false;

        if (player == null)
            return false;

        if (core != null)
        {
            if (core.Importance != NPCCore.NPCImportance.Ambient)
                return false;

            if (core.IsInvulnerable || core.PreventDeath)
                return false;
        }

        if (reactionType == NPCReactionType.Coward && (isFleeing || IsScaredVisible))
            return false;

        if (rearAwarenessRequiresHeldWeapon && !PlayerHasWeaponInHands())
            return false;

        return true;
    }

    private float GetPlayerHorizontalSpeed()
    {
        if (player == null)
            return 0f;

        if (playerMovement == null ||
            playerCharacterController == null ||
            playerRigidbody == null)
        {
            CachePlayerMotionRefs();
        }

        if (playerMovement != null && playerMovement.IsTryingToSprint)
        {
            return Mathf.Max(rearAwarenessMinSpeed + 0.5f, 5f);
        }

        Vector3 velocity = Vector3.zero;

        if (playerCharacterController != null)
        {
            velocity = playerCharacterController.velocity;
        }
        else if (playerRigidbody != null)
        {
            velocity = playerRigidbody.linearVelocity;
        }
        else
        {
            Vector3 currentPos = player.position;

            if (!rearAwarenessPlayerPosInitialized)
            {
                rearAwarenessPlayerPosInitialized = true;
                lastRearAwarenessPlayerPos = currentPos;
                return 0f;
            }

            velocity = (currentPos - lastRearAwarenessPlayerPos) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastRearAwarenessPlayerPos = currentPos;
        }

        velocity.y = 0f;
        return velocity.magnitude;
    }

    private bool IsRearAwarenessBlocked(Vector3 targetPos)
    {
        if (player == null)
            return false;

        Vector3 eye = transform.position + Vector3.up * 1.4f;
        Vector3 target = targetPos + Vector3.up * 1.2f;
        Vector3 toTarget = target - eye;

        float distance = toTarget.magnitude;

        if (distance <= 0.001f)
            return false;

        int playerLayer = LayerMask.NameToLayer("Player");
        int npcLayer = LayerMask.NameToLayer("NPC");

        LayerMask mask = rearAwarenessObstacleMask;

        if (playerLayer >= 0)
            mask &= ~(1 << playerLayer);

        if (npcLayer >= 0)
            mask &= ~(1 << npcLayer);

        if (Physics.Raycast(
                eye,
                toTarget.normalized,
                out RaycastHit hit,
                distance,
                mask,
                QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform))
                return false;

            if (hit.collider.transform.IsChildOf(player))
                return false;

            return true;
        }

        return false;
    }

    private bool PlayerInFrontAndVisible()
    {
        if (Time.time < nextVisionCheckTime)
            return cachedPlayerInFrontAndVisible;

        nextVisionCheckTime = Time.time + visionRefreshInterval;
        cachedPlayerInFrontAndVisible = PlayerInFrontAndVisibleRaw();

        return cachedPlayerInFrontAndVisible;
    }

    // ===== KOLOR / FLASH =====
    private Color ChooseBaseColor()
    {
        switch (reactionType)
        {
            case NPCReactionType.Fighter: return Color.blue;   // Fighter
            case NPCReactionType.Aggressive: return Color.black;  // Aggressive
            case NPCReactionType.Coward: return GetRandomCowardColor();
            default: return Color.gray;
        }
    }

    private Color GetRandomCowardColor()
    {
        Color[] palette =
        {
        Color.green, Color.cyan, /* Color.yellow, */ Color.white, Color.gray,
        new Color(1f, 0.5f, 0f),      // orange
        new Color(0.6f, 0.2f, 0.8f),  // violet
        new Color(0.3f, 0.8f, 0.3f)   // light green
    };
        return palette[Random.Range(0, palette.Length)];
    }

    private void ApplyBodyColor(Color c)
    {
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();

        if (bodyRenderers == null || bodyRenderers.Length == 0)
            return;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            Renderer r = bodyRenderers[i];

            if (r == null)
                continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorID, c);
            _mpb.SetColor(ColorID, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    private bool UsesLocalColorFeedback()
    {
        return core == null;
    }

    private void DisableInteractionOnly()
    {
        if (interactionColliders != null)
            foreach (var c in interactionColliders) if (c) c.enabled = false;

        interactionDisabledForever = true;
    }

    private IEnumerator FlashRedCoroutine(float duration)
    {
        if (isDead || !UsesLocalColorFeedback())
            yield break;

        ApplyBodyColor(Color.red);

        yield return new WaitForSeconds(duration);

        if (!isDead)
            ApplyBodyColor(defaultColor);

        flashCoroutine = null;
    }

    private void RestartLocalHitFlash()
    {
        if (!UsesLocalColorFeedback())
            return;

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(FlashRedCoroutine(hitFlashDuration));
    }

    // ===== PATROL / MOVE =====
    private void PickNewDestination()
    {
        if (isDead) return;
        if (player != null && Vector3.Distance(transform.position, GetCurrentTargetPosition()) < detectionRadius) return;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        Vector3 randomDirection = Random.insideUnitSphere * 20f; randomDirection.y = 0f;
        if (NavMesh.SamplePosition(transform.position + randomDirection, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    private void HandleMoveFacing()
    {
        if (Time.time < rearAwarenessLookUntil)
            return;

        if (isProvoked || inVictoryState || agent == null || !agent.enabled || !agent.hasPath)
            return;

        Vector3 dir = agent.desiredVelocity; dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            var rot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 5f);
        }
    }

    private void FacePositionXZ(Vector3 pos)
    {
        Vector3 dir = pos - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        var rot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, Time.deltaTime * 720f);
    }

    // ===== AGGRO / ATTACK =====
    private void StartAggression(bool byHit)
    {
        if (isDead || deathSequenceStarted || inVictoryState)
            return;

        ClearRearAwarenessHold();

        if (!TryResolvePlayerRefs())
            return;

        Vector3 targetPos = GetCurrentTargetPosition();

        lastSeenPlayerPosition = targetPos;
        lastSeenPlayerTime = Time.time;
        searchingLastSeenPosition = false;
        reactionEndTime = Time.time + reactionDuration;

        ResetCombatDestinationCache();
        DisableInteractionOnly();
        ShowAlert(Color.red);

        if (!_playerPosInitialized)
        {
            _lastPlayerPos = targetPos;
            _playerPosInitialized = true;
        }

        if (isProvoked && attackCoroutine != null)
            return;

        isProvoked = true;

        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(AttackSequence(byHit));
    }

    // =========================================================
    // ATTACK SEQUENCE
    // =========================================================

    private struct CombatFrame
    {
        public Vector3 targetPos;
        public float horizontalDistance;
        public bool verticalTooBig;
        public bool foundGroundUnderPlayer;
        public Vector3 groundUnderPlayer;
        public bool shouldCamp;
        public float shootDistance;
    }

    private IEnumerator AttackSequence(bool byHit)
    {
        if (!CanContinueCombat())
            yield break;

        PrepareCombatAgent();

        yield return FaceTargetBeforeDraw();

        HolsterWeapon(false);

        if (weaponDrawTime > 0f)
            yield return new WaitForSeconds(weaponDrawTime);

        if (aimDelay > 0f)
            yield return AimBeforeCombat();

        float shootDistance = GetShootDistance();

        while (CanContinueCombat())
        {
            CombatFrame frame = BuildCombatFrame(shootDistance);

            if (TryHandleCombatMovement(frame))
            {
                yield return null;
                continue;
            }

            yield return SnapAimBeforeShooting();

            if (CanFireAtTarget(frame))
                yield return FireBurst(shootDistance);

            yield return CombatCooldown();
        }

        HolsterWeapon(true);
        attackCoroutine = null;
    }


    private bool CanContinueCombat()
    {
        return isProvoked &&
               !isDead &&
               player != null &&
               playerStats != null &&
               !playerStats.IsDead;
    }

    private void PrepareCombatAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.isStopped = false;

        if (agent.pathPending && agent.remainingDistance > 2f)
            FacePositionXZ(GetCurrentTargetPosition());

        float speedMul = GetWeaponSpeedMulForNPC();

        agent.speed = 6.5f * speedMul;
        agent.speed = Mathf.Max(agent.speed, maxApproachSpeedWhenCamping * speedMul);
        agent.acceleration = 14f;
    }

    private IEnumerator FaceTargetBeforeDraw()
    {
        float timer = 0f;
        float maxTime = 0.6f;

        while (!isDead && player != null)
        {
            Vector3 targetPos = GetCurrentTargetPosition();
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.001f)
                yield break;

            float angle = Vector3.Angle(transform.forward, toTarget.normalized);

            if (angle <= faceAngleThreshold)
                yield break;

            FacePositionXZ(targetPos);

            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;

            timer += Time.deltaTime;

            if (timer >= maxTime)
                yield break;

            yield return null;
        }
    }

    private IEnumerator AimBeforeCombat()
    {
        float timer = 0f;

        while (timer < aimDelay && !isDead && player != null)
        {
            FacePositionXZ(GetCurrentTargetPosition());

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private float GetShootDistance()
    {
        float wantedDistance = desiredShootingDistance > 0f
            ? desiredShootingDistance
            : engageDistance * 0.85f;

        return Mathf.Clamp(
            wantedDistance,
            minShootingDistance + 0.5f,
            Mathf.Max(minShootingDistance + 1f, engageDistance)
        );
    }

    private void PredictAim(out Vector3 aimPos, out Vector3 aimDir)
    {
        Vector3 currentTargetPos = GetCurrentTargetPosition();

        Vector3 playerVelocity = _playerPosInitialized
            ? (currentTargetPos - _lastPlayerPos) / Mathf.Max(Time.deltaTime, 0.0001f)
            : Vector3.zero;

        _lastPlayerPos = currentTargetPos;
        _playerPosInitialized = true;

        Vector3 muzzle = equippedGun != null && equippedGun.FirePoint != null
            ? equippedGun.FirePoint.position
            : transform.position + Vector3.up * 1.4f;

        float bulletSpeed = equippedGun != null
            ? Mathf.Max(1f, equippedGun.BulletSpeed)
            : 60f;

        Vector3 targetCenter = currentTargetPos + Vector3.up * 1.4f;
        Vector3 toTarget = targetCenter - muzzle;

        float timeToHit = Mathf.Max(0f, toTarget.magnitude / bulletSpeed);

        aimPos = currentTargetPos + playerVelocity * timeToHit + Vector3.up * 1.4f;
        aimDir = (aimPos - muzzle).normalized;
    }

    private CombatFrame BuildCombatFrame(float shootDistance)
    {
        Vector3 targetPos = GetCurrentTargetPosition();

        bool foundUnder;
        Vector3 under = GetGroundPointUnderPlayer(6f, out foundUnder);

        bool verticalTooBig = VerticalGapTooLarge();

        bool pathPartial =
            agent != null &&
            agent.enabled &&
            agent.isOnNavMesh &&
            agent.pathStatus == NavMeshPathStatus.PathPartial;

        return new CombatFrame
        {
            targetPos = targetPos,
            horizontalDistance = HorizontalDistance(transform.position, targetPos),
            verticalTooBig = verticalTooBig,
            foundGroundUnderPlayer = foundUnder,
            groundUnderPlayer = under,
            shouldCamp = foundUnder && (verticalTooBig || pathPartial),
            shootDistance = shootDistance
        };
    }

    private bool TryHandleCombatMovement(CombatFrame frame)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return false;

        agent.acceleration = 14f;

        if (frame.shouldCamp)
            return HandleCampUnderPlayer(frame);

        if (frame.horizontalDistance > frame.shootDistance)
            return HandleChaseTarget(frame);

        if (frame.horizontalDistance < minShootingDistance + retreatBuffer)
            return HandleRetreatFromTarget(frame);

        StopForShooting();
        return false;
    }

    private bool HandleCampUnderPlayer(CombatFrame frame)
    {
        agent.isStopped = false;

        float speedMul = GetWeaponSpeedMulForNPC();
        agent.speed = Mathf.Max(agent.speed, maxApproachSpeedWhenCamping * speedMul);

        bool onMesh;
        Vector3 ring = RingPointAroundXZ(
            frame.groundUnderPlayer,
            Mathf.Min(frame.shootDistance, campUnderPlayerRadius + frame.shootDistance * 0.2f),
            preferAwayFrom: transform.position,
            out onMesh
        );

        Vector3 destination = onMesh ? ring : frame.groundUnderPlayer;

        if (Time.time >= _nextRepath)
        {
            _nextRepath = Time.time + campRepathInterval;
            agent.SetDestination(destination);
        }

        PredictAim(out Vector3 aimPos, out _);
        FacePositionXZ(aimPos);

        return true;
    }

    private bool HandleChaseTarget(CombatFrame frame)
    {
        agent.isStopped = false;

        float speedMul = GetWeaponSpeedMulForNPC();
        agent.speed = 6.5f * speedMul;
        agent.speed = Mathf.Max(agent.speed, maxApproachSpeedWhenCamping * speedMul);

        PredictAim(out Vector3 aimPos, out _);
        FacePositionXZ(aimPos);

        TrySetCombatDestination(frame.targetPos, chaseRepathInterval);

        return true;
    }

    private bool HandleRetreatFromTarget(CombatFrame frame)
    {
        Vector3 back = transform.position - frame.targetPos;
        back.y = 0f;

        if (back.sqrMagnitude <= 0.0001f)
            return false;

        back.Normalize();

        agent.isStopped = false;

        float retreatDistance =
            minShootingDistance + retreatBuffer - frame.horizontalDistance + 0.5f;

        Vector3 retreatTarget = transform.position + back * retreatDistance;

        if (NavMesh.SamplePosition(retreatTarget, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            retreatTarget = hit.position;

        TrySetCombatDestination(retreatTarget, retreatRepathInterval);

        PredictAim(out Vector3 aimPos, out _);
        FacePositionXZ(aimPos);

        return true;
    }

    private void StopForShooting()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.isStopped = true;

        if (agent.hasPath)
            agent.ResetPath();

        ResetCombatDestinationCache();
    }

    private IEnumerator SnapAimBeforeShooting()
    {
        float timer = 0f;
        float maxTime = 0.25f;

        while (timer < maxTime && !isDead)
        {
            PredictAim(out Vector3 aimPos, out _);
            FacePositionXZ(aimPos);

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private bool CanFireAtTarget(CombatFrame frame)
    {
        if (equippedGun == null)
            return false;

        Vector3 muzzle = equippedGun.FirePoint != null
            ? equippedGun.FirePoint.position
            : transform.position + Vector3.up * 1.4f;

        Vector3 target = frame.targetPos + Vector3.up * 1.4f;
        Vector3 toTarget = target - muzzle;

        float distance = toTarget.magnitude;

        if (distance <= 0.001f)
            return false;

        if (HasSameTypeFriendlyInFireLine(muzzle, target))
            return false;

        if (!frame.verticalTooBig)
            return true;

        return !Physics.Raycast(
            muzzle,
            toTarget.normalized,
            distance,
            losObstaclesMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private bool IsSameCombatTypeNPC(NPCController other)
    {
        if (other == null || other == this || other.IsDead)
            return false;

        NPCReactionType otherType = other.GetReactionType();

        if (otherType != reactionType)
            return false;

        return reactionType == NPCReactionType.Aggressive ||
               reactionType == NPCReactionType.Fighter;
    }

    private bool HasSameTypeFriendlyInFireLine(Vector3 muzzle, Vector3 target)
    {
        if (!avoidSameTypeFriendlyFire)
            return false;

        Vector3 toTarget = target - muzzle;
        float distance = toTarget.magnitude;

        if (distance <= 0.001f)
            return false;

        Vector3 direction = toTarget / distance;

        int npcLayer = LayerMask.NameToLayer("NPC");

        LayerMask mask = losObstaclesMask;

        if (npcLayer >= 0)
            mask |= 1 << npcLayer;

        int hitCount = Physics.SphereCastNonAlloc(
            muzzle,
            friendlyFireProbeRadius,
            direction,
            FriendlyFireHits,
            distance,
            mask,
            QueryTriggerInteraction.Ignore
        );

        float closestFriendlyDistance = float.MaxValue;
        NPCController closestFriendly = null;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = FriendlyFireHits[i];
            FriendlyFireHits[i] = default;

            if (hit.collider == null)
                continue;

            if (hit.collider.transform.IsChildOf(transform))
                continue;

            NPCController otherNpc = hit.collider.GetComponentInParent<NPCController>();

            if (!IsSameCombatTypeNPC(otherNpc))
                continue;

            if (hit.distance < closestFriendlyDistance)
            {
                closestFriendlyDistance = hit.distance;
                closestFriendly = otherNpc;
            }
        }

        if (closestFriendly == null)
            return false;

        if (debugLogs)
            Debug.Log($"[NPC] Hold fire, same type friendly in line: {name} -> {closestFriendly.name}");

        return true;
    }
    private IEnumerator FireBurst(float shootDistance)
    {
        if (equippedGun == null)
            yield break;

        for (int i = 0; i < shotsPerBurst; i++)
        {
            if (!CanContinueCombat())
                yield break;

            float distanceNow = HorizontalDistance(transform.position, GetCurrentTargetPosition());

            if (distanceNow > shootDistance + 0.75f)
                yield break;

            if (distanceNow < minShootingDistance + retreatBuffer)
                yield break;

            PredictAim(out Vector3 aimPos, out Vector3 aimDir);
            FacePositionXZ(aimPos);

            equippedGun.TryFire(gameObject, aimDir);

            float wait = Mathf.Max(0.05f, equippedGun.FireRate);
            float timer = 0f;

            while (timer < wait)
            {
                if (!CanContinueCombat())
                    yield break;

                PredictAim(out Vector3 waitAimPos, out _);
                FacePositionXZ(waitAimPos);

                timer += Time.deltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator CombatCooldown()
    {
        float timer = 0f;

        while (timer < fireCooldown)
        {
            if (!CanContinueCombat())
                yield break;

            PredictAim(out Vector3 aimPos, out _);
            FacePositionXZ(aimPos);

            timer += Time.deltaTime;
            yield return null;
        }
    }

    // ===== PLAYER DEATH =====
    private void HandlePlayerDied(string killer)
    {
        if (isDead) return;

        isProvoked = false;
        HideAllIcons();

        if (attackCoroutine != null) { StopCoroutine(attackCoroutine); attackCoroutine = null; }
        StopAllCoroutines();

        Vector3 corpsePos = player ? GetCurrentTargetPosition() : transform.position;
        bool iAmKiller = !string.IsNullOrEmpty(killer) && killer == name;

        if (iAmKiller)
        {
            StartCoroutine(VictorySequence(corpsePos));
        }
        else
        {
            inVictoryState = false;

            if (UsesLocalColorFeedback())
                ApplyBodyColor(defaultColor);

            HolsterWeapon(true);

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
                CancelInvoke(nameof(PickNewDestination));
                InvokeRepeating(nameof(PickNewDestination), 2f, 8f);
                PickNewDestination();
            }
        }
    }

    private bool VerticalGapTooLarge()
    {
        if (player == null) return false;
        return Mathf.Abs(GetCurrentTargetPosition().y - transform.position.y) > verticalAimTolerance;
    }

    private Vector3 GetGroundPointUnderPlayer(float searchRadius, out bool found)
    {
        found = false;
        if (player == null) return transform.position;

        Vector3 xz = new Vector3(GetCurrentTargetPosition().x, GetCurrentTargetPosition().y, GetCurrentTargetPosition().z);

        if (NavMesh.SamplePosition(xz, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
        {
            found = true;
            return hit.position;
        }
        return transform.position;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private Vector3 RingPointAroundXZ(Vector3 targetXZ, float radius, Vector3 preferAwayFrom, out bool onMesh)
    {
        onMesh = false;
        Vector3 dir = (preferAwayFrom - targetXZ);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Random.insideUnitSphere; 
        dir.y = 0f;
        dir.Normalize();

        Vector3 candidate = targetXZ + (-dir) * Mathf.Max(0.1f, radius); 
        if (NavMesh.SamplePosition(candidate, out NavMeshHit nh, 2.5f, NavMesh.AllAreas))
        {
            onMesh = true;
            return nh.position;
        }

        const int tries = 8;
        for (int i = 0; i < tries; i++)
        {
            float ang = (360f / tries) * i;
            Vector3 rot = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
            Vector3 cand = targetXZ + rot * radius;
            if (NavMesh.SamplePosition(cand, out nh, 2.5f, NavMesh.AllAreas))
            {
                onMesh = true;
                return nh.position;
            }
        }

        return transform.position;
    }

    private IEnumerator VictorySequence(Vector3 corpsePos)
    {
        inVictoryState = true;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
            agent.SetDestination(corpsePos);
        }

        float timeout = 6f, t = 0f;
        while (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (!agent.pathPending && agent.remainingDistance <= 1.6f) break;
            t += Time.deltaTime;
            if (t >= timeout) break;
            yield return null;
        }

        FacePositionXZ(corpsePos);
        yield return new WaitForSeconds(2f);

        inVictoryState = false;

        if (UsesLocalColorFeedback())
            ApplyBodyColor(defaultColor);

        HolsterWeapon(true);

        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
            agent.ResetPath();
            CancelInvoke(nameof(PickNewDestination));
            InvokeRepeating(nameof(PickNewDestination), 2f, 8f);
            PickNewDestination();
        }
    }
    private float GetWeaponSpeedMulForNPC()
    {
        if (equippedGun == null || equippedGun.WeaponData == null)
            return 1f;

        var wid = equippedGun.WeaponData;

        float moveMul = Mathf.Approximately(wid.moveSpeedMultiplier, 1f)
            ? -1f
            : wid.moveSpeedMultiplier;

        if (moveMul < 0f)
            moveMul = wid.GetDefaultLoad().moveMul;

        return Mathf.Clamp(moveMul, 0.5f, 1.2f);
    }

    private void HolsterWeapon(bool holster)
    {
        if (!useWeaponSystem) return;
        if (equippedGun == null) return;

        GameObject weaponRoot = GetNpcWeaponRoot(equippedGun);

        if (weaponRoot != null)
            weaponRoot.SetActive(!holster);
    }

    // ===== DAMAGE / DEATH =====
    public void TakeDamage(int damage, string attacker)
    {
        if (isDead || deathSequenceStarted) return;

        lastAttacker = string.IsNullOrEmpty(attacker) ? "Unknown" : attacker;

        bool preventedDeath = false;
        bool shouldDie = false;

        // =========================
        // 1. HP / DAMAGE LOGIC
        // =========================
        if (core != null)
        {
            var result = core.TryTakeDamage(damage, lastAttacker);

            if (result.blocked)
            {
                return;
            }

            currentHP = result.currentHP;

            preventedDeath = result.preventedDeath;
            shouldDie = result.wouldDie;
        }
        else
        {
            currentHP -= damage;
            if (currentHP < 0f) currentHP = 0f;

            shouldDie = currentHP <= 0f;
        }

        // =========================
        // 2. HIT FEEDBACK
        // =========================

        RestartLocalHitFlash();

        HitFeedbackUtility.PlayHitFx(
            transform,
            bloodFxPrefab,
            hurtSfx,
            hitPointWorld: null,
            hitNormalWorld: null,
            bloodFxScale,
            bloodFxLifetime,
            audioSource
        );

        SpawnBloodOnGround(transform.position);

        // =========================
        // 3. PREVENT DEATH
        // =========================

        if (preventedDeath)
        {
            currentHP = Mathf.Max(1f, currentHP);

            if (reactionType == NPCReactionType.Coward)
            {
                if (propagatePanicToWitnesses) PropagateCowardPanic();
                propagatePanicToWitnesses = false;

                lastKnownAttackerPos = player ? GetCurrentTargetPosition() : transform.position + transform.forward;
                DisableInteractionAndCollisionForever();
                StartCowardFlee();

                ShowScared(new Color(1f, 0.85f, 0.2f));
            }
            else if (!isProvoked)
            {
                StartAggression(byHit: true);
            }

            return;
        }

        // =========================
        // 4. DEATH
        // =========================
        if (shouldDie || currentHP <= 0f)
        {
            currentHP = 0f;

            if (UsesLocalColorFeedback())
                ApplyBodyColor(Color.red);

            StartCoroutine(CoDieAfterHitFrame());
            return;
        }

        // =========================
        // 5. SURVIVED HIT REACTION
        // =========================
        if (reactionType == NPCReactionType.Coward)
        {
            if (propagatePanicToWitnesses) PropagateCowardPanic();
            propagatePanicToWitnesses = false;

            lastKnownAttackerPos = player ? GetCurrentTargetPosition() : transform.position + transform.forward;
            DisableInteractionAndCollisionForever();
            StartCowardFlee();

            ShowScared(new Color(1f, 0.85f, 0.2f));
        }
        else if (!isProvoked)
        {
            StartAggression(byHit: true);
        }
    }

    public bool CanBeRunOverByVehicle()
    {
        if (isDead)
            return false;

        if (core != null)
        {
            if (core.Importance != NPCCore.NPCImportance.Ambient)
                return false;

            if (core.IsInvulnerable || core.PreventDeath)
                return false;
        }

        return true;
    }

    public void ReceiveVehicleImpact(
        float damage,
        float speedKmh,
        Vector3 vehicleVelocity,
        Vector3 hitPoint,
        string attackerName = "PlayerVehicle")
    {
        if (!CanBeRunOverByVehicle())
        {
            return;
        }

        pendingVehicleFall = true;
        pendingVehicleVelocity = vehicleVelocity;
        pendingVehicleSpeedKmh = speedKmh;

        lastAttacker = attackerName;

        TakeDamage(Mathf.CeilToInt(damage), attackerName);
    }

    private IEnumerator CoDieAfterHitFrame()
    {
        yield return null;
        if (!isDead && !deathSequenceStarted)
            Die();
    }

    public void TakeDamage(float dmg) => TakeDamage(Mathf.RoundToInt(dmg), "Unknown");

    private void DisableInteractionAndCollisionForever()
    {
        if (interactionDisabledForever) return;
        interactionDisabledForever = true;

        if (interactionColliders != null)
            foreach (var c in interactionColliders) if (c) c.enabled = false;

        if (player != null)
        {
            var playerCol = player.GetComponent<Collider>();
            if (playerCol && physicalColliders != null)
                foreach (var c in physicalColliders) if (c) Physics.IgnoreCollision(c, playerCol, true);
        }
    }

    private void StartCowardFlee()
    {
        cowardFleeSafeUntil = Time.time + fleeDuration;

        if (isFleeing)
            return;

        isFleeing = true;
        isProvoked = true;

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        StartCoroutine(CowardFleeRoutine());
    }

    private IEnumerator CowardFleeRoutine()
    {
        float t0 = Time.time;

        Vector3 dir = (transform.position - (player ? GetCurrentTargetPosition() : transform.position)).normalized;
        if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;

        Vector3 target = transform.position + dir * Mathf.Max(fleeDistance, fleeFarDistance);
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            target = hit.position;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = Mathf.Max(agent.speed, 4.8f);
            agent.SetDestination(target);
        }

        while (Time.time < cowardFleeSafeUntil)
        {
            if (agent != null && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < 0.8f)
            {
                Vector3 away = (transform.position - (player ? GetCurrentTargetPosition() : transform.position)).normalized;
                Vector3 t2 = transform.position + away * (fleeFarDistance * 0.6f + Random.Range(3f, 6f));
                if (NavMesh.SamplePosition(t2, out NavMeshHit h2, 3f, NavMesh.AllAreas))
                    agent.SetDestination(h2.position);
            }
            yield return null;
        }

        OnCowardReportedLastKnownPos?.Invoke(lastKnownAttackerPos);

        isFleeing = false;

        if (!isDead && reactionType == NPCReactionType.Coward)
        {
            isProvoked = false;
            investigatingShot = false;

            HideAllIcons();

            if (UsesLocalColorFeedback())
                ApplyBodyColor(defaultColor);

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
                PickNewDestination();
            }
        }
    }

    public void ReceivePanicFromWitness(Vector3 attackerPos)
    {
        lastKnownAttackerPos = attackerPos;
        DisableInteractionAndCollisionForever();
        StartCowardFlee();
    }

    private void HandleCowardReported(Vector3 lastKnownPos)
    {
        if (reactionType != NPCReactionType.Fighter || isDead) return;
        _defenseMode = true;  
        StartCoroutine(InvestigateRoutine(lastKnownPos));
    }

    private IEnumerator InvestigateRoutine(Vector3 pos)
    {
        if (agent == null || !agent.isOnNavMesh) yield break;

        agent.isStopped = false;
        agent.SetDestination(pos);

        float t0 = Time.time;
        while (Time.time - t0 < patrolInvestigateTime)
        {
            if (!isProvoked && _defenseMode && PlayerInFrontAndVisible())
            {
                StartAggression(byHit: false);
                yield break;
            }

            if (!agent.pathPending && agent.remainingDistance <= investigateArriveTolerance)
                break;

            yield return null;
        }

        _defenseMode = false;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (debugLogs)
            Debug.Log($"DIE CALLED -> {name}");

        if (core != null)
        {
            core.ConfirmDeath(lastAttacker);
        }

        HideAllIcons();

        OnNPCDied?.Invoke(this, lastAttacker);

        InventoryItemInstance droppedInstance = null;
        GameObject pickupPrefab = null;

        if (ShouldDropWeapon() && equippedGun != null)
        {
            droppedInstance = equippedGun.GetInstance();
            pickupPrefab = GetPickupPrefabFromEquippedGun();
        }

        if (flashCoroutine != null) { StopCoroutine(flashCoroutine); flashCoroutine = null; }
        if (attackCoroutine != null) { StopCoroutine(attackCoroutine); attackCoroutine = null; }

        CancelInvoke();
        StopAllCoroutines();

        inVictoryState = false;
        isProvoked = false;
        isFleeing = false;
        investigatingShot = false;

        HideAllIcons();

        if (interactionColliders != null)
        {
            foreach (var c in interactionColliders)
                if (c) c.enabled = false;
        }

        StartCoroutine(CoDieSequence(pickupPrefab, droppedInstance));
    }

    private IEnumerator CoDieSequence(GameObject pickupPrefab, InventoryItemInstance droppedInstance)
    {

        if (debugLogs)
            Debug.Log($"CoDieSequence START -> {name}");

        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.enabled = false;
        }

        HolsterWeapon(true);

        if (cachedAnimator != null)
            cachedAnimator.enabled = false;

        // Prevent the ragdoll from pushing or blocking the player after death.
        IgnorePlayerCollisionsOnDeath();

        yield return new WaitForFixedUpdate();

        NPCDeathUtility.DieLikeNPCController(
            owner: this,
            agent: null,
            root: transform,
            rb: ref rootRb,
            col: ref rootCol,
            anim: null,
            gentleImpulse: true
        );

        if (pendingVehicleFall && rootRb != null)
        {
            Vector3 dir = pendingVehicleVelocity;

            if (dir.sqrMagnitude < 0.01f)
                dir = transform.forward;

            dir.y = 0f;

            if (dir.sqrMagnitude < 0.01f)
                dir = transform.forward;

            dir.Normalize();

            float speed01 = Mathf.InverseLerp(10f, 90f, pendingVehicleSpeedKmh);

            // At low speed the body falls more softly.
            // At high speed it receives a stronger forward and upward impulse.
            float forwardImpulse = Mathf.Lerp(1.5f, 9.0f, speed01);
            float upwardImpulse = Mathf.Lerp(0.15f, 4.2f, speed01);
            float torqueImpulse = Mathf.Lerp(0.4f, 2.0f, speed01);

            rootRb.linearDamping = 0.5f;
            rootRb.angularDamping = 8f;
            rootRb.maxAngularVelocity = 8f;

            rootRb.AddForce(
                dir * forwardImpulse + Vector3.up * upwardImpulse,
                ForceMode.Impulse
            );

            rootRb.AddTorque(
                Random.insideUnitSphere * torqueImpulse,
                ForceMode.Impulse
            );

            pendingVehicleFall = false;
        }

        if (pendingBackstabFall && rootRb != null)
        {
            Vector3 awayFromAttacker = transform.position - (player ? GetCurrentTargetPosition() : transform.position - transform.forward);
            awayFromAttacker.y = 0f;

            if (awayFromAttacker.sqrMagnitude < 0.001f)
                awayFromAttacker = -transform.forward;

            awayFromAttacker.Normalize();

            rootRb.AddForce(awayFromAttacker * 1.0f + Vector3.down * 0.15f, ForceMode.Impulse);
            rootRb.AddTorque(Vector3.Cross(Vector3.up, awayFromAttacker) * 1.4f, ForceMode.Impulse);

            pendingBackstabFall = false;
        }

        if (pendingMeleeFall && rootRb != null)
        {
            Vector3 awayFromAttacker = transform.position - pendingMeleeAttackerPos;
            awayFromAttacker.y = 0f;

            if (awayFromAttacker.sqrMagnitude < 0.001f)
                awayFromAttacker = -transform.forward;

            awayFromAttacker.Normalize();

            rootRb.AddForce(awayFromAttacker * 0.55f + Vector3.down * 0.08f, ForceMode.Impulse);
            rootRb.AddTorque(Vector3.Cross(Vector3.up, awayFromAttacker) * 0.85f, ForceMode.Impulse);

            pendingMeleeFall = false;
        }

        if (debugLogs)
            Debug.Log($"RAGDOLL ENABLED -> {name}");

        if (equippedGun != null)
        {
            var carriedRoot = equippedGun.transform.parent
                ? equippedGun.transform.parent.gameObject
                : equippedGun.gameObject;

            if (carriedRoot != null)
                Destroy(carriedRoot);

            equippedGun = null;
        }

        if (pickupPrefab != null && droppedInstance != null)
            StartCoroutine(DropWeaponAfterDeath(pickupPrefab, droppedInstance, 0.12f));

        StartCoroutine(DespawnAfterSeconds(15f));
    }

    private bool ShouldDropWeapon()
    {
        if (!useWeaponSystem) return false;
        if (!allowWeaponDrop) return false;
        if (equippedGun == null) return false;
        if (reactionType != NPCReactionType.Aggressive && reactionType != NPCReactionType.Fighter) return false;

        return Random.value < (weaponDropChance / 100f);
    }

    private void RefreshBodyRenderers()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        List<Renderer> filtered = new List<Renderer>(all.Length);

        int weaponLayer = LayerMask.NameToLayer("Weapon");

        foreach (Renderer r in all)
        {
            if (r == null)
                continue;

            if (alertIcon != null &&
                (r.transform == alertIcon.transform || r.transform.IsChildOf(alertIcon.transform)))
            {
                continue;
            }

            // Do not color the scared icon or its children.
            if (scaredIcon != null &&
                (r.transform == scaredIcon.transform || r.transform.IsChildOf(scaredIcon.transform)))
            {
                continue;
            }

            // Do not color any weapons under WeaponsList.
            if (weaponsListRoot != null && r.transform.IsChildOf(weaponsListRoot))
            {
                continue;
            }

            // Do not color objects that belong to NPCGun.
            if (r.GetComponentInParent<NPCGun>(true) != null)
            {
                continue;
            }

            // Extra safety if weapons use the Weapon layer.
            if (weaponLayer >= 0 && r.gameObject.layer == weaponLayer)
            {
                continue;
            }

            filtered.Add(r);
        }

        bodyRenderers = filtered.ToArray();
    }


    private IEnumerator DespawnAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Destroy(gameObject);
    }

    // ===== WEAPON =====
    private void AssignRandomWeapon()
    {
        if (!useWeaponSystem)
            return;

        if (availableWeapons == null || availableWeapons.Length == 0)
        {
            if (debugLogs)
                Debug.LogWarning($"[NPC] {name}: No available NPC weapons.");
            return;
        }

        HideAllNpcWeapons();

        NPCGun gunComponent = availableWeapons[Random.Range(0, availableWeapons.Length)];

        if (gunComponent == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[NPC] {name}: Rolled NPC weapon is null.");
            return;
        }

        equippedGun = gunComponent;

        GameObject weaponRoot = GetNpcWeaponRoot(equippedGun);

        if (weaponRoot != null)
            weaponRoot.SetActive(false);

        assignedWeaponName = equippedGun.name;

        if (debugLogs)
            Debug.Log($"[NPC] {name}: Assigned NPC weapon = {assignedWeaponName}, root = {weaponRoot?.name}");

        RefreshBodyRenderers();
    }

    private GameObject GetPickupPrefabFromEquippedGun()
    {
        if (equippedGun == null) return null;

        string gunName = equippedGun.name ?? "";
        gunName = gunName.Replace("(Clone)", "");

        if (gunName.Contains("Glock")) return GlockPickup;
        if (gunName.Contains("M4A1")) return M4A1Pickup;
        if (gunName.Contains("AK97")) return AK97Pickup;
        if (gunName.Contains("SPAS12")) return SPAS12Pickup;

        if (debugLogs)
            Debug.Log($"[NPC] {name}: pickupPrefab=null, gun='{gunName}'");
        return null;
    }

    // ===== PANIC =====

    private void PropagateCowardPanic()
    {
        int count = npcMask.value != 0
            ? Physics.OverlapSphereNonAlloc(
                transform.position,
                panicPropagationRadius,
                PanicOverlapBuffer,
                npcMask,
                QueryTriggerInteraction.Ignore
            )
            : Physics.OverlapSphereNonAlloc(
                transform.position,
                panicPropagationRadius,
                PanicOverlapBuffer,
                ~0,
                QueryTriggerInteraction.Ignore
            );

        Vector3 panicSource = player != null
            ? GetCurrentTargetPosition()
            : transform.position;

        for (int i = 0; i < count; i++)
        {
            Collider col = PanicOverlapBuffer[i];
            PanicOverlapBuffer[i] = null;

            if (col == null)
                continue;

            NPCController npc = col.GetComponentInParent<NPCController>();

            if (npc == null || npc == this || npc.isDead)
                continue;

            if (npc.reactionType != NPCReactionType.Coward)
                continue;

            npc.ReceivePanicFromWitness(panicSource);
        }
    }

    public void ReactToPanic()
    {
        if (isDead) return;

        DisableInteractionAndCollisionForever();
        StartCowardFlee();
        reactionEndTime = Time.time + reactionDuration;

    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead) return;
        if (reactionType == NPCReactionType.Fighter) return;
        if (!other.CompareTag("Player")) return;

        float d = Vector3.Distance(transform.position, other.transform.position);
        if (d > interactRange) return;

        FacePositionXZ(other.transform.position);
    }

    // ========== PUBLIC DIE ENTRY FOR EXTERNAL CALLS (NPCMelee etc.) ==========
    public void DieFromExternal(string attackerName = "Unknown")
    {
        if (isDead) return;
        lastAttacker = attackerName;
        Die();
    }

    private IEnumerator DropWeaponAfterDeath(GameObject pickupPrefab, InventoryItemInstance inst, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (pickupPrefab == null || inst == null) yield break;

        Vector3 dropPos = weaponDropPoint
            ? weaponDropPoint.position
            : transform.position + transform.right * 0.2f + Vector3.up * 0.20f;

        Quaternion dropRot = Quaternion.identity;
        GameObject droppedPickup = Instantiate(pickupPrefab, dropPos, dropRot);

        if (!droppedPickup.activeSelf)
            droppedPickup.SetActive(true);

        var pickup = droppedPickup.GetComponentInChildren<WeaponPickup>(true);
        if (pickup != null)
        {
            pickup.Initialize(inst, null);
            pickup.currentAmmo = Mathf.Max(0, inst.currentAmmo);
            pickup.totalAmmo = Mathf.Max(0, inst.totalAmmo);
            pickup.SetupPhysics(true);
        }

        var rb = droppedPickup.GetComponent<Rigidbody>();
        if (rb == null)
            rb = droppedPickup.AddComponent<Rigidbody>();

        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.mass = 2.0f;
        rb.linearDamping = 1.2f;
        rb.angularDamping = 2.0f;
        rb.useGravity = true;
        rb.isKinematic = false;

        var col = droppedPickup.GetComponent<Collider>();
        if (col == null)
        {
            col = droppedPickup.AddComponent<BoxCollider>();
            col.isTrigger = false;
        }

        droppedPickup.layer = LayerMask.NameToLayer("Weapon");

        IgnoreDroppedPickupCollision(droppedPickup);

        // Drop impulse.
        Vector3 lateral = transform.forward * 0.35f + transform.right * Random.Range(-0.10f, 0.10f);
        Vector3 impulse = lateral + Vector3.up * 0.04f;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(impulse, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 0.35f, ForceMode.Impulse);
    }
    private void IgnoreDroppedPickupCollision(GameObject droppedPickup)
    {
        if (droppedPickup == null) return;

        var pickupCols = droppedPickup.GetComponentsInChildren<Collider>(true);
        if (pickupCols == null || pickupCols.Length == 0) return;

        var npcCols = GetComponentsInChildren<Collider>(true);

        foreach (var npcCol in npcCols)
        {
            if (npcCol == null) continue;

            foreach (var pickupCol in pickupCols)
            {
                if (pickupCol == null) continue;
                Physics.IgnoreCollision(pickupCol, npcCol, true);
            }
        }
    }

    private void SpawnBloodOnGround(Vector3 aroundPoint)
    {
        if (bloodFxPrefab == null) return;

        Vector3 start = aroundPoint + Vector3.up * 0.5f;

        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, 3f, LayerMask.GetMask("Floor", "Default")))
        {
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, hit.normal);
            GameObject fx = Instantiate(bloodFxPrefab, hit.point + hit.normal * 0.01f, rot);
            fx.transform.localScale = Vector3.one * bloodFxScale;
            Destroy(fx, bloodFxLifetime);
        }
    }

    public void TakeBackstabKill(string attackerName = "Player (Melee Backstab)")
    {
        if (isDead || deathSequenceStarted) return;

        lastAttacker = string.IsNullOrEmpty(attackerName) ? "Player (Melee Backstab)" : attackerName;

        if (core != null)
        {
            var result = core.TryTakeDamage(99999f, lastAttacker);

            if (result.blocked)
                return;

            currentHP = result.currentHP;

            if (result.preventedDeath)
            {
                currentHP = Mathf.Max(1f, currentHP);

                RestartLocalHitFlash();

                HitFeedbackUtility.PlayHitFx(
                    transform,
                    bloodFxPrefab,
                    hurtSfx,
                    hitPointWorld: null,
                    hitNormalWorld: null,
                    bloodFxScale,
                    bloodFxLifetime,
                    audioSource
                );

                SpawnBloodOnGround(transform.position);

                if (!isProvoked)
                    StartAggression(byHit: true);

                return;
            }

            if (!result.wouldDie)
                return;
        }
        else
        {
            currentHP = 0f;
        }

        deathSequenceStarted = true;

        if (debugLogs)
            Debug.Log($"[NPC] Backstab kill executed -> {name}");

        currentHP = 0f;

        pendingBackstabFall = true;

        RestartLocalHitFlash();

        HitFeedbackUtility.PlayHitFx(
            transform,
            bloodFxPrefab,
            hurtSfx,
            hitPointWorld: null,
            hitNormalWorld: null,
            bloodFxScale,
            bloodFxLifetime,
            audioSource
        );

        SpawnBloodOnGround(transform.position);

        StartCoroutine(CoBackstabDeath());
    }

    public void TakeMeleeDamage(int damage, string attackerName, Vector3 attackerPos)
    {
        if (isDead || deathSequenceStarted) return;

        lastAttacker = string.IsNullOrEmpty(attackerName) ? "Unknown" : attackerName;

        bool preventedDeath = false;
        bool shouldDie = false;

        // =========================
        // 1. HP / DAMAGE LOGIC 
        // =========================
        if (core != null)
        {
            var result = core.TryTakeDamage(damage, lastAttacker);

            if (result.blocked)
            {
                return;
            }

            currentHP = result.currentHP;
            preventedDeath = result.preventedDeath;
            shouldDie = result.wouldDie;
        }
        else
        {
            // Fallback for old NPC prefabs without NPCCore.
            currentHP -= Mathf.Max(0, damage);
            if (currentHP < 0f) currentHP = 0f;

            shouldDie = currentHP <= 0f;
        }

        // =========================
        // 2. HIT FEEDBACK
        // =========================

        RestartLocalHitFlash();

        HitFeedbackUtility.PlayHitFx(
            transform,
            bloodFxPrefab,
            hurtSfx,
            hitPointWorld: null,
            hitNormalWorld: null,
            bloodFxScale,
            bloodFxLifetime,
            audioSource
        );

        SpawnBloodOnGround(transform.position);

        // =========================
        // 3. PREVENT DEATH
        // =========================
        if (preventedDeath)
        {
            currentHP = Mathf.Max(1f, currentHP);

            if (reactionType == NPCReactionType.Coward)
            {
                if (propagatePanicToWitnesses) PropagateCowardPanic();
                propagatePanicToWitnesses = false;

                lastKnownAttackerPos = player ? GetCurrentTargetPosition() : transform.position + transform.forward;
                DisableInteractionAndCollisionForever();
                StartCowardFlee();
                ShowScared(new Color(1f, 0.85f, 0.2f));
            }
            else if (!isProvoked)
            {
                StartAggression(byHit: true);
            }

            return;
        }

        // =========================
        // 4. DEATH
        // =========================
        if (shouldDie || currentHP <= 0f)
        {
            currentHP = 0f;

            pendingMeleeFall = true;
            pendingMeleeAttackerPos = attackerPos;

            if (UsesLocalColorFeedback())
                ApplyBodyColor(Color.red);

            StartCoroutine(CoDieAfterHitFrame());
            return;
        }

        // =========================
        // 5. SURVIVED HIT REACTION
        // =========================
        if (reactionType == NPCReactionType.Coward)
        {
            if (propagatePanicToWitnesses) PropagateCowardPanic();
            propagatePanicToWitnesses = false;

            lastKnownAttackerPos = player ? GetCurrentTargetPosition() : transform.position + transform.forward;
            DisableInteractionAndCollisionForever();
            StartCowardFlee();
            ShowScared(new Color(1f, 0.85f, 0.2f));
        }
        else if (!isProvoked)
        {
            StartAggression(byHit: true);
        }
    }

    private IEnumerator CoBackstabDeath()
    {
        yield return null;
        if (!isDead)
            Die();
    }

    private void IgnorePlayerCollisionsOnDeath()
    {
        if (player == null) return;

        var playerCols = player.GetComponentsInChildren<Collider>(true);
        var npcCols = GetComponentsInChildren<Collider>(true);

        foreach (var npcCol in npcCols)
        {
            if (npcCol == null) continue;

            foreach (var playerCol in playerCols)
            {
                if (playerCol == null) continue;
                Physics.IgnoreCollision(npcCol, playerCol, true);
            }
        }
    }
    private Vector3 GetCurrentTargetPosition()
    {
        return NPCPlayerTargetUtility.GetTargetPosition(player);
    }

    private GameObject GetNpcWeaponRoot(NPCGun gun)
    {
        if (gun == null)
            return null;

        if (gun.transform.parent != null)
            return gun.transform.parent.gameObject;

        return gun.gameObject;
    }

    private void HideAllNpcWeapons()
    {
        if (availableWeapons == null)
            return;

        foreach (NPCGun gun in availableWeapons)
        {
            if (gun == null)
                continue;

            GameObject root = GetNpcWeaponRoot(gun);

            if (root != null)
                root.SetActive(false);
        }
    }

    private bool CanSeeCurrentTarget()
    {
        return PlayerInFrontAndVisible();
    }

    private void HandleAggressiveMemory()
    {
        Vector3 targetPos = GetCurrentTargetPosition();
        float distToTarget = Vector3.Distance(transform.position, targetPos);

        bool seesTarget = CanSeeCurrentTarget();

        if (seesTarget && distToTarget <= losePlayerDistance)
        {
            lastSeenPlayerPosition = targetPos;
            lastSeenPlayerTime = Time.time;
            searchingLastSeenPosition = false;

            ShowAlert(Color.red);
            return;
        }

        if (distToTarget <= losePlayerDistance)
            return;

        if (!searchingLastSeenPosition)
        {
            searchingLastSeenPosition = true;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
                agent.SetDestination(lastSeenPlayerPosition);
            }

            return;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            bool arrived =
                !agent.pathPending &&
                agent.remainingDistance <= investigateArriveTolerance;

            bool searchedLongEnough =
                Time.time - lastSeenPlayerTime >= searchLastSeenTime;

            if (arrived || searchedLongEnough)
            {
                ReturnAggressiveToIdle();
            }
        }
        else
        {
            ReturnAggressiveToIdle();
        }
    }
    private void ReturnAggressiveToIdle()
    {
        isProvoked = false;
        searchingLastSeenPosition = false;
        investigatingShot = false;
        _defenseMode = false;

        ResetCombatDestinationCache();

        HideAllIcons();

        if (UsesLocalColorFeedback())
            ApplyBodyColor(defaultColor);

        HolsterWeapon(true);

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
            PickNewDestination();
        }
    }

    private bool TryResolvePlayerRefs()
    {
        if (player != null && playerStats != null)
            return true;

        NPCSceneRefs refs = NPCSceneRefs.Instance;

        if (refs != null && refs.HasPlayer())
        {
            player = refs.Player;
            playerStats = refs.PlayerStats;
            CachePlayerMotionRefs();
            return player != null;
        }

        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");

        if (playerGo == null)
            return false;

        player = playerGo.transform;
        playerStats = playerGo.GetComponent<PlayerStats>();
        CachePlayerMotionRefs();

        return player != null;
    }

    private void CachePlayerMotionRefs()
    {
        if (player == null)
            return;

        playerCharacterController = player.GetComponent<CharacterController>();
        playerRigidbody = player.GetComponent<Rigidbody>();
        playerMovement = player.GetComponent<PlayerMovement>();

        if (playerMovement == null)
            playerMovement = player.GetComponentInParent<PlayerMovement>();

        if (playerMovement == null)
            playerMovement = player.GetComponentInChildren<PlayerMovement>(true);

        NPCSceneRefs refs = NPCSceneRefs.Instance;

        if (refs != null && refs.WeaponManager != null)
            playerWeaponManager = refs.WeaponManager;
        else
            playerWeaponManager = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);

        rearAwarenessPlayerPosInitialized = false;
    }

    private bool PlayerHasWeaponInHands()
    {
        if (playerWeaponManager == null)
        {
            NPCSceneRefs refs = NPCSceneRefs.Instance;

            if (refs != null && refs.WeaponManager != null)
                playerWeaponManager = refs.WeaponManager;
            else
                playerWeaponManager = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);
        }

        if (playerWeaponManager == null)
            return true;

        return !playerWeaponManager.IsUsingHandsOnly();
    }

    private void ResolvePlayerRefs()
    {
        TryResolvePlayerRefs();
    }

    private bool TrySetCombatDestination(Vector3 destination, float interval)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return false;

        interval = Mathf.Max(0.02f, interval);

        if (hasLastCombatDestination)
        {
            float sqrDelta = (destination - lastCombatDestination).sqrMagnitude;
            float minDelta = Mathf.Max(0.05f, minCombatDestinationMoveDelta);

            if (Time.time < nextCombatDestinationUpdateTime &&
                sqrDelta < minDelta * minDelta)
            {
                return false;
            }
        }

        nextCombatDestinationUpdateTime = Time.time + interval;
        lastCombatDestination = destination;
        hasLastCombatDestination = true;

        agent.SetDestination(destination);
        return true;
    }

    private void ResetCombatDestinationCache()
    {
        hasLastCombatDestination = false;
        nextCombatDestinationUpdateTime = 0f;
    }

    private void BeginRearAwarenessLook(Vector3 targetPos)
    {
        rearAwarenessLookTarget = targetPos;
        rearAwarenessLookUntil = Time.time + rearAwarenessLookHoldTime;
        rearAwarenessSuppressAutoAggroUntil = Time.time + rearAwarenessAutoAggroSuppressTime;
        rearAwarenessStopUntil = Time.time + rearAwarenessStopDuration;

        if (agent != null && agent.enabled && agent.isOnNavMesh && !isProvoked && !isFleeing)
        {
            agent.isStopped = true;
            rearAwarenessPausedAgent = true;
        }

        FacePositionXZ(targetPos);

        cachedPlayerInFrontAndVisible = false;
        nextVisionCheckTime = Time.time + rearAwarenessAutoAggroSuppressTime;
    }

    private void ReleaseRearAwarenessStopIfNeeded()
    {
        if (!rearAwarenessPausedAgent)
            return;

        if (Time.time < rearAwarenessStopUntil)
            return;

        rearAwarenessPausedAgent = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh && !isDead && !isProvoked && !isFleeing)
            agent.isStopped = false;
    }

    private void ClearRearAwarenessHold()
    {
        rearAwarenessLookUntil = 0f;
        rearAwarenessSuppressAutoAggroUntil = 0f;
        rearAwarenessStopUntil = 0f;
        rearAwarenessPausedAgent = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh && !isDead)
            agent.isStopped = false;
    }
}
