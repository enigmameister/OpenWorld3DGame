using UnityEngine;
using UnityEngine.EventSystems;

public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public InventoryCharacterPreview preview;
    public enum Mode { Left, Right }
    public Mode mode;

    bool isDown;

    public void OnPointerDown(PointerEventData eventData)
    {
        isDown = true;
        if (preview == null) return;
        if (mode == Mode.Left) preview.HoldLeft_On(true);
        if (mode == Mode.Right) preview.HoldRight_On(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Stop();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // gdy kursor zjedzie z przycisku podczas trzymania
        if (isDown) Stop();
    }

    void Stop()
    {
        isDown = false;
        if (preview == null) return;
        preview.HoldLeft_On(false);
        preview.HoldRight_On(false);
    }
}
