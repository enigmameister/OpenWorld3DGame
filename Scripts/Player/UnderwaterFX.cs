using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class UnderwaterFX : MonoBehaviour
{
    [Header("Overlay (opcjonalny)")]
    public Image overlay;                 // np. pó³przezroczysty niebieski UI Image na pe³en ekran
    [Range(0, 1)] public float overlayAlpha = 0.25f;

    [Header("Fog")]
    public bool useFog = true;
    public Color fogColor = new Color(0.1f, 0.3f, 0.45f, 1f);
    public float fogDensityUnderwater = 0.05f;  // wiêksza = „gêœciej”
    public float fogDensityAir = 0.0f;          // wy³¹cz/mniejsza poza wod¹

    PlayerStats _stats;
    bool _lastUnder;

    void Start()
    {
        _stats = FindFirstObjectByType<PlayerStats>();
        if (overlay) overlay.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        bool under = _stats && _stats.isUnderwater;

        // Overlay
        if (overlay)
        {
            if (under && !_lastUnder) overlay.gameObject.SetActive(true);
            if (!under && _lastUnder) overlay.gameObject.SetActive(false);

            if (overlay.isActiveAndEnabled)
            {
                var c = overlay.color;
                c.a = overlayAlpha;
                overlay.color = c;
            }
        }

        // Fog
        if (useFog)
        {
            RenderSettings.fog = under || fogDensityAir > 0f;
            RenderSettings.fogColor = under ? fogColor : RenderSettings.fogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = under ? fogDensityUnderwater : fogDensityAir;
        }

        _lastUnder = under;
    }
}
