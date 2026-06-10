using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventoryTooltip : MonoBehaviour
{
    public static InventoryTooltip Instance { get; private set; }

    [Header("Refs")]
    public RectTransform root;          // ToolTip (RectTransform)
    public RectTransform body;          // Body (RectTransform)

    [Header("Teksty / wiersze")]
    public TextMeshProUGUI nameText;

    public GameObject ammoRow;
    public TextMeshProUGUI ammoValueText;

    public GameObject damageRow;
    public TextMeshProUGUI damageValueText;

    public GameObject fireRateRow;
    public TextMeshProUGUI fireRateValueText;

    public GameObject ammoTypeRow;
    public TextMeshProUGUI ammoTypeValueText;

    public GameObject descriptionRow;
    public TextMeshProUGUI descriptionValueText;

    [Header("Rozmiar")]
    public float tooltipWidth = 180f;   // stała szerokość tooltipa
    public Vector2 screenMargin = new Vector2(8f, 8f);

    private bool isVisible;
    CanvasGroup group;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (!root) root = (RectTransform)transform;

        group = GetComponent<CanvasGroup>();
        if (!group) group = gameObject.AddComponent<CanvasGroup>();

        HideImmediate();
    }

    private void Update()
    {
        if (!isVisible) return;
        SetPosition(Input.mousePosition);
    }

    // ---------------- PUBLIC API ----------------

    public void ShowForItem(InventoryItemInstance inst, int slotSize, Vector2 screenPos)
    {
        if (inst == null || inst.data == null)
        {
            Hide();
            return;
        }

        FillContent(inst);
        SetWidth();
        SetPosition(screenPos);
        Show();
    }

    public void Show(InventoryItemData data, InventoryItemInstance inst)
    {
        if (inst == null && data != null)
            inst = new InventoryItemInstance(data);

        ShowForItem(inst, 1, Input.mousePosition);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        isVisible = true;

        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    public void Hide()
    {
        if (!gameObject.activeInHierarchy) return;

        isVisible = false;
        group.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void HideImmediate()
    {
        if (!group) return;

        isVisible = false;
        group.alpha = 0f;
        gameObject.SetActive(false);
    }

    // ---------------- LAYOUT ----------------

    void SetWidth()
    {
        if (!root) return;
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, tooltipWidth);
    }

    /// <summary>
    /// Ustaw pozycję tooltipa pod kursorem (Screen Space Overlay).
    /// screenPos – pozycja kursora w pikselach (PointerEventData.position / Input.mousePosition).
    /// </summary>
    public void SetPosition(Vector2 screenPos)
    {
        if (!root) return;

        // mały offset od kursora jak w Silkroadzie
        Vector2 pos = screenPos + new Vector2(16f, -16f);

        // rozmiar tooltipa
        Vector2 size = root.rect.size;
        Vector2 half = size * 0.5f + screenMargin;

        float xMin = half.x;
        float xMax = Screen.width - half.x;
        float yMin = half.y;
        float yMax = Screen.height - half.y;

        pos.x = Mathf.Clamp(pos.x, xMin, xMax);
        pos.y = Mathf.Clamp(pos.y, yMin, yMax);

        // w Canvasie typu Screen Space Overlay root.position jest w pikselach ekranu
        root.position = pos;
    }

    // ---------------- WYPEŁNIANIE DANYCH ----------------

    void FillContent(InventoryItemInstance inst)
    {
        var data = inst.data;
        nameText.text = data.itemName;

        // domyślnie wyłącz wszystkie wiersze
        ammoRow?.SetActive(false);
        damageRow?.SetActive(false);
        fireRateRow?.SetActive(false);
        ammoTypeRow?.SetActive(false);
        descriptionRow?.SetActive(false);

        if (data is WeaponItemData w)
        {
            if (ammoRow && ammoValueText)
            {
                ammoRow.SetActive(true);
                ammoValueText.text = $"{inst.currentAmmo}/{inst.totalAmmo}";
            }

            if (damageRow && damageValueText)
            {
                damageRow.SetActive(true);
                damageValueText.text = w.damage.ToString("0");
            }

            if (fireRateRow && fireRateValueText)
            {
                fireRateRow.SetActive(true);
                fireRateValueText.text = w.fireRate.ToString("0.00");
            }

            if (ammoTypeRow && ammoTypeValueText)
            {
                ammoTypeRow.SetActive(true);
                ammoTypeValueText.text = w.bulletType;
            }

            SetDescription(data.description);
        }
        else if (data is MeleeItemData melee)
        {
            if (damageRow && damageValueText)
            {
                damageRow.SetActive(true);
                damageValueText.text = $"{melee.fastDamage:0} / {melee.strongDamage:0}";
            }
            SetDescription(data.description);
        }

        else if (data is GrenadeItemData grenade)
        {
            if (damageRow && damageValueText)
            {
                damageRow.SetActive(true);
                damageValueText.text = grenade.explosionDamage.ToString("0");
            }
            if (ammoTypeRow && ammoTypeValueText)
            {
                ammoTypeRow.SetActive(true);
                ammoTypeValueText.text = $"Radius: {grenade.explosionRadius:0.0} m";
            }
            SetDescription(data.description);
        }

        else if (data is BankCardItemData card)
        {
            // wyłącz “broń/ammo” wiersze
            ammoRow?.SetActive(false);
            damageRow?.SetActive(false);
            fireRateRow?.SetActive(false);
            ammoTypeRow?.SetActive(false);

            // zbuduj opis z meta
            string desc = data.description;

            if (inst.hasBankCardMeta)
            {
                string status = inst.bankCard.status.ToString().ToUpper();
                string pin = inst.bankCard.pin.ToString("0000");

                desc =
                    $"Bank: {card.bankName}\n" +
                    $"Card ID: {inst.bankCard.cardId}\n" +
                    $"PIN: {pin}\n" +
                    $"Status: {status}\n\n" +
                    (string.IsNullOrWhiteSpace(data.description) ? "" : data.description);
            }

            SetDescription(desc);
        }

        else
        {
            SetDescription(data.description);
        }

        if (body)
            LayoutRebuilder.ForceRebuildLayoutImmediate(body);
    }

    void SetDescription(string desc)
    {
        if (!descriptionRow || !descriptionValueText) return;

        bool has = !string.IsNullOrWhiteSpace(desc);
        descriptionRow.SetActive(has);
        if (has) descriptionValueText.text = desc;
    }
}
