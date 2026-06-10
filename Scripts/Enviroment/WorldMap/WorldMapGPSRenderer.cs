using System.Collections.Generic;
using UnityEngine;

public class WorldMapGPSRenderer : MonoBehaviour
{
    public WorldMapRoadRenderer roadRenderer;
    public WorldMapRoadGraphic gpsGraphic;

    public Color gpsColor = Color.yellow;
    public float gpsWidth = 6f;

    public void ShowPath(List<Vector3> worldPath)
    {
        if (roadRenderer == null || gpsGraphic == null || worldPath == null || worldPath.Count < 2)
            return;

        List<Vector2> uiPath = new();

        for (int i = 0; i < worldPath.Count; i++)
            uiPath.Add(roadRenderer.WorldToMapPositionPublic(worldPath[i]));

        List<List<Vector2>> paths = new();
        paths.Add(uiPath);

        gpsGraphic.color = gpsColor;
        gpsGraphic.lineWidth = gpsWidth;
        gpsGraphic.SetPaths(paths);
        gpsGraphic.gameObject.SetActive(true);
    }

    public void HidePath()
    {
        if (gpsGraphic == null)
            return;

        gpsGraphic.SetPaths(new List<List<Vector2>>());
        gpsGraphic.gameObject.SetActive(false);
    }
}