using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public bool isOccupied = false;
    public InventoryItemInstance item;

    [Header("Slot visuals / data")]
    public Image iconImage;
    public GameObject fillImage;
    public GameObject borderImage;
    public TextMeshProUGUI countText; // stackable items

    public int slotIndex; // ustawiane przez InventoryUI
    public InventoryTooltip tooltip => InventoryTooltip.Instance;

    public UnityEngine.Object owner;

    void Awake()
    {
        if (countText == null)
            countText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (item == null || item.data == null) return;

        var tip = InventoryTooltip.Instance;
        if (tip == null) return;

        int slotSize = item.data.slotSize;
        tip.ShowForItem(item, slotSize, e.position);
    }

    public void OnPointerExit(PointerEventData e)
    {
        var tip = InventoryTooltip.Instance;
        if (tip == null) return;

        tip.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (owner is IInventorySlotOwner slotOwner)
        {
            slotOwner.OnSlotClicked(this);
            return;
        }

        var inventory = FindFirstObjectByType<InventoryUI>();
        if (inventory != null)
            inventory.OnSlotClicked(this);
    }

    public void SetItem(InventoryItemInstance instance)
    {
        isOccupied = true;
        item = instance;

        if (iconImage != null && item.data != null)
        {
            // ✅ sprite
            iconImage.sprite = item.data.icon;
            iconImage.enabled = (iconImage.sprite != null);

            // ✅ kolor (domyślnie biały)
            iconImage.color = Color.white;

            // ✅ tint tylko dla kart (źródło prawdy: BankSystem -> Variant DB)
            if (item.hasBankCardMeta)
            {
                if (BankSystem.Instance != null)
                    iconImage.color = BankSystem.Instance.GetVariantColor(item.bankCard.colorVariant);
                else
                    iconImage.color = Color.white; // fallback jak brak BankSystem (np. test scena)
            }

        }

        // Skala X dla slotów 2x/3x
        int size = item.data.slotSize;
        float scaleX = size > 1 ? size + 0.1f : 1f;
        transform.localScale = new Vector3(scaleX, 1f, 1f);

        var rt = GetComponent<RectTransform>();
        if (rt != null)
            rt.pivot = new Vector2(0f, rt.pivot.y);

        UpdateCountDisplay();
    }

    public void Clear()
    {
        isOccupied = false;
        item = null;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
            iconImage.color = Color.white;
        }

        transform.localScale = Vector3.one;

        var rt = GetComponent<RectTransform>();
        if (rt != null)
            rt.pivot = new Vector2(0.5f, rt.pivot.y);

        UpdateCountDisplay();
    }

    public void UpdateCountDisplay()
    {
        if (countText == null) return;

        if (item == null)
        {
            countText.text = "";
            countText.gameObject.SetActive(false);
            return;
        }

        if (item.data is AmmoItemData ammo && ammo.individualMagazines)
        {
            int shown = item.totalAmmo;
            if (shown <= 0) shown = item.currentAmmo;

            if (shown > 0)
            {
                countText.text = $"x{shown}";
                countText.gameObject.SetActive(true);
            }
            else
            {
                countText.text = "";
                countText.gameObject.SetActive(false);
            }
        }
        else
        {
            int shown = Mathf.Max(1, item.count);
            countText.text = (shown > 1) ? $"x{shown}" : "";
            countText.gameObject.SetActive(shown > 1);
        }
    }
}
