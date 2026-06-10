using System;
using System.Collections;   // ✅ TO NAPRAWIA IEnumerator
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BankCardEntryView : MonoBehaviour, IPointerClickHandler
{
    [Header("TMP")]
    [SerializeField] private TMP_Text variantText;
    [SerializeField] private TMP_Text idText;
    [SerializeField] private TMP_Text statusText;

    [Header("Visual")]
    [SerializeField] private Image colorPreview;
    [SerializeField] private GameObject selectionGO;
    [SerializeField] private Image iconCardImage;
    [SerializeField] private Sprite defaultCardSprite;

    [Header("Selection Blink")]
    [SerializeField] private CanvasGroup selectionCanvas; // na Selection
    [SerializeField] private float blinkSpeed = 4f;
    [SerializeField] private float minAlpha = 0.15f;
    [SerializeField] private float maxAlpha = 0.85f;

    private Coroutine _blinkCo;
    private Action _onClick;

    public void Bind(BankCardRecord rec)
    {
        // Jeśli potrzebujesz 1-based w UI: int uiVariant = rec.colorVariant + 1;
        int uiVariant = rec.colorVariant;

        if (variantText) variantText.text = $"VARIANT: {uiVariant}";
        if (idText) idText.text = $"ID: {rec.cardId}";
        if (statusText) statusText.text = $"STATUS: {rec.status.ToString().ToUpperInvariant()}";

        var bank = BankSystem.Instance;

        if (bank != null)
        {
            if (colorPreview) colorPreview.color = bank.GetVariantColor(rec.colorVariant);

            if (iconCardImage)
            {
                iconCardImage.sprite = defaultCardSprite != null ? defaultCardSprite : iconCardImage.sprite;
                iconCardImage.color = bank.GetVariantColor(rec.colorVariant);
            }
        }
    }

    public void SetSelected(bool v)
    {
        if (selectionGO) selectionGO.SetActive(v);

        if (!selectionCanvas) return;

        if (v)
        {
            if (_blinkCo != null) StopCoroutine(_blinkCo);
            _blinkCo = StartCoroutine(CoBlink());
        }
        else
        {
            if (_blinkCo != null) StopCoroutine(_blinkCo);
            _blinkCo = null;
            selectionCanvas.alpha = minAlpha;
        }
    }

    private IEnumerator CoBlink()
    {
        selectionCanvas.alpha = maxAlpha;

        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * blinkSpeed) + 1f) * 0.5f; // 0..1
            selectionCanvas.alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
            yield return null;
        }
    }

    public void SetOnClick(Action onClick) => _onClick = onClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        _onClick?.Invoke();
    }
}
