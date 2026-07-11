using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DoubleDoorsTriggerRelay : MonoBehaviour
{
    public enum DoorSide
    {
        Outside = 0,
        Inside = 1
    }

    [SerializeField] private DoubleDoorsManualController controller;
    [SerializeField] private DoorSide side = DoorSide.Outside;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        if (controller == null)
            controller = GetComponentInParent<DoubleDoorsManualController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[DoorTrigger] {name} ENTER {other.name}, side={side}", this);

        if (controller != null)
            controller.NotifyTriggerEnter(other, (int)side);
    }

    private void OnTriggerExit(Collider other)
    {
        if (controller != null)
            controller.NotifyTriggerExit(other);
    }
}