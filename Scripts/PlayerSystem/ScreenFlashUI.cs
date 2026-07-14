using System.Collections;
using UnityEngine;

public class ScreenFlashUI : MonoBehaviour
{
    public static ScreenFlashUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Default Flash")]
    [SerializeField, Range(0f, 1f)] private float defaultPeakAlpha = 1f;
    [SerializeField, Min(0f)] private float defaultFadeInDuration = 0.03f;
    [SerializeField, Min(0f)] private float defaultHoldDuration = 0.05f;
    [SerializeField, Min(0f)] private float defaultFadeOutDuration = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private Coroutine flashRoutine;

    public bool IsFlashing => flashRoutine != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        SetAlphaImmediate(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Flash()
    {
        Flash(
            defaultPeakAlpha,
            defaultFadeInDuration,
            defaultHoldDuration,
            defaultFadeOutDuration
        );
    }

    public void Flash(
        float peakAlpha,
        float fadeInDuration,
        float holdDuration,
        float fadeOutDuration)
    {
        if (canvasGroup == null)
        {
            Debug.LogWarning("[SCREEN FLASH] CanvasGroup is missing.");
            return;
        }

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(
            FlashRoutine(
                Mathf.Clamp01(peakAlpha),
                Mathf.Max(0f, fadeInDuration),
                Mathf.Max(0f, holdDuration),
                Mathf.Max(0f, fadeOutDuration)
            )
        );
    }

    public void StopFlashImmediate()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        SetAlphaImmediate(0f);
    }

    private IEnumerator FlashRoutine(
        float peakAlpha,
        float fadeInDuration,
        float holdDuration,
        float fadeOutDuration)
    {
        canvasGroup.gameObject.SetActive(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float startAlpha = canvasGroup.alpha;

        if (fadeInDuration > 0f)
        {
            float timer = 0f;

            while (timer < fadeInDuration)
            {
                timer += Time.unscaledDeltaTime;

                canvasGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    peakAlpha,
                    Mathf.Clamp01(timer / fadeInDuration)
                );

                yield return null;
            }
        }

        canvasGroup.alpha = peakAlpha;

        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        if (fadeOutDuration > 0f)
        {
            float timer = 0f;

            while (timer < fadeOutDuration)
            {
                timer += Time.unscaledDeltaTime;

                canvasGroup.alpha = Mathf.Lerp(
                    peakAlpha,
                    0f,
                    Mathf.Clamp01(timer / fadeOutDuration)
                );

                yield return null;
            }
        }

        SetAlphaImmediate(0f);
        flashRoutine = null;

        if (debugLogs)
            Debug.Log("[SCREEN FLASH] Flash completed.");
    }

    private void SetAlphaImmediate(float alpha)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = Mathf.Clamp01(alpha);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnDisable()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }
}