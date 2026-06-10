using TMPro;
using UnityEngine;
using System.Collections;
using System;

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Citizen ID")]
    [SerializeField] public string citizenId;   // null / empty = brak ID
    public bool HasCitizenID => !string.IsNullOrEmpty(citizenId);

    public string CitizenID => citizenId;

    [Header("Statystyki gracza")]
    public int maxHP = 100;
    public int maxArmor = 100;

    public int currentHP;
    public int currentArmor = 0;
    public bool IsDead { get; private set; } = false;
    public GameObject deathScreen; // nowy GameObject zamiast CanvasGroup
    public TMP_Text deathMessage;
    public static event Action<string> OnPlayerDied;   // kto zabił (name)
    public string LastAttackerName { get; private set; } = "";

    [Header("UI")]
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI armorText;

    // ========= UNDERWATER / OXYGEN =========
    [Header("Underwater / Oxygen")]
    public float oxygenMax = 12f;             // sekundy pełnego tlenu
    public float oxygenDrainPerSec = 1.0f;    // spadek pod wodą
    public float oxygenRegenPerSec = 4.0f;    // regen po wynurzeniu
    [Tooltip("Co ile sekund zadawać kolejne obrażenia z tonięcia.")]
    public float drowningTick = 1.0f;
    [Tooltip("Progresywna sekwencja obrażeń (HL-style).")]
    public int[] drowningDamageSeq = new[] { 5, 10, 20, 30 };
    [Range(0f, 1f)] public float postDrownHealCap = 0.60f;  // np. 60% max HP
    [Range(0.5f, 5f)] public float postDrownHealPerSec = 12f; // tempo „cyklicznie”
    private bool _tookDrownDamage;
    private Coroutine _postDrownHealCo;


    [SerializeField] private UnityEngine.UI.Image oxygenBar;   // pasek O2
    [SerializeField] private GameObject oxygenRoot;             // kontener UI

    [HideInInspector] public bool isUnderwater = false;
    [Range(0f, 30f)] public float postDrownRegenPerSec = 4f;

    private float _oxygen;
    private float _drownTimer;
    private int _drownStage;
    private Coroutine oxygenCoroutine;

    public TextMeshProUGUI moneyText;

    [Header("Pieniądze")]
    public int money = 0;
    private int previousMoney;

    [Header("Money charging (płynne zasilanie)")]
    [Tooltip("Ile $ na sekundę ma przybywać podczas doładowania.")]
    public float moneyChargePerSecond = 250f;

    [Tooltip("Kolor tekstu gdy pieniądze się doładowują.")]
    public Color moneyChargingColor = Color.yellow;

    [Tooltip("Kolor normalny tekstu po zakończeniu doładowania. Jeśli alfa=0, zostanie aktualny.")]
    public Color moneyNormalColor = new Color(0, 0, 0, 0);

    private Coroutine _moneyChargeCo;
    private int _moneyChargeTarget;
    private float _moneyChargeFloat;

    private Color _moneyOriginalColor;
    private bool _moneyColorCached;

    void Start()
    {
        currentHP = maxHP;
        currentArmor = 0; // ← Start bez armoru
        UpdateUI();

        _oxygen = oxygenMax;
        _drownTimer = 0f;
        _drownStage = 0;

        if (oxygenBar) oxygenBar.fillAmount = 1f;
        previousMoney = money;
        UpdateMoneyUI();
    }

    public void AddMoney(int amount)
    {
        money += amount;
        UpdateMoneyUI();
    }

    public bool SpendMoney(int amount)
    {
        CancelMoneyCharging();

        if (money >= amount)
        {
            money -= amount;
            UpdateMoneyUI();
            return true;
        }

        return false;
    }

    public void SetMoney(int amount)
    {
        CancelMoneyCharging();

        money = Mathf.Max(0, amount);
        previousMoney = money;   // żeby Update() nie nadpisało UI po chwili
        UpdateMoneyUI();
    }

    public void UpdateMoneyUI()
    {
        if (moneyText != null)
            moneyText.text = $"Cash: {money.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}$";

    }

    /* Funkcja do formatowania pieniędzy gdziekolwiek
        public string FormatCash(int amount)
        {
            return amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "$";
        }
    */

    public void AddMoneySmooth(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0) return;

        // jeśli nie ładujemy – start
        if (_moneyChargeCo == null)
        {
            _moneyChargeTarget = money + amount;
            _moneyChargeFloat = money;

            if (moneyText != null)
            {
                if (!_moneyColorCached)
                {
                    _moneyOriginalColor = moneyText.color;
                    _moneyColorCached = true;
                }
                moneyText.color = moneyChargingColor;
            }

            _moneyChargeCo = StartCoroutine(MoneyChargeRoutine());
        }
        else
        {
            // jeśli już ładujemy – dokładamy do celu
            _moneyChargeTarget += amount;
        }
    }

    private IEnumerator MoneyChargeRoutine()
    {
        float perSec = Mathf.Max(1f, moneyChargePerSecond);

        while (money < _moneyChargeTarget)
        {
            _moneyChargeFloat += perSec * Time.deltaTime;

            int newMoney = Mathf.Min(Mathf.FloorToInt(_moneyChargeFloat), _moneyChargeTarget);
            newMoney = Mathf.Max(newMoney, money);

            if (newMoney != money)
            {
                money = newMoney;
                UpdateMoneyUI();
            }

            yield return null;
        }

        money = _moneyChargeTarget;
        UpdateMoneyUI();

        if (moneyText != null && _moneyColorCached)
            moneyText.color = _moneyOriginalColor;

        _moneyChargeCo = null;
    }

    private void CancelMoneyCharging()
    {
        if (_moneyChargeCo != null)
        {
            StopCoroutine(_moneyChargeCo);
            _moneyChargeCo = null;
        }

        _moneyChargeTarget = money;
        _moneyChargeFloat = money;

        if (moneyText != null && _moneyColorCached)
            moneyText.color = _moneyOriginalColor;
    }


    public void TakeDamage(int damage)
    {
        if (CheatState.Invincible) return; // SAIYAN
        TakeDamage(damage, "Środowisko");
    }
    public void TakeDamage(int damage, string attackerName)
    {
        if (CheatState.Invincible) return; // SAIYAN
        if (IsDead) return;

        int remainingDamage = damage;
        LastAttackerName = attackerName;  // <- zapamiętaj

        Debug.Log($"🩸 Gracz otrzymał {damage} dmg od {attackerName}");

        if (currentArmor > 0)
        {
            int absorbed = Mathf.Min(currentArmor, damage);
            currentArmor -= absorbed;
            remainingDamage -= absorbed;
        }

        currentHP -= remainingDamage;
        currentHP = Mathf.Max(currentHP, 0);

        UpdateUI();

        if (currentHP <= 0)
        {
            Debug.Log("☠️ Gracz nie żyje!");
            OnDeath();
        }

        // 🔴 Czerwony overlay TYLKO jeśli to NIE jest skażenie
        if (DamageIndicatorUI.Instance && attackerName != "Contamination")
        {
            DamageIndicatorUI.Instance.TriggerFlash(damage);
        }

    }
    public void Heal(int amount, string source)
    {
        currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
        UpdateUI();
        Debug.Log($"❤️ Gracz uzdrowiony o {amount} przez {source}");
    }

    void OnDeath()
    {
        if (IsDead) return;
        IsDead = true;

        MouseLook.IsLookLocked = true; // 🔒 Zablokuj rozglądanie natychmiast

        var fallCam = GetComponent<FallImpactCamera>();
        if (fallCam != null)
            fallCam.DoTilt();

        // 🔁 Nadal przewróć gracza
        transform.rotation = Quaternion.Euler(90f, transform.rotation.eulerAngles.y, 0f);
        StartCoroutine(FallOver());

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (deathScreen != null)
            deathScreen.SetActive(true);

        if (deathMessage != null)
            deathMessage.text = "Gracz nie żyje";

        // powiadom NPC-ów kto zabił
        try { OnPlayerDied?.Invoke(LastAttackerName); } catch { /* no-op */ }

        StartCoroutine(DeathLogDelay());
    }


    IEnumerator FallOver()
    {
        float duration = 0.5f;
        float elapsed = 0f;

        Quaternion startRot = Camera.main.transform.localRotation;
        Quaternion endRot = Quaternion.Euler(0f, 0f, 90f);

        while (elapsed < duration && IsDead)  // 👈 sprawdzaj IsDead
        {
            elapsed += Time.deltaTime;
            Camera.main.transform.localRotation =
                Quaternion.Slerp(startRot, endRot, elapsed / duration);
            yield return null;
        }
    }

    public void AddArmor(int amount)
    {
        currentArmor = Mathf.Clamp(currentArmor + amount, 0, maxArmor);
        UpdateUI();
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
        UpdateUI();
    }

    void UpdateUI()
    {
        if (hpText != null)
            hpText.text = "HP: " + currentHP;

        if (armorText != null)
            armorText.text = "Armor: " + currentArmor;

    }

    // Logika oddychania pod wodą i duszenia
    public void SetUnderwaterState(bool underwater)
    {
        isUnderwater = underwater;

        if (oxygenCoroutine != null) StopCoroutine(oxygenCoroutine);
        oxygenCoroutine = StartCoroutine(OxygenManagement());

        if (!underwater && _tookDrownDamage)
        {
            // startuj leczenie do progu (np. 60%), jednorazowo
            if (_postDrownHealCo != null) StopCoroutine(_postDrownHealCo);
            _postDrownHealCo = StartCoroutine(PostDrownHeal());
            _tookDrownDamage = false;
        }

        if (oxygenRoot != null)
            if (underwater) oxygenRoot.SetActive(true);
            else StartCoroutine(HideOxygenBarAfterDelay());
    }

    private IEnumerator PostDrownHeal()
    {
        int target = Mathf.RoundToInt(maxHP * postDrownHealCap);
        target = Mathf.Max(target, currentHP); // nie cofaj, tylko ewentualnie podnieś do progu

        float tick = 0.5f;           // co pół sekundy
        int perTick = Mathf.Max(1, Mathf.RoundToInt(postDrownHealPerSec * tick));

        while (currentHP < target && !isUnderwater && !IsDead)
        {
            currentHP = Mathf.Min(target, currentHP + perTick);
            UpdateUI();
            yield return new WaitForSeconds(tick);
        }
    }

    IEnumerator HideOxygenBarAfterDelay()
    {
        while (_oxygen < oxygenMax)
            yield return null;

        yield return new WaitForSeconds(1.5f);

        if (!isUnderwater && oxygenRoot) oxygenRoot.SetActive(false);
    }

    IEnumerator OxygenManagement()
    {
        while (true)
        {
            if (isUnderwater)
            {
                _oxygen = Mathf.Max(0f, _oxygen - oxygenDrainPerSec * Time.deltaTime);

                if (_oxygen <= 0f)
                {
                    _drownTimer -= Time.deltaTime;
                    if (_drownTimer <= 0f)
                    {
                        int dmg = drowningDamageSeq[Mathf.Min(_drownStage, drowningDamageSeq.Length - 1)];
                        _drownStage = Mathf.Min(_drownStage + 1, drowningDamageSeq.Length - 1);
                        _drownTimer = drowningTick;
                        ApplyEnvironmentalDamage(dmg, "Drowning");
                    }
                }
                else
                {
                    _drownStage = 0;
                    _drownTimer = 0f;
                }
            }
            else
            {
                _oxygen = Mathf.MoveTowards(_oxygen, oxygenMax, oxygenRegenPerSec * Time.deltaTime);
                _drownStage = 0;
                _drownTimer = 0f;
            }

            // UI paska O2
            if (oxygenBar)
            {
                float fill = (oxygenMax > 0f) ? (_oxygen / oxygenMax) : 0f;
                oxygenBar.fillAmount = Mathf.Clamp01(fill);

                // kolor: jasny niebieski -> zielony -> żółty -> pomarańcz -> czerwony
                Color cCyan = new Color(0.65f, 0.92f, 1f);
                Color cGreen = Color.green;
                Color cYellow = Color.yellow;
                Color cOrange = new Color(1f, 0.5f, 0f);
                Color cRed = Color.red;

                Color col;
                if (fill > 0.80f) col = Color.Lerp(cGreen, cCyan, (fill - 0.80f) / 0.20f);
                else if (fill > 0.60f) col = Color.Lerp(cYellow, cGreen, (fill - 0.60f) / 0.20f);
                else if (fill > 0.35f) col = Color.Lerp(cOrange, cYellow, (fill - 0.35f) / 0.25f);
                else col = Color.Lerp(cRed, cOrange, fill / 0.35f);

                oxygenBar.color = col;
            }


            yield return null;
        }
    }
    public void ApplyEnvironmentalDamage(int damage, string reason)
    {
        TakeDamage(damage, reason);

        if (reason == "Drowning")
            _tookDrownDamage = true;

        if (!DamageIndicatorUI.Instance) return;

        var ui = DamageIndicatorUI.Instance;

        if (reason == "Contamination")
        {
            // ✔ szarpnięcie kamerą jak przy upadku
            ui.TriggerHitTilt(damage);

            // ✔ zielone strzałki, dłużej
            ui.TriggerAllColored(
                damage,
                ui.toxicArrowColor,
                ui.toxicArrowTimeMultiplier
            );
        }
        else
        {
            ui.TriggerAll(damage, alsoFlashOverlay: true);
        }
    }

    IEnumerator DeathLogDelay()
    {
        yield return new WaitForSeconds(5f);
        if (IsDead)                    // 👈 tylko jeśli nadal martwy
            Debug.Log("🪦 Gracz nie żyje");
    }

    /// <summary>
    /// // SAVE & LOAD SYSTEM
    /// </summary>
    [System.Serializable]
    public struct PlayerStatsSnapshot
    {
        public int health;
        public int armor;
        public float stamina;
        public float underwaterStamina;

        public int money;   // 💰 NOWE
    }

    public PlayerStatsSnapshot GetSnapshot()
    {
        PlayerStatsSnapshot s = new PlayerStatsSnapshot();

        s.health = currentHP;
        s.armor = currentArmor;

        var pm = GetComponent<PlayerMovement>();
        if (pm != null)
            s.stamina = pm.CurrentStamina;

        s.underwaterStamina = _oxygen;

        s.money = money;     // 💰 DODANE

        return s;
    }
    public void ApplySnapshot(PlayerStatsSnapshot s)
    {
        CancelMoneyCharging(); // ✅ ważne, żeby nie nadpisywało po chwili

        currentHP = s.health;
        currentArmor = s.armor;

        UpdateUI();

        var pm = GetComponent<PlayerMovement>();
        if (pm != null)
            pm.ForceSetStamina(s.stamina);

        _oxygen = s.underwaterStamina;

        money = s.money;
        previousMoney = money;   // ✅ spójność z resztą (miałeś to w SetMoney)
        UpdateMoneyUI();
    }

    public void ResetDeathStateAfterLoad()
    {
        // odblokuj logikę
        IsDead = false;
        LastAttackerName = "";

        // odblokuj rozglądanie
        MouseLook.IsLookLocked = false;

        // przywróć kursor do stanu gry
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // schowaj ekran śmierci
        if (deathScreen != null)
            deathScreen.SetActive(false);

        // postaw ciało z powrotem
        transform.rotation = Quaternion.Euler(
            0f,
            transform.rotation.eulerAngles.y,
            0f
        );

        // wyprostuj kamerę (kasujemy tilt z FallOver)
        if (Camera.main != null)
            Camera.main.transform.localRotation = Quaternion.identity;
    }

    public void AssignCitizenID(string newId)
    {
        citizenId = newId;
        Debug.Log($"[CITIZEN ID] Assigned: {citizenId}");
    }


}