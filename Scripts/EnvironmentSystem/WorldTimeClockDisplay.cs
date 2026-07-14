using TMPro;
using UnityEngine;

public class WorldTimeClockDisplay : MonoBehaviour
{
    [Header("TMP")]
    [SerializeField] private TMP_Text timeText;

    [Header("Format")]
    [SerializeField] private bool useColon = true;

    private int lastMinute = -1;

    private void Awake()
    {
        if (timeText == null)
            timeText = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (timeText == null)
            return;

        if (GameTimeSystem.Instance == null)
        {
            timeText.text = "--:--";
            return;
        }

        int minuteOfDay = GameTime.MinuteOfDay;

        // Odœwie¿aj tylko po zmianie minuty, nie co klatkê.
        if (minuteOfDay == lastMinute)
            return;

        lastMinute = minuteOfDay;

        if (useColon)
            timeText.text = GameTime.Now.ToString("HH:mm");
        else
            timeText.text = GameTime.Now.ToString("HH mm");
    }
}