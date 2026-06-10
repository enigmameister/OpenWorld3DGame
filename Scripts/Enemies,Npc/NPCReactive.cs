using System.Linq;
using UnityEngine;

public class NPCReactive : MonoBehaviour
{
    [Header("Interakcja / Bark")]
    [SerializeField] private float interactRadius = 2.5f;

    [Tooltip("Jeśli TRUE: zwykły bark odpala się automatycznie po wejściu gracza w trigger NPC.")]
    [SerializeField] private bool barkOnTriggerEnter = true;

    [Tooltip("Jeśli TRUE: NPC specjalny używa klawisza E zamiast auto barku.")]
    [SerializeField] private bool useKeyInteraction = false;

    [Tooltip("Jeśli TRUE: NPC może ponawiać bark, gdy gracz stoi obok.")]
    [SerializeField] private bool repeatBarkWhileNearby = false;

    [SerializeField] private float barkCooldown = 3.0f;
    [SerializeField] private Vector2 repeatBarkIntervalRange = new Vector2(4f, 8f);

    [Header("Śledzenie po interakcji")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float lingerTime = 2f;
    [SerializeField] private float interactFaceDuration = 0.6f;

    [Header("Obrót (deg/s)")]
    [SerializeField] private float interactTurnSpeed = 720f;
    [SerializeField] private float idleTurnSpeed = 240f;
    [SerializeField] private float returnTurnSpeed = 180f;

    [Header("Kolory (interakcja)")]
    [SerializeField] private Color interactColor = Color.black;
    [SerializeField] private Transform bodyRoot;

    [Header("NPC Bark UI")]
    [SerializeField] private string npcDisplayName = "NPC";
    [TextArea][SerializeField] private string[] barkLines = { "Cześć.", "Co tam?", "Uważaj." };

    [Header("Blokady interakcji")]
    [SerializeField] private bool blockInteractionWhenProvoked = true;

    [Header("Wykrywanie mierzenia (Aggressive/Fighter)")]
    [SerializeField] private float aimAngleThreshold = 15f;

    [Header("Aiming – czułość")]
    [SerializeField] private bool requireADSForAggro = true;
    [SerializeField, Range(2f, 25f)] private float strictAimAngle = 8f;

    [Header("Natychmiastowa reakcja")]
    [SerializeField] private bool instantAggroOnAim = true;
    [SerializeField] private float aimMaxDistance = 50f;
    [SerializeField] private float quickDrawAggroWindow = 0.25f;
    [SerializeField] private float minAimHoldTime = 0.10f;

    [Header("LOS – warstwy, które BLOKUJĄ wzrok (świat)")]
    [SerializeField] private LayerMask losObstaclesMask = ~0;

    [Header("Reakcja świadka")]
    [SerializeField] private float witnessRadius = 25f;

    // runtime
    private Transform player;
    private Camera playerCam;
    private WeaponManager wm;
    private NPCController npcCtrl;
    private NpcBarkUI barkUI;

    private Renderer[] bodyRenderers;
    private MaterialPropertyBlock mpb;
    private Color defaultColor = Color.white;
    private Quaternion originalRotation;

    private float lastSeenTime = -999f;
    private float interactFaceUntil = -1f;
    private bool sessionActive = false;

    // aim tracking
    private bool lastHands = true;
    private int lastSlot = -1;
    private float lastSwitchTime = -999f;
    private float aimOnMeSince = -1f;

    // bark timing
    private float nextBarkTime = 0f;
    private float nextNearbyBarkTime = 0f;
    private bool playerInsideTrigger = false;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    private bool InteractPressedThisFrame =>
        PlayerInputHandler.Instance != null && PlayerInputHandler.Instance.InteractPressed;

    private void OnEnable()
    {
        NPCController.OnNPCDied += OnNpcDiedGlobal;
    }

    private void OnDisable()
    {
        NPCController.OnNPCDied -= OnNpcDiedGlobal;
    }

    private void Awake()
    {
        npcCtrl = GetComponent<NPCController>();
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        playerCam = Camera.main;
        wm = FindFirstObjectByType<WeaponManager>();
        barkUI = FindFirstObjectByType<NpcBarkUI>(FindObjectsInactive.Include);

        if (wm != null)
        {
            lastHands = wm.IsUsingHandsOnly();
            lastSlot = wm.GetCurrentWeaponIndex();
        }

        RefreshBodyRenderers();

        if (npcCtrl != null) defaultColor = npcCtrl.DefaultColor;
        else if (bodyRenderers.Length > 0 && bodyRenderers[0] != null)
        {
            var mat = bodyRenderers[0].sharedMaterial ?? bodyRenderers[0].material;
            if (mat != null)
            {
                if (mat.HasProperty(BaseColorID)) defaultColor = mat.GetColor(BaseColorID);
                else if (mat.HasProperty(ColorID)) defaultColor = mat.GetColor(ColorID);
                else defaultColor = mat.color;
            }
        }

        mpb = new MaterialPropertyBlock();
        originalRotation = transform.rotation;

        if (npcCtrl == null)
            ApplyBodyColor(defaultColor);
        else
            npcCtrl.RecomputeBaseColor();

        if (losObstaclesMask == 0)
            losObstaclesMask = SuggestObstacleMask();
    }

    private void Update()
    {
        if (DevConsole.IsOpen) return;
        if (player == null) return;

        if (npcCtrl != null && (npcCtrl.IsDead || npcCtrl.IsProvoked || npcCtrl.IsInteractionLocked || npcCtrl.IsScaredVisible))
            sessionActive = false;

        if (wm == null) wm = FindFirstObjectByType<WeaponManager>();
        if (wm != null)
        {
            bool handsNow = wm.IsUsingHandsOnly();
            int slotNow = wm.GetCurrentWeaponIndex();

            if (lastHands && !handsNow && slotNow >= 0 && slotNow <= 3)
                lastSwitchTime = Time.time;

            lastHands = handsNow;
            lastSlot = slotNow;
        }

        // reakcja na mierzenie
        if (npcCtrl != null &&
            !npcCtrl.IsDead &&
            !npcCtrl.IsProvoked &&
            ShouldAggroOnAim() &&
            PlayerIsAimingAtMe())
        {
            npcCtrl.ForceReactToAggression();
            sessionActive = false;
            return;
        }

        // NPC specjalny: interakcja po E
        if (CanInteract())
        {
            float distToPlayer = Vector3.Distance(player.position, transform.position);
            if (distToPlayer <= interactRadius && InteractPressedThisFrame && Time.time >= nextBarkTime)
            {
                StartInteraction();
                nextBarkTime = Time.time + barkCooldown;
                nextNearbyBarkTime = Time.time + Random.Range(repeatBarkIntervalRange.x, repeatBarkIntervalRange.y);
            }
        }

        // zwykły NPC: opcjonalne powtarzanie barku gdy gracz stoi obok
        if (!useKeyInteraction && repeatBarkWhileNearby && playerInsideTrigger && CanInteract())
        {
            if (Time.time >= nextNearbyBarkTime)
            {
                StartInteraction();
                nextBarkTime = Time.time + barkCooldown;
                nextNearbyBarkTime = Time.time + Random.Range(repeatBarkIntervalRange.x, repeatBarkIntervalRange.y);
            }
        }

        // sesja interakcji
        if (sessionActive)
        {
            float dist = Vector3.Distance(player.position, transform.position);
            if (dist <= detectionRadius) lastSeenTime = Time.time;

            bool burst = Time.time <= interactFaceUntil;
            bool linger = (Time.time - lastSeenTime) <= lingerTime;

            if (burst || linger)
            {
                RotateTowardsDeg(player.position, burst ? interactTurnSpeed : idleTurnSpeed);
            }
            else
            {
                ReturnToDefaultRotationDeg(returnTurnSpeed);
                ApplyBodyColor(defaultColor);
                sessionActive = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!barkOnTriggerEnter) return;
        if (!other.CompareTag("Player")) return;

        playerInsideTrigger = true;

        if (!CanAutoBarkNow()) return;

        StartInteraction();
        nextBarkTime = Time.time + barkCooldown;
        nextNearbyBarkTime = Time.time + Random.Range(repeatBarkIntervalRange.x, repeatBarkIntervalRange.y);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInsideTrigger = false;
    }

    // =========================
    // INTERAKCJA
    // =========================
    private bool CanInteract()
    {
        if (npcCtrl == null) return true;

        if (npcCtrl.IsDead) return false;
        if (npcCtrl.GetReactionType() == NPCController.NPCReactionType.Aggressive) return false;
        if (blockInteractionWhenProvoked && npcCtrl.IsProvoked) return false;
        if (npcCtrl.IsInteractionLocked) return false;
        if (npcCtrl.IsScaredVisible) return false;

        return true;
    }

    private bool CanAutoBarkNow()
    {
        if (!CanInteract()) return false;
        if (Time.time < nextBarkTime) return false;
        return true;
    }

    private void StartInteraction()
    {
        if (!CanInteract()) return;

        string line = GetRandomBark();

        sessionActive = true;
        interactFaceUntil = Time.time + interactFaceDuration;
        lastSeenTime = Time.time;

        ApplyBodyColor(interactColor);
        RotateTowardsDeg(player.position, interactTurnSpeed);

        if (barkUI == null)
            barkUI = FindFirstObjectByType<NpcBarkUI>(FindObjectsInactive.Include);

        if (barkUI != null)
            barkUI.ShowBark(
                string.IsNullOrWhiteSpace(npcDisplayName) ? gameObject.name : npcDisplayName,
                line,
                2.25f
            );
        else
            Debug.Log(line);
    }

    private string GetRandomBark()
    {
        if (barkLines == null || barkLines.Length == 0)
            return "Cześć.";

        return barkLines[Random.Range(0, barkLines.Length)];
    }

    // =========================
    // AGGRO ON AIM
    // =========================
    private bool ShouldAggroOnAim()
    {
        if (npcCtrl == null) return false;

        var type = npcCtrl.GetReactionType();
        return type == NPCController.NPCReactionType.Aggressive
            || type == NPCController.NPCReactionType.Fighter;
    }

    private bool PlayerIsAimingAtMe()
    {
        if (playerCam == null || wm == null) return false;

        if (wm.IsUsingHandsOnly())
        {
            aimOnMeSince = -1f;
            return false;
        }

        int slot = wm.GetCurrentWeaponIndex();
        if (slot < 0 || slot > 3)
        {
            aimOnMeSince = -1f;
            return false;
        }

        bool adsHeld = PlayerInputHandler.Instance?.FireAltHeld ?? false;
        bool fireHeld = PlayerInputHandler.Instance?.FireHeld ?? false;

        bool isFighter = npcCtrl != null && npcCtrl.GetReactionType() == NPCController.NPCReactionType.Fighter;
        bool requireAdsNow = !isFighter && requireADSForAggro;

        bool scoped = false;
        if (slot == 1 || slot == 2)
        {
            var guns = player != null ? player.GetComponentsInChildren<Gun>(true) : null;
            var activeGun = guns?.FirstOrDefault(g => g && g.gameObject.activeInHierarchy && !g.isControlledByNPC);
            scoped = activeGun != null && activeGun.IsScoped();
        }

        bool aimingInput = slot switch
        {
            0 => adsHeld,
            1 => (adsHeld || scoped),
            2 => (adsHeld || scoped),
            3 => (fireHeld || adsHeld),
            _ => false
        };

        bool inQuickDrawWindow = Time.time - lastSwitchTime <= quickDrawAggroWindow;

        if (!isFighter)
        {
            if (requireAdsNow && !aimingInput && !inQuickDrawWindow)
            {
                aimOnMeSince = -1f;
                return false;
            }
        }

        Vector3 camPos = playerCam.transform.position;
        Vector3 aimPoint = transform.position + Vector3.up * 1.4f;
        Vector3 toTarget = (aimPoint - camPos).normalized;

        float angle = Vector3.Angle(playerCam.transform.forward, toTarget);
        float angleGate = Mathf.Min(aimAngleThreshold, strictAimAngle);

        if (angle > angleGate)
        {
            aimOnMeSince = -1f;
            return false;
        }

        if (Physics.Raycast(camPos, playerCam.transform.forward, out RaycastHit hit, aimMaxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(transform))
            {
                aimOnMeSince = -1f;
                return false;
            }
        }
        else
        {
            aimOnMeSince = -1f;
            return false;
        }

        if (!HasLineOfSight(aimPoint))
        {
            aimOnMeSince = -1f;
            return false;
        }

        if (instantAggroOnAim)
            return true;

        if (aimOnMeSince < 0f)
            aimOnMeSince = Time.time;

        return (Time.time - aimOnMeSince) >= minAimHoldTime;
    }

    private bool HasLineOfSight(Vector3 targetPoint)
    {
        if (playerCam == null) return false;

        Vector3 origin = playerCam.transform.position;
        Vector3 dir = targetPoint - origin;
        float dist = dir.magnitude;

        if (dist <= 0.001f) return false;

        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, losObstaclesMask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    // =========================
    // ŚWIADEK ŚMIERCI
    // =========================
    private void OnNpcDiedGlobal(NPCController deadNpc, string attackerName)
    {
        if (deadNpc == null || deadNpc == npcCtrl) return;
        if (npcCtrl == null || npcCtrl.IsDead) return;

        float dist = Vector3.Distance(transform.position, deadNpc.transform.position);
        if (dist > witnessRadius) return;

        if (npcCtrl.GetReactionType() == NPCController.NPCReactionType.Coward)
        {
            sessionActive = false;
            return;
        }

        Vector3 witnessEye = transform.position + Vector3.up * 1.6f;
        Vector3 eventPoint = deadNpc.transform.position + Vector3.up * 1.0f;
        Vector3 dir = eventPoint - witnessEye;
        float distToEvent = dir.magnitude;

        if (distToEvent > 0.01f)
        {
            if (!Physics.Raycast(witnessEye, dir.normalized, distToEvent, losObstaclesMask, QueryTriggerInteraction.Ignore))
            {
                npcCtrl.ForceReactToAggression();
                sessionActive = false;
            }
        }
    }

    // =========================
    // WIZUAL / OBRÓT
    // =========================
    private void RefreshBodyRenderers()
    {
        var all = GetComponentsInChildren<Renderer>(true);

        if (bodyRoot != null)
            bodyRenderers = all.Where(r => r != null && r.transform.IsChildOf(bodyRoot)).ToArray();
        else
            bodyRenderers = all.Where(r => r != null).ToArray();
    }

    private void ApplyBodyColor(Color c)
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        if (bodyRenderers == null || bodyRenderers.Length == 0) return;

        foreach (var r in bodyRenderers)
        {
            if (!r) continue;

            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorID, c);
            mpb.SetColor(ColorID, c);
            r.SetPropertyBlock(mpb);

            var mat = Application.isPlaying ? r.material : r.sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty(BaseColorID)) mat.SetColor(BaseColorID, c);
                else if (mat.HasProperty(ColorID)) mat.SetColor(ColorID, c);
                else mat.color = c;
            }
        }
    }

    private void RotateTowardsDeg(Vector3 worldPos, float speedDeg)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, speedDeg * Time.deltaTime);
    }

    private void ReturnToDefaultRotationDeg(float speedDeg)
    {
        transform.rotation = Quaternion.RotateTowards(transform.rotation, originalRotation, speedDeg * Time.deltaTime);
    }

    private LayerMask SuggestObstacleMask()
    {
        int mask = LayerMask.GetMask("Default", "Obstacle", "Car");
        return mask == 0 ? ~0 : mask;
    }
}