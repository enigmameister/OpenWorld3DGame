using UnityEngine;

[DisallowMultipleComponent]
public class VehicleCameraController : MonoBehaviour
{
    [Header("Objects")]
    [SerializeField] private GameObject carObject;
    [SerializeField] private GameObject carCameraRoot;
    [SerializeField] private GameObject playerCamera;

    [Header("Car Cameras")]
    [SerializeField] private GameObject[] carCameras;
    [SerializeField] private Camera activeCarCamera;
    [SerializeField] private float cameraLerpSpeed = 5f;

    [Header("Freelock")]
    [SerializeField] private KeyCode freelockToggleKey = KeyCode.Mouse1;
    [SerializeField] private float orbitYawSpeed = 180f;
    [SerializeField] private float orbitPitchSpeed = 120f;
    [SerializeField] private Vector2 orbitPitchLimits = new Vector2(-10f, 65f);
    [SerializeField] private float orbitMinDistance = 2.5f;
    [SerializeField] private float orbitMaxDistance = 8f;
    [SerializeField] private float orbitZoomSpeed = 2.0f;
    [SerializeField] private float orbitTargetHeight = 1.3f;
    [SerializeField] private float orbitSmooth = 12f;

    private int currentCameraIndex;
    private Transform currentCameraTarget;

    private bool isActive;
    private bool useFreelock;
    private bool freelockJustLatched;

    private float orbitYaw;
    private float orbitPitch;
    private float orbitDistance;

    public int CurrentCameraIndex => currentCameraIndex;
    public bool IsFreelockActive => useFreelock;

    private void Awake()
    {
        ResolveRefs();
        DisableAllCarCameras();
        ResetFreelockDefaults();
    }

    private void LateUpdate()
    {
        if (!isActive || activeCarCamera == null || carObject == null)
            return;

        UpdateFreelockInput();
        UpdateCameraPosition();
    }

    public void SetContext(
        GameObject vehicleObject,
        GameObject carCameraRootObject,
        GameObject playerCameraObject,
        GameObject[] cameraPoints,
        Camera activeCamera)
    {
        if (vehicleObject != null)
            carObject = vehicleObject;

        if (carCameraRootObject != null)
            carCameraRoot = carCameraRootObject;

        if (playerCameraObject != null)
            playerCamera = playerCameraObject;

        if (cameraPoints != null && cameraPoints.Length > 0)
            carCameras = cameraPoints;

        if (activeCamera != null)
            activeCarCamera = activeCamera;

        ResolveRefs();
        PrepareInitialCameraTarget();
    }

    private void ResolveRefs()
    {
        if (carObject == null)
            carObject = gameObject;

        if (activeCarCamera == null && carCameraRoot != null)
            activeCarCamera = carCameraRoot.GetComponent<Camera>();

        if (activeCarCamera == null)
            activeCarCamera = GetComponentInChildren<Camera>(true);
    }

    private void PrepareInitialCameraTarget()
    {
        if (carCameras != null && carCameras.Length > 0 && carCameras[0] != null)
            currentCameraTarget = carCameras[0].transform;
    }

    public void OnPlayerEnteredVehicle()
    {
        isActive = true;

        if (carCameraRoot != null)
            carCameraRoot.SetActive(true);

        if (playerCamera != null)
            playerCamera.SetActive(false);

        ActivateCameraIndex(0);

        useFreelock = false;
        ResetFreelockDefaults();
    }

    public void OnPlayerExitedVehicle()
    {
        isActive = false;

        if (carCameraRoot != null)
            carCameraRoot.SetActive(false);

        if (playerCamera != null)
            playerCamera.SetActive(true);

        DisableAllCarCameras();

        useFreelock = false;
        currentCameraIndex = 0;
        PrepareInitialCameraTarget();
        ResetFreelockDefaults();
    }

    public void OnPlayerRestoredInsideVehicle()
    {
        isActive = true;

        if (carCameraRoot != null)
            carCameraRoot.SetActive(true);

        if (playerCamera != null)
            playerCamera.SetActive(false);

        ActivateCameraIndex(0);

        useFreelock = false;
        ResetFreelockDefaults();
    }

    public void SwitchToNextCamera()
    {
        if (!isActive)
            return;

        if (carCameras == null || carCameras.Length == 0)
            return;

        useFreelock = false;

        for (int i = 0; i < carCameras.Length; i++)
        {
            int nextIndex = (currentCameraIndex + 1) % carCameras.Length;

            currentCameraIndex = nextIndex;

            if (carCameras[currentCameraIndex] != null)
            {
                currentCameraTarget = carCameras[currentCameraIndex].transform;
                ActivateCameraIndex(currentCameraIndex);
                return;
            }
        }

        currentCameraTarget = null;
    }

