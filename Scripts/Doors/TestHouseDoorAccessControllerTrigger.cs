using UnityEngine;

public class TestHouseDoorAccessControllerTrigger : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private TestHouseDoorAccessController door;

    [Header("Input")]
    [SerializeField] private bool requireInteractKey = true;
    [SerializeField] private KeyCode fallbackKey = KeyCode.E;

    [Header("Optional UI")]
    [SerializeField] private GameObject promptRoot;

    private bool playerInside;

    private void Awake()
    {
        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    private void Update()
    {
        if (!playerInside)
            return;

        if (!requireInteractKey)
        {
            TryUse();
            return;
        }

        bool pressed = false;

        if (PlayerInputHandler.Instance != null)
            pressed = PlayerInputHandler.Instance.InteractPressed;
        else
            pressed = Input.GetKeyDown(fallbackKey);

        if (pressed)
            TryUse();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = true;

        if (promptRoot != null)
            promptRoot.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = false;

        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    private void TryUse()
    {
        if (door == null)
        {
            Debug.LogWarning($"[DoorPanelTrigger] {name}: Door reference missing.");
            return;
        }

        door.TryUsePanel();
    }
}