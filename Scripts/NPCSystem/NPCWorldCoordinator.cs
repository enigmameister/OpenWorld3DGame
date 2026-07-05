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

    [Header("Auto Scan")]
    [SerializeField] private bool scanSceneOnStart = true;
    [SerializeField] private bool rescanScenePeriodically = true;
    [SerializeField] private float rescanInterval = 5f;

    [Header("LOD Distances")]
    [SerializeField] private float fullDistance = 60f;
    [SerializeField] private float simpleDistance = 120f;
    [SerializeField] private float sleepDistance = 180f;

    [Header("Combat Safety")]
    [Tooltip("If an NPC is provoked, keep it active from a longer distance.")]
    [SerializeField] private float provokedDistanceMultiplier = 1.75f;

    [Header("Global NPC Budget")]
    [SerializeField] private int globalMaxAliveNPCs = 80;
    [SerializeField] private int globalMaxAmbientNPCs = 60;
    [SerializeField] private int globalMaxCombatNPCs = 20;

    [Tooltip("If TRUE, Mission and StoryCritical NPCs do not count toward the ambient limit.")]
    [SerializeField] private bool ignoreImportantNPCsForAmbientLimit = true;

    [Header("Tick Budget")]
    [Tooltip("How often the coordinator checks LOD. Do not run this every frame.")]
    [SerializeField] private float lodTickInterval = 0.35f;

    [Tooltip("Maximum number of NPCs checked in one LOD tick.")]
    [SerializeField] private int maxChecksPerTick = 20;

    [Header("Sleeping Settings")]
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
    [SerializeField] private bool debugRegistrationLogs = false;
    [SerializeField] private bool debugLodLogs = false;
    [SerializeField] private bool debugDespawnLogs = false;
    [SerializeField] private bool drawGizmos = true;

    private readonly List<NPCEntry> npcs = new();
    private readonly HashSet<GameObject> registeredRoots = new();
    private readonly Dictionary<NPCCore, NPCEntry> coreToEntry = new();

    private float lodTimer;
    private float rescanTimer;
    private int lodIndex;
    private int despawnIndex;
    public int RegisteredCount => npcs.Count;

    private int cachedAliveTotal;
    private int cachedAliveAmbient;
    private int cachedAliveCombat;

    private bool budgetDirty = true;
    private float nextBudgetRefreshTime;

    [SerializeField] private float budgetRefreshInterval = 0.25f;

    private bool listDirty;
    private float nextCleanupTime;
    [SerializeField] private float cleanupInterval = 1.0f;

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

        ResolvePlayerRef();
    }

    private void Start()
    {
        if (scanSceneOnStart)
            ScanSceneForNPCs();
    }

    private bool ResolvePlayerRef()
    {
        if (player != null)
            return true;

        NPCSceneRefs refs = NPCSceneRefs.Instance;

        if (refs != null && refs.HasPlayer())
        {
            player = refs.Player;
            return player != null;
        }

        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");

        if (playerGo != null)
        {
            player = playerGo.transform;
            return true;
        }

        return false;
    }

    private void Update()
    {
        if (!ResolvePlayerRef())
            return;

        TickCleanup();
        TickSceneRescan();
        TickLodAndDespawn();
    }

    private void TickCleanup()
    {
        CleanupNullsThrottled();
    }

    private void TickSceneRescan()
    {
        if (!rescanScenePeriodically)
            return;

        rescanTimer += Time.deltaTime;

        if (rescanTimer < rescanInterval)
            return;

        rescanTimer = 0f;
        ScanSceneForNPCs();
    }

    private void TickLodAndDespawn()
    {
        lodTimer += Time.deltaTime;

        if (lodTimer < lodTickInterval)
            return;

        lodTimer = 0f;

        RefreshLodSlice();

        if (despawnFarAmbientNPCs)
            RefreshAmbientDespawnSlice();
    }

    private void ClampIterationIndices()
    {
        if (lodIndex >= npcs.Count)
            lodIndex = 0;

        if (despawnIndex >= npcs.Count)
            despawnIndex = 0;
    }

    private void RequestCleanup()
    {
        listDirty = true;
        RequestBudgetRefresh();
    }

    private void RequestBudgetRefresh()
    {
        budgetDirty = true;
    }

    private void CleanupNullsThrottled()
    {
        if (!listDirty && Time.time < nextCleanupTime)
            return;

        nextCleanupTime = Time.time + cleanupInterval;
        listDirty = false;

        CleanupNulls();
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

        // Do not register dead bodies again during periodic scene scans.
        if (entry.core != null && entry.core.IsDead)
            return;

        npcs.Add(entry);
        registeredRoots.Add(root);
        RequestBudgetRefresh();

        if (entry.core != null && !coreToEntry.ContainsKey(entry.core))
        {
            coreToEntry.Add(entry.core, entry);
            entry.core.Died += OnCoreDied;
        }

        NPCLodState initialState = NPCLodState.Full;

        if (player != null)
        {
            Vector3 playerTargetPos = NPCPlayerTargetUtility.GetTargetPosition(player);
            initialState = GetTargetLodState(entry, playerTargetPos);
        }

        ApplyLod(entry, initialState, force: true);

        LogRegistration($"[NPCWorldCoordinator] Registered NPC: {root.name}");
    }

    public void UnregisterNPC(GameObject npcRoot)
    {
        if (npcRoot == null)
            return;

        for (int i = npcs.Count - 1; i >= 0; i--)
        {
            NPCEntry entry = npcs[i];

            if (entry == null)
            {
                npcs.RemoveAt(i);
                continue;
            }

            if (entry.root != npcRoot)
                continue;

            RemoveEntryAt(i);
        }

        ClampIterationIndices();
        RequestCleanup();
        RequestBudgetRefresh();
    }

    private void RemoveEntryAt(int index)
    {
        if (index < 0 || index >= npcs.Count)
            return;

        NPCEntry entry = npcs[index];

        UnsubscribeEntry(entry);

        if (entry != null && entry.root != null)
            registeredRoots.Remove(entry.root);

        npcs.RemoveAt(index);
    }

    public bool CanSpawnAmbientNPC()
    {
        CleanupNullsThrottled();
        RefreshBudgetCountsIfNeeded();

        if (cachedAliveTotal >= globalMaxAliveNPCs)
            return false;

        if (cachedAliveAmbient >= globalMaxAmbientNPCs)
            return false;

        return true;
    }

    public bool CanSpawnCombatNPC()
    {
        CleanupNullsThrottled();
        RefreshBudgetCountsIfNeeded();

        if (cachedAliveTotal >= globalMaxAliveNPCs)
            return false;

        if (cachedAliveCombat >= globalMaxCombatNPCs)
            return false;

        return true;
    }

    private NPCLodState GetTargetLodState(NPCEntry entry, Vector3 playerTargetPos)
    {
        if (entry == null || entry.root == null)
            return NPCLodState.Sleeping;

        if (IsDead(entry))
            return NPCLodState.Full;

        if (IsProtectedNPC(entry))
            return NPCLodState.Full;

        float sqrDist = (entry.transform.position - playerTargetPos).sqrMagnitude;

        bool provoked = IsProvoked(entry);

        float fullDist = fullDistance;
        float simpleDist = simpleDistance;

        if (provoked)
        {
            fullDist *= provokedDistanceMultiplier;
            simpleDist *= provokedDistanceMultiplier;
        }

        float fullSqr = fullDist * fullDist;
        float simpleSqr = simpleDist * simpleDist;

        if (sqrDist <= fullSqr)
            return NPCLodState.Full;

        if (sqrDist <= simpleSqr)
            return NPCLodState.Simple;

        return IsImportantNPC(entry)
            ? NPCLodState.Simple
            : NPCLodState.Sleeping;
    }

    private void RefreshBudgetCountsIfNeeded()
    {
        if (!budgetDirty && Time.time < nextBudgetRefreshTime)
            return;

        nextBudgetRefreshTime = Time.time + budgetRefreshInterval;
        budgetDirty = false;

        RecalculateBudgetCounts();
    }

    private void RecalculateBudgetCounts()
    {
        cachedAliveTotal = 0;
        cachedAliveAmbient = 0;
        cachedAliveCombat = 0;

        for (int i = 0; i < npcs.Count; i++)
        {
            NPCEntry entry = npcs[i];

            if (entry == null || entry.root == null)
                continue;

            if (IsDead(entry))
                continue;

            cachedAliveTotal++;

            if (IsCombatNPC(entry))
            {
                cachedAliveCombat++;
                continue;
            }

            if (IsAmbientBudgetNPC(entry))
                cachedAliveAmbient++;
        }
    }

    private bool IsCombatNPC(NPCEntry entry)
    {
        if (entry == null)
            return false;

        if (entry.melee != null)
            return true;

        if (entry.controller == null)
            return false;

        NPCController.NPCReactionType type = entry.controller.GetReactionType();

        return type == NPCController.NPCReactionType.Aggressive ||
               type == NPCController.NPCReactionType.Fighter;
    }

    private bool IsAmbientBudgetNPC(NPCEntry entry)
    {
        if (entry == null)
            return false;

        if (entry.core == null)
            return true;

        if (entry.core.Importance == NPCCore.NPCImportance.Ambient)
            return true;

        return !ignoreImportantNPCsForAmbientLimit;
    }

    public int CountAliveNPCs()
    {
        RefreshBudgetCountsIfNeeded();
        return cachedAliveTotal;
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
        RefreshBudgetCountsIfNeeded();
        return cachedAliveCombat;
    }

    private void OnCoreDied(NPCCore core, string attackerName)
    {
        if (core == null)
            return;

        if (!coreToEntry.TryGetValue(core, out NPCEntry entry))
            return;

        if (entry != null && entry.root != null)
            LogRegistration($"[NPCWorldCoordinator] NPC died, unregistering from LOD: {entry.root.name}");

        int index = npcs.IndexOf(entry);

        if (index >= 0)
            RemoveEntryAt(index);
        else
            UnsubscribeEntry(entry);

        ClampIterationIndices();
        RequestCleanup();
        RequestBudgetRefresh();
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
        Vector3 playerTargetPos = NPCPlayerTargetUtility.GetTargetPosition(player);

        while (checkedCount < maxChecksPerTick && npcs.Count > 0)
        {
            if (lodIndex >= npcs.Count)
                lodIndex = 0;

            NPCEntry entry = npcs[lodIndex];

            if (entry == null || entry.root == null)
            {
                RemoveEntryAt(lodIndex);
                ClampIterationIndices();
                checkedCount++;
                continue;
            }

            RefreshSingleNPC(entry, playerTargetPos);

            lodIndex++;
            checkedCount++;
        }
    }

    private void RefreshSingleNPC(NPCEntry entry, Vector3 playerTargetPos)
    {
        if (entry == null || entry.root == null || player == null)
            return;

        NPCLodState targetState = GetTargetLodState(entry, playerTargetPos);
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

        LogLod($"[NPCWorldCoordinator] {entry.root.name} -> {targetState}");
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
        // Simple mode keeps movement and animation enabled, but disables interaction.
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
        bool removedAny = false;

        for (int i = npcs.Count - 1; i >= 0; i--)
        {
            NPCEntry entry = npcs[i];

            if (entry != null && entry.root != null)
                continue;

            RemoveEntryAt(i);
            removedAny = true;
        }

        ClampIterationIndices();

        if (removedAny)
            RequestBudgetRefresh();
    }

    private void RefreshAmbientDespawnSlice()
    {
        if (player == null || npcs.Count == 0)
            return;

        int checkedCount = 0;
        Vector3 playerTargetPos = NPCPlayerTargetUtility.GetTargetPosition(player);

        while (checkedCount < maxDespawnChecksPerTick && npcs.Count > 0)
        {
            if (despawnIndex >= npcs.Count)
                despawnIndex = 0;

            NPCEntry entry = npcs[despawnIndex];

            if (entry == null || entry.root == null)
            {
                RemoveEntryAt(despawnIndex);
                ClampIterationIndices();
                checkedCount++;
                continue;
            }

            CheckAmbientDespawn(entry, playerTargetPos);

            despawnIndex++;
            checkedCount++;
        }
    }

    private void CheckAmbientDespawn(NPCEntry entry, Vector3 playerTargetPos)
    {
        if (entry == null || entry.root == null)
            return;

        if (IsProtectedNPC(entry))
        {
            entry.farFromPlayerSince = -1f;
            return;
        }

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

        float sqrDist = (entry.transform.position - playerTargetPos).sqrMagnitude;
        float despawnSqr = ambientDespawnDistance * ambientDespawnDistance;

        if (sqrDist < despawnSqr)
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

        DespawnNonProtectedNPC(entry);
    }

    private void DespawnNonProtectedNPC(NPCEntry entry)
    {
        if (entry == null || entry.root == null)
            return;

        LogDespawn($"[NPCWorldCoordinator] Despawn non-protected NPC: {entry.root.name}");

        GameObject root = entry.root;

        UnregisterNPC(root);
        Destroy(root);

        ClampIterationIndices();
        RequestCleanup();
    }

    private bool IsImportantNPC(NPCEntry entry)
    {
        if (entry == null || entry.core == null)
            return false;

        return entry.core.Importance == NPCCore.NPCImportance.Mission ||
               entry.core.Importance == NPCCore.NPCImportance.StoryCritical;
    }

    private bool IsProtectedNPC(NPCEntry entry)
    {
        if (entry == null || entry.root == null)
            return false;

        if (entry.core != null)
        {
            if (entry.core.Importance == NPCCore.NPCImportance.Mission ||
                entry.core.Importance == NPCCore.NPCImportance.StoryCritical)
            {
                return true;
            }
        }

        if (entry.root.GetComponentInChildren<NPCMissionGiver>(true) != null)
            return true;

        if (entry.root.GetComponentInChildren<BankEmployee>(true) != null)
            return true;

        return false;
    }

    public string GetBudgetDebugText()
    {
        RefreshBudgetCountsIfNeeded();

        return $"NPC Budget: Alive={cachedAliveTotal}/{globalMaxAliveNPCs}, " +
               $"Ambient={cachedAliveAmbient}/{globalMaxAmbientNPCs}, " +
               $"Combat={cachedAliveCombat}/{globalMaxCombatNPCs}";
    }

    private void LogRegistration(string message)
    {
        if (debugLogs || debugRegistrationLogs)
            Debug.Log(message);
    }

    private void LogLod(string message)
    {
        if (debugLogs || debugLodLogs)
            Debug.Log(message);
    }

    private void LogDespawn(string message)
    {
        if (debugLogs || debugDespawnLogs)
            Debug.Log(message);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Vector3 center = transform.position;

        Transform playerTransform = player;

        if (playerTransform == null)
        {
            NPCSceneRefs refs = NPCSceneRefs.Instance;

            if (refs != null && refs.HasPlayer())
                playerTransform = refs.Player;
        }

        if (playerTransform != null)
            center = playerTransform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, fullDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, simpleDistance);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(center, sleepDistance);
    }
#endif
}