using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ATMMenuButtonView : MonoBehaviour
{
    [Header("Refs")]
    public Button button;
    public Image selection;          // image/ramka która ma migaæ

    public GameObject Active;
    public GameObject Selected;

    [Header("Blink")]
    public float blinkSpeed = 6f;

    private Coroutine _blink;
    private bool _disabled;

    void Reset()
    {
        button = GetComponent<Button>();
    }

    public void SetDisabled(bool disabled)
    {
        _disabled = disabled;
        if (button) button.interactable = !disabled;

        if (selection)
        {
            selection.enabled = true; // mo¿e zostaæ widoczne jako szare
            var c = selection.color;
            c.a = 1f;
            selection.color = c;
        }
    }

    public void SetSelected(bool selected)
    {
        if (!selection) return;

        if (_disabled)
        {
            StopBlink();
            selection.enabled = true;
            // opcjonalnie: szarawa ramka
            var c = selection.color;
            c.a = 0.35f;
            selection.color = c;
            return;
        }

        if (selected) StartBlink();
        else StopBlink();
    }

    private void StartBlink()
    {
        if (_blink != null) StopCoroutine(_blink);
        _blink = StartCoroutine(Blink());
    }

    private void StopBlink()
    {
        if (_blink != null) StopCoroutine(_blink);
        _blink = null;
        if (selection) selection.enabled = false;
    }

    public void SetHoldActive(bool hold)
    {
        if (hold)
        {
            // wy³¹cz miganie
            SetSelected(false);

            if (Active) Active.SetActive(true);
        }
        else
        {
            if (Active) Active.SetActive(false);
        }
    }

    private IEnumerator Blink()
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
