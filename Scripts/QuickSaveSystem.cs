using System;
using UnityEngine;

public class QuickSaveSystem : MonoBehaviour
{
    [Header("Referencje")]
    public Transform playerRoot;
    public CharacterController playerCC;
    public PlayerStats playerStats;
    public DayNightCycle dayNight;
    public WeaponManager weaponManager;
    public string bankJson;

    [Header("Auto-QuickLoad po śmierci")]
    [SerializeField] private bool autoLoadOnDeath = true;
    [SerializeField] private float autoLoadDelay = 5f;

    private bool _pendingAutoLoad;
    private float _deathTime;


    [Header("Ustawienia")]
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

        public string timestamp;  // 👈 NOWE
        public string bankJson;
    }

    void Update()
    {
        if (CarRaceManager.IsRaceLoading)
            return;

        var input = PlayerInputHandler.Instance;
        if (!input) return;

        // — AUTO-LOAD po śmierci —
        if (_pendingAutoLoad)
        {
            bool timeReached = (Time.time - _deathTime) >= autoLoadDelay;
            bool mouseClicked = Input.GetMouseButtonDown(0);   // LPM

            if (timeReached || mouseClicked)
            {
                _pendingAutoLoad = false;

                // tylko jeśli quicksave istnieje – zgodnie z Twoim założeniem
                if (PlayerPrefs.HasKey(saveKey))
                    DoQuickLoad();
            }

            // podczas czekania na auto-load ignorujemy ręczne QuickSave/QuickLoad
            return;
        }

        // — standardowy QuickSave / QuickLoad z klawiatury / pada —
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
        if (!playerRoot)
        {
            Debug.LogWarning("[QuickSave] Brak playerRoot!");
            return;
        }

        SaveData data = new SaveData();

        // Pozycja
        data.playerPos = playerRoot.position;
        data.playerRot = playerRoot.rotation;

        // Statystyki
        if (playerStats != null)
            data.stats = playerStats.GetSnapshot();

        // Czas dnia
        if (dayNight != null)
            data.time01 = dayNight.GetSaveTime01();

        // Broń
        if (weaponManager != null)
            data.weapons = weaponManager.GetSnapshot();


        if (!debugSkipBankInQuickSave && BankSystem.Instance != null)
            data.bankJson = BankSystem.Instance.ExportToJson(pretty: false);
        else
            data.bankJson = null;

        // 👇 Timestamp w czytelnym formacie
        data.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();

        Debug.Log($"[QuickSave] ✔ Zapisano stan gry o {data.timestamp}");
    }

    // ============================================
    //  LOAD
    // ============================================

    public void DoQuickLoad()
    {
        if (!PlayerPrefs.HasKey(saveKey))
        {
            Debug.LogWarning("[QuickLoad] ❌ Brak zapisu quick-save.");
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
            Debug.LogError($"[QuickLoad] Błąd JSON: {ex}");
            return;
        }

        if (BankSystem.Instance != null && !string.IsNullOrWhiteSpace(data.bankJson))
            BankSystem.Instance.ImportFromJson(data.bankJson);

        if (!string.IsNullOrWhiteSpace(data.bankJson) && BankSystem.Instance != null)
            BankSystem.Instance.ImportFromJson(data.bankJson);

        ApplySaveData(data);

        string ts = string.IsNullOrWhiteSpace(data.timestamp)
            ? "NIEZNANY_CZAS"
            : data.timestamp;

        Debug.Log($"[QuickLoad] ✔ Wczytano zapis z {ts}");
    }

    // ============================================
    //  APPLY
    // ============================================

    private void ApplySaveData(SaveData data)
    {
        if (playerRoot)
        {
            bool hadCC = playerCC != null;
            if (hadCC) playerCC.enabled = false;

            playerRoot.position = data.playerPos;
            playerRoot.rotation = data.playerRot;

            if (hadCC) playerCC.enabled = true;
        }

        if (playerStats)
        {
            // najpierw cofnij stan śmierci + UI
            playerStats.ResetDeathStateAfterLoad();

            // potem wczytaj statystyki (HP, armor, stamina itd.)
            playerStats.ApplySnapshot(data.stats);
        }

        if (dayNight)
            dayNight.LoadTime01(data.time01);

        if (weaponManager)
            weaponManager.ApplySnapshot(data.weapons);
    }

    ///
    /// DEATH AUTOLOAD
    ///

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
        if (!autoLoadOnDeath) return;

        // Auto-load tylko jeśli faktycznie istnieje quicksave
        if (PlayerPrefs.HasKey(saveKey))
        {
            _pendingAutoLoad = true;
            _deathTime = Time.time;
        }
    }

}
