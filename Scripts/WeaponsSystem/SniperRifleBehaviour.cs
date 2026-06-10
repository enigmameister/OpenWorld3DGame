using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SniperRifleBehaviour : MonoBehaviour
{
    private Gun gun;

    [Header("Scope")]
    public bool hasScope = true;
    public bool autoReturnAfterShot = true;
    [Range(0.5f, 1.2f)]
    public float autoReturnDelayFactor = 0.95f;

    [Header("Zoom Levels")]
    public bool useZoomMultipliers = false;
    public List<float> zoomFOVs = new() { 40f, 20f, 12f, 8f, 5f };
    public List<float> zoomMultipliers = new() { 2, 4, 8, 12, 20 };
    [Range(0.1f, 3f)] public float zoomScrollSpeed = 1f;
    public bool invertZoomScroll = false;
    [Min(1)] public int maxZoomLevels = 5;

    [Header("FOV & Stabilizacja")]
    public float sniperMinFOV = 0.9f;
    public bool forceApplyScopedFOV = true;
    public bool disableShakersWhileScoped = true;
    public Behaviour[] extraBehavioursToDisable;

    [Header("Sensitivity")]
    [Range(0f, 1f)] public float scopedSensitivityMultiplier = 0.35f;
    [Range(0f, 1f)] public float adsSensitivityMultiplier = 0.7f;
    public bool applyInADS = false;

    [Header("UI / Render")]
    public GameObject sniperScopeOverlay;

    private int zoomLevel = 0;
    private bool isZoomBlocked;
    private bool isCoolingDown;
    private float zoomAccum;

    private void Awake()
    {
        gun = GetComponent<Gun>();

        if (gun)
            gun.RegisterSniperBehaviour(this);
    }

    // === PUBLIC API używane przez Gun.cs ===
    public bool IsScoped() => zoomLevel > 0 && !isZoomBlocked && !isCoolingDown && !gun.IsReloading;

    public void EnterScope(int level = 1)
    {
        if (!hasScope) return;
        if (gun && gun.IsReloading) return;

        int maxLevels = Mathf.Clamp(
            maxZoomLevels,
            1,
            useZoomMultipliers ? zoomMultipliers.Count : zoomFOVs.Count
        );

        if (zoomLevel <= 0)
            zoomLevel = Mathf.Clamp(level, 1, maxLevels);

        isZoomBlocked = false;
        isCoolingDown = false;

        // włącz ADS w broni i overlay
        gun?.SetADS(true);
        ToggleScopeOverlay(true);
    }

    public void ExitScope()
    {
        zoomLevel = 0;
        isZoomBlocked = false;
        isCoolingDown = false;

        // wyłącz ADS i overlay
        gun?.SetADS(false);
        ToggleScopeOverlay(false);
    }

    public void HandleZoomInput()
    {
        if (!hasScope || isZoomBlocked) return;

        float wheel = Input.mouseScrollDelta.y * (invertZoomScroll ? -1f : 1f);
        if (Mathf.Abs(wheel) < 0.01f) return;

        zoomAccum += wheel * zoomScrollSpeed;
        int steps = (int)(zoomAccum > 0 ? Mathf.Floor(zoomAccum) : Mathf.Ceil(zoomAccum));
        if (steps != 0)
        {
            int allowed = Mathf.Clamp(
                maxZoomLevels,
                1,
                useZoomMultipliers ? zoomMultipliers.Count : zoomFOVs.Count
            );

            zoomLevel = Mathf.Clamp(Mathf.Max(zoomLevel, 1) + steps, 1, allowed);
            zoomAccum -= steps;
        }
    }

    public float GetScopedTargetFOV(float defaultFOV)
    {
        if (zoomLevel <= 0) return defaultFOV;

        float fov = defaultFOV;
        if (useZoomMultipliers && zoomMultipliers.Count >= zoomLevel)
        {
            float mult = Mathf.Max(0.01f, zoomMultipliers[zoomLevel - 1]);
            fov = defaultFOV / mult;
        }
        else if (zoomFOVs.Count >= zoomLevel)
        {
            fov = zoomFOVs[zoomLevel - 1];
        }
        return Mathf.Clamp(fov, sniperMinFOV, 120f);
    }

    public void OnFireShot(bool wasScopedBefore, int prevZoom)
    {
        if (!autoReturnAfterShot) return;
        StartCoroutine(CoScopeBreak(wasScopedBefore, prevZoom));
    }

    public void ToggleScopeOverlay(bool show)
    {
        if (sniperScopeOverlay && sniperScopeOverlay.activeSelf != show)
            sniperScopeOverlay.SetActive(show);
    }

    public void SetZoomBlocked(bool block) => isZoomBlocked = block;
    public void SetCoolingDown(bool cool) => isCoolingDown = cool;
    public int GetZoomLevel() => zoomLevel;
    public void ResetZoom() => zoomLevel = 0;

    // === Wewnętrzne ===
    private IEnumerator CoScopeBreak(bool wasScopedBefore, int prevZoom)
    {
        if (!wasScopedBefore || gun.IsReloading) yield break;

        // rozłącz po strzale (to robi Gun), a tu tylko czekamy na powrót
        float delay = gun.GetFireRate() * Mathf.Clamp(autoReturnDelayFactor, 0.5f, 1.2f);
        yield return new WaitForSeconds(delay);

        if (gun == null || !gun.isActiveAndEnabled || gun.GetCurrentAmmo() <= 0) yield break;

        // powrót do poprzedniego powiększenia
        EnterScope(Mathf.Max(prevZoom, 1));
    }

    public void ForceScopeOn()
    {
        int targetLevel = Mathf.Max(1, maxZoomLevels);
        EnterScope(targetLevel);
    }

}