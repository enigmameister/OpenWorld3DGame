using UnityEngine;

public class WeaponStateSnapshotController : MonoBehaviour
{
    [System.Serializable]
    public struct WeaponSlotSnapshot
    {
        public bool hasWeapon;
        public string weaponObjectName;

        public int currentAmmo;
        public int totalAmmo;

        public int grenadeCount;
    }

    [System.Serializable]
    public struct WeaponSnapshot
    {
        public int activeSlotIndex;
        public WeaponSlotSnapshot[] slots;
    }

    private WeaponManager weaponManager;
    private WeaponInventorySlots slots;
    private WeaponSwitcher switcher;

    void Awake()
    {
        weaponManager = GetComponent<WeaponManager>();
        slots = GetComponent<WeaponInventorySlots>();
        switcher = GetComponent<WeaponSwitcher>();
    }

    public WeaponSnapshot GetSnapshot()
    {
        WeaponSnapshot snap = new WeaponSnapshot();

        snap.activeSlotIndex =
            weaponManager.GetCurrentWeaponIndex();

        GameObject[] weaponSlots = slots.GetSlots();

        int len = weaponSlots != null
            ? weaponSlots.Length
            : 0;

        snap.slots = new WeaponSlotSnapshot[len];

        for (int i = 0; i < len; i++)
        {
            WeaponSlotSnapshot s =
                new WeaponSlotSnapshot();

            s.hasWeapon =
                weaponSlots[i] != null
                && slots.HasWeapon(i);

            s.weaponObjectName =
                weaponSlots[i]
                ? weaponSlots[i].name
                : "";

            if (s.hasWeapon)
            {
                var gun =
                    weaponSlots[i]
                    .GetComponentInChildren<Gun>(true);

                var grenade =
                    weaponSlots[i]
                    .GetComponentInChildren<Grenade>(true);

                if (gun != null)
                {
                    s.currentAmmo =
                        gun.GetCurrentAmmo();

                    s.totalAmmo =
                        gun.GetTotalAmmo();
                }
                else if (grenade != null)
                {
                    var inst = grenade.GetInstance();

                    s.grenadeCount =
                        inst != null
                        ? inst.count
                        : 0;
                }
            }

            snap.slots[i] = s;
        }

        return snap;
    }

    public void ApplySnapshot(WeaponSnapshot snap)
    {
        GameObject[] weaponSlots =
            slots.GetSlots();

        if (weaponSlots == null)
            return;

        int len = Mathf.Min(
            snap.slots != null
                ? snap.slots.Length
                : 0,
            weaponSlots.Length
        );

        for (int i = 0; i < len; i++)
        {
            var s = snap.slots[i];

            if (!s.hasWeapon)
            {
                weaponManager.ClearSlot(i);
                continue;
            }

            if (weaponSlots[i] == null)
                continue;

            slots.SetHasWeapon(i, true);

            var gun =
                weaponSlots[i]
                .GetComponentInChildren<Gun>(true);

            var grenade =
                weaponSlots[i]
                .GetComponentInChildren<Grenade>(true);

            if (gun != null)
            {
                gun.SetAmmo(
                    s.currentAmmo,
                    s.totalAmmo
                );
            }
            else if (grenade != null)
            {
                var inst = grenade.GetInstance();

                if (inst != null)
                {
                    inst.count = s.grenadeCount;

                    var inv =
                        FindFirstObjectByType<InventoryUI>();

                    if (inv != null)
                        inv.RefreshCountDisplay(inst);
                }
            }
        }

        if (snap.activeSlotIndex < 0)
        {
            weaponManager.ActivateHandsOnly();
        }
        else if (
            snap.activeSlotIndex >= 0 &&
            snap.activeSlotIndex < weaponSlots.Length)
        {
            if (
                weaponSlots[snap.activeSlotIndex] != null &&
                slots.HasWeapon(snap.activeSlotIndex))
            {
                switcher.SelectWeapon(
                    snap.activeSlotIndex);
            }
            else
            {
                weaponManager.ActivateHandsOnly();
            }
        }
        else
        {
            weaponManager.ActivateHandsOnly();
        }
    }
}