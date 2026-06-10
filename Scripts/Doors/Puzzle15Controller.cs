using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class Puzzle15Controller : MonoBehaviour
{
    [Header("UI")]
    public GameObject puzzlePanel;
    public GameObject slotPrefab; // Puzzle_13_Slot
    public Transform gridParent;  // GridLayoutGroup (4x4)

    public GameObject successBGPanel;
    public TMP_Text successText;

    public Slider loadingBar;
    public TMP_Text percentText;
    public Button exitButton;

    private int[] puzzle = new int[16];       // wartości 0–15
    private GameObject[] tiles = new GameObject[16];
    private int emptyIndex;
    private bool isSolved = false;

    public GameObject panelBG; // ← to jest "PanelPuzzle15/PanelBG"
    private int[] initialPuzzle = new int[16];

    public GameObject linePrefab;
    public Transform lineContainer; // ← osobny GameObject jako kontener na linie
    private List<GameObject> activeLines = new();

    void Start()
    {
        puzzlePanel.SetActive(false);
        exitButton.onClick.AddListener(ExitPuzzle);

        GenerateTiles();
        GeneratePuzzle();
        UpdateUI();

        if (loadingBar != null)
            loadingBar.interactable = false; // 🔒 nieklikalny

    }

    void Update()
    {
        if (puzzlePanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            ExitPuzzle();
    }

    public void OpenPuzzle()
    {
        panelBG.SetActive(true);
        Time.timeScale = 0f;

        PlayerMovement.IsMovementLocked = true;
        MouseLook.IsLookLocked = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ExitPuzzle()
    {
        panelBG.SetActive(false);
        Time.timeScale = 1f;

        PlayerMovement.IsMovementLocked = false;
        MouseLook.IsLookLocked = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (isSolved)
            Debug.Log("✔️ Puzzle solved – exit triggered");
    }

    void GenerateTiles()
    {
        for (int i = 0; i < 16; i++)
        {
            GameObject tile = Instantiate(slotPrefab, gridParent);
            tiles[i] = tile;

            // (opcja) od razu wyczyść tekst
            TMP_Text txt = tile.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "";
        }
    }

    void GeneratePuzzle()
    {
        // Startujemy od rozwiązanej planszy
        for (int i = 0; i < 15; i++)
            puzzle[i] = i + 1;
        puzzle[15] = 0;

        emptyIndex = 15;

        // Wykonaj N legalnych ruchów wstecz, by uzyskać losowy układ
        int moves = 1000; // im więcej, tym trudniej

        for (int m = 0; m < moves; m++)
        {
            List<int> movable = new List<int>();

            int row = emptyIndex / 4;
            int col = emptyIndex % 4;

            if (col > 0) movable.Add(emptyIndex - 1);     // lewo
            if (col < 3) movable.Add(emptyIndex + 1);     // prawo
            if (row > 0) movable.Add(emptyIndex - 4);     // góra
            if (row < 3) movable.Add(emptyIndex + 4);     // dół

            int chosen = movable[Random.Range(0, movable.Count)];

            (puzzle[emptyIndex], puzzle[chosen]) = (puzzle[chosen], puzzle[emptyIndex]);
            emptyIndex = chosen;
        }

        System.Array.Copy(puzzle, initialPuzzle, 16);
    }

    public void ResetPuzzle()
    {
        System.Array.Copy(initialPuzzle, puzzle, 16);
        emptyIndex = System.Array.IndexOf(puzzle, 0);
        UpdateUI();
    }

    void UpdateUI()
    {
        for (int i = 0; i < 16; i++)
        {
            int val = puzzle[i];
            GameObject tile = tiles[i];

            // 🎯 Ustaw tekst liczby
            TMP_Text txt = tile.transform.Find("Button/Number")?.GetComponent<TMP_Text>();
            if (txt != null)
            {
                txt.text = val == 0 ? "" : val.ToString();
                bool isCorrect = (val == i + 1) || (i == 15 && val == 0);
                txt.color = isCorrect ? Color.green : Color.white;
            }

            // ❌ NIE zmieniamy koloru tła
            // Image img = tile.transform.Find("Button")?.GetComponent<Image>();
            // if (img != null) img.color = Color.white;

            // 🔁 Przypisz interakcję
            Button btn = tile.transform.Find("Button")?.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();

                if (!isSolved) // 🔒 tylko jeśli NIE rozwiązane
                {
                    int index = i;
                    if (CanMove(index))
                        btn.onClick.AddListener(() => TryMove(index));
                }
            }
        }

        DrawConnections();

        if (IsComplete())
            StartCoroutine(ShowSuccess());
    }

    bool CanMove(int index)
    {
        int row = index / 4;
        int col = index % 4;
        int erow = emptyIndex / 4;
        int ecol = emptyIndex % 4;

        return (row == erow && Mathf.Abs(col - ecol) == 1) ||
               (col == ecol && Mathf.Abs(row - erow) == 1);
    }

    void TryMove(int index)
    {
        if (!CanMove(index)) return;

        (puzzle[index], puzzle[emptyIndex]) = (puzzle[emptyIndex], puzzle[index]);
        emptyIndex = index;
        UpdateUI();
    }

    bool IsComplete()
    {
        for (int i = 0; i < 15; i++)
            if (puzzle[i] != i + 1)
                return false;
        return puzzle[15] == 0;
    }
    IEnumerator ShowSuccess()
    {
        if (isSolved) yield break;
        isSolved = true;

        float duration = 1.0f; // ⏩ szybsze ładowanie
        float time = 0f;

        successText.text = "";
        loadingBar.value = 0f;
        loadingBar.gameObject.SetActive(true);
        percentText.gameObject.SetActive(true);

        // 🟦 domyślnie czerwony
        Image fill = loadingBar.fillRect?.GetComponent<Image>();
        if (fill != null)
            fill.color = Color.red;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);

            loadingBar.value = t;
            percentText.text = $"{Mathf.RoundToInt(t * 100)}%";

            // 🌈 Płynna zmiana koloru
            if (fill != null)
            {
                fill.color =
                    t < 0.25f ? Color.Lerp(Color.red, new Color(1f, 0.5f, 0f), t / 0.25f) :               // czerwony → pomarańcz
                    t < 0.5f ? Color.Lerp(new Color(1f, 0.5f, 0f), Color.yellow, (t - 0.25f) / 0.25f) :   // pomarańcz → żółty
                    t < 0.75f ? Color.Lerp(Color.yellow, Color.green, (t - 0.5f) / 0.25f) :              // żółty → zielony
                                Color.Lerp(Color.green, Color.cyan, (t - 0.75f) / 0.25f);                // zielony → niebieski
            }

            yield return null;
        }

        percentText.text = "100%";
        loadingBar.gameObject.SetActive(false);

        if (successBGPanel != null)
            successBGPanel.SetActive(true);

        if (successText != null)
            successText.text = "DOSTĘP PRZYZNANY";
    }



    void DrawConnections()
    {
        foreach (var l in activeLines)
            Destroy(l);
        activeLines.Clear();

        for (int i = 0; i < 16; i++)
        {
            int val = puzzle[i];
            if (val == 0 || val != i + 1) continue; // tylko jeśli kafelek jest poprawnie ustawiony

            // sprawdź prawo (jeśli nie jesteśmy na końcu wiersza)
            if ((i % 4 < 3))
            {
                int rightIndex = i + 1;
                int rightVal = puzzle[rightIndex];
                if (rightVal == val + 1 && rightVal == rightIndex + 1)
                    DrawLineBetween(i, rightIndex);
            }

            // sprawdź dół (jeśli nie jesteśmy w ostatzym rzędzie)
            if (i / 4 < 3)
            {
                int downIndex = i + 4;
                int downVal = puzzle[downIndex];
                if (downVal == val + 1 && downVal == downIndex + 1)
                    DrawLineBetween(i, downIndex);
            }
        }
    }
    void DrawLineBetween(int fromIndex, int toIndex)
    {
        Vector3 fromPos = tiles[fromIndex].transform.position;
        Vector3 toPos = tiles[toIndex].transform.position;

        GameObject line = Instantiate(linePrefab, lineContainer);
        RectTransform rt = line.GetComponent<RectTransform>();

        Vector3 mid = (fromPos + toPos) / 2f;
        Vector3 dir = (toPos - fromPos).normalized;
        float distance = Vector3.Distance(fromPos, toPos);

        line.transform.position = mid;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.sizeDelta = new Vector2(distance, 4); // szerokość linii, np. 4px
        rt.rotation = Quaternion.Euler(0, 0, angle);

        activeLines.Add(line);
    }


    public bool IsSolved() => isSolved;

}
