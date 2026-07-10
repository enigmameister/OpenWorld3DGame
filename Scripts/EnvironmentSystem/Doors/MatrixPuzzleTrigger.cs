using TMPro;
using UnityEngine;

public class MatrixPuzzleTrigger : MonoBehaviour, IPressable
{
    [Header("Controller")]
    [SerializeField] private MatrixPuzzleController controller;

    [Header("World Label")]
    [SerializeField] private string label = "Użyj panelu Matrix";

    private bool puzzleSolved = false;

    [Header("Status Light")]
    public Light statusLight;
    public Color solvedColor = Color.green;
    public Color flashingColorA = Color.red;
    public Color flashingColorB = new Color(0.4f, 0f, 0f);
    public float flashSpeed = 2f;

    public TextMeshPro taskLabel;
    public TextMeshPro resultLabel;

    public string Label => puzzleSolved ? "Panel rozwiązany" : label;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponentInParent<MatrixPuzzleController>();

        if (controller == null)
            controller = GetComponentInChildren<MatrixPuzzleController>(true);
    }

    private void Update()
    {
        UpdateStatusLight();
    }

    public void Press()
    {
        if (puzzleSolved)
            return;

        if (controller == null)
        {
            Debug.LogWarning("[MatrixPuzzleTrigger] Brak MatrixPuzzleController.", this);
            return;
        }

        controller.OpenPuzzleUI();
    }

    public void MarkPuzzleAsSolved()
    {
        puzzleSolved = true;

        if (statusLight != null)
            statusLight.color = solvedColor;
    }

    public void SetSolvedLabel(int codeIndex, int determinant)
    {
        if (taskLabel != null)
            taskLabel.text = $"#{codeIndex + 1}";

        if (resultLabel != null)
            resultLabel.text = $"[{determinant}]";
    }

    private void UpdateStatusLight()
    {
        if (statusLight == null)
            return;

        if (!puzzleSolved)
        {
            float t = Mathf.PingPong(Time.time * flashSpeed, 1f);
            statusLight.color = Color.Lerp(flashingColorA, flashingColorB, t);
        }
        else
        {
            if (statusLight.color != solvedColor)
                statusLight.color = solvedColor;
        }
    }
}