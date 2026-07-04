using UnityEngine;

public class VehicleLayerSetup : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField] private string vehicleBodyLayerName = "VehicleBody";
    [SerializeField] private string vehicleHitboxLayerName = "VehicleHitbox";

    [Header("Refs")]
    [SerializeField] private Transform runOverTriggerRoot;

    [ContextMenu("Apply Vehicle Layers")]
    public void ApplyVehicleLayers()
    {
        int bodyLayer = LayerMask.NameToLayer(vehicleBodyLayerName);
        int hitboxLayer = LayerMask.NameToLayer(vehicleHitboxLayerName);

        if (bodyLayer < 0)
        {
            Debug.LogError($"Layer not found: {vehicleBodyLayerName}");
            return;
        }

        if (hitboxLayer < 0)
        {
            Debug.LogError($"Layer not found: {vehicleHitboxLayerName}");
            return;
        }

        Collider[] allColliders = GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider col = allColliders[i];

            if (col == null)
                continue;

            if (runOverTriggerRoot != null && col.transform.IsChildOf(runOverTriggerRoot))
            {
                col.gameObject.layer = hitboxLayer;
            }
            else
            {
                col.gameObject.layer = bodyLayer;
            }
        }

        if (runOverTriggerRoot != null)
            SetLayerRecursive(runOverTriggerRoot, hitboxLayer);

        Debug.Log($"[VehicleLayerSetup] Applied layers on {name}");
    }

    private void SetLayerRecursive(Transform root, int layer)
    {
        if (root == null)
            return;

        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }
}