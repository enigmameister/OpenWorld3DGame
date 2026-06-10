using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GateButton : MonoBehaviour
{
    public Transform gate;          // Gate_2
    public float moveDistance = 6f; // ile jednostek wzd³u¿ -X
    public float moveSpeed = 3f;

    public Renderer buttonRenderer;
    public Color idleColor = Color.red;
    public Color openColor = Color.green;  // otwieranie
    public Color closeColor = Color.blue;  // zamykanie

    private bool _playerInRange = false;
    private bool _isMoving = false;
    private bool _toOpen = true;       // pierwszy ruch = otwarcie
    private Vector3 _closedPos;
    private Vector3 _openPos;

    void Start()
    {
        if (!gate)
        {
            Debug.LogWarning("GateButton: brak referencji do bramy!");
            enabled = false;
            return;
        }

        _closedPos = gate.position;
        _openPos = _closedPos + Vector3.left * moveDistance;

        if (buttonRenderer != null)
            buttonRenderer.material.color = idleColor;

        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        if (!_playerInRange || _isMoving) return;
        if (PlayerInputHandler.Instance == null) return;

        if (PlayerInputHandler.Instance.InteractPressedThisFrame)
        {
            StartCoroutine(MoveGate());
        }
    }

    IEnumerator MoveGate()
    {
        _isMoving = true;

        if (buttonRenderer != null)
            buttonRenderer.material.color = _toOpen ? openColor : closeColor;

        Vector3 from = _toOpen ? _closedPos : _openPos;
        Vector3 to = _toOpen ? _openPos : _closedPos;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            gate.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        gate.position = to;
        _toOpen = !_toOpen;          // nastêpnym razem odwrotny kierunek
        _isMoving = false;

        if (buttonRenderer != null)
            buttonRenderer.material.color = idleColor;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = false;
    }
}
