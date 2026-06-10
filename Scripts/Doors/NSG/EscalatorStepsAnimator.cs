using UnityEngine;

public class EscalatorStepsAnimator : MonoBehaviour
{
    public Transform[] steps;     // wszystkie kostki schodów
    public Transform bottomPoint; // dó³ taœmy
    public Transform topPoint;    // góra taœmy

    [Tooltip("Docelowa prêdkoœæ taœmy (ustawiana przez Escalator).")]
    public float speed = 1f;      // dodatnia = bottom->top, ujemna odwrotnie

    [Tooltip("Jak szybko schody rozpêdzaj¹ siê / hamuj¹ (w jednostkach prêdkoœci na sekundê).")]
    public float speedChangeRate = 5f;  // im wiêksze, tym gwa³towniej

    private float[] _t;
    private float _length;
    private Vector3 _dir;

    // faktycznie u¿ywana, wyg³adzona prêdkoœæ
    private float _currentSpeed;

    void Start()
    {
        if (steps == null || steps.Length == 0 || bottomPoint == null || topPoint == null)
        {
            Debug.LogWarning("EscalatorStepsAnimator: brak referencji.");
            enabled = false;
            return;
        }

        _dir = (topPoint.position - bottomPoint.position);
        _length = _dir.magnitude;
        _dir.Normalize();

        _t = new float[steps.Length];

        for (int i = 0; i < steps.Length; i++)
        {
            Vector3 fromBottom = steps[i].position - bottomPoint.position;
            float d = Vector3.Dot(fromBottom, _dir);
            _t[i] = Mathf.Repeat(d / _length, 1f);
        }

        // startowo _currentSpeed = speed (ale Escalator w Start i tak ustawi speed=0,
        // wiêc bardzo szybko zjedziemy do zera)
        _currentSpeed = speed;
    }

    void Update()
    {
        // p³ynne dojœcie z _currentSpeed do speed
        _currentSpeed = Mathf.MoveTowards(
            _currentSpeed,
            speed,
            speedChangeRate * Time.deltaTime
        );

        // jeœli praktycznie stoimy – nie ruszaj schodów (¿eby nie p³ywa³y przy 0.0001f)
        if (Mathf.Abs(_currentSpeed) < 0.0001f)
            return;

        float delta = _currentSpeed * Time.deltaTime / _length;

        for (int i = 0; i < steps.Length; i++)
        {
            _t[i] = Mathf.Repeat(_t[i] + delta, 1f);
            steps[i].position = bottomPoint.position + _dir * (_t[i] * _length);
        }
    }
}
