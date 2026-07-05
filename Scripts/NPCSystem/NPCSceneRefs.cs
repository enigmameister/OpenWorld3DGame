using UnityEngine;

[DefaultExecutionOrder(-200)]
public class NPCSceneRefs : MonoBehaviour
{
    public static NPCSceneRefs Instance { get; private set; }

    [Header("Core")]
    [SerializeField] private Transform player;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private WeaponManager weaponManager;

    [Header("Dialogue / UI")]
    [SerializeField] private DialogueGraphUI dialogueGraphUI;
    [SerializeField] private NpcBarkUI barkUI;
    [SerializeField] private NPCMissionListUI missionListUI;
    [SerializeField] private BankDialogueUI bankDialogueUI;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    public Transform Player => player;
    public PlayerStats PlayerStats => playerStats;
    public WeaponManager WeaponManager => weaponManager;
    public DialogueGraphUI DialogueGraphUI => dialogueGraphUI;
    public NpcBarkUI BarkUI => barkUI;
    public NPCMissionListUI MissionListUI => missionListUI;
    public BankDialogueUI BankDialogueUI => bankDialogueUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NPCSceneRefs] Duplicate found. Destroying this instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveMissingReferences();
    }

    public void ResolveMissingReferences()
    {
        ResolvePlayer();

        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);

        if (dialogueGraphUI == null)
            dialogueGraphUI = FindFirstObjectByType<DialogueGraphUI>(FindObjectsInactive.Include);

        if (barkUI == null)
            barkUI = FindFirstObjectByType<NpcBarkUI>(FindObjectsInactive.Include);

        if (missionListUI == null)
            missionListUI = FindFirstObjectByType<NPCMissionListUI>(FindObjectsInactive.Include);

        if (bankDialogueUI == null)
            bankDialogueUI = FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);

        if (debugLogs)
            Debug.Log("[NPCSceneRefs] References resolved.");
    }

    public void ResolvePlayer()
    {
        if (player != null && playerStats != null)
            return;

        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");

        if (playerGo == null)
            return;

        if (player == null)
            player = playerGo.transform;

        if (playerStats == null)
            playerStats = playerGo.GetComponent<PlayerStats>();
    }

    public bool HasPlayer()
    {
        if (player == null)
            ResolvePlayer();

        return player != null;
    }
}