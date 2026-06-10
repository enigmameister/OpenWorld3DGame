using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ShelfCodePanel : MonoBehaviour
{
    [Header("Kod dostępu")]
    public int[] correctCode = new int[4] { 1, 2, 3, 4 };
    private int[] inputDigits = new int[4];
    private bool puzzleSolved = false;

    [Header("Sloty")]
    public TMP_Text[] digitTexts = new TMP_Text[4];
    public Image[] slotBorders = new Image[4]; // obramowania (np. Image background)
    public Color selectedColor = Color.yellow;
    public Color defaultColor = Color.white;

    [Header("Szafka / Animacja")]
    public Animator shelfAnimator;
    public string animationTrigger = "Open";

    [Header("Ustawienia scrollowania")]
    public float scrollDelay = 0.2f;
    private float nextScrollTime = 0f;

    private int selectedIndex = 0;

    void OnEnable()
    {
        if (puzzleSolved)
        {
            gameObject.SetActive(false);
            return;
        }

        selectedIndex = 0;
        for (int i = 0; i < 4; i++) inputDigits[i] = 0;

        Time.timeScale = 0f;
        PlayerMovement.IsMovementLocked = true;
        MouseLook.IsLookLocked = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdateUI();
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        PlayerMovement.IsMovementLocked = false;
        MouseLook.IsLookLocked = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePanel();
        }

        if (Time.unscaledTime < nextScrollTime) return;

        if (Input.GetKey(KeyCode.RightArrow))
        {
            selectedIndex = (selectedIndex + 1) % 4;
            UpdateUI();
            nextScrollTime = Time.unscaledTime + scrollDelay;
        }
        else if (Input.GetKey(KeyCode.LeftArrow))
        {
            selectedIndex = (selectedIndex + 3) % 4;
            UpdateUI();
            nextScrollTime = Time.unscaledTime + scrollDelay;
        }
        else if (Input.GetKey(KeyCode.UpArrow))
        {
            inputDigits[selectedIndex] = (inputDigits[selectedIndex] + 9) % 10;
            UpdateUI();
            CheckCode();
            nextScrollTime = Time.unscaledTime + scrollDelay;
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            inputDigits[selectedIndex] = (inputDigits[selectedIndex] + 1) % 10;
            UpdateUI();
            CheckCode();
            nextScrollTime = Time.unscaledTime + scrollDelay;
        }
    }

    void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    void UpdateUI()
    {
        for (int i = 0; i < 4; i++)
        {
            digitTexts[i].text = inputDigits[i].ToString();

            if (slotBorders.Length > i && slotBorders[i] != null)
                slotBorders[i].color = (i == selectedIndex) ? selectedColor : defaultColor;

            if (digitTexts[i] != null)
                digitTexts[i].color = (i == selectedIndex) ? selectedColor : defaultColor;
        }
    }

    void CheckCode()
    {
        for (int i = 0; i < 4; i++)
        {
            if (inputDigits[i] != correctCode[i])
                return;
        }

        puzzleSolved = true;

        if (shelfAnimator != null)
            shelfAnimator.SetTrigger(animationTrigger);

        ClosePanel();
    }
}
