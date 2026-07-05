using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCMelee : MonoBehaviour, IDamageable
{
    public enum MeleeType
    {
        MeleeOne = 1,
        MeleeTwo = 2,
        MeleeThree = 3
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public bool IsDead => _isDead;
    public bool IsAggro => _aggro;

    public static event System.Action<NPCMelee, string> OnMeleeNPCDied;

    // =========================================================
    // IDENTITY / COMBAT TYPE
    // =========================================================

    [Header("Melee Combo Type")]
    public MeleeType meleeType = MeleeType.MeleeOne;

    // =========================================================
    // VISUAL ROOT
    // =========================================================

    [Header("Visual Root")]
    [Tooltip("Assign the mesh/body root here. The NavMeshAgent root stays separate from visual rotation.")]
    public Transform visualRoot;

    [Tooltip("Use 180 if the mesh is facing backwards.")]
    public float visualYawOffset = 0f;

    // =========================================================
    // DETECTION
    // =========================================================

    [Header("Detection")]
    public float viewDistance = 12f;

    [Range(10f, 180f)]
    public float viewAngle = 90f;

    public LayerMask obstaclesMask = ~0;
    public LayerMask playerMask;

    [Header("Rear Awareness / Sprint Noise")]
    [SerializeField] private bool useRearSprintAwareness = true;
    [SerializeField] private bool rearAwarenessRequiresHeldWeapon = true;
    [SerializeField] private float rearAwarenessRadius = 5.0f;
    [SerializeField] private float rearAwarenessMinSpeed = 3.2f;

    [SerializeField, Range(60f, 179f)]
    private float rearAwarenessMinBackAngle = 90f;

    [SerializeField] private float rearAwarenessCooldown = 0.85f;
    [SerializeField] private float rearAwarenessLookHoldTime = 1.0f;
    [SerializeField] private float rearAwarenessStopDuration = 1.0f;
    [SerializeField] private bool rearAwarenessRequiresLineOfSight = true;
    [SerializeField] private LayerMask rearAwarenessObstacleMask = ~0;

    [SerializeField] private bool debugRearAwareness = false;

    private float _nextRearAwarenessTime;
    private float _rearAwarenessLookUntil;
    private float _rearAwarenessStopUntil;
    private Vector3 _rearAwarenessLookTarget;
    private bool _rearAwarenessPausedAgent;

    private CharacterController _playerCharacterController;
    private Rigidbody _playerRigidbody;
    private PlayerMovement _playerMovement;
    private WeaponManager _playerWeaponManager;

    private Vector3 _lastRearAwarenessPlayerPos;
    private bool _rearAwarenessPlayerPosInitialized;

    // =========================================================
    // MELEE COMBAT
    // =========================================================

    [Header("Melee Combat")]
    public float attackRange = 1.7f;
    public int damagePerHit = 12;
    public float comboStepInterval = 0.35f;
    public float comboCooldown = 0.9f;

    // =========================================================
    // MOVEMENT
    // =========================================================

    [Header("Movement")]
    public float chaseSpeed = 4.2f;
    public float patrolSpeed = 2.2f;
    public float repathRate = 0.25f;
    public float stoppingDistance = 1.0f;

    [Header("Patrol")]
    public float patrolRadius = 8f;
    public float patrolIntervalMin = 4f;
    public float patrolIntervalMax = 8f;

    [Header("Combat Spacing")]
    public float holdDistance = 2.3f;
    public float strafeSpeed = 1.2f;
    public float backoffSpeed = 3.5f;
    public float holdJitter = 0.2f;

    [Header("Enrage After Hit")]
    public float enragedChaseSpeed = 6.0f;
    public float enragedStrafeSpeed = 1.8f;
    public float enragedBackoffSpeed = 4.5f;

    // =========================================================
    // HEALTH / DAMAGE
    // =========================================================

    [Header("Health")]
    public int maxHP = 80;

    [Header("Hit Flash")]
    public Renderer[] bodyRenderers;
    public float hitFlashDuration = 0.25f;

    [Header("Hit FX")]
    public GameObject bloodFxPrefab;
    public float bloodFxScale = 1f;
    public float bloodFxLifetime = 2f;
    public AudioClip hurtSfx;
    public AudioSource audioSource;

    // =========================================================
    // ALERT
    // =========================================================

    [Header("Alert Visibility")]
    [SerializeField] private GameObject alertIcon;
    [SerializeField] private float alertForgetDistance = 28f;
    [SerializeField] private float alertLoseSightDelay = 3f;
    [SerializeField] private float alertRefreshRate = 0.2f;

    // =========================================================
    // SHOT HEARING
    // =========================================================

    [Header("Shot Hearing")]
    public float shotHearRadius = 10f;
    public float shotLOSProbeHeight = 1.4f;

    [Header("Shot Reaction")]
    [SerializeField] private float reactShotMaxDistance = 25f;
    [SerializeField] private float nearMissThreshold = 2.2f;
    [SerializeField] private LayerMask losMask = ~0;

    // =========================================================
    // VEHICLE IMPACT
    // =========================================================

    [Header("Vehicle Impact")]
    [SerializeField] private float vehicleForwardImpulseMin = 2.0f;
    [SerializeField] private float vehicleForwardImpulseMax = 7.0f;
    [SerializeField] private float vehicleUpImpulseMin = 0.4f;
    [SerializeField] private float vehicleUpImpulseMax = 3.0f;
    [SerializeField] private float vehicleTorqueImpulseMin = 0.5f;
    [SerializeField] private float vehicleTorqueImpulseMax = 2.5f;
    [SerializeField] private float ragdollAngularDragAfterVehicleHit = 8f;
    [SerializeField] private float ragdollDragAfterVehicleHit = 0.5f;

    // =========================================================
    // COMPONENT CACHE
    // =========================================================

    private NPCCore core;
    private NavMeshAgent _agent;
    private Rigidbody _rootRb;
    private Collider _rootCol;
    private NPCReactive _reactive;
    private Billboard _billboard;
    private Animator _animator;

    private Transform _player;
    private PlayerStats _playerStats;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private int _hp;
    private bool _aggro;
    private bool _isDead;
    private bool _inCombo;
    private bool _enraged;

    private float _nextRepath;
    private float _lastAlertSeenTime = -999f;
    private float _nextAlertCheckTime;

    private bool _investigatingShot;
    private Vector3 _shotInvestigatePoint;
    private float _investigateUntil;

    private bool _pendingVehicleImpact;
    private Vector3 _pendingVehicleVelocity;
    private float _pendingVehicleSpeedKmh;

    private Color _baseColor = Color.white;
    private MaterialPropertyBlock _mpb;
    private Coroutine _flashCo;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    private Transform _combatTarget;
    private PlayerStats _combatTargetPlayerStats;
    private NPCCore _combatTargetCore;
    private NPCController _combatTargetController;
    private NPCMelee _combatTargetMelee;

    private Transform _queuedTarget;
    private PlayerStats _queuedTargetPlayerStats;
    private NPCCore _queuedTargetCore;
    private NPCController _queuedTargetController;
    private NPCMelee _queuedTargetMelee;

    private bool _combatTargetIsPlayer;

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    private void Awake()
    {
        CacheComponents();
        RefreshBodyRenderersIfNeeded();
        SetupAgent();
        ClampCombatSpacing();

        ResolvePlayerRefs();

        _hp = maxHP;
        _mpb = new MaterialPropertyBlock();

        ResolveVisualRoot();
        ApplyBodyColor(_baseColor);
    }

    private void Start()
    {
        HideAlert();
        Invoke(nameof(PatrolPickNewPoint), Random.Range(0.5f, 1.5f));
    }

    private void OnEnable()
    {
        Gun.OnPlayerShot += OnPlayerShotHeard;
    }

    private void OnDisable()
    {
        Gun.OnPlayerShot -= OnPlayerShotHeard;
    }

    private void OnValidate()
    {
        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();

        if (_agent != null)
            _agent.stoppingDistance = stoppingDistance;

        ClampCombatSpacing();

        float maxStop = Mathf.Max(0f, attackRange - 0.2f);

        if (stoppingDistance > maxStop)
            stoppingDistance = maxStop;
    }

    private void Update()
    {
        if (!CanTick())
            return;

        UpdateAlertVisibility();

        if (_aggro)
        {
            TickCombat();
        }
        else
        {
            TickRearSprintAwareness();
            TickRearAwarenessLookAtPlayer();
            TickIdle();
        }

        TickShotInvestigation();
    }

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void CacheComponents()
    {
        core = GetComponent<NPCCore>();

        _agent = GetComponent<NavMeshAgent>();
        _rootRb = GetComponent<Rigidbody>();
        _rootCol = GetComponent<Collider>();
        _reactive = GetComponent<NPCReactive>();
        _billboard = GetComponent<Billboard>();
        _animator = GetComponentInChildren<Animator>(true);
    }

    private void SetupAgent()
    {
        if (_agent == null)
            return;

        _agent.updateRotation = false;
        _agent.stoppingDistance = stoppingDistance;

        if (!_agent.isOnNavMesh &&
            NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
        {
            _agent.Warp(hit.position);
        }
    }

    private void ResolveVisualRoot()
    {
        if (visualRoot != null)
            return;

        Transform body = transform.Find("Body");
        visualRoot = body != null ? body : transform;
    }

    private void ClampCombatSpacing()
    {
        float margin = 0.25f;

        if (holdDistance >= attackRange - margin)
            holdDistance = Mathf.Max(0.1f, attackRange - margin);
    }

    // =========================================================
    // MAIN TICK
    // =========================================================

    private bool CanTick()
    {
        if (_isDead)
            return false;

        if (_player == null)
            ResolvePlayerRefs();

        return _player != null;
    }

    private bool AgentReady()
    {
        return _agent != null &&
               _agent.enabled &&
               _agent.isOnNavMesh;
    }

    private void TickRearSprintAwareness()
    {
        if (!CanUseRearSprintAwareness())
            return;

        if (Time.time < _nextRearAwarenessTime)
            return;

        Vector3 targetPos = NPCPlayerTargetUtility.GetTargetPosition(_player);
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

        Vector3 forward = visualRoot != null ? visualRoot.forward : transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        float angle = Vector3.Angle(forward.normalized, toPlayer.normalized);

        if (angle < rearAwarenessMinBackAngle)
            return;

        if (rearAwarenessRequiresLineOfSight && IsRearAwarenessBlocked(targetPos))
            return;

        _nextRearAwarenessTime = Time.time + rearAwarenessCooldown;

        BeginRearAwarenessLook(targetPos, playerSpeed, angle, Mathf.Sqrt(toPlayer.sqrMagnitude));
    }

    private bool CanUseRearSprintAwareness()
    {
        if (!useRearSprintAwareness)
            return false;

        if (_isDead || _aggro)
            return false;

        if (_player == null)
            return false;

        if (core != null)
        {
            if (core.Importance != NPCCore.NPCImportance.Ambient)
                return false;

            if (core.IsInvulnerable || core.PreventDeath)
                return false;
        }

        if (rearAwarenessRequiresHeldWeapon && !PlayerHasWeaponInHands())
            return false;

        return true;
    }

    private void BeginRearAwarenessLook(Vector3 targetPos, float speed, float angle, float distance)
    {
        _rearAwarenessLookTarget = targetPos;
        _rearAwarenessLookUntil = Time.time + rearAwarenessLookHoldTime;
        _rearAwarenessStopUntil = Time.time + rearAwarenessStopDuration;

        if (AgentReady())
        {
            _agent.isStopped = true;
            _rearAwarenessPausedAgent = true;
        }

        FaceVisualToDirection(targetPos - transform.position);

        if (debugRearAwareness)
        {
            Debug.Log(
                $"[NPCMelee] Rear awareness look -> {name}, " +
                $"speed={speed:0.00}, angle={angle:0.0}, dist={distance:0.00}"
            );
        }
    }

    private void TickRearAwarenessLookAtPlayer()
    {
        if (Time.time >= _rearAwarenessLookUntil)
        {
            ReleaseRearAwarenessStopIfNeeded();
            return;
        }

        if (_isDead || _aggro)
        {
            ReleaseRearAwarenessStopIfNeeded();
            return;
        }

        if (AgentReady())
            _agent.isStopped = true;

        Vector3 targetPos = _player != null
            ? NPCPlayerTargetUtility.GetTargetPosition(_player)
            : _rearAwarenessLookTarget;

        _rearAwarenessLookTarget = targetPos;
        FaceVisualToDirection(targetPos - transform.position);
    }

    private void ReleaseRearAwarenessStopIfNeeded()
    {
        if (!_rearAwarenessPausedAgent)
            return;

        if (Time.time < _rearAwarenessStopUntil)
            return;

        _rearAwarenessPausedAgent = false;

        if (AgentReady() && !_isDead && !_aggro)
            _agent.isStopped = false;
    }

    private void ClearRearAwarenessHold()
    {
        _rearAwarenessLookUntil = 0f;
        _rearAwarenessStopUntil = 0f;
        _rearAwarenessPausedAgent = false;

        if (AgentReady() && !_isDead)
            _agent.isStopped = false;
    }

    private float GetPlayerHorizontalSpeed()
    {
        if (_player == null)
            return 0f;

        if (_playerMovement == null ||
            _playerCharacterController == null ||
            _playerRigidbody == null)
        {
            CachePlayerMotionRefs();
        }

        if (_playerMovement != null && _playerMovement.IsTryingToSprint)
            return Mathf.Max(rearAwarenessMinSpeed + 0.5f, 5f);

        Vector3 velocity = Vector3.zero;

        if (_playerCharacterController != null)
        {
            velocity = _playerCharacterController.velocity;
        }
        else if (_playerRigidbody != null)
        {
            velocity = _playerRigidbody.linearVelocity;
        }
        else
        {
            Vector3 currentPos = _player.position;

            if (!_rearAwarenessPlayerPosInitialized)
            {
                _rearAwarenessPlayerPosInitialized = true;
                _lastRearAwarenessPlayerPos = currentPos;
                return 0f;
            }

            velocity = (currentPos - _lastRearAwarenessPlayerPos) / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastRearAwarenessPlayerPos = currentPos;
        }

        velocity.y = 0f;
        return velocity.magnitude;
    }

    private bool PlayerHasWeaponInHands()
    {
        if (_playerWeaponManager == null)
        {
            NPCSceneRefs refs = NPCSceneRefs.Instance;

            if (refs != null && refs.WeaponManager != null)
                _playerWeaponManager = refs.WeaponManager;
            else
                _playerWeaponManager = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);
        }

        if (_playerWeaponManager == null)
            return true;

        return !_playerWeaponManager.IsUsingHandsOnly();
    }

    private bool IsRearAwarenessBlocked(Vector3 targetPos)
    {
        if (_player == null)
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

            if (hit.collider.transform.IsChildOf(_player))
                return false;

            return true;
        }

        return false;
    }

    // =========================================================
    // IDLE / PATROL
    // =========================================================

    private void TickIdle()
    {
        if (!AgentReady())
            return;

        _agent.speed = patrolSpeed;

        if (_agent.hasPath && _agent.desiredVelocity.sqrMagnitude > 0.01f)
        {
            FaceVisualToDirection(_agent.desiredVelocity);
            return;
        }

        Vector3 targetPos = NPCPlayerTargetUtility.GetTargetPosition(_player);
        Vector3 toTarget = targetPos - transform.position;

        const float nearFaceDistance = 3.0f;

        if (toTarget.sqrMagnitude <= nearFaceDistance * nearFaceDistance)
            FaceVisualToDirection(toTarget);
    }

    private void PatrolPickNewPoint()
    {
        if (_isDead || _aggro || !AgentReady())
        {
            Invoke(nameof(PatrolPickNewPoint), Random.Range(patrolIntervalMin, patrolIntervalMax));
            return;
        }

        Vector3 randomOffset = Random.insideUnitSphere * patrolRadius;
        randomOffset.y = 0f;

        Vector3 target = transform.position + randomOffset;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            _agent.isStopped = false;
            _agent.SetDestination(hit.position);
        }

        Invoke(nameof(PatrolPickNewPoint), Random.Range(patrolIntervalMin, patrolIntervalMax));
    }

    // =========================================================
    // COMBAT TICK
    // =========================================================

    private void TickCombat()
    {
        if (!IsCombatTargetValid())
        {
            if (!TrySwitchToQueuedTarget())
            {
                ReturnToIdleAfterTargetLost();
                return;
            }
        }

        Vector3 targetPos = NPCPlayerTargetUtility.GetTargetPosition(_combatTarget);
        Vector3 toPlayer = targetPos - transform.position;

        float sqrAlertDistance = alertForgetDistance * alertForgetDistance;

        if (toPlayer.sqrMagnitude <= sqrAlertDistance)
            MarkTargetAsSeenForAlert();

        float distance = toPlayer.magnitude;

        Vector3 direction = distance > 0.001f
            ? toPlayer / distance
            : transform.forward;

        FaceVisualToDirection(direction);

        UpdateCombatAgentSpeed();
        HandleCombatMovement(targetPos, direction, distance);
        TryStartCombo(distance);
    }

    private void UpdateCombatAgentSpeed()
    {
        if (!AgentReady())
            return;

        _agent.speed = _enraged
            ? Mathf.Max(chaseSpeed, enragedChaseSpeed)
            : chaseSpeed;
    }

    private void HandleCombatMovement(Vector3 targetPos, Vector3 directionToPlayer, float distance)
    {
        if (!AgentReady())
            return;

        if (distance <= attackRange)
        {
            StopAgentForAttack();
            return;
        }

        float targetRadius = GetHoldRadius();
        Vector3 ringPoint = GetRingPointAroundTarget(targetPos, directionToPlayer, targetRadius);

        if (distance < targetRadius * 0.9f)
        {
            BackoffFromPlayer(directionToPlayer);
            return;
        }

        if (Mathf.Abs(distance - targetRadius) < 0.3f)
        {
            StrafeAroundPlayer(directionToPlayer);
            return;
        }

        RepathToRingPoint(ringPoint);
    }

    private float GetHoldRadius()
    {
        float jitter = Random.Range(-holdJitter, holdJitter);
        return Mathf.Max(0.1f, holdDistance + jitter);
    }

    private Vector3 GetRingPointAroundTarget(Vector3 targetPos, Vector3 directionToPlayer, float targetRadius)
    {
        Vector3 ringPoint = targetPos - directionToPlayer * targetRadius;

        if (NavMesh.SamplePosition(ringPoint, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
            ringPoint = hit.position;

        return ringPoint;
    }

    private void StopAgentForAttack()
    {
        if (!AgentReady())
            return;

        _agent.isStopped = true;

        if (_agent.hasPath)
            _agent.ResetPath();
    }

    private void BackoffFromPlayer(Vector3 directionToPlayer)
    {
        if (!AgentReady())
            return;

        _agent.isStopped = false;

        float speed = _enraged
            ? Mathf.Max(backoffSpeed, enragedBackoffSpeed)
            : backoffSpeed;

        _agent.Move(-directionToPlayer * speed * Time.deltaTime);
    }

    private void StrafeAroundPlayer(Vector3 directionToPlayer)
    {
        if (!AgentReady())
            return;

        _agent.isStopped = false;

        Vector3 tangent = Vector3.Cross(Vector3.up, directionToPlayer).normalized;

        float speed = _enraged
            ? Mathf.Max(strafeSpeed, enragedStrafeSpeed)
            : strafeSpeed;

        _agent.Move(tangent * speed * Time.deltaTime);
    }

    private void RepathToRingPoint(Vector3 ringPoint)
    {
        if (!AgentReady())
            return;

        if (Time.time < _nextRepath)
            return;

        _nextRepath = Time.time + repathRate;

        _agent.isStopped = false;
        _agent.SetDestination(ringPoint);
    }

    private void EnterAggro()
    {
        if (_aggro)
            return;

        if (_combatTarget == null)
        {
            SetCombatTarget(
                _player,
                _playerStats,
                null,
                null,
                null,
                true
            );
        }

        ClearRearAwarenessHold();

        _aggro = true;
        MarkTargetAsSeenForAlert();

        if (_reactive != null)
            _reactive.enabled = false;
    }

    // =========================================================
    // COMBO / DAMAGE TO PLAYER
    // =========================================================

    private void TryStartCombo(float distance)
    {
        if (_inCombo)
            return;

        if (distance > attackRange + 0.05f)
            return;

        if (_playerStats == null || _playerStats.IsDead)
            return;

        StartCoroutine(ComboRoutine());
    }

    private IEnumerator ComboRoutine()
    {
        _inCombo = true;

        StopAgentForAttack();

        int hits = (int)meleeType;

        for (int i = 0; i < hits; i++)
        {
            if (_isDead || _player == null || (_playerStats != null && _playerStats.IsDead))
                break;

            TryApplyMeleeDamageOnce();

            if (i < hits - 1)
                yield return new WaitForSeconds(comboStepInterval);
        }

        yield return new WaitForSeconds(comboCooldown);

        if (AgentReady())
            _agent.isStopped = false;

        _inCombo = false;
    }

    private void TryApplyMeleeDamageOnce()
    {
        if (!IsCombatTargetValid())
            return;

        Vector3 targetPos = NPCPlayerTargetUtility.GetTargetPosition(_combatTarget);
        Vector3 toTarget = targetPos - transform.position;

        if (toTarget.sqrMagnitude > (attackRange + 0.25f) * (attackRange + 0.25f))
            return;

        if (_combatTargetIsPlayer)
        {
            if (_combatTargetPlayerStats == null || _combatTargetPlayerStats.IsDead)
                return;

            _combatTargetPlayerStats.TakeDamage(damagePerHit, gameObject.name);
            DamageIndicatorUI.Instance?.TriggerFromWorld(transform.position, damagePerHit);
            return;
        }

        IDamageable damageable = _combatTarget.GetComponentInParent<IDamageable>();

        if (damageable == null)
            return;

        damageable.TakeDamage(damagePerHit, gameObject.name);
    }

    private bool TryResolveAttackerTarget(
    string attackerName,
    out Transform target,
    out PlayerStats targetPlayerStats,
    out NPCCore targetCore,
    out NPCController targetController,
    out NPCMelee targetMelee,
    out bool isPlayer)
    {
        target = null;
        targetPlayerStats = null;
        targetCore = null;
        targetController = null;
        targetMelee = null;
        isPlayer = false;

        if (string.IsNullOrWhiteSpace(attackerName))
            return false;

        if (attackerName.Contains("Player"))
        {
            target = _player;
            targetPlayerStats = _playerStats;
            isPlayer = true;
            return target != null;
        }

        NPCController[] controllers = FindObjectsByType<NPCController>(FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            NPCController controller = controllers[i];

            if (controller == null || controller.IsDead)
                continue;

            if (controller.gameObject == gameObject)
                continue;

            if (controller.name != attackerName && controller.gameObject.name != attackerName)
                continue;

            target = controller.transform;
            targetController = controller;
            targetCore = controller.GetComponent<NPCCore>();
            isPlayer = false;
            return true;
        }

        NPCMelee[] melees = FindObjectsByType<NPCMelee>(FindObjectsSortMode.None);

        for (int i = 0; i < melees.Length; i++)
        {
            NPCMelee melee = melees[i];

            if (melee == null || melee == this || melee.IsDead)
                continue;

            if (melee.name != attackerName && melee.gameObject.name != attackerName)
                continue;

            target = melee.transform;
            targetMelee = melee;
            targetCore = melee.GetComponent<NPCCore>();
            isPlayer = false;
            return true;
        }

        return false;
    }

    private void SetCombatTarget(
    Transform target,
    PlayerStats targetPlayerStats,
    NPCCore targetCore,
    NPCController targetController,
    NPCMelee targetMelee,
    bool isPlayer)
    {
        _combatTarget = target;
        _combatTargetPlayerStats = targetPlayerStats;
        _combatTargetCore = targetCore;
        _combatTargetController = targetController;
        _combatTargetMelee = targetMelee;
        _combatTargetIsPlayer = isPlayer;
    }

    private void QueueCombatTarget(
        Transform target,
        PlayerStats targetPlayerStats,
        NPCCore targetCore,
        NPCController targetController,
        NPCMelee targetMelee)
    {
        if (target == null)
            return;

        if (_combatTarget == target)
            return;

        _queuedTarget = target;
        _queuedTargetPlayerStats = targetPlayerStats;
        _queuedTargetCore = targetCore;
        _queuedTargetController = targetController;
        _queuedTargetMelee = targetMelee;
    }

    private void ClearCombatTarget()
    {
        _combatTarget = null;
        _combatTargetPlayerStats = null;
        _combatTargetCore = null;
        _combatTargetController = null;
        _combatTargetMelee = null;
        _combatTargetIsPlayer = false;
    }

    private void ClearQueuedTarget()
    {
        _queuedTarget = null;
        _queuedTargetPlayerStats = null;
        _queuedTargetCore = null;
        _queuedTargetController = null;
        _queuedTargetMelee = null;
    }

    // =========================================================
    // DAMAGE RECEIVED
    // =========================================================

    public void TakeDamage(int damage, string attackerName)
    {
        if (_isDead)
            return;

        RegisterDamageAttacker(attackerName);

        bool preventedDeath;
        bool shouldDie;

        if (!ApplyDamage(damage, attackerName, out preventedDeath, out shouldDie))
            return;

        PlayHitReaction();

        if (preventedDeath)
        {
            _hp = Mathf.Max(1, _hp);
            return;
        }

        if (shouldDie || _hp <= 0)
            Die(attackerName);
    }

    private void RegisterDamageAttacker(string attackerName)
    {
        if (!TryResolveAttackerTarget(
                attackerName,
                out Transform target,
                out PlayerStats targetPlayerStats,
                out NPCCore targetCore,
                out NPCController targetController,
                out NPCMelee targetMelee,
                out bool isPlayer))
        {
            return;
        }

        if (_combatTarget == null || !IsCombatTargetValid())
        {
            SetCombatTarget(
                target,
                targetPlayerStats,
                targetCore,
                targetController,
                targetMelee,
                isPlayer
            );

            return;
        }

        QueueCombatTarget(
            target,
            targetPlayerStats,
            targetCore,
            targetController,
            targetMelee
        );
    }

    private bool IsCombatTargetValid()
    {
        if (_combatTarget == null)
            return false;

        if (_combatTargetIsPlayer)
        {
            return _combatTargetPlayerStats != null && !_combatTargetPlayerStats.IsDead;
        }

        if (_combatTargetCore != null && _combatTargetCore.IsDead)
            return false;

        if (_combatTargetController != null && _combatTargetController.IsDead)
            return false;

        if (_combatTargetMelee != null && _combatTargetMelee.IsDead)
            return false;

        return true;
    }

    private bool TrySwitchToQueuedTarget()
    {
        if (_queuedTarget == null)
            return false;

        _combatTarget = _queuedTarget;
        _combatTargetPlayerStats = _queuedTargetPlayerStats;
        _combatTargetCore = _queuedTargetCore;
        _combatTargetController = _queuedTargetController;
        _combatTargetMelee = _queuedTargetMelee;
        _combatTargetIsPlayer = _queuedTargetPlayerStats != null;

        ClearQueuedTarget();

        return IsCombatTargetValid();
    }

    private void ReturnToIdleAfterTargetLost()
    {
        ClearCombatTarget();
        ClearQueuedTarget();

        _aggro = false;
        _enraged = false;
        _inCombo = false;
        _investigatingShot = false;

        HideAlert();

        if (_reactive != null)
            _reactive.enabled = true;

        if (AgentReady())
        {
            _agent.isStopped = false;
            _agent.ResetPath();
        }

        PatrolPickNewPoint();
    }

    private bool ApplyDamage(int damage, string attackerName, out bool preventedDeath, out bool shouldDie)
    {
        preventedDeath = false;
        shouldDie = false;

        if (core != null)
        {
            NPCCore.DamageResult result = core.TryTakeDamage(damage, attackerName);

            if (result.blocked)
                return false;

            _hp = Mathf.RoundToInt(result.currentHP);

            preventedDeath = result.preventedDeath;
            shouldDie = result.wouldDie;

            return true;
        }

        _hp -= Mathf.Max(0, damage);
        shouldDie = _hp <= 0;

        return true;
    }

    private void PlayHitReaction()
    {
        if (core == null)
            RestartHitFlash();

        if (!_aggro)
        {
            EnterAggro();
            MarkTargetAsSeenForAlert();
        }
        else
        {
            MarkTargetAsSeenForAlert();
        }

        _enraged = true;

        if (AgentReady())
            _agent.speed = Mathf.Max(chaseSpeed, enragedChaseSpeed);

        HitFeedbackUtility.PlayHitFx(
            transform,
            bloodFxPrefab,
            hurtSfx,
            null,
            null,
            bloodFxScale,
            bloodFxLifetime,
            audioSource
        );
    }

    private void RestartHitFlash()
    {
        if (_flashCo != null)
            StopCoroutine(_flashCo);

        _flashCo = StartCoroutine(FlashRed(hitFlashDuration));
    }

    // =========================================================
    // DEATH
    // =========================================================

    private void Die(string attackerName)
    {
        if (_isDead)
            return;

        _hp = 0;
        _isDead = true;

        if (core == null)
            ApplyBodyColor(Color.red);

        if (core != null)
            core.ConfirmDeath(attackerName);

        OnMeleeNPCDied?.Invoke(this, attackerName);

        HideAlert();
        StopAgentOnDeath();
        DisableComponentsOnDeath();

        EnableRagdollFall();
        ApplyVehicleImpactImpulseIfNeeded();

        StopAllCoroutines();
        _flashCo = null;

        StartCoroutine(Despawn(12f));
    }

    private void StopAgentOnDeath()
    {
        if (!AgentReady())
            return;

        _agent.isStopped = true;
        _agent.ResetPath();
        _agent.enabled = false;
    }

    private void DisableComponentsOnDeath()
    {
        if (_animator != null)
            _animator.enabled = false;

        if (_reactive != null)
            _reactive.enabled = false;

        if (_billboard != null)
            _billboard.enabled = false;
    }

    private void EnableRagdollFall()
    {
        RagdollFallUtility.Enable(transform, ref _rootRb, ref _rootCol, false);
    }

    private IEnumerator Despawn(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Destroy(gameObject);
    }

    // =========================================================
    // SHOT HEARING
    // =========================================================

    private void OnPlayerShotHeard(Vector3 shotOrigin, Vector3 shotDir, Vector3 impactPoint)
    {
        if (_isDead || _player == null || CheatState.Alliance)
            return;

        float maxReactSqr = reactShotMaxDistance * reactShotMaxDistance;

        float originSqr = (transform.position - shotOrigin).sqrMagnitude;
        float impactSqr = (transform.position - impactPoint).sqrMagnitude;

        if (originSqr > maxReactSqr && impactSqr > maxReactSqr)
            return;

        Vector3 probePoint = transform.position + Vector3.up * 1.2f;
        float missDistance = DistancePointToRay(probePoint, shotOrigin, shotDir.normalized);

        bool nearMiss = missDistance <= nearMissThreshold;
        bool closeImpact = impactSqr <= shotHearRadius * shotHearRadius;

        if (!nearMiss && !closeImpact)
            return;

        Vector3 eye = transform.position + Vector3.up * shotLOSProbeHeight;
        Vector3 checkTarget = nearMiss ? shotOrigin : impactPoint;
        Vector3 toCheck = checkTarget - eye;

        if (toCheck.sqrMagnitude <= 0.001f)
            return;

        LayerMask maskNoNPC = losMask & ~LayerMask.GetMask("NPC");

        if (Physics.Raycast(
                eye,
                toCheck.normalized,
                out RaycastHit block,
                toCheck.magnitude,
                maskNoNPC,
                QueryTriggerInteraction.Ignore))
        {
            if (!block.collider.transform.IsChildOf(transform))
                return;
        }

        FaceVisualToDirection(checkTarget - transform.position);
    }

    private void TickShotInvestigation()
    {
        if (!_aggro)
            return;

        if (!_investigatingShot)
            return;

        if (Time.time >= _investigateUntil)
            return;

        if (!AgentReady())
            return;

        if (_agent.pathPending)
            return;

        if (_agent.remainingDistance > 0.9f)
            return;

        FaceVisualToDirection(_shotInvestigatePoint - transform.position);
        _investigatingShot = false;
    }

    private static float DistancePointToRay(Vector3 point, Vector3 rayOrigin, Vector3 rayDirNormalized)
    {
        Vector3 toPoint = point - rayOrigin;
        float t = Vector3.Dot(toPoint, rayDirNormalized);

        if (t <= 0f)
            return toPoint.magnitude;

        Vector3 projection = rayOrigin + rayDirNormalized * t;
        return Vector3.Distance(point, projection);
    }

    // =========================================================
    // ALERT
    // =========================================================

    private void ShowAlert()
    {
        if (alertIcon != null && !alertIcon.activeSelf)
            alertIcon.SetActive(true);
    }

    private void HideAlert()
    {
        if (alertIcon != null && alertIcon.activeSelf)
            alertIcon.SetActive(false);
    }

    private void MarkTargetAsSeenForAlert()
    {
        _lastAlertSeenTime = Time.time;
        ShowAlert();
    }

    private void UpdateAlertVisibility()
    {
        if (!_aggro || _isDead)
        {
            HideAlert();
            return;
        }

        if (_player == null)
        {
            HideAlert();
            return;
        }

        if (Time.time < _nextAlertCheckTime)
            return;

        _nextAlertCheckTime = Time.time + alertRefreshRate;

        Vector3 targetPos = NPCPlayerTargetUtility.GetTargetPosition(_player);

        if (CanSeeTargetForAlert(targetPos))
            MarkTargetAsSeenForAlert();

        if (Time.time - _lastAlertSeenTime > alertLoseSightDelay)
            HideAlert();
        else
            ShowAlert();
    }

    private bool CanSeeTargetForAlert(Vector3 targetPos)
    {
        Vector3 eye = transform.position + Vector3.up * shotLOSProbeHeight;
        Vector3 toTarget = targetPos + Vector3.up * 1.2f - eye;

        float distance = toTarget.magnitude;

        if (distance > alertForgetDistance)
            return false;

        Vector3 flatToTarget = toTarget;
        flatToTarget.y = 0f;

        if (flatToTarget.sqrMagnitude < 0.001f)
            return true;

        Vector3 forward = visualRoot != null ? visualRoot.forward : transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        float angle = Vector3.Angle(forward.normalized, flatToTarget.normalized);

        if (angle > viewAngle * 0.5f)
            return false;

        LayerMask maskNoNPC = losMask & ~LayerMask.GetMask("NPC");

        if (Physics.Raycast(eye, toTarget.normalized, out RaycastHit hit, distance, maskNoNPC, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(transform))
                return false;
        }

        return true;
    }

    // =========================================================
    // VISUALS
    // =========================================================

    private void FaceVisualToDirection(Vector3 directionWorld)
    {
        if (visualRoot == null)
            return;

        directionWorld.y = 0f;

        if (directionWorld.sqrMagnitude < 0.000001f)
            return;

        Quaternion lookRotation = Quaternion.LookRotation(directionWorld.normalized);

        if (visualYawOffset != 0f)
            lookRotation *= Quaternion.Euler(0f, visualYawOffset, 0f);

        visualRoot.rotation = Quaternion.Slerp(
            visualRoot.rotation,
            lookRotation,
            Time.deltaTime * 10f
        );
    }

    private void ApplyBodyColor(Color color)
    {
        if (bodyRenderers == null || bodyRenderers.Length == 0)
            return;

        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            Renderer renderer = bodyRenderers[i];

            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorID, color);
            _mpb.SetColor(ColorID, color);
            renderer.SetPropertyBlock(_mpb);
        }
    }

    private IEnumerator FlashRed(float duration)
    {
        ApplyBodyColor(Color.red);

        yield return new WaitForSeconds(duration);

        if (!_isDead)
            ApplyBodyColor(_baseColor);
    }

    private void RefreshBodyRenderersIfNeeded()
    {
        if (bodyRenderers != null && bodyRenderers.Length > 0)
            return;

        bodyRenderers = GetComponentsInChildren<Renderer>(true);
    }

    // =========================================================
    // VEHICLE IMPACT
    // =========================================================

    public void ReceiveVehicleImpact(
        float damage,
        float speedKmh,
        Vector3 vehicleVelocity,
        Vector3 hitPoint,
        string attackerName = "PlayerVehicle")
    {
        if (_isDead)
            return;

        _pendingVehicleImpact = true;
        _pendingVehicleVelocity = vehicleVelocity;
        _pendingVehicleSpeedKmh = speedKmh;

        TakeDamage(Mathf.CeilToInt(damage), attackerName);
    }

    private void ApplyVehicleImpactImpulseIfNeeded()
    {
        if (!_pendingVehicleImpact || _rootRb == null)
            return;

        Vector3 direction = _pendingVehicleVelocity;

        if (direction.sqrMagnitude < 0.01f)
            direction = transform.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            direction = transform.forward;

        direction.Normalize();

        float speed01 = Mathf.InverseLerp(10f, 90f, _pendingVehicleSpeedKmh);

        float forwardImpulse = Mathf.Lerp(vehicleForwardImpulseMin, vehicleForwardImpulseMax, speed01);
        float upImpulse = Mathf.Lerp(vehicleUpImpulseMin, vehicleUpImpulseMax, speed01);
        float torqueImpulse = Mathf.Lerp(vehicleTorqueImpulseMin, vehicleTorqueImpulseMax, speed01);

        _rootRb.linearDamping = ragdollDragAfterVehicleHit;
        _rootRb.angularDamping = ragdollAngularDragAfterVehicleHit;
        _rootRb.maxAngularVelocity = 8f;

        _rootRb.AddForce(
            direction * forwardImpulse + Vector3.up * upImpulse,
            ForceMode.Impulse
        );

        // Keep torque small so the body does not spin unnaturally.
        _rootRb.AddTorque(
            Random.insideUnitSphere * torqueImpulse,
            ForceMode.Impulse
        );

        _pendingVehicleImpact = false;
    }

    // =========================================================
    // PROFILE
    // =========================================================

    public void ApplyProfile(NPCProfile profile)
    {
        if (profile == null)
            return;

        if (!profile.useMelee)
            return;

        maxHP = Mathf.Max(1, profile.meleeMaxHP);
        _hp = maxHP;

        damagePerHit = Mathf.Max(1, profile.meleeDamagePerHit);
        chaseSpeed = Mathf.Max(0.1f, profile.meleeChaseSpeed);
        enragedChaseSpeed = Mathf.Max(chaseSpeed, profile.meleeEnragedChaseSpeed);
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    private void ResolvePlayerRefs()
    {
        NPCSceneRefs refs = NPCSceneRefs.Instance;

        if (refs != null && refs.HasPlayer())
        {
            _player = refs.Player;
            _playerStats = refs.PlayerStats;
            CachePlayerMotionRefs();
            return;
        }

        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");

        if (playerGo == null)
            return;

        _player = playerGo.transform;
        _playerStats = playerGo.GetComponent<PlayerStats>();
        CachePlayerMotionRefs();
    }

    private void CachePlayerMotionRefs()
    {
        if (_player == null)
            return;

        _playerCharacterController = _player.GetComponent<CharacterController>();
        _playerRigidbody = _player.GetComponent<Rigidbody>();

        _playerMovement = _player.GetComponent<PlayerMovement>();

        if (_playerMovement == null)
            _playerMovement = _player.GetComponentInParent<PlayerMovement>();

        if (_playerMovement == null)
            _playerMovement = _player.GetComponentInChildren<PlayerMovement>(true);

        NPCSceneRefs refs = NPCSceneRefs.Instance;

        if (refs != null && refs.WeaponManager != null)
            _playerWeaponManager = refs.WeaponManager;
        else
            _playerWeaponManager = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);

        _rearAwarenessPlayerPosInitialized = false;
    }
}