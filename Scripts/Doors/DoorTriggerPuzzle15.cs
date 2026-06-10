using UnityEngine;
using UnityEngine.UI;

public class DoorTriggerPuzzle15 : MonoBehaviour
{
    [Header("Puzzle")]
    public Puzzle15Controller puzzleController;
    public GameObject puzzleCanvas; // ca³a logika 15 Puzzle

    [Header("Drzwi")]
    public Animator doorAnimator;
    public Light statusLight;
    public Color openedColor = Color.green;
    public Color flashingColorA = Color.red;
    public Color flashingColorB = new Color(0.4f, 0f, 0f);
    public float flashSpeed = 2f;

    private bool playerInZone = false;
    private bool doorOpened = false;

    void Update()
    {
        if (!doorOpened && statusLight != null)
        {
            float t = Mathf.PingPong(Time.time * flashSpeed, 1f);
            statusLight.color = Color.Lerp(flashingColorA, flashingColorB, t);
        }

        if (playerInZone && !doorOpened && PlayerInputHandler.Instance != null &&
            PlayerInputHandler.Instance.InteractPressedThisFrame)
        {
            puzzleController.OpenPuzzle();
        }

        if (puzzleController != null && puzzleController.IsSolved() && !doorOpened)
        {
            TriggerDoorOpen();
        }
    }

    private void TriggerDoorOpen()
    {
        doorOpened = true;
        if (doorAnimator != null)
            doorAnimator.SetBool("Key", true);

        if (statusLight != null)
            statusLight.color = openedColor;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInZone = false;
    }
}
