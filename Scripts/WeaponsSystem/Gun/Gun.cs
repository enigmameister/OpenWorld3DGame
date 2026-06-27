using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class Gun : MonoBehaviour, IInventoryItemInstanceProvider
{
    [SerializeField] private WeaponADSController adsController; // ADS Controller Reference
    [SerializeField] private WeaponFOVController fovController; // FOV Controller Reference
    [SerializeField] private WeaponReloadController reloadController; // Reloading Controller Reference
    [SerializeField] private WeaponFireController fireController; // Fire Controller Reference
    [SerializeField] private WeaponRecoilController weaponRecoil; // Recoil Controller Reference
    [SerializeField] private WeaponScopeController scopeController; // Scope Controller Reference

    // ====== PUBLIC API / INTERFEJS ======
    public InventoryItemInstance inventoryInstance;
    public bool isControlledByNPC = false;

    public enum FireMode { SemiAuto, FullAuto }
    public FireMode fireMode = FireMode.SemiAuto;

    private static readonly List<Vector2> EmptyRecoilPattern = new();

    public WeaponItemData weaponData;

    // Pluginy (opcjonalne)
    [SerializeField] private SniperRifleBehaviour sniperCfg;   // pełna logika snajperska
    [SerializeField] private ShotgunBehaviour shotgunCfg;      // konfiguracja strzelby

    public void RegisterSniperBehaviour(SniperRifleBehaviour b) => sniperCfg = b;

    [Header("Strzelanie")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public string bulletType = "Bullet";

    [Header("Raycast")]
    public LayerMask hitMask = ~0; // domyślnie wszystko

    [Header("Hit FX")]
    public GameObject hitEffectPrefab;

    [Header("Amunicja")]
    public int totalAmmo;
    public int currentAmmo;

    [Header("Przeładowanie")]
    private float nextFireTime = 0f;
    private Coroutine _fireCooldownBarCo;

    [Header("Recoil")]
    public RecoilController recoilController;
    private bool _isADS;

    [SerializeField] private float adsCooldown = 0.12f;
    private float lastAdsChangeTime = -10f;

    [Header("Efekty")]
    public ParticleSystem muzzleFlash;
    public ParticleSystem shellEjectParticles;

    [Header("Kamery / FOV")]
    public Camera playerCamera;
    public Camera worldCamera;
    public Camera weaponOverlayCamera;

    [Header("Scope capability (ogólne)")]
    public bool supportsScope = true;
    public bool scopeUnlocked = true;

    [Header("Czułość podczas celowania (opcjonalne)")]
    [SerializeField] private MonoBehaviour lookSensitivityTarget;   // skrypt z polem float, np. "mouseSensitivity"
    [SerializeField] private string lookSensitivityField = "mouseSensitivity";
    private FieldInfo _sensField;
    private float _savedSensitivity = -1f;
    private bool _hasSensitivityTarget = false;

    [Header("Animacja / View-model")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private string fireTriggerName = "FireTrigger";

    private PlayerStats _playerStats;
    private GunUI _gunUI;

    // Wygody
    bool InfAmmo => CheatState.InfiniteAmmo && !isControlledByNPC;
    private bool IsShotgun => weaponData != null && weaponData.isShotgun;
    public bool IsReloading => reloadController != null && reloadController.IsReloading;

    // === DEBUG / HUD accessors ===
    public bool IsADS() => _isADS;
    public float GetComputedTargetFOV()
    {
        return fovController != null
            ? fovController.GetComputedTargetFOV()
            : 0f;
    }

    public InventoryItemInstance GetInstance() => inventoryInstance;
    public static System.Action<Vector3, Vector3, Vector3> OnPlayerShot;

    [SerializeField] private bool useExternalCombatInput = false;

    void Awake()
    {
        if (!adsController) adsController = GetComponent<WeaponADSController>();
        if (!fovController) fovController = GetComponent<WeaponFOVController>();
        if (!reloadController) reloadController = GetComponent<WeaponReloadController>();
        if (!fireController) fireController = GetComponent<WeaponFireController>();
        if (!weaponRecoil) weaponRecoil = GetComponent<WeaponRecoilController>();
        if (!scopeController) scopeController = GetComponent<WeaponScopeController>();

        // auto-find pluginów
        if (!sniperCfg) sniperCfg = GetComponent<SniperRifleBehaviour>();
        if (!shotgunCfg) shotgunCfg = GetComponent<ShotgunBehaviour>();

        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (playerCamera == null) playerCamera = Camera.main;

        var player = GameObject.FindGameObjectWithTag("Player");
        _playerStats = player ? player.GetComponent<PlayerStats>() : null;

        _gunUI = FindFirstObjectByType<GunUI>();

        // Sensitivity reflection (opcjonalne)
        if (lookSensitivityTarget != null && !string.IsNullOrEmpty(lookSensitivityField))
        {
            _sensField = lookSensitivityTarget.GetType()
                .GetField(lookSensitivityField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (_sensField != null && _sensField.FieldType == typeof(float))
            {
                _savedSensitivity = (float)_sensField.GetValue(lookSensitivityTarget);
                _hasSensitivityTarget = true;
            }
        }

        RebindRuntimeReferences();
    }

    void Update()
    {
        if (isControlledByNPC) return;

        if (PlayerInputHandler.GameplayInputBlocked)
        {
            if (IsReloading)
                reloadController?.StopReload();

            if (_isADS)
                SetADS(false);

            scopeController?.UpdateScopeVisuals(IsReloading);
            fovController?.UpdateFOV(_isADS, IsReloading, sniperCfg);

            return;
        }

        bool isReloading = IsReloading;

        if (PlayerDead)
        {
            SetADS(false);
            scopeController?.UpdateScopeVisuals(isReloading);
            return;
        }

        if (DevConsole.IsOpen)
        {
            if (isReloading) reloadController?.StopReload();
            if (_isADS) SetADS(false);
            scopeController?.UpdateScopeVisuals(isReloading);
            return;
        }

        if (InventoryUI.IsInventoryOpen || InventoryUI.IsDraggingInventoryItem)
        {
            if (reloadController == null || !reloadController.AllowReloadWhileInventoryOpen)
            {
                if (isReloading) reloadController?.StopReload();
                if (_isADS) SetADS(false);
                return;
            }

            scopeController?.UpdateScopeVisuals(isReloading);
            return;
        }

        if (!useExternalCombatInput)
            HandlePlayerInput();

        isReloading = IsReloading;

        scopeController?.UpdateScopeVisuals(isReloading);
        fovController?.UpdateFOV(_isADS, isReloading, sniperCfg);
    }

    private void Fire()
    {
        fireController?.TryFire();
    }

    public bool TryNPCFire(GameObject shooter, Vector3 direction)
    {
        return fireController != null &&
               fireController.TryNPCFire(shooter, direction);
    }

    public void SpawnBullet(Vector3 direction)
    {
        fireController?.SpawnBullet(direction);
    }

    public void RaiseOnPlayerShot(Vector3 impactPoint)
    {
        fireController?.RaiseOnPlayerShot(impactPoint);
    }

    public void SpawnBulletHole(RaycastHit hit, bool ignoreDecalDedup = false)
    {
        fireController?.SpawnBulletHole(hit, ignoreDecalDedup);
    }

    public void SetExternalCombatInput(bool value)
    {
        useExternalCombatInput = value;
    }

    void OnEnable()
    {
        if (isControlledByNPC) { SetADS(false); return; }

        SetADS(false);
        sniperCfg?.ExitScope();                 // pewne wyłączenie siatki

        scopeController?.ResetScopeVisuals();
    }

    void OnDisable()
    {
        ForceCancelCombatState();
    }

    public void BindCamera(Camera cam, bool snapshotDefaultFOV = true)
    {
        playerCamera = cam;
        worldCamera = cam;

        fovController?.BindCamera(cam, snapshotDefaultFOV);
        fireController?.BindCamera(cam);
    }

    public void ApplyWeaponData(WeaponItemData data)
    {
        weaponData = data;
        if (data == null) return;

        bulletType = data.bulletType;
        fovController?.SetWeaponFOV(data.adsFOV);

        if (animator != null && data.overrideController != null)
            animator.runtimeAnimatorController = data.overrideController;

        if (data.pickupClip != null && animator != null)
            animator.Play(data.pickupClip.name, 0, 0f);

        weaponRecoil?.RefreshWeaponDataCache();
    }

    public float GetReloadTime() => weaponData != null ? weaponData.reloadTime : 1.5f;
    public float GetFireRate() => weaponData != null ? weaponData.fireRate : 0.1f;
    public float GetBulletSpeed() => weaponData != null ? weaponData.bulletSpeed : 50f;
    public float GetDamage() => weaponData != null ? weaponData.damage : 25f;
    public int GetMagazineSize() => weaponData != null ? weaponData.magazineSize : 30;
    public List<Vector2> GetRecoilPattern()
    {
        return weaponData != null && weaponData.recoilPattern != null
            ? weaponData.recoilPattern
            : EmptyRecoilPattern;
    }

    public float GetRecoilResetTime() => weaponData != null ? weaponData.recoilResetTime : 0.4f;

    public int GetCurrentAmmo() => currentAmmo;
    public int GetTotalAmmo() => totalAmmo;
    public bool HasScope()
    {
        // jeżeli plugin snajperski istnieje – jego flaga rozstrzyga
        if (sniperCfg) return supportsScope && scopeUnlocked && sniperCfg.hasScope;
        // fallback (np. karabin bez plugina = brak scope)
        return false;
    }

    public int GetMaxReserveAmmo()
    {
        int mag = GetMagazineSize();
        return Mathf.Max(0, mag * 3);
    }

    private bool PlayerDead => _playerStats != null && _playerStats.IsDead;

    // ====== AMMO ======
    private void SyncInstanceAmmo()
    {
        if (inventoryInstance == null) return;
        inventoryInstance.currentAmmo = currentAmmo;
        inventoryInstance.totalAmmo = totalAmmo;
    }

    public void LoadAmmoFromInstance()
    {
        if (inventoryInstance != null)
        {
            currentAmmo = Mathf.Clamp(inventoryInstance.currentAmmo, 0, GetMagazineSize());
            totalAmmo = Mathf.Clamp(inventoryInstance.totalAmmo, 0, GetMaxReserveAmmo());
        }
    }

    public void SetAmmo(int current, int total)
    {
        if (current >= 0) currentAmmo = Mathf.Clamp(current, 0, GetMagazineSize());
        if (total >= 0) totalAmmo = Mathf.Clamp(total, 0, GetMaxReserveAmmo());
        SyncInstanceAmmo();
    }

    // ====== ADS / SENS ======
    public void SetADS(bool on)
    {
        _isADS = on && !IsReloading && !PlayerDead;

        ApplyScopedSensitivity(
            sniperCfg && sniperCfg.IsScoped(),
            _isADS
        );

        adsController?.SetADS(_isADS);
    }

    private void ApplyScopedSensitivity(bool scoped, bool ads)
    {
        if (!_hasSensitivityTarget || _sensField == null) return;
        if (_savedSensitivity < 0f)
            _savedSensitivity = (float)_sensField.GetValue(lookSensitivityTarget);

        float mult = 1f;
        if (sniperCfg && scoped) mult = Mathf.Max(0f, sniperCfg.scopedSensitivityMultiplier);
        else if (sniperCfg && ads && sniperCfg.applyInADS) mult = Mathf.Max(0f, sniperCfg.adsSensitivityMultiplier);

        _sensField.SetValue(lookSensitivityTarget, _savedSensitivity * mult);
    }

    private void HandlePlayerInput()
    {
        if (PlayerDead) return;

        bool fireInput = fireMode == FireMode.SemiAuto
            ? (PlayerInputHandler.Instance?.FirePressed ?? false)
            : (PlayerInputHandler.Instance?.FireHeld ?? false);

        if (IsReloading && currentAmmo > 0 && fireInput)
        {
            reloadController?.StopReload();
            Fire();
            nextFireTime = Time.time + GetFireRate();
            return;
        }

        if (IsReloading)
        {
            if (_isADS) SetADS(false);
            return;
        }

        if (fireInput && Time.time >= nextFireTime)
        {
            if (InfAmmo && currentAmmo <= 0)
            {
                currentAmmo = GetMagazineSize();
                _gunUI?.UpdateBulletIcons(currentAmmo);
                SyncInstanceAmmo();
            }

            if (currentAmmo > 0)
            {
                Fire();
                nextFireTime = Time.time + GetFireRate();
            }
            else
            {
                if (InfAmmo)
                {
                    currentAmmo = GetMagazineSize();
                    _gunUI?.UpdateBulletIcons(currentAmmo);
                    SyncInstanceAmmo();
                    Fire();
                    nextFireTime = Time.time + GetFireRate();
                }
                else if (totalAmmo > 0)
                {
                    if (!IsReloading)
                        StartCoroutine(CoQueueAutoReload());
                }
                else
                {
                    if (!InventoryUI.IsDraggingInventoryItem)
                    {
                        var wm = GetComponentInParent<WeaponManager>();
                        if (wm != null)
                        {
                            int myIndex = wm.FindSlotIndexForInstance(inventoryInstance);
                            wm.TrySwitchToAvailableWeapon(myIndex);
                        }
                    }
                    nextFireTime = Time.time + 0.2f;
                }
            }
        }

        // Manual reload
        if ((PlayerInputHandler.Instance?.ReloadPressed ?? false)
            && !InfAmmo
            && currentAmmo < GetMagazineSize()
            && totalAmmo > 0
            && !IsReloading)
        {
            reloadController?.StartReload();
        }

        // ADS / SCOPE (plugin snajperski)
        bool hasScopeNow = HasScope();
        if (hasScopeNow && sniperCfg)
        {
            // Toggle scope pod Alt (klik)
            bool altPressed = PlayerInputHandler.Instance?.FireAltPressed == true;
            if (altPressed)
            {
                if (sniperCfg.IsScoped())
                {
                    sniperCfg.ExitScope();
                    SetADS(false);
                }
                else
                {
                    sniperCfg.EnterScope(1);
                    SetADS(true);
                }
            }

            // Scroll zoom tylko gdy jesteśmy w scope i nie reload
            if (sniperCfg.IsScoped() && !IsReloading)
                sniperCfg.HandleZoomInput();
        }
        else
        {
            // klasyczny ADS bez scope
            bool altHeld = PlayerInputHandler.Instance?.FireAltHeld ?? false;

            if (altHeld != _isADS && Time.time - lastAdsChangeTime >= adsCooldown)
            {
                SetADS(altHeld);
                lastAdsChangeTime = Time.time;
            }

            if (!altHeld && _isADS && Time.time - lastAdsChangeTime >= adsCooldown)
            {
                SetADS(false);
                lastAdsChangeTime = Time.time;
            }
        }

        // po zmianie – odśwież czułość
        ApplyScopedSensitivity(sniperCfg && sniperCfg.IsScoped(), _isADS);
    }

    private IEnumerator CoQueueAutoReload()
    {
        yield return null;
        if (!IsReloading && currentAmmo == 0 && totalAmmo > 0)
            reloadController?.StartReload();
    }

    private IEnumerator AutoReloadAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!IsReloading && currentAmmo == 0 && totalAmmo > 0)
        {
            reloadController?.StartReload();
        }
    }

    public void TryReloadFromInventoryUI()
    {
        reloadController?.SetAllowReloadWhileInventoryOpen(true);
        reloadController?.StartReload();
    }

    public bool StartReserveInsert(int amount, System.Action onApplied, System.Action onCanceled = null)
    {
        return reloadController != null &&
               reloadController.StartReserveInsert(amount, onApplied, onCanceled);
    }

    private void StartFireCooldownBar(float duration)
    {
        if (isControlledByNPC || _gunUI == null) return;
        if (sniperCfg == null) return;           // ⬅️ pasek tylko dla snajperek
        if (duration <= 0.001f) return;

        if (_fireCooldownBarCo != null) StopCoroutine(_fireCooldownBarCo);
        _fireCooldownBarCo = StartCoroutine(CoFireCooldownBar(duration));
    }

    private IEnumerator CoFireCooldownBar(float duration)
    {
        _gunUI.StartSniperCooldown(duration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }
        _gunUI.StopSniperCooldown();
        _fireCooldownBarCo = null;
    }

    #region Compatibility API

    public bool IsSniperWeapon()
    {
        return sniperCfg != null;
    }

    public void ResetAimStateOnHolster()
    {
        ForceCancelCombatState();
    }

    public bool IsScoped()
    {
        // Jeśli ma plugin – jego stan decyduje.
        if (sniperCfg != null)
            return sniperCfg.IsScoped();

        // Jeśli nie – fallback: brak scope
        return false;
    }

    public void SetInventoryInstance(InventoryItemInstance instance)
    {
        inventoryInstance = instance;
        LoadAmmoFromInstance();
    }

    #endregion

    private bool isMounted = false;

    public void EnterMountedScope()
    {
        isMounted = true;
        if (sniperCfg != null)
            sniperCfg.ForceScopeOn();
    }

    public void ExitMountedScope()
    {
        isMounted = false;
        if (sniperCfg != null)
            sniperCfg.ExitScope();
    }

    public bool TryFireMounted()
    {
        if (!isMounted) return false;

        // normalny strzał – Fire() i tak pilnuje amunicji / fire rate
        Fire();
        return true;
    }

    public void CombatFireInput(bool firePressed, bool fireHeld)
    {
        bool fireInput = fireMode == FireMode.SemiAuto ? firePressed : fireHeld;

        if (!fireInput) return;

        // Shotgun / Half-Life style:
        // jeżeli trwa reload i mamy chociaż 1 nabój, LPM przerywa reload i strzela
        if (IsReloading && currentAmmo > 0)
        {
            reloadController?.StopReload();

            Fire();
            nextFireTime = Time.time + GetFireRate();
            return;
        }

        if (IsReloading)
        {
            return;
        }

        if (Time.time >= nextFireTime)
        {
            if (currentAmmo > 0 || InfAmmo)
            {
                Fire();
                nextFireTime = Time.time + GetFireRate();
            }
            else if (totalAmmo > 0)
            {
                reloadController?.StartReload();
            }
        }
    }

    public void CombatReloadInput(bool reloadPressed)
    {
        if (!reloadPressed) return;

        if (!InfAmmo &&
            currentAmmo < GetMagazineSize() &&
            totalAmmo > 0 &&
            !IsReloading)
        {
            reloadController?.StartReload();
        }
    }
    public void CombatADSInput(bool altPressed, bool altHeld, float scroll)
    {
        if (IsReloading)
        {
            if (_isADS) SetADS(false);
            return;
        }

        if (HasScope() && sniperCfg)
        {
            if (altPressed)
            {
                if (sniperCfg.IsScoped())
                {
                    sniperCfg.ExitScope();
                    SetADS(false);
                }
                else
                {
                    sniperCfg.EnterScope(1);
                    SetADS(true);
                }
            }

            if (sniperCfg.IsScoped() && !IsReloading)
                sniperCfg.HandleZoomInput();

            return;
        }

        if (altHeld != _isADS && Time.time - lastAdsChangeTime >= adsCooldown)
        {
            SetADS(altHeld);
            lastAdsChangeTime = Time.time;
        }

        if (!altHeld && _isADS && Time.time - lastAdsChangeTime >= adsCooldown)
        {
            SetADS(false);
            lastAdsChangeTime = Time.time;
        }
    }

    public bool HasInfiniteAmmo()
    {
        return CheatState.InfiniteAmmo && !isControlledByNPC;
    }

    public bool IsPlayerDead()
    {
        return PlayerDead;
    }

    public bool IsShotgunWeapon()
    {
        return IsShotgun;
    }

    public void RefreshBulletUI()
    {
        _gunUI?.UpdateBulletIcons(currentAmmo);
    }

    public void OnReloadFinished()
    {
        if (sniperCfg)
            sniperCfg.SetZoomBlocked(false);
    }

    public WeaponItemData GetWeaponData()
    {
        return weaponData;
    }

    public void ApplyScopedSensitivityPublic(bool scoped, bool ads)
    {
        ApplyScopedSensitivity(scoped, ads);
    }

    public void StartSniperFireCooldown(float duration)
    {
        StartFireCooldownBar(duration);
    }

    public void StopSniperFireCooldown()
    {
        if (_fireCooldownBarCo != null)
        {
            StopCoroutine(_fireCooldownBarCo);
            _gunUI?.StopSniperCooldown();
            _fireCooldownBarCo = null;
        }
    }

    public void StartAutoReloadAfterDelay(float delay)
    {
        StartCoroutine(AutoReloadAfterDelay(delay));
    }

    public void BindRecoilController(RecoilController controller)
    {
        recoilController = controller;
        weaponRecoil?.SetCameraRecoilController(controller);
    }

    public void RebindRuntimeReferences()
    {
        fireController?.ApplyWeaponRefs(
            bulletPrefab,
            firePoint,
            hitMask,
            hitEffectPrefab,
            muzzleFlash,
            shellEjectParticles,
            recoilController,
            fireTriggerName
        );

        fireController?.BindCamera(playerCamera);

        fovController?.BindCamera(playerCamera, true);

        scopeController?.RefreshSniperReference(sniperCfg);
        scopeController?.BindOverlayCamera(weaponOverlayCamera);

        weaponRecoil?.SetCameraRecoilController(recoilController);
    }

    public void ForceCancelCombatState()
    {
        if (reloadController != null)
        {
            if (reloadController.IsReloading)
                reloadController.StopReload();

            reloadController.CleanupOnDisable();
        }

        SetADS(false);

        if (sniperCfg != null)
            sniperCfg.ExitScope();

        lastAdsChangeTime = Time.time;

        if (_fireCooldownBarCo != null)
        {
            StopCoroutine(_fireCooldownBarCo);
            _gunUI?.StopSniperCooldown();
            _fireCooldownBarCo = null;
        }

        ApplyScopedSensitivity(false, false);

        scopeController?.ResetScopeVisuals();

        adsController?.ResetADS();
    }
}