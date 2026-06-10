using UnityEngine;

public class CarCamera : MonoBehaviour
{
    [Header("Refs")]
    public Transform target;
    public Transform cameraTransform;
    public Camera cam;

    [Header("Follow")]
    public Vector3 baseOffset = new Vector3(0f, 3f, -6f);
    public float followSmoothTime = 0.16f;
    public float rotationSpeed = 7f;

    [Header("NFS Steering Camera")]
    public float sideOffset = 0.75f;
    public float tiltAmount = 3.5f;
    public float tiltSpeed = 6f;

    [Header("FOV Speed Feeling")]
    public float normalFov = 62f;
    public float speedFov = 74f;
    public float nitroFov = 84f;
    public float fovSpeed = 5f;

    [Header("Camera Shake")]
    public float highSpeedShake = 0.045f;
    public float nitroShake = 0.08f;
    public float shakeSpeedStart = 120f;

    private CarControll car;
    private NitroSystem nitro;
    private Vector3 followVelocity;

    void Start()
    {
        if (target != null)
        {
            car = target.GetComponent<CarControll>();
            nitro = target.GetComponent<NitroSystem>();
        }

        if (cam == null && cameraTransform != null)
            cam = cameraTransform.GetComponent<Camera>();

        if (cameraTransform == null)
            cameraTransform = transform;

        if (cam != null)
            cam.fieldOfView = normalFov;
    }

    void LateUpdate()
    {
        if (target == null || car == null)
            return;

        float steer = car.GetSteerInput();
        float speed01 = Mathf.InverseLerp(0f, car.maxSpeedKPH, car.currentSpeedKPH);

        Vector3 offset = baseOffset + new Vector3(steer * sideOffset, 0f, 0f);
        Vector3 desiredPos = target.position + target.TransformDirection(offset);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPos,
            ref followVelocity,
            followSmoothTime
        );

        Vector3 lookTarget = target.position + Vector3.up * 1.1f + target.forward * 2f;

        Quaternion lookRot = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            lookRot,
            rotationSpeed * Time.deltaTime
        );

        UpdateCameraTilt(steer);
        UpdateFov(speed01);
        UpdateShake();
    }

    void UpdateCameraTilt(float steer)
    {
        if (cameraTransform == null)
            return;

        float tilt = -steer * tiltAmount;

        cameraTransform.localRotation = Quaternion.Lerp(
            cameraTransform.localRotation,
            Quaternion.Euler(0f, 0f, tilt),
            tiltSpeed * Time.deltaTime
        );
    }

    void UpdateFov(float speed01)
    {
        if (cam == null)
            return;

        bool nitroActive = nitro != null && nitro.isUsingNitro;

        float targetFov = Mathf.Lerp(normalFov, speedFov, speed01);

        if (nitroActive)
            targetFov = nitroFov;

        cam.fieldOfView = Mathf.Lerp(
            cam.fieldOfView,
            targetFov,
            fovSpeed * Time.deltaTime
        );
    }

    void UpdateShake()
    {
        if (cameraTransform == null || car == null)
            return;

        bool nitroActive = nitro != null && nitro.isUsingNitro;

        float speedKPH = car.currentSpeedKPH;

        if (speedKPH < shakeSpeedStart && !nitroActive)
        {
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                Vector3.zero,
                Time.deltaTime * 10f
            );
            return;
        }

        float speedShake01 = Mathf.InverseLerp(shakeSpeedStart, car.maxSpeedKPH, speedKPH);
        float shakePower = speedShake01 * highSpeedShake;

        if (nitroActive)
            shakePower += nitroShake;

        if (shakePower <= 0.001f)
        {
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                Vector3.zero,
                Time.deltaTime * 10f
            );
            return;
        }

        Vector3 shake = Random.insideUnitSphere * shakePower;
        shake.z = 0f;

        cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition,
            shake,
            Time.deltaTime * 18f
        );
    }
}