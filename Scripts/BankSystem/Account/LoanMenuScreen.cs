using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoanMenuScreen : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private CanvasGroup root;

    [Header("Header")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private float headerHeight = 42f;

    [Header("Buttons")]
    [SerializeField] private RectTransform buttonsRoot;
    [SerializeField] private LoanMenuButtonView buttonPrefab;
    [SerializeField] private float topPadding = 12f;
    [SerializeField] private float bottomPadding = 12f;
    [SerializeField] private float gapUnderHeader = 12f;


    private readonly List<LoanMenuButtonView> _spawnedButtons = new();

    public void Rebuild(List<string> labels, System.Action<int> onClick)
    {
        ClearButtonsInternal();

        if (labels == null)
            labels = new List<string>();

        for (int i = 0; i < labels.Count; i++)
        {
            var btn = Instantiate(buttonPrefab, buttonsRoot);
            btn.gameObject.SetActive(true);
            btn.Setup(i, labels[i], onClick);
            _spawnedButtons.Add(btn);
        }

        RefreshLayout();
    }

    public void ClearButtons()
    {
        ClearButtonsInternal();
        RefreshLayout();
    }

    private void ClearButtonsInternal()
    {
        for (int i = 0; i < _spawnedButtons.Count; i++)
        {
            if (_spawnedButtons[i] != null)
            {
                Transform t = _spawnedButtons[i].transform;
                t.SetParent(null, false);   // najwa¿niejsze
                Destroy(_spawnedButtons[i].gameObject);
            }
        }

        _spawnedButtons.Clear();
    }

    private void RefreshLayout()
    {
        if (buttonsRoot == null || rootRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(buttonsRoot);

        float buttonsHeight = 0f;
        int activeCount = 0;

        var layout = buttonsRoot.GetComponent<VerticalLayoutGroup>();
        float spacing = layout != null ? layout.spacing : 0f;

        for (int i = 0; i < buttonsRoot.childCount; i++)
        {
            RectTransform child = buttonsRoot.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
                continue;

            float h = LayoutUtility.GetPreferredHeight(child);
            if (h <= 0f)
                h = child.rect.height;

            buttonsHeight += h;
            activeCount++;
        }

        if (activeCount > 1)
            buttonsHeight += spacing * (activeCount - 1);

        float totalHeight = topPadding + headerHeight + gapUnderHeader + buttonsHeight + bottomPadding;

        rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
    }

    public void RefreshNow()
    {
        RefreshLayout();
    }

    public void Show(bool visible)
    {
        if (!root)
        {
            gameObject.SetActive(visible);
            return;
        }

        root.gameObject.SetActive(visible);
        root.alpha = visible ? 1f : 0f;
        root.interactable = visible;
        root.blocksRaycasts = visible;
    }

    public int ButtonCount => _spawnedButtons.Count;

    public LoanMenuButtonView GetButton(int index)
    {
        if (index < 0 || index >= _spawnedButtons.Count)
            return null;

        return _spawnedButtons[index];
    }

    public void SetSelectedIndex(int index)
    {
        for (int i = 0; i < _spawnedButtons.Count; i++)
        {
            if (_spawnedButtons[i] != null)
                _spawnedButtons[i].SetSelected(i == index);
        }
    }
}