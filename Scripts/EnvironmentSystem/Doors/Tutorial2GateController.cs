using UnityEngine;

public class Tutorial2GateController : MonoBehaviour
{
    [Header("Ruch bramy")]
    [Tooltip("Transform tej bramy (ten, który ma siê poruszaæ).")]
    public Transform gate;                // np. rodzic krat
    [Tooltip("Kierunek ruchu (lokalny). Np. (0,1,0) do góry.")]
    public Vector3 localMoveDir = Vector3.up;
    [Tooltip("Maksymalny skok bramy w metrach (przy 100% otwarcia).")]
    public float openDistance = 2.0f;

    [Header("Szybkoœci")]
    [Tooltip("m/s gdy zawór trzymany (otwieranie)")]
    public float openSpeed = 1.5f;
    [Tooltip("m/s gdy E puszczone (opadanie)")]
    public float closeSpeed = 2.5f;


    [Header("Crush / obra¿enia przy opadaniu")]
    public bool crushEnabled = true;
    public int crushDamage = 200;                      // ile zadaæ (daj 999, jeœli ma zawsze zabijaæ)
    public LayerMask crushMask = 0;                    // ustaw np. na warstwê Player
    [Tooltip("Wysokoœæ 'plastra' przy dolnej krawêdzi bramy, którym wykrywamy trafienie.")]
    public float bottomSliceHeight = 0.08f;
    [Tooltip("Margines na boki (w metrach) wzglêdem collidera bramy.")]
    public float slicePadding = 0.02f;
    [Tooltip("Minimalny spadek (m/s) aby zadaæ obra¿enia – 0 = zawsze, gdy opada.")]
    public float minDropSpeedToDamage = 0f;
    [Tooltip("Ile sekund przerwy po trafieniu, by nie wielokrotnie biæ w tej samej klatce.")]
    public float crushCooldown = 0.25f;

    // runtime
    private float _progress;
    private float _targetWhileHeld;
    private bool _isHeld;
    private Vector3 _startLocalPos;

    // ---- crush runtime ----
    private Collider _gateCol;
    private Vector3 _prevWorldPos;
    private float _cooldownT;

    void Awake()
    {
        if (!gate) gate = transform;
        _startLocalPos = gate.localPosition;
        _progress = 0f;
        ApplyPosition();

        _gateCol = gate.GetComponentInChildren<Collider>();
        _prevWorldPos = gate.position;
    }

    /// Wywo³uje zawór: ile „odkrêcono” (0..1). Trzymanie E wywo³uje to co klatkê.
    public void DriveWhileHeld(float valve01)
    {
        _isHeld = true;
        _targetWhileHeld = Mathf.Clamp01(valve01);
    }

    /// Wo³aj gdy E zosta³o puszczone.
    public void Release() { _isHeld = false; }

    void Update()
    {
        // ====== ruch bramy ======
        if (_isHeld)
        {
            // idealny sync z zaworem:
            _progress = _targetWhileHeld;   // 0..1 dok³adnie jak zawór
        }
        else
        {
            float currentMeters = _progress * openDistance;
            currentMeters = Mathf.MoveTowards(currentMeters, 0f, closeSpeed * Time.deltaTime);
            _progress = (openDistance > 0f) ? currentMeters / openDistance : 0f;
        }

        // zapamiêtaj poprzedni¹ pozycjê
        Vector3 before = gate.position;

        ApplyPosition(); // ustawia gate.localPosition

        // ====== logika zgniatania ======
        if (!crushEnabled) { _prevWorldPos = gate.position; return; }

        if (_cooldownT > 0f) _cooldownT -= Time.deltaTime;

        Vector3 after = gate.position;
        float dy = (after.y - before.y) / Mathf.Max(Time.deltaTime, 1e-6f); // m/s

        bool isDropping = !_isHeld && (after.y < before.y - 1e-5f);
        bool fastEnough = Mathf.Abs(minDropSpeedToDamage) <= 1e-4f || (-dy >= minDropSpeedToDamage);

        if (isDropping && fastEnough && _gateCol != null && _cooldownT <= 0f)
        {
            TryCrush();
        }

        _prevWorldPos = after;
    }

    private void ApplyPosition()
    {
        Vector3 dir = gate.transform.TransformDirection(localMoveDir.normalized);
        Vector3 localDir = gate.parent ? gate.parent.InverseTransformDirection(dir) : localMoveDir.normalized;
        gate.localPosition = _startLocalPos + localDir * (openDistance * _progress);
    }

    private void TryCrush()
    {
        // bierzemy world-bounds collidera bramy
        Bounds b = _gateCol.bounds;

        // cienki "plaster" przy spodzie bramy
        Vector3 center = new Vector3(b.center.x, b.min.y + bottomSliceHeight * 0.5f, b.center.z);
        Vector3 half = new Vector3(b.extents.x + slicePadding,
                                     bottomSliceHeight * 0.5f,
                                     b.extents.z + slicePadding);

        // k¹t obrotu pud³a – weŸ orientacjê bramy (dla obiektów obróconych)
        Quaternion rot = gate.rotation;

        // szukamy obiektów do zranienia
        var hits = Physics.OverlapBox(center, half, rot, crushMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        // bij tylko raz na „zetkniêcie” (cooldown)
        _cooldownT = crushCooldown;

        foreach (var h in hits)
        {
            // znajdŸ IDamageable na trafionym lub jego rodzicu
            var dmg = h.GetComponentInParent<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(crushDamage, "Gate");
            }
        }
    }

    /// Mo¿esz podejrzeæ progres (np. do UI)
    public float GetProgress01() => _progress;
}