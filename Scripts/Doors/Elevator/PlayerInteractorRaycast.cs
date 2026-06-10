using UnityEngine;

public class PlayerInteractorRaycast : MonoBehaviour
{
    [Header("Wejœcie (fallback jeœli nie ma PlayerInputHandler)")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Raycast")]
    public Camera cam;                 // kamera gracza
    public LayerMask interactMask;     // warstwa np. "Interactable"
    public float maxDistance = 3.0f;

    [Header("Debug")]
    public bool showDebug = true;

    IPressable _current;
    ElevatorButton _currentElevBtn;    // do hoveru windy
    KeypadButtonNSG _currentKeypad;    // do hoveru klawiatury
    float _cooldown;

    void Reset()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (!cam) return;

        // cooldown anty-spam
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;

        var ray = new Ray(cam.transform.position, cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            if (showDebug)
                Debug.DrawLine(ray.origin, hit.point, Color.cyan);

            // szukamy obiektu z IPressable
            var pressable = hit.collider.GetComponentInParent<IPressable>();

            if (pressable != _current)
            {
                // zdejmij hover ze starego
                if (_currentElevBtn) _currentElevBtn.SetHover(false);
                if (_currentKeypad) _currentKeypad.SetHover(false);

                _current = pressable;
                _currentElevBtn = hit.collider.GetComponentInParent<ElevatorButton>();
                _currentKeypad = hit.collider.GetComponentInParent<KeypadButtonNSG>();

                if (_currentElevBtn) _currentElevBtn.SetHover(true);
                if (_currentKeypad) _currentKeypad.SetHover(true);
            }

            // naciœniêcie klawisza "interact"
            bool interactPressed =
                PlayerInputHandler.Instance?.InteractPressedThisFrame
                ?? Input.GetKeyDown(interactKey);

            if (_current != null && _cooldown <= 0f && interactPressed)
            {
                _current.Press();
                _cooldown = 0.2f;
            }
        }
        else
        {
            // nic nie celujemy – zdejmij hover
            if (_currentElevBtn) _currentElevBtn.SetHover(false);
            if (_currentKeypad) _currentKeypad.SetHover(false);

            _current = null;
            _currentElevBtn = null;
            _currentKeypad = null;
        }
    }
}
