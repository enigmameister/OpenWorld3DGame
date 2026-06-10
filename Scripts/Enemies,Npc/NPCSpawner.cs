using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class NPCSpawner : MonoBehaviour
{
    [Header("Prefab i limit")]
    public GameObject npcPrefab;
    public GameObject[] meleePrefabs;
    public int maxNPCs = 20;

    [Header("Rozmieszczenie")]
    public float spawnRadius = 30f;
    public float minDistanceBetweenNPCs = 2.0f;
    public float minDistanceToPlayer = 8.0f;

    [Header("Warstwy / NavMesh")]
    public LayerMask npcLayer;            // do OverlapSphere
    public float navmeshMaxSampleDist = 6f;

    [Header("Szanse typów (suma nie musi byæ 1)")]
    [Range(0f, 1f)] public float fighterChance = 0.33f;
    [Range(0f, 1f)] public float aggressiveChance = 0.33f;

    [Header("Melee")]
    [Range(0f, 1f)] public float meleeChance = 0.25f;  // np. 25%

    // reszta to Coward

    [Header("Czêstotliwoœæ spawnowania")]
    public Vector2 spawnDelayRange = new Vector2(5f, 10f);

    private readonly List<GameObject> npcs = new();
    private float timer = 0f;
    private float nextSpawnDelay = 0f;

    private Transform npcParent;
    private Transform player;

    void Start()
    {
        npcParent = GameObject.Find("NPCContainer")?.transform; // opcjonalnie przypisz w Inspectorze i usuñ ten Find
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        ScheduleNextSpawn();
    }
    void Update()
    {
        // sprz¹tnij null-e po NPC, które siê zdespawnowa³y
        npcs.RemoveAll(go => go == null);

        timer += Time.deltaTime;

        if (timer >= nextSpawnDelay && npcs.Count < maxNPCs)
        {
            if (TryGetValidSpawnPoint(out Vector3 spawnPos))
            {
                // >>> DECYZJA: MELEE czy zwyk³y NPC (PRZED instancj¹)
                bool spawnMelee = meleePrefabs != null && meleePrefabs.Length > 0 && Random.value < meleeChance;

                GameObject prefabToSpawn;
                if (spawnMelee)
                    prefabToSpawn = meleePrefabs[Random.Range(0, meleePrefabs.Length)];
                else
                    prefabToSpawn = npcPrefab;

                // >>> INSTANCJA WYBRANEGO PREFABA
                GameObject npc = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
                if (npcParent != null) npc.transform.SetParent(npcParent, true);

                // >>> DLA ZWYK£EGO NPC ustawiamy typ reakcji
                if (!spawnMelee)
                {
                    var ctrl = npc.GetComponent<NPCController>();
                    if (ctrl != null)
                    {
                        float r = Random.value;
                        NPCController.NPCReactionType finalType;
                        if (r < Mathf.Clamp01(fighterChance))
                            finalType = NPCController.NPCReactionType.Fighter;
                        else if (r < Mathf.Clamp01(fighterChance) + Mathf.Clamp01(aggressiveChance))
                            finalType = NPCController.NPCReactionType.Aggressive;
                        else
                            finalType = NPCController.NPCReactionType.Coward;

                        ctrl.SetReactionType(finalType);
                    }
                }
                // (dla Melee nic nie trzeba – logika, kolor bazowy=white i flash red s¹ w NPCMelee)

                npcs.Add(npc);
            }

            ScheduleNextSpawn();
        }
    }

    private void ScheduleNextSpawn()
    {
        timer = 0f;
        if (spawnDelayRange.x > spawnDelayRange.y)
            (spawnDelayRange.x, spawnDelayRange.y) = (spawnDelayRange.y, spawnDelayRange.x);

        nextSpawnDelay = Random.Range(spawnDelayRange.x, spawnDelayRange.y);
    }

    private bool TryGetValidSpawnPoint(out Vector3 validPos)
    {
        // kilka prób, by unikn¹æ kolizji / nie-NavMesh
        for (int i = 0; i < 20; i++)
        {
            // 1) Pozycja kandydat w kole
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(circle.x, 0f, circle.y);

            // 2) Trzymaj dystans od gracza
            if (player != null && Vector3.Distance(candidate, player.position) < minDistanceToPlayer)
                continue;

            // 3) Przy³ó¿ do NavMesh
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navmeshMaxSampleDist, NavMesh.AllAreas))
                continue;

            Vector3 navPos = hit.position;

            // 4) Minimalna odleg³oœæ od innych NPC
            if (Physics.OverlapSphere(navPos, minDistanceBetweenNPCs, npcLayer).Length > 0)
                continue;

            validPos = navPos;
            return true;
        }

        // Fallback – œrodek spawnera (te¿ próbujemy NavMesh)
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit fh, navmeshMaxSampleDist, NavMesh.AllAreas))
        {
            validPos = fh.position;
            return true;
        }

        validPos = transform.position;
        return false;
    }
}
