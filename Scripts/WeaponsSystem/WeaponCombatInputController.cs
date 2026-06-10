using UnityEngine;
using static MountedSniperStation;

public class WeaponCombatInputController : MonoBehaviour
{
    [SerializeField] private WeaponManager weaponManager;

    private GameObject cachedWeaponObject;
    private Gun cachedGun;

    void Awake()
    {
        if (!weaponManager)
            weaponManager = GetComponent<WeaponManager>();
    }

    void Update()
    {
        if (MountedSniperState.IsActive)
            return;

        if (!weaponManager) return;
        if (ShouldBlockInput()) return;
        if (weaponManager.IsUsingHandsOnly()) return;

        Gun gun = GetCachedCurrentGun();
        if (!gun) return;

        gun.SetExternalCombatInput(true);

        var input = PlayerInputHandler.Instance;
        if (!input) return;

        if (!MountedSniperState.IsActive)
            gun.CombatReloadInput(input.ReloadPressed);

        gun.CombatFireInput(input.FirePressed, input.FireHeld);
        gun.CombatReloadInput(input.ReloadPressed);
        gun.CombatADSInput(
            input.FireAltPressed,
            input.FireAltHeld,
            Input.mouseScrollDelta.y
        );
    }

    private Gun GetCachedCurrentGun()
    {
        GameObject current = weaponManager.GetCurrentWeaponSlotObject();

        if (!current)
        {
            cachedWeaponObject = null;
            cachedGun = null;
            return null;
        }

        if (current != cachedWeaponObject)
        {
            cachedWeaponObject = current;
            cachedGun = current.GetComponentInChildren<Gun>(true);
        }

        return cachedGun;
    }

    private bool ShouldBlockInput()
    {
        if (InventoryUI.IsInventoryOpen) return true;
        if (InventoryUI.IsDraggingInventoryItem) return true;
        if (DevConsole.IsOpen) return true;

        return false;
    }
}