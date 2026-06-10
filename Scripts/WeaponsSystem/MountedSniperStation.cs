using TMPro;
using UnityEngine;

public class MountedSniperStation : MonoBehaviour
{
    [Header("Pozycja gracza")]
    public Transform mountPoint;          // PlayerPosition

    [Header("Snajperka na stanowisku")]
    public Transform rotationRoot;        // SniperRiffle (obracamy tym)
    public Transform cameraAnchor;        // CameraAnchor na lunecie / przy broni
    public Gun sniperGun;                 // Gun na SniperRiffle
    public SniperRifleBehaviour sniperCfg;// SniperRifleBehaviour na SniperRiffle
    public Camera sniperCamera;           // MainCamera gracza

    [Header("Model snajperki (do ukrywania w scope)")]
    [SerializeField] private Renderer[] rifleRenderers;

    [Header("Limity kątów (względem pozycji startowej)")]
    public float minYaw = -45f;
    public float maxYaw = 45f;
    public float minPitch = -20f;
    public float maxPitch = 20f;

    [Header("Czułość obrotu")]
    public float yawSensitivity = 0.15f;
    public float pitchSensitivity = 0.15f;
    [SerializeField] private float mountedScopeSensitivityMultiplier = 0.15f;

    [Header("Limit strzałów stacji")]
    public int stationMaxShots = 10;   // np. 10 – całkowita liczba strzałów stanowiska
    [SerializeField] private int stationShotsLeft;   // ile jeszcze zostało (pokazywane w UI)

    [Header("HUD / overlay gracza / stacji")]
    public GunUI playerGunUI;                    // Canvas->PlayerGUI->GunUI
    public GameObject playerWeaponsAmmoInfoRoot;// Canvas->PlayerGUI->GunUI->WeaponsAmmoInfo
    public GameObject sniperStationUIRoot;       // Canvas->PlayerGUI->SniperStationUI
    public TextMeshProUGUI stationAmmoText;      // SniperStationUI/AmmoText

    [Header("Bullet Icons UI (SniperStation)")]
    public Transform stationBulletContainer;     // SniperStationUI/AmmoBullets
    public GameObject stationBulletIconPrefab;   // ten sam prefab co w GunUI

    [Header("Ukrywanie rąk FPS")]
    [SerializeField] private GameObject unarmedHandsFPS;

    public bool IsPlayerMounted { get; private set; }

    // --- stan wewnętrzny ---
    Transform _playerRoot;
    PlayerMovement _playerMovement;
    MouseLook _mouseLook;
    WeaponManager _weaponManager;
    CameraSwitcher _cameraSwitcher;
    ThirdPersonCameraCollision _tppCollision;

    Vector3 _savedPlayerPos;
    Quaternion _savedPlayerRot;

    Transform _originalCamParent;
    Vector3 _originalCamLocalPos;
    Quaternion _originalCamLocalRot;

    float _baseYaw;
    float _basePitch;
    float _yawOffset;
    float _pitchOffset;

    int _lastCombinedAmmo;        // poprzednia suma (TotalAmmo + CurrentAmmo)
    bool _ammoTrackingInitialized;

    private float mountInputLockUntil;

    private bool playerInTrigger;
    private Transform cachedPlayerRoot;

    [SerializeField] private float mountInputLockTime = 0.35f;
    [SerializeField] private GameObject playerHolstersRoot;

    private LayerMask savedSniperHitMask;
    [SerializeField] private LayerMask mountedSniperHitMask = ~0;
    [SerializeField] private GameObject playerWeaponSlotsRoot;

    private int savedWeaponIndexBeforeMount = -1;

    private Camera savedWeaponOverlayCamera;
    private int savedWeaponOverlayMask;

    void Start()
    {
        if (!rotationRoot && sniperGun != null)
            rotationRoot = sniperGun.transform;

        // jeśli nie ustawione ręcznie w Inspectorze – zbierz automatycznie wszystkie Renderery pod rotationRoot
        if ((rifleRenderers == null || rifleRenderers.Length == 0) && rotationRoot != null)
            rifleRenderers = rotationRoot.GetComponentsInChildren<Renderer>(true);

        if (stationShotsLeft <= 0)
            stationShotsLeft = stationMaxShots;

        if (sniperGun) sniperGun.enabled = false;
        if (sniperCfg) sniperCfg.enabled = false;

        if (!playerGunUI) playerGunUI = FindFirstObjectByType<GunUI>();

        if (sniperStationUIRoot) sniperStationUIRoot.SetActive(false);
    }

