using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class WeaponReloadController : MonoBehaviour
{
    [SerializeField] private Gun gun;
    [SerializeField] private Animator animator;

    [Header("Reload UI (Auto Found)")]
    [SerializeField] private GameObject reloadBarRoot;
    [SerializeField] private Image reloadProgressBar;

    private Coroutine reloadCoroutine;
    private bool isReloading;
    private bool wasReloadInterrupted;
    private bool allowReloadWhileInventoryOpen;

    public bool IsReloading => isReloading;
    public bool AllowReloadWhileInventoryOpen => allowReloadWhileInventoryOpen;
    private static readonly WaitForSeconds ShotgunShellDelay = new WaitForSeconds(0.05f);

    void Awake()
    {
        if (!gun) gun = GetComponent<Gun>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);

        ReloadUI_SetActive(false);
    }

    public void SetAllowReloadWhileInventoryOpen(bool value)
    {
        allowReloadWhileInventoryOpen = value;
    }

    public void StartReload()
    {
        if (!gun) return;
        if (gun.HasInfiniteAmmo()) return;
        if (gun.IsPlayerDead()) return;
        if (gun.GetCurrentAmmo() >= gun.GetMagazineSize()) return;
        if (gun.GetTotalAmmo() <= 0) return;
        if (reloadCoroutine != null) return;

        if (gun.IsShotgunWeapon())
        {
            reloadCoroutine = StartCoroutine(ReloadShotgunRoutine());
            gun.RefreshBulletUI();
            return;
        }

        reloadCoroutine = StartCoroutine(ReloadRoutine());
        gun.RefreshBulletUI();
    }

    public void StopReload()
    {
        if (!isReloading) return;

        wasReloadInterrupted = true;

        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        isReloading = false;

        ReloadUI_SetActive(false);
        allowReloadWhileInventoryOpen = false;
    }

    public bool StartReserveInsert(int amount, System.Action onApplied, System.Action onCanceled = null)
    {
        if (!gun) return false;
        if (gun.HasInfiniteAmmo()) return false;
        if (isReloading || amount <= 0) return false;

        allowReloadWhileInventoryOpen = true;
        ReloadUI_SetActive(true);

        StartCoroutine(CoReserveInsert(amount, onApplied, onCanceled));
        return true;
    }

    private IEnumerator CoReserveInsert(int amount, System.Action onApplied, System.Action onCanceled)
    {
        isReloading = true;

        float t = 0f;
        float dur = gun.GetReloadTime();

        while (t < dur)
        {
            t += Time.deltaTime;

            if (reloadProgressBar != null)
                reloadProgressBar.fillAmount = Mathf.Clamp01(t / dur);

            yield return null;
        }

        int cap = gun.GetMaxReserveAmmo();
        int newReserve = Mathf.Min(cap, gun.GetTotalAmmo() + amount);

        gun.SetAmmo(gun.GetCurrentAmmo(), newReserve);

        isReloading = false;
        allowReloadWhileInventoryOpen = false;
        ReloadUI_SetActive(false);

        onApplied?.Invoke();
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        wasReloadInterrupted = false;

        float reloadTime = gun.GetReloadTime();
        float elapsed = 0f;

        ReloadUI_SetActive(true);

        while (elapsed < reloadTime)
        {
            if (wasReloadInterrupted) break;

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / reloadTime);

            if (reloadProgressBar != null)
            {
                reloadProgressBar.fillAmount = progress;
            }

            yield return null;
        }

        ReloadUI_SetActive(false);

        if (!wasReloadInterrupted)
        {
            int neededAmmo = gun.GetMagazineSize() - gun.GetCurrentAmmo();
            int ammoToLoad = Mathf.Min(neededAmmo, gun.GetTotalAmmo());

            if (ammoToLoad <= 0 && gun.GetCurrentAmmo() == 0 && gun.GetTotalAmmo() > 0)
                ammoToLoad = Mathf.Min(gun.GetMagazineSize(), gun.GetTotalAmmo());

            gun.SetAmmo(
                gun.GetCurrentAmmo() + ammoToLoad,
                gun.GetTotalAmmo() - ammoToLoad
            );

            gun.RefreshBulletUI();
        }

        isReloading = false;
        reloadCoroutine = null;
        allowReloadWhileInventoryOpen = false;

        gun.OnReloadFinished();
    }

    private IEnumerator ReloadShotgunRoutine()
    {
        isReloading = true;
        wasReloadInterrupted = false;

        ReloadUI_SetActive(true);

        while (gun.GetCurrentAmmo() < gun.GetMagazineSize() &&
               gun.GetTotalAmmo() > 0 &&
               !wasReloadInterrupted)
        {
            animator?.SetTrigger("Reload");

            float reloadTime = gun.GetReloadTime();
            float elapsed = 0f;

            if (reloadProgressBar != null)
            {
                reloadProgressBar.type = Image.Type.Filled;
                reloadProgressBar.fillMethod = Image.FillMethod.Horizontal;
                reloadProgressBar.fillOrigin = (int)Image.OriginHorizontal.Left;
                reloadProgressBar.fillAmount = 0f;
            }

            while (elapsed < reloadTime)
            {
                if (wasReloadInterrupted) break;

                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / reloadTime);

                if (reloadProgressBar != null)
                {
                    reloadProgressBar.fillAmount = progress;
                }

                yield return null;
            }

            if (wasReloadInterrupted) break;

            gun.SetAmmo(
                gun.GetCurrentAmmo() + 1,
                gun.GetTotalAmmo() - 1
            );

            gun.RefreshBulletUI();

            yield return ShotgunShellDelay;
        }

        ReloadUI_SetActive(false);

        isReloading = false;
        wasReloadInterrupted = false;
        reloadCoroutine = null;
        allowReloadWhileInventoryOpen = false;
    }

    private void ReloadUI_SetActive(bool on)
    {
        if (!reloadBarRoot || !reloadProgressBar) return;

        reloadBarRoot.SetActive(on);

        if (on)
        {
            reloadProgressBar.type = Image.Type.Filled;
            reloadProgressBar.fillMethod = Image.FillMethod.Horizontal;
            reloadProgressBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    public void CleanupOnDisable()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        isReloading = false;
        wasReloadInterrupted = false;
        allowReloadWhileInventoryOpen = false;

        ReloadUI_SetActive(false);
    }
}