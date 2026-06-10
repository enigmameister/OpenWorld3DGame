using UnityEngine;

public class KeypadButtonNSG : MonoBehaviour, IPressable
{
    public ReaderZone reader;
    public string key = "0";

    [Header("Optional Hover Highlight")]
    public Renderer highlightRenderer;
    public Color hoverColor = Color.yellow;
    private Color originalColor;

    void Awake()
    {
        if (highlightRenderer != null)
            originalColor = highlightRenderer.material.color;
    }

    public void Press()
    {
        if (!reader || string.IsNullOrEmpty(key)) return;
        reader.OnKeypadKey(key[0]);
    }

    public string Label => $"Keypad[{key}]";

    public void SetHover(bool state)
    {
        if (!highlightRenderer) return;
        highlightRenderer.material.color = state ? hoverColor : originalColor;
    }
}
