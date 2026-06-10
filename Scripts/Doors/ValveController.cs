using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class ValveController : MonoBehaviour
{
    private Quaternion _wheelStart;   // <— DODAJ

    [Header("Wizual")]
    public Transform wheel;                 // to zielone kółko / zawór
    [Tooltip("Ile maksymalnie obrócić (stopnie).")]
    public float maxTurnAngle = 45f;
    [Tooltip("Oś obrotu w lokalnych współrzędnych kółka.")]
    public Vector3 localAxis = Vector3.forward; // Z+ → „przekręcanie”

    [Header("Wejście / interakcja")]
    public string interactKey = "E";       // tylko do tooltipu
    public float holdSpeed = 1.0f;         // jak szybko rośnie 0..1 przy trzymaniu
    public float returnSpeed = 1.2f;       // jak szybko maleje gdy puścisz (sam zawór)

    [Header("Zasięg / Trigger")]
    public float interactRadius = 2.0f;    // dystans do gracza
    public LayerMask playerMask = ~0;

    [Header("Bramy sterowane tym zaworem")]
    public Tutorial2GateController gate;

    [Header("UI (opcjonalnie)")]
    public TextMeshProUGUI hintText;       // np. „Przytrzymaj [E] – zawór”
    public TextMeshProUGUI percentText;

    // runtime
    private float _valve01;     // 0..1 – stan zaworu (tylko wizual/feedback)
    private bool _playerInside;
    private Transform _player;

    void Awake()
    {
        if (wheel) _wheelStart = wheel.localRotation;   // <— ZAPAMIĘTAJ START
    }

    void Update()
    {
        UpdatePlayerNearby();

        bool held = IsInteractHeld();

        if (_playerInside && held)
        {
            _valve01 = Mathf.MoveTowards(_valve01, 1f, holdSpeed * Time.deltaTime);
            if (gate) gate.DriveWhileHeld(_valve01); // otwieraj bramę proporcjonalnie
        }
        else
        {
            // zawór sam „cofa” się powoli (nie na siłę do zera – to tylko wizual)
            _valve01 = Mathf.MoveTowards(_valve01, 0f, returnSpeed * Time.deltaTime);
            if (gate) gate.Release();                // pozwól bramie opaść swoim tempem
        }

        ApplyWheelRotation();
        UpdateUI();
    }

    private void UpdatePlayerNearby()
    {
        if (!_player) // leniwe wyszukiwanie gracza po tagu
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            _player = p ? p.transform : null;
        }
        if (!_player) { _playerInside = false; return; }

        _playerInside = (Vector3.Distance(transform.position, _player.position) <= interactRadius);
    }

    private bool IsInteractHeld()
    {
        // Jeśli masz PlayerInputHandler z projektem – priorytet:
        // (zabezpieczenie, jeśli nie istnieje – fallback na klawisz)
        if (PlayerInputHandler.Instance != null)
            return PlayerInputHandler.Instance.InteractHeld || PlayerInputHandler.Instance.InteractPressed;
        return Input.GetKey(KeyCode.E);
    }

    private void ApplyWheelRotation()
    {
        if (!wheel) return;

        float angle = _valve01 * maxTurnAngle;

        // NIE używaj Quaternion.identity – trzymaj się startowej rotacji
        Vector3 axis = localAxis.normalized;
        wheel.localRotation = _wheelStart * Quaternion.AngleAxis(angle, axis);
    }

    private void UpdateUI()
    {
        if (hintText)
            hintText.text = _playerInside ? $"Przytrzymaj [{interactKey}] – odkręć zawór" : "";

        if (percentText)
            percentText.text = $"{Mathf.RoundToInt(_valve01 * 100f)}%";
    }
}
