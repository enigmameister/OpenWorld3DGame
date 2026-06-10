using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class WorldMapRaceEventMarkerUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image iconImage;

    private RaceEventDefinition raceEvent;
    private WorldMapUI worldMapUI;

    void Awake()
    {
        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);
    }

    public void Setup(RaceEventDefinition definition, WorldMapUI owner)
    {
        raceEvent = definition;
        worldMapUI = owner;

        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);

        if (iconImage == null)
            return;

        if (raceEvent.minimapIconSource != null)
        {
            iconImage.sprite = raceEvent.minimapIconSource.iconSprite;
            iconImage.color = raceEvent.minimapIconSource.iconColor;
        }

        iconImage.enabled = iconImage.sprite != null;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (worldMapUI != null && raceEvent != null)
            worldMapUI.ShowRaceEventInfo(raceEvent);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (worldMapUI != null && raceEvent != null)
            worldMapUI.HideRaceEventInfo(raceEvent);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right && worldMapUI != null && raceEvent != null)
            worldMapUI.OpenGpsDialog(raceEvent, eventData.position);
    }
}