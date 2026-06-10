using UnityEngine;
using static MountedSniperStation;

public class WeaponInputController : MonoBehaviour
{
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Cooldowns")]
    [SerializeField] private float scrollCooldown = 0.25f;
    [SerializeField] private float hotkeyCooldown = 0.12f;
    [SerializeField] private float dropCooldownTime = 0.25f;

    private float nextSwitchTime;
    private float dropCooldown;
    private bool isDropping;

    private PlayerStats playerStats;
    private ATMUIController atmUI;

    void Awake()
    {
        if (!weaponManager) weaponManager = GetComponent<WeaponManager>();
        if (!playerMovement) playerMovement = FindFirstObjectByType<PlayerMovement>();

        playerStats = GetComponentInParent<PlayerStats>();
        atmUI = FindFirstObjectByType<ATMUIController>();
    }

    void Update()
    {
        if (MountedSniperState.IsActive)
            return;

        if (!weaponManager) return;
        if (ShouldBlockInput()) return;

        weaponManager.TryAutoReturnFromHands();

        if (playerMovement && playerMovement.IsAdjustingCarryPublic)
            return;

        HandleHotkeys();
        HandleScroll();
        HandleDrop();
        HandleMeleeInput();

        weaponManager.CheckAutoSwitchWeaponIfNeeded();
    }

    private bool ShouldBlockInput()
    {
        if (InventoryUI.IsInventoryOpen) return true;
        if (InventoryUI.IsDraggingInventoryItem) return true;
        if (DevConsole.IsOpen) return true;

        if (playerStats != null && playerStats.IsDead) return true;
        if (atmUI != null && atmUI.IsOpen) return true;

        return false;
    }

    private void HandleHotkeys()
    {
        if (Time.time < nextSwitchTime) return;

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            weaponManager.ActivateHandsOnly();
            nextSwitchTime = Time.time + hotkeyCooldown;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            weaponManager.SelectWeapon(0);
            nextSwitchTime = Time.time + hotkeyCooldown;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            weaponManager.SelectWeapon(1);
            nextSwitchTime = Time.time + hotkeyCooldown;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            weaponManager.SelectWeapon(2);
            nextSwitchTime = Time.time + hotkeyCooldown;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            weaponManager.SelectWeapon(3);
            nextSwitchTime = Time.time + hotkeyCooldown;
            return;
        }

        if (PlayerInputHandler.Instance?.PrevWeaponPressedThisFrame ?? false)
        {
            weaponManager.ToggleLastUsedWeapon();
            nextSwitchTime = Time.time + hotkeyCooldown;
        }
    }

    private void HandleScroll()
    {
        if (MountedSniperState.IsActive) return;

        if (Time.time < nextSwitchTime) return;
        if (Input.GetMouseButton(2)) return;
        if (weaponManager.IsCurrentScopeActive()) return;

        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            weaponManager.ScrollWeaponInput(scroll > 0f ? +1 : -1);
            nextSwitchTime = Time.time + scrollCooldown;
        }
    }

    private void HandleDrop()
    {
        if (MountedSniperState.IsActive) return;

        dropCooldown -= Time.deltaTime;

        bool dropPressed =
            (PlayerInputHandler.Instance?.DropWeaponPressedThisFrame ?? false)
            || Input.GetKeyDown(KeyCode.G);

        if (dropPressed && dropCooldown <= 0f && !isDropping)
        {
            isDropping = true;
            weaponManager.DropCurrentWeapon();
            dropCooldown = dropCooldownTime;
            isDropping = false;
        }
    }

    private void HandleMeleeInput()
    {
        weaponManager.HandleMeleeInput();
    }
}