    private void ActivateCameraIndex(int index)
    {
        if (carCameras == null || carCameras.Length == 0)
            return;

        currentCameraIndex = Mathf.Clamp(index, 0, carCameras.Length - 1);

        for (int i = 0; i < carCameras.Length; i++)
        {
            if (carCameras[i] != null)
                carCameras[i].SetActive(i == currentCameraIndex);
        }

        currentCameraTarget = carCameras[currentCameraIndex] != null
            ? carCameras[currentCameraIndex].transform
            : null;
    }

    private void DisableAllCarCameras()
    {
        if (carCameras != null)
        {
            for (int i = 0; i < carCameras.Length; i++)
            {
                if (carCameras[i] != null)
                    carCameras[i].SetActive(false);
            }
        }

        if (carCameraRoot != null)
            carCameraRoot.SetActive(false);
    }

    private void UpdateFreelockInput()
    {
        if (!isActive)
            return;

        if (WorldMapUI.IsOpen)
            return;

        if (Input.GetKeyDown(freelockToggleKey) && !useFreelock)
            EnableFreelockFromCurrentCamera();

        if (!useFreelock)
            return;

        if (PlayerInputHandler.Instance == null)
            return;

        Vector2 look = PlayerInputHandler.Instance.LookDelta;

        if (look.sqrMagnitude > 0.000001f)
        {
            orbitYaw += look.x * orbitYawSpeed * Time.unscaledDeltaTime;
            orbitPitch -= look.y * orbitPitchSpeed * Time.unscaledDeltaTime;
            orbitPitch = Mathf.Clamp(orbitPitch, orbitPitchLimits.x, orbitPitchLimits.y);
            freelockJustLatched = false;
        }

        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            orbitDistance = Mathf.Clamp(
                orbitDistance - scroll * orbitZoomSpeed,
                orbitMinDistance,
                orbitMaxDistance
            );

            freelockJustLatched = false;
        }
    }

    private void UpdateCameraPosition()
    {
        if (useFreelock)
        {
            Vector3 target = carObject.transform.position + Vector3.up * orbitTargetHeight;
            Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);

            Vector3 desiredPos = target + rot * (Vector3.back * orbitDistance);
            Quaternion desiredRot = Quaternion.LookRotation(target - desiredPos, Vector3.up);

            float k = freelockJustLatched ? 1f : Time.deltaTime * orbitSmooth;

            activeCarCamera.transform.position = Vector3.Lerp(
                activeCarCamera.transform.position,
                desiredPos,
                k
            );

            activeCarCamera.transform.rotation = Quaternion.Slerp(
                activeCarCamera.transform.rotation,
                desiredRot,
                k
            );

            freelockJustLatched = false;
            return;
        }

        if (currentCameraTarget == null)
            return;

        activeCarCamera.transform.position = Vector3.Lerp(
            activeCarCamera.transform.position,
            currentCameraTarget.position,
            Time.deltaTime * cameraLerpSpeed
        );

        activeCarCamera.transform.rotation = Quaternion.Slerp(
            activeCarCamera.transform.rotation,
            currentCameraTarget.rotation,
            Time.deltaTime * cameraLerpSpeed
        );
    }

    private void EnableFreelockFromCurrentCamera()
    {
        if (activeCarCamera == null || carObject == null)
            return;

        Vector3 target = carObject.transform.position + Vector3.up * orbitTargetHeight;
        Vector3 offset = activeCarCamera.transform.position - target;

        orbitDistance = Mathf.Clamp(offset.magnitude, orbitMinDistance, orbitMaxDistance);

        Quaternion look = Quaternion.LookRotation(
            target - activeCarCamera.transform.position,
            Vector3.up
        );

        Vector3 e = look.eulerAngles;

        orbitYaw = e.y;
        orbitPitch = e.x > 180f ? e.x - 360f : e.x;

        useFreelock = true;
        freelockJustLatched = true;
    }

    private void ResetFreelockDefaults()
    {
        if (carObject == null)
            return;

        orbitYaw = carObject.transform.eulerAngles.y;
        orbitPitch = Mathf.Clamp(15f, orbitPitchLimits.x, orbitPitchLimits.y);
        orbitDistance = Mathf.Clamp(orbitMaxDistance * 0.65f, orbitMinDistance, orbitMaxDistance);
        freelockJustLatched = false;
    }

    public VehicleCameraSnapshot GetSnapshot()
    {
        return new VehicleCameraSnapshot
        {
            cameraIndex = currentCameraIndex,

            useFreelock = useFreelock,
            orbitYaw = orbitYaw,
            orbitPitch = orbitPitch,
            orbitDistance = orbitDistance
        };
    }

    public void ApplySnapshot(VehicleCameraSnapshot snapshot)
    {
        if (carCameras == null || carCameras.Length == 0)
            return;

        ActivateCameraIndex(snapshot.cameraIndex);

        useFreelock = snapshot.useFreelock;
        orbitYaw = snapshot.orbitYaw;
        orbitPitch = snapshot.orbitPitch;
        orbitDistance = Mathf.Clamp(snapshot.orbitDistance, orbitMinDistance, orbitMaxDistance);
        freelockJustLatched = true;
    }
}