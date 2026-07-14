using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MotionLightTriggerRelay : MonoBehaviour
{
    [SerializeField] private MotionLightDetector detector;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        if (detector == null)
            detector = GetComponentInParent<MotionLightDetector>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (detector != null)
            detector.NotifyTriggerEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (detector != null)
            detector.NotifyTriggerStay(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (detector != null)
            detector.NotifyTriggerExit(other);
    }
}