using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class NPCSpawner : MonoBehaviour
{
    private enum PlannedSpawnType
    {
        Civilian,
        Fighter,
        Aggressive,
        Melee
    }

    // =========================================================
    // PREFABS
    // =========================================================

    [Header("NPC Prefabs")]
    [SerializeField] private GameObject[] civilianPrefabs;
    [SerializeField] private GameObject[] fighterPrefabs;
    [SerializeField] private GameObject[] aggressivePrefabs;
    [SerializeField] private GameObject[] meleePrefabs;

    // =========================================================
    // LIMITS
    // =========================================================

    [Header("Local Limit")]
    [SerializeField] private int maxNPCs = 20;

    // =========================================================
    // SPAWN AREA
    // =========================================================

    [Header("Spawn Area")]
    [SerializeField] private float spawnRadius = 30f;
    [SerializeField] private float minDistanceBetweenNPCs = 2.0f;
    [SerializeField] private float minDistanceToPlayer = 8.0f;

    // =========================================================
    // NAVMESH / LAYERS
    // =========================================================

    [Header("Layers / NavMesh")]
    [SerializeField] private LayerMask npcLayer;
    [SerializeField] private float navmeshMaxSampleDist = 6f;
    [SerializeField] private int maxSpawnPointAttempts = 20;

    // =========================================================
    // SPAWN CHANCES
    // =========================================================

    [Header("Spawn Chances")]
    [Range(0f, 1f)][SerializeField] private float fighterChance = 0.25f;
    [Range(0f, 1f)][SerializeField] private float aggressiveChance = 0.20f;
    [Range(0f, 1f)][SerializeField] private float meleeChance = 0.15f;

    // =========================================================
    // SPAWN RATE
    // =========================================================

    [Header("Spawn Rate")]
    [SerializeField] private Vector2 spawnDelayRange = new Vector2(5f, 10f);

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField] private Transform npcParent;
    [SerializeField] private Transform player;

    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private readonly List<GameObject> npcs = new();

    private readonly Collider[] nearbyNpcBuffer = new Collider[16];

    private float timer;
    private float nextSpawnDelay;

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    private void Start()
    {
        ResolveNpcParent();
        ResolvePlayerRef();

        HideAllPrefabWarnings();
        ScheduleNextSpawn();
    }

    private void Update()
    {
        if (!CanTickSpawner())
            return;

        TickCleanup();
        TickSpawn();
    }

    // =========================================================
    // MAIN TICK
    // =========================================================

    private bool CanTickSpawner()
    {
        if (player == null)
            ResolvePlayerRef();

        return player != null;
    }

    private void TickCleanup()
    {
        CleanupLocalList();
    }

    private void TickSpawn()
    {
        timer += Time.deltaTime;

        if (timer < nextSpawnDelay)
            return;

        TrySpawnOneNPC();
        ScheduleNextSpawn();
    }

    private void TrySpawnOneNPC()
    {
        if (npcs.Count >= maxNPCs)
            return;

        PlannedSpawnType plannedType = RollSpawnType();
        GameObject prefab = PickPrefab(plannedType);

        if (prefab == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[NPCSpawner] {name}: Missing prefab for type {plannedType}.");

            return;
        }

        if (!CanSpawnByGlobalBudget(plannedType))
            return;

        if (!TryGetValidSpawnPoint(out Vector3 spawnPoint))
            return;

        GameObject npc = Instantiate(prefab, spawnPoint, Quaternion.identity);

        if (npcParent != null)
            npc.transform.SetParent(npcParent, true);

        npcs.Add(npc);

        NPCWorldCoordinator.Instance?.RegisterNPC(npc);

        if (debugLogs)
            Debug.Log($"[NPCSpawner] Spawned {plannedType}: {npc.name}");
    }

    // =========================================================
    // SPAWN TYPE / PREFAB SELECTION
    // =========================================================

    private PlannedSpawnType RollSpawnType()
    {
        float melee = Mathf.Clamp01(meleeChance);
        float fighter = Mathf.Clamp01(fighterChance);
        float aggressive = Mathf.Clamp01(aggressiveChance);

        float totalSpecial = melee + fighter + aggressive;

        if (totalSpecial > 1f)
        {
            melee /= totalSpecial;
            fighter /= totalSpecial;
            aggressive /= totalSpecial;
        }

        float roll = Random.value;

        if (roll < melee)
            return PlannedSpawnType.Melee;

        if (roll < melee + fighter)
            return PlannedSpawnType.Fighter;

        if (roll < melee + fighter + aggressive)
            return PlannedSpawnType.Aggressive;

        return PlannedSpawnType.Civilian;
    }

    private GameObject PickPrefab(PlannedSpawnType type)
    {
        switch (type)
        {
            case PlannedSpawnType.Fighter:
                return PickFromArray(fighterPrefabs);

            case PlannedSpawnType.Aggressive:
                return PickFromArray(aggressivePrefabs);

            case PlannedSpawnType.Melee:
                return PickFromArray(meleePrefabs);

            case PlannedSpawnType.Civilian:
            default:
                return PickFromArray(civilianPrefabs);
        }
    }

    private GameObject PickFromArray(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
            return null;

        int startIndex = Random.Range(0, prefabs.Length);

        for (int i = 0; i < prefabs.Length; i++)
        {
            int index = (startIndex + i) % prefabs.Length;
            GameObject prefab = prefabs[index];

            if (prefab != null)
                return prefab;
        }

        return null;
    }

    // =========================================================
    // GLOBAL BUDGET
    // =========================================================

    private bool CanSpawnByGlobalBudget(PlannedSpawnType type)
    {
        NPCWorldCoordinator coordinator = NPCWorldCoordinator.Instance;

        if (coordinator == null)
            return true;

        if (IsCombatSpawnType(type))
            return coordinator.CanSpawnCombatNPC();

        return coordinator.CanSpawnAmbientNPC();
    }

    private bool IsCombatSpawnType(PlannedSpawnType type)
    {
        return type == PlannedSpawnType.Fighter ||
               type == PlannedSpawnType.Aggressive ||
               type == PlannedSpawnType.Melee;
    }

    // =========================================================
    // SPAWN POINT
    // =========================================================

    private bool TryGetValidSpawnPoint(out Vector3 validPos)
    {
        Vector3 playerTargetPos = NPCPlayerTargetUtility.GetTargetPosition(player);
        float minPlayerSqr = minDistanceToPlayer * minDistanceToPlayer;

        for (int i = 0; i < maxSpawnPointAttempts; i++)
        {
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(circle.x, 0f, circle.y);

            if ((candidate - playerTargetPos).sqrMagnitude < minPlayerSqr)
                continue;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navmeshMaxSampleDist, NavMesh.AllAreas))
                continue;

            Vector3 navPos = hit.position;

            if (HasNearbyNPC(navPos))
                continue;

            validPos = navPos;
            return true;
        }

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit fallbackHit, navmeshMaxSampleDist, NavMesh.AllAreas))
        {
            validPos = fallbackHit.position;
            return true;
        }

        validPos = transform.position;
        return false;
    }

    private bool HasNearbyNPC(Vector3 position)
    {
        if (npcLayer.value == 0)
            return false;

        int count = Physics.OverlapSphereNonAlloc(
            position,
            minDistanceBetweenNPCs,
            nearbyNpcBuffer,
            npcLayer,
            QueryTriggerInteraction.Ignore
        );

        return count > 0;
    }

    // =========================================================
    // CLEANUP / SCHEDULING
    // =========================================================

    private void CleanupLocalList()
    {
        for (int i = npcs.Count - 1; i >= 0; i--)
        {
            if (npcs[i] != null)
                continue;

            npcs.RemoveAt(i);
        }
    }

    private void ScheduleNextSpawn()
    {
        timer = 0f;

        if (spawnDelayRange.x > spawnDelayRange.y)
            (spawnDelayRange.x, spawnDelayRange.y) = (spawnDelayRange.y, spawnDelayRange.x);

        float minDelay = Mathf.Max(0.05f, spawnDelayRange.x);
        float maxDelay = Mathf.Max(minDelay, spawnDelayRange.y);

        nextSpawnDelay = Random.Range(minDelay, maxDelay);
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    private void ResolveNpcParent()
    {
        if (npcParent != null)
            return;

        GameObject container = GameObject.Find("NPCContainer");

        if (container != null)
            npcParent = container.transform;
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

        if (playerGo == null)
            return false;

        player = playerGo.transform;
        return true;
    }

    // =========================================================
    // DEBUG
    // =========================================================

    private void HideAllPrefabWarnings()
    {
        if (!debugLogs)
            return;

        if (civilianPrefabs == null || civilianPrefabs.Length == 0)
            Debug.LogWarning($"[NPCSpawner] {name}: civilianPrefabs is empty.");

        if (fighterPrefabs == null || fighterPrefabs.Length == 0)
            Debug.LogWarning($"[NPCSpawner] {name}: fighterPrefabs is empty.");

        if (aggressivePrefabs == null || aggressivePrefabs.Length == 0)
            Debug.LogWarning($"[NPCSpawner] {name}: aggressivePrefabs is empty.");

        if (meleePrefabs == null || meleePrefabs.Length == 0)
            Debug.LogWarning($"[NPCSpawner] {name}: meleePrefabs is empty.");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minDistanceToPlayer);
    }
#endif
}