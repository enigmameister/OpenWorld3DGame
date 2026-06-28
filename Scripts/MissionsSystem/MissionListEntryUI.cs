using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MissionListEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Refs")]
    [SerializeField] private TMP_Text missionNameText;
    [SerializeField] private Button button;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(1f, 0.55f, 0f);
    [SerializeField] private Color readyToClaimColor = new Color(1f, 0.82f, 0.15f);
    [SerializeField] private Color repeatableCompletedColor = new Color(0.25f, 0.6f, 1f);
    [SerializeField] private Color lockedColor = Color.gray;

    private NPCMissionLink mission;
    private NPCMissionListUI owner;
    private Color baseColor;
    private bool interactable = true;

    private void Awake()
    {
        if (missionNameText == null)
            missionNameText = GetComponentInChildren<TMP_Text>(true);

        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(Click);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(Click);
    }

    public void Setup(NPCMissionLink link, NPCMissionListUI listOwner, int index, MissionRuntimeState state)
    {
        mission = link;
        owner = listOwner;

        string title = "MISSION";

        if (link != null && link.definition != null && !string.IsNullOrWhiteSpace(link.definition.title))
            title = link.definition.title;

        if (missionNameText != null)
            missionNameText.text = $"{index + 1}. {title}";

        interactable = true;
        baseColor = ResolveColor(link, state);
        ApplyColor(baseColor);

        if (button != null)
            button.interactable = interactable;
    }

    private Color ResolveColor(NPCMissionLink link, MissionRuntimeState state)
    {
        if (link == null || link.definition == null)
            return lockedColor;

        if (state == MissionRuntimeState.ReadyToClaim)
            return readyToClaimColor;

        if (state == MissionRuntimeState.RewardClaimed && link.definition.repeatable)
            return repeatableCompletedColor;

        if (state == MissionRuntimeState.RewardClaimed && !link.definition.repeatable)
            return lockedColor;

        return normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!interactable)
            return;

        ApplyColor(hoverColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ApplyColor(baseColor);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Click();
    }

    private void Click()
    {
        if (!interactable)
            return;

        if (owner != null && mission != null)
            owner.SelectMission(mission);
    }

    private void ApplyColor(Color color)
    {
        if (missionNameText != null)
            missionNameText.color = color;
    }
}