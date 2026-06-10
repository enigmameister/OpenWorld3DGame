using System.Linq;
using UnityEngine;

public class WeaponDropController : MonoBehaviour
{
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private WeaponInventorySlots slots;
    [SerializeField] private WeaponHolsterController holsters;
    [SerializeField] private WeaponHUDNotifier hud;

    [SerializeField] private InventoryUI inventoryUI;

    [Header("Drop")]
    [SerializeField] private GameObject[] allPickupPrefabs;
    [SerializeField] private LayerMask dropObstacleMask;

    void Awake()
    {
        if (!weaponManager) weaponManager = GetComponent<WeaponManager>();
        if (!slots) slots = GetComponent<WeaponInventorySlots>();
        if (!holsters) holsters = GetComponent<WeaponHolsterController>();
        if (!hud) hud = GetComponent<WeaponHUDNotifier>();

        if (!inventoryUI) inventoryUI = weaponManager ? weaponManager.inventoryUI : null;
        if (!inventoryUI) inventoryUI = FindFirstObjectByType<InventoryUI>();
    }

    public void DropCurrentWeapon()
    {
        int currentIndex = weaponManager.GetRawCurrentWeaponIndex();

        if (currentIndex < 0 || currentIndex >= slots.GetSlots().Length) return;
        if (!slots.HasWeapon(currentIndex)) return;

        var inst = slots.GetInstance(currentIndex);
        bool isGrenade = inst?.data is GrenadeItemData;

        if (isGrenade)
        {
            var one = new InventoryItemInstance(inst.data) { count = 1 };
            DropWeapon(currentIndex, one);
            return;
        }

        DropWeapon(currentIndex, null);
    }

    public void DropWeapon(int index)
    {
        DropWeapon(index, null);
    }

    public void DropWeapon(int index, InventoryItemInstance dropOverride)
    {
        GameObject weapon = slots.GetSlotObject(index);

        if (weapon == null)
        {
            Debug.LogWarning($"[DropWeapon] ❌ Slot {index} is null");
            return;
        }

        var gun = weapon.GetComponentInChildren<Gun>(true);
        var grenade = weapon.GetComponentInChildren<Grenade>(true);
        var melee = weapon.GetComponentInChildren<Melee>(true);

        var canonicalInst =
              gun?.inventoryInstance
           ?? grenade?.inventoryInstance
           ?? melee?.inventoryInstance;

        if (canonicalInst == null)
        {
            Debug.LogWarning("[DropWeapon] ❌ Brak kanonicznej instancji.");
            return;
        }

        var pickupInst = dropOverride ?? canonicalInst;

        GameObject prefab = pickupInst.data.prefab
            ?? allPickupPrefabs.FirstOrDefault(p => p.name == pickupInst.data.itemName);

        if (prefab == null)
        {
            Debug.LogWarning("❌ DropWeapon – brak dropPrefab");
            return;
        }

        var toSpawn = new InventoryItemInstance(pickupInst.data)
        {
            count = 1,
            currentAmmo = pickupInst.currentAmmo,
            totalAmmo = pickupInst.totalAmmo
        };

        SpawnDroppedPickup(prefab, toSpawn, weapon);

        if (inventoryUI != null)
        {
            inventoryUI.RemoveItem(canonicalInst, 1);

            if (grenade != null)
            {
                if (canonicalInst.count > 0)
                {
                    grenade.SetInventoryInstance(canonicalInst);
                    inventoryUI.RefreshCountDisplay(canonicalInst);
                }
            }
        }

        if (canonicalInst.count <= 0)
        {
            if (grenade != null)
            {
                weapon.SetActive(false);

                slots.SetSlotObject(index, null);
                slots.SetHasWeapon(index, false);

                if (weaponManager.GetRawCurrentWeaponIndex() == index)
                {
                    weaponManager.SetCurrentWeaponIndex(-1);
                    weaponManager.TrySwitchToAvailableWeapon();
                }

                holsters?.Refresh();
                hud?.Refresh();
                return;
            }

            weapon.SetActive(false);

            slots.SetSlotObject(index, null);
            slots.SetHasWeapon(index, false);

            holsters?.Refresh();

            if (weaponManager.GetRawCurrentWeaponIndex() == index)
            {
                weaponManager.SetCurrentWeaponIndex(-1);
                weaponManager.ActivateHandsOnly();
                weaponManager.TrySwitchToAvailableWeapon();
            }
        }

        hud?.Refresh();
    }

    private GameObject SpawnDroppedPickup(GameObject prefab, InventoryItemInstance instance, GameObject weapon)
    {
        Transform origin = transform;

        Vector3 originPos = origin.position + Vector3.up * 0.9f;
        Vector3 fwd = origin.forward;

        float sphereRadius = 0.25f;
        float castDistance = 1.4f;

        Vector3 dropPos;

        if (Physics.SphereCast(
            originPos,
            sphereRadius,
            fwd,
            out RaycastHit hit,
            castDistance,
            dropObstacleMask,
            QueryTriggerInteraction.Ignore))
        {
            dropPos = hit.point - fwd * (sphereRadius + 0.05f);
            dropPos.y = originPos.y;
        }
        else
        {
            dropPos = originPos + fwd * 0.5f;
        }

        Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

        GameObject pickup = Instantiate(prefab, dropPos, rot);
        GameObjectUtil.CopyTagAndLayer(prefab, pickup);

        if (pickup.layer == 0)
            pickup.layer = LayerMask.NameToLayer("Default");

        var pickupScript = pickup.GetComponentInChildren<WeaponPickup>(true);
        if (pickupScript == null)
        {
            Debug.LogError("❌ Pickup prefab NIE MA WeaponPickup.cs – nie wykonano Initialize!");
            return pickup;
        }

        pickupScript.Initialize(instance, weaponManager.gameObject);
        pickupScript.SetupPhysics(isPickupFromScene: true);

        var rb = pickup.GetComponentInChildren<Rigidbody>(true);
        var col = pickup.GetComponentInChildren<Collider>(true);

        if (rb == null) rb = pickup.AddComponent<Rigidbody>();

        if (col == null)
        {
            col = pickup.AddComponent<BoxCollider>();
            col.isTrigger = false;
        }

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.mass = 3f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.05f;

        Vector3 impulse = fwd * 4.0f + Vector3.up * 1.2f;
        impulse += Vector3.down * 0.2f;

        rb.AddForce(impulse, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.Impulse);

        var ownerCols = GetComponentsInParent<Collider>();
        foreach (var oc in ownerCols)
            if (oc && col) Physics.IgnoreCollision(col, oc, true);

        if (weapon != null)
        {
            foreach (var wc in weapon.GetComponentsInChildren<Collider>())
                if (wc && col) Physics.IgnoreCollision(col, wc, true);
        }

        if (instance.data is GrenadeItemData)
        {
            if (!pickup.TryGetComponent<AutoStopPhysics>(out _))
                pickup.AddComponent<AutoStopPhysics>();
        }

        return pickup;
    }
}