using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCMelee : MonoBehaviour, IDamageable
{
    public enum MeleeType { MeleeOne = 1, MeleeTwo = 2, MeleeThree = 3 }

    [Header("Typ combosa")]
    public MeleeType meleeType = MeleeType.MeleeOne;

    [Header("WIZUAL – obrót mesh'a")]
    public Transform visualRoot;          // <- PRZECIĄGNIJ tu 'Body' z hierarchii
    [Tooltip("Jeśli model jest odwrócony, wpisz 180.")]
    public float visualYawOffset = 0f;    // zwykle 0, dla „chodzi tyłem” ustaw 180

    [Header("Detekcja (tylko przód)")]
    public float viewDistance = 12f;
    [Range(10f, 180f)] public float viewAngle = 90f;
    public LayerMask obstaclesMask = ~0;
    public LayerMask playerMask;

    [Header("Walka wręcz")]
    public float attackRange = 1.7f;
    public int damagePerHit = 12;
    public float comboStepInterval = 0.35f;
    public float comboCooldown = 0.9f;

    [Header("Ruch")]
    public float chaseSpeed = 4.2f;
    public float patrolSpeed = 2.2f;
    public float repathRate = 0.25f;
    public float stoppingDistance = 1.0f;

    [Header("Patrol (idle)")]
    public float patrolRadius = 8f;
    public float patrolIntervalMin = 4f;
    public float patrolIntervalMax = 8f;

    [Header("HP / Kolor")]
    public int maxHP = 80;
    public Renderer[] bodyRenderers;
    public float hitFlashDuration = 0.25f;

    [Header("Hit FX (opcjonalnie)")]
    public GameObject bloodFxPrefab;
    public float bloodFxScale = 1f;
    public float bloodFxLifetime = 2f;
    public AudioClip hurtSfx;
    public AudioSource audioSource;

    [Header("Dystansowanie (HL-style)")]
    public float holdDistance = 2.3f;
    public float strafeSpeed = 1.2f;
    public float backoffSpeed = 3.5f;
    public float holdJitter = 0.2f;

    [Header("Alert visibility")]
    [SerializeField] private float alertForgetDistance = 28f;
    [SerializeField] private float alertLoseSightDelay = 3f;
    [SerializeField] private float alertRefreshRate = 0.2f;

    private float _lastAlertSeenTime = -999f;
    private float _nextAlertCheckTime = 0f;

    // runtime
    private Rigidbody _rootRb;
    private Collider _rootCol;
    private NPCCore core;

    private int _hp;
    private Transform _player;
    private PlayerStats _playerStats;
    private NavMeshAgent _agent;
    private bool _aggro;
    private bool _isDead;
    private float _nextRepath;
    private bool _inCombo;
    private Color _baseColor = Color.white;
    private MaterialPropertyBlock _mpb;
    private Coroutine _flashCo;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    [Header("Słuch (strzał w pobliżu)")]
    public float shotHearRadius = 10f;
    public float shotLOSProbeHeight = 1.4f;
    private bool _investigatingShot = false;
    private Vector3 _shotInvestigatePoint;
    private float _investigateUntil = 0f;

    [Header("Reakcja na strzał w pobliżu")]
    [SerializeField] private float reactShotMaxDistance = 25f;
    [SerializeField] private float nearMissThreshold = 2.2f;
    [SerializeField] private LayerMask losMask = ~0;
    [SerializeField] private GameObject alertIcon;

    [Header("Szał po trafieniu")]
    public float enragedChaseSpeed = 6.0f;
    public float enragedStrafeSpeed = 1.8f;
    public float enragedBackoffSpeed = 4.5f;
    private bool _enraged = false;

    public bool IsDead => _isDead;
    public bool IsAggro => _aggro;

    public static event System.Action<NPCMelee, string> OnMeleeNPCDied;

    private void Awake()
    {
        core = GetComponent<NPCCore>();

        _agent = GetComponent<NavMeshAgent>();
        _rootRb = GetComponent<Rigidbody>();
        _rootCol = GetComponent<Collider>();

        if (_agent != null)
        {
            _agent.updateRotation = false;           // <- rotujemy SAMI wizualem
            _agent.stoppingDistance = stoppingDistance;

            // jeśli start poza NavMesh – spróbuj skleić
            if (!_agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit nh, 2.5f, NavMesh.AllAreas))
                _agent.Warp(nh.position);
        }

        // trzymany dystans < attackRange
        float margin = 0.25f;
        if (holdDistance >= attackRange - margin)
            holdDistance = Mathf.Max(0.1f, attackRange - margin);

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo)
        {
            _player = playerGo.transform;
            _playerStats = playerGo.GetComponent<PlayerStats>();
        }

        _hp = maxHP;

        _mpb = new MaterialPropertyBlock();
        ApplyBodyColor(_baseColor);

        // jeśli nie ustawiono, spróbuj zgadnąć „Body”
        if (visualRoot == null)
        {
            var body = transform.Find("Body");
            if (body) visualRoot = body;
            else visualRoot = transform; // awaryjnie obracaj cały root
        }
    }
    private void Start()
    {
        HideAlert();
        Invoke(nameof(PatrolPickNewPoint), Random.Range(0.5f, 1.5f));
    }

    private void OnValidate()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        if (_agent != null) _agent.stoppingDistance = stoppingDistance;

        float margin = 0.25f;
        if (holdDistance >= attackRange - margin)
            holdDistance = Mathf.Max(0.1f, attackRange - margin);

        float maxStop = Mathf.Max(0f, attackRange - 0.2f);
        if (stoppingDistance > maxStop) stoppingDistance = maxStop;
    }

    private void Update()
    {
        if (_isDead || _player == null) return;
        UpdateAlertVisibility();

        // ---- IDLE / PATROL ----
        if (!_aggro && _agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.speed = patrolSpeed;

            // Obracaj MESH w kierunku ruchu
            if (_agent.hasPath && _agent.desiredVelocity.sqrMagnitude > 0.01f)
            {
                FaceVisualToDirection(_agent.desiredVelocity);
            }
            else
            {
                // gdy stoi i gracz blisko – odwróć się do gracza
                Vector3 targetPos = NPCPlayerTargetUtility.GetTargetPosition(_player);
                float d = Vector3.Distance(transform.position, targetPos);
                if (d <= 3.0f) FaceVisualToDirection(targetPos - transform.position);
            }
        }

        // ---- WALKA / POŚCIG ----
        if (_aggro)
        {
            Vector3 targetPos = NPCPlayerTargetUtility.GetTargetPosition(_player);
            Vector3 toPlayer = targetPos - transform.position;

            if (Vector3.Distance(transform.position, targetPos) <= alertForgetDistance)
                MarkPlayerAsSeenForAlert();

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                _agent.speed = _enraged ? Mathf.Max(chaseSpeed, enragedChaseSpeed) : chaseSpeed;


            float dist = toPlayer.magnitude;
            Vector3 dir = (dist > 0.001f) ? toPlayer / dist : transform.forward;

            // Face wizual do gracza non-stop w walce
            FaceVisualToDirection(dir);

            // docelowy punkt na pierścieniu
            float jitter = Random.Range(-holdJitter, holdJitter);
            float targetRadius = Mathf.Max(0.1f, holdDistance + jitter);
            Vector3 ringPoint = targetPos - dir * targetRadius;
            if (NavMesh.SamplePosition(ringPoint, out NavMeshHit nh, 1.0f, NavMesh.AllAreas))
                ringPoint = nh.position;

            if (dist <= attackRange)
            {
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.ResetPath();
                }
            }
            else
            {
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                {
                    if (dist < targetRadius * 0.9f)
                    {
                        _agent.isStopped = false;
                        _agent.Move(-dir * backoffSpeed * Time.deltaTime);
                    }
                    else
                    {
                        if (Mathf.Abs(dist - targetRadius) < 0.3f)
                        {
                            _agent.isStopped = false;
                            Vector3 tangent = Vector3.Cross(Vector3.up, dir).normalized;
                            _agent.Move(tangent * strafeSpeed * Time.deltaTime);
                        }
                        else if (Time.time >= _nextRepath)
                        {
                            _nextRepath = Time.time + repathRate;
                            _agent.isStopped = false;
                            _agent.SetDestination(ringPoint);
                        }
                    }
                }
            }

            if (!_inCombo && dist <= attackRange + 0.05f && _playerStats != null && !_playerStats.IsDead)
                StartCoroutine(ComboRoutine());
        }

        if (_aggro && _investigatingShot && Time.time < _investigateUntil)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh && !_agent.pathPending && _agent.remainingDistance <= 0.9f)
            {
                FaceVisualToDirection(_shotInvestigatePoint - transform.position);
                _investigatingShot = false;
            }
        }
    }

    // ====== ROTACJA WIZUALU ======
    private void FaceVisualToDirection(Vector3 dirWorld)
    {
        if (visualRoot == null) return;
        dirWorld.y = 0f;
        if (dirWorld.sqrMagnitude < 1e-6f) return;

        var look = Quaternion.LookRotation(dirWorld.normalized);
        if (visualYawOffset != 0f)
            look *= Quaternion.Euler(0f, visualYawOffset, 0f);

        visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, look, Time.deltaTime * 10f);
    }

    // ---- PATROL ----
    private void PatrolPickNewPoint()
    {
        if (_isDead || _aggro || _agent == null || !_agent.enabled || !_agent.isOnNavMesh)
        {
            Invoke(nameof(PatrolPickNewPoint), Random.Range(patrolIntervalMin, patrolIntervalMax));
            return;
        }

        Vector3 rnd = Random.insideUnitSphere * patrolRadius; rnd.y = 0f;
        Vector3 target = transform.position + rnd;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            _agent.isStopped = false;
            _agent.SetDestination(hit.position);
        }

        Invoke(nameof(PatrolPickNewPoint), Random.Range(patrolIntervalMin, patrolIntervalMax));
    }

    private IEnumerator ComboRoutine()
    {
        _inCombo = true;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }

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

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = false;

        _inCombo = false;
    }

    private void TryApplyMeleeDamageOnce()
    {
        if (_playerStats == null || _playerStats.IsDead) return;

        Vector3 targetPos = NPCPlayerTargetUtility.GetTargetPosition(_player);
        Vector3 to = targetPos - transform.position;

        if (to.magnitude > attackRange + 0.25f) return;

        // Na razie dalej bije PlayerStats.
        // Później można tu zrobić osobną logikę: jeśli gracz siedzi w aucie, melee bije pojazd.
        _playerStats.TakeDamage(damagePerHit, gameObject.name);
        DamageIndicatorUI.Instance?.TriggerFromWorld(transform.position, damagePerHit);
    }

    private void EnableRagdollFall()
    {
        RagdollFallUtility.Enable(transform, ref _rootRb, ref _rootCol, false);
    }

    public void TakeDamage(int damage, string attackerName)
    {
        if (_isDead) return;

        bool preventedDeath = false;
        bool shouldDie = false;

        // =========================
        // 1. HP / DAMAGE LOGIC
        // =========================
        if (core != null)
        {
            var result = core.TryTakeDamage(damage, attackerName);

            if (result.blocked)
            {
                // NPC odporny albo damage = 0.
                return;
            }

            _hp = Mathf.RoundToInt(result.currentHP);

            preventedDeath = result.preventedDeath;
            shouldDie = result.wouldDie;
        }
        else
        {
            _hp -= Mathf.Max(0, damage);
            shouldDie = _hp <= 0;
        }

        // =========================
        // 2. HIT FEEDBACK
        // =========================
        if (_flashCo != null)
            StopCoroutine(_flashCo);

        _flashCo = StartCoroutine(FlashRed(hitFlashDuration));

        if (!_aggro)
        {
            EnterAggro();
            MarkPlayerAsSeenForAlert();

            var reactive = GetComponent<NPCReactive>();
            if (reactive != null)
                reactive.enabled = false;
        }
        else
        {
            MarkPlayerAsSeenForAlert();
        }

        _enraged = true;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
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

        // =========================
        // 3. PREVENT DEATH
        // =========================
        if (preventedDeath)
        {
            _hp = Mathf.Max(1, _hp);

            // NPC przeżywa, ale dalej reaguje jak trafiony.
            return;
        }

        // =========================
        // 4. DEATH
        // =========================
        if (shouldDie || _hp <= 0)
        {
            _hp = 0;

            // Tak samo jak w NPCController:
            // przy natychmiastowej śmierci od eksplozji flash coroutine może nie zdążyć.
            ApplyBodyColor(Color.red);

            _isDead = true;

            if (core != null)
                core.ConfirmDeath(attackerName);

            OnMeleeNPCDied?.Invoke(this, attackerName);

            HideAlert();

            var anim = GetComponentInChildren<Animator>();

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
                _agent.enabled = false;
            }

            if (anim)
                anim.enabled = false;

            var reactive = GetComponent<NPCReactive>();
            if (reactive)
                reactive.enabled = false;

            var billboard = GetComponent<Billboard>();
            if (billboard)
                billboard.enabled = false;

            EnableRagdollFall();

            StopAllCoroutines();
            StartCoroutine(Despawn(12f));
        }
    }

    private void OnEnable() { Gun.OnPlayerShot += OnPlayerShotHeard; }
    private void OnDisable() { Gun.OnPlayerShot -= OnPlayerShotHeard; }

    private void OnPlayerShotHeard(Vector3 shotOrigin, Vector3 shotDir, Vector3 impactPoint)
    {
        if (_isDead || _player == null || CheatState.Alliance) return;

        float dOrigin = Vector3.Distance(transform.position, shotOrigin);
        float dImpact = Vector3.Distance(transform.position, impactPoint);
        float dMin = Mathf.Min(dOrigin, dImpact);
        if (dMin > reactShotMaxDistance) return;

        float miss = DistancePointToRay(transform.position + Vector3.up * 1.2f, shotOrigin, shotDir.normalized);
        float distToImpact = Vector3.Distance(transform.position, impactPoint);
        bool nearMiss = miss <= nearMissThreshold;
        bool closeImpact = distToImpact <= shotHearRadius;
        if (!nearMiss && !closeImpact) return;

        Vector3 eye = transform.position + Vector3.up * shotLOSProbeHeight;
        Vector3 toCheck = (nearMiss ? (shotOrigin - eye) : (impactPoint - eye));
        var maskNoNPC = losMask & ~LayerMask.GetMask("NPC");
        if (Physics.Raycast(eye, toCheck.normalized, out RaycastHit block, toCheck.magnitude, maskNoNPC, QueryTriggerInteraction.Ignore))
        {
            if (!block.collider.transform.IsChildOf(transform)) return;
        }

        FaceVisualToDirection((nearMiss ? shotOrigin : impactPoint) - transform.position);
    }

    private static float DistancePointToRay(Vector3 point, Vector3 rayOrigin, Vector3 rayDirNormalized)
    {
        Vector3 toPoint = point - rayOrigin;
        float t = Vector3.Dot(toPoint, rayDirNormalized);
        if (t <= 0f) return toPoint.magnitude;
        Vector3 proj = rayOrigin + rayDirNormalized * t;
        return Vector3.Distance(point, proj);
    }

    private IEnumerator FlashRed(float duration)
    {
        ApplyBodyColor(Color.red);
        yield return new WaitForSeconds(duration);
        if (!_isDead) ApplyBodyColor(_baseColor);
    }

    private IEnumerator Despawn(float sec)
    {
        yield return new WaitForSeconds(sec);
        Destroy(gameObject);
    }

    private void ApplyBodyColor(Color c)
    {
        if (bodyRenderers == null || bodyRenderers.Length == 0) return;

        foreach (var r in bodyRenderers)
        {
            if (!r) continue;

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorID, c);
            _mpb.SetColor(ColorID, c);
            r.SetPropertyBlock(_mpb);

            if (Application.isPlaying)
            {
                var mat = r.material;
                if (mat != null)
                {
                    if (mat.HasProperty(BaseColorID)) mat.SetColor(BaseColorID, c);
                    else if (mat.HasProperty(ColorID)) mat.SetColor(ColorID, c);
                    else mat.color = c;
                }
            }
        }
    }
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

    private void MarkPlayerAsSeenForAlert()
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
        {
            MarkPlayerAsSeenForAlert();
        }

        if (Time.time - _lastAlertSeenTime > alertLoseSightDelay)
        {
            HideAlert();
        }
        else
        {
            ShowAlert();
        }
    }

    private bool CanSeeTargetForAlert(Vector3 targetPos)
    {
        Vector3 eye = transform.position + Vector3.up * shotLOSProbeHeight;
        Vector3 to = targetPos + Vector3.up * 1.2f - eye;

        float dist = to.magnitude;
        if (dist > alertForgetDistance)
            return false;

        Vector3 flatTo = to;
        flatTo.y = 0f;

        if (flatTo.sqrMagnitude < 0.001f)
            return true;

        Vector3 forward = visualRoot != null ? visualRoot.forward : transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        float angle = Vector3.Angle(forward.normalized, flatTo.normalized);
        if (angle > viewAngle * 0.5f)
            return false;

        LayerMask maskNoNPC = losMask & ~LayerMask.GetMask("NPC");

        if (Physics.Raycast(eye, to.normalized, out RaycastHit hit, dist, maskNoNPC, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(transform))
                return false;
        }

        return true;
    }

    private void EnterAggro()
    {
        if (_aggro) return;

        _aggro = true;
        MarkPlayerAsSeenForAlert();

        var reactive = GetComponent<NPCReactive>();
        if (reactive != null)
            reactive.enabled = false;
    }

    public void ApplyProfile(NPCProfile profile)
    {
        if (profile == null) return;
        if (!profile.useMelee) return;

        maxHP = Mathf.Max(1, profile.meleeMaxHP);
        _hp = maxHP;

        damagePerHit = Mathf.Max(1, profile.meleeDamagePerHit);
        chaseSpeed = Mathf.Max(0.1f, profile.meleeChaseSpeed);
        enragedChaseSpeed = Mathf.Max(chaseSpeed, profile.meleeEnragedChaseSpeed);
    }
}