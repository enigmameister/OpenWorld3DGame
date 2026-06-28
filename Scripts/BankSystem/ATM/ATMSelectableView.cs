using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ATMSelectableView : MonoBehaviour
{
    [Header("Refs")]
    public Image selection;          // ramka do migania kursora
    public Image activeSelection;    // STA£E zaznaczenie (opcjonalne) – mo¿esz wskazaæ ten sam Image co selection
    public bool disabled;

    [Header("Blink")]
    public float blinkSpeed = 6f;

    Coroutine _blink;

    void Awake()
    {
        // jeœli nie podepniesz activeSelection, a chcesz minimalnie dzia³aæ:
        // mo¿esz zostawiæ null, wtedy sta³e zaznaczenie po prostu nic nie zrobi
        if (activeSelection) activeSelection.enabled = false;
        if (selection) selection.enabled = false;
    }

    public void SetDisabled(bool v)
    {
        disabled = v;

        if (v)
        {
            StopBlink();
        }

        // disabled: mo¿esz zostawiæ selection.enabled=false (jak u Ciebie),
        // ale wtedy nie bêdzie widaæ "szaroœci". Ja zostawiam tak jak mia³eœ.
        if (selection)
        {
            // jeœli chcesz szary zarys nawet gdy disabled:
            // selection.enabled = true;
            // a = 0.25f
            // u Ciebie by³o: enabled = !v
            selection.enabled = !v;
            var c = selection.color;
            c.a = v ? 0.25f : c.a;
            selection.color = c;
        }

        // sta³e zaznaczenie off gdy disabled
        if (activeSelection) activeSelection.enabled = false;
    }

    /// <summary>
    /// Migaj¹ce zaznaczenie kursora
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (!selection) return;

        if (disabled)
        {
            StopBlink();
            selection.enabled = true;
            return;
        }

        if (selected) StartBlink();
        else StopBlink();
    }

    /// <summary>
    /// Sta³e zaznaczenie (np. wybrany tryb albo wybrana kwota)
    /// </summary>
    public void SetActiveSelected(bool on)
    {
        if (disabled) on = false;

        if (activeSelection)
        {
            activeSelection.enabled = on;

            // opcjonalnie: ustaw pe³n¹ alfê gdy active
            var c = activeSelection.color;
            c.a = on ? 1f : 0f;
            activeSelection.color = c;
        }
        else
        {
            // jeœli nie masz activeSelection – fallback:
            // gdy "on", poka¿ selection bez migania
            if (selection)
            {
                if (on)
                {
                    StopBlink();
                    selection.enabled = true;
                    var c = selection.color;
                    c.a = 1f;
                    selection.color = c;
                }
                else
                {
                    // nie gaœ kursora jeœli akurat miga (to robi SetSelected)
                    // tu tylko wy³¹cz "sta³e" podœwietlenie
                    if (_blink == null) selection.enabled = false;
                }
            }
        }
    }


    void StartBlink()
    {
        if (_blink != null) StopCoroutine(_blink);
        _blink = StartCoroutine(Blink());
    }

    void StopBlink()
    {
        if (_blink != null) StopCoroutine(_blink);
        _blink = null;

        if (selection) selection.enabled = false;
    }

    IEnumerator Blink()
    {
        selection.enabled = true;

        while (true)
        {
            var c = selection.color;
            c.a = 0.35f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * blinkSpeed)) * 0.65f;
            selection.color = c;
            yield return null;
        }
    }
}
