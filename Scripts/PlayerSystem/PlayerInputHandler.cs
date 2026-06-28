using UnityEngine;
using UnityEngine.InputSystem; // zostaw – dla New Input System

public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance;

    private InputActions inputActions;
    public InputActionMap playerMap { get; private set; }

    public static bool GameplayInputBlocked;

    // ── OPTIONAL: czułość do freelocka (możesz wystawić w Inspectorze)
    [SerializeField] public float lookSensitivity = 1.0f;

    // cache na akcję "Look", jeśli istnieje w Input Actions
    private InputAction _lookAction;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        inputActions = new InputActions();
        playerMap = inputActions.Player;

        // spróbuj znaleźć akcję „Look” (Vector2)
        _lookAction = playerMap.FindAction("Look", throwIfNotFound: false);
    }


    void OnEnable() => inputActions.Enable();
    void OnDisable() => inputActions.Disable();

    public Vector2 Move => playerMap.FindAction("Move").ReadValue<Vector2>();
    public bool IsSprinting => playerMap.FindAction("Sprint").IsPressed();
    public bool IsCrouching => playerMap.FindAction("Crouch").IsPressed();
    public bool JumpPressed => playerMap.FindAction("Jump").triggered;
    public bool JumpHeld => playerMap.FindAction("Jump").IsPressed();
    public bool FirePressed => !GameplayInputBlocked && playerMap.FindAction("Fire").IsPressed();
    public bool FirePressedThisFrame => !GameplayInputBlocked && playerMap.FindAction("Fire").triggered;
    public bool FireReleasedThisFrame => !GameplayInputBlocked && playerMap.FindAction("Fire").WasReleasedThisFrame();
    public bool FireHeld => !GameplayInputBlocked && playerMap.FindAction("Fire").ReadValue<float>() > 0.1f;
    public bool ReloadPressed => !GameplayInputBlocked && playerMap.FindAction("Reload").IsPressed();
    public bool ReloadPressedThisFrame => !GameplayInputBlocked && playerMap.FindAction("Reload").triggered;
    public bool InteractPressedThisFrame => !GameplayInputBlocked && inputActions.Player.Interact.triggered;
    public bool InteractPressed => !GameplayInputBlocked && playerMap.FindAction("Interact").triggered;
    public bool InteractHeld => !GameplayInputBlocked && playerMap.FindAction("Interact").IsPressed();
    public bool InventoryPressed => !GameplayInputBlocked && playerMap.FindAction("Inventory").triggered;
    public bool ToggleConsolePressed => !GameplayInputBlocked && playerMap.FindAction("ToggleConsole").triggered;
    public bool MapTogglePressedThisFrame => playerMap.FindAction("MapToggle")?.WasPressedThisFrame() == true;
    public bool ObjectivesPressedThisFrame => !GameplayInputBlocked && playerMap.FindAction("Objectives") != null && playerMap.FindAction("Objectives").triggered;
    public bool ObjectivesRawPressedThisFrame => playerMap.FindAction("Objectives")?.triggered ?? false;
    public bool FireAltHeld => !GameplayInputBlocked && playerMap.FindAction("FireAlt").ReadValue<float>() > 0.1f;
    public bool FireAltPressed => !GameplayInputBlocked && playerMap.FindAction("FireAlt").triggered;
    public bool DropWeaponPressed => !GameplayInputBlocked && playerMap.FindAction("DropWeapon").triggered;
    public bool DropWeaponPressedThisFrame => !GameplayInputBlocked && playerMap.FindAction("DropWeapon").triggered;
    public bool SwitchCameraPressedThisFrame => playerMap.FindAction("SwitchCamera")?.WasPressedThisFrame() ?? false;
    public bool PrevWeaponPressedThisFrame => !GameplayInputBlocked && (playerMap.FindAction("PrevWeapon")?.triggered ?? false);
    public bool QuickSavePressedThisFrame => playerMap.FindAction("QuickSave")?.triggered ?? false;
    public bool QuickLoadPressedThisFrame => playerMap.FindAction("QuickLoad")?.triggered ?? false;

    public Vector2 LookDelta
    {
        get
        {
            if (GameplayInputBlocked) return Vector2.zero;
            if (_lookAction != null) return _lookAction.ReadValue<Vector2>() * lookSensitivity;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.delta.ReadValue() * lookSensitivity;
#endif
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * lookSensitivity;
        }
    }

    public static void SetGameplayBlocked(bool blocked)
    {
        GameplayInputBlocked = blocked;
    }

}