    void Update()
    {
        if (Time.time >= mountInputLockUntil)
        {
            bool togglePressed =
                Input.GetKeyDown(KeyCode.E) ||
                Input.GetKeyDown(KeyCode.Escape);

            if (togglePressed)
            {
                if (IsPlayerMounted)
                {
                    UnmountPlayer();
                    mountInputLockUntil = Time.time + mountInputLockTime;
                    return;
                }

                if (playerInTrigger && cachedPlayerRoot != null)
                {
                    MountPlayer(cachedPlayerRoot);
                    mountInputLockUntil = Time.time + mountInputLockTime;
                    return;
                }
            }
        }

        if (!IsPlayerMounted) return;

        var input = PlayerInputHandler.Instance;

        Vector2 look = input != null
            ? input.LookDelta
            : new Vector2(
                Input.GetAxisRaw("Mouse X"),
                Input.GetAxisRaw("Mouse Y")
            );


        // >>> poprawiona oś – mysz w górę => celownik w górę
        _yawOffset += look.x * yawSensitivity;
        _pitchOffset += look.y * pitchSensitivity;

        _yawOffset = Mathf.Clamp(_yawOffset, minYaw, maxYaw);
        _pitchOffset = Mathf.Clamp(_pitchOffset, minPitch, maxPitch);

        float yaw = _baseYaw + _yawOffset;
        float pitch = _basePitch + _pitchOffset;

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

        if (rotationRoot)
            rotationRoot.rotation = rot;

        if (sniperCamera && cameraAnchor)
        {
            sniperCamera.transform.position = cameraAnchor.position;
            sniperCamera.transform.rotation = cameraAnchor.rotation;
        }

        HandleMountedWeaponInput();
    }

    void LateUpdate()
    {
        if (!IsPlayerMounted) return;

        // 1) Kamera przyklejona do lunety
        if (sniperCamera && cameraAnchor)
        {
            sniperCamera.transform.position = cameraAnchor.position;
            sniperCamera.transform.rotation = cameraAnchor.rotation;
        }

        if (_playerMovement != null)
            _playerMovement.UpdateExternalUIOnly();

        // 2) Śledzenie zużycia amunicji stacji
        TrackStationAmmo();

        // 3) Ukrywanie modelu snajperki przy włączonym scope
        bool scoped = sniperCfg != null && sniperCfg.IsScoped();

        SetRifleModelVisible(!scoped);

        if (_mouseLook != null)
        {
            if (scoped)
            {
                float sensMul =
                    mountedScopeSensitivityMultiplier;

                _mouseLook.SetSensitivityMultiplier(sensMul);
            }
            else
            {
                _mouseLook.ResetSensitivity();
            }
        }
        if (_playerMovement != null)
            _playerMovement.UpdateExternalUIOnly();

        if (unarmedHandsFPS != null)
            unarmedHandsFPS.SetActive(false);
    }

    // ================== STATION ACTIONS ==================

    private void HandleMountedWeaponInput()
    {
        if (!sniperGun) return;

        var input = PlayerInputHandler.Instance;

        bool firePressed = input != null ? input.FirePressed : Input.GetMouseButtonDown(0);
        bool fireHeld = input != null ? input.FireHeld : Input.GetMouseButton(0);

        bool altPressed = input != null ? input.FireAltPressed : Input.GetMouseButtonDown(1);
        bool altHeld = input != null ? input.FireAltHeld : Input.GetMouseButton(1);

        sniperGun.CombatADSInput(altPressed, altHeld, Input.mouseScrollDelta.y);
        sniperGun.CombatFireInput(firePressed, fireHeld);
    }

