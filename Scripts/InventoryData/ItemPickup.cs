using UnityEngine;

public interface IItemPickupMeta
{
    // Wstrzyknij meta do instancji (gdy podnosisz z ziemi do Inventory)
    void WriteToInstance(InventoryItemInstance inst);

    // Wczytaj meta z instancji (gdy wyrzucasz z Inventory na ziemiê)
    void ReadFromInstance(InventoryItemInstance inst);
}

public class ItemPickup : MonoBehaviour, IPressable
{
    [Header("Item")]
    [SerializeField] private InventoryItemData itemData;

    [Header("Trigger dziecka (auto-pickup)")]
    [SerializeField] private Collider childTrigger;
    [SerializeField] private bool pickupOnTriggerEnter = true;

    [Header("Destroy")]
    [SerializeField] private bool hideOnPickup = true;
    [SerializeField] private float destroyDelay = 0.05f;

    private bool _picked;
    private float _blockUntil;

    private IItemPickupMeta[] _meta;

    public string Label => itemData != null ? itemData.itemName : "Item";

    void Reset()
    {
        if (!childTrigger)
        {
            var cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (c != null && c.isTrigger) { childTrigger = c; break; }
            }
        }
    }

    void Awake()
    {
        _meta = GetComponents<IItemPickupMeta>();
    }

    public void InitializeFromInstance(InventoryItemInstance source)
    {
        if (source == null) return;
        if (source.data != null) itemData = source.data;

        if (_meta == null) _meta = GetComponents<IItemPickupMeta>();
        foreach (var m in _meta) m?.ReadFromInstance(source);
    }

    public void IgnorePickupFor(float seconds)
    {
        _blockUntil = Time.time + seconds;
    }

    public void Press()
    {
        TryPickup();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!pickupOnTriggerEnter) return;
        if (Time.time < _blockUntil) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerStats>() != null)
            TryPickup();
    }

    private void TryPickup()
    {
        if (_picked) return;
        if (Time.time < _blockUntil) return;

        if (itemData == null)
        {
            Debug.LogWarning("ItemPickup: brak itemData.");
            return;
        }

        var inv = InventoryUI.Instance;
        if (inv == null)
        {
            Debug.LogWarning("ItemPickup: InventoryUI.Instance == null.");
            return;
        }

        var inst = new InventoryItemInstance(itemData) { count = 1 };

        if (_meta == null) _meta = GetComponents<IItemPickupMeta>();
        foreach (var m in _meta) m?.WriteToInstance(inst);

        if (!inv.TryAddItem(inst)) return; // brak miejsca

        _picked = true;

        if (hideOnPickup)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        Destroy(gameObject, destroyDelay);
    }
}
