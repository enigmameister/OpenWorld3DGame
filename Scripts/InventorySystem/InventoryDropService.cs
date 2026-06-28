using System;
using UnityEngine;

public class InventoryDropService
{
    private readonly MonoBehaviour owner;
    private readonly LayerMask dropObstacleMask;
    private readonly Func<PlayerStats> getPlayerStats;

    private Transform cachedPlayerTransform;

    public InventoryDropService(
        MonoBehaviour owner,
        LayerMask dropObstacleMask,
        Func<PlayerStats> getPlayerStats)
    {
        this.owner = owner;
        this.dropObstacleMask = dropObstacleMask;
        this.getPlayerStats = getPlayerStats;
    }

    private PlayerStats GetPlayerStats()
    {
        PlayerStats stats = getPlayerStats?.Invoke();

        if (stats != null && cachedPlayerTransform == null)
            cachedPlayerTransform = stats.transform;

        return stats;
    }

    private Transform GetPlayerTransform()
    {
        PlayerStats stats = GetPlayerStats();

        if (cachedPlayerTransform == null && stats != null)
            cachedPlayerTransform = stats.transform;

        return cachedPlayerTransform;
    }

    public void SpawnDraggedPickupOnly(InventoryItemInstance instance)
    {
        if (instance == null || instance.data == null)
            return;

        if (instance.data is BankCardItemData)
        {
            SpawnBankCard(instance);
            return;
        }

        int cur = instance.currentAmmo;
        int tot = instance.totalAmmo;

        if (instance.data is AmmoItemData)
        {
            int mag = Mathf.Max(0, instance.totalAmmo);
            cur = tot = mag;
        }

        SpawnPickup(instance, cur, tot);
    }

    public void SpawnPickup(InventoryItemData data, int currentAmmo = -1, int totalAmmo = -1)
    {
        if (data == null)
            return;

        GameObject pickupPrefab =
            data.prefab != null
                ? data.prefab
                : Resources.Load<GameObject>($"Pickups/{data.itemName}") ??
                  Resources.Load<GameObject>($"Pickups/{data.name}");

        if (pickupPrefab == null)
            return;

        PlayerStats player = GetPlayerStats();
        Transform t = GetPlayerTransform();

        Vector3 pos;
        Quaternion rot;
        GetDropPose(t, out pos, out rot);

        GameObject dropped = UnityEngine.Object.Instantiate(pickupPrefab, pos, rot);
        GameObjectUtil.CopyTagAndLayer(pickupPrefab, dropped);

        InventoryItemInstance inst;

        ItemPickup cardPickup = dropped.GetComponentInChildren<ItemPickup>(true);
        if (cardPickup != null && data is BankCardItemData)
        {
            return;
        }

        WeaponPickup pickup = dropped.GetComponentInChildren<WeaponPickup>(true);
        if (pickup == null)
            return;

        if (data is AmmoItemData ammoData)
        {
            pickup.ammoOnly = true;
            pickup.itemData = ammoData.weapon;
            pickup.ammoInventoryData = ammoData;

            int magAmount = totalAmmo >= 0
                ? totalAmmo
                : currentAmmo >= 0
                    ? currentAmmo
                    : ammoData.amountPerUnit;

            inst = new InventoryItemInstance(ammoData, magAmount, magAmount);
        }
        else
        {
            inst = new InventoryItemInstance(data, currentAmmo, totalAmmo);
        }

        pickup.Initialize(inst, player != null ? player.gameObject : null);

        if (inst.data is AmmoItemData && inst.totalAmmo <= 0 && inst.currentAmmo <= 0)
            pickup.nonInteractable = true;

        if (data is AmmoItemData)
        {
            pickup.totalAmmo = inst.totalAmmo;
            pickup.currentAmmo = inst.currentAmmo;
        }

        ApplyPickupPhysics(dropped, pickup, player, t);
    }

    public void SpawnPickup(InventoryItemInstance source, int currentAmmo = -1, int totalAmmo = -1)
    {
        if (source == null || source.data == null)
            return;

        InventoryItemData data = source.data;

        GameObject pickupPrefab =
            data.prefab != null
                ? data.prefab
                : Resources.Load<GameObject>($"Pickups/{data.itemName}") ??
                  Resources.Load<GameObject>($"Pickups/{data.name}");

        if (pickupPrefab == null)
            return;

        PlayerStats player = GetPlayerStats();
        Transform t = GetPlayerTransform();

        Vector3 pos;
        Quaternion rot;
        GetDropPose(t, out pos, out rot);

        GameObject dropped = UnityEngine.Object.Instantiate(pickupPrefab, pos, rot);
        GameObjectUtil.CopyTagAndLayer(pickupPrefab, dropped);

        ItemPickup itemPickup = dropped.GetComponentInChildren<ItemPickup>(true);
        if (itemPickup != null)
        {
            itemPickup.InitializeFromInstance(source);
            itemPickup.IgnorePickupFor(0.6f);
        }

        WeaponPickup weaponPickup = dropped.GetComponentInChildren<WeaponPickup>(true);
        if (weaponPickup != null)
        {
            InventoryItemInstance inst;

            if (data is AmmoItemData ammoData)
            {
                weaponPickup.ammoOnly = true;
                weaponPickup.itemData = ammoData.weapon;
                weaponPickup.ammoInventoryData = ammoData;

                int magAmount = totalAmmo >= 0
                    ? totalAmmo
                    : currentAmmo >= 0
                        ? currentAmmo
                        : ammoData.amountPerUnit;

                inst = new InventoryItemInstance(ammoData, magAmount, magAmount);

                weaponPickup.totalAmmo = inst.totalAmmo;
                weaponPickup.currentAmmo = inst.currentAmmo;
            }
            else
            {
                inst = new InventoryItemInstance(data, currentAmmo, totalAmmo);
            }

            weaponPickup.Initialize(inst, player != null ? player.gameObject : null);

            if (inst.data is AmmoItemData && inst.totalAmmo <= 0 && inst.currentAmmo <= 0)
                weaponPickup.nonInteractable = true;

            ApplyPickupPhysics(dropped, weaponPickup, player, t);
            return;
        }

        ApplyBasicPhysicsImpulse(dropped, t);
    }

