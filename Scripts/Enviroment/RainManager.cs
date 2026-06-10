using UnityEngine;

public class RainManager : MonoBehaviour
{
    [Header("Globalny deszcz")]
    public GameObject globalRainEffect; // Jeden RainEffect nad œwiatem

    [Header("Lokalne efekty (opcjonalnie)")]
    public Transform playerTransform;
    public Transform carTransform;

    [Header("Raycast ustawienia")]
    public float checkHeight = 5f;
    public float maxDistance = 10f;
    public LayerMask roofMask; // Warstwa, na której s¹ dachy

    private bool playerUnderRoof = false;
    private bool carUnderRoof = false;

    void Update()
    {
        // SprawdŸ dach nad graczem
        if (playerTransform != null)
            playerUnderRoof = IsUnderRoof(playerTransform);

        if (carTransform != null)
            carUnderRoof = IsUnderRoof(carTransform);
    }

    bool IsUnderRoof(Transform target)
    {
        Vector3 origin = target.position + Vector3.up * checkHeight;
        Vector3 direction = Vector3.down;

        bool hit = Physics.Raycast(origin, direction, out RaycastHit hitInfo, maxDistance, roofMask);

        Debug.DrawRay(origin, direction * maxDistance, hit ? Color.red : Color.green);

        return hit;
    }
}
