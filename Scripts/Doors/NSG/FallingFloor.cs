using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FallingFloor : MonoBehaviour
{
    public float delayBeforeFall = 0.2f;
    public float fallDistance = 3f;
    public float fallSpeed = 2f;
    public bool destroyAfterFall = true;

    private bool _triggered = false;

    void Start()
    {
        // collider powinien byæ zwyk³y (nie trigger),
        // ale czêsto wygodniej daæ drugi ma³y trigger nad nim – zale¿nie jak masz zrobione.
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        StartCoroutine(FallRoutine());
    }

    IEnumerator FallRoutine()
    {
        _triggered = true;
        yield return new WaitForSeconds(delayBeforeFall);

        Vector3 start = transform.position;
        Vector3 end = start + Vector3.down * fallDistance;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * fallSpeed;
            transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        if (destroyAfterFall)
            Destroy(gameObject);
        else
            GetComponent<Collider>().enabled = false;
    }
}
