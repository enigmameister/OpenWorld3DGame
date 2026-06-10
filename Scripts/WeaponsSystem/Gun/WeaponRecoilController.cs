using System.Collections.Generic;
using UnityEngine;

public class WeaponRecoilController : MonoBehaviour
{
    [SerializeField] private Gun gun;
    [SerializeField] private RecoilController recoilController;

    private int recoilIndex;
    private float lastShotTime;

    private List<Vector2> cachedPattern;
    private float cachedResetTime = 0.4f;

    void Awake()
    {
        if (!gun) gun = GetComponent<Gun>();
        RefreshWeaponDataCache();
    }

    public void SetCameraRecoilController(RecoilController controller)
    {
        recoilController = controller;
    }

    public void RefreshWeaponDataCache()
    {
        if (gun == null)
        {
            cachedPattern = null;
            cachedResetTime = 0.4f;
            return;
        }

        cachedPattern = gun.GetRecoilPattern();
        cachedResetTime = gun.GetRecoilResetTime();
        ResetRecoil();
    }

    public void ApplyShotRecoil()
    {
        if (recoilController == null)
            return;

        if (cachedPattern == null || cachedPattern.Count == 0)
        {
            RefreshWeaponDataCache();

            if (cachedPattern == null || cachedPattern.Count == 0)
                return;
        }

        float now = Time.time;

        if (now - lastShotTime > cachedResetTime)
            recoilIndex = 0;

        int patternCount = cachedPattern.Count;

        int index = recoilIndex < patternCount
            ? recoilIndex
            : patternCount - 1;

        recoilController.ApplyRecoil(cachedPattern[index]);

        recoilIndex++;
        lastShotTime = now;
    }

    public void ResetRecoil()
    {
        recoilIndex = 0;
        lastShotTime = 0f;
    }
}