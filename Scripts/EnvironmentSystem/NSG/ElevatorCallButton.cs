using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ElevatorCallButton : MonoBehaviour, IPressable
{
    public SimpleElevator elevator;

    [Tooltip("Jeœli zaznaczone – przycisk wo³a windê na górê. Jeœli odznaczone – na dó³.")]
    public bool callToTop = false;

    [Header("Kolor przycisku")]
    public Renderer buttonRenderer;
    public Color idleColor = Color.red;
    public Color activeColor = Color.green;

    [SerializeField] private string topLabel = "Wezwij windê na górê";
    [SerializeField] private string bottomLabel = "Wezwij windê na dó³";

    private bool _isActive = false;

    public string Label => callToTop ? topLabel : bottomLabel;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false;
    }

    private void Start()
    {
        if (buttonRenderer != null)
            buttonRenderer.material.color = idleColor;
    }

    public void Press()
    {
        if (elevator == null)
            return;

        if (callToTop)
            elevator.CallToTop();
        else
            elevator.CallToBottom();

        _isActive = !_isActive;

        if (buttonRenderer != null)
            buttonRenderer.material.color = _isActive ? activeColor : idleColor;
    }
}