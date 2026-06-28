using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("Ustawienia")]
    public float mouseSens = 100f;
    private float defaultMouseSens;

    public Transform playerBody;
    public bool useSmoothing = true;
    public float smoothTime = 0.05f;

    [Header("Free Look Zoom")]
    public Transform cameraTransform; // Przypisz MainCamera
    public float zoomSpeed = 2f;
    public float minZoom = -6f;
    public float maxZoom = -2f;

    private float xRotation = 0f;
    private Vector2 currentMouseDelta;
    private Vector2 currentMouseDeltaVelocity;

    private bool isFreeLooking = false;
    private bool transitioningBack = false;
    private float freeLookReleaseSpeed = 6f;

    private float currentYaw = 0f;

    private float currentZoom = -4f;
    private float targetZoom = -4f;
    private float defaultZoom = -4f;

    private CameraSwitcher cameraSwitcher;
    private Gun currentGun;

    public static bool IsLookLocked = false;

    void Awake()
    {
        cameraSwitcher = FindFirstObjectByType<CameraSwitcher>();
        defaultMouseSens = mouseSens;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraTransform != null)
        {
            defaultZoom = cameraTransform.localPosition.z;
            currentZoom = defaultZoom;
            targetZoom = defaultZoom;
        }
    }

    void Update()
    {

        if (IsLookLocked || PlayerInputHandler.GameplayInputBlocked || InventoryUI.IsInventoryOpen || (FindFirstObjectByType<PlayerStats>()?.IsDead ?? false))
        {
            ResetLookFull();
            return;
        }
            
        if (IsLookLocked || InventoryUI.IsInventoryOpen || (FindFirstObjectByType<PlayerStats>()?.IsDead ?? false))
        {
            // wyzeruj bufor ruchu i freelook, żeby po zamknięciu nie było „resztek”
            isFreeLooking = false;
            transitioningBack = false;
            currentMouseDelta = Vector2.zero;
            currentMouseDeltaVelocity = Vector2.zero;

            if (cameraTransform != null)
            {
                targetZoom = defaultZoom;
                currentZoom = defaultZoom;
                var p = cameraTransform.localPosition;
                p.z = defaultZoom;
                cameraTransform.localPosition = p;
            }
            return;
        }

        bool wasFreeLooking = isFreeLooking;
        bool isScoped = currentGun != null && currentGun.IsScoped();
        isFreeLooking = Input.GetMouseButton(2) && cameraSwitcher != null && cameraSwitcher.IsTPPActive && !isScoped;

        if (isFreeLooking && !wasFreeLooking)
        {
            // Właśnie aktywowano freelook – zapisz aktualną rotację jako startową
            currentYaw = transform.eulerAngles.y;
        }

        bool isUsingScope = currentGun != null && currentGun.IsScoped(); // zakładamy metodę IsScoped()

        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        Vector2 targetMouseDelta = new Vector2(mouseX, mouseY) * mouseSens;

        if (useSmoothing)
        {
            currentMouseDelta = Vector2.SmoothDamp(currentMouseDelta, targetMouseDelta, ref currentMouseDeltaVelocity, smoothTime);
        }
        else
        {
            currentMouseDelta = targetMouseDelta;
        }

        if (isFreeLooking)
        {
            currentYaw += currentMouseDelta.x;
        }

        xRotation -= currentMouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Zmiana zoomu tylko w Free Look
        if (isFreeLooking && cameraTransform != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetZoom = Mathf.Clamp(targetZoom + scroll * zoomSpeed, minZoom, maxZoom);
            }
        }

        // Wykryj wyjście z Free Look
        if (!isFreeLooking && wasFreeLooking)
        {
            transitioningBack = true;
            currentYaw = transform.eulerAngles.y;
        }

        // Ustaw rotację kamery
        if (isFreeLooking)
        {
            transform.rotation = Quaternion.Euler(xRotation, currentYaw, 0f);
        }

        else if (transitioningBack)
        {
            float targetYaw = playerBody.eulerAngles.y;
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * freeLookReleaseSpeed);
            transform.rotation = Quaternion.Euler(xRotation, currentYaw, 0f);

            if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, targetYaw)) < 0.5f)
            {
                transitioningBack = false;
                currentYaw = targetYaw;
            }
        }
        else
        {
            transform.rotation = Quaternion.Euler(xRotation, playerBody.eulerAngles.y, 0f);
        }

        // Płynne przywracanie zoomu
        if (!isFreeLooking && transitioningBack && cameraTransform != null)
        {
            targetZoom = defaultZoom;
        }

        if (cameraTransform != null)
        {
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * 10f);
            Vector3 localPos = cameraTransform.localPosition;
            localPos.z = currentZoom;
            cameraTransform.localPosition = localPos;
        }

        var wm = FindFirstObjectByType<WeaponManager>();
        if (wm != null)
        {
            var slots = wm.GetWeaponSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].activeSelf)
                {
                    currentGun = slots[i].GetComponentInChildren<Gun>();
                    break;
                }
            }
        }

    }

    void LateUpdate()
    {
        if (IsLookLocked || PlayerInputHandler.GameplayInputBlocked || InventoryUI.IsInventoryOpen || (FindFirstObjectByType<PlayerStats>()?.IsDead ?? false))
        {
            currentMouseDelta = Vector2.zero;
            currentMouseDeltaVelocity = Vector2.zero;
            return;
        }

        if (!isFreeLooking && !transitioningBack)
        {
            playerBody.Rotate(Vector3.up * currentMouseDelta.x);
        }
    }

    public void ResetLook()
    {
        currentMouseDelta = Vector2.zero;
        currentMouseDeltaVelocity = Vector2.zero;
    }

    public void ResetLookFull()
    {
        isFreeLooking = false;
        transitioningBack = false;

        currentMouseDelta = Vector2.zero;
        currentMouseDeltaVelocity = Vector2.zero;

        if (cameraTransform != null)
        {
            targetZoom = defaultZoom;
            currentZoom = defaultZoom;

            Vector3 p = cameraTransform.localPosition;
            p.z = defaultZoom;
            cameraTransform.localPosition = p;
        }
    }
    public void SetSensitivityMultiplier(float multiplier)
    {
        mouseSens = defaultMouseSens * Mathf.Max(0.01f, multiplier);
    }

    public void ResetSensitivity()
    {
        mouseSens = defaultMouseSens;
    }
}
