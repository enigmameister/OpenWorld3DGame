using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Escalator : MonoBehaviour
{
    [Header("Kierunek i prêdkoœæ")]
    public Vector3 localDirection = new Vector3(0f, 1f, 1f);
    public float speed = 2f;
    public Transform directionReference;

    [Header("Wejœcie jak w real life")]
    public Transform entryPoint;
    public float entryRadius = 0.75f;

    [Header("Animacja stopni")]
    public EscalatorStepsAnimator stepsAnimator;

    private CharacterController _rider;
    private float _stepsBaseSpeed;   // zapamiêtamy oryginaln¹ prêdkoœæ animacji

    private void Start()
    {
        // jeœli mamy animator stopni – zapamiêtaj prêdkoœæ i na starcie zatrzymaj
        if (stepsAnimator != null)
        {
            _stepsBaseSpeed = stepsAnimator.speed;
            stepsAnimator.speed = 0f;           // na starcie schody stoj¹
        }
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Update()
    {
        if (_rider == null) return;

        Transform refTr = directionReference ? directionReference : transform;
        Vector3 dirWorld = refTr.TransformDirection(localDirection.normalized);

        _rider.Move(dirWorld * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var cc = other.GetComponent<CharacterController>()
                 ?? other.GetComponentInParent<CharacterController>();

        if (cc == null) return;

        // sprawdzamy, czy gracz wchodzi w pobli¿u EntryPoint (dó³ schodów)
        if (entryPoint != null)
        {
            Vector3 playerPos = cc.transform.position;
            float dist = Vector3.Distance(playerPos, entryPoint.position);

            if (dist > entryRadius)
            {
                // wszed³ z góry / ze œrodka -> nie uruchamiamy schodów
                return;
            }
        }

        _rider = cc;

        // W£¥CZ animacjê stopni
        if (stepsAnimator != null)
            stepsAnimator.speed = _stepsBaseSpeed;
    }

    private void OnTriggerExit(Collider other)
    {
        if (_rider == null) return;
        if (!other.CompareTag("Player")) return;

        var cc = other.GetComponent<CharacterController>()
                 ?? other.GetComponentInParent<CharacterController>();

        if (cc == _rider)
        {
            _rider = null;

            // WY£¥CZ animacjê stopni
            if (stepsAnimator != null)
                stepsAnimator.speed = 0f;
        }
    }
}
