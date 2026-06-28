using System.Collections.Generic;
using UnityEngine;

public class MinimapSpeedTrapIconManager : MonoBehaviour
{
    [Header("Refs")]
    public Camera minimapCamera;
    public RectTransform minimapRect;
    public RectTransform iconParent;
    public RectTransform speedTrapIconPrefab;

    [Header("Clamp")]
    public bool clampToCircle = true;
    public float edgePadding = 12f;

    private readonly List<Entry> entries = new();

    class Entry
    {
        public Transform target;
        public RectTransform icon;
    }

    public void ClearIcons()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].icon != null)
                Destroy(entries[i].icon.gameObject);
        }

        entries.Clear();
    }

    public void RegisterSpeedTrap(Transform target)
    {
        if (target == null || speedTrapIconPrefab == null)
            return;

        Transform parent = iconParent != null ? iconParent : minimapRect;

        RectTransform icon = Instantiate(speedTrapIconPrefab, parent);
        icon.gameObject.SetActive(true);

        entries.Add(new Entry
        {
            target = target,
            icon = icon
        });
    }

    void LateUpdate()
    {
        if (minimapCamera == null || minimapRect == null)
            return;

        Vector2 rectSize = minimapRect.rect.size;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];

            if (e == null || e.target == null || e.icon == null)
                continue;

            Vector3 viewport = minimapCamera.WorldToViewportPoint(e.target.position);

            Vector2 uiPos = new Vector2(
                (viewport.x - 0.5f) * rectSize.x,
                (viewport.y - 0.5f) * rectSize.y
            );

            if (clampToCircle)
            {
                float radius = Mathf.Min(rectSize.x, rectSize.y) * 0.5f - edgePadding;

                if (uiPos.magnitude > radius)
                    uiPos = uiPos.normalized * radius;
            }
            else
            {
                float halfX = rectSize.x * 0.5f - edgePadding;
                float halfY = rectSize.y * 0.5f - edgePadding;

                uiPos.x = Mathf.Clamp(uiPos.x, -halfX, halfX);
                uiPos.y = Mathf.Clamp(uiPos.y, -halfY, halfY);
            }

            e.icon.anchoredPosition = uiPos;
        }
    }
}