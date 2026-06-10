using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldBoxInventory : MonoBehaviour, IPressable
{
    [System.Serializable]
    public class StartItem
    {
        public InventoryItemData data;
        public int count = 1;
        public int currentAmmo = -1;
        public int totalAmmo = -1;
    }

    [Header("Box")]
    public string boxName = "BOX";
    public int totalSlots = 15;
    public int slotsPerRow = 5;

    [Header("Cash")]
    [Min(0)] public int cash = 0;

    [Header("Start items")]
    public List<StartItem> startItems = new();

    private readonly List<InventoryItemInstance> items = new();
    private bool initialized;

    public string Label => boxName;

    private void Awake()
    {
        InitOnce();
    }

    private void InitOnce()
    {
        if (initialized) return;
        initialized = true;

        items.Clear();

        foreach (var s in startItems)
        {
            if (s == null || s.data == null) continue;

            int amount = Mathf.Max(1, s.count);

            for (int i = 0; i < amount; i++)
            {
                var inst = new InventoryItemInstance(s.data, s.currentAmmo, s.totalAmmo);
                items.Add(inst);
            }
        }
    }

    public List<InventoryItemInstance> GetItems()
    {
        InitOnce();
        return items;
    }

    public void Press()
    {
        InitOnce();

        if (BoxInventoryUI.Instance == null)
        {
            Debug.LogWarning("[WorldBoxInventory] Brak BoxInventoryUI na scenie.");
            return;
        }

        BoxInventoryUI.Instance.Open(this);
    }
}