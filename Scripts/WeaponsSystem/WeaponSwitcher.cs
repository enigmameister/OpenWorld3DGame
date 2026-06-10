using System.Collections.Generic;
using UnityEngine;

public class WeaponSwitcher : MonoBehaviour
{
    private const int HANDS_INDEX = -1;

    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private WeaponInventorySlots slots;
    [SerializeField] private WeaponViewModelController viewModels;
    [SerializeField] private WeaponHolsterController holsters;
    [SerializeField] private WeaponHUDNotifier hud;

    private int lastWeaponIndex = -1;
    private bool lastWasHands = false;

    void Awake()
    {
        if (!weaponManager) weaponManager = GetComponent<WeaponManager>();
        if (!slots) slots = GetComponent<WeaponInventorySlots>();
        if (!viewModels) viewModels = GetComponent<WeaponViewModelController>();
        if (!holsters) holsters = GetComponent<WeaponHolsterController>();
        if (!hud) hud = GetComponent<WeaponHUDNotifier>();
    }

    public void SelectWeapon(int index)
    {
        GameObject[] weaponSlots = slots.GetSlots();

        lastWasHands = weaponManager.IsUsingHandsOnly();
        lastWeaponIndex = weaponManager.GetCurrentWeaponIndex();

        if (index < 0 || index >= weaponSlots.Length || !slots.HasWeapon(index))
        {
            Debug.LogWarning($"❌ Nie można wybrać broni z index = {index}");
            return;
        }

        viewModels?.HideHands();

        int currentIndex = weaponManager.GetRawCurrentWeaponIndex();

        if (currentIndex != -1)
        {
            GameObject oldWeapon = slots.GetSlotObject(currentIndex);

            if (oldWeapon != null)
            {
                var oldGun = oldWeapon.GetComponentInChildren<Gun>(true);
                if (oldGun != null)
                    oldGun.ResetAimStateOnHolster();

                viewModels?.HideWeapon(oldWeapon);
            }
        }

        GameObject newWeapon = slots.GetSlotObject(index);
        viewModels?.ShowWeapon(newWeapon);

        weaponManager.SetCurrentWeaponIndex(index);

        holsters?.Refresh();
        hud?.Refresh();

        weaponManager.SetForceHandsManual(false);
    }

    public void ScrollWeapon(int direction)
    {
        var list = BuildCycle(includeHands: true);
        if (list.Count == 0) return;

        int cur = CurrentCyclePointer();
        int pos = list.IndexOf(cur);
        if (pos < 0) pos = 0;

        int next = (pos + direction + list.Count) % list.Count;
        int target = list[next];

        lastWasHands = weaponManager.IsUsingHandsOnly();
        lastWeaponIndex = weaponManager.GetCurrentWeaponIndex();

        if (target == HANDS_INDEX)
            weaponManager.ActivateHandsOnly();
        else
            SelectWeapon(target);
    }

    public void ToggleLastUsed()
    {
        bool prevWasHands = weaponManager.IsUsingHandsOnly();
        int prevIndex = weaponManager.GetCurrentWeaponIndex();

        if (lastWasHands)
        {
            if (!prevWasHands)
                weaponManager.ActivateHandsOnly();
        }
        else
        {
            if (lastWeaponIndex >= 0 &&
                lastWeaponIndex < slots.GetSlots().Length &&
                slots.HasWeapon(lastWeaponIndex) &&
                slots.GetSlotObject(lastWeaponIndex) != null)
            {
                if (prevIndex != lastWeaponIndex || prevWasHands)
                    SelectWeapon(lastWeaponIndex);
            }
            else
            {
                TrySwitchToAvailableWeapon();
            }
        }

        lastWasHands = prevWasHands;
        lastWeaponIndex = prevIndex;
    }

    public void TrySwitchToAvailableWeapon(int excludeIndex = -1)
    {
        if (InventoryUI.IsInventoryOpen || InventoryUI.IsDraggingInventoryItem)
            return;

        int bestSlot = -1;
        int bestPri = 0;

        GameObject[] weaponSlots = slots.GetSlots();

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (i == excludeIndex) continue;
            if (slots.GetSlotObject(i) == null || !slots.HasWeapon(i)) continue;

            int pri = GetPriorityForIndex(i);
            if (pri > bestPri)
            {
                bestPri = pri;
                bestSlot = i;
            }
        }

        if (bestSlot >= 0)
        {
            if (weaponManager.GetRawCurrentWeaponIndex() != bestSlot)
                SelectWeapon(bestSlot);
        }
        else
        {
            weaponManager.ActivateHandsOnly();
        }
    }

    public int FindBestAvailableWeaponIndex()
    {
        int best = -1;
        int bestPri = 0;

        GameObject[] weaponSlots = slots.GetSlots();

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (slots.GetSlotObject(i) == null || !slots.HasWeapon(i)) continue;

            int pri = GetPriorityForIndex(i);
            if (pri > bestPri)
            {
                bestPri = pri;
                best = i;
            }
        }

        return best;
    }

    public static int GetPriorityForIndex(int index)
    {
        return index switch
        {
            2 => 4, // Rifles
            1 => 3, // Pistols
            3 => 2, // Nades
            0 => 1, // Melee
            _ => 0
        };
    }

    private List<int> BuildCycle(bool includeHands = true)
    {
        var list = new List<int>(5);

        if (slots.HasWeapon(0) && slots.GetSlotObject(0)) list.Add(0);
        if (slots.HasWeapon(1) && slots.GetSlotObject(1)) list.Add(1);
        if (slots.HasWeapon(2) && slots.GetSlotObject(2)) list.Add(2);
        if (slots.HasWeapon(3) && slots.GetSlotObject(3)) list.Add(3);

        if (includeHands)
            list.Add(HANDS_INDEX);

        return list;
    }

    private int CurrentCyclePointer()
    {
        return weaponManager.IsUsingHandsOnly()
            ? HANDS_INDEX
            : weaponManager.GetCurrentWeaponIndex();
    }
}