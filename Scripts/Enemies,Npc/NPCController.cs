using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class NPCController : MonoBehaviour, IDamageable
{
    private NPCCore core;

    public enum NPCReactionType { Coward, Aggressive, Fighter }

    [Header("Typ zachowania")]
    public NPCReactionType reactionType = NPCReactionType.Coward;

    [Header("Fighter wariant")]
    [SerializeField] private FighterVariant fighterVariant = FighterVariant.Blue; // dotyczy tylko Fighter
    public enum FighterVariant { Blue, Black }

    [Header("Coward – ucieczka i blokady")]
    [SerializeField] private bool propagatePanicToWitnesses = true;
    [SerializeField] private float fleeDuration = 6.0f;
    [SerializeField] private float fleeFarDistance = 18.0f;   // od jakiej odległości czuje się „bezpiecznie”
    [SerializeField] private LayerMask npcMask;               // warstwa NPC (do świadków)
    [SerializeField] private Collider[] interactionColliders; // collidery „E”
    [SerializeField] private Collider[] physicalColliders;    // collidery ciała NPC (do IgnoreCollision z graczem)

    [Header("Fighter – incydenty Cowardów")]
    [SerializeField] private float patrolInvestigateTime = 6f;
    [SerializeField] private float investigateArriveTolerance = 1.2f;

    private bool isFleeing;
    private bool interactionDisabledForever;
    private Vector3 lastKnownAttackerPos;

    // Prosty „bus” zdarzeń – globalny
    public static event System.Action<Vector3> OnCowardReportedLastKnownPos;       // po ucieczce

    [Header("WeaponsINV")]
    public NPCGun[] availableWeapons;
    private NPCGun equippedGun;
    [SerializeField] private string assignedWeaponName;

    [Header("NPC Weapon Roots")]
    [SerializeField] private Transform weaponsListRoot;

    [Header("Feature Flags")]
    [SerializeField] private bool useWeaponSystem = true;
    [SerializeField] private bool allowWeaponDrop = true;

    [Header("Drop / Loot")]
    [Range(0f, 100f)] public float weaponDropChance = 50f;
    [SerializeField] private Transform weaponDropPoint;

    [Header("WeaponPickup prefabs")]
    public GameObject GlockPickup;
    public GameObject M4A1Pickup;
    public GameObject AK97Pickup;
    public GameObject SPAS12Pickup;

    [Header("Statystyki HP")]
    public float maxHP = 100f;
    private float currentHP;

    [Header("AI Settings")]
    [SerializeField] private float interactRange = 2.5f;
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float engageDistance = 8f;
    [SerializeField] private float fleeDistance = 20f;
    [SerializeField] private float reactionDuration = 30f;

    [Header("Strzelanie (NPC)")]
    public int shotsPerBurst = 2;
    public float fireCooldown = 1.5f;
    public float aimDelay = 0.15f;
    public float minShootingDistance = 5f;

    [Header("Kolory / trafienia")]
    [SerializeField] private Renderer[] bodyRenderers;
    [SerializeField] private float hitFlashDuration = 0.5f;

    [Header("Hit FX (opcjonalnie)")]
    [SerializeField] private GameObject bloodFxPrefab;
    [SerializeField] private float bloodFxScale = 1f;
    [SerializeField] private float bloodFxLifetime = 2f;
    [SerializeField] private AudioClip hurtSfx;
    [SerializeField] private AudioSource audioSource; // opcjonalnie (może być null)

    // kolor bazowy (nieserializowany)
    private Color defaultColor;
    public Color DefaultColor => defaultColor;

    [Header("Wzrok (Aggressive/Fighter)")]
    [SerializeField] private float viewDistance = 30f;
    [SerializeField, Range(10f, 180f)] private float viewAngle = 110f;
    [SerializeField] private LayerMask losObstaclesMask = ~0; // warstwy, które BLOKUJĄ wzrok
                                                              // na górze pól// na górze pól
    private bool _defenseMode; // Fighter wchodzi do walki tylko, gdy broni Cowarda albo został trafiony

    [Header("Black Aggressive Hunt")]
    [SerializeField] private bool blackVariantHuntsSilently = true;
    [SerializeField] private float blackHuntRepathInterval = 0.75f;
    [SerializeField] private float blackHuntSpeed = 4.5f;

    private float _nextBlackHuntRepath;

    [Header("Walka – sekwencja")]
    // NPCController.cs
    private Vector3 _lastPlayerPos;
    private bool _playerPosInitialized = false;

    [Header("Vertical Awareness (gdy gracz wysoko)")]
    [SerializeField] private float verticalAimTolerance = 2.5f;   // jeśli |ΔY| > tego – NIE strzelaj
    [SerializeField] private float campUnderPlayerRadius = 4.0f;   // promień „czatowania” pod graczem
    [SerializeField] private float campRepathInterval = 0.8f;      // jak często odświeżać trasę „pod spodem”
    [SerializeField] private float maxApproachSpeedWhenCamping = 6.5f; // prędkość w trybie czatowania

    [SerializeField] private float faceAngleThreshold = 15f;        // stopnie – kiedy uznajemy, że patrzy na gracza
    [SerializeField] private float weaponDrawTime = 0.05f;          // delikatna zwłoka „wyciągania broni”
    [SerializeField] private float desiredShootingDistance = 12f;  // dystans docelowy do strzału
    [SerializeField] private float retreatBuffer = 0.75f;          // ile się odsunąć, gdy za blisko
    private Coroutine attackCoroutine;
    private float _nextRepath = 0f;

    [Header("Słuch (strzał w pobliżu)")]
    [SerializeField] private float shotHearRadius = 18f;
    [SerializeField] private float shotLOSProbeHeight = 1.6f;
    [SerializeField] private float investigateFromShotTime = 4.0f;

    [Header("Reakcja na strzał w pobliżu")]
    [SerializeField] private float reactShotMaxDistance = 30f;
    [SerializeField] private float nearMissThreshold = 2.5f;
    [SerializeField] private LayerMask shotHearingObstaclesMask = ~0;

    private bool investigatingShot = false;
    private float investigateShotUntil = 0f;
    private Vector3 lastShotPoint;
    // wewnętrzne
    private NavMeshAgent agent;
    private Transform player;
    private PlayerStats playerStats;
    private bool isProvoked = false;
    private bool isDead = false;
    private float reactionEndTime;

    private MaterialPropertyBlock _mpb;
    private Coroutine flashCoroutine;
    private bool inVictoryState = false;
    private static float panicPropagationRadius = 15f;
    private Quaternion startRotation;

    private Rigidbody rootRb;
    private Collider rootCol;

    // zapamiętanie atakującego (do eventu śmierci)
    private string lastAttacker = "Unknown";
    private Animator cachedAnimator;
    private bool deathSequenceStarted = false;

    // globalny event śmierci NPC (dla świadków – obsługuje NPCReactive)
    public static System.Action<NPCController, string> OnNPCDied;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    public bool IsInteractionLocked => interactionDisabledForever || isFleeing;
    public bool IsScaredVisible => scaredIcon != null && scaredIcon.activeSelf;

    [SerializeField] private GameObject alertIcon;      // już masz
    [SerializeField] private SpriteRenderer alertSprite; // już masz

    [Header("Alert Sprites")]
    [SerializeField] private GameObject scaredIcon;      // ⬅️ NOWE: obiekt „Scared” (wyłączony w prefabie)
    [SerializeField] private SpriteRenderer scaredSpriteRenderer; // ⬅️ opcjonalnie (jeśli chcesz sterować kolorem)

    private bool pendingBackstabFall = false;
    private bool pendingMeleeFall = false;
    private Vector3 pendingMeleeAttackerPos;

    // ===== PUBLICZNE WŁAŚCIWOŚCI/INTERFEJS =====
    public bool IsDead => isDead;
    public bool IsProvoked => isProvoked;
    public NPCReactionType GetReactionType() => reactionType;
    public FighterVariant GetFighterVariant() => fighterVariant;
    public bool IsAggressiveBlackVariant() =>
        reactionType == NPCReactionType.Aggressive && fighterVariant == FighterVariant.Black;

    /// <summary>Z zewnątrz (NPCReactive) możesz wymusić przejście w agresję.</summary>
    public void ForceReactToAggression() => StartAggression(byHit: false);

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

    // ===== UNITY =====

    private void Update()
    {

        if (CheatState.Alliance)
        {
            // rozbrój agresję, zatrzymaj broń, pozwól im „żyć swoim życiem”
            if (isProvoked) { isProvoked = false; HolsterWeapon(true); }
            HideAllIcons();

            // możesz zostawić patrol itd.
            return;
        }

        if (isDead || player == null) return;
        if (inVictoryState) return;


        if (reactionType == NPCReactionType.Fighter)
        {
            if (_defenseMode && !isProvoked && PlayerInFrontAndVisible())
                StartAggression(byHit: false);
        }


        // Aggressive: czarny poluje non-stop; niebieski (gdybyś miał) może nadal na wzrok
        if (reactionType == NPCReactionType.Aggressive && !isProvoked)
        {
            if (fighterVariant == FighterVariant.Black)
            {
                if (PlayerInFrontAndVisible())
                {
                    StartAggression(byHit: false);
                }
                else if (blackVariantHuntsSilently)
                {
                    SilentBlackHuntTick();
                }
            }
            else
            {
                if (PlayerInFrontAndVisible())
                    StartAggression(byHit: false);
            }
        }

        // wygaszenie prowokacji (tylko nie-Fighter)
        if (isProvoked &&
            reactionType != NPCReactionType.Fighter &&
            (Time.time > reactionEndTime || Vector3.Distance(transform.position, GetCurrentTargetPosition()) > 25f))
        {
            isProvoked = false;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
            ApplyBodyColor(defaultColor);
            HolsterWeapon(true);
            if (attackCoroutine != null) { StopCoroutine(attackCoroutine); attackCoroutine = null; }
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
                // Dotarł – chwilę „czeka/rozgląda się”
                investigateShotUntil = Mathf.Min(investigateShotUntil, Time.time + 1.25f);
            }
        }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rootRb = GetComponent<Rigidbody>();
        cachedAnimator = GetComponentInChildren<Animator>(true);
        core = GetComponent<NPCCore>();
        // bierz tylko solid collider z roota
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

        // fallback: jeśli root nie ma sensownego collidera, weź pierwszy fizyczny z physicalColliders
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

        var p = GameObject.FindGameObjectWithTag("Player");
        player = p ? p.transform : null;
        playerStats = p ? p.GetComponent<PlayerStats>() : null;

        RefreshBodyRenderers();
        _mpb = new MaterialPropertyBlock();

        RecomputeBaseColor();
        currentHP = maxHP;

        if (!alertSprite && alertIcon) alertSprite = alertIcon.GetComponent<SpriteRenderer>();
        HideAllIcons();

        if (scaredIcon && !scaredSpriteRenderer)
            scaredSpriteRenderer = scaredIcon.GetComponent<SpriteRenderer>();
        if (scaredIcon) scaredIcon.SetActive(false);

        // ustaw root rigidbody poprawnie za życia
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

            // Czarny NPC jest "łowcą", ale nie pokazuje alertu i nie wyciąga broni,
            // dopóki faktycznie nie wejdzie w walkę.
            HolsterWeapon(true);
            HideAllIcons();
        }
    }

    private void OnEnable()
    {
        PlayerStats.OnPlayerDied += HandlePlayerDied;

        OnCowardReportedLastKnownPos += HandleCowardReported;

        Gun.OnPlayerShot += OnPlayerShotHeard;  // ✅
    }
    private void OnDisable()
    {
        PlayerStats.OnPlayerDied -= HandlePlayerDied;

        OnCowardReportedLastKnownPos -= HandleCowardReported;

        Gun.OnPlayerShot -= OnPlayerShotHeard;  // ✅
    }

    // Helper — odległość punktu od promienia (ray)
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

        // limit zasięgu reakcji (użyj bliższego z dwóch punktów zdarzenia)
        float dOrigin = Vector3.Distance(transform.position, shotOrigin);
        float dImpact = Vector3.Distance(transform.position, impactPoint);
        float dMin = Mathf.Min(dOrigin, dImpact);
        if (dMin > reactShotMaxDistance) return;

        float miss = DistancePointToRay(transform.position + Vector3.up * shotLOSProbeHeight, shotOrigin, shotDir.normalized);
        float distToImpact = Vector3.Distance(transform.position, impactPoint);
        bool nearMiss = miss <= nearMissThreshold;
        bool closeImpact = distToImpact <= shotHearRadius;
        if (!nearMiss && !closeImpact) return;

        // LOS – użyj wysokości z pola
        Vector3 eye = transform.position + Vector3.up * shotLOSProbeHeight;
        Vector3 toCheck = (nearMiss ? (shotOrigin - eye) : (impactPoint - eye));
        var maskNoNPC = shotHearingObstaclesMask & ~LayerMask.GetMask("NPC");

        if (Physics.Raycast(eye, toCheck.normalized, out RaycastHit block, toCheck.magnitude,
                            maskNoNPC, QueryTriggerInteraction.Ignore))
        {
            // jeśli trafiliśmy siebie/swoje dzieci – zignoruj
            if (!block.collider.transform.IsChildOf(transform)) return;
        }

        Vector3 focus = nearMiss ? shotOrigin : impactPoint;
        FacePositionXZ(focus);

        if (reactionType == NPCReactionType.Coward)
        {
            propagatePanicToWitnesses = false;
            DisableInteractionAndCollisionForever();
            StartCowardFlee();

            // zamiast alertIcon.SetActive(false);
            ShowScared(new Color(1f, 0.85f, 0.2f)); // ciepły strach
            return;
        }

        if (!isProvoked) StartAggression(byHit: false);
        reactionEndTime = Time.time + reactionDuration;

        // investigation (współpracuje z blokiem w Update)
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
        if (scaredIcon) scaredIcon.SetActive(false); // nigdy jednocześnie
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

    private bool PlayerInFrontAndVisible()
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

        // LOS – tylko przeszkody świata blokują
        if (Physics.Raycast(eye, to.normalized, out RaycastHit hit, dist, losObstaclesMask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }


    // ===== KOLOR / FLASH =====
    private Color ChooseBaseColor()
    {
        switch (reactionType)
        {
            case NPCReactionType.Fighter: return Color.blue;   // Fighter = niebieski
            case NPCReactionType.Aggressive: return Color.black;  // Aggressive = czarny
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
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        if (bodyRenderers == null || bodyRenderers.Length == 0) return;

        foreach (var r in bodyRenderers)
        {
            if (!r) continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorID, c);
            _mpb.SetColor(ColorID, c);
            r.SetPropertyBlock(_mpb);

            // Fallback – gdy shader ignoruje MPB/properties
            var mat = Application.isPlaying ? r.material : r.sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty(BaseColorID)) mat.SetColor(BaseColorID, c);
                else if (mat.HasProperty(ColorID)) mat.SetColor(ColorID, c);
                else mat.color = c; // absolutny fallback
            }
        }
    }

    private void DisableInteractionOnly()
    {
        if (interactionColliders != null)
            foreach (var c in interactionColliders) if (c) c.enabled = false;

        interactionDisabledForever = true;
    }

    private IEnumerator FlashRedCoroutine(float duration)
    {
        if (isDead) yield break;

        ApplyBodyColor(Color.red);

        yield return new WaitForSeconds(duration);

        if (!isDead)
        {
            ApplyBodyColor(defaultColor);
        }
    }

    // ===== PATROL / RUCH =====
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
        if (isProvoked || inVictoryState || agent == null || !agent.enabled || !agent.hasPath) return;

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

    // ===== AGGRESJA / ATAK =====
    private void StartAggression(bool byHit)
    {
        isProvoked = true;

        DisableInteractionOnly();

        ShowAlert(Color.red);

        if (!_playerPosInitialized && player != null)
        {
            _lastPlayerPos = GetCurrentTargetPosition();
            _playerPosInitialized = true;
        }

        reactionEndTime = Time.time + reactionDuration;

        if (attackCoroutine != null) StopCoroutine(attackCoroutine);
        attackCoroutine = StartCoroutine(AttackSequence(byHit));
    }

    private IEnumerator AttackSequence(bool byHit)
    {
        if (player == null) yield break;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            if (agent.pathPending && agent.remainingDistance > 2f)
                FacePositionXZ(GetCurrentTargetPosition());

            agent.speed = 6.5f * GetWeaponSpeedMulForNPC();
            agent.speed = Mathf.Max(agent.speed, maxApproachSpeedWhenCamping * GetWeaponSpeedMulForNPC());

            agent.acceleration = 14f;
        }

        // 1) Obrót na gracza (wstępny)
        // 1) Obrót na gracza (wstępny)
        float t = 0f, faceMax = 0.6f;
        while (!isDead && player != null)
        {
            Vector3 targetPos = GetCurrentTargetPosition();
            Vector3 to = targetPos - transform.position;
            to.y = 0f;

            float ang = Vector3.Angle(transform.forward, to);
            if (ang <= faceAngleThreshold) break;

            FacePositionXZ(GetCurrentTargetPosition());
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            t += Time.deltaTime;
            if (t >= faceMax) break;
            yield return null;
        }

        // 2) Wyjmij broń
        HolsterWeapon(false);
        if (weaponDrawTime > 0f) yield return new WaitForSeconds(weaponDrawTime);

        // 3) Krótki aim
        if (aimDelay > 0f)
        {
            float a = 0f;
            while (a < aimDelay && !isDead)
            {
                FacePositionXZ(GetCurrentTargetPosition());
                a += Time.deltaTime;
                yield return null;
            }
        }

        // 4) Docelowy dystans
        float shootDist = Mathf.Clamp(
            desiredShootingDistance > 0f ? desiredShootingDistance : (engageDistance * 0.85f),
            minShootingDistance + 0.5f,
            Mathf.Max(minShootingDistance + 1f, engageDistance)
        );

        // ——— lokals: przewidzenie punktu celowania + kierunek z lufy ———
        (Vector3 aimPos, Vector3 dir) PredictAim()
        {
            Vector3 curPos = GetCurrentTargetPosition();
            Vector3 playerVel = _playerPosInitialized
                ? (curPos - _lastPlayerPos) / Mathf.Max(Time.deltaTime, 0.0001f)
                : Vector3.zero;

            _lastPlayerPos = curPos;
            _playerPosInitialized = true;

            Vector3 muzzle = (equippedGun != null && equippedGun.FirePoint != null)
                ? equippedGun.FirePoint.position
                : transform.position + Vector3.up * 1.4f;

            float bulletSpeed = equippedGun != null
                ? Mathf.Max(1f, equippedGun.BulletSpeed)
                : 60f;

            Vector3 toTarget = (curPos + Vector3.up * 1.4f) - muzzle;
            float timeToHit = Mathf.Max(0.0f, toTarget.magnitude / Mathf.Max(1f, bulletSpeed));

            Vector3 aimNow = curPos + playerVel * timeToHit + Vector3.up * 1.4f;
            Vector3 d = (aimNow - muzzle).normalized;
            return (aimNow, d);
        }

        while (isProvoked && !isDead && player != null && playerStats != null && !playerStats.IsDead)
        {
            // ——— stan pionowy ———
            bool verticalTooBig = VerticalGapTooLarge();
            float horizDist = HorizontalDistance(transform.position, GetCurrentTargetPosition());

            // Czy powinniśmy „czaić się” pod graczem?
            bool foundUnder;
            Vector3 under = GetGroundPointUnderPlayer(searchRadius: 6f, out foundUnder);
            bool shouldCamp = foundUnder && (verticalTooBig || (agent != null && agent.isOnNavMesh && agent.pathStatus == NavMeshPathStatus.PathPartial));

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.acceleration = 14f;

                if (shouldCamp)
                {
                    // ——— TRYB „CZAJENIA SIĘ POD GRACZEM” ———
                    agent.isStopped = false;
                    agent.speed = Mathf.Max(agent.speed, maxApproachSpeedWhenCamping * GetWeaponSpeedMulForNPC());

                    // Ustaw cel na pierścieniu wokół XZ gracza, trzymaj horyzontalny dystans (shootDist)
                    bool onMesh;
                    Vector3 ring = RingPointAroundXZ(under, Mathf.Min(shootDist, campUnderPlayerRadius + shootDist * 0.2f),
                                                     preferAwayFrom: transform.position, out onMesh);
                    if (onMesh)
                    {
                        // repath co jakiś czas – unikaj spamowania SetDestination
                        if (Time.time >= _nextRepath)
                        {
                            _nextRepath = Time.time + campRepathInterval;
                            agent.SetDestination(ring);
                        }
                    }
                    else
                    {
                        // fallback – idź bliżej XZ gracza
                        if (Time.time >= _nextRepath)
                        {
                            _nextRepath = Time.time + campRepathInterval;
                            agent.SetDestination(under);
                        }
                    }

                    // Obracaj się w kierunku przewidzianego punktu celowania (użyj istniejącej PredictAim)
                    var predCamp = PredictAim();
                    FacePositionXZ(predCamp.aimPos);
                }
                else
                {
                    // ——— KLASYKA: podejdź/odsuń się po HORYZONTALNYM dystansie ———
                    if (horizDist > shootDist)
                    {
                        agent.isStopped = false;
                        agent.speed = 6.5f * GetWeaponSpeedMulForNPC();
                        agent.speed = Mathf.Max(agent.speed, maxApproachSpeedWhenCamping * GetWeaponSpeedMulForNPC());

                        var pred = PredictAim();
                        FacePositionXZ(pred.aimPos);
                        agent.SetDestination(GetCurrentTargetPosition()); // idź bliżej
                        yield return null;
                        continue;
                    }

                    if (horizDist < (minShootingDistance + retreatBuffer))
                    {
                        Vector3 back = (transform.position - GetCurrentTargetPosition());
                        back.y = 0f;
                        if (back.sqrMagnitude > 0.0001f)
                        {
                            back = back.normalized;
                            agent.isStopped = false;
                            Vector3 target = transform.position + back * ((minShootingDistance + retreatBuffer) - horizDist + 0.5f);
                            agent.SetDestination(target);
                        }
                        var pred = PredictAim();
                        FacePositionXZ(pred.aimPos);
                        yield return null;
                        continue;
                    }

                    // w dystansie → zatrzymaj się i strzel
                    agent.isStopped = true;
                    agent.ResetPath();
                }
            }

            // Szybki „snap” obrotu na przewidziane miejsce
            {
                float snap = 0f, snapMax = 0.25f;
                while (snap < snapMax && !isDead)
                {
                    var pred = PredictAim();
                    FacePositionXZ(pred.aimPos);
                    snap += Time.deltaTime;
                    yield return null;
                }
            }

            // ——— STRZAŁY: strzelamy tylko gdy różnica wysokości nie jest przesadna LUB mamy czysty LOS pod kątem ———
            bool allowFire = !verticalTooBig;
            if (!allowFire)
            {
                // spróbuj mimo różnicy: jeśli ray z lufy do celu nie trafia w świat – pozwól
                Vector3 muzzle = (equippedGun != null && equippedGun.FirePoint != null)
                    ? equippedGun.FirePoint.position
                    : transform.position + Vector3.up * 1.4f;

                Vector3 toTarget = (GetCurrentTargetPosition() + Vector3.up * 1.4f) - muzzle;
                float dist = toTarget.magnitude;

                if (!Physics.Raycast(muzzle, toTarget.normalized, dist, losObstaclesMask, QueryTriggerInteraction.Ignore))
                    allowFire = true;
            }

            if (equippedGun != null && allowFire)
            {
                if (equippedGun == null)
                {
                    Debug.LogWarning($"[NPC] {name}: Cannot fire, equippedGun is NULL.");
                }
                else if (!allowFire)
                {
                    Debug.Log($"[NPC] {name}: Cannot fire, allowFire=false. Vertical/LOS issue.");
                }

                else
                {
                    for (int i = 0; i < shotsPerBurst; i++)
                    {
                        if (!isProvoked || isDead || player == null || playerStats == null || playerStats.IsDead)
                            break;

                        float dNow = HorizontalDistance(transform.position, GetCurrentTargetPosition());

                        if (dNow > shootDist + 0.75f || dNow < (minShootingDistance + retreatBuffer))
                            break;

                        var pred = PredictAim();
                        FacePositionXZ(pred.aimPos);

                        equippedGun.TryFire(gameObject, pred.dir);

                        float wait = Mathf.Max(0.05f, equippedGun.FireRate);
                        float tWait = 0f;

                        while (tWait < wait)
                        {
                            if (!isProvoked || isDead || player == null || playerStats == null || playerStats.IsDead)
                                break;

                            var pred2 = PredictAim();
                            FacePositionXZ(pred2.aimPos);

                            tWait += Time.deltaTime;
                            yield return null;
                        }
                    }
                }
            }

            // cooldown między seriami – normalnie
            {
                float tCool = 0f;
                while (tCool < fireCooldown)
                {
                    if (!isProvoked || isDead || player == null || playerStats == null || playerStats.IsDead) break;
                    var pred = PredictAim();
                    FacePositionXZ(pred.aimPos);
                    tCool += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // koniec sekwencji (utrata celu/uspokojenie)
        HolsterWeapon(true);
        attackCoroutine = null;
    }



    // ===== ŚMIERĆ GRACZA =====
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

    // Czy gracz jest istotnie wyżej/niżej od NPC?
    private bool VerticalGapTooLarge()
    {
        if (player == null) return false;
        return Mathf.Abs(GetCurrentTargetPosition().y - transform.position.y) > verticalAimTolerance;
    }

    // Znajdź punkt navmesh „pod” graczem (XZ gracza, Y z NavMesh).
    // Zwraca found=false, jeśli nie znaleziono blisko – wtedy używaj obecnej logiki.
    private Vector3 GetGroundPointUnderPlayer(float searchRadius, out bool found)
    {
        found = false;
        if (player == null) return transform.position;

        Vector3 xz = new Vector3(GetCurrentTargetPosition().x, GetCurrentTargetPosition().y, GetCurrentTargetPosition().z);
        // przycięcie Y – próbujemy znaleźć dowolny punkt na NavMesh wokół XZ gracza
        if (NavMesh.SamplePosition(xz, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
        {
            found = true;
            return hit.position;
        }
        return transform.position;
    }

    // Policz dystans HORYZONTALNY (ignorujemy Y).
    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // Wygeneruj punkt na „pierścieniu” wokół targetXZ (tylko XZ; Y z NavMesh).
    private Vector3 RingPointAroundXZ(Vector3 targetXZ, float radius, Vector3 preferAwayFrom, out bool onMesh)
    {
        onMesh = false;
        Vector3 dir = (preferAwayFrom - targetXZ);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Random.insideUnitSphere; // dowolny kierunek
        dir.y = 0f;
        dir.Normalize();

        Vector3 candidate = targetXZ + (-dir) * Mathf.Max(0.1f, radius); // stań „przed” graczem (od jego XZ)
        if (NavMesh.SamplePosition(candidate, out NavMeshHit nh, 2.5f, NavMesh.AllAreas))
        {
            onMesh = true;
            return nh.position;
        }

        // fallback: kilka losowych prób po okręgu
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

    // NPCController.cs  (DODAJ w klasie)
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
                // NPC jest odporny albo damage = 0.
                // Na razie bez efektów, żeby np. StoryCritical nie flashował od każdego trafienia.
                return;
            }

            // Synchronizacja starego HP z nowym NPCCore,
            // żeby reszta starego NPCController dalej działała.
            currentHP = result.currentHP;

            preventedDeath = result.preventedDeath;
            shouldDie = result.wouldDie;
        }
        else
        {
            // Fallback dla starych NPC bez NPCCore.
            currentHP -= damage;
            if (currentHP < 0f) currentHP = 0f;

            shouldDie = currentHP <= 0f;
        }

        // =========================
        // 2. HIT FEEDBACK
        // =========================
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(FlashRedCoroutine(hitFlashDuration));

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
            // NPC dostał obrażenia, ale NPCCore nie pozwala mu umrzeć.
            // Zostaje na 1 HP i może reagować normalnie.
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

            // Przy mocnym damage od granatu coroutine flash może zostać szybko zatrzymana przez Die(),
            // więc ustawiamy czerwony kolor od razu.
            ApplyBodyColor(Color.red);

            // pozwól flash/blood FX wejść w tę klatkę
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

    private IEnumerator CoDieAfterHitFrame()
    {
        yield return null;
        if (!isDead && !deathSequenceStarted)
            Die();
    }

    public void TakeDamage(float dmg) => TakeDamage(Mathf.RoundToInt(dmg), "Unknown");

    // ADD ▼ — permanenty zakaz „E” + brak kolizji z graczem
    private void DisableInteractionAndCollisionForever()
    {
        if (interactionDisabledForever) return;
        interactionDisabledForever = true;

        if (interactionColliders != null)
            foreach (var c in interactionColliders) if (c) c.enabled = false;

        // wyłącz fizyczną kolizję z graczem
        if (player != null)
        {
            var playerCol = player.GetComponent<Collider>();
            if (playerCol && physicalColliders != null)
                foreach (var c in physicalColliders) if (c) Physics.IgnoreCollision(c, playerCol, true);
        }
    }

    private void StartCowardFlee()
    {
        if (isFleeing) return;
        isFleeing = true;

        isProvoked = true;

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        // NIE zatrzymuj flashCoroutine tutaj.
        // Hit flash musi mieć czas się pokazać.

        StartCoroutine(CowardFleeRoutine());
    }

    // ADD ▼ — ucieczka + raport po ucieczce
    private IEnumerator CowardFleeRoutine()
    {
        float t0 = Time.time;

        // pędź w stronę od gracza
        Vector3 dir = (transform.position - (player ? GetCurrentTargetPosition() : transform.position)).normalized;
        if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;

        // pierwszy cel ucieczki
        Vector3 target = transform.position + dir * Mathf.Max(fleeDistance, fleeFarDistance);
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            target = hit.position;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = Mathf.Max(agent.speed, 4.8f);
            agent.SetDestination(target);
        }

        // minimum czasu ucieczki + próba trzymania dystansu
        while (Time.time - t0 < fleeDuration)
        {
            if (agent != null && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < 0.8f)
            {
                // wybierz kolejny punkt jeszcze dalej
                Vector3 away = (transform.position - (player ? GetCurrentTargetPosition() : transform.position)).normalized;
                Vector3 t2 = transform.position + away * (fleeFarDistance * 0.6f + Random.Range(3f, 6f));
                if (NavMesh.SamplePosition(t2, out NavMeshHit h2, 3f, NavMesh.AllAreas))
                    agent.SetDestination(h2.position);
            }
            yield return null;
        }

        // po ucieczce – przekaż ostatnią znaną pozycję
        OnCowardReportedLastKnownPos?.Invoke(lastKnownAttackerPos);

        isFleeing = false;
        // NPC może wrócić do swojego patrolu/idle, ale interakcje/kolizje z graczem pozostają wyłączone
    }


    public void ReceivePanicFromWitness(Vector3 attackerPos)
    {
        lastKnownAttackerPos = attackerPos;
        DisableInteractionAndCollisionForever();
        StartCowardFlee();
    }

    // ADD ▼ — fighter idzie „sprawdzić” meldunek Cowarda (investigate, nie od razu pościg)
    private void HandleCowardReported(Vector3 lastKnownPos)
    {
        if (reactionType != NPCReactionType.Fighter || isDead) return;
        _defenseMode = true;                  // <— klucz
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

        // zakończ patrol/check — ale zostaw _defenseMode na chwilę lub wyzeruj jeśli chcesz:
        _defenseMode = false;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log($"DIE CALLED -> {name}");

        if (core != null)
        {
            core.ConfirmDeath(lastAttacker);
        }

        HideAllIcons();

        // powiadom świadków
        OnNPCDied?.Invoke(this, lastAttacker);

        // cache dropu zanim ruszymy broń
        InventoryItemInstance droppedInstance = null;
        GameObject pickupPrefab = null;

        if (ShouldDropWeapon() && equippedGun != null)
        {
            droppedInstance = equippedGun.GetInstance();
            pickupPrefab = GetPickupPrefabFromEquippedGun();
        }

        // zatrzymaj wszystkie aktywne coroutines/logikę
        if (flashCoroutine != null) { StopCoroutine(flashCoroutine); flashCoroutine = null; }
        if (attackCoroutine != null) { StopCoroutine(attackCoroutine); attackCoroutine = null; }

        CancelInvoke();
        StopAllCoroutines();

        inVictoryState = false;
        isProvoked = false;
        isFleeing = false;
        investigatingShot = false;

        HideAllIcons();

        // na śmierć wyłącz interakcyjne collidery
        if (interactionColliders != null)
        {
            foreach (var c in interactionColliders)
                if (c) c.enabled = false;
        }

        StartCoroutine(CoDieSequence(pickupPrefab, droppedInstance));
    }

    private IEnumerator CoDieSequence(GameObject pickupPrefab, InventoryItemInstance droppedInstance)
    {
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

        // KLUCZOWE:
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

            // Nie koloruj ikony Alert ani jej dzieci.
            if (alertIcon != null &&
                (r.transform == alertIcon.transform || r.transform.IsChildOf(alertIcon.transform)))
            {
                continue;
            }

            // Nie koloruj ikony Scared ani jej dzieci.
            if (scaredIcon != null &&
                (r.transform == scaredIcon.transform || r.transform.IsChildOf(scaredIcon.transform)))
            {
                continue;
            }

            // Nie koloruj żadnej broni z WeaponsList.
            if (weaponsListRoot != null && r.transform.IsChildOf(weaponsListRoot))
            {
                continue;
            }

            // Nie koloruj obiektów, które są częścią NPCGun.
            if (r.GetComponentInParent<NPCGun>(true) != null)
            {
                continue;
            }

            // Dodatkowy bezpiecznik, jeśli bronie mają layer Weapon.
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

    // ===== BROŃ =====
    private void AssignRandomWeapon()
    {
        if (!useWeaponSystem)
            return;

        if (availableWeapons == null || availableWeapons.Length == 0)
        {
            Debug.LogWarning($"[NPC] {name}: Brak dostępnych broni NPC.");
            return;
        }

        HideAllNpcWeapons();

        NPCGun gunComponent = availableWeapons[Random.Range(0, availableWeapons.Length)];

        if (gunComponent == null)
        {
            Debug.LogWarning($"[NPC] {name}: Wylosowana broń NPC jest null.");
            return;
        }

        equippedGun = gunComponent;

        GameObject weaponRoot = GetNpcWeaponRoot(equippedGun);

        if (weaponRoot != null)
            weaponRoot.SetActive(false);

        assignedWeaponName = equippedGun.name;

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

        Debug.Log($"[NPC] {name} pickupPrefab=null, gun='{gunName}'");
        return null;
    }

    // ===== PANIKA =====

    private void PropagateCowardPanic()
    {
        var nearby = Physics.OverlapSphere(transform.position, panicPropagationRadius);

        foreach (var col in nearby)
        {
            if (col == null) continue;

            NPCController npc = col.GetComponentInParent<NPCController>();
            if (npc == null) continue;
            if (npc == this || npc.isDead) continue;

            if (npc.reactionType == NPCReactionType.Coward)
            {
                npc.ReceivePanicFromWitness(player ? GetCurrentTargetPosition() : transform.position);
            }
        }
    }

    public void ReactToPanic()
    {
        if (isDead) return;
        // zachowaj wsteczną kompatybilność: teraz też twardo blokujemy interakcję + uciekamy
        DisableInteractionAndCollisionForever();
        StartCowardFlee();
        reactionEndTime = Time.time + reactionDuration;
        // FleeFromPlayer(); // niepotrzebne — StartCowardFlee() robi lepszą ucieczkę
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

        // bardzo lekki wyrzut
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
            // Backstab traktujemy jako bardzo duże obrażenia,
            // ale dalej pozwalamy NPCCore zdecydować, czy NPC może umrzeć.
            var result = core.TryTakeDamage(99999f, lastAttacker);

            if (result.blocked)
                return;

            currentHP = result.currentHP;

            if (result.preventedDeath)
            {
                currentHP = Mathf.Max(1f, currentHP);

                if (flashCoroutine != null)
                    StopCoroutine(flashCoroutine);

                flashCoroutine = StartCoroutine(FlashRedCoroutine(hitFlashDuration));

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

        Debug.Log($"BACKSTAB KILL EXECUTED -> {name}");

        currentHP = 0f;

        pendingBackstabFall = true;

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRedCoroutine(hitFlashDuration));

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
        // 1. HP / DAMAGE LOGIC przez NPCCore
        // =========================
        if (core != null)
        {
            var result = core.TryTakeDamage(damage, lastAttacker);

            if (result.blocked)
            {
                // NPC odporny, martwy albo damage = 0.
                return;
            }

            currentHP = result.currentHP;
            preventedDeath = result.preventedDeath;
            shouldDie = result.wouldDie;
        }
        else
        {
            // Fallback dla starych NPC bez NPCCore.
            currentHP -= Mathf.Max(0, damage);
            if (currentHP < 0f) currentHP = 0f;

            shouldDie = currentHP <= 0f;
        }

        // =========================
        // 2. HIT FEEDBACK
        // =========================
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(FlashRedCoroutine(hitFlashDuration));

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

    private void SilentBlackHuntTick()
    {
        if (isDead || player == null)
            return;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        HideAllIcons();
        HolsterWeapon(true);

        agent.isStopped = false;
        agent.speed = blackHuntSpeed;

        if (Time.time < _nextBlackHuntRepath)
            return;

        _nextBlackHuntRepath = Time.time + blackHuntRepathInterval;

        Vector3 targetPos = GetCurrentTargetPosition();

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(targetPos);
    }

    private GameObject GetNpcWeaponRoot(NPCGun gun)
    {
        if (gun == null)
            return null;

        // NPCGun jest na WeaponLogic, więc rootem broni jest parent:
        // AK97_NPC / Glock_NPC / M4A1_NPC / SPAS12_NPC
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
}
