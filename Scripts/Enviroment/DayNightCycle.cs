using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DayNightCycle : MonoBehaviour
{
    [Header("Źródło czasu (opcjonalnie)")]
    [Tooltip("Jeśli puste, użyje GameTimeSystem.Instance")]
    public GameTimeSystem timeSystem;

    [Header("Światło słoneczne")]
    public Light directionalLight;

    [Header("Intensywność światła")]
    public float intensityDay = 1.0f;
    public float intensityNight = 0.05f;

    [Header("Skyboxy pogodowe")]
    public SkyboxSet[] skyboxVariants;
    public float skyboxTransitionSpeed = 0.5f;

    private Material targetSkybox;
    private SkyboxSet activeSet;
    private int lastHour = -1;

    [Header("Efekty pogodowe")]
    public GameObject rainEffect;

    private bool isRaining = false;

    [Header("Ambient")]
    public Color ambientDay = new Color(0.7f, 0.7f, 0.7f);
    public Color ambientNight = new Color(0.06f, 0.07f, 0.09f);

    [Header("Fog")]
    public bool useFog = true;
    public Color fogDay = new Color(0.75f, 0.8f, 0.9f);
    public Color fogNight = new Color(0.03f, 0.04f, 0.06f);

    public float fogDensityDay = 0.0005f;
    public float fogDensityNight = 0.0008f;

    [System.Serializable]
    public class SkyboxSet
    {
        public Material morning;
        public Material day;
        public Material evening;
        public Material night;
        public Material midnight;
        public bool isRainy;
    }

    class RainPeriod
    {
        public float startMinute;
        public float endMinute;
    }

    private List<RainPeriod> rainPeriods = new List<RainPeriod>();

    // do wykrywania „nowego dnia” (na bazie daty z GameTimeSystem)
    private System.DateTime _lastDate;

    private GameTimeSystem TS => timeSystem != null ? timeSystem : GameTimeSystem.Instance;

    void Start()
    {
        if (TS != null)
            _lastDate = TS.CurrentTime.Date;

        PickNewWeatherSet();
        ForceUpdateVisuals();
    }
    void Awake()
    {
        if (timeSystem == null)
            timeSystem = GameTimeSystem.Instance != null
                ? GameTimeSystem.Instance
                : FindFirstObjectByType<GameTimeSystem>();
    }
    void Update()
    {
        if (TS == null) return;

        var now = TS.CurrentTime;

        // wykryj nowy dzień po dacie (nie po wrapie minut)
        if (now.Date != _lastDate)
        {
            _lastDate = now.Date;
            PickNewWeatherSet();
            lastHour = -1; // wymuś aktualizację skyboxa
        }

        int hour = now.Hour;
        int minute = now.Minute;

        UpdateDirectionalLightIntensity(hour, minute);
        UpdateDirectionalLight(hour, minute);
        UpdateSkybox(hour);
        UpdateRainEffect(GetMinuteOfDay(hour, minute));

        // płynne przejście skybox (jak u Ciebie)
        if (RenderSettings.skybox != null && targetSkybox != null)
        {
            RenderSettings.skybox.Lerp(RenderSettings.skybox, targetSkybox, Time.deltaTime * skyboxTransitionSpeed);
        }
    }

    void UpdateEnvironmentLighting(float dayFactor)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

        RenderSettings.ambientLight = Color.Lerp(
            ambientNight,
            ambientDay,
            dayFactor
        );

        RenderSettings.fog = useFog;

        RenderSettings.fogColor = Color.Lerp(
            fogNight,
            fogDay,
            dayFactor
        );

        RenderSettings.fogDensity = Mathf.Lerp(
            fogDensityNight,
            fogDensityDay,
            dayFactor
        );

        RenderSettings.reflectionIntensity = Mathf.Lerp(
            0.05f,
            1f,
            dayFactor
        );
    }

    private static float GetMinuteOfDay(int hour, int minute)
    {
        return Mathf.Clamp(hour, 0, 23) * 60f + Mathf.Clamp(minute, 0, 59);
    }

    private void ForceUpdateVisuals()
    {
        if (TS == null) return;
        var now = TS.CurrentTime;

        int hour = now.Hour;
        int minute = now.Minute;

        UpdateDirectionalLightIntensity(hour, minute);
        UpdateDirectionalLight(hour, minute);
        UpdateSkybox(hour, force: true);
        UpdateRainEffect(GetMinuteOfDay(hour, minute));

        if (targetSkybox != null)
            RenderSettings.skybox = targetSkybox;
    }

    void UpdateSkybox(int hour, bool force = false)
    {
        if (activeSet == null || skyboxVariants == null || skyboxVariants.Length == 0) return;

        if (lastHour == hour && !force) return;
        lastHour = hour;

        if (hour >= 6 && hour < 9)
            targetSkybox = activeSet.morning;
        else if (hour >= 9 && hour < 17)
            targetSkybox = activeSet.day;
        else if (hour >= 17 && hour < 20)
            targetSkybox = activeSet.evening;
        else if (hour >= 20 && hour < 23)
            targetSkybox = activeSet.night;
        else
            targetSkybox = activeSet.midnight;

        if (targetSkybox != null)
            RenderSettings.skybox = targetSkybox;
    }

    void PickNewWeatherSet()
    {
        if (skyboxVariants == null || skyboxVariants.Length == 0)
        {
            activeSet = null;
            rainPeriods.Clear();
            return;
        }

        int index = Random.Range(0, skyboxVariants.Length);
        activeSet = skyboxVariants[index];

        rainPeriods.Clear();

        if (!activeSet.isRainy)
        {
            // bez deszczu
            if (rainEffect != null) rainEffect.SetActive(false);
            isRaining = false;
            return;
        }

        int numRainPeriods = Random.Range(1, 4);
        int maxAttempts = 30;
        int attempts = 0;

        while (rainPeriods.Count < numRainPeriods && attempts < maxAttempts)
        {
            float start = Random.Range(0f, 1440f); // cały dzień
            float duration = Random.Range(60f, 720f); // 1–12h
            float end = (start + duration) % 1440f;

            bool overlaps = false;
            foreach (var p in rainPeriods)
            {
                if (PeriodsOverlap(start, end, p.startMinute, p.endMinute))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
                rainPeriods.Add(new RainPeriod { startMinute = start, endMinute = end });

            attempts++;
        }
    }

    void UpdateDirectionalLightIntensity(int hour, int minute)
    {
        if (directionalLight == null) return;

        float time = hour + minute / 60f;
        float t = 0f;

        if (time >= 6f && time < 18f)
        {
            float dayProgress = Mathf.InverseLerp(6f, 18f, time);
            t = Mathf.Sin(dayProgress * Mathf.PI);
        }

        directionalLight.intensity = Mathf.Lerp(intensityNight, intensityDay, t);

        UpdateEnvironmentLighting(t);
    }

    void UpdateDirectionalLight(int hour, int minute)
    {
        if (directionalLight == null) return;

        float time = hour + minute / 60f;

        // obrót: 0h = północ, 12h = góra, 24h = północ
        float xRotation = (time / 24f) * 360f - 90f;
        directionalLight.transform.rotation = Quaternion.Euler(xRotation, 170f, 0f);
    }

    void UpdateRainEffect(float minuteOfDay)
    {
        if (rainEffect == null) return;

        bool shouldRain = false;

        foreach (var period in rainPeriods)
        {
            if (IsWithinPeriod(minuteOfDay, period.startMinute, period.endMinute))
            {
                shouldRain = true;
                break;
            }
        }

        if (shouldRain && !isRaining)
        {
            isRaining = true;
            rainEffect.SetActive(true);
        }
        else if (!shouldRain && isRaining)
        {
            isRaining = false;
            rainEffect.SetActive(false);
        }
    }

    bool PeriodsOverlap(float aStart, float aEnd, float bStart, float bEnd)
    {
        bool aWraps = aEnd < aStart;
        bool bWraps = bEnd < bStart;

        if (!aWraps && !bWraps)
            return !(aEnd <= bStart || aStart >= bEnd);

        if (aWraps && bWraps)
            return true;

        if (aWraps)
            return !(aEnd <= bStart && aStart >= bEnd);
        else
            return !(bEnd <= aStart && bStart >= aEnd);
    }

    bool IsWithinPeriod(float time, float start, float end)
    {
        if (start <= end)
            return time >= start && time <= end;
        else
            return time >= start || time <= end;
    }

    // ====== COMPAT LAYER (dla starych skryptów) ======
    // DayNightCycle dalej steruje wizualami,
    // ale dane czasu pobiera z GameTimeSystem.

    public int CurrentHour
    {
        get
        {
            var ts = TS;
            return ts != null ? ts.CurrentTime.Hour : 0;
        }
    }

    public int CurrentMinute
    {
        get
        {
            var ts = TS;
            return ts != null ? ts.CurrentTime.Minute : 0;
        }
    }

    // Minuta dnia 0..1439 (jak wcześniej)
    public long GetMinuteOfDayNow()
    {
        return (long)(CurrentHour * 60 + CurrentMinute);
    }

    // TotalMinutes: monotoniczny czas w minutach od startu gry
    public long GetTotalMinutesNow()
    {
        var ts = TS;
        if (ts == null) return (long)(Time.timeSinceLevelLoad / 60f);

        // bazujemy na DateTime: różnica od startu czasu gry
        // (start = data ustawiona w GameTimeSystem Awake)
        // Uwaga: to działa tylko jeśli GameTimeSystem startuje w 2050-07-01 00:00
        // (tak jak u Ciebie).
        var start = new System.DateTime(ts.startYear, ts.startMonth, ts.startDay, 0, 0, 0, System.DateTimeKind.Utc);
        var diff = ts.CurrentTime - start;
        return (long)System.Math.Floor(diff.TotalMinutes);
    }

    // SAVE: czas doby jako 0..1
    public float GetSaveTime01()
    {
        float minute = CurrentHour * 60f + CurrentMinute;
        return Mathf.Clamp01(minute / 1440f);
    }

    // LOAD: ustaw czas doby z 0..1 (bez zmiany daty)
    // Jeśli chcesz zmieniać też dzień/miesiąc/rok przy loadzie – powiemy jak.
    public void LoadTime01(float t01)
    {
        t01 = Mathf.Clamp01(t01);

        var ts = TS;
        if (ts == null) return;

        int totalMinutes = Mathf.RoundToInt(t01 * 1440f);
        int hour = Mathf.Clamp(totalMinutes / 60, 0, 23);
        int minute = Mathf.Clamp(totalMinutes % 60, 0, 59);

        // ustawiamy nowy DateTime zachowując aktualną datę
        var now = ts.CurrentTime;
        var newTime = new System.DateTime(now.Year, now.Month, now.Day, hour, minute, 0, System.DateTimeKind.Utc);

        // potrzebujesz setter: dodaj w GameTimeSystem metodę SetTime(DateTime)
        ts.SetTime(newTime);

        // wymuś odświeżenie wizualne
        lastHour = -1;
        ForceUpdateVisuals();
    }

    // DevConsole używa SetHour() :contentReference[oaicite:9]{index=9}
    public void SetHour(int hour)
    {
        hour = Mathf.Clamp(hour, 0, 23);
        var ts = TS;
        if (ts == null) return;

        var now = ts.CurrentTime;
        var newTime = new System.DateTime(now.Year, now.Month, now.Day, hour, 0, 0, System.DateTimeKind.Utc);
        ts.SetTime(newTime);

        lastHour = -1;
        ForceUpdateVisuals();
    }

    // (opcjonalnie) kompatybilny formatter dla UI/debug
    private static readonly string[] DayShortNames =
    { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public string GetDayShortName()
    {
        // liczony od startowej daty (Mon..Sun)
        var ts = TS;
        if (ts == null) return "Mon";

        var start = new System.DateTime(ts.startYear, ts.startMonth, ts.startDay, 0, 0, 0, System.DateTimeKind.Utc);
        var days = (int)System.Math.Floor((ts.CurrentTime - start).TotalDays);
        return DayShortNames[((days % 7) + 7) % 7];
    }

    public string GetFormattedDateTimeMultiline()
    {
        var ts = TS;
        if (ts == null) return "00:00\n01-07-2050 Mon";
        var t = ts.CurrentTime;
        return $"{t:HH:mm}\n{t:dd-MM-yyyy} {t:ddd}";
    }



}
