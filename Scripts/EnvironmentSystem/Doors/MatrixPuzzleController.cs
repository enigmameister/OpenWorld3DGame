using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MatrixPuzzleController : MonoBehaviour
{
    [Header("Matrix")]
    public GameObject matrixCanvas;           // Canvas
    public Transform matrixGrid;              // GridLayoutGroup dla 4 slotów
    public GameObject matrixSlotPrefab;       // Prefab z Button + TMP_Text
    private List<TextMeshProUGUI> matrixTexts = new(); // Referencje TMP z prefabów
    private int[] matrixValues = new int[4];  // a b c d
    private int correctDeterminant;

    [Header("Input")]
    public TextMeshProUGUI inputDisplay;      // TMP wyświetlający wpisaną wartość
    private string currentInput = "";

    [Header("Attempts")]
    public int maxAttempts = 3;
    private int remainingAttempts;

    [Header("Success")]
    public Slider loadingBar;
    public GameObject successPanel;
    public TextMeshProUGUI successText;
    public Image fillImage;

    private bool puzzleSolved = false;

    [Header("KeyPad")]
    public Transform numbersGrid;               // GridLayoutGroup: 1–9
    public Transform controlGrid;               // GridLayoutGroup: # 0 *
    public GameObject keypadNumberPrefab;       // Prefab z Button + TMP

    [Header("Próby")]
    public TextMeshProUGUI attemptsText;  // ← TMP: AttemptsLeft/Count

    [Header("Opcje")]
    public bool useMatrix3x3 = false;

    [Header("Trigger powiązany z tym panelem")]
    public MatrixPuzzleTrigger linkedTrigger;

    [Header("Kod finalny")]
    public int codeIndex = -1; // np. 0, 1, 2, 3


    void Start()
    {
        successPanel.SetActive(false);
        loadingBar.gameObject.SetActive(false);
        matrixCanvas.SetActive(false); // <- domyślnie ukryte

        StartCoroutine(DelayedMatrixInit());
        GenerateKeypad();
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && matrixCanvas.activeSelf)
        {
            ClosePuzzleUI();
        }
    }

    private IEnumerator DelayedMatrixInit()
    {
        yield return new WaitForEndOfFrame(); // pozwala innym Start() zakończyć
        GenerateMatrix();
    }

    public void OpenPuzzleUI()
    {
        if (puzzleSolved) return;

        matrixCanvas.SetActive(true);
        Time.timeScale = 0f;

        PlayerMovement.IsMovementLocked = true;
        MouseLook.IsLookLocked = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ClosePuzzleUI()
    {
        matrixCanvas.SetActive(false);
        Time.timeScale = 1f;

        PlayerMovement.IsMovementLocked = false;
        MouseLook.IsLookLocked = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


    void UpdateAttemptsUI()
    {
        if (attemptsText != null)
            attemptsText.text = $"TRIES: {remainingAttempts}";
    }

    void GenerateKeypad()
    {
        string[] numberKeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        string[] controlKeys = { "#", "0", "*" };

        // Numery 1–9
        foreach (string key in numberKeys)
        {
            GameObject button = Instantiate(keypadNumberPrefab, numbersGrid);
            SetupKey(button, key);
        }

        // Kontrolki: # 0 *
        foreach (string key in controlKeys)
        {
            GameObject button = Instantiate(keypadNumberPrefab, controlGrid);
            SetupKey(button, key);
        }
    }
    void SetupKey(GameObject buttonObj, string key)
    {
        TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = key;

        Button btn = buttonObj.GetComponentInChildren<Button>();
        if (btn != null)
            btn.onClick.AddListener(() => OnKeypadPressed(key));
    }

    void GenerateMatrix()
    {
        currentInput = "";
        inputDisplay.text = "";
        remainingAttempts = maxAttempts;
        UpdateAttemptsUI();

        int determinant = -999;

        if (useMatrix3x3)
        {
            matrixValues = new int[9];
            while (determinant < 0 || determinant > 9)
            {
                for (int i = 0; i < 9; i++)
                    matrixValues[i] = Random.Range(0, 10);

                determinant = CalculateDeterminant3x3(matrixValues);
            }
        }
        else
        {
            matrixValues = new int[4];
            while (determinant < 0 || determinant > 9)
            {
                for (int i = 0; i < 4; i++)
                    matrixValues[i] = Random.Range(0, 10);

                determinant = CalculateDeterminant2x2(matrixValues[0], matrixValues[1], matrixValues[2], matrixValues[3]);
            }
        }

        correctDeterminant = determinant;

        if (codeIndex >= 0 && PuzzleCodeManager.Instance != null)
        {
            PuzzleCodeManager.Instance.SetDigit(codeIndex, correctDeterminant);
            Debug.Log($"[MATRIX {codeIndex}] Wyznacznik = {correctDeterminant}");
        }
        else
        {
            Debug.LogWarning($"⚠️ Matrix nie ustawił kodu – codeIndex={codeIndex}, PuzzleCodeManager.Instance={PuzzleCodeManager.Instance}");
        }

        // Wygeneruj prefabowe sloty
        matrixTexts.Clear();
        foreach (Transform child in matrixGrid)
            Destroy(child.gameObject);

        for (int i = 0; i < matrixValues.Length; i++)
        {
            GameObject slot = Instantiate(matrixSlotPrefab, matrixGrid);
            var tmp = slot.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = matrixValues[i].ToString();
                matrixTexts.Add(tmp);
            }

            var btn = slot.GetComponentInChildren<Button>();
            if (btn != null) btn.interactable = false;
        }
    }

    int CalculateDeterminant2x2(int a, int b, int c, int d)
    {
        return a * d - b * c;
    }

    int CalculateDeterminant3x3(int[] m)
    {
        return
            m[0] * (m[4] * m[8] - m[5] * m[7]) -
            m[1] * (m[3] * m[8] - m[5] * m[6]) +
            m[2] * (m[3] * m[7] - m[4] * m[6]);
    }

    public void OnKeypadPressed(string key)
    {
        if (puzzleSolved) return;

        switch (key)
        {
            case "#":
                currentInput = "";
                inputDisplay.text = "";
                break;

            case "*":
                CheckInput();
                break;

            default: // 0–9
                if (currentInput.Length < 2)
                {
                    currentInput += key;
                    inputDisplay.text = currentInput;
                }
                break;
        }
    }

    void CheckInput()
    {
        if (!int.TryParse(currentInput, out int inputValue))
            return;

        if (inputValue == correctDeterminant)
        {
            puzzleSolved = true;
            StartCoroutine(ShowSuccess());
        }
        else
        {
            remainingAttempts--;
            UpdateAttemptsUI(); // ← DODAJ TUTAJ

            if (remainingAttempts <= 0)
            {
                GenerateMatrix();
            }

            currentInput = "";
            inputDisplay.text = "";
        }
    }

    IEnumerator ShowSuccess()
    {
        inputDisplay.color = Color.green;

        loadingBar.value = 0f;
        loadingBar.gameObject.SetActive(true);
        float time = 0f;
        float duration = 1.0f;

        if (fillImage == null && loadingBar.fillRect != null)
            fillImage = loadingBar.fillRect.GetComponent<Image>();

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            loadingBar.value = t;

            if (fillImage != null)
            {
                fillImage.color =
                    t < 0.25f ? Color.Lerp(Color.red, new Color(1f, 0.5f, 0f), t / 0.25f) :
                    t < 0.5f ? Color.Lerp(new Color(1f, 0.5f, 0f), Color.yellow, (t - 0.25f) / 0.25f) :
                    t < 0.75f ? Color.Lerp(Color.yellow, Color.green, (t - 0.5f) / 0.25f) :
                                Color.Lerp(Color.green, Color.cyan, (t - 0.75f) / 0.25f);
            }

            yield return null;
        }

        successPanel.SetActive(true);
        if (successText != null)
            successText.text = "WYNIK POPRAWNY";

        if (linkedTrigger != null)
            linkedTrigger.MarkPuzzleAsSolved();

        if (linkedTrigger != null)
            linkedTrigger.SetSolvedLabel(codeIndex, correctDeterminant);

    }
}
