using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DamageIndicatorUI : MonoBehaviour
{
    public static DamageIndicatorUI Instance { get; private set; }

    [Header("HL Overlay (pełnoekranowy flash/vignette)")]
    public Image overlay;
    [Range(0, 1f)] public float maxAlpha = 0.7f;
    public float alphaPerDamage = 0.025f;     // ile alfy na 1 dmg
    public float overlayFadeSpeed = 3.5f;     // szybkość wygaszania
    public Color hitColor = new Color(1f, 0f, 0f, 0.7f);
    public Color fallColor = new Color(1f, 0.35f, 0f, 0.7f);

    [Header("8-kierunkowe strzałki (opcjonalne)")]
    public Image North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest;
    public float arrowFlashTime = 0.75f;
    public AnimationCurve arrowAlphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public float damageToArrowAlpha = 0.02f;  // 50 dmg → ~1.0

    [Header("Specjalne kolory")]
    public Color toxicArrowColor = new Color(0f, 1f, 0f, 0.7f);
    public float toxicArrowTimeMultiplier = 2f;   // ile razy dłużej świecą strzałki przy zatruciu

    [Header("Biohazard (skażenie)")]
    public Image biohazardIcon;
    public float biohazardFadeTime = 0.35f;

    private Coroutine _biohazardRoutine;
    private Color _biohazardBaseColor;

    [Header("Referencje")]
    public Transform player;                  // jeśli puste – znajdziemy po Tag=Player

    [Header("Hit Tilt (jak FallImpactCamera)")]
    public float hitTiltMax = 8f;         // max kąt dla bardzo dużych obrażeń
    public float hitTiltScale = 1f;       // globalny mnożnik
    public float hitTiltDamageNorm = 50f; // 50 dmg ≈ pełen efekt
    private FallImpactCamera _impactCam;

    // internals
    private readonly Dictionary<Image, Coroutine> _arrowRoutines = new();
    private readonly Dictionary<Image, Color> _arrowBaseColors = new(); // bazowe kolory strzałek
    private float _overlayTargetA = 0f;
    private float _overlayHoldUntil = 0f;


    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!player)
        {
            var pl = GameObject.FindGameObjectWithTag("Player");
            if (pl) player = pl.transform;
        }

        // ⬇️ Szukamy FallImpactCamera (najczęściej na CameraHolder albo MainCamera)

        if (_impactCam == null && Camera.main)
            _impactCam = Camera.main.GetComponent<FallImpactCamera>();

        // start: overlay i strzałki przezroczyste
        if (overlay)
        {
            overlay.raycastTarget = false;
            var c = overlay.color; c.a = 0f; overlay.color = c;
        }
        foreach (var img in GetAllArrows())
        {
            if (!img) continue;
            img.raycastTarget = false;

            // zapamiętaj bazowy kolor (np. czerwony) – bez zmian RGB, tylko alfa będzie sterowana
            _arrowBaseColors[img] = img.color;

            SetAlpha(img, 0f);
        }

        if (biohazardIcon)
        {
            _biohazardBaseColor = biohazardIcon.color;

            // startowo schowany (alfa 0 + inactive)
            var c = _biohazardBaseColor;
            c.a = 0f;
            biohazardIcon.color = c;
            biohazardIcon.gameObject.SetActive(false);
        }


    }

    void Update()
    {
        // fade overlay
        if (overlay)
        {
            var c = overlay.color;

            // jeśli mamy "hold", to przez ten czas nie zmieniamy alfy
            if (Time.time >= _overlayHoldUntil)
            {
                c.a = Mathf.MoveTowards(c.a, _overlayTargetA, Time.deltaTime * overlayFadeSpeed);
                overlay.color = c;

                if (Mathf.Approximately(c.a, _overlayTargetA))
                    _overlayTargetA = 0f; // po dojściu – gaś do zera
            }
            else
            {
                // trzymamy aktualną alfę (nic nie robimy)
                overlay.color = c;
            }
        }
    }


    private void ApplyDamageTilt(float signedSide, float damage)
    {
        if (_impactCam == null) return;

        // damage 0..∞ => 0..1
        float t = Mathf.Clamp01(damage / Mathf.Max(1f, hitTiltDamageNorm));
        float angle = hitTiltMax * hitTiltScale * t;

        // dodatni signedSide = uderzenie z prawej → przechył w prawo (jeśli czucie będzie odwrotne, pomnóż przez -1f)
        _impactCam.DoTiltSigned(angle * Mathf.Sign(signedSide));
    }

    // publiczny helper, żeby z zewnątrz wywołać "szarpnięcie" kamery
    public void TriggerHitTilt(float damage)
    {
        // losowo w lewo/prawo, jak przy upadku/AoE
        float side = (Random.value < 0.5f) ? -1f : 1f;
        ApplyDamageTilt(side, damage * 0.8f);
    }

    // ========= PUBLIC API (kompatybilne) =========

    /// <summary>KLASYK: kierunek z pozycji w świecie (pocisk/cios). Domyślnie TYLKO strzałka (overlay robi PlayerStats.TriggerFlash).</summary>
    public void TriggerFromWorld(Vector3 attackerWorldPos, float damage, bool alsoFlashOverlay = false)
    {
        // 8‑kierunkowy wskaźnik (jeśli mamy player i przypięte grafiki)
        if (player && HasAnyArrow())
        {
            Vector3 toAtt = attackerWorldPos - player.position;
            toAtt.y = 0f;
            if (toAtt.sqrMagnitude < 0.0001f) toAtt = player.forward;
            toAtt.Normalize();

            Vector3 f = player.forward; f.y = 0; f.Normalize();
            Vector3 r = player.right; r.y = 0; r.Normalize();

            float x = Vector3.Dot(r, toAtt);
            float z = Vector3.Dot(f, toAtt);
            float angle = Mathf.Atan2(x, z) * Mathf.Rad2Deg; // 0=N, 90=E...

            Image img = AngleToImage(angle);
            ApplyDamageTilt(x, damage);
            FlashArrow(img, damage);
        }

        if (alsoFlashOverlay)
            TriggerFlash(Mathf.RoundToInt(damage)); // HL‑flash (opcjonalnie)
    }

    /// <summary>Upadek / AoE – wszystkie kierunki. Domyślnie TYLKO strzałki (overlay robi PlayerStats.TriggerFlash).</summary>
    public void TriggerAll(float damage, bool alsoFlashOverlay = false)
    {
        ApplyDamageTilt(Random.value < 0.5f ? -1f : 1f, damage * 0.8f);

        if (HasAnyArrow())
        {
            foreach (var img in GetAllArrows())
                FlashArrow(img, damage);
        }

        if (alsoFlashOverlay)
            TriggerFlash(Mathf.RoundToInt(damage), fallColor);
    }

    /// <summary>
    /// Strzałki we wszystkich kierunkach, z nadpisanym kolorem i dłuższym czasem.
    /// Bez overlay’a – idealne np. dla skażenia.
    /// </summary>
    public void TriggerAllColored(float damage, Color arrowColor, float timeMultiplier = 1f, bool applyTilt = false)
    {
        if (applyTilt)
        {
            // ten sam efekt co przy upadku/AoE
            ApplyDamageTilt(Random.value < 0.5f ? -1f : 1f, damage * 0.8f);
        }

        if (HasAnyArrow())
        {
            foreach (var img in GetAllArrows())
                FlashArrow(img, damage, timeMultiplier, arrowColor);
        }
    }

    /// <summary>Half-Life flash: pełnoekranowy overlay, kolor zależny od typu.</summary>
    public void TriggerFlash(int damage, Color? overrideColor = null, float holdTime = 0f)
    {
        if (!overlay) return;

        Color baseCol = overrideColor ?? hitColor;
        overlay.color = new Color(baseCol.r, baseCol.g, baseCol.b, overlay.color.a);

        float add = Mathf.Clamp01(damage * alphaPerDamage);
        _overlayTargetA = Mathf.Clamp01(Mathf.Max(_overlayTargetA, add));
        _overlayTargetA = Mathf.Min(_overlayTargetA, maxAlpha);

        if (holdTime > 0f)
        {
            // trzymamy minimum do tego czasu (jeśli już był ustawiony dalej – nie skracamy)
            _overlayHoldUntil = Mathf.Max(_overlayHoldUntil, Time.time + holdTime);
        }
    }


    // ========= helpers =========

    private IEnumerable<Image> GetAllArrows()
    {
        yield return North; yield return NorthEast; yield return East; yield return SouthEast;
        yield return South; yield return SouthWest; yield return West; yield return NorthWest;
    }
    private bool HasAnyArrow()
    {
        foreach (var img in GetAllArrows()) if (img) return true;
        return false;
    }
    private static void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }

    private Image AngleToImage(float signedAngleDeg)
    {
        float a = Mathf.Repeat(signedAngleDeg + 360f, 360f);

        float[] mids = { 0, 45, 90, 135, 180, 225, 270, 315 };
        Image[] imgs = { North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest };

        int best = 0;
        float bestDiff = float.MaxValue;
        for (int i = 0; i < mids.Length; i++)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(a, mids[i]));
            if (d < bestDiff) { bestDiff = d; best = i; }
        }
        return imgs[best];
    }

    private void FlashArrow(Image img, float damage, float timeMultiplier = 1f, Color? overrideColor = null)
    {
        if (!img) return;

        if (_arrowRoutines.TryGetValue(img, out var running) && running != null)
            StopCoroutine(running);

        // jeśli chcemy inny kolor (np. zielony przy skażeniu)
        if (overrideColor.HasValue)
        {
            Color c = overrideColor.Value;
            img.color = new Color(c.r, c.g, c.b, img.color.a); // zmieniamy RGB, alfa będzie sterowana w coroutine
        }

        float targetA = Mathf.Clamp01(damage * damageToArrowAlpha);
        targetA = Mathf.Max(0.35f, targetA); // minimalna widoczność

        float duration = arrowFlashTime * Mathf.Max(0.01f, timeMultiplier);

        _arrowRoutines[img] = StartCoroutine(ArrowRoutine(img, targetA, duration));
    }

    private IEnumerator ArrowRoutine(Image img, float targetA, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float a = arrowAlphaCurve.Evaluate(1f - k) * targetA;
            SetAlpha(img, a);
            yield return null;
        }

        // wygaszone
        SetAlpha(img, 0f);

        // przywróć bazowy kolor (np. czerwony) z alfą 0
        if (_arrowBaseColors.TryGetValue(img, out var baseCol))
        {
            baseCol.a = 0f;
            img.color = baseCol;
        }

        _arrowRoutines[img] = null;
    }

    public void SetContaminationIcon(bool active)
    {
        if (!biohazardIcon) return;

        if (_biohazardRoutine != null)
            StopCoroutine(_biohazardRoutine);

        float targetA = active ? 1f : 0f;
        _biohazardRoutine = StartCoroutine(FadeBiohazard(targetA));
    }

    private IEnumerator FadeBiohazard(float targetAlpha)
    {
        // jeśli włączamy – upewnij się, że obiekt jest aktywny
        if (targetAlpha > 0f && !biohazardIcon.gameObject.activeSelf)
            biohazardIcon.gameObject.SetActive(true);

        Color start = biohazardIcon.color;
        float startA = start.a;

        float t = 0f;
        float duration = Mathf.Max(0.01f, biohazardFadeTime);

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float a = Mathf.Lerp(startA, targetAlpha, k);

            Color c = _biohazardBaseColor;
            c.a = a;
            biohazardIcon.color = c;

            yield return null;
        }

        // docelowy stan
        Color finalCol = _biohazardBaseColor;
        finalCol.a = targetAlpha;
        biohazardIcon.color = finalCol;

        // jeśli znikamy całkowicie – można wyłączyć GO
        if (Mathf.Approximately(targetAlpha, 0f))
            biohazardIcon.gameObject.SetActive(false);

        _biohazardRoutine = null;
    }



}
