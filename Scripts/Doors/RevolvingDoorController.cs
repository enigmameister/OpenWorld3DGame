using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class RevolvingDoorController : MonoBehaviour
{
    [Header("Napêd")]
    public float rotationSpeed = 30f;
    public float accelTime = 0.6f;
    public float decelTime = 0.8f;

    [Header("Auto-stop po czasie bez ruchu")]
    public float idleDelay = 1f;

    float _target;       // 0..1
    float _current;      // 0..1
    float _idleTimer = 0f;

    [Header("Czujnik")]
    public Collider triggerZone;          // przypnij Sensor (IsTrigger)
    public string requiredTag = "Player"; // kogo uznajemy

    float _currentSpeed;      // aktualna prêdkoœæ (deg/s)
    int _insideCount = 0;     // ile obiektów jest w triggerze
    float _idleUntil = -1f;

    void Reset()
    {
        // spróbuj znaleŸæ trigger w dzieciach:
        if (!triggerZone) triggerZone = GetComponentInChildren<Collider>();
    }

    void Update()
    {
        // miêkkie wyhamowanie po idle
        if (_idleTimer > 0f)
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0f) _target = 0f;
        }

        // lerp prêdkoœci: start szybciej, stop wolniej (albo jak wolisz)
        float tau = (_target > _current) ? accelTime : decelTime;
        if (tau <= 0.0001f) _current = _target;
        else _current = Mathf.MoveTowards(_current, _target, Time.deltaTime / tau);

        // obrót wa³u (ten skrypt ma byæ na „Wa³ œrodkowy”)
        transform.Rotate(Vector3.up, rotationSpeed * _current * Time.deltaTime, Space.Self);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!triggerZone || other == triggerZone) return;
        if (string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag))
        {
            _insideCount++;
            _idleUntil = -1f; // natychmiast rusz
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!triggerZone || other == triggerZone) return;
        if (string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag))
        {
            _insideCount = Mathf.Max(0, _insideCount - 1);
            if (_insideCount == 0) _idleUntil = Time.time + idleDelay; // po chwili stop
        }
    }


    public void SetPresence(bool hasSomeone)
    {
        if (hasSomeone)
        {
            _target = 1f;
            _idleTimer = 0f;
        }
        else
        {
            // odliczanie do zatrzymania – jeœli nikt nie ma w triggerze
            _idleTimer = idleDelay;
        }
    }
    // dla wygody – jeœli sensor jest dzieckiem tego obiektu:
    void OnEnable()
    {
        if (triggerZone && triggerZone.transform.IsChildOf(transform))
        {
            var sensorMB = triggerZone.gameObject.GetComponent<RevolvingDoorSensor>();
            if (!sensorMB) triggerZone.gameObject.AddComponent<RevolvingDoorSensor>().Init(this);
        }
    }

    // wewnêtrzna pomoc: pozwala trzymaæ zdarzenia na samym sensorze
    public void SensorEnter(Collider c) => OnTriggerEnter(c);
    public void SensorExit(Collider c) => OnTriggerExit(c);
}

// Ma³y „forwarder” zdarzeñ z obiektu Sensor:
public class RevolvingDoorSensor : MonoBehaviour
{
    RevolvingDoorController _ctrl;

    public void Init(RevolvingDoorController ctrl) { _ctrl = ctrl; }

    void OnTriggerEnter(Collider other) { _ctrl?.SensorEnter(other); }
    void OnTriggerExit(Collider other) { _ctrl?.SensorExit(other); }
}
