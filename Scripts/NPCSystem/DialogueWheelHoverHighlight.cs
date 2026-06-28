using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DialogueWheelHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Optional visuals")]
    public Image background;
    public TMP_Text label;
    public GameObject highlightObject; // jeœli masz osobny glow/outline

    [Header("Colors (optional)")]
    public Color normalBg = Color.white;
    public Color hoverBg = Color.white;
    public Color normalText = Color.white;
    public Color hoverText = Color.white;

    private bool _hasBg;
    private bool _hasLabel;

    private void Awake()
    {
        if (!background) background = GetComponent<Image>();
        if (!label) label = GetComponentInChildren<TMP_Text>(true);

        _hasBg = background != null;
        _hasLabel = label != null;

        Apply(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => Apply(true);
    public void OnPointerExit(PointerEventData eventData) => Apply(false);

    private void Apply(bool hover)
    {
        if (highlightObject) highlightObject.SetActive(hover);

        if (_hasBg) background.color = hover ? hoverBg : normalBg;
        if (_hasLabel) label.color = hover ? hoverText : normalText;
    }
}
