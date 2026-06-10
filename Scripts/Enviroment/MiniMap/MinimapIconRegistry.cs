using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MinimapIconRegistry : MonoBehaviour
{
    [Header("UI Parents")]
    public Transform raceEventsRoot;
    public Transform missionsRoot;
    public Transform shopsRoot;
    public Transform garageRoot;

    [Header("Prefab")]
    public GameObject minimapIconPrefab;

    [Header("Minimap")]
    public Camera minimapCamera;
    public RectTransform radarRect;

    void Start()
    {
        RegisterAllMarkers();
    }

    void RegisterAllMarkers()
    {
        MinimapWorldMarker[] markers = FindObjectsByType<MinimapWorldMarker>(FindObjectsSortMode.None);

        foreach (var marker in markers)
        {
            CreateIcon(marker);
        }
    }

    void CreateIcon(MinimapWorldMarker marker)
    {
        Transform parent = GetCategoryParent(marker.category);

        if (parent == null)
            return;

        GameObject iconObj = Instantiate(minimapIconPrefab, parent);

        MinimapIconFollowTarget follow =
            iconObj.GetComponent<MinimapIconFollowTarget>();

        if (follow != null)
        {
            follow.target = marker.transform;

            follow.minimapCamera = minimapCamera;
            follow.radarRect = radarRect;
        }

        Image img = iconObj.GetComponentInChildren<Image>();

        if (img != null)
        {
            img.sprite = marker.iconSprite;
            img.color = marker.iconColor;
        }
    }

    Transform GetCategoryParent(MinimapIconCategory category)
    {
        switch (category)
        {
            case MinimapIconCategory.RaceEvent:
                return raceEventsRoot;

            case MinimapIconCategory.Mission:
                return missionsRoot;

            case MinimapIconCategory.Shop:
                return shopsRoot;

            case MinimapIconCategory.Garage:
                return garageRoot;
        }

        return raceEventsRoot;
    }
}