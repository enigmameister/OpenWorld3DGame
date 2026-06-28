using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NPCWorldCoordinator : MonoBehaviour
{
    public static NPCWorldCoordinator Instance { get; private set; }

    public enum NPCLodState
    {
        Full,
        Simple,
        Sleeping
    }

    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Auto scan")]
    [SerializeField] private bool scanSceneOnStart = true;
    [SerializeField] private bool rescanScenePeriodically = true;
    [SerializeField] private float rescanInterval = 5f;

    [Header("LOD distances")]
    [SerializeField] private float fullDistance = 60f;
    [SerializeField] private float simpleDistance = 120f;
    [SerializeField] private float sleepDistance = 180f;

    [Header("Combat safety")]
    [Tooltip("Jeœli NPC jest sprowokowany, trzymamy go aktywnego z wiêkszej odleg³oœci.")]
    [SerializeField] private float provokedDistanceMultiplier = 1.75f;

    [Header("Global NPC Budget")]
    [SerializeField] private int globalMaxAliveNPCs = 80;
    [SerializeField] private int globalMaxAmbientNPCs = 60;
    [SerializeField] private int globalMaxCombatNPCs = 20;

    [Tooltip("Jeœli TRUE, Mission i StoryCritical nie wliczaj¹ siê do limitu Ambient.")]
    [SerializeField] private bool ignoreImportantNPCsForAmbientLimit = true;

    [Header("Tick budget")]
    [Tooltip("Co ile sekund koordynator sprawdza LOD. Nie rób tego co klatkê.")]
    [SerializeField] private float lodTickInterval = 0.35f;

    [Tooltip("Ilu NPC maksymalnie sprawdziæ w jednym ticku.")]
    [SerializeField] private int maxChecksPerTick = 20;

    [Header("Sleeping settings")]
    [SerializeField] private bool disableAnimatorInSleep = true;
    [SerializeField] private bool hideRenderersInSleep = false;
    [SerializeField] private bool disableReactiveInSimple = true;

    [Header("Ambient Despawn")]
    [SerializeField] private bool despawnFarAmbientNPCs = true;
    [SerializeField] private float ambientDespawnDistance = 260f;
    [SerializeField] private float ambientDespawnDelay = 20f;
    [SerializeField] private int maxDespawnChecksPerTick = 10;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool drawGizmos = true;

    private readonly List<NPCEntry> npcs = new();
    private readonly HashSet<GameObject> registeredRoots = new();
    private readonly Dictionary<NPCCore, NPCEntry> coreToEntry = new();

    private float lodTimer;
    private float rescanTimer;
    private int lodIndex;
    private int despawnIndex;
    public int RegisteredCount => npcs.Count;

    private class NPCEntry
    {
        public GameObject root;
        public Transform transform;

        public NPCCore core;
        public NPCController controller;
        public NPCMelee melee;
        public NPCReactive reactive;
        public Billboard billboard;

        public NavMeshAgent agent;
        public Animator[] animators;
        public Renderer[] renderers;

        public bool controllerDefaultEnabled;
        public bool meleeDefaultEnabled;
        public bool reactiveDefaultEnabled;
        public bool billboardDefaultEnabled;
        public bool agentDefaultEnabled;

        public bool[] animatorDefaultEnabled;
        public bool[] rendererDefaultEnabled;

        public NPCLodState lodState = NPCLodState.Full;
        public float farFromPlayerSince = -1f;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NPCWorldCoordinator] Duplicate found. Destroying this instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (player == null)
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                player = playerGo.transform;
        }
    }

    private void Start()
    {
        if (scanSceneOnStart)
            ScanSceneForNPCs();
    }

    private void Update()
    {
        if (player == null)
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                player = playerGo.transform;

            if (player == null)
                return;
        }

        CleanupNulls();

        if (rescanScenePeriodically)
        {
            rescanTimer += Time.deltaTime;
            if (rescanTimer >= rescanInterval)
            {
                rescanTimer = 0f;
                ScanSceneForNPCs();
            }
        }

        lodTimer += Time.deltaTime;
        if (lodTimer >= lodTickInterval)
        {
            lodTimer = 0f;
            RefreshLodSlice();

            if (despawnFarAmbientNPCs)
                RefreshAmbientDespawnSlice();
        }
    }

    public void RegisterNPC(GameObject npcRoot)
    {
        if (npcRoot == null) return;

        GameObject root = npcRoot;

        if (registeredRoots.Contains(root))
            return;

        NPCEntry entry = BuildEntry(root);
        if (entry == null)
            return;

        // Nie rejestruj martwych cia³ ponownie podczas okresowego skanowania sceny.
        if (entry.core != null && entry.core.IsDead)
            return;

        npcs.Add(entry);
        registeredRoots.Add(root);

        if (entry.core != null && !coreToEntry.ContainsKey(entry.core))
        {
            coreToEntry.Add(entry.core, entry);
            entry.core.Died += OnCoreDied;
        }

        ApplyLod(entry, NPCLodState.Full, force: true);

        if (debugLogs)
            Debug.Log($"[NPCWorldCoordinator] Registered NPC: {root.name}");
    }

    public void UnregisterNPC(GameObject npcRoot)
    {
        if (npcRoot == null) return;

        for (int i = npcs.Count - 1; i >= 0; i--)
        {
            NPCEntry entry = npcs[i];

            if (entry == null || entry.root == npcRoot)
            {
                UnsubscribeEntry(entry);

                if (entry != null && entry.root != null)
                    registeredRoots.Remove(entry.root);

                npcs.RemoveAt(i);
            }
        }

        if (lodIndex >= npcs.Count)
            lodIndex = 0;

        if (despawnIndex >= npcs.Count)
            despawnIndex = 0;
    }

    public bool CanSpawnAmbientNPC()
    {
        CleanupNulls();

        int aliveTotal = CountAliveNPCs();
        int aliveAmbient = CountAliveAmbientNPCs();

        if (aliveTotal >= globalMaxAliveNPCs)
            return false;

        if (aliveAmbient >= globalMaxAmbientNPCs)
            return false;

        return true;
    }

    public bool CanSpawnCombatNPC()
    {
        CleanupNulls();

        int aliveTotal = CountAliveNPCs();
        int aliveCombat = CountAliveCombatNPCs();

        if (aliveTotal >= globalMaxAliveNPCs)
            return false;

        if (aliveCombat >= globalMaxCombatNPCs)
            return false;

        return true;
    }

    public int CountAliveNPCs()
    {
        int count = 0;

        for (int i = 0; i < npcs.Count; i++)
        {
            NPCEntry entry = npcs[i];
            if (entry == null || entry.root == null) continue;

            if (IsDead(entry)) continue;

            count++;
        }

        return count;
    }

    public int CountAliveAmbientNPCs()
    {
        int count = 0;

        for (int i = 0; i < npcs.Count; i++)
        {
            NPCEntry entry = npcs[i];
            if (entry == null || entry.root == null) continue;

            if (IsDead(entry)) continue;

            if (entry.core == null)
            {
                // NPC bez core traktujemy jako ambient, ¿eby nie omija³ limitu.
                count++;
                continue;
            }

            if (entry.core.Importance == NPCCore.NPCImportance.Ambient)
                count++;
            else if (!ignoreImportantNPCsForAmbientLimit)
                count++;
        }

        return count;
    }

    public int CountAliveCombatNPCs()
    {
        int count = 0;

        for (int i = 0; i < npcs.Count; i++)
        {
            NPCEntry entry = npcs[i];
            if (entry == null || entry.root == null) continue;

            if (IsDead(entry)) continue;

            bool isCombat = false;

            if (entry.melee != null)
            {
                isCombat = true;
            }
            else if (entry.controller != null)
            {
                var type = entry.controller.GetReactionType();

                isCombat =
                    type == NPCController.NPCReactionType.Aggressive ||
                    type == NPCController.NPCReactionType.Fighter;
            }

            if (isCombat)
                count++;
        }

        return count;
    }

    private void OnCoreDied(NPCCore core, string attackerName)
    {
        if (core == null) return;

        if (!coreToEntry.TryGetValue(core, out NPCEntry entry))
            return;

        if (debugLogs && entry != null && entry.root != null)
            Debug.Log($"[NPCWorldCoordinator] NPC died, unregistering from LOD: {entry.root.name}");

        if (entry != null && entry.root != null)
            registeredRoots.Remove(entry.root);

        UnsubscribeEntry(entry);
        npcs.Remove(entry);

        if (lodIndex >= npcs.Count)
            lodIndex = 0;

        if (despawnIndex >= npcs.Count)
            despawnIndex = 0;
    }

    private void UnsubscribeEntry(NPCEntry entry)
    {
        if (entry == null) return;

        if (entry.core != null)
        {
            entry.core.Died -= OnCoreDied;
            coreToEntry.Remove(entry.core);
        }
    }

    public void ScanSceneForNPCs()
    {
        NPCController[] controllers = FindObjectsByType<NPCController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                RegisterNPC(controllers[i].gameObject);
        }

        NPCMelee[] melees = FindObjectsByType<NPCMelee>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < melees.Length; i++)
        {
            if (melees[i] != null)
                RegisterNPC(melees[i].gameObject);
        }
    }

    private NPCEntry BuildEntry(GameObject root)
    {
        if (root == null) return null;

        NPCController controller = root.GetComponentInChildren<NPCController>(true);
        NPCMelee melee = root.GetComponentInChildren<NPCMelee>(true);
        NPCCore core = root.GetComponentInChildren<NPCCore>(true);

        if (controller == null && melee == null)
            return null;

        NavMeshAgent agent = root.GetComponentInChildren<NavMeshAgent>(true);
        NPCReactive reactive = root.GetComponentInChildren<NPCReactive>(true);
        Billboard billboard = root.GetComponentInChildren<Billboard>(true);

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        NPCEntry entry = new NPCEntry
        {
            root = root,
            transform = root.transform,

            core = core,
            controller = controller,
            melee = melee,
            reactive = reactive,
            billboard = billboard,

            agent = agent,
            animators = animators,
            renderers = renderers,

            controllerDefaultEnabled = controller != null && controller.enabled,
            meleeDefaultEnabled = melee != null && melee.enabled,
            reactiveDefaultEnabled = reactive != null && reactive.enabled,
            billboardDefaultEnabled = billboard != null && billboard.enabled,
            agentDefaultEnabled = agent != null && agent.enabled,

            animatorDefaultEnabled = new bool[animators.Length],
            rendererDefaultEnabled = new bool[renderers.Length]
        };

        for (int i = 0; i < animators.Length; i++)
            entry.animatorDefaultEnabled[i] = animators[i] != null && animators[i].enabled;

        for (int i = 0; i < renderers.Length; i++)
            entry.rendererDefaultEnabled[i] = renderers[i] != null && renderers[i].enabled;

        return entry;
    }

    private void RefreshLodSlice()
    {
        if (npcs.Count == 0 || player == null)
            return;

        int checkedCount = 0;

        while (checkedCount < maxChecksPerTick && npcs.Count > 0)
        {
            if (lodIndex >= npcs.Count)
                lodIndex = 0;

            NPCEntry entry = npcs[lodIndex];

            if (entry == null || entry.root == null)
            {
                npcs.RemoveAt(lodIndex);
                checkedCount++;
                continue;
            }

            RefreshSingleNPC(entry);

            lodIndex++;
            checkedCount++;
        }
    }

    private void RefreshSingleNPC(NPCEntry entry)
    {
        if (entry == null || entry.root == null || player == null)
            return;

        bool isDead = IsDead(entry);
        if (isDead)
        {
            ApplyLod(entry, NPCLodState.Full);
            return;
        }

        Vector3 targetPos = NPCPlayerTargetUtility.GetTargetPosition(player);
        float dist = Vector3.Distance(entry.transform.position, targetPos);

        bool provoked = IsProvoked(entry);

        float fullDist = fullDistance;
        float simpleDist = simpleDistance;

        if (provoked)
        {
            fullDist *= provokedDistanceMultiplier;
            simpleDist *= provokedDistanceMultiplier;
        }

        NPCLodState targetState;

        if (dist <= fullDist)
            targetState = NPCLodState.Full;
        else if (dist <= simpleDist)
            targetState = NPCLodState.Simple;
        else
            targetState = NPCLodState.Sleeping;

        ApplyLod(entry, targetState);
    }

    private bool IsDead(NPCEntry entry)
    {
        if (entry.core != null) return entry.core.IsDead;

        if (entry.controller != null) return entry.controller.IsDead;

        if (entry.melee != null) return entry.melee.IsDead;

        return false;
    }

    private bool IsProvoked(NPCEntry entry)
    {
        if (entry.controller != null) return entry.controller.IsProvoked;

        if (entry.melee != null) return entry.melee.IsAggro;

        return false;
    }

    private void ApplyLod(NPCEntry entry, NPCLodState targetState, bool force = false)
    {
        if (entry == null || entry.root == null)
            return;

        if (!force && entry.lodState == targetState)
            return;

        entry.lodState = targetState;

        switch (targetState)
        {
            case NPCLodState.Full:
                ApplyFull(entry);
                break;

            case NPCLodState.Simple:
                ApplySimple(entry);
                break;

            case NPCLodState.Sleeping:
                ApplySleeping(entry);
                break;
        }

        if (debugLogs)
            Debug.Log($"[NPCWorldCoordinator] {entry.root.name} -> {targetState}");
    }

    private void ApplyFull(NPCEntry entry)
    {
        SetControllerEnabled(entry, true);
        SetMeleeEnabled(entry, true);
        SetReactiveEnabled(entry, true);
        SetBillboardEnabled(entry, true);

        RestoreAgent(entry);
        RestoreAnimators(entry);
        RestoreRenderers(entry);
    }

    private void ApplySimple(NPCEntry entry)
    {
        // Simple zostawia ruch/animacje, ale ogranicza interakcje.
        SetControllerEnabled(entry, true);
        SetMeleeEnabled(entry, true);

        if (disableReactiveInSimple)
            SetReactiveEnabled(entry, false);
        else
            SetReactiveEnabled(entry, true);

        SetBillboardEnabled(entry, true);

        RestoreAgent(entry);
        RestoreAnimators(entry);
        RestoreRenderers(entry);
    }

    private void ApplySleeping(NPCEntry entry)
    {
        SetControllerEnabled(entry, false);
        SetMeleeEnabled(entry, false);
        SetReactiveEnabled(entry, false);
        SetBillboardEnabled(entry, false);

        StopAgent(entry);

        if (disableAnimatorInSleep)
            SetAnimatorsEnabled(entry, false);
        else
            RestoreAnimators(entry);

        if (hideRenderersInSleep)
            SetRenderersEnabled(entry, false);
        else
            RestoreRenderers(entry);
    }

    private void SetControllerEnabled(NPCEntry entry, bool value)
    {
        if (entry.controller == null) return;
        entry.controller.enabled = value && entry.controllerDefaultEnabled;
    }

    private void SetMeleeEnabled(NPCEntry entry, bool value)
    {
        if (entry.melee == null) return;
        entry.melee.enabled = value && entry.meleeDefaultEnabled;
    }

    private void SetReactiveEnabled(NPCEntry entry, bool value)
    {
        if (entry.reactive == null) return;
        entry.reactive.enabled = value && entry.reactiveDefaultEnabled;
    }

    private void SetBillboardEnabled(NPCEntry entry, bool value)
    {
        if (entry.billboard == null) return;
        entry.billboard.enabled = value && entry.billboardDefaultEnabled;
    }

    private void RestoreAgent(NPCEntry entry)
    {
        if (entry.agent == null) return;

        if (!entry.agentDefaultEnabled)
            return;

        if (!entry.agent.enabled)
            entry.agent.enabled = true;

        if (entry.agent.enabled && entry.agent.isOnNavMesh)
            entry.agent.isStopped = false;
    }

    private void StopAgent(NPCEntry entry)
    {
        if (entry.agent == null) return;
        if (!entry.agent.enabled) return;

        if (entry.agent.isOnNavMesh)
        {
            entry.agent.isStopped = true;
            entry.agent.ResetPath();
        }
    }

    private void RestoreAnimators(NPCEntry entry)
    {
        if (entry.animators == null) return;

        for (int i = 0; i < entry.animators.Length; i++)
        {
            Animator anim = entry.animators[i];
            if (anim == null) continue;

            bool defaultEnabled = entry.animatorDefaultEnabled != null &&
                                  i < entry.animatorDefaultEnabled.Length &&
                                  entry.animatorDefaultEnabled[i];

            anim.enabled = defaultEnabled;
        }
    }

    private void SetAnimatorsEnabled(NPCEntry entry, bool value)
    {
        if (entry.animators == null) return;

        for (int i = 0; i < entry.animators.Length; i++)
        {
            if (entry.animators[i] != null)
                entry.animators[i].enabled = value;
        }
    }

    private void RestoreRenderers(NPCEntry entry)
    {
        if (entry.renderers == null) return;

        for (int i = 0; i < entry.renderers.Length; i++)
        {
            Renderer r = entry.renderers[i];
            if (r == null) continue;

            bool defaultEnabled = entry.rendererDefaultEnabled != null &&
                                  i < entry.rendererDefaultEnabled.Length &&
                                  entry.rendererDefaultEnabled[i];

            r.enabled = defaultEnabled;
        }
    }

    private void SetRenderersEnabled(NPCEntry entry, bool value)
    {
        if (entry.renderers == null) return;

        for (int i = 0; i < entry.renderers.Length; i++)
        {
            if (entry.renderers[i] != null)
                entry.renderers[i].enabled = value;
        }
    }

    private void CleanupNulls()
    {
        for (int i = npcs.Count - 1; i >= 0; i--)
        {
            NPCEntry entry = npcs[i];

            if (entry == null || entry.root == null)
            {
                UnsubscribeEntry(entry);

                if (entry != null && entry.root != null)
                    registeredRoots.Remove(entry.root);

                npcs.RemoveAt(i);
            }
        }

        if (lodIndex >= npcs.Count)
            lodIndex = 0;

        if (despawnIndex >= npcs.Count)
            despawnIndex = 0;
    }

    private void RefreshAmbientDespawnSlice()
    {
        if (player == null || npcs.Count == 0)
            return;

        int checkedCount = 0;

        while (checkedCount < maxDespawnChecksPerTick && npcs.Count > 0)
        {
            if (despawnIndex >= npcs.Count)
                despawnIndex = 0;

            NPCEntry entry = npcs[despawnIndex];

            if (entry == null || entry.root == null)
            {
                npcs.RemoveAt(despawnIndex);
                checkedCount++;
                continue;
            }

            CheckAmbientDespawn(entry);

            despawnIndex++;
            checkedCount++;
        }
    }

    private void CheckAmbientDespawn(NPCEntry entry)
    {
        if (entry == null || entry.root == null)
            return;

        if (entry.core == null)
            return;

        if (entry.core.IsDead)
            return;

        if (entry.core.Importance != NPCCore.NPCImportance.Ambient)
            return;

        if (IsProvoked(entry))
        {
            entry.farFromPlayerSince = -1f;
            return;
        }

        Vector3 targetPos = NPCPlayerTargetUtility.GetTargetPosition(player);
        float dist = Vector3.Distance(entry.transform.position, targetPos);

        if (dist < ambientDespawnDistance)
        {
            entry.farFromPlayerSince = -1f;
            return;
        }

        if (entry.farFromPlayerSince < 0f)
        {
            entry.farFromPlayerSince = Time.time;
            return;
        }

        if (Time.time - entry.farFromPlayerSince < ambientDespawnDelay)
            return;

        DespawnAmbientNPC(entry);
    }

    private void DespawnAmbientNPC(NPCEntry entry)
    {
        if (entry == null || entry.root == null)
            return;

        if (debugLogs)
            Debug.Log($"[NPCWorldCoordinator] Despawn ambient NPC: {entry.root.name}");

        GameObject root = entry.root;

        UnregisterNPC(root);
        Destroy(root);

        if (despawnIndex >= npcs.Count)
            despawnIndex = 0;
    }

    public string GetBudgetDebugText()
    {
        return $"NPC Budget: Alive={CountAliveNPCs()}/{globalMaxAliveNPCs}, " +
               $"Ambient={CountAliveAmbientNPCs()}/{globalMaxAmbientNPCs}, " +
               $"Combat={CountAliveCombatNPCs()}/{globalMaxCombatNPCs}";
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, fullDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, simpleDistance);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, sleepDistance);
    }
#endif
}