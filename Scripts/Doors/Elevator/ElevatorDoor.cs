using System;
using System.Collections;
using UnityEngine;

public class ElevatorDoor : MonoBehaviour
{
    [Header("Obiekt referencyjny (lokalnie)")]
    public Transform frame;

    [Header("Skrzydła")]
    public Transform leftWing;
    public Transform rightWing;

    public enum Axis { X, Y, Z, Custom }
    public Axis axis = Axis.X;
    public Vector3 customLocalAxis = Vector3.right;

    [Header("Parametry ruchu")]
    public float distance = 0.8f;
    public float openSpeed = 2f;
    public float closeSpeed = 2f;
    public bool startOpen = false;
    public float stopEps = 0.005f;
    public bool lockClosedWhileMoving = false;

    [Header("Fotokomórka")]
    public Collider obstructionZone;
    public LayerMask obstructionMask = ~0;
    [Tooltip("Czas po ostatnim wykryciu zanim drzwi znów mogą się zamknąć.")]
    public float obstructionHoldTime = 0.8f;  // sekund
    float _obstructUntil = 0f;

    // runtime
    float _t, _target;                               // 0..1
    Vector3 _axisLocal;
    Vector3 _leftStart, _rightStart;

    public bool IsFullyOpen => Mathf.Abs(_t - 1f) < stopEps;
    public bool IsFullyClosed => _t < stopEps;
    public bool Obstructed => IsObstructed();

    void Awake()
    {
        if (!frame) frame = transform;
        _axisLocal = GetAxisVector();

        if (leftWing) _leftStart = leftWing.localPosition;
        if (rightWing) _rightStart = rightWing.localPosition;

        _t = _target = startOpen ? 1f : 0f;
        Apply();
    }

    void Update()
    {
        // auto–reopen podczas zamykania (ALE nie podczas blokady jazdy)
        if (!lockClosedWhileMoving && _target < 1f && obstructionZone && IsObstructed())
            _target = 1f;

        float sp = (_target > _t ? openSpeed : closeSpeed);
        float step = (distance <= 0.0001f) ? 1f : (sp / distance) * Time.deltaTime;
        _t = Mathf.MoveTowards(_t, _target, step);

        Apply();
    }

    public void SetLocked(bool locked)
    {
        lockClosedWhileMoving = locked;
        if (locked) _target = 0f; // wymuś zamknięcie
    }

    // ---------- API natychmiastowe ----------
    public void Open(bool immediate = false)
    {
        _target = 1f;
        if (immediate) { _t = 1f; Apply(); }
    }

    public void Close(bool immediate = false)
    {
        if (!immediate && Obstructed) { _target = 1f; return; }
        _target = 0f;
        if (immediate) { _t = 0f; Apply(); }
    }

    // ---------- API korutynowe ----------
    public IEnumerator OpenRoutine()
    {
        Open(false);
        while (!IsFullyOpen) yield return null;
    }

    /// <summary>
    /// Zamyka drzwi. Jeśli keepOpenWhile() zwraca true – utrzymuje otwarte.
    /// </summary>
    public IEnumerator CloseRoutine(Func<bool> keepOpenWhile = null)
    {
        // jeżeli ktoś podał warunek „trzymaj otwarte póki…”
        if (keepOpenWhile != null)
        {
            while (keepOpenWhile())
            {
                Open(false);
                yield return null;
            }
        }

        Close(false);
        while (!IsFullyClosed)
        {
            // w trakcie zamykania – jeśli pojawi się przeszkoda, otwórz i poczekaj
            if (Obstructed)
            {
                Open(false);
                yield return null;
                continue;
            }
            yield return null;
        }
    }

    // ---------- pomocnicze ----------
    Vector3 GetAxisVector()
    {
        switch (axis)
        {
            case Axis.X: return Vector3.right;
            case Axis.Y: return Vector3.up;
            case Axis.Z: return Vector3.forward;
            default: return customLocalAxis.normalized;
        }
    }

    void Apply()
    {
        if (leftWing)
            leftWing.localPosition = _leftStart - _axisLocal * (distance * _t);
        if (rightWing)
            rightWing.localPosition = _rightStart + _axisLocal * (distance * _t);
    }

    // public – bo kontroler może chcieć sprawdzać bezpośrednio
    public bool IsObstructed()
    {
        if (!obstructionZone) return false;

        var b = obstructionZone.bounds;
        var hits = Physics.OverlapBox(
            b.center, b.extents, obstructionZone.transform.rotation,
            obstructionMask, QueryTriggerInteraction.Ignore
        );

        bool found = false;
        foreach (var h in hits)
        {
            if (h.transform == transform || h.transform.IsChildOf(transform))
                continue;
            found = true;
            break;
        }

        if (found)
            _obstructUntil = Time.time + obstructionHoldTime;

        // prawdziwy „obstructed” tylko jeśli ostatnie wykrycie było niedawno
        return Time.time < _obstructUntil;
    }
}
