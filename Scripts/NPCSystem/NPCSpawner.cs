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

    [Header("NPC Prefabs")]
    [SerializeField] private GameObject[] civilianPrefabs;
    [SerializeField] private GameObject[] fighterPrefabs;
    [SerializeField] private GameObject[] aggressivePrefabs;
    [SerializeField] private GameObject[] meleePrefabs;

    [Header("Local Limit")]
    [SerializeField] private int maxNPCs = 20;

    [Header("Rozmieszczenie")]
    [SerializeField] private float spawnRadius = 30f;
    [SerializeField] private float minDistanceBetweenNPCs = 2.0f;
    [SerializeField] private float minDistanceToPlayer = 8.0f;

    [Header("Warstwy / NavMesh")]
    [SerializeField] private LayerMask npcLayer;
    [SerializeField] private float navmeshMaxSampleDist = 6f;
    [SerializeField] private int maxSpawnPointAttempts = 20;

    [Header("Szanse typów")]
    [Range(0f, 1f)][SerializeField] private float fighterChance = 0.25f;
    [Range(0f, 1f)][SerializeField] private float aggressiveChance = 0.20f;
    [Range(0f, 1f)][SerializeField] private float meleeChance = 0.15f;

    [Header("Czêstotliwoœæ spawnowania")]
    [SerializeField] private Vector2 spawnDelayRange = new Vector2(5f, 10f);

    [Header("Refs")]
    [SerializeField] private Transform npcParent;
    [SerializeField] private Transform player;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly List<GameObject> npcs = new();

    private float timer;
    private float nextSpawnDelay;

    private void Start()
    {
        if (npcParent == null)
            npcParent = GameObject.Find("NPCContainer")?.transform;

        if (player == null)
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                player = playerGo.transform;
        }

        HideAllPrefabWarnings();
        ScheduleNextSpawn();
    }

    private void Update()
    {
        CleanupLocalList();

        timer += Time.deltaTime;

        if (timer < nextSpawnDelay)
            return;

        if (npcs.Count >= maxNPCs)
        {
            ScheduleNextSpawn();
            return;
        }

        PlannedSpawnType plannedType = RollSpawnType();

        GameObject prefabToSpawn = PickPrefab(plannedType);

        if (prefabToSpawn == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[NPCSpawner] {name}: Brak prefabu dla typu {plannedType}.");

            ScheduleNextSpawn();
            return;
        }

        if (!CanSpawnByGlobalBudget(plannedType))
        {
            ScheduleNextSpawn();
            return;
        }

        if (TryGetValidSpawnPoint(out Vector3 spawnPos))
        {
            GameObject npc = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            if (npcParent != null)
                npc.transform.SetParent(npcParent, true);

            NPCWorldCoordinator.Instance?.RegisterNPC(npc);

            npcs.Add(npc);

            if (debugLogs)
                Debug.Log($"[NPCSpawner] Spawned {plannedType}: {npc.name}");
        }

        ScheduleNextSpawn();
    }

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

        float r = Random.value;

        if (r < melee)
            return PlannedSpawnType.Melee;

        if (r < melee + fighter)
            return PlannedSpawnType.Fighter;

        if (r < melee + fighter + aggressive)
            return PlannedSpawnType.Aggressive;

        return PlannedSpawnType.Civilian;
    }

    private GameObject PickPrefab(PlannedSpawnType type)
    {
        switch (type)
        {
            case PlannedSpawnType.Civilian:
                return PickRandom(civilianPrefabs);

            case PlannedSpawnType.Fighter:
                return PickRandom(fighterPrefabs);

            case PlannedSpawnType.Aggressive:
                return PickRandom(aggressivePrefabs);

            case PlannedSpawnType.Melee:
                return PickRandom(meleePrefabs);

            default:
                return null;
        }
    }

    private GameObject PickRandom(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
            return null;

        List<GameObject> valid = null;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null)
                continue;

            valid ??= new List<GameObject>();
            valid.Add(prefabs[i]);
        }

        if (valid == null || valid.Count == 0)
            return null;

        return valid[Random.Range(0, valid.Count)];
    }

    private bool CanSpawnByGlobalBudget(PlannedSpawnType type)
    {
        if (NPCWorldCoordinator.Instance == null)
            return true;

        bool isCombat =
            type == PlannedSpawnType.Fighter ||
            type == PlannedSpawnType.Aggressive ||
            type == PlannedSpawnType.Melee;

        if (isCombat)
            return NPCWorldCoordinator.Instance.CanSpawnCombatNPC();

        return NPCWorldCoordinator.Instance.CanSpawnAmbientNPC();
    }

    private void ScheduleNextSpawn()
    {
        timer = 0f;

        if (spawnDelayRange.x > spawnDelayRange.y)
            (spawnDelayRange.x, spawnDelayRange.y) = (spawnDelayRange.y, spawnDelayRange.x);

        nextSpawnDelay = Random.Range(spawnDelayRange.x, spawnDelayRange.y);
    }

    private void CleanupLocalList()
    {
        for (int i = npcs.Count - 1; i >= 0; i--)
        {
            if (npcs[i] == null)
                npcs.RemoveAt(i);
        }
    }

    private bool TryGetValidSpawnPoint(out Vector3 validPos)
    {
        Vector3 playerTargetPos = NPCPlayerTargetUtility.GetTargetPosition(player);

        for (int i = 0; i < maxSpawnPointAttempts; i++)
        {
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(circle.x, 0f, circle.y);

            if (playerTargetPos != Vector3.zero &&
                Vector3.Distance(candidate, playerTargetPos) < minDistanceToPlayer)
            {
                continue;
            }

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navmeshMaxSampleDist, NavMesh.AllAreas))
                continue;

            Vector3 navPos = hit.position;

            if (Physics.OverlapSphere(navPos, minDistanceBetweenNPCs, npcLayer, QueryTriggerInteraction.Ignore).Length > 0)
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