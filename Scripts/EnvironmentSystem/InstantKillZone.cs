using UnityEngine;

public class InstantKillZone : MonoBehaviour
{
    public string killerName = "Œrodowisko"; // nazwa wyœwietlana w death logu

    private void OnTriggerEnter(Collider other)
    {
        IDamageable dmg = other.GetComponent<IDamageable>() ??
                          other.GetComponentInParent<IDamageable>();

        if (dmg != null)
        {
            dmg.TakeDamage(99999, killerName);
        }
    }
}
