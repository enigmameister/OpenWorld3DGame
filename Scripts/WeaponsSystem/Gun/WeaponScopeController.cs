using UnityEngine;

public class WeaponScopeController : MonoBehaviour
{
    [SerializeField] private Gun gun;
    [SerializeField] private SniperRifleBehaviour sniperCfg;

    [Header("Scope Visuals")]
    [SerializeField] private bool hideWeaponInScope = true;
    [SerializeField] private Camera weaponOverlayCamera;

    [Header("HUD ukrywany w scope")]
    [SerializeField] private GameObject minimapRoot;
    [SerializeField] private GameObject weaponHudRoot; // np. WeaponsAmmoInfo / sloty broni

    [Header("TPP model")]
    [SerializeField] private GameObject tppWeaponModelRoot;

    private int overlayMaskBefore = -1;
    private bool overlayMaskChanged;
    private int weaponLayerMask;

    private Behaviour[] camShakers;
    private Behaviour[] extraBehavioursToDisable;

    private bool lastScopeActive;
    private bool initializedScopeState;

    private bool IsTPP()
    {
        return CameraSwitcher.Instance != null &&
               CameraSwitcher.Instance.IsTPPActive;
    }

    void Awake()
    {
        if (!gun) gun = GetComponent<Gun>();
        if (!sniperCfg) sniperCfg = GetComponent<SniperRifleBehaviour>();

        if (weaponOverlayCamera != null)
            overlayMaskBefore = weaponOverlayCamera.cullingMask;

        weaponLayerMask = 0;

        int fpsLayer = LayerMask.NameToLayer("WeaponRenderFPS");
        if (fpsLayer >= 0)
            weaponLayerMask |= 1 << fpsLayer;

        int armsLayer = LayerMask.NameToLayer("Arms");
        if (armsLayer >= 0)
            weaponLayerMask |= 1 << armsLayer;

        CacheCameraShakers();

        if (sniperCfg && sniperCfg.extraBehavioursToDisable != null)
            extraBehavioursToDisable = sniperCfg.extraBehavioursToDisable;
        else
            extraBehavioursToDisable = System.Array.Empty<Behaviour>();
    }

    public void BindOverlayCamera(Camera cam)
    {
        weaponOverlayCamera = cam;

        if (weaponOverlayCamera != null)
            overlayMaskBefore = weaponOverlayCamera.cullingMask;
    }

    public void RefreshSniperReference(SniperRifleBehaviour sniper)
    {
        sniperCfg = sniper;

        if (sniperCfg && sniperCfg.extraBehavioursToDisable != null)
            extraBehavioursToDisable = sniperCfg.extraBehavioursToDisable;
        else
            extraBehavioursToDisable = System.Array.Empty<Behaviour>();
    }

    public void UpdateScopeVisuals(bool isReloading)
    {
        bool scopeActive =
            sniperCfg &&
            sniperCfg.IsScoped() &&
            !isReloading;

        if (initializedScopeState && lastScopeActive == scopeActive)
            return;

        initializedScopeState = true;
        lastScopeActive = scopeActive;

        if (sniperCfg)
            sniperCfg.ToggleScopeOverlay(scopeActive);

        if (hideWeaponInScope &&
            !IsTPP() &&
            weaponOverlayCamera != null &&
            weaponLayerMask != 0)
        {
            if (scopeActive && !overlayMaskChanged)
            {
                overlayMaskBefore = weaponOverlayCamera.cullingMask;
                weaponOverlayCamera.cullingMask = overlayMaskBefore & ~weaponLayerMask;
                overlayMaskChanged = true;
            }
            else if (!scopeActive && overlayMaskChanged)
            {
                weaponOverlayCamera.cullingMask = overlayMaskBefore;
                overlayMaskChanged = false;
            }
        }

        SetActiveSafe(minimapRoot, !scopeActive);
        SetActiveSafe(weaponHudRoot, !scopeActive);
        SetActiveSafe(tppWeaponModelRoot, !scopeActive);

        if (sniperCfg && sniperCfg.disableShakersWhileScoped)
            SetScopeShakers(!scopeActive);
    }

    public void ResetScopeVisuals()
    {
        if (sniperCfg)
            sniperCfg.ToggleScopeOverlay(false);

        if (minimapRoot)
            minimapRoot.SetActive(true);

        if (weaponHudRoot)
            weaponHudRoot.SetActive(true);

        if (tppWeaponModelRoot)
            tppWeaponModelRoot.SetActive(true);

        if (weaponOverlayCamera != null && overlayMaskChanged)
        {
            weaponOverlayCamera.cullingMask = overlayMaskBefore;
            overlayMaskChanged = false;
        }

        SetScopeShakers(true);
    }

    public void SetScopeShakers(bool on)
    {
        if (camShakers != null)
        {
            for (int i = 0; i < camShakers.Length; i++)
            {
                if (camShakers[i])
                    camShakers[i].enabled = on;
            }
        }

        if (extraBehavioursToDisable != null)
        {
            for (int i = 0; i < extraBehavioursToDisable.Length; i++)
            {
                if (extraBehavioursToDisable[i])
                    extraBehavioursToDisable[i].enabled = on;
            }
        }
    }

    private void CacheCameraShakers()
    {
        Camera baseCam = Camera.main;
        if (!baseCam) return;

        var list = new System.Collections.Generic.List<Behaviour>();

        foreach (var b in baseCam.GetComponents<Behaviour>())
        {
            string t = b.GetType().Name;

            if (t.Contains("FallImpact") ||
                t.Contains("Fatigue") ||
                t.Contains("HeadBob") ||
                t.Contains("Breath"))
            {
                list.Add(b);
            }
        }

        camShakers = list.ToArray();
    }

    private void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}