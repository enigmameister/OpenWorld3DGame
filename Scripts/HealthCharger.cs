using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthCharger : MonoBehaviour, IPressable, IHoldInteractable
{
    [Header("Health Charger")]
    [SerializeField] private int maxAvailableHP = 100;
    [SerializeField] private int availableHP = 100;

    [Tooltip("Ile HP na sekundê leczy apteczka.")]
    [SerializeField] private float healPerSecond = 25f;

    [Header("Player")]
    [SerializeField] private PlayerStats playerStats;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI availableHPText;
    [SerializeField] private Image currentBar;

    [Header("Status Objects")]
    [SerializeField] private GameObject statusReady;
    [SerializeField] private GameObject statusEmpty;
    [SerializeField] private GameObject statusIdle;
    [SerializeField] private GameObject statusPass;

    [Header("Audio optional")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip healLoopClip;
    [SerializeField] private AudioClip emptyClip;
    [SerializeField] private AudioClip fullHpClip;

    private float healAccumulator;
    private bool isHolding;

    public string Label
    {
        get
        {
            if (availableHP <= 0)
                return "Health charger empty";

            if (playerStats != null && playerStats.currentHP >= playerStats.maxHP)
                return "Health full";

            return "Hold to heal";
        }
    }

    public string HoldLabel => Label;

    public bool CanHoldInteract =>
        availableHP > 0 &&
        playerStats != null &&
        !playerStats.IsDead &&
        playerStats.currentHP < playerStats.maxHP;

    private void Awake()
    {
        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();

        availableHP = Mathf.Clamp(availableHP, 0, maxAvailableHP);

        UpdateVisuals();
    }

    private void OnValidate()
    {
        maxAvailableHP = Mathf.Max(1, maxAvailableHP);
        availableHP = Mathf.Clamp(availableHP, 0, maxAvailableHP);
        healPerSecond = Mathf.Max(1f, healPerSecond);
    }

    public void Press()
    {
        // Zostawiamy puste, bo ta apteczka dzia³a na HOLD.
        // Dziêki temu nadal jest kompatybilna z IPressable i PickupInteractor j¹ wykryje.
    }

    public void HoldStarted()
    {
        isHolding = true;
        healAccumulator = 0f;

        if (CanHoldInteract)
        {
            SetStatus(statusPass);

            if (audioSource != null && healLoopClip != null)
            {
                audioSource.clip = healLoopClip;
                audioSource.loop = true;

                if (!audioSource.isPlaying)
                    audioSource.Play();
            }
        }
        else
        {
            PlayOneShotStateSound();
            UpdateVisuals();
        }
    }

    public void HoldTick(float deltaTime)
    {
        if (!CanHoldInteract)
        {
            StopHealingSound();
            UpdateVisuals();
            return;
        }

        float rawHeal = healPerSecond * deltaTime;
        healAccumulator += rawHeal;

        int healPoints = Mathf.FloorToInt(healAccumulator);

        if (healPoints <= 0)
            return;

        healAccumulator -= healPoints;

        int missingHP = playerStats.maxHP - playerStats.currentHP;
        int realHeal = Mathf.Min(healPoints, missingHP, availableHP);

        if (realHeal <= 0)
        {
            StopHealingSound();
            UpdateVisuals();
            return;
        }

        availableHP -= realHeal;

        playerStats.Heal(realHeal, "Health Charger");

        UpdateVisuals();

        if (availableHP <= 0 || playerStats.currentHP >= playerStats.maxHP)
            StopHealingSound();
    }

    public void HoldEnded()
    {
        isHolding = false;
        healAccumulator = 0f;

        StopHealingSound();
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (availableHPText != null)
            availableHPText.text = availableHP.ToString();

        if (currentBar != null)
            currentBar.fillAmount = maxAvailableHP > 0
                ? (float)availableHP / maxAvailableHP
                : 0f;

        if (availableHP <= 0)
        {
            SetStatus(statusEmpty);
            return;
        }

        if (isHolding && CanHoldInteract)
        {
            SetStatus(statusPass);
            return;
        }

        SetStatus(statusReady != null ? statusReady : statusIdle);
    }

    private void SetStatus(GameObject activeStatus)
    {
        if (statusReady != null)
            statusReady.SetActive(activeStatus == statusReady);

        if (statusEmpty != null)
            statusEmpty.SetActive(activeStatus == statusEmpty);

        if (statusIdle != null)
            statusIdle.SetActive(activeStatus == statusIdle);

        if (statusPass != null)
            statusPass.SetActive(activeStatus == statusPass);
    }

    private void StopHealingSound()
    {
        if (audioSource != null && audioSource.loop)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
        }
    }

    private void PlayOneShotStateSound()
    {
        if (audioSource == null)
            return;

        if (availableHP <= 0 && emptyClip != null)
            audioSource.PlayOneShot(emptyClip);

        if (playerStats != null && playerStats.currentHP >= playerStats.maxHP && fullHpClip != null)
            audioSource.PlayOneShot(fullHpClip);
    }

    public int GetAvailableHP()
    {
        return availableHP;
    }

    public void SetAvailableHP(int value)
    {
        availableHP = Mathf.Clamp(value, 0, maxAvailableHP);
        UpdateVisuals();
    }
}