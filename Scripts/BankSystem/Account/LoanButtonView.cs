using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LoanButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private GameObject selectedBg;

    private int _index;
    private System.Action<int> _onClick;

    public Button Button => button;

    public void Setup(int index, bool selected, System.Action<int> onClick)
    {
        _index = index;
        _onClick = onClick;

        if (label) label.text = (index + 1).ToString();

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }

        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectedBg) selectedBg.SetActive(selected);

        if (selected && button && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private void HandleClick()
    {
        _onClick?.Invoke(_index);
    }
}