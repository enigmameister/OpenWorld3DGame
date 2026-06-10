using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PointerClickToggleSlider : MonoBehaviour, IPointerClickHandler
{
    private BankAccountCreateUI _ui;
    private Slider _slider;

    public void Init(BankAccountCreateUI ui)
    {
        _ui = ui;
        _slider = GetComponent<Slider>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_ui == null || _slider == null) return;
        bool nextOn = _slider.value < 1f; // 0->ON, 1->OFF
        _ui.ExternalToggleCardIssue(nextOn);
    }

}
