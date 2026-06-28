using UnityEngine;
using UnityEngine.UI;

public class VariantSlotView : MonoBehaviour
{
    public GameObject activeBorder;
    public Image preview;
    public GameObject arrow;

    private Image _borderImg;

    private void Awake()
    {
        if (activeBorder) _borderImg = activeBorder.GetComponent<Image>();
    }

    public void SetHover(bool hover)
    {
        if (arrow) arrow.SetActive(hover);
    }

    // selected = czy border ma œwieciæ
    // highlighted = np. ¿ó³ty, gdy wariant zosta³ wybrany ENTERem
    public void SetSelected(bool selected, bool highlighted = false)
    {
        if (activeBorder) activeBorder.SetActive(selected);

        if (_borderImg != null)
        {
            _borderImg.color = highlighted ? Color.yellow : Color.white;
        }
    }

    public void SetPreviewColor(Color color)
    {
        if (preview) preview.color = color;
    }
}