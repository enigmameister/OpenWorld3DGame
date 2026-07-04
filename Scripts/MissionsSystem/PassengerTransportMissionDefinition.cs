using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Missions/Passenger Transport Mission Definition")]
public class PassengerTransportMissionDefinition : MissionDefinition
{
    [Header("Passenger")]
    public string passengerDisplayName = "Passenger";

    [Tooltip("If true, runtime can use NPCMissionGiver.NpcName from passenger NPC instead of this text.")]
    public bool usePassengerNpcName = true;

    [Header("Reward")]
    [Min(0)]
    public int rewardMoney = 0;

    public InventoryItemData rewardItem;

    [Min(1)]
    public int rewardItemCount = 1;

    [Tooltip("Optional text shown in UI, for example: $500, Glock, Safehouse Key, $250 + Glock.")]
    public string rewardDisplayText = "";

    [Tooltip("If true, reward is given immediately after passenger reaches destination. If false, player must return to mission giver.")]
    public bool rewardAtDestination = false;

    [Header("GPS Labels")]
    public string pickupGpsLabelFormat = "Pick up {PASSENGER}";
    public string destinationGpsLabelFormat = "Drive {PASSENGER} to the destination";
    public string returnGpsLabelFormat = "Return to {GIVER}";

    [Header("Objective Texts")]
    public string needCarObjectiveFormat = "Find a car.";
    public string driveToPassengerObjectiveFormat = "Drive to {PASSENGER}.";
    public string waitForPassengerObjectiveFormat = "Stop near {PASSENGER} and wait for them to get in.";
    public string driveToDestinationObjectiveFormat = "Drive {PASSENGER} to the marked location.";
    public string playerLeftCarObjectiveFormat = "Get back in the car to continue.";
    public string passengerWalkingAwayObjectiveFormat = "Wait until {PASSENGER} reaches the safe place.";
    public string returnObjectiveFormat = "Return to {GIVER} for your reward.";

    [Header("Communicates")]
    [TextArea(2, 5)]
    public string acceptTextFormat = "Find a car first.";

    [TextArea(2, 5)]
    public string enteredCarTextFormat = "Drive to {PASSENGER}.";

    [TextArea(2, 5)]
    public string passengerBoardedTextFormat = "{PASSENGER} got in. Drive to the marked location.";

    [TextArea(2, 5)]
    public string playerLeftCarTextFormat = "Get back in the car to continue.";

    [TextArea(2, 5)]
    public string playerReturnedToCarTextFormat = "Continue driving to the destination.";

    [TextArea(2, 5)]
    public string playerNeedsAnotherCarTextFormat = "Find another car to continue.";

    [TextArea(2, 5)]
    public string arrivedTextFormat = "You arrived. Let {PASSENGER} out.";

    [TextArea(2, 5)]
    public string returnTextFormat = "Return to {GIVER} for your reward.";

    [TextArea(2, 5)]
    public string rewardTextAfterClaimFormat = "{GIVER} gave you your reward.";

    [TextArea(2, 5)]
    public string noRewardTextAfterClaimFormat = "Mission completed.";

    [Header("Ride Dialogue")]
    public PassengerRideDialogueLine[] rideLines;

    [Header("Special Ride Lines")]
    [TextArea(2, 5)]
    public string passengerLineWhenPlayerLeavesCar = "Why are you getting out? We are not done yet.";
}

[Serializable]
public class PassengerRideDialogueLine
{
    public PassengerDialogueSpeaker speaker = PassengerDialogueSpeaker.Passenger;

    [Tooltip("Used only when speaker is Custom.")]
    public string customSpeakerName;

    [TextArea(2, 4)]
    public string text;

    public float delayAfterPrevious = 3f;
}

public enum PassengerDialogueSpeaker
{
    Passenger,
    Player,
    Giver,
    Custom
}