using UnityEngine;

[CreateAssetMenu(menuName = "Missions/Delivery Mission Definition")]
public class DeliveryMissionDefinition : MissionDefinition
{
    [Header("Delivery Item")]
    public InventoryItemData packageItem;

    [Header("Reward")]
    public int rewardMoney = 500;
    public bool rewardAtReceiver = false;

    [Header("Delivery Text")]
    [TextArea(2, 5)]
    public string acceptCommunicateText = "Take the package to Ralph.";

    [TextArea(2, 5)]
    public string deliveredCommunicateText = "Package delivered.";

    [TextArea(2, 5)]
    public string returnCommunicateText = "Return to Fredo for your reward.";

    [Header("Receiver Dialogue Graphs")]
    public DialogueGraph receiverWaitingGraph;
    public DialogueGraph receiverMissingPackageGraph;
    public DialogueGraph receiverDeliveredGraph;
}