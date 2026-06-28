using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class ReaderZone : MonoBehaviour
{
    [Header("Key / ID (karta)")]
    public string acceptedKeyId = "GateA";

    [Header("Odległość od gracza")]
    public float activationRange = 2f;

    [Header("Opóźnienie skanowania (hover z inventory)")]
    public float hoverActivationDelay = 1.5f;

    [Header("Opóźnienie skanowania (E z inventory)")]
    public float useActivationDelay = 0.3f;

    [Header("Opóźnienie skanowania po poprawnym PINie")]
    public float keypadActivationDelay = 0.6f;

    [Header("Światła statusu")]
    public GameObject statusIdleLight; // czerwone
    public GameObject statusPassLight; // zielone

    [Header("Pasek ładowania")]
    [Tooltip("Image z Fill Method = Horizontal, Fill Origin = Left")]
    public Image progressBarFill;

    [Header("Autoryzacja z ekwipunku (karta)")]
    public bool allowHoverFromInventory = true;
    public bool allowUseFromInventory = true;

    [Header("PIN z klawiatury")]
    [Tooltip("Czy ten czytnik ma klawiaturę numeryczną?")]
    public bool enableKeypad = false;

    [Tooltip("Wymagana sekwencja, np. *5321")]
    public string keypadCode = "*5321";

    [Tooltip("Czas po którym rozpoczęta sekwencja wpisywania wygasa, jeśli nic nie wciskasz")]
    public float keypadResetTime = 5f;

    [Header("Callbacki")]
    public System.Action onActivatedExternally;        // InventoryUI – schowanie ghosta
    public System.Action<ReaderZone> onAccessGranted;  // ReadersToDoor – odblokowanie drzwi

    // ==== runtime – wspólne dla wszystkich trybów ====
    bool isActive = false;     // zielony włączony
    bool hoverTriggered = false;
    Coroutine activeCoroutine;

    bool playerNear = false;

    // ==== runtime – keypad ====
    string _keypadBuffer = "";
    float _lastKeypadInputTime = -999f;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        SetLightStatus(false);
        SetProgress(0f);
    }

    // =========================================================
    //  PUBLIC API – KARTA
    // =========================================================

    /// <summary>Przeciąganie karty z Inventory na czytnik.</summary>
    public void TryActivateWithItem(InventoryItemInstance instance)
    {
        InternalTryActivate(instance, fromHover: true);
    }

    /// <summary>Użycie karty z inventory (Interact przy czytniku).</summary>
    public void TryActivateWithItemByUse(InventoryItemInstance instance)
    {
        InternalTryActivate(instance, fromHover: false);
    }

    void InternalTryActivate(InventoryItemInstance instance, bool fromHover)
    {
        if (instance?.data == null || !instance.data.isKeyItem)
            return;

        if (fromHover && !allowHoverFromInventory) return;
        if (!fromHover && !allowUseFromInventory) return;

        if (hoverTriggered || isActive) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > activationRange) return;

        if (instance.data.keyId != acceptedKeyId)
            return;

        float delay = fromHover ? hoverActivationDelay : useActivationDelay;
        StartAuthorization(delay);
    }

    // =========================================================
    //  PUBLIC API – KEYPAD
    // =========================================================

    /// <summary>Ustawiany z ReadersToDoor – wspólny PIN dla wielu paneli.</summary>
    public void SetExpectedCode(string code)
    {
        keypadCode = code;
    }

    /// <summary>Wywoływane z przycisków klawiatury (KeypadButton).</summary>
    public void OnKeypadKey(char key)
    {
        if (!enableKeypad) return;
        if (hoverTriggered || isActive) return; // w trakcie autoryzacji/po niej – ignorujemy

        // # kasuje aktualne wpisywanie
        if (key == '#')
        {
            _keypadBuffer = "";
            SetProgress(0f);
            return;
        }

        // * zawsze zaczyna nową sekwencję
        if (key == '*')
        {
            _keypadBuffer = "*";
            _lastKeypadInputTime = Time.time;
            return;
        }

        // cyfry ignorujemy, jeśli nie było gwiazdki
        if (string.IsNullOrEmpty(_keypadBuffer))
            return;

        _keypadBuffer += key;
        _lastKeypadInputTime = Time.time;

        // jeśli bufor nie jest prefiksem kodu – błąd -> reset
        if (string.IsNullOrEmpty(keypadCode) || !keypadCode.StartsWith(_keypadBuffer))
        {
            _keypadBuffer = "";
            SetProgress(0f);
            return;
        }

        // pełne dopasowanie
        if (_keypadBuffer == keypadCode)
        {
            _keypadBuffer = "";
            _lastKeypadInputTime = -999f;
            StartAuthorization(keypadActivationDelay);
        }
    }

    // =========================================================
    //  AUTORYZACJA – wspólna dla karty i PINu
    // =========================================================

    void StartAuthorization(float delay)
    {
        if (hoverTriggered || isActive) return;

        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        activeCoroutine = StartCoroutine(DelayedActivation(delay));
    }

    IEnumerator DelayedActivation(float delay)
    {
        hoverTriggered = true;
        SetProgress(0f);

        if (delay <= 0.0001f)
        {
            SetProgress(1f);
        }
        else
        {
            float t = 0f;
            while (t < delay)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / delay);
                SetProgress(norm);
                yield return null;
            }
        }

        hoverTriggered = false;
        activeCoroutine = null;

        // sukces
        SetLightStatus(true);
        onActivatedExternally?.Invoke();
        onAccessGranted?.Invoke(this);
    }

    public void ResetToIdle()
    {
        SetLightStatus(false);
        _keypadBuffer = "";
        _lastKeypadInputTime = -999f;

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        hoverTriggered = false;
        SetProgress(0f);
    }

    // Wywoływane z InventoryUI, gdy gracz "zabiera" kursor z czytnika
    // podczas przeciągania karty (bez dokańczenia skanowania).
    public void CancelHover()
    {   
        if (hoverTriggered && activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        hoverTriggered = false;
        // NIE zmieniamy świateł, tylko kasujemy pasek
        SetProgress(0f);
    }

    public bool IsGreenActive()
    {
        return isActive && statusPassLight != null && statusPassLight.activeSelf;
    }

    public void SetLightStatus(bool passed)
    {
        isActive = passed;
        if (statusIdleLight) statusIdleLight.SetActive(!passed);
        if (statusPassLight) statusPassLight.SetActive(passed);
    }

    // alias dla starego kodu
    public void SetAccessLight(bool granted) => SetLightStatus(granted);

    // =========================================================
    //  PASEK
    // =========================================================

    void SetProgress(float t)
    {
        if (!progressBarFill) return;

        float clamped = Mathf.Clamp01(t);
        progressBarFill.fillAmount = clamped;
        progressBarFill.color = EvaluateProgressColor(clamped);
    }

    Color EvaluateProgressColor(float t)
    {
        Color c0 = Color.red;
        Color c1 = new Color(1f, 0.5f, 0f); // pomarańcz
        Color c2 = Color.yellow;
        Color c3 = Color.green;

        if (t <= 0f) return c0;
        if (t >= 1f) return c3;

        if (t < 1f / 3f)
            return Color.Lerp(c0, c1, t * 3f);
        if (t < 2f / 3f)
            return Color.Lerp(c1, c2, (t - 1f / 3f) * 3f);

        return Color.Lerp(c2, c3, (t - 2f / 3f) * 3f);
    }

    // =========================================================
    //  TRIGGER + UŻYCIE KARTY Z INVENTORY (E)
    // =========================================================

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerNear = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerNear = false;
    }

    void Update()
    {
        // auto-reset wpisywanego PINu po czasie
        if (enableKeypad &&
            !string.IsNullOrEmpty(_keypadBuffer) &&
            Time.time - _lastKeypadInputTime > keypadResetTime)
        {
            _keypadBuffer = "";
            SetProgress(0f);
        }

        // karta z inventory + Interact
        if (allowUseFromInventory && playerNear)
        {
            var ih = PlayerInputHandler.Instance;
            if (ih != null && ih.InteractPressedThisFrame)
            {
                var inv = InventoryUI.Instance ?? Object.FindFirstObjectByType<InventoryUI>();
                if (inv == null) return;

                InventoryItemInstance keyItem = inv.FindKeyItem(acceptedKeyId);
                if (keyItem != null)
                    TryActivateWithItemByUse(keyItem);
            }
        }
    }
}
