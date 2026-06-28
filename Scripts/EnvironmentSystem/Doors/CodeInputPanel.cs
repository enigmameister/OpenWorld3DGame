using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class CodeInputPanel : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelCanvas;
    public Transform inputCodeGrid;
    public GameObject codeSlotPrefab;
    public Animator doorAnimator;
    public Light statusLight;
    public Color solvedColor = Color.green;

    [Header("Keypad")]
    public Transform numbersGrid;
    public Transform controlGrid;
    public GameObject keypadNumberPrefab;

    private List<TextMeshProUGUI> codeSlots = new();
    private string currentInput = "";

    private bool keypadGenerated = false;

    void Start()
    {
        if (!keypadGenerated)
        {
            GenerateSlots();
            GenerateKeypad();
            keypadGenerated = true;
        }
    }

    public void OpenPanel()
    {
        if (!PuzzleCodeManager.Instance.IsCodeComplete())
        {
            Debug.LogWarning("❗ Próba otwarcia panelu przed ustawieniem pełnego kodu!");
            return;
        }

        panelCanvas.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 0f;
        PlayerMovement.IsMovementLocked = true;
        MouseLook.IsLookLocked = true;

        currentInput = "";
        UpdateCodeUI();
    }

    public void ClosePanel()
    {
        panelCanvas.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Time.timeScale = 1f;
        PlayerMovement.IsMovementLocked = false;
        MouseLook.IsLookLocked = false;
    }

    public void OnKeyPressed(string key)
    {
        switch (key)
        {
            case "#":
                currentInput = "";
                break;

            case "*":
                ClosePanel();
                return;

            default:
                if (currentInput.Length < 4)
                    currentInput += key;
                break;
        }

        UpdateCodeUI();

        if (currentInput.Length == 4)
        {
            if (currentInput == PuzzleCodeManager.Instance.GetFullCode())
            {
                ClosePanel();

                if (doorAnimator != null)
                    doorAnimator.SetBool("Key", true);

                if (statusLight != null)
                    statusLight.color = solvedColor;
            }
            else
            {
                currentInput = "";
                UpdateCodeUI();
            }
        }

    }

    void UpdateCodeUI()
    {
        for (int i = 0; i < codeSlots.Count; i++)
        {
            codeSlots[i].text = i < currentInput.Length ? currentInput[i].ToString() : "";
        }
    }

    void GenerateSlots()
    {
        foreach (Transform child in inputCodeGrid)
            Destroy(child.gameObject);
        codeSlots.Clear();

        for (int i = 0; i < 4; i++)
        {
            GameObject slot = Instantiate(codeSlotPrefab, inputCodeGrid);
            var txt = slot.GetComponentInChildren<TextMeshProUGUI>();

            if (txt != null)
            {
                codeSlots.Add(txt);
            }
            else
            {
                Debug.LogWarning($"❗ Brak TMP w slocie {i}");
            }
        }
    }

    void GenerateKeypad()
    {
        string[] digits = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        string[] controls = { "#", "0", "*" };

        foreach (string key in digits)
        {
            GameObject btn = Instantiate(keypadNumberPrefab, numbersGrid);
            SetupKey(btn, key);
        }

        foreach (string key in controls)
        {
            GameObject btn = Instantiate(keypadNumberPrefab, controlGrid);
            SetupKey(btn, key);
        }
    }

    void SetupKey(GameObject buttonObj, string key)
    {
        TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = key;

        KeypadButtonFinal kb = buttonObj.GetComponent<KeypadButtonFinal>();
        if (kb != null)
            kb.Initialize(key, this);
    }
}
