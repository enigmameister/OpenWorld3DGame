using UnityEngine;

public class CameraCollisionHandler : MonoBehaviour
{
    public Transform cameraPivot; // punkt zaczepienia (np. ty³ g³owy postaci lub maska auta)
    public Transform cameraTarget; // transform z docelowej pozycji kamery
    public float minDistance = 0.5f;
    public float maxDistance = 3f;
    public LayerMask collisionMask;

    private Vector3 desiredCameraPos;

    void LateUpdate()
    {
        Vector3 direction = (cameraTarget.position - cameraPivot.position).normalized;
        float desiredDistance = Vector3.Distance(cameraPivot.position, cameraTarget.position);

        if (Physics.Raycast(cameraPivot.position, direction, out RaycastHit hit, desiredDistance, collisionMask))
        {
            desiredCameraPos = cameraPivot.position + direction * (hit.distance - 0.2f); // ma³y bufor
        }
        else
        {
            desiredCameraPos = cameraTarget.position;
        }

        transform.position = Vector3.Lerp(transform.position, desiredCameraPos, Time.deltaTime * 10f);
        transform.LookAt(cameraPivot.position);
    }
}
