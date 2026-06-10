using UnityEngine;

public class AutoStopPhysics : MonoBehaviour
{
    public float stopAfterSeconds = 2.5f;

    private Rigidbody rb;
    private float timer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (rb == null) return;

        timer += Time.deltaTime;
        if (timer >= stopAfterSeconds)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; // Zatrzymaj fizyk�
            Destroy(this); // Usu� skrypt po wykonaniu
        }
    }
}
