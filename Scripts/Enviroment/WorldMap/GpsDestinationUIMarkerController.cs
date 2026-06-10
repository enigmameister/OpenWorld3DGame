using UnityEngine;

public class GpsDestinationUIMarkerController : MonoBehaviour
{
    [Header("World Map")]
    public RectTransform worldMapMarker;
    public WorldMapRoadRenderer worldMapRoadRenderer;

    [Header("Minimap")]
    public RectTransform minimapMarker;
    public RectTransform minimapRect;
    public Camera minimapCamera;
    public float minimapEdgePadding = 12f;
    public bool clampToCircle = true;
    public bool forceMarkerParentToMinimapRect = true;

    private Transform target;

    void Start()
    {
        if (forceMarkerParentToMinimapRect && minimapMarker != null && minimapRect != null)
            minimapMarker.SetParent(minimapRect, false);

        Hide();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        UpdateWorldMapMarker();
        UpdateMinimapMarker();
    }

    public void Show(Transform targetTransform)
    {
        target = targetTransform;

        if (worldMapMarker != null)
            worldMapMarker.gameObject.SetActive(true);

        if (minimapMarker != null)
            minimapMarker.gameObject.SetActive(true);

        UpdateWorldMapMarker();
        UpdateMinimapMarker();
    }

    public void Hide()
    {
        target = null;

        if (worldMapMarker != null)
            worldMapMarker.gameObject.SetActive(false);

        if (minimapMarker != null)
            minimapMarker.gameObject.SetActive(false);
    }

    void UpdateWorldMapMarker()
    {
        if (worldMapMarker == null || worldMapRoadRenderer == null || target == null)
            return;

        worldMapMarker.anchoredPosition =
            worldMapRoadRenderer.WorldToMapPositionPublic(target.position);
    }

    void UpdateMinimapMarker()
    {
        if (minimapMarker == null || minimapRect == null || minimapCamera == null || target == null)
            return;

        Vector3 viewport = minimapCamera.WorldToViewportPoint(target.position);

        Vector2 rectSize = minimapRect.rect.size;

        Vector2 uiPos = new Vector2(
            (viewport.x - 0.5f) * rectSize.x,
            (viewport.y - 0.5f) * rectSize.y
        );

        if (clampToCircle)
        {
            float radius = Mathf.Min(rectSize.x, rectSize.y) * 0.5f - minimapEdgePadding;

            if (uiPos.magnitude > radius)
                uiPos = uiPos.normalized * radius;
        }
        else
        {
            float halfX = rectSize.x * 0.5f - minimapEdgePadding;
            float halfY = rectSize.y * 0.5f - minimapEdgePadding;

            uiPos.x = Mathf.Clamp(uiPos.x, -halfX, halfX);
            uiPos.y = Mathf.Clamp(uiPos.y, -halfY, halfY);
        }

        minimapMarker.anchoredPosition = uiPos;
    }
}