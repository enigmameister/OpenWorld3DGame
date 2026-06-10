#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

[ExecuteAlways]
public class RoadTerrainConformer : MonoBehaviour
{
    [Header("References")]
    public SplineContainer spline;
    public Terrain[] terrains;

    [Header("Road Shape")]
    public float roadHalfWidth = 6f;
    public float sideBlendWidth = 10f;
    public float heightOffsetBelowRoad = -0.08f;

    [Header("Sampling")]
    [Range(32, 2000)] public int splineSamples = 600;
    public float sampleStepAcross = 1f;

    [Header("Brush")]
    public int flattenRadius = 5;
    public int blendRadius = 14;
    [Range(0.05f, 1f)] public float brushFalloff = 0.35f;

    [Header("Safety")]
    public bool autoFindTerrains = true;

    [ContextMenu("CONFORM TERRAIN TO ROAD")]
    public void ConformTerrainToRoad()
    {
        if (spline == null)
            spline = GetComponent<SplineContainer>();

        if ((terrains == null || terrains.Length == 0) && autoFindTerrains)
            terrains = Terrain.activeTerrains;

        if (spline == null || terrains == null || terrains.Length == 0)
        {
            Debug.LogWarning("Brakuje SplineContainer albo Terrainów.");
            return;
        }

        foreach (Terrain terrain in terrains)
        {
            if (terrain == null)
                continue;

            ConformSingleTerrain(terrain);
        }

        Debug.Log("Terrainy dopasowane do drogi.");
    }

    void ConformSingleTerrain(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

#if UNITY_EDITOR
        Undo.RegisterCompleteObjectUndo(data, "Conform Terrain To Road");
#endif

        int resolution = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, resolution, resolution);

        float terrainWidth = data.size.x;
        float terrainLength = data.size.z;
        float terrainHeight = data.size.y;

        List<RoadSample> samples = new();

        for (int s = 0; s <= splineSamples; s++)
        {
            float t = s / (float)splineSamples;

            SplineUtility.Evaluate(
                spline.Spline,
                t,
                out float3 localPos,
                out float3 localDir,
                out float3 localUp
            );

            Vector3 center = spline.transform.TransformPoint((Vector3)localPos);
            Vector3 forward = spline.transform.TransformDirection((Vector3)localDir);
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.001f)
                continue;

            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float totalWidth = roadHalfWidth + sideBlendWidth;

            for (float offset = -totalWidth; offset <= totalWidth; offset += Mathf.Max(0.1f, sampleStepAcross))
            {
                Vector3 worldPos = center + right * offset;

                float normalizedX = (worldPos.x - terrainPos.x) / terrainWidth;
                float normalizedZ = (worldPos.z - terrainPos.z) / terrainLength;

                if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f)
                    continue;

                int hx = Mathf.RoundToInt(normalizedX * (resolution - 1));
                int hz = Mathf.RoundToInt(normalizedZ * (resolution - 1));

                float targetWorldY = center.y + heightOffsetBelowRoad;
                float targetHeight01 = Mathf.InverseLerp(
                    terrainPos.y,
                    terrainPos.y + terrainHeight,
                    targetWorldY
                );

                targetHeight01 = Mathf.Clamp01(targetHeight01);

                float distFromCenter = Mathf.Abs(offset);

                samples.Add(new RoadSample
                {
                    x = hx,
                    z = hz,
                    targetHeight = targetHeight01,
                    distanceFromCenter = distFromCenter
                });
            }
        }

        samples.Sort((a, b) => b.targetHeight.CompareTo(a.targetHeight));

        foreach (RoadSample sample in samples)
        {
            if (sample.distanceFromCenter <= roadHalfWidth)
            {
                ApplyBrush(
                    heights,
                    sample.x,
                    sample.z,
                    sample.targetHeight,
                    flattenRadius,
                    true
                );
            }
            else
            {
                ApplyBrush(
                    heights,
                    sample.x,
                    sample.z,
                    sample.targetHeight,
                    blendRadius,
                    false
                );
            }
        }

        data.SetHeights(0, 0, heights);

#if UNITY_EDITOR
        EditorUtility.SetDirty(data);
#endif
    }

    void ApplyBrush(
        float[,] heights,
        int centerX,
        int centerZ,
        float targetHeight,
        int radius,
        bool hardFlatten
    )
    {
        int width = heights.GetLength(1);
        int height = heights.GetLength(0);

        int sqrRadius = radius * radius;

        for (int z = -radius; z <= radius; z++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int sqrDist = x * x + z * z;

                if (sqrDist > sqrRadius)
                    continue;

                int hx = centerX + x;
                int hz = centerZ + z;

                if (hx < 0 || hz < 0 || hx >= width || hz >= height)
                    continue;

                float dist = Mathf.Sqrt(sqrDist);
                float t = dist / Mathf.Max(1f, radius);

                float weight = hardFlatten
                    ? 1f
                    : Mathf.Exp(-t * t / brushFalloff);

                heights[hz, hx] = Mathf.Lerp(
                    heights[hz, hx],
                    targetHeight,
                    weight
                );
            }
        }
    }

    struct RoadSample
    {
        public int x;
        public int z;
        public float targetHeight;
        public float distanceFromCenter;
    }
}