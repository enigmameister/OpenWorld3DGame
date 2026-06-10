using TMPro;
using UnityEngine;

public class TimeDisplayUI : MonoBehaviour
{
    public TextMeshProUGUI timeText;

    void Update()
    {
        if (timeText == null) return;
        if (GameTimeSystem.Instance == null) return;

        var t = GameTime.Now;

        // Format:
        // 00:00
        // 01-07-2050 Thu
        string line1 = t.ToString("HH:mm");
        string line2 = t.ToString("dd-MM-yyyy ddd");

        timeText.text = $"{line1}\n{line2}";
    }

}
