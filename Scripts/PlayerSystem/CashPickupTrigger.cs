using UnityEngine;

[DisallowMultipleComponent]
public class CashPickupTrigger : MonoBehaviour
{
    [Tooltip("Jeœli puste, skrypt znajdzie CashPickup w parentach.")]
    public CashPickup parentCash;

    private void Awake()
    {
        if (parentCash == null)
            parentCash = GetComponentInParent<CashPickup>();

        // wymuœ trigger (¿eby nie by³o pomy³ki w inspektorze)
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (parentCash == null) return;
        parentCash.TriggerPickupFromChild(other);
    }
}
