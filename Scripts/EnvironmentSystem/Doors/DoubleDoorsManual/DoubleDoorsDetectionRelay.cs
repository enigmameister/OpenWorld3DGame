using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DoubleDoorsDetectionRelay : MonoBehaviour
{
    [SerializeField] private DoubleDoorsManualController controller;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        if (controller == null)
            controller = GetComponentInParent<DoubleDoorsManualController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (controller != null)
            controller.NotifyDetectionEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (controller != null)
            controller.NotifyDetectionExit(other);
    }
}