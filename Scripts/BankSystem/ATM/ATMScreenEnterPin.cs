using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ATMScreenEnterPin : MonoBehaviour
{
    [Header("UI")]
    public Image[] dots;        // 4 elementy: D0..D3
    public Image[] underlines;  // 4 elementy: U0..U3
    public TextMeshProUGUI attemptsText;

    [Header("Blink")]
    public float blinkSpeed = 6f;
    [Range(0f, 1f)] public float underlineMinA = 0.25f;
    [Range(0f, 1f)] public float underlineMaxA = 1f;

    [Header("Rules")]
    public int pinLength = 4;

    // stan
    private string _typed = "";
    private int _attemptsLeft = 3;
    private bool _acceptInput;

    private InventoryItemInstance _card;
    private ATMUIController _ui;

    // wywo³ania do kontrolera (pod³¹czysz póŸniej)
    public Action<string> onPinSubmit;   // gdy Enter i mamy 4 cyfry
    public Action onCancel;              // ESC/back z ekranu PIN

    public void Open(int attemptsLeft, bool acceptInput = true)
    {
        _attemptsLeft = Mathf.Max(0, attemptsLeft);
        _typed = "";
        _acceptInput = acceptInput;

        RefreshUI();
    }

    public void Open(InventoryItemInstance selectedCard, ATMUIController ui, int attemptsLeft = 3, bool acceptInput = true)
    {
        _card = selectedCard;
        _ui = ui;
        Open(attemptsLeft, acceptInput); // wywo³uje Twoje istniej¹ce Open(int,...)
    }

    public void SetAttempts(int attemptsLeft)
    {
        _attemptsLeft = Mathf.Max(0, attemptsLeft);
        RefreshUI();
    }

    void OnEnable()
    {
        // na wszelki wypadek
        if (dots == null || dots.Length == 0) { }
        _typed = "";
        RefreshUI();
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        // miganie aktywnej kreski (slot = aktualna d³ugoœæ wpisu)
        BlinkUnderline();

        if (!_acceptInput) return;

        // ESC = anuluj/wyjœcie (logikê przejœæ ogarnie ATMUIController)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            onCancel?.Invoke();
            return;
        }

        // Backspace
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (_typed.Length > 0)
            {
                _typed = _typed.Substring(0, _typed.Length - 1);
                RefreshUI();
            }
            return;
        }

        // Enter (tylko gdy komplet)
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (_typed.Length == pinLength)
                onPinSubmit?.Invoke(_typed);

            return;
        }

        // Cyfry: bierzemy z inputString (³apie 0-9 z klawiatury)
        string s = Input.inputString;
        if (string.IsNullOrEmpty(s)) return;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= '0' && c <= '9')
            {
                if (_typed.Length < pinLength)
                {
                    _typed += c;
                    RefreshUI();
                }
            }
        }
    }

    private void RefreshUI()
    {
        // attempts
        if (attemptsText != null)
            attemptsText.text = $"ATTEMPTS LEFT: {_attemptsLeft}";

        // dots on/off
        for (int i = 0; i < pinLength; i++)
        {   
            if (dots != null && i < dots.Length && dots[i] != null)
            {
                // prosto: enabled
                dots[i].enabled = i < _typed.Length;
            }

            if (underlines != null && i < underlines.Length && underlines[i] != null)
            {
                // wszystkie kreski widoczne, alpha ustawiana w BlinkUnderline()
                underlines[i].enabled = true;

                var c = underlines[i].color;
                c.a = underlineMinA;
                underlines[i].color = c;
            }
        }

        BlinkUnderline(); // od razu odœwie¿ “aktywny” slot
    }

    private void BlinkUnderline()
    {
        if (underlines == null || underlines.Length == 0) return;

        int activeIndex = Mathf.Clamp(_typed.Length, 0, pinLength - 1);

        float a = underlineMinA + Mathf.Abs(Mathf.Sin(Time.unscaledTime * blinkSpeed)) * (underlineMaxA - underlineMinA);

        for (int i = 0; i < underlines.Length; i++)
        {
            var u = underlines[i];
            if (!u) continue;

            var c = u.color;
            c.a = (i == activeIndex && _typed.Length < pinLength) ? a : underlineMinA;
            u.color = c;
        }
    }

    public void ClearTyped()
    {
        _typed = "";
        RefreshUI();
    }

    public void SetAcceptInput(bool v)
    {
        _acceptInput = v;
    }

    public bool IsAcceptingInput()
    {
        return _acceptInput;
    }

    public string GetTyped() => _typed;
}
