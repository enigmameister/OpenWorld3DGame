using UnityEngine;

public class WeaponFOVController : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera worldCamera;

    [Header("FOV")]
    [SerializeField] private float defaultFOV = 60f;
    [SerializeField] private float adsFOV = 40f;

    [SerializeField] private float adsFOVBlendTime = 0.12f;

    private float fovVel;
    private float targetFOVComputed;
    private bool pendingSetFOV;
    private bool forceFOVThisFrame;

    public float GetComputedTargetFOV() => targetFOVComputed;

    public void BindCamera(Camera cam, bool snapshotDefaultFOV = true)
    {
        worldCamera = cam;

        if (cam && snapshotDefaultFOV)
            defaultFOV = cam.fieldOfView;
    }

    public void SetWeaponFOV(float ads)
    {
        adsFOV = ads;
    }

    public void UpdateFOV(bool isADS, bool isReloading, SniperRifleBehaviour sniperCfg)
    {
        if (!worldCamera) return;

        bool scoped = sniperCfg && sniperCfg.IsScoped() && !isReloading;

        forceFOVThisFrame =
            scoped &&
            sniperCfg != null &&
            sniperCfg.forceApplyScopedFOV;

        float targetFOV = isADS ? adsFOV : defaultFOV;

        if (scoped)
            targetFOV = sniperCfg.GetScopedTargetFOV(defaultFOV);

        float lower = (isADS || scoped)
            ? (sniperCfg ? sniperCfg.sniperMinFOV : 5f)
            : 5f;

        targetFOV = Mathf.Clamp(targetFOV, lower, 120f);

        targetFOVComputed = targetFOV;
        pendingSetFOV = true;
    }

    void LateUpdate()
    {
        if (!worldCamera || !pendingSetFOV) return;

        if (forceFOVThisFrame)
        {
            worldCamera.fieldOfView = targetFOVComputed;
            fovVel = 0f;
        }
        else
        {
            worldCamera.fieldOfView = Mathf.SmoothDamp(
                worldCamera.fieldOfView,
                targetFOVComputed,
                ref fovVel,
                adsFOVBlendTime
            );
        }

        pendingSetFOV = false;
    }
}