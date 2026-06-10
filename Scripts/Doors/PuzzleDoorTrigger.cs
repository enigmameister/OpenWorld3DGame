using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using System.Collections.Generic;

public class PuzzleDoorTrigger : MonoBehaviour
{
    public Animator doorAnimator;
    public Light statusLight;
    public Color openedColor = Color.green;

    [Header("UI Puzzle")]
    public GameObject puzzleCanvas;
    public Button confirmButton;
    public Button resetButton;
    public Button exitButton;
    public TMP_Text messageText;

    public DropSlot[] answerSlots;

    private bool playerInZone = false;
    private bool puzzleSolved = false;

    public Slider loadingBar;
    public Image loadingFill; // ← Image na Fill Area paska
    public float loadingDuration = 2f;
    public TMP_Text loadingPercentText;
    private bool answerIsCorrect = false;

    [Header("Slot Generator")]
    public GameObject answerSlotPrefab; // przypisz prefab w Inspectorze
    public Transform slotContainer;     // obiekt np. SlotsANSWER
    public int numberOfSlots = 10;      // ile slotów chcesz utworzyć

    [Header("Kodony")]
    public GameObject codonPrefab;
    public Transform codonsContainer;
    public int totalCodons = 10;
    private string selectedStopCodon;
    public TMP_Text infoText; // ← przypisz pole tekstowe instrukcji

    private readonly string[] possibleCodons = new string[]
{
    "CAG", "CGU", "GAC", "GCU", "UAC", "UCG", "CCG", "AAC", "GAG", "CUU"
};

    private readonly string[] stopCodons = new string[] { "UAA", "UAG", "UGA" };
    private List<string> generatedCodonSequence;
    void Start()
    {
        puzzleCanvas.SetActive(false);
        confirmButton.onClick.AddListener(CheckAnswer);
        resetButton.onClick.AddListener(ResetPuzzle);
        exitButton.onClick.AddListener(ExitPuzzleUI);
        messageText.gameObject.SetActive(false);
        GenerateCodonSequence(10); // ← generuj 10-elementową sekwencję
        GenerateCodonTiles(totalCodons);
        GenerateAnswerSlots(totalCodons);
        UpdateInstructionText();

    }

    void Update()
    {
        if (playerInZone && !puzzleSolved && PlayerInputHandler.Instance.InteractPressedThisFrame)
        {
            OpenPuzzleUI();
        }

        if (!answerIsCorrect && messageText.gameObject.activeSelf && Input.GetMouseButtonDown(0))
        {
            messageText.gameObject.SetActive(false);
        }

    }

    private void UpdateInstructionText()
    {
        infoText.text =
            "<b>Twoim zadaniem jest zbudowanie poprawnej sekwencji mRNA.</b>\n\n" +
            "Sekwencja musi zaczynać się od kodonu <b>AUG</b>\n" +
            "Powinna zawierać dowolne kodony wewnętrzne\n" +
            $"Sekwencja musi zakończyć się kodonem <b>{selectedStopCodon}</b>\n" +
            "Kodony muszą być ułożone kolejno — <i>bez przerw między nimi</i>\n\n" +
            "Przeciągnij kodony do pól odpowiedzi, a następnie naciśnij <b>CHECK</b>";
    }


    private void GenerateCodonTiles(int count)
    {
        foreach (Transform child in codonsContainer)
            Destroy(child.gameObject);

        // losowy indeks na AUG i STOP
        int augIndex = Random.Range(0, count - 2);
        int stopIndex = Random.Range(augIndex + 1, count);

        generatedCodonSequence = new List<string>(); // jeśli używasz globalnie

        for (int i = 0; i < count; i++)
        {
            string codon;

            if (i == augIndex)
            {
                codon = "AUG";
            }
            else if (i == stopIndex)
            {
                selectedStopCodon = stopCodons[Random.Range(0, stopCodons.Length)];
                codon = selectedStopCodon;
            }
            else
            {
                codon = possibleCodons[Random.Range(0, possibleCodons.Length)];
            }

            generatedCodonSequence.Add(codon);

            GameObject tile = Instantiate(codonPrefab, codonsContainer);

            // ✅ nazwij nadrzędny obiekt
            tile.name = "Parent_" + codon;

            // 🔍 znajdź Codon_Code
            Transform codonGO = tile.transform.Find("Codon_Code");
            if (codonGO != null)
            {
                codonGO.name = codon; // ← zmiana nazwy z "Codon_Code" na "AUG", "CGU", itd.
                TMP_Text label = codonGO.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = codon;
            }

            // ustaw oryginalnego parenta
            DraggableCodon drag = tile.GetComponentInChildren<DraggableCodon>();
            if (drag != null)
                drag.originalParent = tile.transform;
        }

        // zaktualizuj dynamiczną instrukcję z nazwą STOP
        UpdateInstructionText();
    }


    private void GenerateCodonSequence(int totalLength)
    {
        generatedCodonSequence = new List<string>();

        // Losuj indeks na AUG i STOP
        int augIndex = Random.Range(0, totalLength - 2);
        int stopIndex = Random.Range(augIndex + 1, totalLength);

        for (int i = 0; i < totalLength; i++)
        {
            if (i == augIndex)
            {
                generatedCodonSequence.Add("AUG");
            }
            else if (i == stopIndex)
            {
                string stop = stopCodons[Random.Range(0, stopCodons.Length)];
                generatedCodonSequence.Add(stop);
            }
            else
            {
                string codon = possibleCodons[Random.Range(0, possibleCodons.Length)];
                generatedCodonSequence.Add(codon);
            }
        }
    }

