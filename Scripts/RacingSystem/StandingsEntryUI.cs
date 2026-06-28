using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StandingsEntryUI : MonoBehaviour
{
    public TextMeshProUGUI placeText;
    public TextMeshProUGUI nameText;
    public Image background;

    [Header("Colors")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.1f);
    public Color playerColor = new Color(0.3f, 0.6f, 1f, 0.4f);

    private bool isPlayer;

    public void Set(int place, string name, bool isPlayerEntry)
    {
        isPlayer = isPlayerEntry;

        if (placeText != null)
            placeText.text = place.ToString();

        if (nameText != null)
            nameText.text = name;

        UpdateVisual();
    }

    public void UpdateVisual()
    {
        if (background != null)
            background.color = isPlayer ? playerColor : normalColor;

        // 🔥 HIGHLIGHT GRACZA
        if (isPlayer)
        {
            if (placeText != null)
                placeText.color = Color.yellow;

            if (nameText != null)
                nameText.color = Color.yellow;
        }
        else
        {
            if (placeText != null)
                placeText.color = Color.white;

            if (nameText != null)
                nameText.color = Color.white;
        }
    }

    public void SetDistanceText(string text)
    {
        if (nameText != null)
            nameText.text = text;
    }
}