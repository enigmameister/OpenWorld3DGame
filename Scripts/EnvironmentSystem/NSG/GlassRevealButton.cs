using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GlassRevealButton : MonoBehaviour, IPressable
{
    public GlassGameManager glassGameManager;
    public Renderer buttonRenderer;
    public Color idleColor = Color.red;
    public Color activeColor = Color.green;

    [SerializeField] private string revealLabel = "Poka¿ szk³o";
    [SerializeField] private string hideLabel = "Ukryj podgl¹d szk³a";

    private bool _revealed = false;

    public string Label => _revealed ? hideLabel : revealLabel;

    private void Start()
    {
        if (buttonRenderer != null)
            buttonRenderer.material.color = idleColor;
    }

    public void Press()
    {
        _revealed = !_revealed;

        if (glassGameManager != null)
            glassGameManager.RevealAll(_revealed);

        if (buttonRenderer != null)
            buttonRenderer.material.color = _revealed ? activeColor : idleColor;
    }
}