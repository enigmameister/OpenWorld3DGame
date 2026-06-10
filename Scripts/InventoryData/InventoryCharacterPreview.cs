using UnityEngine;
using UnityEngine.UI;

public class InventoryCharacterPreview : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage previewImage;
    [SerializeField] private Vector2 renderSize = new Vector2(512, 512);

    [Header("Źródło (LIVE / Fallback)")]
    [SerializeField] private bool useLiveTarget = true;
    [SerializeField] private Transform liveTargetRoot;
    [SerializeField] private GameObject fallbackPreviewPrefab;

    [Header("Kamera")]
    [SerializeField] private float distance = 2.5f;
    [SerializeField] private float yAngle = 20f;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private LayerMask cameraCullingMask;

    [Header("Wygląd (tylko fallback)")]
    [SerializeField] private Color clearColor = new Color(0, 0, 0, 0);
    [SerializeField] private bool addLight = true;
    [SerializeField] private float lightIntensity = 1.5f;
    [SerializeField] private Color lightColor = Color.white;

    [Header("Offset celu")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);

    // --- runtime ---
    private static Transform worldRoot;
    private static readonly Vector3 WORLD_POS = new Vector3(1e6f, 1e6f, 1e6f);

    private RenderTexture rt;
    private Transform pivot;
    private Camera previewCam;

    private Transform cloneInstance;

    private bool rotateLeft, rotateRight;
    private float yaw;
    private Quaternion baseRot;

    public bool IsOpen { get; private set; }

    void EnsureWorldRoot()
    {
        if (worldRoot) return;
        var go = new GameObject("__PreviewWorld");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.position = WORLD_POS;
        worldRoot = go.transform;
    }

    // --- SAFETY: odpięcie RT od wszystkich kamer zanim ją zwolnimy ---
    void DetachRTFromAllCameras()
    {
        if (rt == null) return;

        if (previewCam && previewCam.targetTexture == rt)
            previewCam.targetTexture = null;

        // dodatkowe bezpieczeństwo – gdyby inna kamera dostała tę RT
        var cams = Camera.allCameras;
        for (int i = 0; i < cams.Length; i++)
            if (cams[i] && cams[i].targetTexture == rt)
                cams[i].targetTexture = null;
    }

    public void OpenPreview()
    {
        // Jeśli coś było otwarte i nie domknięte – domknij bezpiecznie
        if (IsOpen) ClosePreview();

        // RenderTexture
        rt = new RenderTexture((int)renderSize.x, (int)renderSize.y, 16, RenderTextureFormat.ARGB32)
        { name = "InventoryPreviewRT" };
        rt.Create();
        if (previewImage) previewImage.texture = rt;

        // Pivot + kamera
        pivot = new GameObject("PreviewPivot").transform;
        pivot.gameObject.hideFlags = HideFlags.HideAndDontSave;

        var camGO = new GameObject("InventoryPreviewCamera");
        camGO.hideFlags = HideFlags.HideAndDontSave;
        previewCam = camGO.AddComponent<Camera>();
        previewCam.clearFlags = CameraClearFlags.SolidColor;
        previewCam.backgroundColor = new Color(0, 0, 0, 0);
        previewCam.nearClipPlane = 0.01f;
        previewCam.farClipPlane = 20f;
        previewCam.fieldOfView = 35f;
        previewCam.useOcclusionCulling = false;
        previewCam.enabled = true;
        previewCam.targetTexture = rt;

        bool canUseLive = useLiveTarget && liveTargetRoot && liveTargetRoot.gameObject.activeInHierarchy;

        if (canUseLive)
        {
            pivot.position = liveTargetRoot.position + targetOffset;
            camGO.transform.SetParent(pivot, false);
            camGO.transform.localPosition = new Vector3(0, 0, -distance);
            camGO.transform.localRotation = Quaternion.Euler(yAngle, 0f, 0f);

            previewCam.cullingMask = cameraCullingMask;
            previewCam.backgroundColor = new Color(0, 0, 0, 0);

            Vector3 fwd = liveTargetRoot.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            baseRot = Quaternion.LookRotation(-fwd.normalized, Vector3.up);
            yaw = 0f;
            pivot.rotation = baseRot;
        }
        else
        {
            EnsureWorldRoot();

            cloneInstance = Instantiate(fallbackPreviewPrefab, worldRoot).transform;
            cloneInstance.localPosition = Vector3.zero;
            cloneInstance.localRotation = Quaternion.identity;
            cloneInstance.localScale = Vector3.one;

            pivot.SetParent(worldRoot, false);
            pivot.position = worldRoot.position;

            camGO.transform.SetParent(pivot, false);
            camGO.transform.localPosition = new Vector3(0, 0, -distance);
            camGO.transform.localRotation = Quaternion.Euler(yAngle, 0f, 0f);

            previewCam.cullingMask = ~0;
            previewCam.backgroundColor = clearColor;

            if (addLight)
            {
                var lightGO = new GameObject("PreviewLight");
                lightGO.hideFlags = HideFlags.HideAndDontSave;
                lightGO.transform.SetParent(pivot, false);
                lightGO.transform.localPosition = new Vector3(0f, 1f, 0.5f);
                var l = lightGO.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = lightIntensity;
                l.color = lightColor;
                l.shadows = LightShadows.None;
            }

            Vector3 fwd = cloneInstance.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            baseRot = Quaternion.LookRotation(-fwd.normalized, Vector3.up);
            yaw = 0f;
            pivot.rotation = baseRot;
        }

        IsOpen = true;
    }

    public void ClosePreview()
    {
        if (!IsOpen) return;

        rotateLeft = rotateRight = false;

        if (previewImage) previewImage.texture = null;

        // KLUCZ: odpinamy RT od wszystkich kamer zanim zwolnimy
        DetachRTFromAllCameras();

        if (previewCam) Destroy(previewCam.gameObject);

        if (rt)
        {
            // Release nie jest konieczny, ale zostawiamy – po odpięciu nie wywali warningu
            rt.Release();
            Destroy(rt);
            rt = null;
        }

        if (pivot) Destroy(pivot.gameObject);
        if (cloneInstance) Destroy(cloneInstance.gameObject);

        previewCam = null;
        pivot = null;
        cloneInstance = null;
        IsOpen = false;
    }

    void OnDisable()
    {
        // np. zamykanie panelu, zmiana sceny, wyjście z Play – zawsze domknij
        if (IsOpen) ClosePreview();
    }

    void OnDestroy()
    {
        // dodatkowa asekuracja przy niszczeniu obiektu
        if (rt || previewCam) ClosePreview();
    }

    void LateUpdate()
    {
        if (!IsOpen || pivot == null) return;

        if (useLiveTarget && liveTargetRoot && liveTargetRoot.gameObject.activeInHierarchy)
            pivot.position = liveTargetRoot.position + targetOffset;

        float dir = 0f;
        if (rotateLeft) dir -= 1f;
        if (rotateRight) dir += 1f;
        if (dir != 0f) yaw += dir * rotationSpeed * Time.unscaledDeltaTime;

        pivot.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseRot;
    }

    // UI events
    public void HoldLeft_On(bool on) => rotateLeft = on;
    public void HoldRight_On(bool on) => rotateRight = on;

    public void CenterView()
    {
        if (!IsOpen || pivot == null) return;

        yaw = 0f;
        Vector3 fwd =
            (useLiveTarget && liveTargetRoot && liveTargetRoot.gameObject.activeInHierarchy)
            ? liveTargetRoot.forward
            : (cloneInstance ? cloneInstance.forward : Vector3.forward);

        fwd.y = 0f; if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        baseRot = Quaternion.LookRotation(-fwd.normalized, Vector3.up);
        pivot.rotation = baseRot;
    }

    // placeholder – nic nie robi
    public void ApplyPreviewLayerToTarget(bool enable) { }
}
