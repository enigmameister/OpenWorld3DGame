using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ShootingMinigameController : MonoBehaviour
{
    public List<Target> targets;

    [Header("UI")]
    public GameObject minigameUI;
    public GameObject infoBoxGO;
    public TMP_Text messageText;
    public GameObject countdownText;
    public TMP_Text countdownTMP;
    public GameObject timerTextGO;
    public TMP_Text timerTMP;

    [Header("Settings")]
    public float timer = 40f;
    public Animator doorAnimator;
    public GameObject shootingTrigger;

    public string introText = "Strzel by rozpocząć...";
    public string secondIntroText = "Bez limitu? Żadne wyzwanie. Spróbuj w 20 sekund.";
    [Range(0.01f, 0.5f)] public float textWriteSpeed = 0.05f;

    [HideInInspector] public bool isGameRunning = false;
    private bool skipFirstPhase = false; // Czy rozpocząć bezpośrednio od drugiej fazy
    [HideInInspector] public bool secondPhaseCompleted = false;
    private Coroutine introCoroutine;
    private bool isIntroTextVisible = false;

    [Header("Light Indicators")]
    public List<LightIndicator> lightIndicators;
    private int lightProgressIndex = 0;

    [Header("World Countdown (TMP 3D)")]
    public TMPro.TextMeshPro countdown3DText;

    void Start()
    {
        messageText.text = "";
        countdownText.SetActive(false);
        timerTextGO.SetActive(false);
        minigameUI.SetActive(false);

        foreach (var light in lightIndicators)
        {
            light.Initialize(Color.white); // Ustawia biały tylko przy starcie gry
        }

        if (countdown3DText != null)
            countdown3DText.text = ""; // ⬅️ Resetuj 3D tekst
    }

    public bool CanTriggerUI()
    {
        return !isGameRunning && !secondPhaseCompleted;
    }

    public void ShowMinigameUI()
    {
        if (isGameRunning) return;

        minigameUI.SetActive(true);

        if (skipFirstPhase)
            ShowIntroText(secondIntroText, WaitForSecondShot);
        else
            ShowIntroText(introText, WaitForFirstShot);
    }

    public void HideMinigameUI()
    {
        if (isGameRunning) return;

        HideIntroText();
        minigameUI.SetActive(false);
    }

    void ShowIntroText(string text, System.Func<IEnumerator> nextStep)
    {
        if (isIntroTextVisible) return;

        isIntroTextVisible = true;
        infoBoxGO.SetActive(true);
        messageText.text = "";

        if (introCoroutine != null)
            StopCoroutine(introCoroutine);

        introCoroutine = StartCoroutine(AnimateIntroText(text, nextStep));
    }

    public void HideIntroText()
    {
        if (!isIntroTextVisible) return;

        isIntroTextVisible = false;
        if (introCoroutine != null)
            StopCoroutine(introCoroutine);

        messageText.text = "";
        infoBoxGO.SetActive(false);
    }

    IEnumerator AnimateIntroText(string text, System.Func<IEnumerator> nextStep)
    {
        messageText.text = "";
        foreach (char c in text)
        {
            messageText.text += c;
            yield return new WaitForSecondsRealtime(textWriteSpeed);

            if (!isIntroTextVisible) yield break;
        }

        StartCoroutine(nextStep());
    }

    IEnumerator WaitForFirstShot()
    {
        while (!Input.GetButtonDown("Fire1"))
            yield return null;

        infoBoxGO.SetActive(false);
        StartCoroutine(StartCountdown());
        isGameRunning = true;
    }

    IEnumerator StartCountdown()
    {
        countdownText.SetActive(true);
        for (int i = 5; i > 0; i--)
        {
            countdownTMP.text = i.ToString();
            yield return new WaitForSecondsRealtime(1f);
        }
        countdownText.SetActive(false);
        StartCoroutine(StartTargetPhase());
    }

    IEnumerator StartTargetPhase()
    {
        ResetAllLights();
        if (countdown3DText != null)
            countdown3DText.text = "10";

        Time.timeScale = 1f;

        List<Target> shuffled = new List<Target>(targets);
        Shuffle(shuffled);

        for (int i = 0; i < shuffled.Count; i++)
        {
            var target = shuffled[i];
            target.ResetTarget();
            target.gameObject.SetActive(true);
            target.StartMovement();

            yield return new WaitUntil(() => target.IsHit);
            yield return new WaitForSeconds(0.3f);
            target.gameObject.SetActive(false);

            if (lightProgressIndex >= 0 && lightProgressIndex < lightIndicators.Count)
            {
                lightIndicators[lightProgressIndex].MarkHit();
                lightProgressIndex++;

                if (countdown3DText != null)
                {
                    int remaining = 10 - lightProgressIndex;
                    countdown3DText.text = remaining > 0 ? remaining.ToString() : "";
                }
            }

        }

        yield return new WaitForSeconds(1f);
        StartCoroutine(SecondPhase());
    }

    IEnumerator SecondPhase()
    {
        messageText.text = "";
        infoBoxGO.SetActive(true);
        yield return AnimateIntroText(secondIntroText, WaitForSecondShot); 
        yield return WaitForSecondShot();

        ResetAllTargetsToStart();
        StartCoroutine(StartTimedRound());
    }

    IEnumerator WaitForSecondShot()
    {
        while (!Input.GetButtonDown("Fire1"))
            yield return null;

        infoBoxGO.SetActive(false);
    }

    IEnumerator StartTimedRound()
    {
        isGameRunning = true;
        ResetAllLights();
        ResetAllTargetsToStart();

        if (countdown3DText != null)
            countdown3DText.text = "10";

        foreach (var t in targets)
        {
            t.gameObject.SetActive(true);
            t.enabled = false;
        }

        // 5 → 0 Countdown
        countdownText.SetActive(true);
        for (int i = 5; i > 0; i--)
        {
            countdownTMP.text = i.ToString();
            yield return new WaitForSecondsRealtime(1f);
        }
        countdownText.SetActive(false);

        // Reset stanu tarcz
        foreach (var t in targets)
        {
            t.ResetTarget();
            t.gameObject.SetActive(false);
        }

        timerTextGO.SetActive(true);
        float remainingTime = timer; // np. 20s
        Time.timeScale = 1f;

        List<Target> shuffled = new List<Target>(targets);
        Shuffle(shuffled);

        // 🔁 Czekaj aż sekwencja trafi wszystkie tarcze albo timer się skończy
        var timedTargets = StartCoroutine(ActivateTimedTargets(shuffled));

        while (remainingTime > 0f)
        {
            if (AllTargetsHit()) break;

            remainingTime -= Time.deltaTime;
            remainingTime = Mathf.Max(0f, remainingTime);

            int seconds = Mathf.FloorToInt(remainingTime);
            int hundredths = Mathf.FloorToInt((remainingTime - seconds) * 100);
            timerTMP.text = $"{seconds:D2}:{hundredths:D2}";

            if (remainingTime < 10f)
                timerTMP.color = Color.Lerp(Color.red, Color.white, Mathf.PingPong(Time.time * 3f, 1f));

            yield return null;
        }

        timerTextGO.SetActive(false);

        if (AllTargetsHit())
        {
            ResetAllTargetsToStart();
            doorAnimator.SetTrigger("Open");
            messageText.text = "Udało się!";
            ResetLightsToDefaultWhite(); // ← TUTAJ
            isGameRunning = false;
            countdown3DText.text = "";
        }

        else
        {
            messageText.text = "Czas minął!";
            yield return new WaitForSeconds(2f);

            skipFirstPhase = true; // 👈 Pomijaj pierwszą fazę

            if (shootingTrigger != null)
            {
                shootingTrigger.SetActive(true);
            }

            ResetTargets();
            minigameUI.SetActive(false);
            isGameRunning = false;
        }
    }
    void ResetAllLights()
    {
        lightProgressIndex = 0;
        foreach (var light in lightIndicators)
            light.SetColor(Color.red); // ustawia czerwony przy starcie każdej fazy
    }

    IEnumerator ActivateTimedTargets(List<Target> shuffled)
    {
        foreach (var t in shuffled)
        {
            t.ResetTarget();
            t.gameObject.SetActive(true);
            t.StartMovement();

            Debug.Log($"🎯 [Timed] Aktywowano: {t.name}");

            yield return new WaitUntil(() => t.IsHit);

            yield return new WaitForSeconds(0.1f);
            t.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.4f);

            if (lightProgressIndex >= 0 && lightProgressIndex < lightIndicators.Count)
            {
                lightIndicators[lightProgressIndex].MarkHit();
                lightProgressIndex++;

                if (countdown3DText != null)
                {
                    int remaining = 10 - lightProgressIndex;
                    countdown3DText.text = remaining > 0 ? remaining.ToString() : "";
                }
            }
        }

    }
    void ResetLightsToDefaultWhite()
    {
        foreach (var light in lightIndicators)
            light.SetColor(Color.white);
    }

    void ResetAllTargetsToStart()
    {
        foreach (var t in targets)
            t.ResetTarget();
    }

    void ResetTargets()
    {
        foreach (var t in targets)
            t.ResetTarget();
    }

    void Shuffle(List<Target> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Target temp = list[i];
            int rand = Random.Range(i, list.Count);
            list[i] = list[rand];
            list[rand] = temp;
        }
    }

    bool AllTargetsHit() => targets.TrueForAll(t => t.IsHit);
}