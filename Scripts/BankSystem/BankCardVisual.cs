using TMPro;
using UnityEngine;

public class BankCardVisual : MonoBehaviour
{
    [Header("TMP")]
    [SerializeField] private TMP_Text idText;
    [SerializeField] private TMP_Text pinText;
    [SerializeField] private TMP_Text statusText;

    [Header("Background renderers (2 materiały / 2 rendery)")]
    [SerializeField] private Renderer bgRendererA;
    [SerializeField] private Renderer bgRendererB;

    [Header("Colors")]
    [SerializeField]

    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        // ✅ dopiero tutaj wolno
        _mpb = new MaterialPropertyBlock();
    }

    public void Apply(BankCardMeta meta)
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock(); // safety (np. edit-time)

        if (idText) idText.text = meta.cardId;
        if (pinText) pinText.text = meta.pin.ToString("0000");
        if (statusText) statusText.text = meta.status.ToString().ToUpperInvariant();

        var c = (BankSystem.Instance != null)
            ? BankSystem.Instance.GetVariantColor(meta.colorVariant)
            : Color.white;

        ApplyColor(bgRendererA, c);
        ApplyColor(bgRendererB, c);
    }

    private void ApplyColor(Renderer r, Color c)
    {
        if (!r) return;

        r.GetPropertyBlock(_mpb);

        if (r.sharedMaterial && r.sharedMaterial.HasProperty(BaseColorId))
            _mpb.SetColor(BaseColorId, c);
        else
            _mpb.SetColor(ColorId, c);

        r.SetPropertyBlock(_mpb);
    }

    public void SetStatus(BankCardStatus status)
    {
        if (statusText)
            statusText.text = status.ToString().ToUpperInvariant();
    }
}
