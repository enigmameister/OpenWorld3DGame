using UnityEngine;

public class ATMInteractZone : MonoBehaviour
{
    [SerializeField] private ATMUIController ui;

    [Header("Cooldown")]
    [SerializeField] private float reopenCooldown = 5f;

    private bool _playerInside;
    private float _nextAllowedOpenTime;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }
    void Update()
    {
        if (!ui) return;

        bool interactPressed = PlayerInputHandler.Instance != null && PlayerInputHandler.Instance.InteractPressed;

        // otwieranie tylko w triggerze, gdy cooldown min¹³
        if (!_playerInside) return;
        if (ui.IsOpen) return;
        if (Time.time < _nextAllowedOpenTime) return;

        if (interactPressed)
        {
            ui.Open();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerStats>() != null)
            _playerInside = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerStats>() != null)
        {
            _playerInside = false;

            // opcjonalnie: wyjœcie z triggera zamyka ATM
            if (ui != null && ui.IsOpen)
            {
                ui.Close();
                _nextAllowedOpenTime = Time.time + reopenCooldown;
            }
        }
    }
}
