using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PinSlotView : MonoBehaviour
{
    public Image border;
    public TMP_Text text;
    public GameObject arrow;

    public void SetDigit(int d)
    {
        if (!text) return;
        text.text = (d < 0) ? "" : d.ToString();
    }

    public void SetArrow(bool on)
    {
        if (arrow) arrow.SetActive(on);
    }

    public void SetBorderColor(Color c)
    {
        if (border) border.color = c;
    }

    // kompatybilnoœæ z wczeœniejszymi wywo³aniami
    public void SetActive(bool active, Color idle, Color activeCol)
    {
        SetBorderColor(active ? activeCol : idle);
    }
}
