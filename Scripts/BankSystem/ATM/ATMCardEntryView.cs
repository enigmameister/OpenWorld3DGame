using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ATMCardEntryView : MonoBehaviour
{
    [Header("UI refs")]
    public Image selection;
    public Image iconCard;
    public Image cardVariant;
    public TextMeshProUGUI idText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI variantText;

    [Header("Blink")]
    public float blinkSpeed = 6f;

    private Coroutine _blink;
    private bool _blocked;

    // zapamiêtane “normalne” kolory
    private Color _iconNormal = Color.white;
    private Color _variantNormal = Color.white;
    private Color _statusNormal = Color.white;
    private Color _selectionNormal = Color.white;
    private bool _cached;

    private Color _iconMetaColor = Color.white;
    private Color _variantMetaColor = Color.white;

    void CacheDefaultsIfNeeded()
    {
        if (_cached) return;
        _cached = true;

        if (iconCard != null) _iconNormal = iconCard.color;
        if (cardVariant != null) _variantNormal = cardVariant.color;
        if (statusText != null) _statusNormal = statusText.color;
        if (selection != null) _selectionNormal = selection.color;
    }

    public void Bind(InventoryItemInstance inst)
    {
        CacheDefaultsIfNeeded();
        if (inst == null || inst.data == null) return;

        // icon
        if (iconCard != null)
        {
            iconCard.sprite = inst.data.icon;
            iconCard.enabled = iconCard.sprite != null;

            _iconMetaColor = inst.hasBankCardMeta
                ? BankCardColors.Get(inst.bankCard.colorVariant)
                : _iconNormal;

            iconCard.color = _iconMetaColor;
        }

        // wariant (jeœli dalej chcesz go kolorowaæ)
        if (cardVariant != null)
        {
            _variantMetaColor = inst.hasBankCardMeta
                ? BankCardColors.Get(inst.bankCard.colorVariant)
                : _variantNormal;

            cardVariant.color = _variantMetaColor;
        }


        // ID + status
        if (idText != null)
            idText.text = inst.hasBankCardMeta ? $"ID: {inst.bankCard.cardId}" : "ID: -";

        string statusStr = inst.hasBankCardMeta
            ? inst.bankCard.status.ToString().ToUpperInvariant()
            : "-";

        if (statusText != null)
        {
            statusText.text = $"STATUS: {statusStr}";
            statusText.color = _statusNormal;
        }

        if (variantText != null)
        {
            variantText.text = inst.hasBankCardMeta
                ? $"VARIANT: {inst.bankCard.colorVariant}"
                : "VARIANT: —";
        }
    }

    public void SetBlocked(bool blocked)
    {
        CacheDefaultsIfNeeded();
        _blocked = blocked;

        // szarzenie
        if (iconCard != null)
            iconCard.color = blocked ? new Color(0.45f, 0.45f, 0.45f, 1f) : _iconMetaColor;


        // UWAGA: wariant ma wracaæ do “normalnego” (czyli tego z Bind)
        if (cardVariant != null)
            cardVariant.color = blocked ? new Color(0.25f, 0.25f, 0.25f, 1f) : _variantMetaColor;

        // status na czerwono
        if (statusText != null)
            statusText.color = blocked ? Color.red : _statusNormal;

        // selection: jeœli blocked — zawsze widoczna szara ramka
        if (selection != null)
        {
            selection.enabled = blocked; // tylko dla blocked pokazuj stale
            var c = blocked ? new Color(0.35f, 0.35f, 0.35f, 1f) : _selectionNormal;
            selection.color = c;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selection == null) return;

        // blocked: nie migamy
        if (_blocked)
        {
            StopBlink();
            selection.enabled = true;
            return;
        }

        if (selected) StartBlink();
        else StopBlink();
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

        // jak nie zaznaczone — niewidoczne
        if (!_blocked)
            selection.enabled = false;
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
