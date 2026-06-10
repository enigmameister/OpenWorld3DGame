using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.AI;

public class WorldMapUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject worldMapRoot;

    [Header("Canvas Group")]
    public CanvasGroup worldMapCanvasGroup;

    [Header("Map")]
    public RectTransform mapViewport; // Fill / okno mapy
    public RectTransform mapImage;    // Map_IMG

    [Header("Renderer")]
    public WorldMapRoadRenderer roadRenderer;

    private bool mapBuilt = false;

    [Header("Zoom UI")]
    public Button zoomInButton;
    public Button zoomOutButton;
    public TextMeshProUGUI zoomValueText;

    [Header("Zoom")]
    public float minZoom = 1f;
    public float maxZoom = 4f;
    public float zoomStep = 0.25f;

    private float zoom;
    private bool opened;

    private Vector2 dragStartMouse;
    private Vector2 dragStartMapPos;
    private bool dragging;

    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;

    [Header("Race Event Markers")]
    public RectTransform raceEventsIconsRoot;
    public WorldMapRaceEventMarkerUI raceEventMarkerPrefab;
    public RaceEventDefinition[] raceEvents;

    [Header("Info Panel")]
    public WorldMapEventInfoPanel raceEventInfoPanel;

    [Header("GPS Dialog")]
    public GameObject gpsDialogRoot;
    public RectTransform gpsDialogRect;
    public Button followGpsButton;
    public Button unfollowGpsButton;

    private RaceEventDefinition selectedRaceEvent;
    private RaceEventDefinition followedRaceEvent;

    private bool keepRaceEventInfoWhileGpsDialogOpen = false;

    [Header("GPS")]
    public WorldMapGPSPathfinder gpsPathfinder;
    public WorldMapGPSRenderer worldMapGpsRenderer;
    public MinimapGPSRoute minimapGpsRoute;
    public Transform playerTransform;
    private RaceEventDefinition hoveredRaceEvent;
    private RaceEventDefinition gpsDialogRaceEvent;

    [Header("GPS Optimization")]
    public float gpsRecalculateDistance = 80f;
    public float gpsTargetMoveTolerance = 2f;
    public float gpsSimplifyMinDistance = 8f;

    private Vector3 lastGpsPlayerPosition;
    private Vector3 lastGpsTargetPosition;

    private List<Vector3> currentGpsWorldPath = new();

    [Header("GPS Runtime Recalculate")]
    public bool autoRecalculateGps = true;
    public float gpsAutoRecalculateInterval = 0.15f;
    public float gpsAutoRecalculateMoveDistance = 4f;

    private float gpsAutoRecalculateTimer = 0f;

    [Header("GPS Smooth Follow")]
    public bool smoothGpsStartFollow = true;
    public float gpsTrimSearchRadius = 80f;
    public float gpsMinRenderRefreshInterval = 0.05f;

    private float gpsRenderRefreshTimer = 0f;

    [Header("GPS Visual Toggle")]
    public Slider gpsPathToggleSlider;
    public GameObject sliderGpsRoot;
    public bool gpsPathVisible = true;

    private const string GpsPathVisibleKey = "GPS_Path_Visible";

    [Header("GPS Destination Marker")] // WorldMap Icon GPS_Point
    public GpsDestinationMarkerController gpsDestinationMarker;
    private Transform currentGpsTargetPoint;

    [Header("GPS Arrival")]
    public float gpsArrivalDistance = 12f;

    [Header("GPS UI Destination Marker")] // Minimap Icon GPS_Point
    public GpsDestinationUIMarkerController gpsUiDestinationMarker;

    [Header("HUD GPS Arrow")] // Top Screen GPS Arrow Destination
    public GpsHudArrowController gpsHudArrow;

    [Header("Custom GPS Target")] 
    public bool allowCustomGpsTarget = true;
    public Transform customGpsTargetTransform;

    private bool hasCustomGpsTarget = false;
    private Vector3 customGpsTargetPosition;

    public enum GpsTravelMode
    {
        Vehicle,
        Pedestrian
    }

    [Header("GPS Travel Mode")]
    public GpsTravelMode currentGpsTravelMode;

    [Header("Pedestrian GPS")]
    public float navMeshSampleRadius = 20f;
    public int navMeshAreaMask = NavMesh.AllAreas;

    [Header("Dashed GPS Renderers")]
    public WorldMapGPSDashedRenderer worldMapDashedRenderer;
    public MinimapGPSDashedRoute minimapDashedRoute;
    private bool hasPendingCustomGpsTarget = false;
    private Vector3 pendingCustomGpsTargetPosition;

    public static bool IsOpen { get; private set; }

    void Start()
    {
        zoom = minZoom;

        if (zoomInButton != null)
            zoomInButton.onClick.AddListener(ZoomIn);

        if (zoomOutButton != null)
            zoomOutButton.onClick.AddListener(ZoomOut);

        if (worldMapCanvasGroup == null && worldMapRoot != null)
            worldMapCanvasGroup = worldMapRoot.GetComponent<CanvasGroup>();

        if (worldMapRoot != null)
            worldMapRoot.SetActive(true);

        if (followGpsButton != null)
            followGpsButton.onClick.AddListener(FollowSelectedGps);

        if (unfollowGpsButton != null)
            unfollowGpsButton.onClick.AddListener(UnfollowGps);

        gpsPathVisible = PlayerPrefs.GetInt(GpsPathVisibleKey, 1) == 1;

        if (gpsPathToggleSlider != null)
        {
            gpsPathToggleSlider.value = gpsPathVisible ? 1f : 0f;
            gpsPathToggleSlider.onValueChanged.AddListener(OnGpsPathToggleChanged);
        }

        if (gpsDialogRoot != null)
            CloseGpsDialog();

        ApplyZoom();
        SetOpen(false);
    }

    void OnDestroy()
    {
        if (zoomInButton != null)
            zoomInButton.onClick.RemoveListener(ZoomIn);

        if (zoomOutButton != null)
            zoomOutButton.onClick.RemoveListener(ZoomOut);

        if (followGpsButton != null)
            followGpsButton.onClick.RemoveListener(FollowSelectedGps);

        if (unfollowGpsButton != null)
            unfollowGpsButton.onClick.RemoveListener(UnfollowGps);

        if (gpsPathToggleSlider != null)
            gpsPathToggleSlider.onValueChanged.RemoveListener(OnGpsPathToggleChanged);
    }

    void Update()
    {
        if (PlayerInputHandler.Instance != null &&
            PlayerInputHandler.Instance.MapTogglePressedThisFrame)
        {
            Toggle();
            return;
        }

        if (IsGpsBlockedByRace())
        {
            if (followedRaceEvent != null || currentGpsWorldPath.Count > 0)
                UnfollowGps();

            if (gpsDialogRoot != null && gpsDialogRoot.activeSelf)
                CloseGpsDialog();
        }

        UpdateActiveGpsRouteRuntime();
        UpdateGpsVisualFollow();

        if (!opened)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            SetOpen(false);

        HandleDrag();
        HandleMouseWheelZoom();

        if (Input.GetMouseButtonDown(0))
        {
            if (gpsDialogRoot != null && gpsDialogRoot.activeSelf)
            {
                if (gpsDialogRect == null ||
                    !RectTransformUtility.RectangleContainsScreenPoint(gpsDialogRect, Input.mousePosition))
                {
                    CloseGpsDialog();
                }
            }
        }

        if (!IsGpsBlockedByRace() && Input.GetMouseButtonDown(1))
        {
            bool insideMap =
                mapViewport != null &&
                RectTransformUtility.RectangleContainsScreenPoint(mapViewport, Input.mousePosition);

            if (insideMap && !IsPointerOverRaceEventMarker())
            {
                if (allowCustomGpsTarget)
                    OpenCustomGpsDialog(Input.mousePosition);
                else
                    OpenGpsDialog(null, Input.mousePosition);
            }
        }
    }

    public void Toggle()
    {
        SetOpen(!opened);
    }

    public void SetOpen(bool value)
    {
        opened = value;
        IsOpen = opened;

        if (worldMapRoot != null)
            worldMapRoot.SetActive(true);

        if (worldMapCanvasGroup != null)
        {
            worldMapCanvasGroup.alpha = opened ? 1f : 0f;
            worldMapCanvasGroup.interactable = opened;
            worldMapCanvasGroup.blocksRaycasts = opened;
        }

        if (opened && !mapBuilt)
        {
            Canvas.ForceUpdateCanvases();

            if (roadRenderer != null)
                roadRenderer.Build();

            BuildRaceEventMarkers();

            mapBuilt = true;
        }

        PlayerInputHandler.SetGameplayBlocked(opened);
        MouseLook.IsLookLocked = opened;

        if (opened)
        {
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            previousFixedDeltaTime = Time.fixedDeltaTime;

            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0.02f;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            CloseGpsDialog();

            Time.timeScale = previousTimeScale;
            Time.fixedDeltaTime = previousFixedDeltaTime;

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    public void ZoomIn()
    {
        SetZoom(zoom + zoomStep);
    }

    public void ZoomOut()
    {
        SetZoom(zoom - zoomStep);
    }

    void SetZoom(float value)
    {
        zoom = Mathf.Clamp(value, minZoom, maxZoom);
        ApplyZoom();
    }

    void ApplyZoom()
    {
        if (mapImage != null)
            mapImage.localScale = Vector3.one * zoom;

        if (zoomValueText != null)
            zoomValueText.text = $"ZOOM: X{zoom:0.##}";

        if (zoom <= minZoom + 0.01f && mapImage != null)
            mapImage.anchoredPosition = Vector2.zero;

        ClampMapPosition();
    }

    void HandleDrag()
    {
        if (mapImage == null)
            return;

        if (zoom <= minZoom + 0.01f)
        {
            dragging = false;
            mapImage.anchoredPosition = Vector2.zero;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            dragging = true;
            dragStartMouse = Input.mousePosition;
            dragStartMapPos = mapImage.anchoredPosition;
        }

        if (Input.GetMouseButtonUp(0))
            dragging = false;

        if (!dragging)
            return;

        Vector2 mouseDelta = (Vector2)Input.mousePosition - dragStartMouse;
        mapImage.anchoredPosition = dragStartMapPos + mouseDelta;

        ClampMapPosition();
    }

    void ClampMapPosition()
    {
        if (mapViewport == null || mapImage == null)
            return;

        Vector2 viewportSize = mapViewport.rect.size;
        Vector2 mapSize = mapImage.rect.size * zoom;

        Vector2 pos = mapImage.anchoredPosition;

        float maxX = Mathf.Max(0f, (mapSize.x - viewportSize.x) * 0.5f);
        float maxY = Mathf.Max(0f, (mapSize.y - viewportSize.y) * 0.5f);

        pos.x = Mathf.Clamp(pos.x, -maxX, maxX);
        pos.y = Mathf.Clamp(pos.y, -maxY, maxY);

        if (mapSize.x <= viewportSize.x)
            pos.x = 0f;

        if (mapSize.y <= viewportSize.y)
            pos.y = 0f;

        mapImage.anchoredPosition = pos;
    }

    void BuildRaceEventMarkers()
    {
        if (raceEventMarkerPrefab == null || raceEventsIconsRoot == null || roadRenderer == null)
            return;

        for (int i = raceEventsIconsRoot.childCount - 1; i >= 0; i--)
            Destroy(raceEventsIconsRoot.GetChild(i).gameObject);

        if (raceEvents == null || raceEvents.Length == 0)
            raceEvents = FindObjectsByType<RaceEventDefinition>(FindObjectsSortMode.None);

        Canvas.ForceUpdateCanvases();

        for (int i = 0; i < raceEvents.Length; i++)
        {
            RaceEventDefinition def = raceEvents[i];

            if (def == null)
                continue;

            Transform markerPoint = def.worldMapMarkerPoint != null
                ? def.worldMapMarkerPoint
                : def.raceStartPoint;

            if (markerPoint == null)
                continue;

            WorldMapRaceEventMarkerUI marker =
                Instantiate(raceEventMarkerPrefab, raceEventsIconsRoot);

            RectTransform rect = marker.GetComponent<RectTransform>();

            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = roadRenderer.WorldToMapPositionPublic(markerPoint.position);
            }

            marker.Setup(def, this);
        }
    }

    public void HideRaceEventInfo(RaceEventDefinition def)
    {
        if (keepRaceEventInfoWhileGpsDialogOpen)
            return;

        if (hoveredRaceEvent != def)
            return;

        hoveredRaceEvent = null;

        if (raceEventInfoPanel != null)
            raceEventInfoPanel.Hide();
    }

    public void CloseGpsDialog()
    {
        keepRaceEventInfoWhileGpsDialogOpen = false;
        gpsDialogRaceEvent = null;
        hoveredRaceEvent = null;

        if (gpsDialogRoot != null)
            gpsDialogRoot.SetActive(false);

        if (raceEventInfoPanel != null)
            raceEventInfoPanel.Hide();
    }

    public void ShowRaceEventInfo(RaceEventDefinition def)
    {
        hoveredRaceEvent = def;

        if (raceEventInfoPanel != null)
            raceEventInfoPanel.Show(def);
    }

    public void OpenGpsDialog(RaceEventDefinition def, Vector2 screenPosition)
    {
        if (IsGpsBlockedByRace())
            return;

        gpsDialogRaceEvent = def;

        bool hasSelectedEvent = def != null;
        bool hasActiveGps = followedRaceEvent != null && currentGpsWorldPath != null && currentGpsWorldPath.Count >= 2;

        keepRaceEventInfoWhileGpsDialogOpen = hasSelectedEvent;

        if (hasSelectedEvent)
        {
            hoveredRaceEvent = def;

            if (raceEventInfoPanel != null)
                raceEventInfoPanel.Show(def);
        }
        else
        {
            hoveredRaceEvent = null;

            if (raceEventInfoPanel != null)
                raceEventInfoPanel.Hide();
        }

        if (gpsDialogRoot != null)
            gpsDialogRoot.SetActive(true);

        if (gpsDialogRect != null)
            gpsDialogRect.position = screenPosition;

        if (followGpsButton != null)
            followGpsButton.gameObject.SetActive(hasSelectedEvent);

        if (unfollowGpsButton != null)
            unfollowGpsButton.gameObject.SetActive(hasActiveGps);
    }

    void FollowSelectedGps()
    {
        Vector3 playerPos = GetGpsStartPosition();

        Transform targetPoint = null;
        Vector3 targetPos;
        string debugName;

        if (gpsDialogRaceEvent != null)
        {
            RaceEventDefinition targetEvent = gpsDialogRaceEvent;

            targetPoint = targetEvent.worldMapMarkerPoint != null
                ? targetEvent.worldMapMarkerPoint
                : targetEvent.raceStartPoint;

            if (targetPoint == null)
            {
                Debug.LogWarning("GPS: targetPoint == null");
                return;
            }

            targetPos = targetPoint.position;
            debugName = targetEvent.raceDisplayName;

            followedRaceEvent = targetEvent;
            hasCustomGpsTarget = false;
            currentGpsTargetPoint = targetPoint;
        }
        else if (hasPendingCustomGpsTarget)
        {
            customGpsTargetPosition = pendingCustomGpsTargetPosition;
            hasCustomGpsTarget = true;
            hasPendingCustomGpsTarget = false;

            targetPos = customGpsTargetPosition;
            debugName = "CUSTOM POINT";

            followedRaceEvent = null;

            if (customGpsTargetTransform != null)
            {
                customGpsTargetTransform.position = targetPos;
                currentGpsTargetPoint = customGpsTargetTransform;
            }
            else
            {
                Debug.LogWarning("GPS: customGpsTargetTransform is null");
                return;
            }
        }
        else
        {
            Debug.LogWarning("GPS: no target selected");
            return;
        }

        if (gpsPathfinder == null)
        {
            Debug.LogWarning("GPS: gpsPathfinder == null");
            return;
        }

        bool success = RecalculateGpsRoute(playerPos, targetPos);

        if (!success)
        {
            Debug.LogWarning("GPS: path empty or too short");

            followedRaceEvent = null;
            hasCustomGpsTarget = false;
            currentGpsTargetPoint = null;

            if (gpsDestinationMarker != null)
                gpsDestinationMarker.Hide();

            if (gpsUiDestinationMarker != null)
                gpsUiDestinationMarker.Hide();

            return;
        }

        if (gpsDestinationMarker != null)
            gpsDestinationMarker.Show(currentGpsTargetPoint);

        if (gpsUiDestinationMarker != null)
            gpsUiDestinationMarker.Show(currentGpsTargetPoint);

        CloseGpsDialog();

        Debug.Log("FOLLOW GPS: " + debugName);
    }

    List<Vector3> SimplifyGpsPath(List<Vector3> source, float minDistance)
    {
        List<Vector3> result = new();

        if (source == null || source.Count == 0)
            return result;

        result.Add(source[0]);

        Vector3 last = source[0];

        for (int i = 1; i < source.Count - 1; i++)
        {
            if (Vector3.Distance(last, source[i]) >= minDistance)
            {
                result.Add(source[i]);
                last = source[i];
            }
        }

        if (source.Count > 1)
            result.Add(source[source.Count - 1]);

        return result;
    }

    void UnfollowGps()
    {
        followedRaceEvent = null;
        hasCustomGpsTarget = false;
        customGpsTargetPosition = Vector3.zero;

        currentGpsWorldPath.Clear();
        currentGpsTargetPoint = null;

        hasPendingCustomGpsTarget = false;
        pendingCustomGpsTargetPosition = Vector3.zero;

        if (gpsDestinationMarker != null)
            gpsDestinationMarker.Hide();

        if (gpsUiDestinationMarker != null)
            gpsUiDestinationMarker.Hide();

        if (gpsHudArrow != null)
            gpsHudArrow.Clear();

        HideAllGpsPathRenderers();

        if (gpsDialogRoot != null)
            CloseGpsDialog();

        Debug.Log("UNFOLLOW GPS");
    }

    void HandleMouseWheelZoom()
    {
        if (mapViewport == null)
            return;

        if (!RectTransformUtility.RectangleContainsScreenPoint(mapViewport, Input.mousePosition))
            return;

        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        SetZoom(zoom + scroll * zoomStep);
    }

    bool IsPointerOverRaceEventMarker()
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData data = new PointerEventData(EventSystem.current);
        data.position = Input.mousePosition;

        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(data, results);

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject.GetComponentInParent<WorldMapRaceEventMarkerUI>() != null)
                return true;
        }

        return false;
    }

    Vector3 GetGpsStartPosition()
    {
        if (CarInteraction.ActiveVehicleTransform != null)
            return CarInteraction.ActiveVehicleTransform.position;

        if (playerTransform != null)
            return playerTransform.position;

        return Vector3.zero;
    }

    void UpdateActiveGpsRouteRuntime()
    {
        if (!autoRecalculateGps)
            return;

        if (followedRaceEvent == null && !hasCustomGpsTarget)
            return;

        if (currentGpsTargetPoint != null)
        {
            float dist = Vector3.Distance(GetGpsStartPosition(), currentGpsTargetPoint.position);

            if (dist <= gpsArrivalDistance)
            {
                UnfollowGps();
                Debug.Log("GPS: destination reached");
                return;
            }
        }

        if (gpsPathfinder == null)
            return;

        gpsAutoRecalculateTimer += Time.unscaledDeltaTime;

        if (gpsAutoRecalculateTimer < gpsAutoRecalculateInterval)
            return;

        gpsAutoRecalculateTimer = 0f;

        Vector3 currentStartPos = GetGpsStartPosition();

        if (Vector3.Distance(currentStartPos, lastGpsPlayerPosition) < gpsAutoRecalculateMoveDistance)
            return;

        Vector3 targetPos;

        if (hasCustomGpsTarget)
        {
            targetPos = customGpsTargetPosition;
        }
        else
        {
            Transform targetPoint = followedRaceEvent.worldMapMarkerPoint != null
                ? followedRaceEvent.worldMapMarkerPoint
                : followedRaceEvent.raceStartPoint;

            if (targetPoint == null)
                return;

            targetPos = targetPoint.position;
        }

        RecalculateGpsRoute(currentStartPos, targetPos);
    }

    bool RecalculateGpsRoute(Vector3 startPos, Vector3 targetPos)
    {
        bool vehicleMode = IsGpsUsingVehicleMode();
        currentGpsTravelMode = vehicleMode ? GpsTravelMode.Vehicle : GpsTravelMode.Pedestrian;

        if (vehicleMode)
        {
            currentGpsWorldPath = gpsPathfinder.FindPath(startPos, targetPos);
        }
        else
        {
            if (!TryBuildPedestrianNavMeshPath(startPos, targetPos, out currentGpsWorldPath))
                return false;
        }

        if (currentGpsWorldPath == null || currentGpsWorldPath.Count < 2)
            return false;

        lastGpsPlayerPosition = startPos;
        lastGpsTargetPosition = targetPos;

        if (gpsHudArrow != null)
            gpsHudArrow.SetPath(currentGpsWorldPath);

        RenderGpsPath(currentGpsWorldPath);

        return true;
    }

    bool TryBuildPedestrianNavMeshPath(Vector3 startPos, Vector3 targetPos, out List<Vector3> result)
    {
        result = new List<Vector3>();

        if (!NavMesh.SamplePosition(startPos, out NavMeshHit startHit, navMeshSampleRadius, navMeshAreaMask))
            return false;

        if (!NavMesh.SamplePosition(targetPos, out NavMeshHit targetHit, navMeshSampleRadius, navMeshAreaMask))
            return false;

        NavMeshPath navPath = new NavMeshPath();

        bool ok = NavMesh.CalculatePath(
            startHit.position,
            targetHit.position,
            navMeshAreaMask,
            navPath
        );

        if (!ok || navPath.status == NavMeshPathStatus.PathInvalid)
            return false;

        if (navPath.corners == null || navPath.corners.Length < 2)
            return false;

        result.AddRange(navPath.corners);
        return true;
    }

    void UpdateGpsVisualFollow()
    {
        if (!smoothGpsStartFollow)
            return;

        if (followedRaceEvent == null && !hasCustomGpsTarget)
            return;

        if (!gpsPathVisible)
            return;

        if (currentGpsWorldPath == null || currentGpsWorldPath.Count < 2)
            return;

        if (worldMapGpsRenderer == null && minimapGpsRoute == null)
            return;

        gpsRenderRefreshTimer += Time.unscaledDeltaTime;

        if (gpsRenderRefreshTimer < gpsMinRenderRefreshInterval)
            return;

        gpsRenderRefreshTimer = 0f;

        Vector3 playerPos = GetGpsStartPosition();

        List<Vector3> trimmedPath = BuildTrimmedGpsPath(playerPos, currentGpsWorldPath);

        if (trimmedPath == null || trimmedPath.Count < 2)
            return;

        List<Vector3> simplifiedPath = SimplifyGpsPath(trimmedPath, gpsSimplifyMinDistance);

        RenderGpsPath(simplifiedPath);
    }

    List<Vector3> BuildTrimmedGpsPath(Vector3 playerPos, List<Vector3> path)
    {
        List<Vector3> result = new();

        if (path == null || path.Count < 2)
            return result;

        int bestIndex = 0;
        Vector3 bestPoint = path[0];
        float bestSqr = float.MaxValue;

        float searchRadiusSqr = gpsTrimSearchRadius * gpsTrimSearchRadius;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 a = path[i];
            Vector3 b = path[i + 1];

            Vector3 p = ClosestPointOnSegment(a, b, playerPos);
            float sqr = (playerPos - p).sqrMagnitude;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestPoint = p;
                bestIndex = i;
            }
        }

        result.Add(bestPoint);

        for (int i = bestIndex + 1; i < path.Count; i++)
            result.Add(path[i]);

        return result;
    }

    Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;

        float t = Vector3.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude);
        t = Mathf.Clamp01(t);

        return a + ab * t;
    }

    bool IsGpsBlockedByRace()
    {
        if (CarRaceManager.IsRaceStarting || CarRaceManager.IsRaceLoading)
            return true;

        if (CarRaceManager.ActiveRaceManager != null)
        {
            var phase = CarRaceManager.ActiveRaceManager.racePhase;

            return phase == CarRaceManager.RacePhase.Transition ||
                   phase == CarRaceManager.RacePhase.Countdown ||
                   phase == CarRaceManager.RacePhase.Racing;
        }

        return false;
    }

    void OnGpsPathToggleChanged(float value)
    {
        gpsPathVisible = value >= 0.5f;

        PlayerPrefs.SetInt(GpsPathVisibleKey, gpsPathVisible ? 1 : 0);
        PlayerPrefs.Save();

        RefreshGpsPathVisibility();
    }

    void RefreshGpsPathVisibility()
    {
        if (!gpsPathVisible)
        {
            HideAllGpsPathRenderers();
            return;
        }

        if (currentGpsWorldPath != null && currentGpsWorldPath.Count >= 2)
        {
            List<Vector3> trimmedPath = BuildTrimmedGpsPath(GetGpsStartPosition(), currentGpsWorldPath);
            RenderGpsPath(trimmedPath);
        }
    }

    void OpenCustomGpsDialog(Vector2 screenPosition)
    {
        if (IsGpsBlockedByRace())
            return;

        if (roadRenderer == null || mapImage == null)
            return;

        Vector2 localPoint;

        bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapImage,
            screenPosition,
            null,
            out localPoint
        );

        if (!ok)
            return;

        pendingCustomGpsTargetPosition = roadRenderer.MapToWorldPositionPublic(localPoint, 0f);
        hasPendingCustomGpsTarget = true;

        gpsDialogRaceEvent = null;

        keepRaceEventInfoWhileGpsDialogOpen = false;
        hoveredRaceEvent = null;

        if (raceEventInfoPanel != null)
            raceEventInfoPanel.Hide();

        if (gpsDialogRoot != null)
            gpsDialogRoot.SetActive(true);

        if (gpsDialogRect != null)
            gpsDialogRect.position = screenPosition;

        bool hasActiveGps = followedRaceEvent != null || hasCustomGpsTarget || currentGpsWorldPath.Count >= 2;

        if (followGpsButton != null)
            followGpsButton.gameObject.SetActive(true);

        if (unfollowGpsButton != null)
            unfollowGpsButton.gameObject.SetActive(hasActiveGps);
    }

    bool IsGpsUsingVehicleMode()
    {
        return CarInteraction.ActiveVehicleTransform != null;
    }

    void RenderGpsPath(List<Vector3> path)
    {
        if (!gpsPathVisible)
        {
            HideAllGpsPathRenderers();
            return;
        }

        List<Vector3> simplifiedPath = SimplifyGpsPath(path, gpsSimplifyMinDistance);

        if (currentGpsTravelMode == GpsTravelMode.Vehicle)
        {
            if (worldMapGpsRenderer != null)
                worldMapGpsRenderer.ShowPath(simplifiedPath);

            if (minimapGpsRoute != null)
                minimapGpsRoute.ShowRoute(simplifiedPath);

            if (worldMapDashedRenderer != null)
                worldMapDashedRenderer.HidePath();

            if (minimapDashedRoute != null)
                minimapDashedRoute.HideRoute();
        }
        else
        {
            if (worldMapGpsRenderer != null)
                worldMapGpsRenderer.HidePath();

            if (minimapGpsRoute != null)
                minimapGpsRoute.HideRoute();

            if (worldMapDashedRenderer != null)
                worldMapDashedRenderer.ShowPath(simplifiedPath);

            if (minimapDashedRoute != null)
                minimapDashedRoute.ShowRoute(simplifiedPath);
        }
    }

    void HideAllGpsPathRenderers()
    {
        if (worldMapGpsRenderer != null)
            worldMapGpsRenderer.HidePath();

        if (minimapGpsRoute != null)
            minimapGpsRoute.HideRoute();

        if (worldMapDashedRenderer != null)
            worldMapDashedRenderer.HidePath();

        if (minimapDashedRoute != null)
            minimapDashedRoute.HideRoute();
    }

}