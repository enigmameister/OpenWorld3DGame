using System;
using UnityEngine;

public class MoneyDropSpawner : MonoBehaviour
{
    [Serializable]
    public struct CashVisualTier
    {
        [Min(1)] public int minValue;        // od tej kwoty w górê
        public GameObject prefab;            // jaki prefab ma polecieæ
    }

    [Header("Cash visuals (tiers)")]
    [Tooltip("Posortuj rosn¹co po minValue. Spawner wybierze najwy¿szy próg <= amount.")]
    public CashVisualTier[] tiers;

    [Header("Drop")]
    [SerializeField] private Transform dropOrigin;
    [SerializeField] private float forward = 0.8f;
    [SerializeField] private float up = 1.0f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    public void SpawnCash(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0) return;

        var prefab = PickPrefab(amount);
        if (!prefab)
        {
            Debug.LogWarning("[MoneyDropSpawner] No cash prefab tier set!");
            return;
        }

        var t = dropOrigin ? dropOrigin : transform;
        Vector3 origin = t.position + Vector3.up * up;
        Vector3 fwd = t.forward;

        float sphereRadius = 0.25f;
        float castDistance = forward;

        Vector3 dropPos = origin + fwd * forward;

        if (Physics.SphereCast(origin, sphereRadius, fwd, out RaycastHit hit, castDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            dropPos = hit.point - fwd * (sphereRadius + 0.05f);
            dropPos.y = origin.y;
        }

        Vector3 pos = dropPos + Vector3.up * 0.4f;
        if (Physics.Raycast(pos, Vector3.down, out RaycastHit ground, 3f, ~0, QueryTriggerInteraction.Ignore))
            pos = ground.point + ground.normal * 0.05f;

        Quaternion rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, Vector3.up), Vector3.up);

        var go = Instantiate(prefab, pos, rot);

        // ustaw Value na CashPickup
        var cash = go.GetComponent<CashPickup>();
        if (cash != null)
            cash.value = amount;   // <-- u Ciebie pole mo¿e siê nazywaæ Value/value, dopasuj

        // lekki impuls
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(fwd * 2.2f + Vector3.up * 0.8f, ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 2.0f, ForceMode.Impulse);
        }
    }

    private GameObject PickPrefab(int amount)
    {
        if (tiers == null || tiers.Length == 0) return null;

        // wybierz najwy¿szy próg <= amount
        GameObject best = null;
        int bestMin = int.MinValue;

        for (int i = 0; i < tiers.Length; i++)
        {
            var t = tiers[i];
            if (!t.prefab) continue;
            if (t.minValue <= amount && t.minValue > bestMin)
            {
                bestMin = t.minValue;
                best = t.prefab;
            }
        }

        // jeœli kwota mniejsza ni¿ najni¿szy próg, weŸ najni¿szy prefab
        if (!best)
        {
            int lowest = int.MaxValue;
            for (int i = 0; i < tiers.Length; i++)
            {
                var t = tiers[i];
                if (!t.prefab) continue;
                if (t.minValue < lowest)
                {
                    lowest = t.minValue;
                    best = t.prefab;
                }
            }
        }

        return best;
    }
}
