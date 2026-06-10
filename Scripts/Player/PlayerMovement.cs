using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour, IWettable
{
    private PlayerStats playerStats;
    private Transform cachedCameraTransform;
    private CameraSwitcher cameraSwitcher;
    private Collider carriedColliderCache;

    // ——— RUCH / STAMINA ———
    [Header("Ruch")]
    public float speed = 5f;
    public float crouchSpeed = 2.5f;
    public float jumpHeight = 1.5f;
    public float gravity = -15.24f;
    private float _defaultStepOffset;
    [Range(0f, 1f)] public float airControlFactor = 0.2f;
    public bool blockJump = false;
    private int defaultObstacleMask;

    [Header("Kolizje z pojazdami")]
    public LayerMask vehicleCollisionMask;      // ustaw na warstwę: Vehicle (lub taką, na której są auta)
    [Range(0f, 0.1f)] public float vehicleSkin = 0.03f;  // mały margines


    public bool IsInVehicle { get; set; }   // ustawiane przez CarInteraction
    CharacterController _cc;                // cache

    [Header("Wizualny offset od ziemi")]
    public float visualGroundOffset = 0f;   // wpisz np. 0.07 w Inspectorze
    private float _centerYOffset = 0f;

    // PlayerMovement.cs
    [HideInInspector]
    public bool elevatorLockVertical = false;   // true = winda steruje Y, my nie ruszamy

    [Header("Kucanie")]
    private Vector3 originalModelLocalPos;
    public float crouchScale = 0.5f;
    public Transform playerModel;
    public Transform weaponCamera;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    private Vector3 originalScale;
    private float originalSpeed;
    private float standingHeight = 2.0f;
    private float crouchedHeight = 1.0f;

    private float normalWeaponHeight;
    private float crouchWeaponHeight;

    [SerializeField] Transform cameraPivot;        // np. Player/CameraHolder/MainCamera
    [Range(4f, 20f)] public float crouchLerp = 10f;
    [Range(0.2f, 1.0f)] public float crouchCamDrop = 0.6f;

    float _camOrigLocalY;

    [Header("Sprint i kondycja")]
    public float sprintSpeed = 8f;
    public float staminaMax = 100f;
    public float staminaDrainPerSecond = 20f;
    public float staminaRegenWalking = 10f;
    public float staminaRegenIdle = 20f;
    public float regenCooldown = 4f;

    private float _lastStaminaUI = -999f;
    private int _lastStaminaText = -999;

    [Tooltip("Poniżej tej wartości gracz zaczyna słabnąć (delikatne spowolnienie).")]
    public float lowStaminaThreshold = 60f;
    [Tooltip("Poniżej tej wartości gracz jest wyczerpany (mocne spowolnienie).")]
    public float exhaustedThreshold = 15f;
    [Range(0.4f, 1f)] public float lowStaminaSpeedMultiplier = 0.85f;
    [Range(0.2f, 1f)] public float exhaustedSpeedMultiplier = 0.65f;

    [Header("Weapon Load (fallback gdy item ma 1.0)")]
    [Range(0.5f, 1.2f)] public float fallbackMeleeMoveMul = 1.02f;
    [Range(0.5f, 2f)] public float fallbackMeleeStaminaMul = 0.95f;
    [Range(0.5f, 1.2f)] public float fallbackPistolMoveMul = 0.96f;
    [Range(0.5f, 2f)] public float fallbackPistolStaminaMul = 1.05f;
    [Range(0.5f, 1.2f)] public float fallbackRifleMoveMul = 0.88f;
    [Range(0.5f, 2f)] public float fallbackRifleStaminaMul = 1.20f;
    [Range(0.5f, 1.2f)] public float fallbackNadeMoveMul = 0.98f;
    [Range(0.5f, 2f)] public float fallbackNadeStaminaMul = 1.00f;

    private float _activeWeaponMoveMul = 1f;
    private float _activeWeaponStaminaMul = 1f;

    private GameObject _cachedWeaponLoadObject;
    private int _cachedWeaponLoadIndex = int.MinValue;
    private bool _cachedHandsOnly;

    [Header("Koszt skoku")]
    public float jumpStaminaCost = 8f;

    private float currentStamina;
    private float staminaRegenTimer;
    private bool isSprinting;
    private bool staminaDepleted;
    private float staminaDistanceTracker = 0f;
    private int staminaLevel = 0;
    public Image staminaBarFill;
    public TextMeshProUGUI staminaText;
    public bool IsTryingToSprint { get; private set; }

    private FatigueBreathCamera breathCam;

    [Header("Debugowanie")]
    public bool showDebug = false;

    [Header("Drabinka")]
    private bool onLadder = false;
    private Transform ladderTransform;
    private float ladderJumpTimer = 0f;
    public float ladderSpeed = 2.5f;

    [Header("Woda")]
    private bool inWater = false;
    private float waterSurfaceY;
    public float swimSpeed = 3f;
    public float swimGravity = -1.5f;
    public float verticalSwimMultiplier = 1.5f;
    public float headOffsetFromFeet = 1.6f;

    private bool isClimbingOutOfWater = false;
    private Vector3 climbTargetPosition;
    public bool IsClimbingOutOfWater => isClimbingOutOfWater;

    public static bool IsMovementLocked = false;

    // --- HL2 CARRY ---
    [Header("Carry (HL2 – LPM + scroll)")]
    public float carryDefaultHold = 1.2f; // ile na starcie KAŻDEGO podniesienia
    public float carryHoldDistance = 1.2f;
    public float carryMinHold = 0.05f;
    public float carryMaxHold = 2.0f;
    public float carryScrollSpeed = 1.8f;
    public float carryEyeLocalYOffset = -0.03f; // lekko pod celownikiem (lokalnie względem kamery)
    public float carryMaxDistance = 3.0f;
    public float dropDistance = 6.0f;
    public float carryStaminaDrainPerSec = 10f;
    [Range(0.02f, 0.25f)] public float dropThresholdPct = 0.10f;

    private Transform carryAnchor;         // ZAWSZE dziecko aktywnej kamery
    private MovableObject carried;
    private WeaponManager wm;
    private float carryRegrabLock = 0f;   // krótki lock po wymuszonej zmianie
    private Vector3 _lastCarryAnchorPos;
    private Quaternion _lastCarryAnchorRot;

    [Header("Spowolnienie podczas noszenia")]
    [Range(0.35f, 1f)] public float carryingSpeedMul = 0.70f;

    [Header("Carry – kolizje i podłoga")]
    public LayerMask groundMask = ~0;         // warstwy traktowane jako podłoga/geo
    public LayerMask obstacleMask = ~0;       // warstwy kolizyjne przed graczem
    public float groundClearance = 0.02f;     // minimalny margines nad podłogą

    [Header("Carry – scroll / clamp")]
    public bool invertCarryScroll = false;     // jeśli chcesz odwrócić kierunek scrolla
    public float carryMinClearance = 0.02f;    // minimalny margines od gracza/geo

    [Header("Marker lądowania")]
    public GameObject landingMarkerPrefab;   // przypnij Prefab (np. mały quad z przezroczystym sprite'em)
    public float landingMarkerScale = 0.35f;
    public LayerMask landingMask = ~0;       // zwykle Default/Obstacle
    private Transform landingMarker;         // runtime instancja


    // ——— CHEATS ———
    private bool NoStamina => CheatState.InfiniteStamina; // CO2
    private float BoltMul => Mathf.Max(0.01f, CheatState.PlayerSpeedMultiplier); // BOLT

    // ================== START ==================
    
    void Start()
    {
        _defaultStepOffset = controller.stepOffset;

        // kompensacja SkinWidth – żeby dół kapsuły faktycznie był na Y = 0
        _centerYOffset = -controller.skinWidth;

        controller.height = standingHeight;
        controller.center = new Vector3(0f, standingHeight / 2f + _centerYOffset, 0f);

        if (!cameraPivot) cameraPivot = cachedCameraTransform;

        if (cameraPivot) _camOrigLocalY = cameraPivot.localPosition.y;

        if (playerModel)
        {
            originalModelLocalPos = playerModel.localPosition;
            // „zatop” model od razu o podaną wartość
            originalModelLocalPos += Vector3.down * visualGroundOffset;
            playerModel.localPosition = originalModelLocalPos;
        }

        originalModelLocalPos = playerModel ? playerModel.localPosition : Vector3.zero;

        originalScale = playerModel.localScale;
        originalSpeed = speed;
        currentStamina = staminaMax;

        if (weaponCamera != null)
        {
            normalWeaponHeight = weaponCamera.localPosition.y;
            crouchWeaponHeight = normalWeaponHeight - 0.15f;
        }
        wm = FindFirstObjectByType<WeaponManager>();

        // w Start():
        var cam = cachedCameraTransform;
        if (cam != null) breathCam = cam.GetComponent<FatigueBreathCamera>();

        if (carryAnchor == null && cam != null)
        {
            var go = new GameObject("CarryAnchor");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = Vector3.zero;            // punkt tuż przy kamerze
            go.transform.localRotation = Quaternion.identity;
            carryAnchor = go.transform;
        }

        // marker – instancja (ukryty na starcie)
        if (landingMarkerPrefab != null && landingMarker == null)
        {
            landingMarker = Instantiate(landingMarkerPrefab).transform;
            landingMarker.gameObject.SetActive(false);
            landingMarker.localScale = Vector3.one * landingMarkerScale;
        }
    }

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        controller = _cc;

        playerStats = GetComponentInParent<PlayerStats>();

        Camera cam = Camera.main;
        if (cam != null)
            cachedCameraTransform = cam.transform;

        cameraSwitcher = CameraSwitcher.Instance;
        defaultObstacleMask = LayerMask.GetMask("Default", "Obstacle");
    }

    void Update()
    {

        if (carryRegrabLock > 0f) carryRegrabLock -= Time.deltaTime;

        if (IsMovementLocked)
        {
            // ➜ gdy gracz jest „zablokowany” (np. siedzi w aucie),
            //    NIE ruszamy postaci, ale stamina normalnie się odradza
            PassiveStaminaTick();
            UpdateStaminaUI(); // jeśli używasz tego HUD-u
            return;
        }

        if (IsInVehicle || controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
        {
            PassiveStaminaTick();
            UpdateStaminaUI();
            return;
        }

        if (ATMUIController.AnyOpen)
        {
            PassiveStaminaTick();
            UpdateStaminaUI();
            return;
        }

        if (InventoryUI.IsInventoryOpen) return;
        if (playerStats != null && playerStats.IsDead) return;

        if (ladderJumpTimer > 0f) { HandleLadderJump(); return; }

        bool wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;

        // stepOffset: na ziemi oryginał, w powietrzu 0 (bez „zeskakiwania po stopniach”)
        controller.stepOffset = isGrounded ? _defaultStepOffset : 0f;

        if (isGrounded && !wasGrounded)
        {
            if (ladderJumpTimer <= 0.01f) { velocity.x = 0f; velocity.z = 0f; }
        }

        if (inWater && !isClimbingOutOfWater) { HandleSwimming(Vector3.up, waterSurfaceY); return; }
        if (onLadder) { HandleLadderClimb(); return; }

        HandleMovement();
        HandleInteraction();
        UpdateBreathingEffect();
        UpdateStaminaUI();


        // 2) Scroll tylko przy trzymanym LPM – regulacja dystansu
        // 1) Zawsze przypięta do aktywnej kamery (FPS/TPP)
        // utrzymuj kotwicę jako dziecko aktywnej kamery
        var camT = cachedCameraTransform;
        if (camT && carryAnchor && carryAnchor.parent != camT)
            carryAnchor.SetParent(camT, worldPositionStays: false);

        if (carryAnchor)
        {
            carryAnchor.localRotation = Quaternion.identity;
            // pozycja i tak zostanie nadpisana w UpdateCarryAnchorDesiredPose(...)
            // zostawienie tej linii nie szkodzi, ale nie jest już kluczowe:
            carryAnchor.localPosition = new Vector3(0f, carryEyeLocalYOffset, carryHoldDistance);
        }

        if (carried != null)
        {
            UpdateCarryAnchorDesiredPose(forceUseObject: carried);
            UpdateLandingMarker();
        }
        else
        {
            SetLandingMarkerActive(false);
        }
    }
    void HandleMovement()
    {
        var inputHandler = PlayerInputHandler.Instance;

        RefreshWeaponLoadMultipliers();
        if (controller == null || !controller.enabled) return;

        // —— INPUT / MOVE VEC ——
        Vector2 input = inputHandler?.Move ?? Vector2.zero;
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        if (!isGrounded) move = Vector3.Lerp(Vector3.zero, move, airControlFactor);

        // — KUCANIE —
        bool crouchInput = inputHandler?.IsCrouching ?? false;
        bool currentlyCrouched = controller.height < (standingHeight - 0.05f);
        bool canStand = true;

        if (currentlyCrouched || crouchInput) canStand = CanStandUp();
        bool shouldCrouch = crouchInput || (!canStand && currentlyCrouched);

        float targetHeight = shouldCrouch ? crouchedHeight : standingHeight;
        Vector3 targetCenter = new Vector3(0f, targetHeight / 2f + _centerYOffset, 0f);

        // połowa różnicy wysokości kapsuły – tyle „opada” wzrok/korpus
        float crouchDrop = (standingHeight - crouchedHeight) * 0.5f;

        // zastosuj CC
        // w TPP nie zmieniaj środka postaci – kamera to kompensuje wizualnie
        if (cameraSwitcher == null)
            cameraSwitcher = CameraSwitcher.Instance;

        bool isTPP = cameraSwitcher != null && cameraSwitcher.IsTPPActive;

        if (!isTPP)
        {
            controller.height = targetHeight;
            controller.center = targetCenter;
        }

        if (playerModel)
        {
            Vector3 targetModelPos = originalModelLocalPos;

            // tylko w FPS obniżaj model, w TPP nie ma sensu (bo kamera jest z zewnątrz)
            if (!isTPP && shouldCrouch)
                targetModelPos += Vector3.down * crouchDrop;

            playerModel.localPosition = Vector3.Lerp(
                playerModel.localPosition, targetModelPos, Time.deltaTime * crouchLerp);
        }

        // zjazd kamery (HL/CS feeling) – tylko w FPS
        // zjazd kamery przy kucaniu – FPS + TPP (w TPP trochę mniejszy efekt)
        if (cameraPivot)
        {
            // w TPP robimy delikatniejszy drop
            float dropMul = isTPP ? 0.3f : 1f;
            float camTargetY = shouldCrouch
                ? (_camOrigLocalY - crouchCamDrop * dropMul)
                : _camOrigLocalY;

            Vector3 camPos = cameraPivot.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, camTargetY, Time.deltaTime * crouchLerp);
            cameraPivot.localPosition = camPos;
        }

        // wysokość kamery broni – też tylko FPS
        if (!isTPP && weaponCamera)
        {
            var wp = weaponCamera.localPosition;
            float targetWeaponHeight = shouldCrouch ? crouchWeaponHeight : normalWeaponHeight;
            wp.y = Mathf.Lerp(wp.y, targetWeaponHeight, Time.deltaTime * crouchLerp);
            weaponCamera.localPosition = wp;
        }

        // prędkości i broń
        float targetSpeed = shouldCrouch ? crouchSpeed : originalSpeed;
        speed = targetSpeed;

        // —— SPRINT / STAMINA ——
        IsTryingToSprint =
            inputHandler != null &&
            inputHandler.IsSprinting &&
            move != Vector3.zero &&
            !shouldCrouch &&
            isGrounded;

        if (IsTryingToSprint && !staminaDepleted && (NoStamina || currentStamina > 0f))
        {
            // sprint
            speed = sprintSpeed;

            if (!NoStamina)
            {
                float drain = staminaDrainPerSecond * _activeWeaponStaminaMul
                              * Time.deltaTime * Mathf.Max(1f - staminaLevel * 0.05f, 0.3f);

                currentStamina -= drain;
                staminaDistanceTracker += move.magnitude * Time.deltaTime;

                if (staminaDistanceTracker >= 50f) { staminaLevel++; staminaDistanceTracker = 0f; }

                if (currentStamina <= 0f)
                {
                    currentStamina = 0f;
                    staminaDepleted = true;
                    staminaRegenTimer = regenCooldown;
                }
            }
            else
            {
                currentStamina = staminaMax;
                staminaDepleted = false;
                staminaRegenTimer = 0f;
            }
        }
        else
        {
            // brak sprintu
            speed = targetSpeed;

            if (NoStamina)
            {
                currentStamina = staminaMax;
                staminaDepleted = false;
                staminaRegenTimer = 0f;
            }
            else
            {
                if (staminaRegenTimer > 0f) staminaRegenTimer -= Time.deltaTime;
                else if (currentStamina < staminaMax)
                {
                    float regen = (move != Vector3.zero ? staminaRegenWalking : staminaRegenIdle) * Time.deltaTime;
                    currentStamina = Mathf.Min(staminaMax, currentStamina + regen);
                    if (currentStamina >= 25f) staminaDepleted = false;
                }
            }
        }

        if (!NoStamina)
        {
            if (currentStamina <= exhaustedThreshold || staminaDepleted) speed *= exhaustedSpeedMultiplier;
            else if (currentStamina <= lowStaminaThreshold) speed *= lowStaminaSpeedMultiplier;
        }

        // —— CARRY spowolnienie ——
        if (carried != null) speed *= carryingSpeedMul;

        // —— JUMP —— 
        bool jumpRequested = inputHandler?.JumpPressed ?? false;

        // na windzie (gdy elevatorLockVertical == true) traktujemy to jak „sztuczne grounded”
        bool onMovingElevator = elevatorLockVertical;

        // pozwól skoczyć gdy jesteśmy normalnie na ziemi ALBO stoimy na jadącej windzie
        bool canJumpNow = isGrounded || onMovingElevator;

        // dodatkowy bezpiecznik: nie pozwalaj odpalać nowego skoku, jeśli już lecimy w górę
        bool notAlreadyJumpingUp = velocity.y <= 0.01f;

        if (!blockJump && jumpRequested && canJumpNow && notAlreadyJumpingUp)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (!NoStamina && jumpStaminaCost > 0f)
            {
                currentStamina = Mathf.Max(0f, currentStamina - jumpStaminaCost);
                if (currentStamina <= 0f)
                {
                    currentStamina = 0f;
                    staminaDepleted = true;
                    staminaRegenTimer = regenCooldown;
                }
                else
                {
                    staminaRegenTimer = Mathf.Max(staminaRegenTimer, 0.75f);
                }
            }
        }

        // —— FINAL SPEED (cheat + obciążenie bronią) ——
        float finalSpeed = speed * BoltMul * _activeWeaponMoveMul;

        if (move.sqrMagnitude > 1e-6f)
            ResolveVehicleBlocking(ref move, finalSpeed * Time.deltaTime);

        // grawitacja
        // ➜ dodajemy ją ZAWSZE, oprócz sytuacji gdy stoimy na jadącej windzie
        if (!elevatorLockVertical || !isGrounded)
        {
            velocity.y += gravity * Time.deltaTime;
        }
        // else: elevatorLockVertical == true && isGrounded == true
        //       stoję na windzie → nie dokładam grawitacji, winda wozi mnie sama

        // miękki docisk do ziemi – tylko gdy stoimy
        if (isGrounded && velocity.y < 0f)
        {
            if (elevatorLockVertical)
            {
                // na windzie – nie wciskaj w dół, po prostu trzymaj przy podłodze
                velocity.y = 0f;
            }
            else
            {
                // normalnie jak wcześniej
                velocity.y = -2f;
            }
        }

        Vector3 finalMove = move * finalSpeed + velocity;
        controller.Move(finalMove * Time.deltaTime);

    }

    // public/properties – na końcu klasy lub obok innych getterów
    public bool IsCarryingObjectPublic => carried != null;
    public bool IsAdjustingCarryPublic =>
        carried != null && (PlayerInputHandler.Instance?.FireHeld ?? Input.GetMouseButton(0));

    // PlayerMovement.cs  (DODAJ metodę w klasie)
    private void RefreshWeaponLoadMultipliers()
    {
        if (wm == null)
        {
            _activeWeaponMoveMul = 1f;
            _activeWeaponStaminaMul = 1f;
            _cachedWeaponLoadObject = null;
            _cachedWeaponLoadIndex = int.MinValue;
            _cachedHandsOnly = true;
            return;
        }

        bool handsOnly = wm.IsUsingHandsOnly();
        int currentIndex = wm.GetRawCurrentWeaponIndex();
        GameObject currentWeapon = handsOnly ? null : wm.GetCurrentWeaponSlotObject();

        if (_cachedWeaponLoadObject == currentWeapon &&
            _cachedWeaponLoadIndex == currentIndex &&
            _cachedHandsOnly == handsOnly)
        {
            return;
        }

        _cachedWeaponLoadObject = currentWeapon;
        _cachedWeaponLoadIndex = currentIndex;
        _cachedHandsOnly = handsOnly;

        _activeWeaponMoveMul = 1f;
        _activeWeaponStaminaMul = 1f;

        if (handsOnly || currentWeapon == null)
            return;

        InventoryItemData data = null;

        Gun gun = currentWeapon.GetComponentInChildren<Gun>(true);
        if (gun != null && gun.inventoryInstance != null)
        {
            data = gun.inventoryInstance.data;
        }
        else
        {
            Melee melee = currentWeapon.GetComponentInChildren<Melee>(true);
            if (melee != null && melee.inventoryInstance != null)
            {
                data = melee.inventoryInstance.data;
            }
            else
            {
                Grenade grenade = currentWeapon.GetComponentInChildren<Grenade>(true);
                if (grenade != null && grenade.inventoryInstance != null)
                    data = grenade.inventoryInstance.data;
            }
        }

        if (data is not WeaponItemData wid)
            return;

        float moveMul = Mathf.Approximately(wid.moveSpeedMultiplier, 1f)
            ? -1f
            : wid.moveSpeedMultiplier;

        float stamMul = Mathf.Approximately(wid.staminaDrainMultiplier, 1f)
            ? -1f
            : wid.staminaDrainMultiplier;

        if (moveMul < 0f || stamMul < 0f)
        {
            var load = wid.GetDefaultLoad();

            if (moveMul < 0f)
                moveMul = load.moveMul;

            if (stamMul < 0f)
                stamMul = load.staminaMul;
        }

        if (moveMul <= 0f || stamMul <= 0f)
        {
            switch (wid.category)
            {
                case WeaponCategory.Melees:
                    moveMul = fallbackMeleeMoveMul;
                    stamMul = fallbackMeleeStaminaMul;
                    break;

                case WeaponCategory.Pistols:
                    moveMul = fallbackPistolMoveMul;
                    stamMul = fallbackPistolStaminaMul;
                    break;

                case WeaponCategory.Riffles:
                    moveMul = fallbackRifleMoveMul;
                    stamMul = fallbackRifleStaminaMul;
                    break;

                case WeaponCategory.Nades:
                    moveMul = fallbackNadeMoveMul;
                    stamMul = fallbackNadeStaminaMul;
                    break;

                default:
                    moveMul = 1f;
                    stamMul = 1f;
                    break;
            }
        }

        _activeWeaponMoveMul = Mathf.Clamp(moveMul, 0.5f, 1.2f);
        _activeWeaponStaminaMul = Mathf.Clamp(stamMul, 0.5f, 2f);
    }


    void UpdateBreathingEffect()
    {
        if (breathCam == null) return;

        // Zaczynamy „oddech” poniżej lowStaminaThreshold
        float tired = NoStamina ? 0f : Mathf.Clamp01(1f - (currentStamina / Mathf.Max(lowStaminaThreshold, 0.001f)));
        breathCam.UpdateBreath(tired, Time.deltaTime);
    }

    void UpdateStaminaUI()
    {
        float normalized = currentStamina / Mathf.Max(1f, staminaMax);

        if (staminaBarFill != null)
        {
            if (!Mathf.Approximately(_lastStaminaUI, normalized))
            {
                staminaBarFill.fillAmount = normalized;
                _lastStaminaUI = normalized;
            }
        }

        if (staminaText != null)
        {
            int rounded = Mathf.CeilToInt(currentStamina);

            if (_lastStaminaText != rounded)
            {
                staminaText.text = $"Stamina: {rounded}";
                _lastStaminaText = rounded;
            }
        }
    }

    // PlayerMovement.cs  ── DODAJ to pole gdzieś obok innych:
    private void PassiveStaminaTick()
    {
        if (NoStamina)
        {
            currentStamina = staminaMax;
            staminaDepleted = false;
            staminaRegenTimer = 0f;
            return;
        }

        // odliczanie cooldownu po drenażu
        if (staminaRegenTimer > 0f)
            staminaRegenTimer -= Time.deltaTime;
        else if (currentStamina < staminaMax)
        {
            // w aucie traktujemy to jak idle – pełna regeneracja
            float regen = staminaRegenIdle * Time.deltaTime;
            currentStamina = Mathf.Min(staminaMax, currentStamina + regen);
            if (currentStamina >= 25f) staminaDepleted = false;
        }
    }

    private bool CanStandUp()
    {
        float radius = controller.radius;

        Vector3 centerWorld = transform.position + controller.center;
        Vector3 bottom = centerWorld - Vector3.up * (controller.height / 2f - radius);
        Vector3 top = bottom + Vector3.up * (standingHeight - radius * 2f);

        int mask = defaultObstacleMask;
        float checkRadius = radius - 0.05f;

        return !Physics.CheckCapsule(bottom, top, checkRadius, mask, QueryTriggerInteraction.Ignore);
    }

    bool CapsuleCastVehicles(Vector3 dir, float distance, out RaycastHit hit)
    {
        hit = default;
        if (controller == null) return false;

        // kapsuła CharacterControllera
        float r = Mathf.Max(0.01f, controller.radius - 0.005f);
        float half = Mathf.Max(0.1f, controller.height * 0.5f - r);

        Vector3 center = transform.position + controller.center;
        Vector3 p1 = center + Vector3.up * (half - r);
        Vector3 p2 = center - Vector3.up * (half - r);

        return Physics.CapsuleCast(
            p1, p2, r,
            dir.normalized,
            out hit,
            distance,
            vehicleCollisionMask,
            QueryTriggerInteraction.Ignore
        );
    }

    // jeżeli prosto przed graczem stoi pojazd – przesuń ruch tak, by ślizgał się po normalnej
    void ResolveVehicleBlocking(ref Vector3 moveDir, float stepDistance)
    {
        if (moveDir.sqrMagnitude < 1e-6f) return;

        if (CapsuleCastVehicles(moveDir, stepDistance + vehicleSkin, out var hit))
        {
            // rzut na płaszczyznę styczną – „poślizg” zamiast wciskania się w auto
            Vector3 slide = Vector3.ProjectOnPlane(moveDir, hit.normal);
            // zachowaj „siłę” wejściowego ruchu (tylko kierunek zmieniamy)
            if (slide.sqrMagnitude > 1e-6f)
                moveDir = slide.normalized * moveDir.magnitude;
            else
                moveDir = Vector3.zero; // prostopadły najazd w zderzak -> zatrzymaj
        }
    }

    // Swimming
    // ——— WODA ———
    public void EnterWater(float surfaceY)
    {
        inWater = true;
        waterSurfaceY = surfaceY;
        controller.Move(Vector3.down * 0.2f);
        if (showDebug) Debug.Log("💧 EnterWater - wymuszam zanurzenie: velocity.y = -0.5");
    }

    public void ExitWater()
    {
        inWater = false;
        velocity = Vector3.zero;
    }

    public void HandleSwimming(Vector3 waterNormal, float surfaceY)
    {
        var inputHandler = PlayerInputHandler.Instance;
        isGrounded = false;

        if (!inWater || isClimbingOutOfWater) return;

        Vector2 input = inputHandler?.Move ?? Vector2.zero;
        Vector3 move = transform.right * input.x + transform.forward * input.y;

        bool jumpHeld = inputHandler.JumpHeld;
        bool crouchHeld = inputHandler?.IsCrouching ?? false;

        Vector3 verticalMove = Vector3.zero;
        float headY = controller.bounds.max.y;
        float maxHeadY = surfaceY + 0.1f;
        float bottomY = transform.position.y;

        float breathThreshold = surfaceY + 0.05f;
        bool fullySubmerged = headY < breathThreshold;

        if (playerStats != null && playerStats.isUnderwater != fullySubmerged)
            playerStats.SetUnderwaterState(fullySubmerged);

        // Wspinaczka z wody
        if (jumpHeld && headY >= surfaceY - 0.1f)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.8f;
            Vector3 forward = transform.forward;

            if (Physics.Raycast(rayOrigin, forward, out RaycastHit wallHit, 0.6f, defaultObstacleMask))
            {
                Vector3 ledgeCheck = wallHit.point + Vector3.up * 1.2f;
                bool spaceAbove = !Physics.CheckSphere(ledgeCheck, 0.3f, defaultObstacleMask);

                if (spaceAbove)
                {
                    climbTargetPosition = wallHit.point + Vector3.up * 1.3f - forward * 0.1f;
                    isClimbingOutOfWater = true;
                }
            }
        }

        if (isClimbingOutOfWater)
        {
            controller.enabled = false;
            transform.position = climbTargetPosition;
            velocity = Vector3.up * 6f;
            isClimbingOutOfWater = false;
            inWater = false;
            controller.enabled = true;
            return;
        }

        if (jumpHeld)
        {
            float maxHeadAboveWater = 0.25f;
            float currentHeadY = controller.bounds.max.y;
            float targetHeadY = surfaceY + maxHeadAboveWater;
            float diff = targetHeadY - currentHeadY;
            if (diff > 0.01f)
            {
                velocity.y = Mathf.MoveTowards(velocity.y, swimSpeed * 1.2f, Time.deltaTime * 3f);
                verticalMove = Vector3.up;
            }
            else
            {
                velocity.y = Mathf.MoveTowards(velocity.y, 0f, Time.deltaTime * 2f);
            }
        }
        else if (crouchHeld)
        {
            verticalMove = Vector3.down;
            float crouchSinkSpeed = -1.0f;
            velocity.y = Mathf.MoveTowards(velocity.y, crouchSinkSpeed, Time.deltaTime * 2f);
        }
        else
        {
            float passiveFallSpeed = -0.3f;
            velocity.y = Mathf.MoveTowards(velocity.y, passiveFallSpeed, Time.deltaTime * 0.25f);
        }

        velocity.y = Mathf.Clamp(velocity.y, -1.0f, 1.5f);

        Vector3 swimDir = move + verticalMove * verticalSwimMultiplier;
        Vector3 moveTotal = swimDir * swimSpeed * Time.deltaTime + new Vector3(0, velocity.y, 0) * Time.deltaTime;
        controller.Move(moveTotal);

        // --- staminy w wodzie ---
        bool anySwimInput = (move.sqrMagnitude > 1e-4f) || jumpHeld || crouchHeld;

        // czy stoimy na dnie? (krótki spherecast)
        bool onBottom = false;
        {
            float r = Mathf.Max(0.1f, controller.radius * 0.8f);
            onBottom = Physics.SphereCast(transform.position, r, Vector3.down, out _, 0.25f,
                                          defaultObstacleMask,
                                          QueryTriggerInteraction.Ignore);
        }

        if (!NoStamina)
        {
            if (anySwimInput && !onBottom)
            {
                // pływanie zużywa ok. 25% tempa lądowego; wynurzanie/zanurzanie ~40%
                float swimDrainMult = (jumpHeld || crouchHeld) ? 0.40f : 0.25f;
                float drain = staminaDrainPerSecond * swimDrainMult * Time.deltaTime;
                currentStamina = Mathf.Max(0f, currentStamina - drain);
                staminaRegenTimer = Mathf.Max(staminaRegenTimer, 0.5f);
            }
            else
            {
                // brak ruchu albo stoimy na dnie → regeneracja jak na postoju na lądzie
                if (staminaRegenTimer > 0f) staminaRegenTimer -= Time.deltaTime;
                else if (currentStamina < staminaMax)
                {
                    currentStamina = Mathf.Min(staminaMax, currentStamina + staminaRegenIdle * Time.deltaTime);
                    if (currentStamina >= 25f) staminaDepleted = false;
                }
            }
        }
        else
        {
            currentStamina = staminaMax;
            staminaDepleted = false;
            staminaRegenTimer = 0f;
        }
    }

    void HandleInteraction()
    {
        bool handsActive = wm == null || wm.IsUsingHandsOnly();
        if (carried != null && !handsActive)
        {
            carried.EndCarry();
            carried = null;
            carriedColliderCache = null;

            SetLandingMarkerActive(false);
            carryHoldDistance = carryDefaultHold;

            // „bezpiecznik” – przez moment nie łap od razu z powrotem
            carryRegrabLock = 0.25f;
        }

        var input = PlayerInputHandler.Instance;
        bool fireDown = input?.FirePressedThisFrame ?? Input.GetMouseButtonDown(0);
        bool fireHeld = input?.FireHeld ?? Input.GetMouseButton(0);
        bool fireUp = input?.FireReleasedThisFrame ?? Input.GetMouseButtonUp(0);

        // ——— START PODNOSZENIA ———
        if (carried == null && fireDown)
        {
            // tylko gdy aktywne są Hands i nie trwa blokada re-grab
            if (!handsActive || carryRegrabLock > 0f)
                return;

            var cam = cachedCameraTransform;
            if (cam == null) return;

            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, carryMaxDistance))
            {
                if (hit.collider.TryGetComponent(out MovableObject m))
                {
                    carried = m;
                    carriedColliderCache = m.GetComponent<Collider>();
                    float minSafe = GetMinSafeCarryDistance(m);
                    carryHoldDistance = Mathf.Clamp(carryDefaultHold, minSafe, carryMaxHold);
                    UpdateCarryAnchorDesiredPose(forceUseObject: m);
                    carried.BeginCarry(carryAnchor, carryHoldDistance);
                    carried.SetCarryDistance(carryHoldDistance);
                    SetLandingMarkerActive(true);
                }
            }
        }


        // ——— TRZYMANIE / SCROLL / DROP ———
        if (carried != null)
        {
            // stały dren staminy
            if (!NoStamina)
            {
                currentStamina = Mathf.Max(0f, currentStamina - carryStaminaDrainPerSec * Time.deltaTime);
                staminaRegenTimer = Mathf.Max(staminaRegenTimer, 0.25f);
            }
            else
            {
                currentStamina = staminaMax;
                staminaDepleted = false;
                staminaRegenTimer = 0f;
            }


            // regulacja kółkiem TYLKO gdy LPM trzymany
            if (fireHeld)
            {
                // podczas trzymania LPM:
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.0001f)
                {
                    float delta = (invertCarryScroll ? -scroll : scroll) * carryScrollSpeed;
                    float minSafe = GetMinSafeCarryDistance(carried);
                    carryHoldDistance = Mathf.Clamp(carryHoldDistance + delta, minSafe, carryMaxHold);
                    carried.SetCarryDistance(carryHoldDistance);
                }

            }

            // NIE wypuszczaj, gdy dojedziesz do min/max — tylko clamp.
            // Drop TYLKO po puszczeniu LPM lub na wyczerpaniu staminy.
            float stamina01 = currentStamina / Mathf.Max(1f, staminaMax);
            bool exhausted = stamina01 <= dropThresholdPct;

            if (fireUp || exhausted)
            {
                carried.EndCarry();
                carried = null;
                carriedColliderCache = null;
                SetLandingMarkerActive(false);

                // po dropie zawsze wracamy do domyślnego startu przy następnym pick-upie
                carryHoldDistance = carryDefaultHold;
            }
            else
            {
                // liczenie pozycji anchoru (nie schodź pod ziemię ani w ścianę)
                UpdateCarryAnchorDesiredPose(forceUseObject: carried);
                UpdateLandingMarker();
            }
        }
        else
        {
            SetLandingMarkerActive(false);
        }
    }
    float GetMinSafeCarryDistance(MovableObject obj)
    {
        float skin = controller ? controller.skinWidth : 0.03f;
        return Mathf.Max(carryMinHold, skin + Mathf.Max(carryMinClearance, 0.002f));
    }

    // Zwraca połowę wysokości kolajdera – przydatne do marginesów
    float GetHalfHeight(Collider col)
    {
        if (!col) return 0.25f;
        // szacujemy „pionową” połówkę
        return Mathf.Max(0.15f, col.bounds.extents.y);
    }
    void UpdateCarryAnchorDesiredPose(MovableObject forceUseObject = null)
    {
        if (!carryAnchor) return;

        Transform cam = cachedCameraTransform != null
            ? cachedCameraTransform
            : transform;

        // punkt "oka" (lekko pod celownikiem jeśli carryEyeLocalYOffset != 0)
        Vector3 eye = cam.position + cam.TransformVector(0f, carryEyeLocalYOffset, 0f);
        Vector3 fwd = cam.forward;

        // 1) punkt docelowy „na wprost”
        Vector3 desired = eye + fwd * carryHoldDistance;
        Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

        // Wymiary obiektu (do marginesów pionowych)
        float hh = GetHalfHeight(carriedColliderCache);

        // 2) ANTY‑OBSTACLE (przesuń tuż przed ścianę)

        int mask = obstacleMask;
        if (forceUseObject) mask &= ~(1 << forceUseObject.gameObject.layer);

        if (Physics.Raycast(eye, fwd, out RaycastHit frontHit, carryHoldDistance, mask, QueryTriggerInteraction.Ignore))
        {
            // dodatkowa osłona: jeśli jednak trafiło dziecko niesionego obiektu – zignoruj
            bool hitCarried = forceUseObject && frontHit.collider &&
                              frontHit.collider.transform.IsChildOf(forceUseObject.transform);
            if (!hitCarried)
            {
                desired = frontHit.point - fwd * Mathf.Max(carryMinClearance, 0.02f);

                // jeżeli to "podłoga", podnieś nad nią
                if (Vector3.Dot(frontHit.normal, Vector3.up) > 0.5f)
                    desired.y = Mathf.Max(desired.y, frontHit.point.y + hh + carryMinClearance);
            }
        }

        // 3) ANTY‑GROUND (podłoga tylko z tagiem Floor)
        {
            Vector3 probe = desired + Vector3.up * (hh + 0.5f);
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit downHit, hh + 1.0f, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (downHit.collider)
                {
                    bool isFloor = downHit.collider.CompareTag("Floor") || (downHit.collider is TerrainCollider);
                    if (isFloor)
                    {
                        float minY = downHit.point.y + hh + carryMinClearance;
                        if (desired.y < minY) desired.y = minY;
                    }
                }

            }
        }

        // 4) MINIMALNY DYSTANS WZGLĘDEM GRACZA (pozwól „do kontaktu”)
        //    -> tylko kapsuła gracza + drobny margines; bez pół‑głębokości obiektu
        float minSafe =
            controller
            ? Mathf.Max(controller.skinWidth + carryMinClearance, 0.02f)
            : Mathf.Max(carryMinClearance, 0.02f);

        float along = Vector3.Dot(desired - eye, fwd);
        if (along < minSafe)
            desired = eye + fwd * minSafe;

        bool samePos =
            (_lastCarryAnchorPos - desired).sqrMagnitude < 0.000001f;

        bool sameRot =
            Quaternion.Angle(_lastCarryAnchorRot, rot) < 0.01f;

        if (!samePos || !sameRot)
        {
            carryAnchor.SetPositionAndRotation(desired, rot);

            _lastCarryAnchorPos = desired;
            _lastCarryAnchorRot = rot;
        }
    }


    void UpdateLandingMarker()
    {
        if (!carried || !landingMarker) return;

        // przewidywany środek skrzynki: anchor + forward * carryDistance
        float dist = carried ? carried.carryDistance : carryHoldDistance;
        Vector3 probe = carryAnchor.position + carryAnchor.forward * dist;

        // trochę unieś, żeby nie startować spod ziemi
        probe += Vector3.up * 1.0f;

        if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 5f, landingMask, QueryTriggerInteraction.Ignore))
        {
            // akceptuj Floor LUB TerrainCollider (gdy teren nie ma tagu)
            bool ok =
                (hit.collider != null && hit.collider.CompareTag("Floor")) ||
                (hit.collider is TerrainCollider);

            if (ok)
            {
                SetLandingMarkerActive(true);
                landingMarker.position = hit.point + Vector3.up * 0.01f;
                landingMarker.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                return;
            }
        }

        SetLandingMarkerActive(false);
    }

    void HandleLadderJump()
    {
        ladderJumpTimer -= Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
        velocity.y += gravity * Time.deltaTime;
    }

    void HandleLadderClimb()
    {
        Vector2 input = PlayerInputHandler.Instance?.Move ?? Vector2.zero;
        float vertical = input.y;
        Vector3 climb = Vector3.up * vertical * ladderSpeed;
        controller.Move(climb * Time.deltaTime);
        velocity = Vector3.zero;

        if (PlayerInputHandler.Instance?.JumpPressed ?? false)
        {
            ExitLadder();
            Transform cam = cachedCameraTransform != null
                ? cachedCameraTransform
                : transform;

            Vector3 jumpDirection = cam.forward;
            jumpDirection.y = 0f;
            jumpDirection.Normalize();

            Vector3 jumpForce = jumpDirection * speed * airControlFactor;
            jumpForce.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            velocity = jumpForce;
            ladderJumpTimer = 0.25f;
        }
    }

    public void EnterLadder(Transform ladder)
    {
        onLadder = true;
        ladderTransform = ladder;
        velocity = Vector3.zero;
    }

    public void ExitLadder()
    {
        onLadder = false;
        ladderTransform = null;
    }

    // Używane przez system save/load do przywrócenia staminy
    public void ForceSetStamina(float value)
    {
        currentStamina = Mathf.Clamp(value, 0f, staminaMax);
    }

    private void SetLandingMarkerActive(bool active)
    {
        if (!landingMarker) return;

        GameObject go = landingMarker.gameObject;

        if (go.activeSelf != active)
            go.SetActive(active);
    }

    public void UpdateExternalUIOnly()
    {
        PassiveStaminaTick();
        UpdateStaminaUI();
    }

    public float CurrentStamina => currentStamina;
    public float MaxStamina => staminaMax;
    public bool IsSprinting => isSprinting;

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var cc = GetComponent<CharacterController>();
        if (!cc) return;

        Gizmos.color = Color.cyan;
        Vector3 centerW = transform.position + cc.center;

        // pół-wysokość bez promienia
        float half = Mathf.Max(0.01f, cc.height * 0.5f - cc.radius);
        Vector3 p1 = centerW + Vector3.up * half;
        Vector3 p2 = centerW - Vector3.up * half;

        // górna i dolna sfera
        Gizmos.DrawWireSphere(p1, cc.radius);
        Gizmos.DrawWireSphere(p2, cc.radius);
        
        // linie łączące sfery (symulacja cylindra)
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        foreach (var dir in dirs)
        {
            Gizmos.DrawLine(p1 + dir * cc.radius, p2 + dir * cc.radius);
        }
    }
#endif


}
