using TMPro;
using UnityEngine;
using System.Collections;
using System;

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Citizen ID")]
    [SerializeField] public string citizenId;
    public bool HasCitizenID => !string.IsNullOrEmpty(citizenId);

    public string CitizenID => citizenId;

    [Header("Player stats")]
    public int maxHP = 100;
    public int maxArmor = 100;

    public int currentHP;
    public int currentArmor = 0;
    public bool IsDead { get; private set; } = false;
    public GameObject deathScreen; 
    public TMP_Text deathMessage;
    public static event Action<string> OnPlayerDied;   
    public string LastAttackerName { get; private set; } = "";

    [Header("UI")]
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI armorText;

    // ========= UNDERWATER / OXYGEN =========
    [Header("Underwater / Oxygen")]
    public float oxygenMax = 12f;             // Oxygen full
    public float oxygenDrainPerSec = 1.0f;    // Drain underwater
    public float oxygenRegenPerSec = 4.0f;    // Regen Oxygen

    [Tooltip("Damage freq during drowning")]
    public float drowningTick = 1.0f;

    [Tooltip("Damage sequence underwater")]

    public int[] drowningDamageSeq = new[] { 5, 10, 20, 30 };
    [Range(0f, 1f)] public float postDrownHealCap = 0.60f;  
    [Range(0.5f, 5f)] public float postDrownHealPerSec = 12f; 
    private bool _tookDrownDamage;
    private Coroutine _postDrownHealCo;

    [SerializeField] private UnityEngine.UI.Image oxygenBar;   
    [SerializeField] private GameObject oxygenRoot;             

    [HideInInspector] public bool isUnderwater = false;
    [Range(0f, 30f)] public float postDrownRegenPerSec = 4f;

    private float _oxygen;
    private float _drownTimer;
    private int _drownStage;
    private Coroutine oxygenCoroutine;

    public TextMeshProUGUI moneyText;

    [Header("Money")]
    public int money = 0;
    private int previousMoney;

    [Header("Money charging")]
    [Tooltip("How much money charge per second?")]
    public float moneyChargePerSecond = 250f;

    [Tooltip("Money text color druing charing")]
    public Color moneyChargingColor = Color.yellow;

    [Tooltip("Color after charged money in InventoryUI")]
    public Color moneyNormalColor = new Color(0, 0, 0, 0);

    private Coroutine _moneyChargeCo;
    private int _moneyChargeTarget;
    private float _moneyChargeFloat;

    private Color _moneyOriginalColor;
    private bool _moneyColorCached;

    void Start()
    {
        currentHP = maxHP;
        currentArmor = 0;
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
        previousMoney = money;
        UpdateMoneyUI();
    }

    public void UpdateMoneyUI()
    {
        if (moneyText != null)
            moneyText.text = $"Cash: {money.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}$";

    }

    public void AddMoneySmooth(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0) return;

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
        TakeDamage(damage, "Enviornment");
    }

    public void TakeDamage(int damage, string attackerName)
    {
        if (CheatState.Invincible) return; // SAIYAN
        if (IsDead) return;

        int remainingDamage = damage;
        LastAttackerName = attackerName;  

        Debug.Log($"🩸 Player received {damage} dmg from {attackerName}");

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

        MouseLook.IsLookLocked = true; // Freelock block

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
            deathMessage.text = "Player Died";

        // powiadom NPC-ów kto zabił
        try { OnPlayerDied?.Invoke(LastAttackerName); } catch {  }

        StartCoroutine(DeathLogDelay());
    }

    IEnumerator FallOver()
    {
        float duration = 0.5f;
        float elapsed = 0f;

        Quaternion startRot = Camera.main.transform.localRotation;
        Quaternion endRot = Quaternion.Euler(0f, 0f, 90f);

        while (elapsed < duration && IsDead)
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

    public void SetUnderwaterState(bool underwater)
    {
        isUnderwater = underwater;

        if (oxygenCoroutine != null) StopCoroutine(oxygenCoroutine);
        oxygenCoroutine = StartCoroutine(OxygenManagement());

        if (!underwater && _tookDrownDamage)
        {
       
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
        target = Mathf.Max(target, currentHP); 

        float tick = 0.5f;           
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
            ui.TriggerHitTilt(damage);
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
        if (IsDead)
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

        public int money;  
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

        s.money = money;     

        return s;
    }
    public void ApplySnapshot(PlayerStatsSnapshot s)
    {
        CancelMoneyCharging(); 

        currentHP = s.health;
        currentArmor = s.armor;

        UpdateUI();

        var pm = GetComponent<PlayerMovement>();
        if (pm != null)
            pm.ForceSetStamina(s.stamina);

        Debug.Log($"[QuickLoad] Restore stamina={s.stamina}");

        _oxygen = s.underwaterStamina;

        money = s.money;
        previousMoney = money;   
        UpdateMoneyUI();
    }

    public void ResetDeathStateAfterLoad()
    {
        // Unlock logic
        IsDead = false;
        LastAttackerName = "";

        // Unlock freelock
        MouseLook.IsLookLocked = false;

        // Restore cursor 
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Hide death screen
        if (deathScreen != null)
            deathScreen.SetActive(false);

        // Restore Body Position
        transform.rotation = Quaternion.Euler(
            0f,
            transform.rotation.eulerAngles.y,
            0f
        );

        // Reset cam
        if (Camera.main != null)
            Camera.main.transform.localRotation = Quaternion.identity;
    }

    public void AssignCitizenID(string newId)
    {
        citizenId = newId;
        Debug.Log($"[CITIZEN ID] Assigned: {citizenId}");
    }


}