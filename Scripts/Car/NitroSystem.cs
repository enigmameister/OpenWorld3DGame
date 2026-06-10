using UnityEngine;

public class NitroSystem : MonoBehaviour
{
    [Header("Nitro Settings")]
    public float maxNitro = 100f;
    public float currentNitro = 100f;
    public float nitroUsagePerSecond = 25f;
    public float nitroRegenPerSecond = 15f;
    public float speedThreshold = 80f;
    public float nitroBoostMultiplier = 1.5f;
    public float regenDelay = 3f; // czas po którym można zacząć ładować nitro

    [HideInInspector] public bool isUsingNitro = false;

    [Header("Efekty wizualne")]
    public ParticleSystem nitroLeft;
    public ParticleSystem nitroRight;

    private CarControll carControl;
    private Rigidbody rb;
    private InputActions input;
    private float regenTimer = 0f;

    [Header("AI Nitro")]
    public bool useExternalNitroInput = false;
    [Range(0f, 1f)] public float externalNitroInput = 0f;

    public void SetExternalNitroInput(float value)
    {
        externalNitroInput = Mathf.Clamp01(value);
    }

    [HideInInspector] public bool nitroLocked = false;

    void Awake()
    {
        input = new InputActions();
    }

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();

    void Start()
    {
        carControl = GetComponent<CarControll>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        float nitroInput = useExternalNitroInput
            ? externalNitroInput
            : input.Car.Nitro.ReadValue<float>();

        float throttleInput = useExternalNitroInput ? 1f : input.Car.Movement.ReadValue<Vector2>().y;
        float speedKPH = rb.linearVelocity.magnitude * 3.6f;

        // Sprawdź warunki użycia
        isUsingNitro = nitroInput > 0 && throttleInput > 0.1f && currentNitro > 0 && !carControl.isReversing;

        if (nitroLocked)
        {
            isUsingNitro = false;

            if (nitroLeft != null && nitroLeft.isPlaying) nitroLeft.Stop();
            if (nitroRight != null && nitroRight.isPlaying) nitroRight.Stop();
            return;
        }

        // Zużycie
        if (isUsingNitro)
        {
            currentNitro -= nitroUsagePerSecond * Time.deltaTime;
            regenTimer = 0f; // resetuj licznik ładowania

            if (nitroLeft != null && !nitroLeft.isPlaying) nitroLeft.Play();
            if (nitroRight != null && !nitroRight.isPlaying) nitroRight.Play();
        }
        else
        {
            if (nitroLeft != null && nitroLeft.isPlaying) nitroLeft.Stop();
            if (nitroRight != null && nitroRight.isPlaying) nitroRight.Stop();

            regenTimer += Time.deltaTime;

            if (regenTimer >= regenDelay && speedKPH > speedThreshold)
            {
                currentNitro += nitroRegenPerSecond * Time.deltaTime;
            }
        }

        currentNitro = Mathf.Clamp(currentNitro, 0f, maxNitro);
    }

    public float GetNitroNormalized()
    {
        return currentNitro / maxNitro;
    }

    public float GetBoostMultiplier()
    {
        return isUsingNitro ? nitroBoostMultiplier : 1f;
    }

    public void RefillNitro()
    {
        currentNitro = maxNitro;
        regenTimer = 0f;
        isUsingNitro = false;

        if (nitroLeft != null) nitroLeft.Stop();
        if (nitroRight != null) nitroRight.Stop();
    }
}
