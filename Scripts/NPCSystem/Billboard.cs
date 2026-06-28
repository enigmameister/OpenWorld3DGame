using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null) return;
        }
        // Obróæ sprite w kierunku kamery (tylko na osi Y)
        Vector3 dir = transform.position - mainCam.transform.position;
        dir.y = 0f;
        transform.rotation = Quaternion.LookRotation(dir);
    }
}
