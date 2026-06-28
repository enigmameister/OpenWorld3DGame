using UnityEngine;

[RequireComponent(typeof(Light))]
public class LightIndicator : MonoBehaviour
{
    private Light pointLight;

    public Color hitColor = Color.green;
    public Color defaultColor = Color.red;

    void Awake()
    {
        pointLight = GetComponent<Light>();
    }

    public void Initialize(Color initialColor)
    {
        SetColor(initialColor);
    }

    public void SetColor(Color color)
    {
        if (pointLight != null)
            pointLight.color = color;
    }

    public void MarkHit() => SetColor(hitColor);

    public void ResetLight() => SetColor(defaultColor);
}
