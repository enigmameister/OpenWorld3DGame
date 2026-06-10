using UnityEngine;

public class ThirdPersonCameraCollision : MonoBehaviour
{
    [Header("Referencje")]
    [Tooltip("Pivot kamery – zwykle Player/CameraHolder")]
    public Transform pivot;

    [Tooltip("Docelowa pozycja kamery TPP – np. TPP_Cam_Pos")]
    public Transform tppTarget;

    [Header("Kolizje")]
    [Tooltip("Warstwy traktowane jako przeszkody (œciany, geo)")]
    public LayerMask collisionMask = ~0;

    [Tooltip("Promieñ 'bañki' kamery – lepszy ni¿ go³y ray")]
    public float sphereRadius = 0.2f;

    [Tooltip("Minimalna odleg³oœæ od pivotu (¿eby kamera nie wlecia³a w g³owê)")]
    public float minDistance = 0.3f;

    [Tooltip("Margines od œciany")]
    public float wallOffset = 0.05f;

    [Header("P³ynnoœæ")]
    public float positionLerpSpeed = 15f;

    private float _currentDistance;

    void Start()
    {
        if (!pivot)
            Debug.LogWarning($"{name}: Brak pivotu kamery (CameraHolder)!");

        if (!tppTarget)
            Debug.LogWarning($"{name}: Brak tppTarget (TPP_Cam_Pos)!");

        if (pivot && tppTarget)
        {
            // startowa odleg³oœæ = taka jak docelowa TPP
            _currentDistance = Vector3.Distance(pivot.position, tppTarget.position);
        }
    }

    void LateUpdate()
    {
        // jeœli nie mamy referencji – nic nie rób
        if (!pivot || !tppTarget) return;

        // tylko TPP – w FPS nic nie ruszamy
        if (CameraSwitcher.Instance != null && !CameraSwitcher.Instance.IsTPPActive)
            return;

        Vector3 pivotPos = pivot.position;

        // idealna pozycja TPP (bez kolizji)
        Vector3 idealPos = tppTarget.position;

        Vector3 dir = (idealPos - pivotPos);
        float idealDist = dir.magnitude;

        if (idealDist < 0.001f)
            return;

        dir /= idealDist; // normalizacja

        float targetDist = idealDist;

        // SphereCast: sprawdŸ, czy miêdzy pivotem a idealn¹ pozycj¹ jest œciana
        if (Physics.SphereCast(
                pivotPos,
                sphereRadius,
                dir,
                out RaycastHit hit,
                idealDist,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            // odleg³oœæ do œciany minus ma³y margines
            targetDist = Mathf.Clamp(hit.distance - wallOffset, minDistance, idealDist);
        }

        // p³ynne przejœcie (¿eby kamera nie „skaka³a” przy dotykaniu œcian)
        _currentDistance = Mathf.Lerp(_currentDistance, targetDist, Time.deltaTime * positionLerpSpeed);

        // ustaw now¹ pozycjê kamery
        Vector3 newPos = pivotPos + dir * _currentDistance;
        transform.position = newPos;
    }
}
