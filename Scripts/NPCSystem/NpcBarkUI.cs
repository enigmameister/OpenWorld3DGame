using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcBarkUI : MonoBehaviour
{
    [Header("Layout Fix")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private bool forceTextWrapping = true;
    [SerializeField] private float minRowHeight = 60f;

    [System.Serializable]
    private class BarkSlot
    {
        public GameObject rowRoot;
        public TMP_Text nameText;
        public TMP_Text lineText;
        public void SetActive(bool on)
        {
            if (rowRoot != null)
                rowRoot.SetActive(on);
        }

        public void SetData(string speaker, string line, Color nameColor, Color textColor)
        {
            if (nameText)
            {
                nameText.text = speaker;
                nameText.color = nameColor;
            }

            if (lineText)
            {
                lineText.text = line;
                lineText.color = textColor;
            }
        }
    }

    private class BarkEntry
    {
        public string speaker;
        public string line;
        public float expireAt;
        public Color nameColor;
        public Color textColor;
    }

    [Header("Root")]
    [SerializeField] private CanvasGroup rootGroup;

    [Header("Optional gameplay UI")]
    [SerializeField] private GameObject wheelRoot;

    [Header("Slots (top -> bottom)")]
    [SerializeField] private GameObject row1;
    [SerializeField] private TMP_Text name1;
    [SerializeField] private TMP_Text text1;

    [SerializeField] private GameObject row2;
    [SerializeField] private TMP_Text name2;
    [SerializeField] private TMP_Text text2;

    [SerializeField] private GameObject row3;
    [SerializeField] private TMP_Text name3;
    [SerializeField] private TMP_Text text3;

    [SerializeField] private GameObject row4;
    [SerializeField] private TMP_Text name4;
    [SerializeField] private TMP_Text text4;

    [Header("Timing")]
    [SerializeField] private float defaultDuration = 2.5f;
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private int maxVisibleBarks = 4;

    [Header("Colors")]
    [SerializeField] private Color defaultNameColor = new Color(1f, 0.62f, 0.12f);
    [SerializeField] private Color defaultTextColor = new Color(0.92f, 0.92f, 0.92f);

    private readonly List<BarkEntry> activeBarks = new();
    private BarkSlot[] slots;

    private void Awake()
    {
        gameObject.SetActive(true);

        if (contentRoot == null)
            contentRoot = transform.Find("Dialog/Content") as RectTransform;

        PrepareAllTextFields();

        slots = new BarkSlot[]
        {
        new BarkSlot { rowRoot = row1, nameText = name1, lineText = text1 },
        new BarkSlot { rowRoot = row2, nameText = name2, lineText = text2 },
        new BarkSlot { rowRoot = row3, nameText = name3, lineText = text3 },
        new BarkSlot { rowRoot = row4, nameText = name4, lineText = text4 },
        };

        if (wheelRoot != null)
            wheelRoot.SetActive(false);

        HideImmediate();
    }

    private void Update()
    {
        bool changed = false;

        for (int i = activeBarks.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime >= activeBarks[i].expireAt)
            {
                activeBarks.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            RefreshUI();

        float targetAlpha = activeBarks.Count > 0 ? 1f : 0f;
        if (rootGroup != null)
        {
            rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }
    }

    public void ShowBark(string speaker, string line, float duration = -1f)
    {
        ShowBark(speaker, line, defaultNameColor, defaultTextColor, duration);
    }

    public void ShowBark(string speaker, string line, Color nameColor, Color textColor, float duration = -1f)
    {
        if (string.IsNullOrWhiteSpace(line) || rootGroup == null)
            return;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (wheelRoot != null && wheelRoot.activeSelf)
            wheelRoot.SetActive(false);

        BarkEntry entry = new BarkEntry
        {
            speaker = string.IsNullOrWhiteSpace(speaker) ? "NPC" : speaker,
            line = line,
            expireAt = Time.unscaledTime + (duration > 0f ? duration : defaultDuration),
            nameColor = nameColor,
            textColor = textColor
        };

        activeBarks.Add(entry);

        while (activeBarks.Count > Mathf.Max(1, maxVisibleBarks))
            activeBarks.RemoveAt(0);

        RefreshUI();
        ForceBarkLayout();
    }

    private void ForceBarkLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (contentRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);

        RectTransform self = transform as RectTransform;

        if (self != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(self);

        Canvas.ForceUpdateCanvases();
    }

    public void HideImmediate()
    {
        activeBarks.Clear();

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        if (wheelRoot != null)
            wheelRoot.SetActive(false);

        RefreshUI();
    }

    public void SetWheelVisible(bool visible)
    {
        if (wheelRoot != null)
            wheelRoot.SetActive(visible);
    }

    private void RefreshUI()
    {
        if (slots == null || slots.Length == 0)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].SetActive(false);
        }

        int visibleCount = Mathf.Min(activeBarks.Count, slots.Length);
        int startIndex = Mathf.Max(0, activeBarks.Count - slots.Length);

        for (int slotIndex = 0; slotIndex < visibleCount; slotIndex++)
        {
            BarkEntry bark = activeBarks[startIndex + slotIndex];
            BarkSlot slot = slots[slotIndex];

            slot.SetActive(true);
            slot.SetData(bark.speaker, bark.line, bark.nameColor, bark.textColor);
        }

        ForceBarkLayout();
    }

    private void PrepareAllTextFields()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
            PrepareText(texts[i]);

        LayoutElement[] layoutElements = GetComponentsInChildren<LayoutElement>(true);

        for (int i = 0; i < layoutElements.Length; i++)
        {
            if (layoutElements[i] == null)
                continue;

            if (layoutElements[i].gameObject.name.StartsWith("Row_"))
                layoutElements[i].preferredHeight = Mathf.Max(layoutElements[i].preferredHeight, minRowHeight);
        }
    }

    private void PrepareText(TMP_Text text)
    {
        if (text == null || !forceTextWrapping)
            return;

        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
    }
}