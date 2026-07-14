using UnityEngine;

public class ElevatorButton : MonoBehaviour, IPressable
{
    public enum Mode { CallFromOutside, RequestInCar }

    [Header("Powiązania")]
    public ElevatorController controller;
    public Mode mode = Mode.CallFromOutside;

    [Header("Parametry")]
    public int floorIndex;

    [Header("Anti double press")]
    [SerializeField] private float pressCooldown = 0.25f;
    private float nextAllowedPressTime;

    [Header("Feedback")]
    public Renderer indicator;
    public Color hoverColor = Color.cyan;
    public bool useOriginalAsIdleColor = true;
    public Color idleColor = Color.white;

    [Header("Shader Color Property")]
    [SerializeField] private string colorProperty = "_BaseColor";

    private Material runtimeMaterial;
    private Color originalColor;

    void Awake()
    {
        if (!indicator)
            indicator = GetComponentInChildren<Renderer>();

        if (indicator)
        {
            runtimeMaterial = indicator.material;

            if (runtimeMaterial.HasProperty(colorProperty))
                originalColor = runtimeMaterial.GetColor(colorProperty);
            else
                originalColor = runtimeMaterial.color;

            if (useOriginalAsIdleColor)
                idleColor = originalColor;
        }
    }

    public void Press()
    {
        if (!controller) return;

        if (Time.time < nextAllowedPressTime)
            return;

        nextAllowedPressTime = Time.time + pressCooldown;

        if (mode == Mode.CallFromOutside)
            controller.CallFromOutside(floorIndex);
        else
            controller.RequestFloor(floorIndex);

#if UNITY_EDITOR
        Debug.Log($"[Button] {Label} pressed.");
#endif
    }

    public string Label => $"{mode} #{floorIndex}";

    public void SetHover(bool on)
    {
        if (runtimeMaterial == null)
            return;

        Color target = on ? hoverColor : idleColor;

        // Base color
        if (runtimeMaterial.HasProperty(colorProperty))
            runtimeMaterial.SetColor(colorProperty, target);
        else if (runtimeMaterial.HasProperty("_BaseColor"))
            runtimeMaterial.SetColor("_BaseColor", target);
        else if (runtimeMaterial.HasProperty("_Color"))
            runtimeMaterial.SetColor("_Color", target);
        else
            runtimeMaterial.color = target;

        // Emission / glow, żeby kolor był widoczny nawet w ciemności.
        Color emission = on ? target * 2.5f : Color.black;

        if (runtimeMaterial.HasProperty("_EmissionColor"))
        {
            runtimeMaterial.EnableKeyword("_EMISSION");
            runtimeMaterial.SetColor("_EmissionColor", emission);
        }
    }
}