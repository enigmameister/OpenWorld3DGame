using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoanMenuButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Image background;

    private int _index;
    private System.Action<int> _onClick;

    public Button Button => button;

    public void Setup(int index, string label, System.Action<int> onClick)
    {
        _index = index;
        _onClick = onClick;

        if (labelText != null)
            labelText.text = label;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _onClick?.Invoke(_index));
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (background == null) return;

        Color c = background.color;
        c.a = selected ? 0.55f : 0.10f;
        background.color = c;
    }

    public void Click()
    {
        _onClick?.Invoke(_index);
    }
}