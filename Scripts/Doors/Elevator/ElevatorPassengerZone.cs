using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ElevatorPassengerZone : MonoBehaviour
{
    public ElevatorController elevator;   // przypinasz w Inspectorze

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!elevator) return;

        var cc = other.GetComponent<CharacterController>() ??
                 other.GetComponentInParent<CharacterController>();

        if (cc != null)
        {
            elevator.RegisterPassenger(cc);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!elevator) return;

        var cc = other.GetComponent<CharacterController>() ??
                 other.GetComponentInParent<CharacterController>();

        if (cc != null)
        {
            elevator.UnregisterPassenger(cc);
        }
    }
}
