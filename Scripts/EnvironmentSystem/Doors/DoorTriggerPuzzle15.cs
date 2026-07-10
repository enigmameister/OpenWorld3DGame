using UnityEngine;
using UnityEngine.UI;

public class DoorTriggerPuzzle15 : MonoBehaviour, IPressable
{
    [Header("Puzzle")]
    public Puzzle15Controller puzzleController;
    public GameObject puzzleCanvas;

    [Header("Drzwi")]
    public Animator doorAnimator;
    public Light statusLight;
    public Color openedColor = Color.green;
    public Color flashingColorA = Color.red;
    public Color flashingColorB = new Color(0.4f, 0f, 0f);
    public float flashSpeed = 2f;

    [SerializeField] private string label = "U¿yj panelu puzzli";

    private bool doorOpened = false;

    public string Label
    {
        get
        {
            if (doorOpened)
                return "Drzwi otwarte";

            if (puzzleController != null && puzzleController.IsSolved())
                return "Dostêp przyznany";

            return label;
        }
    }

    void Update()
    {
        if (!doorOpened && statusLight != null)
        {
            float t = Mathf.PingPong(Time.time * flashSpeed, 1f);
            statusLight.color = Color.Lerp(flashingColorA, flashingColorB, t);
        }

        if (puzzleController != null && puzzleController.IsSolved() && !doorOpened)
        {
            TriggerDoorOpen();
        }
    }

    public void Press()
    {
        if (doorOpened)
            return;

        if (puzzleController == null)
            return;

        puzzleController.OpenPuzzle();
    }

    private void TriggerDoorOpen()
    {
        doorOpened = true;

        if (doorAnimator != null)
            doorAnimator.SetBool("Key", true);

        if (statusLight != null)
            statusLight.color = openedColor;
    }
}