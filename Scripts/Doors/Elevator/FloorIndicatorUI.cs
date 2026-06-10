using System.Collections;
using UnityEngine;
using TMPro;

public class FloorIndicatorUI : MonoBehaviour
{
    [Header("Refs (grupy z cyfrą i strzałką)")]
    [SerializeField] private RectTransform slideA;
    [SerializeField] private RectTransform slideB;
    [SerializeField] private TextMeshProUGUI numberA;
    [SerializeField] private TextMeshProUGUI numberB;
    [SerializeField] private TextMeshProUGUI arrowA;
    [SerializeField] private TextMeshProUGUI arrowB;

    [Header("Anim")]
    [SerializeField] private float slideTime = 0.40f;
    [SerializeField]
    private AnimationCurve slideCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Layout")]
    [Tooltip("Dodatkowy offset strzałki względem numeru (piksele, + w dół).")]
    [SerializeField] private float arrowYOffset = -18f;

    [Header("Znaki")]
    [SerializeField] private string idleMark = "-";   // winda stoi
    [SerializeField] private string upMark = "▲";   // jazda w górę
    [SerializeField] private string downMark = "▼";   // jazda w dół

    RectTransform _panel;
    bool _useA = true;
    bool _animating = false;
    int _currentFloor;

    // bezpieczny dostęp do RectTransform
    RectTransform Panel
    {
        get
        {
            if (_panel == null)
                _panel = transform as RectTransform;   // działa nawet jeśli Awake się jeszcze nie wykonał
            return _panel;
        }
    }

    // jeśli Panel jest null (skrypt podpięty do nie-UI), po prostu zwracamy 0
    float ViewportH => Panel ? Panel.rect.height : 0f;
    void Awake()
    {
        AlignArrows();
        Snap(_currentFloor);
    }

    void AlignArrows()
    {
        if (arrowA) arrowA.rectTransform.anchoredPosition =
            new Vector2(0, arrowYOffset);
        if (arrowB) arrowB.rectTransform.anchoredPosition =
            new Vector2(0, arrowYOffset);
    }

    public void Snap(int floor)
    {
        _currentFloor = floor;
        string txt = floor.ToString();
        if (numberA) numberA.text = txt;
        if (numberB) numberB.text = txt;

        // winda stoi → kreska
        if (arrowA) arrowA.text = idleMark;
        if (arrowB) arrowB.text = idleMark;

        if (slideA) slideA.anchoredPosition = Vector2.zero;
        if (slideB) slideB.anchoredPosition = new Vector2(0, -ViewportH);

        _useA = true;
        _animating = false;
        StopAllCoroutines();
    }

    public void TickTo(int nextFloor, bool up)
    {
        if (nextFloor == _currentFloor && !_animating) return;
        _currentFloor = nextFloor;
        StopAllCoroutines();
        StartCoroutine(SlideRoutine(nextFloor, up));
    }

    IEnumerator SlideRoutine(int next, bool up)
    {
        _animating = true;

        var fromRT = _useA ? slideA : slideB;
        var toRT = _useA ? slideB : slideA;
        var fromNum = _useA ? numberA : numberB;
        var toNum = _useA ? numberB : numberA;
        var fromArr = _useA ? arrowA : arrowB;
        var toArr = _useA ? arrowB : arrowA;

        if (toNum) toNum.text = next.ToString();

        // ustaw symbol strzałki dla obu slajdów
        string mark = up ? upMark : downMark;
        if (fromArr) fromArr.text = mark;
        if (toArr) toArr.text = mark;

        float h = ViewportH;
        Vector2 fromStart = Vector2.zero;
        Vector2 fromEnd = up ? new Vector2(0, +h) : new Vector2(0, -h);
        Vector2 toStart = up ? new Vector2(0, -h) : new Vector2(0, +h);
        Vector2 toEnd = Vector2.zero;

        if (toRT) toRT.anchoredPosition = toStart;

        float t = 0f, dur = Mathf.Max(0.0001f, slideTime);
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = slideCurve.Evaluate(Mathf.Clamp01(t));
            if (fromRT) fromRT.anchoredPosition = Vector2.LerpUnclamped(fromStart, fromEnd, k);
            if (toRT) toRT.anchoredPosition = Vector2.LerpUnclamped(toStart, toEnd, k);
            yield return null;
        }

        if (fromRT) fromRT.anchoredPosition = fromEnd;
        if (toRT) toRT.anchoredPosition = Vector2.zero;

        // po zakończeniu przewijania nie zmieniamy strzałki — zostaje aż do Snap()
        _useA = !_useA;
        _animating = false;
    }

    // Ustaw strzałkę natychmiast (bez przesuwania licznika)
    public void SetDirection(int dir) // dir: -1 = dół, 0 = postój, +1 = góra
    {
        string mark = dir > 0 ? upMark : dir < 0 ? downMark : idleMark;
        if (arrowA) arrowA.text = mark;
        if (arrowB) arrowB.text = mark;
    }

    // Wygodne aliasy (opcjonalnie)
    public void SetIdle() => SetDirection(0);
    public void SetUp() => SetDirection(+1);
    public void SetDown() => SetDirection(-1);

}
