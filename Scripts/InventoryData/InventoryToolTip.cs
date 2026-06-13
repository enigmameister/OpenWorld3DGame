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
    public RectTransform border;
    public RectTransform fill;

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

    [Header("Auto size")]
    [SerializeField] private float minWidth = 90f;
    [SerializeField] private float maxWidth = 220f;
    [SerializeField] private float minHeight = 32f;
    [SerializeField] private float maxHeight = 320f;
    [SerializeField] private Vector2 bodyMargin = new Vector2(12f, 12f);

    [SerializeField] private float borderThickness = 3f;
    [SerializeField] private float bodyPadding = 8f;

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

        if (!border && root) border = root.Find("Border") as RectTransform;
        if (!fill && root) fill = root.Find("Fill") as RectTransform;

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

        // Ważne: obiekt musi być aktywny PRZED liczeniem layoutu.
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        FillContent(inst);
        RebuildTooltipSize();
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
        isVisible = false;

        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    public void HideImmediate()
    {
        isVisible = false;

        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
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
            bool isMeleeWeapon = w.category == WeaponCategory.Melees;

            if (!isMeleeWeapon)
            {
                if (ammoRow && ammoValueText)
                {
                    ammoRow.SetActive(true);
                    ammoValueText.text = $"{inst.currentAmmo}/{inst.totalAmmo}";
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
            }

            if (damageRow && damageValueText)
            {
                damageRow.SetActive(true);
                damageValueText.text = w.damage.ToString("0");
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
    }

    void SetDescription(string desc)
    {
        if (!descriptionRow || !descriptionValueText) return;

        bool has = !string.IsNullOrWhiteSpace(desc);
        descriptionRow.SetActive(has);
        if (has) descriptionValueText.text = desc;
    }

    private void RebuildTooltipSize()
    {
        if (root == null || body == null)
            return;

        float finalWidth = Mathf.Clamp(tooltipWidth, minWidth, maxWidth);

        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalWidth);

        ApplyPanelRects();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(body);

        float rowsHeight = 0f;
        int activeRows = 0;

        for (int i = 0; i < body.childCount; i++)
        {
            RectTransform child = body.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
                continue;

            float h = LayoutUtility.GetPreferredHeight(child);

            if (h <= 0.01f)
                h = child.rect.height;

            if (h <= 0.01f)
                h = 22f;

            rowsHeight += h;
            activeRows++;
        }

        VerticalLayoutGroup vertical = body.GetComponent<VerticalLayoutGroup>();

        float paddingTop = 0f;
        float paddingBottom = 0f;
        float spacing = 0f;

        if (vertical != null)
        {
            paddingTop = vertical.padding.top;
            paddingBottom = vertical.padding.bottom;
            spacing = vertical.spacing;
        }

        float contentHeight =
            rowsHeight +
            paddingTop +
            paddingBottom +
            Mathf.Max(0, activeRows - 1) * spacing;

        float finalHeight = Mathf.Clamp(
            contentHeight + bodyPadding * 2f,
            minHeight,
            maxHeight
        );

        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalWidth);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);

        ApplyPanelRects();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(body);
    }


    private void ApplyPanelRects()
    {
        if (border != null)
        {
            border.anchorMin = Vector2.zero;
            border.anchorMax = Vector2.one;
            border.pivot = new Vector2(0.5f, 0.5f);
            border.offsetMin = Vector2.zero;
            border.offsetMax = Vector2.zero;
        }

        if (fill != null)
        {
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0.5f, 0.5f);
            fill.offsetMin = new Vector2(borderThickness, borderThickness);
            fill.offsetMax = new Vector2(-borderThickness, -borderThickness);
        }

        if (body != null)
        {
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.pivot = new Vector2(0f, 1f);
            body.offsetMin = new Vector2(bodyPadding, bodyPadding);
            body.offsetMax = new Vector2(-bodyPadding, -bodyPadding);
        }
    }
}
