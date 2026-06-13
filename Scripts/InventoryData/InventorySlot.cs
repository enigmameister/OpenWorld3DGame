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
    public Image placementPreviewImage;
    public Image occupiedHighlightImage;

    [SerializeField] private Color defaultOccupiedColor = new Color(0.25f, 0.65f, 1f, 0.22f);
    public TextMeshProUGUI countText; // stackable items

    public int slotIndex; // ustawiane przez InventoryUI
    public InventoryTooltip tooltip => InventoryTooltip.Instance;

    public UnityEngine.Object owner;

    void Awake()
    {
        if (countText == null)
            countText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (placementPreviewImage == null)
        {
            Transform t = transform.Find("PlacementPreview");
            if (t != null)
                placementPreviewImage = t.GetComponent<Image>();
        }

        if (occupiedHighlightImage == null)
        {
            Transform t = transform.Find("OccupiedHighlight");
            if (t != null)
                occupiedHighlightImage = t.GetComponent<Image>();
        }

        ClearOccupiedHighlight();

        ClearPlacementPreview();
    }

    public void SetPlacementPreview(bool visible, Color color)
    {
        if (placementPreviewImage == null)
            return;

        placementPreviewImage.gameObject.SetActive(visible);

        if (visible)
            placementPreviewImage.color = color;
    }

    public void ClearPlacementPreview()
    {
        if (placementPreviewImage == null)
            return;

        placementPreviewImage.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (InventoryUI.IsDraggingInventoryItem || InventoryUI.draggedItem != null)
        {
            InventoryTooltip.Instance?.Hide();
            return;
        }

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

        // Skala dla itemów 1x1 / 1x2 / 1x3 oraz po rotacji 2x1 / 3x1
        int width = item.WidthSlots;
        int height = item.HeightSlots;

        float scaleX = width > 1 ? width + 0.1f : 1f;
        float scaleY = height > 1 ? height + 0.1f : 1f;

        transform.localScale = new Vector3(scaleX, scaleY, 1f);

        var rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            // pivot top-left, żeby item rozszerzał się w prawo i w dół
            if (width > 1 || height > 1)
                rt.pivot = new Vector2(0f, 1f);
            else
                rt.pivot = new Vector2(0.5f, 0.5f);
        }

        UpdateCountDisplay();
        SetOccupiedHighlight(true);
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
            rt.pivot = new Vector2(0.5f, 0.5f);

        if (fillImage != null) fillImage.SetActive(true);
        if (borderImage != null) borderImage.SetActive(true);

        UpdateCountDisplay();
        ClearPlacementPreview();
        ClearOccupiedHighlight();
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

    public void SetOccupiedHighlight(bool visible)
    {
        SetOccupiedHighlight(visible, defaultOccupiedColor);
    }

    public void SetOccupiedHighlight(bool visible, Color color)
    {
        if (occupiedHighlightImage == null)
            return;

        occupiedHighlightImage.gameObject.SetActive(visible);

        if (visible)
            occupiedHighlightImage.color = color;
    }

    public void ClearOccupiedHighlight()
    {
        if (occupiedHighlightImage == null)
            return;

        occupiedHighlightImage.gameObject.SetActive(false);
    }

    public void SetDraggingVisual(bool dragging)
    {
        if (dragging)
        {
            if (iconImage != null)
                iconImage.enabled = false;

            if (countText != null)
                countText.gameObject.SetActive(false);

            transform.localScale = Vector3.one;

            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
                rt.pivot = new Vector2(0.5f, 0.5f);

            if (fillImage != null)
                fillImage.SetActive(true);

            if (borderImage != null)
                borderImage.SetActive(true);

            ClearOccupiedHighlight();
            ClearPlacementPreview();

            return;
        }

        if (iconImage != null && item != null && item.data != null)
        {
            iconImage.sprite = item.data.icon;
            iconImage.enabled = iconImage.sprite != null;
            iconImage.color = Color.white;

            if (item.hasBankCardMeta && BankSystem.Instance != null)
                iconImage.color = BankSystem.Instance.GetVariantColor(item.bankCard.colorVariant);
        }

        UpdateCountDisplay();
        ClearPlacementPreview();
    }
}
