using UnityEngine;

public class MinimapWorldMarker : MonoBehaviour
{
    public Sprite iconSprite;
    public Color iconColor = Color.white;

    public MinimapIconCategory category = MinimapIconCategory.RaceEvent;
}

public enum MinimapIconCategory
{
    RaceEvent,
    Mission,
    Shop,
    Garage
}
