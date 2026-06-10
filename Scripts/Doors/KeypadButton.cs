using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class KeypadButton : MonoBehaviour
{
    public TextMeshProUGUI label;
    public MatrixPuzzleController controller;

    void Start()
    {
        var btn = GetComponent<Button>();
        if (btn != null && label != null && controller != null)
        {
            string key = label.text;
            btn.onClick.AddListener(() => controller.OnKeypadPressed(key));
        }
    }
}
