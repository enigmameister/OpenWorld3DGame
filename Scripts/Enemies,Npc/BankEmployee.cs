using UnityEngine;

public class BankEmployee : MonoBehaviour
{
    public enum Role
    {
        Teller,
        Manager,
        Security
    }

    public DialogueGraph dialogueGraph;
    public string EmployeeName => employeeName; // albo public field

    [Header("Identity")]
    public string employeeName = "BANK TELLER";
    public Role role = Role.Teller;

    [Header("Interaction")]
    public float facePlayerSpeedDeg = 720f;

    [Header("Work hours (game time) - later")]
    public int openHour = 8;
    public int closeHour = 16;

    public bool IsWorkingNow() => GameTime.IsTimeBetweenHours(openHour, closeHour);

}