    void SetRifleModelVisible(bool visible)
    {
        if (rifleRenderers == null) return;

        foreach (var r in rifleRenderers)
            if (r != null)
                r.enabled = visible;
    }


    void TrackStationAmmo()
    {
        if (sniperGun == null) return;

        // kombinacja total + current z Guna
        int combined = sniperGun.GetTotalAmmo() + sniperGun.GetCurrentAmmo();


        if (!_ammoTrackingInitialized)
        {
            _lastCombinedAmmo = combined;
            _ammoTrackingInitialized = true;
            return;
        }

        int diff = _lastCombinedAmmo - combined; // >0 = wystrzelono diff pocisków

        if (diff > 0)
        {
            stationShotsLeft = Mathf.Max(0, stationShotsLeft - diff);

            // Jeśli limit wystrzelony – blokujemy broń całkowicie
            if (stationShotsLeft <= 0)
            {
                sniperGun.SetAmmo(0, 0);
                combined = 0;
            }

            UpdateStationAmmoUI();
        }

        _lastCombinedAmmo = combined;
    }

    /// <summary>
    /// Dopasuj ammo broni do pozostałego limitu stacji.
    /// Wywołuj przy montowaniu gracza.
    /// </summary>
    void ClampGunAmmoToStationLimit()
    {
        if (sniperGun == null) return;

        int combinedGun = sniperGun.GetTotalAmmo() + sniperGun.GetCurrentAmmo();


        // Nie pozwól żeby broń miała więcej amunicji niż stacja (np. magazyn 15 a limit 10)
        int allowedCombined = Mathf.Min(combinedGun, stationShotsLeft);

        // Podział na current/total – staramy się zostawić tyle w magazynku ile było,
        // ale nie więcej niż allowedCombined
        int newCurrent = Mathf.Min(sniperGun.GetCurrentAmmo(), allowedCombined);
        int newTotal = Mathf.Max(0, allowedCombined - newCurrent);

        sniperGun.SetAmmo(newCurrent, newTotal);

        _lastCombinedAmmo = allowedCombined;
        _ammoTrackingInitialized = true;

        UpdateStationAmmoUI();
    }

    void UpdateStationAmmoUI()
    {
        if (stationAmmoText)
            stationAmmoText.text = stationShotsLeft.ToString();

        // Ilość ikon = liczba naboi w magazynku, ale nie więcej niż pozostały limit
        int bulletsInMag = Mathf.Clamp(sniperGun != null ? sniperGun.GetCurrentAmmo() : 0, 0, stationShotsLeft);
        UpdateStationBulletIcons(bulletsInMag);
    }

    void UpdateStationBulletIcons(int count)
    {
        if (stationBulletContainer == null || stationBulletIconPrefab == null) return;

        for (int i = stationBulletContainer.childCount - 1; i >= 0; i--)
            Destroy(stationBulletContainer.GetChild(i).gameObject);

        for (int i = 0; i < count; i++)
            Instantiate(stationBulletIconPrefab, stationBulletContainer);
    }

    // ================== WEJŚCIE / WYJŚCIE ==================