    private void GenerateAnswerSlots(int count)
    {
        // Usuń stare sloty (np. w trybie powtórnym)
        foreach (Transform child in slotContainer)
            Destroy(child.gameObject);

        answerSlots = new DropSlot[count];

        for (int i = 0; i < count; i++)
        {
            GameObject slotGO = Instantiate(answerSlotPrefab, slotContainer);
            slotGO.name = $"Answer Slot {i + 1}";

            DropSlot slot = slotGO.GetComponent<DropSlot>();
            if (slot != null)
            {
                slot.AcceptsOnlyOne = true;
                answerSlots[i] = slot;
            }
        }
    }
    public void ExitPuzzleUI()
    {
        // Zawsze zamyka UI i przywraca grę
        puzzleCanvas.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        messageText.gameObject.SetActive(false);

        // Jeśli odpowiedź poprawna → odpal drzwi z opóźnieniem
        if (answerIsCorrect && !loadingBar.gameObject.activeSelf)
        {
            puzzleSolved = true;
            StartCoroutine(TriggerDoorAfterDelay(0.5f)); // małe opóźnienie po zamknięciu
        }
    }
    private System.Collections.IEnumerator TriggerDoorAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        doorAnimator.SetBool("Key", true);

        if (statusLight != null)
            statusLight.color = openedColor;
    }

    void OpenPuzzleUI()
    {
        puzzleCanvas.SetActive(true);
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        messageText.text = "";
        messageText.gameObject.SetActive(false);
    }

    public void CheckAnswer()
    {
        Debug.Log("📥 Sekwencja gracza: " + string.Join("-", GetCurrentPlayerAnswer()));

        if (puzzleSolved) return;

        string[] answer = GetCurrentPlayerAnswer();


        if (!IsPotentiallyValid(answer))
        {
            messageText.text = "Sekwencja nieprawidłowa lub niepełna.";
            messageText.gameObject.SetActive(true);
            return;
        }

        // ✅ Jeśli potencjalnie dobra – rozpocznij ładowanie
        messageText.gameObject.SetActive(false);
        loadingBar.value = 0f;
        loadingBar.gameObject.SetActive(true);
        loadingPercentText.text = "";
        StartCoroutine(LoadingCheck(answer));
    }

    private bool IsPotentiallyValid(string[] seq)
    {
        if (seq.Length == 0) return false;

        // Brak luk między wypełnionymi slotami
        bool emptyFound = false;
        for (int i = 0; i < seq.Length; i++)
        {
            if (string.IsNullOrEmpty(seq[i]))
            {
                emptyFound = true;
            }
            else if (emptyFound)
            {
                return false;
            }
        }

        if (seq[0] != "AUG") return false;

        string[] stopCodons = { "UAA", "UAG", "UGA" };
        return seq.Any(c => stopCodons.Contains(c));
    }

    public void ResetPuzzle()
    {
        messageText.gameObject.SetActive(false);
        messageText.text = "";
        ResetAnswerSlots();
    }

    private string[] GetCurrentPlayerAnswer()
    {
        return answerSlots
            .Select(slot => slot.GetCodon())
            .ToArray();
    }
    private bool IsCorrectSequence(string[] seq)
    {
        string[] filtered = seq.Where(s => !string.IsNullOrEmpty(s)).ToArray();

        int startIndex = -1;
        for (int i = 0; i < filtered.Length; i++)
        {
            if (filtered[i] == "AUG")
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex == -1) return false;

        for (int i = startIndex + 1; i < filtered.Length; i++)
        {
            if (stopCodons.Contains(filtered[i]))
            {
                return true;
            }
        }

        return false;
    }


    private void ResetAnswerSlots()
    {
        foreach (var slot in answerSlots)
        {
            // Znajdź wszystkie kodony w slotach — niezależnie od głębokości
            var codons = slot.GetComponentsInChildren<DraggableCodon>(true);

            foreach (var codon in codons)
            {
                codon.transform.SetParent(codon.originalParent);
                codon.transform.localPosition = Vector3.zero;
            }
        }
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

    private System.Collections.IEnumerator LoadingCheck(string[] answer)
    {
        float timer = 0f;
        loadingBar.value = 0f;
        loadingBar.gameObject.SetActive(true);
        loadingFill.color = Color.red;

        while (timer < loadingDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / loadingDuration);
            loadingBar.value = t;

            if (loadingPercentText != null)
                loadingPercentText.text = Mathf.RoundToInt(t * 100f) + "%";

            // 🎨 Zmiana koloru: czerwony → żółty → pomarańczowy → zielony
            if (t < 0.33f)
                loadingFill.color = Color.Lerp(Color.red, Color.yellow, t / 0.33f);
            else if (t < 0.66f)
                loadingFill.color = Color.Lerp(Color.yellow, new Color(1f, 0.65f, 0f), (t - 0.33f) / 0.33f); // żółty → pomarańcz
            else
                loadingFill.color = Color.Lerp(new Color(1f, 0.65f, 0f), Color.green, (t - 0.66f) / 0.34f); // pomarańcz → zielony

            yield return null;
        }

        loadingBar.gameObject.SetActive(false);
        if (IsCorrectSequence(answer))
        {
            answerIsCorrect = true;
            messageText.text = "Odpowiedź poprawna!";
            messageText.gameObject.SetActive(true);
        }
        else
        {
            answerIsCorrect = false;
            messageText.text = "Nieprawidłowa sekwencja.";
            messageText.gameObject.SetActive(true);
        }

        if (loadingPercentText != null)
            loadingPercentText.text = "";

    }


}