    public void SpawnBankCard(InventoryItemInstance instance)
    {
        if (instance == null || instance.data == null)
            return;

        BankCardItemData data = instance.data as BankCardItemData;

        if (data == null || data.prefab == null)
            return;

        PlayerStats player = GetPlayerStats();
        Transform t = GetPlayerTransform();

        Vector3 pos;
        Quaternion rot;
        GetDropPose(t, out pos, out rot);

        GameObject dropped = UnityEngine.Object.Instantiate(data.prefab, pos, rot);
        GameObjectUtil.CopyTagAndLayer(data.prefab, dropped);

        ItemPickup pickup = dropped.GetComponentInChildren<ItemPickup>(true);
        if (pickup != null)
        {
            pickup.InitializeFromInstance(instance);
            pickup.IgnorePickupFor(0.6f);
        }

        ApplyBasicPhysicsImpulse(dropped, t, 2.0f, 1.0f);
    }

    private void GetDropPose(Transform playerTransform, out Vector3 pos, out Quaternion rot)
    {
        Vector3 originPos = (playerTransform != null ? playerTransform.position : Vector3.zero) + Vector3.up * 1.0f;
        Vector3 fwd = playerTransform != null ? playerTransform.forward : Vector3.forward;

        float sphereRadius = 0.25f;
        float castDistance = 0.8f;

        Vector3 dropPos = originPos + fwd * 0.6f;

        if (playerTransform != null)
        {
            int mask = dropObstacleMask.value != 0 ? dropObstacleMask.value : ~0;

            if (Physics.SphereCast(
                    originPos,
                    sphereRadius,
                    fwd,
                    out RaycastHit hit,
                    castDistance,
                    mask,
                    QueryTriggerInteraction.Ignore))
            {
                dropPos = hit.point - fwd * (sphereRadius + 0.05f);
                dropPos.y = originPos.y;
            }
        }

        pos = dropPos + Vector3.up * 0.4f;

        if (Physics.Raycast(pos, Vector3.down, out RaycastHit ground, 3f, ~0, QueryTriggerInteraction.Ignore))
            pos = ground.point + ground.normal * 0.05f;

        rot = playerTransform != null
            ? Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, Vector3.up), Vector3.up)
            : Quaternion.identity;
    }

    private void ApplyPickupPhysics(
       GameObject dropped,
       WeaponPickup pickup,
       PlayerStats player,
       Transform playerTransform)
    {
        if (dropped == null || pickup == null)
            return;

        pickup.SetupPhysics(isPickupFromScene: true);
        pickup.IgnoreAutoPickupFrom(player != null ? player.gameObject : null, 0.6f);

        bool softDrop =
            pickup.itemData is GrenadeItemData ||
            pickup.itemData is AmmoItemData ||
            (pickup.itemData?.prefab != null && pickup.itemData.prefab.name.Contains("Grenade")) ||
            pickup.count > 1;

        if (softDrop)
        {
            ApplySoftDropPhysics(dropped, playerTransform);
            return;
        }

        ApplyBasicPhysicsImpulse(dropped, playerTransform, 2.5f, 1.5f);
    }

    private void ApplySoftDropPhysics(GameObject dropped, Transform playerTransform)
    {
        if (dropped == null)
            return;

        Rigidbody rb = dropped.GetComponentInChildren<Rigidbody>() ?? dropped.GetComponent<Rigidbody>();

        if (rb == null)
            return;

        Vector3 fwd = playerTransform != null ? playerTransform.forward : Vector3.forward;

        rb.position += Vector3.up * 0.03f;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Ma³y pchniêcie do przodu, bez krêcenia jak pi³ka.
        rb.AddForce(fwd * 0.45f + Vector3.up * 0.15f, ForceMode.Impulse);

        // Mocne t³umienie toczenia.
        rb.linearDamping = Mathf.Max(rb.linearDamping, 2.5f);
        rb.angularDamping = Mathf.Max(rb.angularDamping, 8f);
    }

    private void ApplyBasicPhysicsImpulse(
        GameObject dropped,
        Transform playerTransform,
        float forwardForce = 2.5f,
        float torqueForce = 1.5f)
    {
        if (dropped == null)
            return;

        Rigidbody rb = dropped.GetComponentInChildren<Rigidbody>() ?? dropped.GetComponent<Rigidbody>();

        if (rb == null || playerTransform == null)
            return;

        Vector3 fwd = playerTransform.forward;

        rb.position += Vector3.up * 0.05f;
        rb.AddForce(fwd * forwardForce + Vector3.up * 1.0f, ForceMode.Impulse);
        rb.AddTorque(UnityEngine.Random.insideUnitSphere * torqueForce, ForceMode.Impulse);
    }
}