using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance;

    private InputActions inputActions;

    public InputActionMap playerMap { get; private set; }
    public InputActionMap carMap { get; private set; }

    public static bool GameplayInputBlocked;

    [Header("Look")]
    [SerializeField] public float lookSensitivity = 1.0f;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction interactAction;
    private InputAction fireAction;
    private InputAction fireAltAction;
    private InputAction reloadAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction jumpAction;
    private InputAction inventoryAction;
    private InputAction toggleConsoleAction;
    private InputAction mapToggleAction;
    private InputAction objectivesAction;
    private InputAction dropWeaponAction;
    private InputAction switchCameraAction;
    private InputAction prevWeaponAction;
    private InputAction quickSaveAction;
    private InputAction quickLoadAction;

    private InputAction carMovementAction;
    private InputAction carNitroAction;
    private InputAction carHandbrakeAction;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        inputActions = new InputActions();

        playerMap = inputActions.Player;
        carMap = inputActions.Car;

        CachePlayerActions();
        CacheCarActions();

        LoadBindingOverrides();
    }

    private void OnEnable()
    {
        inputActions?.Enable();
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        inputActions?.Dispose();
        inputActions = null;
    }

    private void CachePlayerActions()
    {
        moveAction = FindPlayerAction("Move");
        lookAction = FindPlayerAction("Look");
        interactAction = FindPlayerAction("Interact");
        fireAction = FindPlayerAction("Fire");
        fireAltAction = FindPlayerAction("FireAlt");
        reloadAction = FindPlayerAction("Reload");
        sprintAction = FindPlayerAction("Sprint");
        crouchAction = FindPlayerAction("Crouch");
        jumpAction = FindPlayerAction("Jump");
        inventoryAction = FindPlayerAction("Inventory");
        toggleConsoleAction = FindPlayerAction("ToggleConsole");
        mapToggleAction = FindPlayerAction("MapToggle");
        objectivesAction = FindPlayerAction("Objectives");
        dropWeaponAction = FindPlayerAction("DropWeapon");
        switchCameraAction = FindPlayerAction("SwitchCamera");
        prevWeaponAction = FindPlayerAction("PrevWeapon");
        quickSaveAction = FindPlayerAction("QuickSave");
        quickLoadAction = FindPlayerAction("QuickLoad");
    }

    private void CacheCarActions()
    {
        carMovementAction = FindCarAction("Movement");
        carNitroAction = FindCarAction("Nitro");
        carHandbrakeAction = FindCarAction("Handbrake");
    }

    private InputAction FindPlayerAction(string actionName)
    {
        return playerMap?.FindAction(actionName, throwIfNotFound: false);
    }

    private InputAction FindCarAction(string actionName)
    {
        return carMap?.FindAction(actionName, throwIfNotFound: false);
    }

    // =====================================================
    // PLAYER MOVEMENT / LOOK
    // =====================================================

    public Vector2 Move =>
        GameplayInputBlocked
            ? Vector2.zero
            : ReadVector2(moveAction);

    public Vector2 LookDelta =>
        GameplayInputBlocked
            ? Vector2.zero
            : ReadVector2(lookAction) * lookSensitivity;

    public bool IsSprinting =>
        !GameplayInputBlocked &&
        IsPressed(sprintAction);

    public bool IsCrouching =>
        !GameplayInputBlocked &&
        IsPressed(crouchAction);

    public bool JumpPressed =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(jumpAction);

    public bool JumpHeld =>
        !GameplayInputBlocked &&
        IsPressed(jumpAction);

    // =====================================================
    // COMBAT
    // =====================================================

    public bool FirePressed =>
        !GameplayInputBlocked &&
        IsPressed(fireAction);

    public bool FirePressedThisFrame =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(fireAction);

    public bool FireReleasedThisFrame =>
        !GameplayInputBlocked &&
        WasReleasedThisFrame(fireAction);

    public bool FireHeld =>
        !GameplayInputBlocked &&
        ReadFloat(fireAction) > 0.1f;

    public bool FireAltPressed =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(fireAltAction);

    public bool FireAltHeld =>
        !GameplayInputBlocked &&
        ReadFloat(fireAltAction) > 0.1f;

    public bool ReloadPressed =>
        !GameplayInputBlocked &&
        IsPressed(reloadAction);

    public bool ReloadPressedThisFrame =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(reloadAction);

    public bool DropWeaponPressed =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(dropWeaponAction);

    public bool DropWeaponPressedThisFrame =>
        DropWeaponPressed;

    public bool PrevWeaponPressedThisFrame =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(prevWeaponAction);

    // =====================================================
    // INTERACTION / UI TOGGLES
    // =====================================================

    public bool InteractPressedThisFrame =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(interactAction);

    public bool InteractPressed =>
        InteractPressedThisFrame;

    public bool InteractHeld =>
        !GameplayInputBlocked &&
        IsPressed(interactAction);

    public bool InventoryPressed =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(inventoryAction);

    public bool ToggleConsolePressed =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(toggleConsoleAction);

    public bool MapTogglePressedThisFrame =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(mapToggleAction);

    public bool ObjectivesPressedThisFrame =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(objectivesAction);

    public bool InteractUiPressedThisFrame =>
        WasPressedThisFrame(interactAction);

    public bool MapToggleRawPressedThisFrame =>
        WasPressedThisFrame(mapToggleAction);

    public bool ObjectivesRawPressedThisFrame =>
        WasPressedThisFrame(objectivesAction);


    public bool SwitchCameraPressedThisFrame =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(switchCameraAction);

    // =====================================================
    // SYSTEM
    // =====================================================

    public bool QuickSavePressedThisFrame =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(quickSaveAction);

    public bool QuickLoadPressedThisFrame =>
        !GameplayInputBlocked &&
        WasPressedThisFrame(quickLoadAction);

    // =====================================================
    // CAR INPUT
    // =====================================================

    public Vector2 CarMovement =>
        GameplayInputBlocked
            ? Vector2.zero
            : ReadVector2(carMovementAction);

    public bool CarNitroHeld =>
        !GameplayInputBlocked &&
        IsPressed(carNitroAction);

    public bool CarHandbrakeHeld =>
        !GameplayInputBlocked &&
        IsPressed(carHandbrakeAction);

    // =====================================================
    // PUBLIC HELPERS FOR REBINDING / UI HINTS
    // =====================================================

    public InputAction GetPlayerAction(string actionName)
    {
        return FindPlayerAction(actionName);
    }

    public InputAction GetCarAction(string actionName)
    {
        return FindCarAction(actionName);
    }

    public string GetPlayerBindingDisplay(string actionName, int bindingIndex = -1)
    {
        var action = GetPlayerAction(actionName);
        if (action == null)
            return "";

        if (bindingIndex >= 0 && bindingIndex < action.bindings.Count)
            return action.GetBindingDisplayString(bindingIndex);

        return action.GetBindingDisplayString();
    }

    public string GetCarBindingDisplay(string actionName, int bindingIndex = -1)
    {
        var action = GetCarAction(actionName);
        if (action == null)
            return "";

        if (bindingIndex >= 0 && bindingIndex < action.bindings.Count)
            return action.GetBindingDisplayString(bindingIndex);

        return action.GetBindingDisplayString();
    }

    public void SaveBindingOverrides()
    {
        if (inputActions == null)
            return;

        string json = inputActions.asset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("INPUT_BINDINGS", json);
        PlayerPrefs.Save();
    }

    public void LoadBindingOverrides()
    {
        if (inputActions == null)
            return;

        string json = PlayerPrefs.GetString("INPUT_BINDINGS", "");

        if (!string.IsNullOrWhiteSpace(json))
            inputActions.asset.LoadBindingOverridesFromJson(json);
    }

    public void ResetBindingOverrides()
    {
        if (inputActions == null)
            return;

        inputActions.asset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey("INPUT_BINDINGS");
        PlayerPrefs.Save();
    }

    public void StartPlayerRebind(string actionName, int bindingIndex, System.Action onComplete = null)
    {
        var action = GetPlayerAction(actionName);
        StartRebind(action, bindingIndex, onComplete);
    }

    public void StartCarRebind(string actionName, int bindingIndex, System.Action onComplete = null)
    {
        var action = GetCarAction(actionName);
        StartRebind(action, bindingIndex, onComplete);
    }

    private void StartRebind(InputAction action, int bindingIndex, System.Action onComplete)
    {
        if (action == null)
            return;

        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            return;

        action.Disable();

        action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .OnComplete(operation =>
            {
                operation.Dispose();
                action.Enable();

                SaveBindingOverrides();
                onComplete?.Invoke();
            })
            .OnCancel(operation =>
            {
                operation.Dispose();
                action.Enable();

                onComplete?.Invoke();
            })
            .Start();
    }

    // =====================================================
    // BLOCKING
    // =====================================================

    public static void SetGameplayBlocked(bool blocked)
    {
        GameplayInputBlocked = blocked;
    }

    // =====================================================
    // SAFE READ HELPERS
    // =====================================================

    private static bool WasPressedThisFrame(InputAction action)
    {
        return action != null && action.WasPressedThisFrame();
    }

    private static bool WasReleasedThisFrame(InputAction action)
    {
        return action != null && action.WasReleasedThisFrame();
    }

    private static bool IsPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }

    private static float ReadFloat(InputAction action)
    {
        return action != null ? action.ReadValue<float>() : 0f;
    }

    private static Vector2 ReadVector2(InputAction action)
    {
        return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
    }
}