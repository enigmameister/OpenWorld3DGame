using System.Collections.Generic;
using UnityEngine;

public class MinimapRaceRouteOverlayController : MonoBehaviour
{
    public Transform racingObjectsRoot;

    private readonly Dictionary<string, GameObject> objectsByName = new();

    void Awake()
    {
        RebuildCache();
        HideAll();
    }

    [ContextMenu("REBUILD CACHE")]
    public void RebuildCache()
    {
        objectsByName.Clear();

        if (racingObjectsRoot == null)
            return;

        CacheChildren(racingObjectsRoot);
    }

    void CacheChildren(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            objectsByName[child.name] = child.gameObject;
            child.gameObject.SetActive(false);

            CacheChildren(child);
        }
    }

    public void ShowRoute(RaceRoute route)
    {
        HideAll();

        if (route == null)
            return;

        if (objectsByName.Count == 0)
            RebuildCache();

        for (int i = 0; i < route.Count; i++)
        {
            RoadSegment segment = route.GetSegment(i);

            if (segment == null)
                continue;

            ShowByName(segment.name);

            if (segment.startNode != null)
                ShowByName(segment.startNode.name);

            if (segment.endNode != null)
                ShowByName(segment.endNode.name);
        }
    }

    void ShowByName(string objectName)
    {
        if (objectsByName.TryGetValue(objectName, out GameObject obj) && obj != null)
            obj.SetActive(true);
        else
            Debug.LogWarning($"[MinimapRoute] Brak obiektu minimapy: {objectName}", this);
    }

    public void HideAll()
    {
        foreach (var pair in objectsByName)
        {
            if (pair.Value != null)
                pair.Value.SetActive(false);
        }
    }
}