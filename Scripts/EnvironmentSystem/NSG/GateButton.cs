using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GateButton : MonoBehaviour, IPressable
{
    public Transform gate;
    public float moveDistance = 6f;
    public float moveSpeed = 3f;

    public Renderer buttonRenderer;
    public Color idleColor = Color.red;
    public Color openColor = Color.green;
    public Color closeColor = Color.blue;

    [SerializeField] private string label = "U¿yj przycisku bramy";

    private bool _isMoving = false;
    private bool _toOpen = true;
    private Vector3 _closedPos;
    private Vector3 _openPos;

    public string Label => _isMoving ? "Brama w ruchu" : label;

    void Start()
    {
        if (!gate)
        {
            Debug.LogWarning("GateButton: brak referencji do bramy!", this);
            enabled = false;
            return;
        }

        _closedPos = gate.position;
        _openPos = _closedPos + Vector3.left * moveDistance;

        if (buttonRenderer != null)
            buttonRenderer.material.color = idleColor;
    }

    public void Press()
    {
        if (_isMoving)
            return;

        if (gate == null)
            return;

        StartCoroutine(MoveGate());
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
        _toOpen = !_toOpen;
        _isMoving = false;

        if (buttonRenderer != null)
            buttonRenderer.material.color = idleColor;
    }
}