using System.Collections.Generic;
using UnityEngine;

public class MinimapAIIconManager : MonoBehaviour
{
    [Header("Prefab")]
    public MinimapIconFollowTarget aiIconPrefab;

    [Header("Refs")]
    public Transform iconsRoot;
    public Camera minimapCamera;
    public RectTransform radarRect;

    [Header("Settings")]
    public float aiIconScale = 0.65f;

    [Header("Colors")]
    public Color[] aiColors =
    {
        Color.yellow,
        Color.cyan,
        Color.green,
        Color.magenta
    };

    private readonly List<MinimapIconFollowTarget> spawnedIcons = new();

    public void RegisterAI(Transform aiTarget, int index)
    {
        if (aiIconPrefab == null || aiTarget == null)
            return;

        Transform parent = iconsRoot != null ? iconsRoot : transform;

        MinimapIconFollowTarget icon = Instantiate(aiIconPrefab, parent);

        icon.SetTarget(aiTarget);
        icon.minimapCamera = minimapCamera;
        icon.radarRect = radarRect;

        icon.transform.localScale = Vector3.one * aiIconScale;

        if (aiColors != null && aiColors.Length > 0)
            icon.SetColor(aiColors[index % aiColors.Length]);

        spawnedIcons.Add(icon);
    }

    public void ClearIcons()
    {
        for (int i = spawnedIcons.Count - 1; i >= 0; i--)
        {
            if (spawnedIcons[i] != null)
                Destroy(spawnedIcons[i].gameObject);
        }

        spawnedIcons.Clear();
    }
}