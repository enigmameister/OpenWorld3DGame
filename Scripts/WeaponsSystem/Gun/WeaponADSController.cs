using UnityEngine;

public class WeaponADSController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform adsAnchor;

    [Header("Settings")]
    [SerializeField] private bool useAdsAnchor = true;
    [SerializeField] private bool matchRotationInAds = true;
    [SerializeField] private float adsSpeed = 10f;
    [SerializeField] private bool moveWeaponInTPP = true;

    [Header("Optimization")]
    [SerializeField] private float positionSnapDistance = 0.0001f;
    [SerializeField] private float rotationSnapAngle = 0.05f;

    private Vector3 defaultLocalPosition;
    private Quaternion defaultLocalRotation;

    private bool isADS;
    private bool needsUpdate;

    private bool IsTPPActive =>
        CameraSwitcher.Instance != null &&
        CameraSwitcher.Instance.IsTPPActive;

    void Awake()
    {
        if (visualRoot != null)
        {
            defaultLocalPosition = visualRoot.localPosition;
            defaultLocalRotation = visualRoot.localRotation;
        }

        needsUpdate = false;
    }

    void Update()
    {
        if (!needsUpdate) return;
        if (visualRoot == null) return;

        bool allowMoveThisMode =
            !IsTPPActive ||
            (IsTPPActive && moveWeaponInTPP);

        bool useADSPosition =
            allowMoveThisMode &&
            isADS &&
            useAdsAnchor &&
            adsAnchor != null;

        Vector3 targetPosition = useADSPosition
            ? adsAnchor.localPosition
            : defaultLocalPosition;

        Quaternion targetRotation =
            useADSPosition && matchRotationInAds
                ? adsAnchor.localRotation
                : defaultLocalRotation;

        float lerpSpeed = Time.deltaTime * adsSpeed;

        visualRoot.localPosition = Vector3.Lerp(
            visualRoot.localPosition,
            targetPosition,
            lerpSpeed
        );

        visualRoot.localRotation = Quaternion.Lerp(
            visualRoot.localRotation,
            targetRotation,
            lerpSpeed
        );

        float posDelta = (visualRoot.localPosition - targetPosition).sqrMagnitude;
        float rotDelta = Quaternion.Angle(visualRoot.localRotation, targetRotation);

        if (posDelta <= positionSnapDistance && rotDelta <= rotationSnapAngle)
        {
            visualRoot.localPosition = targetPosition;
            visualRoot.localRotation = targetRotation;
            needsUpdate = false;
        }
    }

    public void SetADS(bool value)
    {
        if (isADS == value)
            return;

        isADS = value;
        needsUpdate = true;
    }

    public bool IsADS()
    {
        return isADS;
    }
    public void ResetADS()
    {
        isADS = false;
        needsUpdate = false;

        if (visualRoot != null)
        {
            visualRoot.localPosition = defaultLocalPosition;
            visualRoot.localRotation = defaultLocalRotation;
        }
    }
}