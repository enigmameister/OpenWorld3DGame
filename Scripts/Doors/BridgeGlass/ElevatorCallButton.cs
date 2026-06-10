using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ElevatorCallButton : MonoBehaviour
{
    public SimpleElevator elevator;
    [Tooltip("Jeœli zaznaczone – przycisk wo³a windê na górê. Jeœli odznaczone – na dó³.")]
    public bool callToTop = false;

    [Header("Kolor przycisku (opcjonalnie)")]
    public Renderer buttonRenderer;
    public Color idleColor = Color.red;
    public Color activeColor = Color.green;

    private bool _playerInRange = false;
    private bool _isActive = false;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void Start()
    {
        if (buttonRenderer != null)
            buttonRenderer.material.color = idleColor;
    }

    private void Update()
    {
        if (!_playerInRange) return;

        var input = PlayerInputHandler.Instance;
        if (input == null) return;

        if (input.InteractPressedThisFrame)
        {
            if (elevator != null)
            {
                if (callToTop)
                    elevator.CallToTop();
                else
                    elevator.CallToBottom();

                _isActive = !_isActive;

                if (buttonRenderer != null)
                    buttonRenderer.material.color = _isActive ? activeColor : idleColor;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = false;
    }
}
