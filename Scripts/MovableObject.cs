using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovableObject : MonoBehaviour
{
    private Rigidbody rb;

    [Header("HL2 – przenoszenie (LPM)")]
    public bool isCarried = false;
    public float carryDistance = 1.2f;

    public float carrySpring = 1200f;   // było 900
    public float carryDamping = 100f;    // było 90
    public float maxAccel = 25f;    // było 40 (mniej szarpie)

    [Header("HL2 – stabilizacja rotacji podczas carry")]
    public bool stabilizeWhileCarried = true;
    public float rotSpring = 30f;      // siła „sprężyny” rotacji
    public float rotDamping = 6f;      // tłumienie rotacji
    public float maxAngAccel = 50f;    // limit „szarpnięcia” kątowego


    private Transform carryTarget;           // kotwica (dziecko kamery), NIE zmieniamy parenta obiektu

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;

        // POZWÓL NA ROTACJĘ (HL2 feel)
        rb.constraints = RigidbodyConstraints.None;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.angularDamping = 0.2f;   // poczuj swobodę
        rb.maxAngularVelocity = 20f;
    }


    public void BeginCarry(Transform target, float distanceOverride)
    {
        carryTarget = target;
        isCarried = true;
        if (distanceOverride > 0f) carryDistance = distanceOverride;

        // NATYCHMIASTOWE USTAWIENIE W PUNKCIE DOCELU (środek ekranu, przed kamerą)
        rb.position = carryTarget.position + carryTarget.forward * carryDistance;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }


    public void EndCarry()
    {
        isCarried = false;
        carryTarget = null;
        // nie dajemy żadnego impulsu – obiekt po prostu zostaje z aktualną prędkością (zerową)
    }

    public void SetCarryDistance(float d) => carryDistance = Mathf.Max(0.05f, d);

    void FixedUpdate()
    {
        if (!isCarried || carryTarget == null) return;

        // — pozycja — (sprężyna jak wcześniej)
        Vector3 targetPos = carryTarget.position + carryTarget.forward * carryDistance;
        Vector3 toTarget = targetPos - rb.position;
        Vector3 desiredV = toTarget * (carrySpring * Time.fixedDeltaTime);
        Vector3 dv = desiredV - rb.linearVelocity;
        dv = Vector3.ClampMagnitude(dv, maxAccel);
        rb.AddForce(dv * carryDamping, ForceMode.Force);

        // — rotacja — (stabilizacja: patrz w tę samą stronę co kamera, „up” = światowy up)
        if (stabilizeWhileCarried)
        {
            // docelowo wyrównujemy yaw do kamery; up = global up, więc box się nie „przewraca” sam
            Vector3 fwd = carryTarget.forward;
            Vector3 flatFwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
            if (flatFwd.sqrMagnitude < 1e-4f) flatFwd = Vector3.forward;

            Quaternion desiredRot = Quaternion.LookRotation(flatFwd, Vector3.up);

            // przelicz „błąd rotacji” na oś/kat i zamień na moment
            Quaternion qErr = desiredRot * Quaternion.Inverse(rb.rotation);
            qErr.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) { angle -= 360f; }
            axis = axis.normalized;

            // sprężyna kątowa
            Vector3 angVelErr = -rb.angularVelocity;
            Vector3 ctrl = axis * Mathf.Deg2Rad * angle * rotSpring + angVelErr * rotDamping;

            // ogranicz „szarpnięcie”
            if (ctrl.sqrMagnitude > maxAngAccel * maxAngAccel)
                ctrl = ctrl.normalized * maxAngAccel;

            rb.AddTorque(ctrl, ForceMode.Acceleration);
        }
    }

    public bool IsResting()
    {
        // uznaj obiekt za "stojący", jeśli nie porusza się lub śpi
        if (rb == null) return false;
        return rb.IsSleeping() || rb.linearVelocity.magnitude < 0.05f;
    }

}
