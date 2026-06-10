using UnityEngine;

public class WaterZone : MonoBehaviour
{
    public Vector3 waterNormal = Vector3.up;

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out IWettable wettable))
            wettable.EnterWater(GetWaterSurfaceY());
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out IWettable wettable))
            wettable.ExitWater();
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.TryGetComponent(out IWettable wettable)) return;

        // Jeśli gracz jest w trakcie wynurzania się „wymuszonego” – pomiń tick.
        if (other.TryGetComponent<PlayerMovement>(out var pm) && pm.IsClimbingOutOfWater)
            return;

        // ⬇️ TO BYŁO transform.position.y – przez to „połowa akwarium” liczyła się jako powierzchnia.
        wettable.HandleSwimming(waterNormal, GetWaterSurfaceY());
    }

    float GetWaterSurfaceY()
    {
        var col = GetComponent<Collider>();
        return col.bounds.max.y; // faktyczna powierzchnia wody
    }
}