    void MountPlayer(Transform player)
    {
        if (IsPlayerMounted) return;
        if (!sniperGun || !sniperCamera || !rotationRoot || !cameraAnchor)
        {
            Debug.LogWarning("MountedSniperStation: brak referencji (Gun/Camera/RotationRoot/CameraAnchor).", this);
            return;
        }

        if (playerWeaponSlotsRoot) playerWeaponSlotsRoot.SetActive(false);
        if (playerHolstersRoot) playerHolstersRoot.SetActive(false);

        // root gracza
        _playerRoot = player.root;
        _playerMovement = _playerRoot.GetComponentInChildren<PlayerMovement>();
        _mouseLook = _playerRoot.GetComponentInChildren<MouseLook>();
        _weaponManager = _playerRoot.GetComponentInChildren<WeaponManager>();

        savedWeaponIndexBeforeMount = _weaponManager != null
    ? _weaponManager.GetRawCurrentWeaponIndex()
    : -1;

        Gun activeGunBeforeMount = null;

        if (_weaponManager != null)
        {
            GameObject activeObj = _weaponManager.GetCurrentWeaponSlotObject();
            if (activeObj)
                activeGunBeforeMount = activeObj.GetComponentInChildren<Gun>(true);
        }

        if (activeGunBeforeMount != null && activeGunBeforeMount.weaponOverlayCamera != null)
        {
            savedWeaponOverlayCamera = activeGunBeforeMount.weaponOverlayCamera;
            savedWeaponOverlayMask = savedWeaponOverlayCamera.cullingMask;
        }

        // system kamer
        _cameraSwitcher = sniperCamera.GetComponentInParent<CameraSwitcher>();
        _tppCollision = sniperCamera.GetComponentInParent<ThirdPersonCameraCollision>();

        // zapisz pozycję / rotację
        _savedPlayerPos = _playerRoot.position;
        _savedPlayerRot = _playerRoot.rotation;

        // przestaw gracza pod stanowisko
        if (mountPoint)
        {
            _playerRoot.position = mountPoint.position;
            _playerRoot.rotation = mountPoint.rotation;
        }

        // wyłącz ruch i zarządzanie bronią
        if (_playerMovement) _playerMovement.enabled = false;
        if (_mouseLook) _mouseLook.enabled = false;
        if (_weaponManager != null)
        {
            foreach (var slot in _weaponManager.GetWeaponSlots())
            {
                if (!slot) continue;

                var guns = slot.GetComponentsInChildren<Gun>(true);
                foreach (var g in guns)
                {
                    if (g != null && g != sniperGun)
                    {
                        g.SetExternalCombatInput(true);
                        g.SetADS(false);
                    }
                }

                slot.SetActive(false);
            }
        }

        // wyłącz system przełączania kamer / TPP
        if (_cameraSwitcher) _cameraSwitcher.enabled = false;
        if (_tppCollision) _tppCollision.enabled = false;

        // przepnij kamerę na CameraAnchor
        _originalCamParent = sniperCamera.transform.parent;
        _originalCamLocalPos = sniperCamera.transform.localPosition;
        _originalCamLocalRot = sniperCamera.transform.localRotation;

        sniperCamera.transform.SetParent(cameraAnchor, worldPositionStays: false);
        sniperCamera.transform.localPosition = Vector3.zero;
        sniperCamera.transform.localRotation = Quaternion.identity;

        // bazowy kąt z aktualnej rotacji snajperki
        Vector3 euler = rotationRoot.rotation.eulerAngles;
        _baseYaw = euler.y;
        _basePitch = euler.x;
        _yawOffset = 0f;
        _pitchOffset = 0f;

        // włącz logikę snajperki – zachowuje się jak normalna broń
        sniperGun.enabled = true;
        if (sniperCfg) sniperCfg.enabled = true;

        sniperGun.BindCamera(sniperCamera, true);
        sniperGun.RebindRuntimeReferences();
        savedSniperHitMask = sniperGun.hitMask;
        sniperGun.hitMask = mountedSniperHitMask;
        sniperGun.RebindRuntimeReferences();

        sniperGun.isControlledByNPC = false;

        sniperGun.SetAmmo(
            Mathf.Min(sniperGun.GetMagazineSize(), stationShotsLeft),
            Mathf.Max(0, stationShotsLeft - sniperGun.GetMagazineSize())
        );

        if (sniperCfg != null)
        {
            sniperCfg.autoReturnAfterShot = true;
            sniperCfg.ExitScope();
        }

        if (unarmedHandsFPS)
            unarmedHandsFPS.SetActive(false);
        
        // HUD – wyłącz tylko WeaponsAmmoInfo, włącz SniperStationUI
        if (playerWeaponsAmmoInfoRoot) playerWeaponsAmmoInfoRoot.SetActive(false);
        if (sniperStationUIRoot) sniperStationUIRoot.SetActive(true);

        // dopasuj ammo broni do limitu stacji
        ClampGunAmmoToStationLimit();

        IsPlayerMounted = true;
        MountedSniperState.IsActive = true;

        mountInputLockUntil = Time.time + mountInputLockTime;

        sniperGun.SetExternalCombatInput(true);
        sniperGun.SetADS(false);

        if (sniperCfg != null)
            sniperCfg.ExitScope();
    }

