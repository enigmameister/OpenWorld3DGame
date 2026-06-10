using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MovingPlatformCarrier : MonoBehaviour
{
    [Tooltip("Transform platformy, który siê porusza (np. HangCylinder albo Root pod nim).")]
    public Transform platformRoot;

    private Vector3 _lastPlatformPos;
    private readonly HashSet<Transform> _riders = new HashSet<Transform>();

    private void Reset()
    {
        platformRoot = transform;
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;    // ten collider ma byæ TRIGGEREM
    }

    private void Start()
    {
        if (platformRoot == null) platformRoot = transform;
        _lastPlatformPos = platformRoot.position;
    }

    private void LateUpdate()
    {
        if (_riders.Count == 0)
        {
            _lastPlatformPos = platformRoot.position;
            return;
        }

        Vector3 delta = platformRoot.position - _lastPlatformPos;
        _lastPlatformPos = platformRoot.position;

        // nie ruszamy w osi Y, ¿eby nie wp³ywaæ na skakanie
        delta.y = 0f;

        if (delta == Vector3.zero) return;

        foreach (var t in _riders)
        {
            if (t == null) continue;
            t.position += delta;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // bierzemy root gracza (tam gdzie jest PlayerMovement)
        var pm = other.GetComponentInParent<PlayerMovement>();
        if (pm != null) _riders.Add(pm.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var pm = other.GetComponentInParent<PlayerMovement>();
        if (pm != null && _riders.Contains(pm.transform))
            _riders.Remove(pm.transform);
    }
}
