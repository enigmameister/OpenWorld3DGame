using UnityEngine;

public class WeaponInventorySlots : MonoBehaviour
{
    [Header("Slots")]
    public GameObject[] weaponSlots = new GameObject[4];
    public bool[] hasWeapon = new bool[4];

    void Awake()
    {
        if (weaponSlots == null || weaponSlots.Length == 0)
            weaponSlots = new GameObject[4];

        if (hasWeapon == null || hasWeapon.Length != weaponSlots.Length)
            hasWeapon = new bool[weaponSlots.Length];
    }

    public bool HasWeapon(int index)
    {
        return index >= 0 && index < hasWeapon.Length && hasWeapon[index];
    }

    public GameObject GetSlotObject(int index)
    {
        if (index < 0 || index >= weaponSlots.Length) return null;
        return weaponSlots[index];
    }

    public GameObject[] GetSlots()
    {
        return weaponSlots;
    }

    public InventoryItemInstance GetInstance(int index)
    {
        GameObject weapon = GetSlotObject(index);
        if (weapon == null) return null;

        return weapon.GetComponentInChildren<Gun>(true)?.inventoryInstance
            ?? weapon.GetComponentInChildren<Grenade>(true)?.inventoryInstance
            ?? weapon.GetComponentInChildren<Melee>(true)?.inventoryInstance;
    }

    public void SetSlot(int index, GameObject weapon)
    {
        if (index < 0 || index >= weaponSlots.Length) return;

        weaponSlots[index] = weapon;
        hasWeapon[index] = weapon != null;
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= weaponSlots.Length) return;

        if (weaponSlots[index] != null)
            weaponSlots[index].SetActive(false);

        weaponSlots[index] = null;
        hasWeapon[index] = false;
    }

    public int FindSlotIndexForInstance(InventoryItemInstance instance)
    {
        if (instance == null) return -1;

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            GameObject weapon = weaponSlots[i];
            if (weapon == null) continue;

            var slotInstance =
                weapon.GetComponentInChildren<Gun>(true)?.inventoryInstance ??
                weapon.GetComponentInChildren<Grenade>(true)?.inventoryInstance ??
                weapon.GetComponentInChildren<Melee>(true)?.inventoryInstance;

            if (slotInstance == instance)
                return i;
        }

        return -1;
    }

    public void SetHasWeapon(int index, bool value)
    {
        if (index < 0 || index >= hasWeapon.Length)
            return;

        hasWeapon[index] = value;
    }

    public void SetSlotObject(int index, GameObject obj)
    {
        if (index < 0 || index >= weaponSlots.Length)
            return;

        weaponSlots[index] = obj;
    }
}