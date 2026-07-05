using System;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class QuickSaveSystem : MonoBehaviour
{
    public static QuickSaveSystem Instance { get; private set; }

    [Header("References")]
    public Transform playerRoot;
    public CharacterController playerCC;
    public PlayerStats playerStats;
    public DayNightCycle dayNight;
    public WeaponManager weaponManager;
    public string bankJson;

    [Header("Fallback Respawn")]
    [SerializeField] private Transform fallbackRespawnPoint;
    [SerializeField] private bool fallbackFullHP = true;
    [SerializeField] private int fallbackArmor = 0;

    [Header("Vehicle Save")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private bool saveVehicleState = true;

    private Transform currentVehicleRoot;
    private bool playerWasInVehicle;

    [Header("Auto-QuickLoad after death")]
    [SerializeField] private bool autoLoadOnDeath = true;
    [SerializeField] private float autoLoadDelay = 5f;

    private bool _pendingAutoLoad;
    private float _deathTime;

    [Header("NPC Restore")]
    [SerializeField] private GameObject[] npcRestorePrefabs;
    [SerializeField] private Transform npcRestoreParent;

    [Header("Settings")]
    public string saveKey = "QUICKSAVE_SLOT_0";

    [Header("Debug")]
    [SerializeField] private bool debugSkipBankInQuickSave = false;

    [Serializable]
    private class SaveData
    {
        public Vector3 playerPos;
        public Quaternion playerRot;

        public PlayerStats.PlayerStatsSnapshot stats;
        public float time01;
        public WeaponStateSnapshotController.WeaponSnapshot weapons;
        public NPCSaveData[] npcs;
        public VehicleSaveData[] vehicles;

        public bool wasInVehicle;
        public string vehicleSaveId;
        public Vector3 vehiclePos;
        public Quaternion vehicleRot;
        public Vector3 vehicleLinearVelocity;
        public Vector3 vehicleAngularVelocity;

        public string timestamp;
        public string bankJson;
    }

    [Serializable]
    private class NPCSaveData
    {
        public string saveId;
        public string prefabName;

        public Vector3 position;
        public Quaternion rotation;

        public bool isDead;
        public bool activeSelf;

        public bool isController;
        public bool isMelee;

        public bool controllerProvoked;
        public int controllerReactionType;

        public bool meleeAggro;
    }

    [Serializable]
    private class VehicleSaveData
    {
        public string saveId;

        // Old variables
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
        public VehicleDestructible.VehicleDamageSnapshot damage;

        // Save through VehicleFacade
        public bool hasFacadeSnapshot;
        public VehicleFacade.VehicleSaveSnapshot facadeSnapshot;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ResolveMissingRefs();

        if (fallbackRespawnPoint == null && playerRoot != null)
            fallbackRespawnPoint = playerRoot;
    }

    void Update()
    {
        if (IsQuickSaveBlocked())
            return;

        var input = PlayerInputHandler.Instance;
        if (!input) return;

        if (_pendingAutoLoad)
        {
            bool timeReached = (Time.time - _deathTime) >= autoLoadDelay;
            bool mouseClicked = Input.GetMouseButtonDown(0);

            if (timeReached || mouseClicked)
            {
                _pendingAutoLoad = false;

                if (PlayerPrefs.HasKey(saveKey))
                    DoQuickLoad();
                else
                    RespawnAtFallbackStart();
            }

            return;
        }

        if (input.QuickSavePressedThisFrame)
            DoQuickSave();

        if (input.QuickLoadPressedThisFrame)
            DoQuickLoad();
    }

    // ============================================
    //  SAVE
    // ============================================
    public void DoQuickSave()
    {
        SaveData data = new SaveData();
        ResolveCurrentVehicleFromRuntime();

        data.npcs = CaptureNPCs();
        data.vehicles = CaptureVehicles();

        ResolveMissingRefs();

        bool isInVehicle =
            playerWasInVehicle &&
            currentVehicleRoot != null;

        data.wasInVehicle = isInVehicle;
        Debug.Log($"[QuickSave] InVehicle={isInVehicle}, currentVehicle={(currentVehicleRoot != null ? currentVehicleRoot.name : "NULL")}");

        if (isInVehicle)
        {
            data.playerPos = currentVehicleRoot.position;
            data.playerRot = currentVehicleRoot.rotation;

            if (saveVehicleState)
            {
                QuickSaveEntity vehicleEntity = currentVehicleRoot.GetComponent<QuickSaveEntity>();

                if (vehicleEntity == null)
                    vehicleEntity = currentVehicleRoot.gameObject.AddComponent<QuickSaveEntity>();

                data.vehicleSaveId = vehicleEntity.SaveId;

                VehicleFacade vehicleFacade = currentVehicleRoot.GetComponent<VehicleFacade>();

                if (vehicleFacade == null)
                    vehicleFacade = currentVehicleRoot.GetComponentInChildren<VehicleFacade>(true);

                if (vehicleFacade != null)
                {
                    VehicleFacade.VehicleSaveSnapshot snapshot = vehicleFacade.CaptureSaveSnapshot();

                    data.vehiclePos = snapshot.runtime.position;
                    data.vehicleRot = snapshot.runtime.rotation;
                    data.vehicleLinearVelocity = snapshot.runtime.linearVelocity;
                    data.vehicleAngularVelocity = snapshot.runtime.angularVelocity;
                }
                else
                {
                    data.vehiclePos = currentVehicleRoot.position;
                    data.vehicleRot = currentVehicleRoot.rotation;

                    Rigidbody vehicleRb = currentVehicleRoot.GetComponent<Rigidbody>();

                    if (vehicleRb == null)
                        vehicleRb = currentVehicleRoot.GetComponentInChildren<Rigidbody>();

                    if (vehicleRb != null)
                    {
                        data.vehicleLinearVelocity = vehicleRb.linearVelocity;
                        data.vehicleAngularVelocity = vehicleRb.angularVelocity;
                    }
                }

                Debug.Log($"[QuickSave] Vehicle saved: {currentVehicleRoot.name}, id={data.vehicleSaveId}");
            }
        }
        else
        {
            data.playerPos = playerRoot.position;
            data.playerRot = playerRoot.rotation;
        }

        // Statistics
        if (playerStats != null)
            data.stats = playerStats.GetSnapshot();

        // Game time
        if (dayNight != null)
            data.time01 = dayNight.GetSaveTime01();

        // Weapon
        if (weaponManager != null)
            data.weapons = weaponManager.GetSnapshot();


        if (!debugSkipBankInQuickSave && BankSystem.Instance != null)
            data.bankJson = BankSystem.Instance.ExportToJson(pretty: false);
        else
            data.bankJson = null;

        // Timestamp
        data.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();

        Debug.Log($"[QuickSave] Saved date: {data.timestamp}");
    }

    private VehicleSaveData[] CaptureVehicles()
    {
        QuickSaveEntity[] entities = FindObjectsByType<QuickSaveEntity>(FindObjectsSortMode.None);
        System.Collections.Generic.List<VehicleSaveData> result = new System.Collections.Generic.List<VehicleSaveData>();

        for (int i = 0; i < entities.Length; i++)
        {
            QuickSaveEntity entity = entities[i];

            if (entity == null)
                continue;

            VehicleFacade facade = entity.GetComponent<VehicleFacade>();

            if (facade == null)
                facade = entity.GetComponentInChildren<VehicleFacade>(true);

            if (facade != null)
            {
                VehicleFacade.VehicleSaveSnapshot snapshot = facade.CaptureSaveSnapshot();

                VehicleSaveData data = new VehicleSaveData
                {
                    saveId = entity.SaveId,

                    hasFacadeSnapshot = true,
                    facadeSnapshot = snapshot,

                    
                    position = snapshot.runtime.position,
                    rotation = snapshot.runtime.rotation,
                    linearVelocity = snapshot.runtime.linearVelocity,
                    angularVelocity = snapshot.runtime.angularVelocity,
                    damage = snapshot.hasDamageSnapshot ? snapshot.damageSnapshot : default
                };

                result.Add(data);
                continue;
            }

            // Fallback without VehicleFacade
            VehicleDestructible destructible = entity.GetComponent<VehicleDestructible>();

            if (destructible == null)
                continue;

            Rigidbody rb = entity.GetComponent<Rigidbody>();

            if (rb == null)
                rb = entity.GetComponentInChildren<Rigidbody>();

            VehicleSaveData oldData = new VehicleSaveData
            {
                saveId = entity.SaveId,
                hasFacadeSnapshot = false,
                position = entity.transform.position,
                rotation = entity.transform.rotation,
                linearVelocity = rb != null ? rb.linearVelocity : Vector3.zero,
                angularVelocity = rb != null ? rb.angularVelocity : Vector3.zero,
                damage = destructible.GetSnapshot()
            };

            result.Add(oldData);
        }

        Debug.Log($"[QuickSave] Vehicle snapshot count={result.Count}");

        return result.ToArray();
    }

    private void ResolveCurrentVehicleFromRuntime()
    {
        bool playerSaysInVehicle =
            playerMovement != null &&
            playerMovement.IsInVehicle;

        if (CarInteraction.ActiveVehicleTransform != null)
        {
            currentVehicleRoot = CarInteraction.ActiveVehicleTransform;
            playerWasInVehicle = playerSaysInVehicle;
            return;
        }

        if (!playerSaysInVehicle)
        {
            playerWasInVehicle = false;
            return;
        }

        playerWasInVehicle = currentVehicleRoot != null;
    }

    // ============================================
    //  LOAD
    // ============================================

    public void DoQuickLoad()
    {
        if (!PlayerPrefs.HasKey(saveKey))
        {
            Debug.LogWarning("[QuickLoad] Save not found");
            return;
        }

        string json = PlayerPrefs.GetString(saveKey);
        SaveData data;

        try
        {
            data = JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[QuickLoad] JSON error: {ex}");
            return;
        }

        if (BankSystem.Instance != null && !string.IsNullOrWhiteSpace(data.bankJson))
            BankSystem.Instance.ImportFromJson(data.bankJson);

        ApplySaveData(data);

        string ts = string.IsNullOrWhiteSpace(data.timestamp)
            ? "WRONG TIME FORMAT"
            : data.timestamp;

        Debug.Log($"[QuickLoad] Sucess loaded {ts}");
    }

    // ============================================
    //  APPLY
    // ============================================

    private void ApplySaveData(SaveData data)
    {
        ResolveMissingRefs();

        RestoreVehicles(data.vehicles);

        Transform savedVehicle = null;


        if (saveVehicleState && data.wasInVehicle)
        {
            savedVehicle = FindEntityBySaveId(data.vehicleSaveId);

            if (savedVehicle != null)
            {
                SetTransformAndStopPhysics(savedVehicle, data.vehiclePos, data.vehicleRot);

                currentVehicleRoot = savedVehicle;
                playerWasInVehicle = true;

                Debug.Log($"[QuickLoad] Vehicle restored: {savedVehicle.name}, id={data.vehicleSaveId}");
            }
            else
            {
                Debug.LogWarning($"[QuickLoad] Saved vehicle not found, id={data.vehicleSaveId}");
            }
        }

        bool restoredIntoVehicle = false;

        if (data.wasInVehicle && savedVehicle != null)
        {
            VehicleSaveData savedVehicleData = FindVehicleDataBySaveId(data.vehicles, data.vehicleSaveId);

            VehicleFacade vehicleFacade = savedVehicle.GetComponent<VehicleFacade>();

            if (vehicleFacade == null)
                vehicleFacade = savedVehicle.GetComponentInChildren<VehicleFacade>(true);

            if (vehicleFacade != null && savedVehicleData != null && savedVehicleData.hasFacadeSnapshot)
            {
                vehicleFacade.RestorePlayerInsideFromLoad(savedVehicleData.facadeSnapshot);
                restoredIntoVehicle = true;
            }
            else
            {
                CarInteraction carInteraction = savedVehicle.GetComponent<CarInteraction>();

                if (carInteraction == null)
                    carInteraction = savedVehicle.GetComponentInChildren<CarInteraction>(true);

                if (carInteraction != null)
                {
                    SetTransformAndVelocity(
                        savedVehicle,
                        data.vehiclePos,
                        data.vehicleRot,
                        data.vehicleLinearVelocity,
                        data.vehicleAngularVelocity
                    );

                    carInteraction.RestorePlayerInsideCarFromLoad(
                        data.vehicleLinearVelocity,
                        data.vehicleAngularVelocity
                    );

                    restoredIntoVehicle = true;
                }
            }
        }

        if (!restoredIntoVehicle && playerRoot)
        {
            bool hadCC = playerCC != null;

            if (hadCC)
                playerCC.enabled = false;

            if (data.wasInVehicle && savedVehicle != null)
            {
                playerRoot.position = savedVehicle.position + savedVehicle.right * 2.0f + Vector3.up * 0.1f;
                playerRoot.rotation = savedVehicle.rotation;
            }
            else
            {
                playerRoot.position = data.playerPos;
                playerRoot.rotation = data.playerRot;
            }

            if (hadCC)
                playerCC.enabled = true;
        }

        if (playerStats)
        {
            playerStats.ResetDeathStateAfterLoad();
            playerStats.ApplySnapshot(data.stats);
        }

        if (dayNight)
            dayNight.LoadTime01(data.time01);

        if (weaponManager)
            weaponManager.ApplySnapshot(data.weapons);

        RestoreNPCs(data.npcs);
    }

    private VehicleSaveData FindVehicleDataBySaveId(VehicleSaveData[] vehicles, string saveId)
    {
        if (vehicles == null || string.IsNullOrWhiteSpace(saveId))
            return null;

        for (int i = 0; i < vehicles.Length; i++)
        {
            if (vehicles[i] == null)
                continue;

            if (vehicles[i].saveId == saveId)
                return vehicles[i];
        }

        return null;
    }

    private void RestoreVehicles(VehicleSaveData[] savedVehicles)
    {
        if (savedVehicles == null)
            return;

        for (int i = 0; i < savedVehicles.Length; i++)
        {
            VehicleSaveData saved = savedVehicles[i];

            if (saved == null)
                continue;

            Transform vehicleTransform = FindEntityBySaveId(saved.saveId);

            if (vehicleTransform == null)
                continue;

            VehicleFacade facade = vehicleTransform.GetComponent<VehicleFacade>();

            if (facade == null)
                facade = vehicleTransform.GetComponentInChildren<VehicleFacade>(true);

            if (facade != null && saved.hasFacadeSnapshot)
            {
                facade.RestoreVehicleOnlyFromLoad(saved.facadeSnapshot);
                continue;
            }

            // Fallback dla starych save'ów albo pojazdów bez VehicleFacade.
            SetTransformAndVelocity(
                vehicleTransform,
                saved.position,
                saved.rotation,
                saved.linearVelocity,
                saved.angularVelocity
            );

            VehicleDestructible destructible = vehicleTransform.GetComponent<VehicleDestructible>();

            if (destructible != null)
                destructible.ApplySnapshot(saved.damage);
        }

        Debug.Log($"[QuickLoad] Vehicle snapshot restored count={savedVehicles.Length}");
    }

    void OnEnable()
    {
        PlayerStats.OnPlayerDied += OnPlayerDied;
    }

    void OnDisable()
    {
        PlayerStats.OnPlayerDied -= OnPlayerDied;
    }

    private void OnPlayerDied(string killerName)
    {
        if (!autoLoadOnDeath)
            return;

        _pendingAutoLoad = true;
        _deathTime = Time.time;
    }

    private void ResolveMissingRefs()
    {
        if (playerRoot == null)
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");

            if (playerGo != null)
                playerRoot = playerGo.transform;
        }

        if (playerRoot != null)
        {
            if (playerCC == null)
                playerCC = playerRoot.GetComponent<CharacterController>();

            if (playerStats == null)
                playerStats = playerRoot.GetComponent<PlayerStats>();

            if (playerMovement == null)
                playerMovement = playerRoot.GetComponent<PlayerMovement>();
        }

        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);
    }

    public void SetCurrentVehicle(Transform vehicleRoot, bool isPlayerInside)
    {
        currentVehicleRoot = vehicleRoot;
        playerWasInVehicle = isPlayerInside;
    }

    public void SetCurrentVehicle(VehicleFacade vehicle, bool isPlayerInside)
    {
        if (vehicle == null)
        {
            currentVehicleRoot = null;
            playerWasInVehicle = false;
            return;
        }

        currentVehicleRoot = vehicle.transform;
        playerWasInVehicle = isPlayerInside;
    }

    private bool IsQuickSaveBlocked()
    {
        if (CarRaceManager.IsRaceLoading)
            return true;

        CarRaceManager race = FindFirstObjectByType<CarRaceManager>(FindObjectsInactive.Include);

        if (race == null)
            return false;

        System.Type type = typeof(CarRaceManager);

        string[] boolNames =
        {
        "IsRaceActive",
        "IsRacing",
        "RaceActive",
        "IsRaceRunning",
        "IsInRace"
    };

        for (int i = 0; i < boolNames.Length; i++)
        {
            string name = boolNames[i];

            var staticProperty = type.GetProperty(
                name,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static
            );

            if (staticProperty != null &&
                staticProperty.PropertyType == typeof(bool) &&
                (bool)staticProperty.GetValue(null))
            {
                return true;
            }

            var instanceProperty = type.GetProperty(
                name,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance
            );

            if (instanceProperty != null &&
                instanceProperty.PropertyType == typeof(bool) &&
                (bool)instanceProperty.GetValue(race))
            {
                return true;
            }

            var staticField = type.GetField(
                name,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static
            );

            if (staticField != null &&
                staticField.FieldType == typeof(bool) &&
                (bool)staticField.GetValue(null))
            {
                return true;
            }

            var instanceField = type.GetField(
                name,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance
            );

            if (instanceField != null &&
                instanceField.FieldType == typeof(bool) &&
                (bool)instanceField.GetValue(race))
            {
                return true;
            }
        }

        return false;
    }

    private void RespawnAtFallbackStart()
    {
        ResolveMissingRefs();

        Transform respawnPoint = fallbackRespawnPoint != null
            ? fallbackRespawnPoint
            : playerRoot;

        if (playerRoot != null && respawnPoint != null)
        {
            bool hadCC = playerCC != null;

            if (hadCC)
                playerCC.enabled = false;

            playerRoot.position = respawnPoint.position;
            playerRoot.rotation = respawnPoint.rotation;

            if (hadCC)
                playerCC.enabled = true;
        }

        if (playerStats != null)
        {
            playerStats.ResetDeathStateAfterLoad();

            PlayerStats.PlayerStatsSnapshot stats = playerStats.GetSnapshot();

            if (fallbackFullHP)
                stats.health = playerStats.maxHP;

            stats.armor = Mathf.Clamp(fallbackArmor, 0, playerStats.maxArmor);

            playerStats.ApplySnapshot(stats);
        }

        if (weaponManager != null)
            weaponManager.ActivateHandsOnly();

        Debug.Log("[QuickLoad] No quicksave found. Respawned at fallback start.");
    }
    private Transform FindEntityBySaveId(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            return null;

        QuickSaveEntity[] entities = FindObjectsByType<QuickSaveEntity>(FindObjectsSortMode.None);

        for (int i = 0; i < entities.Length; i++)
        {
            QuickSaveEntity entity = entities[i];

            if (entity == null)
                continue;

            if (entity.SaveId == saveId)
                return entity.transform;
        }

        return null;
    }

    private NPCSaveData[] CaptureNPCs()
    {
        QuickSaveEntity[] entities = FindObjectsByType<QuickSaveEntity>(FindObjectsSortMode.None);
        System.Collections.Generic.List<NPCSaveData> result = new System.Collections.Generic.List<NPCSaveData>();

        for (int i = 0; i < entities.Length; i++)
        {
            QuickSaveEntity entity = entities[i];

            if (entity == null)
                continue;

            NPCController controller = entity.GetComponent<NPCController>();
            NPCMelee melee = entity.GetComponent<NPCMelee>();
            NPCCore core = entity.GetComponent<NPCCore>();

            if (core != null && core.Importance != NPCCore.NPCImportance.Ambient)
                continue;

            if (controller == null && melee == null && core == null)
                continue;

            NPCSaveData data = new NPCSaveData();

            data.saveId = entity.SaveId;
            data.prefabName = entity.gameObject.name.Replace("(Clone)", "");

            data.position = entity.transform.position;
            data.rotation = entity.transform.rotation;

            data.activeSelf = entity.gameObject.activeSelf;
            data.isDead = core != null && core.IsDead;

            data.isController = controller != null;
            data.isMelee = melee != null;

            if (controller != null)
            {
                data.controllerProvoked = controller.IsProvoked;
                data.controllerReactionType = (int)controller.GetReactionType();
            }

            if (melee != null)
            {
                data.meleeAggro = melee.IsAggro;
            }

            result.Add(data);
        }

        return result.ToArray();
    }

    private void RestoreNPCs(NPCSaveData[] savedNPCs)
    {
        NPCSpawner[] pausedSpawners = DisableNPCSpawnersForRestore();

        DestroyCurrentAmbientNPCs();

        if (savedNPCs == null)
        {
            StartCoroutine(ReenableNPCSpawnersNextFrame(pausedSpawners));
            return;
        }

        int restoredCount = 0;

        for (int i = 0; i < savedNPCs.Length; i++)
        {
            NPCSaveData saved = savedNPCs[i];

            if (saved.isDead)
                continue;

            GameObject prefab = FindNpcPrefabByName(saved.prefabName);

            if (prefab == null)
            {
                Debug.LogWarning($"[QuickLoad] Missing NPC prefab for restore: {saved.prefabName}");
                continue;
            }

            GameObject npc = Instantiate(
                prefab,
                saved.position,
                saved.rotation,
                npcRestoreParent
            );

            QuickSaveEntity entity = npc.GetComponent<QuickSaveEntity>();

            if (entity == null)
                entity = npc.AddComponent<QuickSaveEntity>();

            entity.OverrideSaveIdForRestore(saved.saveId);

            NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();

            if (agent != null && agent.enabled)
            {
                if (NavMesh.SamplePosition(saved.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    npc.transform.position = hit.position;
                }

                agent.ResetPath();
                agent.isStopped = false;
            }

            NPCWorldCoordinator.Instance?.RegisterNPC(npc);

            restoredCount++;
        }

        Debug.Log($"[QuickLoad] NPC restored from prefabs count={restoredCount}");
        StartCoroutine(ReenableNPCSpawnersNextFrame(pausedSpawners));
    }

    private NPCSpawner[] DisableNPCSpawnersForRestore()
    {
        NPCSpawner[] spawners = FindObjectsByType<NPCSpawner>(FindObjectsSortMode.None);

        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
                spawners[i].enabled = false;
        }

        return spawners;
    }

    private IEnumerator ReenableNPCSpawnersNextFrame(NPCSpawner[] spawners)
    {
        yield return null;
        yield return null;

        if (spawners == null)
            yield break;

        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
                spawners[i].enabled = true;
        }
    }

    private void DestroyCurrentAmbientNPCs()
    {
        QuickSaveEntity[] entities = FindObjectsByType<QuickSaveEntity>(FindObjectsSortMode.None);

        for (int i = 0; i < entities.Length; i++)
        {
            QuickSaveEntity entity = entities[i];

            if (entity == null)
                continue;

            NPCCore core = entity.GetComponent<NPCCore>();
            NPCController controller = entity.GetComponent<NPCController>();
            NPCMelee melee = entity.GetComponent<NPCMelee>();

            if (core == null && controller == null && melee == null)
                continue;

            if (core != null && core.Importance != NPCCore.NPCImportance.Ambient)
                continue;

            NPCWorldCoordinator.Instance?.UnregisterNPC(entity.gameObject);

            entity.gameObject.SetActive(false);
            Destroy(entity.gameObject);
        }
    }

    private void SetTransformAndStopPhysics(Transform target, Vector3 position, Quaternion rotation)
    {
        if (target == null)
            return;

        Rigidbody rb = target.GetComponent<Rigidbody>();

        if (rb == null)
            rb = target.GetComponentInChildren<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
            rb.rotation = rotation;
        }

        target.position = position;
        target.rotation = rotation;

        Physics.SyncTransforms();
    }
    private void SetTransformAndVelocity(
    Transform target,
    Vector3 position,
    Quaternion rotation,
    Vector3 linearVelocity,
    Vector3 angularVelocity)
    {
        if (target == null)
            return;

        Rigidbody rb = target.GetComponent<Rigidbody>();

        if (rb == null)
            rb = target.GetComponentInChildren<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = linearVelocity;
            rb.angularVelocity = angularVelocity;
            rb.WakeUp();
        }

        target.position = position;
        target.rotation = rotation;

        Physics.SyncTransforms();
    }
    private GameObject FindNpcPrefabByName(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
            return null;

        if (npcRestorePrefabs == null)
            return null;

        string cleanName = prefabName.Replace("(Clone)", "").Trim();

        for (int i = 0; i < npcRestorePrefabs.Length; i++)
        {
            GameObject prefab = npcRestorePrefabs[i];

            if (prefab == null)
                continue;

            if (prefab.name == cleanName)
                return prefab;
        }

        return null;
    }
}
