using UnityEngine;

public class FatigueBreathCamera : MonoBehaviour
{
    public enum ApplyTarget { MainCamera, ThisTransform }
    [Header("Target")]
    public ApplyTarget applyTo = ApplyTarget.MainCamera;

    [Header("Profiles (realistyczny oddech)")]
    [Tooltip("czas wdechu / pauzy / wydechu (sekundy) przy maks. zmęczeniu")]
    public float inhaleTime = 1.1f;
    public float holdTime = 0.25f;
    public float exhaleTime = 1.6f;

    [Tooltip("amplituda ruchu (metry) i roll (stopnie) przy maks. zmęczeniu")]
    public float posAmp = 0.055f;
    public float rollAmp = 2.6f;

    [Tooltip("puls FOV przy maks. zmęczeniu")]
    public float fovPulse = 2.8f;

    [Header("Krzywe kształtu (0..1)")]
    public AnimationCurve inhaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve exhaleCurve = new AnimationCurve(
        new Keyframe(0, 1, 0, -3),
        new Keyframe(1, 0, -1, 0)
    );

    [Header("Audio (opcjonalnie)")]
    public AudioSource audioSource;         // podczep do MainCamera
    public AudioClip inhaleSfx;
    public AudioClip exhaleSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.6f;
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    Transform t;
    Camera cam;
    Vector3 baseLocalPos;
    Quaternion baseLocalRot;
    float baseFov;

    enum Phase { Inhale, Hold, Exhale, Idle }
    Phase phase = Phase.Idle;
    float phaseTimer = 0f;

    float fatigue01; // 0..1 (1 = bardzo zmęczony)

    void Awake()
    {
        t = (applyTo == ApplyTarget.MainCamera && Camera.main) ? Camera.main.transform : transform;
        cam = t.GetComponent<Camera>();
        baseLocalPos = t.localPosition;
        baseLocalRot = t.localRotation;
        baseFov = cam ? cam.fieldOfView : 60f;
    }

    /// <summary>
    /// Wywołuj co klatkę. fatigue01: 0..1 (1 = maks. zmęczenie).
    /// </summary>
    public void UpdateBreath(float fatigue01, float dt)
    {
        this.fatigue01 = Mathf.Clamp01(fatigue01);

        if (this.fatigue01 > 0.01f)
        {
            if (phase == Phase.Idle) StartInhale();
            StepBreath(dt);
        }
        else
        {
            // powrót do bazowych wartości
            phase = Phase.Idle;
            phaseTimer = 0f;
            SmoothReturn(dt);
        }
    }

    void StartInhale()
    {
        phase = Phase.Inhale;
        phaseTimer = 0f;
        Play(inhaleSfx);
    }

    void StartHold()
    {
        phase = Phase.Hold;
        phaseTimer = 0f;
    }

    void StartExhale()
    {
        phase = Phase.Exhale;
        phaseTimer = 0f;
        Play(exhaleSfx);
    }

    void StepBreath(float dt)
    {
        float f = EaseByFatigue(1f); // im większa fatyga tym większa amplituda i krótsza faza

        switch (phase)
        {
            case Phase.Inhale:
                {
                    float dur = Mathf.Lerp(inhaleTime * 1.6f, inhaleTime, fatigue01);
                    phaseTimer += dt;
                    float x = Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, dur));
                    float y = inhaleCurve.Evaluate(x); // 0→1
                    ApplyOffsets(y * f, dt);
                    if (x >= 1f) StartHold();
                }
                break;

            case Phase.Hold:
                {
                    float dur = Mathf.Lerp(holdTime * 1.5f, holdTime, fatigue01);
                    phaseTimer += dt;
                    ApplyOffsets(1f * f, dt); // trzymamy szczyt
                    if (phaseTimer >= dur) StartExhale();
                }
                break;

            case Phase.Exhale:
                {
                    float dur = Mathf.Lerp(exhaleTime * 1.6f, exhaleTime, fatigue01);
                    phaseTimer += dt;
                    float x = Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, dur));
                    float y = exhaleCurve.Evaluate(x); // 1→0
                    ApplyOffsets(y * f, dt);
                    if (x >= 1f) StartInhale();
                }
                break;
        }
    }

    void ApplyOffsets(float breath01, float dt)
    {
        // pozycja (Y w górę przy wdechu), roll
        float ampPos = posAmp * fatigue01;
        float ampRoll = rollAmp * fatigue01;

        Vector3 targetPos = baseLocalPos + new Vector3(0f, ampPos * breath01, 0f);
        float targetRoll = ampRoll * breath01; // rośnie na wdechu, opada na wydechu

        t.localPosition = Vector3.Lerp(t.localPosition, targetPos, dt * 8f);
        t.localRotation = Quaternion.Slerp(t.localRotation, baseLocalRot * Quaternion.Euler(0f, 0f, targetRoll), dt * 8f);

        // FOV „pulse” – minimalny, by nie męczyć oczu
        if (cam)
        {
            float targetFov = baseFov + fovPulse * fatigue01 * (breath01 - 0.5f) * 2f; // -fov..+fov
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, dt * 6f);
        }
    }

    void SmoothReturn(float dt)
    {
        t.localPosition = Vector3.Lerp(t.localPosition, baseLocalPos, dt * 6f);
        t.localRotation = Quaternion.Slerp(t.localRotation, baseLocalRot, dt * 6f);
        if (cam) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov, dt * 6f);
    }

    float EaseByFatigue(float x) => x; // hook pod ewentualne nieliniowości

    void Play(AudioClip clip)
    {
        if (!audioSource || !clip) return;
        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip, sfxVolume * Mathf.Lerp(0.35f, 1f, fatigue01));
    }

    // Użyteczne w edytorze:
    public void ResetBase()
    {
        if (!t) t = transform;
        if (!cam) cam = GetComponent<Camera>();
        baseLocalPos = t.localPosition;
        baseLocalRot = t.localRotation;
        if (cam) baseFov = cam.fieldOfView;
    }
}