    void UnmountPlayer()
    {
        if (!IsPlayerMounted) return;

        // wyłącz scope + snajperkę
        if (sniperCfg)
        {
            sniperCfg.ExitScope();
            sniperCfg.enabled = false;
        }

        sniperGun.SetExternalCombatInput(false);
        sniperGun.SetADS(false);
        sniperGun.hitMask = savedSniperHitMask;
        sniperGun.RebindRuntimeReferences();

        if (playerWeaponSlotsRoot) playerWeaponSlotsRoot.SetActive(true);
        if (playerHolstersRoot) playerHolstersRoot.SetActive(true);

        if (sniperGun)
            sniperGun.enabled = false;

        // oddaj kamerę graczowi
        if (sniperCamera && _originalCamParent)
        {
            sniperCamera.transform.SetParent(_originalCamParent, worldPositionStays: false);
            sniperCamera.transform.localPosition = _originalCamLocalPos;
            sniperCamera.transform.localRotation = _originalCamLocalRot;
        }

        // przywróć system kamer
        if (_cameraSwitcher) _cameraSwitcher.enabled = true;
        if (_tppCollision) _tppCollision.enabled = true;

        // przywróć pozycję / rotację gracza
        if (_playerRoot)
        {
            _playerRoot.position = _savedPlayerPos;
            _playerRoot.rotation = _savedPlayerRot;
        }

        if (_playerMovement) _playerMovement.enabled = true;
        if (_mouseLook) _mouseLook.enabled = true;
        if (_weaponManager != null)
        {
            foreach (var slot in _weaponManager.GetWeaponSlots())
            {
                if (!slot) continue;

                var guns = slot.GetComponentsInChildren<Gun>(true);
                foreach (var g in guns)
                {
                    if (g != null && g != sniperGun)
                    {
                        g.SetExternalCombatInput(false);
                        g.SetADS(false);
                    }
                }
            }

            if (savedWeaponIndexBeforeMount >= 0 &&
                savedWeaponIndexBeforeMount < _weaponManager.GetWeaponSlots().Length &&
                _weaponManager.HasWeapon(savedWeaponIndexBeforeMount))
            {
                _weaponManager.SelectWeapon(savedWeaponIndexBeforeMount);
            }
            else
            {
                _weaponManager.ActivateHandsOnly();
            }

            _weaponManager.RefreshWeaponHUD();
        }

        // HUD – z powrotem HUD broni gracza, wyłącz HUD stacji
        if (playerWeaponsAmmoInfoRoot) playerWeaponsAmmoInfoRoot.SetActive(true);
        if (sniperStationUIRoot) sniperStationUIRoot.SetActive(false);

        if (_mouseLook != null)
            _mouseLook.ResetSensitivity();

        if (unarmedHandsFPS)
        {
            bool shouldShowHands =
                _weaponManager == null ||
                _weaponManager.IsUsingHandsOnly();

            unarmedHandsFPS.SetActive(shouldShowHands);
        }

        if (savedWeaponOverlayCamera != null)
        {
            savedWeaponOverlayCamera.cullingMask = savedWeaponOverlayMask;
        }

        MountedSniperState.IsActive = false;
        IsPlayerMounted = false;
        Debug.Log("MountedSniperStation: Player unmounted.");
        SetRifleModelVisible(true);
        mountInputLockUntil = Time.time + mountInputLockTime;
    }

    // ================== TRIGGER / E ==================

    void OnTriggerStay(Collider other)
    {
        Transform t = other.transform;
        Transform playerRoot = null;

        while (t != null)
        {
            if (t.CompareTag("Player"))
            {
                playerRoot = t;
                break;
            }

            t = t.parent;
        }

        if (!playerRoot) return;

        playerInTrigger = true;
        cachedPlayerRoot = playerRoot;
    }

    void OnTriggerExit(Collider other)
    {
        Transform t = other.transform;

        while (t != null)
        {
            if (t.CompareTag("Player"))
            {
                playerInTrigger = false;
                cachedPlayerRoot = null;
                return;
            }

            t = t.parent;
        }
    }
}
