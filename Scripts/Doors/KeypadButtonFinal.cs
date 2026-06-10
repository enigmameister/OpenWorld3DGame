using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class KeypadButtonFinal : MonoBehaviour
{
    private string key;
    private CodeInputPanel controller;

    public void Initialize(string keyValue, CodeInputPanel inputPanel)
    {
        controller = inputPanel;
        key = keyValue;

        Button btn = GetComponentInChildren<Button>();
        if (btn == null)
        {
            Debug.LogWarning($"{name} ❗ Brak komponentu Button!");
            return;
        }

        if (controller == null)
        {
            Debug.LogWarning($"{name} ❗ Initialize: brak controller == null");
            return;
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => controller.OnKeyPressed(key));
    }
}
