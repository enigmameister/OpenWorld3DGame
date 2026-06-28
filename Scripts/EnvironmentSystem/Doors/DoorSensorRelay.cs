using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DoorSensorRelay : MonoBehaviour
{
    public RevolvingDoorController controller;   // wska¿ wa³ z kontrolerem
    public string requiredTag = "Player";

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        // Dla pewnoœci – daj kinematyczny Rigidbody, aby trigger zawsze dzia³a³ niezale¿nie od typu gracza
        if (!TryGetComponent<Rigidbody>(out var rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (controller && (string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag)))
            controller.SetPresence(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (controller && (string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag)))
            controller.SetPresence(false);
    }
}
