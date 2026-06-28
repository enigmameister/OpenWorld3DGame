using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GlassRevealButton : MonoBehaviour
{
    public GlassGameManager glassGameManager;
    public Renderer buttonRenderer;
    public Color idleColor = Color.red;
    public Color activeColor = Color.green;

    private bool _playerInRange = false;
    private bool _revealed = false;

    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;

        if (buttonRenderer != null)
            buttonRenderer.material.color = idleColor;
    }

    private void Update()
    {
        if (!_playerInRange) return;
        if (PlayerInputHandler.Instance == null) return;

        if (PlayerInputHandler.Instance.InteractPressedThisFrame)
        {
            _revealed = !_revealed;

            if (glassGameManager != null)
                glassGameManager.RevealAll(_revealed);

            if (buttonRenderer != null)
                buttonRenderer.material.color = _revealed ? activeColor : idleColor;
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

