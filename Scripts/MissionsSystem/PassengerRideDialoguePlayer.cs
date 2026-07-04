using System.Collections;
using UnityEngine;

public class PassengerRideDialoguePlayer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DialogueWindowUI rideDialogueWindow;

    [Header("Settings")]
    [SerializeField] private float lineHoldAfterTyped = 1.4f;

    [Header("Auto Hide")]
    [SerializeField] private float hideAfterLastLineDelay = 2.5f;

    private Coroutine temporaryLineRoutine;
    private PassengerTransportMissionDefinition definition;
    private PassengerTransportMissionRuntime runtime;

    private Coroutine playRoutine;
    private int currentIndex;
    private bool paused;
    private bool playing;

    public bool IsPlaying => playing;
    public bool IsPaused => paused;

    private void Awake()
    {
        if (rideDialogueWindow == null)
        {
            Debug.LogWarning(
                "[PassengerRideDialoguePlayer] Ride Dialogue Window is missing. " +
                "Assign Root_RideDialogueUI / DialogueWindowUI manually in Inspector."
            );
        }
    }

    public void StartDialogue(
      PassengerTransportMissionDefinition missionDefinition,
      PassengerTransportMissionRuntime missionRuntime)
    {
        definition = missionDefinition;
        runtime = missionRuntime;

        currentIndex = 0;
        paused = false;
        playing = true;

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        // Nie otwieramy okna tutaj.
        // Okno otworzy siê dopiero przy pierwszej prawdziwej linii dialogu.

        playRoutine = StartCoroutine(CoPlay());
    }

    public void PauseAndHide()
    {
        paused = true;

        if (rideDialogueWindow != null)
        {
            if (rideDialogueWindow.IsTyping)
                rideDialogueWindow.FinishTypewriterInstant();

            rideDialogueWindow.SetVisualVisibleOnly(false);
        }
    }

    public void Resume()
    {
        paused = false;

        if (rideDialogueWindow != null)
            rideDialogueWindow.CloseWindowWithFade(unlockPlayer: false);
    }

    public void StopDialogue()
    {
        playing = false;
        paused = false;
        currentIndex = 0;

        if (temporaryLineRoutine != null)
        {
            StopCoroutine(temporaryLineRoutine);
            temporaryLineRoutine = null;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (rideDialogueWindow != null)
            rideDialogueWindow.CloseWindowWithFade(unlockPlayer: false);
    }

    public void StopRoutineOnly()
    {
        playing = false;
        paused = false;
        currentIndex = 0;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (rideDialogueWindow != null && rideDialogueWindow.IsTyping)
            rideDialogueWindow.FinishTypewriterInstant();
    }

    public void ShowTemporaryLine(string speaker, string text, float duration = 4f)
    {
        if (temporaryLineRoutine != null)
        {
            StopCoroutine(temporaryLineRoutine);
            temporaryLineRoutine = null;
        }

        temporaryLineRoutine = StartCoroutine(CoShowTemporaryLine(speaker, text, duration));
    }

    private IEnumerator CoShowTemporaryLine(string speaker, string text, float duration)
    {
        if (rideDialogueWindow == null)
            yield break;

        if (string.IsNullOrWhiteSpace(text))
            yield break;

        rideDialogueWindow.OpenWindow(clearHistory: false, lockPlayer: false);
        rideDialogueWindow.SetVisualVisibleOnly(true);
        rideDialogueWindow.ClearHistory();

        bool isPlayer = IsPlayerSpeaker(speaker);
        bool done = false;

        rideDialogueWindow.TypeLine(
            string.IsNullOrWhiteSpace(speaker) ? "Passenger" : speaker,
            text,
            isPlayer,
            () => done = true
        );

        while (!done)
            yield return null;

        float timer = 0f;
        float holdTime = Mathf.Max(0.1f, duration);

        while (timer < holdTime)
        {
            if (!paused)
                timer += Time.deltaTime;

            yield return null;
        }

        if (rideDialogueWindow != null)
            rideDialogueWindow.CloseWindowWithFade(unlockPlayer: false);

        temporaryLineRoutine = null;
    }

    private IEnumerator CoPlay()
    {
        if (definition == null || definition.rideLines == null || definition.rideLines.Length == 0)
        {
            playing = false;
            yield break;
        }

        while (currentIndex < definition.rideLines.Length)
        {
            while (paused)
                yield return null;

            PassengerRideDialogueLine line = definition.rideLines[currentIndex];

            if (line == null || string.IsNullOrWhiteSpace(line.text))
            {
                currentIndex++;
                continue;
            }

            float delay = Mathf.Max(0f, line.delayAfterPrevious);
            float delayTimer = 0f;

            while (delayTimer < delay)
            {
                if (!paused)
                    delayTimer += Time.deltaTime;

                yield return null;
            }

            while (paused)
                yield return null;

            string speaker = runtime != null
                ? runtime.ResolveSpeakerName(line)
                : line.customSpeakerName;

            string text = runtime != null
                ? runtime.FormatMissionText(line.text)
                : line.text;

            bool isPlayer = line.speaker == PassengerDialogueSpeaker.Player ||
                            IsPlayerSpeaker(speaker);

            bool lineFinished = false;

            if (rideDialogueWindow != null)
            {
                rideDialogueWindow.OpenWindow(clearHistory: false, lockPlayer: false);
                rideDialogueWindow.SetVisualVisibleOnly(true);
                rideDialogueWindow.ClearHistory();

                rideDialogueWindow.TypeLine(
                    string.IsNullOrWhiteSpace(speaker) ? "Passenger" : speaker,
                    text,
                    isPlayer,
                    () => lineFinished = true
                );
            }
            else
            {
                lineFinished = true;
            }

            while (!lineFinished)
                yield return null;

            currentIndex++;

            float holdTimer = 0f;

            while (holdTimer < lineHoldAfterTyped)
            {
                if (!paused)
                    holdTimer += Time.deltaTime;

                yield return null;
            }
        }

        playing = false;
        playRoutine = null;

        float hideTimer = 0f;

        while (hideTimer < hideAfterLastLineDelay)
        {
            if (!paused)
                hideTimer += Time.deltaTime;

            yield return null;
        }

        if (rideDialogueWindow != null)
            rideDialogueWindow.CloseWindowWithFade(unlockPlayer: false);
    }

    private bool IsPlayerSpeaker(string speaker)
    {
        if (string.IsNullOrWhiteSpace(speaker))
            return false;

        return speaker.Trim().ToLowerInvariant() == "player";
    }

    public IEnumerator ShowTemporaryLineAndWait(string speaker, string text, float holdAfterTyped = 2.0f)
    {
        if (rideDialogueWindow == null)
            yield break;

        if (string.IsNullOrWhiteSpace(text))
            yield break;

        rideDialogueWindow.OpenWindow(clearHistory: false, lockPlayer: false);
        rideDialogueWindow.SetVisualVisibleOnly(true);
        rideDialogueWindow.ClearHistory();

        bool isPlayer = IsPlayerSpeaker(speaker);
        bool done = false;

        rideDialogueWindow.TypeLine(
            string.IsNullOrWhiteSpace(speaker) ? "Passenger" : speaker,
            text,
            isPlayer,
            () => done = true
        );

        while (!done)
            yield return null;

        float timer = 0f;

        while (timer < holdAfterTyped)
        {
            timer += Time.deltaTime;
            yield return null;
        }
    }
}