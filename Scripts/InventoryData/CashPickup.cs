using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CashPickup : MonoBehaviour, IPressable
{
    [Header("Wartość banknotu")]
    [Min(1)]
    public int value = 100;

    [Header("FX")]
    public bool hideOnPickup = true;
    public float destroyDelay = 0.05f;

    private bool _picked;

    // ===== IPressable (raycast + E) =====
    public void Press()
    {
        TryPickup(GetPlayerStats());
    }

    public string Label => $"{value}$";

    public void TriggerPickupFromChild(Collider playerCollider)
    {
        if (_picked) return;
        if (playerCollider == null) return;
        if (!playerCollider.CompareTag("Player")) return;

        var stats = playerCollider.GetComponent<PlayerStats>()
                 ?? playerCollider.GetComponentInParent<PlayerStats>();

        TryPickup(stats);
    }

    private void TryPickup(PlayerStats stats)
    {
        if (_picked) return;
        _picked = true;

        if (stats != null)
        {
            // ✅ stan pieniędzy od razu, animacja tylko w UI
            if (InventoryUI.Instance != null)
                InventoryUI.Instance.ApplyMoneyChange(value);
            else
                stats.SetMoney(stats.money + value); // fallback jeśli InventoryUI nie istnieje w scenie
        }

        // wyłącz wszystkie collidery (parent + child), żeby nie zebrać 2x
        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        if (hideOnPickup)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
        }

        StartCoroutine(DestroySoon());
    }

    private IEnumerator DestroySoon()
    {
        if (destroyDelay > 0f)
            yield return new WaitForSeconds(destroyDelay);

        Destroy(gameObject);
    }

    private PlayerStats GetPlayerStats()
    {
        if (PlayerInputHandler.Instance != null)
        {
            var ps = PlayerInputHandler.Instance.GetComponent<PlayerStats>()
                  ?? PlayerInputHandler.Instance.GetComponentInParent<PlayerStats>();
            if (ps != null) return ps;
        }

        return FindFirstObjectByType<PlayerStats>();
    }
}
