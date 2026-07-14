using UnityEngine;

public class CityHallEmployee : MonoBehaviour
{
    public enum Role
    {
        Receptionist,
        CitizenIdClerk,
        LicenseClerk,
        LostAndFoundClerk,
        FilePickupClerk
    }

    [Header("Identity")]
    [SerializeField] private string employeeName = "CITY HALL EMPLOYEE";
    [SerializeField] private Role role = Role.Receptionist;

    [Header("Dialogue")]
    [SerializeField] private DialogueGraph dialogueGraph;

    [Header("Working Hours")]
    [SerializeField, Range(0, 23)] private int openHour = 7;
    [SerializeField, Range(1, 24)] private int closeHour = 17;

    [Header("Interaction")]
    [SerializeField] private float facePlayerSpeedDeg = 720f;

    public string EmployeeName => employeeName;
    public Role EmployeeRole => role;
    public DialogueGraph DialogueGraph => dialogueGraph;
    public int OpenHour => openHour;
    public int CloseHour => closeHour;
    public float FacePlayerSpeedDeg => facePlayerSpeedDeg;

    public bool IsWorkingNow()
    {
        return GameTime.IsTimeBetweenHours(openHour, closeHour);
    }

    public CityHallVisitType GetHandledVisitType()
    {
        return role switch
        {
            Role.CitizenIdClerk => CityHallVisitType.CitizenId,
            Role.LicenseClerk => CityHallVisitType.DrivingLicense,
            Role.LostAndFoundClerk => CityHallVisitType.LostAndFound,
            Role.FilePickupClerk => CityHallVisitType.FilePickup,
            _ => CityHallVisitType.None
        };
    }
}