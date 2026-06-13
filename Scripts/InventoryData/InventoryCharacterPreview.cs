using UnityEngine;
using UnityEngine.UI;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

public class InventoryCharacterPreview : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage previewImage;
    [SerializeField] private Vector2Int renderSize = new Vector2Int(384, 384);

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

    private static Transform worldRoot;
    private static readonly Vector3 WORLD_POS = new Vector3(1e6f, 1e6f, 1e6f);

    private RenderTexture rt;
    private Transform pivot;
    private Camera previewCam;
    private GameObject camGO;

    private Transform cloneInstance;
    private Light previewLight;

    private bool rotateLeft;
    private bool rotateRight;

    private float yaw;
    private Quaternion baseRot;

    private bool usingLiveTarget;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (previewImage != null)
        {
            previewImage.raycastTarget = false;
            previewImage.maskable = false;
        }
    }

    private void EnsureWorldRoot()
    {
        if (worldRoot != null)
            return;

        GameObject go = new GameObject("__PreviewWorld");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.position = WORLD_POS;
        worldRoot = go.transform;
    }

    private void EnsureRenderTexture()
    {
        int width = Mathf.Max(64, renderSize.x);
        int height = Mathf.Max(64, renderSize.y);

        if (rt != null && rt.width == width && rt.height == height)
            return;

        ReleaseRenderTexture();

        rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            name = "InventoryPreviewRT",
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false
        };

        rt.Create();
    }

    private void EnsureCameraAndPivot()
    {
        if (pivot == null)
        {
            GameObject pivotGO = new GameObject("PreviewPivot");
            pivotGO.hideFlags = HideFlags.HideAndDontSave;
            pivot = pivotGO.transform;
        }

        if (previewCam == null)
        {
            camGO = new GameObject("InventoryPreviewCamera");
            camGO.hideFlags = HideFlags.HideAndDontSave;

            previewCam = camGO.AddComponent<Camera>();
            previewCam.clearFlags = CameraClearFlags.SolidColor;
            previewCam.nearClipPlane = 0.01f;
            previewCam.farClipPlane = 20f;
            previewCam.fieldOfView = 35f;
            previewCam.useOcclusionCulling = false;
            previewCam.allowHDR = false;
            previewCam.allowMSAA = false;
            previewCam.depth = -100f;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
            UniversalAdditionalCameraData camData =
                previewCam.GetComponent<UniversalAdditionalCameraData>();

            if (camData == null)
                camData = camGO.AddComponent<UniversalAdditionalCameraData>();

            camData.renderPostProcessing = false;
            camData.renderShadows = false;
            camData.requiresDepthOption = CameraOverrideOption.Off;
            camData.requiresColorOption = CameraOverrideOption.Off;
            camData.antialiasing = AntialiasingMode.None;
#endif
        }
    }

    public void OpenPreview()
    {
        EnsureRenderTexture();
        EnsureCameraAndPivot();

        if (previewImage != null)
            previewImage.texture = rt;

        previewCam.targetTexture = rt;
        previewCam.enabled = true;

        bool canUseLive =
            useLiveTarget &&
            liveTargetRoot != null &&
            liveTargetRoot.gameObject.activeInHierarchy;

        usingLiveTarget = canUseLive;

        if (canUseLive)
            SetupLivePreview();
        else
            SetupFallbackPreview();

        IsOpen = true;
    }

    private void SetupLivePreview()
    {
        CleanupFallbackClone();

        if (pivot == null || previewCam == null || liveTargetRoot == null)
            return;

        pivot.SetParent(null, true);
        pivot.position = liveTargetRoot.position + targetOffset;

        previewCam.transform.SetParent(pivot, false);
        previewCam.transform.localPosition = new Vector3(0f, 0f, -distance);
        previewCam.transform.localRotation = Quaternion.Euler(yAngle, 0f, 0f);

        previewCam.cullingMask = cameraCullingMask;
        previewCam.backgroundColor = new Color(0f, 0f, 0f, 0f);

        Vector3 fwd = liveTargetRoot.forward;
        fwd.y = 0f;

        if (fwd.sqrMagnitude < 0.0001f)
            fwd = Vector3.forward;

        baseRot = Quaternion.LookRotation(-fwd.normalized, Vector3.up);
        yaw = 0f;
        pivot.rotation = baseRot;
    }

    private void SetupFallbackPreview()
    {
        EnsureWorldRoot();

        if (pivot == null || previewCam == null || fallbackPreviewPrefab == null)
            return;

        if (cloneInstance == null)
        {
            cloneInstance = Instantiate(fallbackPreviewPrefab, worldRoot).transform;
            cloneInstance.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        cloneInstance.SetParent(worldRoot, false);
        cloneInstance.localPosition = Vector3.zero;
        cloneInstance.localRotation = Quaternion.identity;
        cloneInstance.localScale = Vector3.one;
        cloneInstance.gameObject.SetActive(true);

        pivot.SetParent(worldRoot, false);
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;

        previewCam.transform.SetParent(pivot, false);
        previewCam.transform.localPosition = new Vector3(0f, 0f, -distance);
        previewCam.transform.localRotation = Quaternion.Euler(yAngle, 0f, 0f);

        previewCam.cullingMask = cameraCullingMask.value != 0 ? cameraCullingMask : ~0;
        previewCam.backgroundColor = clearColor;

        SetupPreviewLight();

        Vector3 fwd = cloneInstance.forward;
        fwd.y = 0f;

        if (fwd.sqrMagnitude < 0.0001f)
            fwd = Vector3.forward;

        baseRot = Quaternion.LookRotation(-fwd.normalized, Vector3.up);
        yaw = 0f;
        pivot.rotation = baseRot;
    }

    private void SetupPreviewLight()
    {
        if (!addLight)
        {
            if (previewLight != null)
                previewLight.gameObject.SetActive(false);

            return;
        }

        if (previewLight == null)
        {
            GameObject lightGO = new GameObject("PreviewLight");
            lightGO.hideFlags = HideFlags.HideAndDontSave;
            lightGO.transform.SetParent(pivot, false);
            lightGO.transform.localPosition = new Vector3(0f, 1f, 0.5f);
            lightGO.transform.localRotation = Quaternion.Euler(45f, 180f, 0f);

            previewLight = lightGO.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.shadows = LightShadows.None;
        }

        previewLight.gameObject.SetActive(true);
        previewLight.intensity = lightIntensity;
        previewLight.color = lightColor;
    }

    public void ClosePreview()
    {
        if (!IsOpen)
            return;

        rotateLeft = false;
        rotateRight = false;

        if (previewImage != null)
            previewImage.texture = null;

        if (previewCam != null)
            previewCam.enabled = false;

        if (cloneInstance != null)
            cloneInstance.gameObject.SetActive(false);

        IsOpen = false;
    }

    private void CleanupFallbackClone()
    {
        if (cloneInstance != null)
            cloneInstance.gameObject.SetActive(false);

        if (previewLight != null)
            previewLight.gameObject.SetActive(false);
    }

    private void ReleaseRenderTexture()
    {
        if (previewCam != null && previewCam.targetTexture == rt)
            previewCam.targetTexture = null;

        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }

    private void OnDisable()
    {
        if (IsOpen)
            ClosePreview();
    }

    private void OnDestroy()
    {
        if (previewImage != null)
            previewImage.texture = null;

        if (previewCam != null)
            previewCam.targetTexture = null;

        ReleaseRenderTexture();

        if (camGO != null)
            Destroy(camGO);

        if (pivot != null)
            Destroy(pivot.gameObject);

        if (cloneInstance != null)
            Destroy(cloneInstance.gameObject);
    }

    private void LateUpdate()
    {
        if (!IsOpen || pivot == null)
            return;

        if (usingLiveTarget && liveTargetRoot != null && liveTargetRoot.gameObject.activeInHierarchy)
            pivot.position = liveTargetRoot.position + targetOffset;

        float dir = 0f;

        if (rotateLeft)
            dir -= 1f;

        if (rotateRight)
            dir += 1f;

        if (dir != 0f)
            yaw += dir * rotationSpeed * Time.unscaledDeltaTime;

        pivot.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseRot;
    }

    public void HoldLeft_On(bool on)
    {
        rotateLeft = on;
    }

    public void HoldRight_On(bool on)
    {
        rotateRight = on;
    }

    public void CenterView()
    {
        if (!IsOpen || pivot == null)
            return;

        yaw = 0f;

        Vector3 fwd =
            usingLiveTarget && liveTargetRoot != null && liveTargetRoot.gameObject.activeInHierarchy
                ? liveTargetRoot.forward
                : cloneInstance != null
                    ? cloneInstance.forward
                    : Vector3.forward;

        fwd.y = 0f;

        if (fwd.sqrMagnitude < 0.0001f)
            fwd = Vector3.forward;

        baseRot = Quaternion.LookRotation(-fwd.normalized, Vector3.up);
        pivot.rotation = baseRot;
    }

    public void ApplyPreviewLayerToTarget(bool enable)
    {
        // zostawione dla kompatybilności ze starymi wywołaniami
    }
}