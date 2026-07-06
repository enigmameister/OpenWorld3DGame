using UnityEngine;

public class ExhaustBackfireFX : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CarControll car;

    [Header("Exhaust Particles")]
    [SerializeField] private ParticleSystem leftExhaustFx;
    [SerializeField] private ParticleSystem rightExhaustFx;

    [Header("Rules")]
    [SerializeField] private int minGearForBackfire = 2;
    [SerializeField] private float minSpeedKPH = 15f;
    [SerializeField] private float cooldown = 0.08f;

    [Header("Burst")]
    [SerializeField] private int burstCount = 12;
    [SerializeField] private bool randomizeBurst = true;
    [SerializeField] private Vector2Int randomBurstRange = new Vector2Int(8, 18);

    private float nextAllowedTime;

    void Awake()
    {
        if (car == null)
            car = GetComponent<CarControll>();
    }

    void OnEnable()
    {
        if (car != null)
            car.OnGearShiftUp += HandleGearShiftUp;
    }

    void OnDisable()
    {
        if (car != null)
            car.OnGearShiftUp -= HandleGearShiftUp;
    }

    void HandleGearShiftUp(int oldGear, int newGear)
    {
        if (Time.time < nextAllowedTime)
            return;

        if (car == null)
            return;

        if (newGear < minGearForBackfire)
            return;

        if (car.currentSpeedKPH < minSpeedKPH)
            return;

        nextAllowedTime = Time.time + cooldown;

        int count = randomizeBurst
            ? Random.Range(randomBurstRange.x, randomBurstRange.y + 1)
            : burstCount;

        PlayBurst(leftExhaustFx, count);
        PlayBurst(rightExhaustFx, count);
    }

    void PlayBurst(ParticleSystem fx, int count)
    {
        if (fx == null)
            return;

        fx.Emit(count);
    }
}