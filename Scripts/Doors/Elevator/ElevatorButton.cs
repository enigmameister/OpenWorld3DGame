// ElevatorButton.cs
using UnityEngine;

public class ElevatorButton : MonoBehaviour, IPressable
{
    public enum Mode { CallFromOutside, RequestInCar }

    [Header("Powiązania")]
    public ElevatorController controller;
    public Mode mode = Mode.CallFromOutside;

    [Header("Parametry")]
    [Tooltip("Indeks piętra (0 = parter, 1 = 1. piętro, ...).")]
    public int floorIndex;

    [Header("Feedback (opcjonalnie)")]
    public Renderer indicator;                 // np. mały LED na przycisku
    public Color hoverColor = Color.cyan;
    public Color idleColor = Color.white;

    Color _original;
    void Awake()
    {
        if (!indicator) indicator = GetComponentInChildren<Renderer>();
        if (indicator) _original = indicator.sharedMaterial.color;
    }

    public void Press()
    {
        if (!controller) return;
        if (mode == Mode.CallFromOutside)
            controller.CallFromOutside(floorIndex);
        else
            controller.RequestFloor(floorIndex);
#if UNITY_EDITOR
        Debug.Log($"[Button] {Label} pressed.");
#endif
    }

    public string Label => $"{mode} #{floorIndex}";

    // Te metody zawoła raycaster do wizualnego podświetlenia (opcjonalne)
    public void SetHover(bool on)
    {
        if (!indicator) return;
        indicator.material.color = on ? hoverColor : idleColor;
    }
}
