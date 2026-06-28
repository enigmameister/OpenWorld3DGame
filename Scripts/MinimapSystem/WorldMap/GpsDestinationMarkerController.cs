using UnityEngine;

public class GpsDestinationMarkerController : MonoBehaviour
{
    [Header("World 3D Marker")]
    public GameObject worldMarkerPrefab;

    [Header("Animation")]
    public float heightOffset = 3f;
    public float bobAmplitude = 0.35f;
    public float bobSpeed = 2.5f;
    public float rotateSpeed = 90f;

    private GameObject activeMarker;
    private Transform target;

    void Update()
    {
        if (activeMarker == null || target == null)
            return;

        Vector3 pos = target.position;
        pos.y += heightOffset + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        activeMarker.transform.position = pos;

        Camera cam = Camera.main;

        if (cam != null)
        {
            Vector3 dir = activeMarker.transform.position - cam.transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.01f)
                activeMarker.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        else
        {
            activeMarker.transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }
    }

    public void Show(Transform targetTransform)
    {
        target = targetTransform;

        if (target == null)
        {
            Hide();
            return;
        }

        if (activeMarker == null && worldMarkerPrefab != null)
            activeMarker = Instantiate(worldMarkerPrefab);

        if (activeMarker != null)
            activeMarker.SetActive(true);
    }

    public void Hide()
    {
        target = null;

        if (activeMarker != null)
            activeMarker.SetActive(false);
    }
}