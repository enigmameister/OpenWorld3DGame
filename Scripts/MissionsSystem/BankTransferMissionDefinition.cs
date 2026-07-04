using UnityEngine;

[CreateAssetMenu(menuName = "Missions/Bank Transfer Mission Definition")]
public class BankTransferMissionDefinition : MissionDefinition
{
    [Header("Bank Transfer")]
    public int targetAccountId = 1993;
    public int requiredAmount = 1500;

    [Header("Target Account")]
    public bool createTargetAccountIfMissing = true;

    [Header("Reward")]
    public InventoryItemData rewardItem;
    public int rewardItemCount = 1;

    [Header("Communicates")]
    [TextArea(2, 5)]
    public string acceptText = "Open a bank account and transfer $1500 to account 1993.";

    [TextArea(2, 5)]
    public string accountMissingText = "Open a bank account first.";

    [TextArea(2, 5)]
    public string transferDoneText = "Transfer complete. Return to Fredo.";

    [TextArea(2, 5)]
    public string rewardTextAfterClaim = "Fredo gave you a Glock.";
}