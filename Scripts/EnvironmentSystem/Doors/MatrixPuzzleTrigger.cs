using TMPro;
using UnityEngine;

public class MatrixPuzzleTrigger : MonoBehaviour
{
    public GameObject matrixCanvas; // Canvas z MatrixPuzzleController
    private bool playerInRange = false;
    private bool puzzleSolved = false;

    [Header("Status Light")]
    public Light statusLight;
    public Color solvedColor = Color.green;
    public Color flashingColorA = Color.red;
    public Color flashingColorB = new Color(0.4f, 0f, 0f);
    public float flashSpeed = 2f;

    public TextMeshPro taskLabel;
    public TextMeshPro resultLabel;

    void Update()
    {
        if (statusLight != null)
        {
            if (!puzzleSolved)
            {
                float t = Mathf.PingPong(Time.time * flashSpeed, 1f);
                statusLight.color = Color.Lerp(flashingColorA, flashingColorB, t);
            }
            else
            {
                // ✳️ Nadaj raz tylko jeśli światło ma inny kolor niż docelowy
                if (statusLight.color != solvedColor)
                    statusLight.color = solvedColor;
            }
        }

        if (playerInRange && !puzzleSolved && Input.GetKeyDown(KeyCode.E))
        {
            OpenPuzzle();
        }
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
    }

    public void MarkPuzzleAsSolved()
    {
        puzzleSolved = true;

        if (statusLight != null)
        {
            statusLight.color = solvedColor;
        }
    }

    void OpenPuzzle()
    {
        if (puzzleSolved) return;

        MatrixPuzzleController controller = matrixCanvas.GetComponent<MatrixPuzzleController>();
        if (controller != null)
            controller.OpenPuzzleUI();

        matrixCanvas.SetActive(true);
        Time.timeScale = 0f;

        // Zablokuj sterowanie gracza (opcjonalnie)
        PlayerMovement.IsMovementLocked = true;
        MouseLook.IsLookLocked = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ExitPuzzle()
    {
        MatrixPuzzleController controller = matrixCanvas.GetComponent<MatrixPuzzleController>();
        if (controller != null)
            controller.ClosePuzzleUI();

        matrixCanvas.SetActive(false);
        Time.timeScale = 1f;

        PlayerMovement.IsMovementLocked = false;
        MouseLook.IsLookLocked = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetSolvedLabel(int codeIndex, int determinant)
    {
        if (taskLabel != null)
            taskLabel.text = $"#{codeIndex + 1}";

        if (resultLabel != null)
            resultLabel.text = $"[{determinant}]";
    }

}
