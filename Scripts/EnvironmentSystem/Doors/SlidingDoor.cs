using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SlidingDoor : MonoBehaviour
{
    public enum SlideAxis { XPlus, XMinus, YPlus, YMinus, ZPlus, ZMinus, Custom }

    [Header("Ruch drzwi")]
    public Transform moving;                 // obiekt który się przesuwa (np. "Doors")
    public SlideAxis axis = SlideAxis.XPlus; // kierunek przesunięcia
    public Vector3 customLocalDir = Vector3.right;
    [Range(0.01f, 10f)] public float distance = 1.5f;

    [Header("Szybkość")]
    public float openSpeed = 3f;
    public float closeSpeed = 2.5f;
    public float autoCloseDelay = 1.0f;

    [Header("Kierunek")]
    public bool invertSide;   // gdy drzwi jadą w złą stronę – zaznacz

    // runtime
    Vector3 _startLocal;
    Vector3 _targetLocal;
    float _t;
    int _insideCount;
    float _idleUntil = -1f;

    void Awake()
    {
        if (!moving) moving = transform;
        _startLocal = moving.localPosition;
        _targetLocal = _startLocal;

        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void Update()
    {
        // automatyczne zamknięcie po czasie
        if (_insideCount == 0 && _idleUntil > 0f && Time.time >= _idleUntil)
            _targetLocal = _startLocal;

        // płynny ruch
        moving.localPosition = Vector3.MoveTowards(
            moving.localPosition, _targetLocal,
            ((_targetLocal == _startLocal) ? closeSpeed : openSpeed) * Time.deltaTime
        );
    }
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _insideCount++;

        // oś ruchu: lokalna -> świat
        Vector3 dirLocal = AxisDir().normalized;
        Vector3 dirWorld = moving.TransformDirection(dirLocal);

        // pozycja gracza względem środka drzwi, rzut na oś ruchu
        float proj = Vector3.Dot(other.transform.position - moving.position, dirWorld);
        float sign = Mathf.Sign(proj);                 // + z jednej strony, – z drugiej
        if (invertSide) sign = -sign;                  // <— przełącznik

        _targetLocal = _startLocal + dirLocal * distance;
        _idleUntil = -1f;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _insideCount = Mathf.Max(0, _insideCount - 1);
        if (_insideCount == 0)
            _idleUntil = Time.time + autoCloseDelay;
    }

    Vector3 AxisDir()
    {
        switch (axis)
        {
            case SlideAxis.XPlus: return Vector3.right;
            case SlideAxis.XMinus: return Vector3.left;
            case SlideAxis.YPlus: return Vector3.up;
            case SlideAxis.YMinus: return Vector3.down;
            case SlideAxis.ZPlus: return Vector3.forward;
            case SlideAxis.ZMinus: return Vector3.back;
            case SlideAxis.Custom: return customLocalDir.normalized;
        }
        return Vector3.right;
    }
}
