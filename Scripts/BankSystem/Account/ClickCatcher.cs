using UnityEngine;
using UnityEngine.EventSystems;

public class ClickCatcher : MonoBehaviour, IPointerClickHandler
{
    public TransactionsHistoryPanelUI panel;

    public void OnPointerClick(PointerEventData eventData)
    {
        panel?.ClearSelection();
    }
}