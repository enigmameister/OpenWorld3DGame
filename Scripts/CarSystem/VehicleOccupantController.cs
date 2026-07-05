using UnityEngine;

[DisallowMultipleComponent]
public class VehicleOccupantController : MonoBehaviour
{
    [Header("Vehicle")]
    [SerializeField] private GameObject vehicleObject;

    [Header("Player")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private GameObject playerVisualRoot;
    [SerializeField] private CharacterController playerCC;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerStats playerStats;

    [Header("Seat / Exit")]
    [SerializeField] private Transform seatPosition;
    [SerializeField] private Transform exitPoint;

    public GameObject PlayerObject => playerObject;
    public PlayerStats PlayerStats => playerStats;
    public PlayerMovement PlayerMovement => playerMovement;
    public CharacterController PlayerCharacterController => playerCC;

    public bool HasPlayerInside { get; private set; }

    private void Awake()
    {
        ResolveRefs();
    }

    public void SetContext(
        GameObject newVehicleObject,
        GameObject newPlayerObject,
        Transform newSeatPosition,
        Transform newExitPoint)
    {
        if (newVehicleObject != null)
            vehicleObject = newVehicleObject;

        if (newPlayerObject != null)
            playerObject = newPlayerObject;

        if (newSeatPosition != null)
            seatPosition = newSeatPosition;

        if (newExitPoint != null)
            exitPoint = newExitPoint;

        ResolveRefs();
    }

    private void ResolveRefs()
    {
        if (vehicleObject == null)
            vehicleObject = gameObject;

        if (playerObject == null)
        {
            GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");

            if (foundPlayer != null)
                playerObject = foundPlayer;
        }

        if (playerObject != null)
        {
            if (playerMovement == null)
                playerMovement = playerObject.GetComponent<PlayerMovement>();

            if (playerCC == null)
                playerCC = playerObject.GetComponent<CharacterController>();

            if (playerStats == null)
                playerStats = playerObject.GetComponent<PlayerStats>();

            if (playerVisualRoot == null)
            {
                Transform model = playerObject.transform.Find("Model");

                if (model != null)
                    playerVisualRoot = model.gameObject;
            }
        }
    }

    public void EnterVehicle()
    {
        ResolveRefs();

        HasPlayerInside = true;

        if (playerMovement != null)
            playerMovement.IsInVehicle = true;

        if (playerVisualRoot != null)
            playerVisualRoot.SetActive(false);

        if (playerCC != null)
            playerCC.enabled = false;

        MovePlayerToSeat();
    }

    public void ExitVehicle()
    {
        ResolveRefs();

        HasPlayerInside = false;

        if (playerObject != null && !playerObject.activeSelf)
            playerObject.SetActive(true);

        MovePlayerToExitPoint();

        if (playerVisualRoot != null)
            playerVisualRoot.SetActive(true);

        if (playerCC != null)
            playerCC.enabled = true;

        if (playerMovement != null)
            playerMovement.IsInVehicle = false;
    }

    public void RestoreInsideVehicleFromLoad()
    {
        ResolveRefs();

        HasPlayerInside = true;

        if (playerObject != null)
            playerObject.SetActive(false);

        if (playerMovement != null)
            playerMovement.IsInVehicle = true;

        if (playerVisualRoot != null)
            playerVisualRoot.SetActive(false);

        if (playerCC != null)
            playerCC.enabled = false;

        MovePlayerToSeat();
    }

    public void ForceExitToVehicleSide()
    {
        ResolveRefs();

        HasPlayerInside = false;

        if (playerObject != null)
            playerObject.SetActive(true);

        MovePlayerToExitPoint();

        if (playerVisualRoot != null)
            playerVisualRoot.SetActive(true);

        if (playerCC != null)
            playerCC.enabled = true;

        if (playerMovement != null)
            playerMovement.IsInVehicle = false;
    }

    private void MovePlayerToSeat()
    {
        if (playerObject == null || seatPosition == null)
            return;

        playerObject.transform.SetPositionAndRotation(
            seatPosition.position,
            seatPosition.rotation
        );
    }

    private void MovePlayerToExitPoint()
    {
        if (playerObject == null)
            return;

        if (exitPoint != null)
        {
            playerObject.transform.SetPositionAndRotation(
                exitPoint.position,
                Quaternion.Euler(0f, exitPoint.eulerAngles.y, 0f)
            );

            return;
        }

        if (vehicleObject != null)
        {
            Vector3 exitPos = vehicleObject.transform.position + vehicleObject.transform.right * 2f;

            playerObject.transform.SetPositionAndRotation(
                exitPos,
                Quaternion.Euler(0f, vehicleObject.transform.eulerAngles.y, 0f)
            );
        }
    }
}