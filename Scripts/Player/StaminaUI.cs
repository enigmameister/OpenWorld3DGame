using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StaminaUI : MonoBehaviour
{
    public GameObject staminaRoot;
    public Image staminaFill;
    public TextMeshProUGUI staminaText;
    public PlayerMovement playerMovement;

    [Header("Ustawienia")]
    public float hideDelay = 5f;
    private float lastStamina = -1f;
    private float hideTimer = 0f;

    private Color fullColor = Color.green;
    private Color midColor = Color.yellow;
    private Color emptyColor = Color.red;

    void Start()
    {
        if (staminaRoot != null)
            staminaRoot.SetActive(false);

        if (playerMovement != null)
            lastStamina = playerMovement.MaxStamina;
    }


    void Update()
    {
        if (playerMovement == null) return;

        float stamina = playerMovement.CurrentStamina;
        float maxStamina = playerMovement.MaxStamina;
        float normalized = maxStamina > 0f ? stamina / maxStamina : 0f;

        // Kolor
        Color color = normalized > 0.5f
            ? Color.Lerp(midColor, fullColor, (normalized - 0.5f) * 2f)
            : Color.Lerp(emptyColor, midColor, normalized * 2f);

        staminaFill.fillAmount = normalized;
        staminaFill.color = color;

        if (staminaText != null)
            staminaText.text = $"Stamina: {Mathf.CeilToInt(stamina)}";

        // ——— NOWA LOGIKA WIDOCZNOŚCI ———
        bool staminaDroppedThisFrame = (lastStamina >= 0f) && (stamina < lastStamina - 0.01f);
        bool staminaNotFull = stamina < maxStamina - 0.01f;

        bool shouldShow =
            playerMovement.IsTryingToSprint ||      // sprint
            staminaDroppedThisFrame ||            // np. skok, atak itp.
            staminaNotFull;                          // regeneracja po spadku

        if (shouldShow)
        {
            if (!staminaRoot.activeSelf)
                staminaRoot.SetActive(true);
            hideTimer = hideDelay;                   // trzyma pasek przez chwilę
        }
        else
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f && staminaRoot.activeSelf)
                staminaRoot.SetActive(false);
        }

        // zapamiętaj na kolejną klatkę
        lastStamina = stamina;
    }

}
