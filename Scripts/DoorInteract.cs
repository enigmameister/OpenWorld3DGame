using UnityEngine;

public class DoorInteract : MonoBehaviour
{
    [Header("Co obracamy (pivot w osi zawiasu)")]
    public Transform door;                 // np. "Zawiasy"
    public Vector3 localAxis = Vector3.up; // zwykle (0,1,0)
    public float openAngle = 90f;
    public float openSpeedDeg = 180f;
    public float closeSpeedDeg = 140f;
    public float autoCloseDelay = 1.5f;

    [Header("Strefy (triggery)")]
    public DoorUseZoneRelay frontZone;     // trigger od „frontu”
    public DoorUseZoneRelay backZone;      // trigger od „tyłu”
    public string requiredTag = "Player";
    public bool pressToOpen = false;       // jeśli true – wymagaj wciśnięcia klawisza
    public KeyCode interactKey = KeyCode.E;

    [Header("Zabezpieczenie")]
    [Tooltip("Jeśli zaznaczone – drzwi mogą być blokowane (secured).")]
    public bool hasSecurity = true;        // <--- typ drzwi: z zabezpieczeniem czy bez
    [Tooltip("Aktualny stan blokady. Gdy true i hasSecurity=true – drzwi się nie otworzą.")]
    public bool secured = false;

    // runtime
    int _frontCount, _backCount;
    Quaternion _startLocalRot;
    float _currentAngle, _targetAngle;
    float _autoCloseAt = -1f;

    bool _wasOpen;                         // do wykrywania otwarcia/zamknięcia

    public bool IsOpen => Mathf.Abs(_currentAngle) > 1f;
    public bool IsLocked() => hasSecurity && secured;

    void Awake()
    {
        if (!door) door = transform;
        _startLocalRot = door.localRotation;

        if (frontZone) frontZone.Bind(this, isFront: true, requiredTag);
        if (backZone) backZone.Bind(this, isFront: false, requiredTag);
    }

    void Update()
    {
        bool someoneFront = _frontCount > 0;
        bool someoneBack = _backCount > 0;

        // --- logika otwierania / zamykania ---

        // jeśli drzwi są „zabezpieczalne” i aktualnie zablokowane -> nie ustawiaj nowych targetów,
        // ale wciąż dociągnij je do pozycji zamkniętej
        if (IsLocked())
        {
            if (_currentAngle != 0f)
                _currentAngle = Mathf.MoveTowards(_currentAngle, 0f, closeSpeedDeg * Time.deltaTime);

            ApplyRotation();
            return;
        }

        if (!pressToOpen)
        {
            if (someoneFront && !someoneBack) SetTarget(-openAngle);
            else if (someoneBack && !someoneFront) SetTarget(+openAngle);
            else if (someoneFront && someoneBack) SetTarget(0f);
            else if (_autoCloseAt < 0f)
                _autoCloseAt = Time.time + autoCloseDelay;
        }
        else
        {
            if (Input.GetKeyDown(interactKey))
            {
                if (someoneFront && !someoneBack) SetTarget(-openAngle);
                else if (someoneBack && !someoneFront) SetTarget(+openAngle);
            }

            if (!someoneFront && !someoneBack && _autoCloseAt < 0f)
                _autoCloseAt = Time.time + autoCloseDelay;
        }

        if (_autoCloseAt > 0f && Time.time >= _autoCloseAt)
        {
            SetTarget(0f);
            _autoCloseAt = -1f;
        }

        bool opening = Mathf.Abs(_targetAngle) > 0.01f &&
                       Mathf.Abs(_currentAngle - _targetAngle) > 0.1f;

        float speed = opening ? openSpeedDeg : closeSpeedDeg;
        _currentAngle = Mathf.MoveTowards(_currentAngle, _targetAngle, speed * Time.deltaTime);

        ApplyRotation();
    }

    void ApplyRotation()
    {
        door.localRotation =
            _startLocalRot * Quaternion.AngleAxis(_currentAngle, localAxis.normalized);
    }

    void SetTarget(float angle)
    {
        _targetAngle = angle;
        _autoCloseAt = -1f;
    }

    // ==== API wołane przez relaye ====
    public void ZoneEnter(bool isFront)
    {
        if (IsLocked()) return;
        if (isFront) _frontCount++;
        else _backCount++;
        _autoCloseAt = -1f;
    }

    public void ZoneExit(bool isFront)
    {
        if (isFront) _frontCount = Mathf.Max(0, _frontCount - 1);
        else _backCount = Mathf.Max(0, _backCount - 1);
        _autoCloseAt = -1f;
    }

    // === Publiczne metody dla czytnika itd. ===

    public void LockDoor()
    {
        if (!hasSecurity) return;
        secured = true;
        // upewnij się że cel to pozycja zamknięta
        SetTarget(0f);
    }

    public void UnlockDoor()
    {
        if (!hasSecurity) return;
        secured = false;
    }
}
