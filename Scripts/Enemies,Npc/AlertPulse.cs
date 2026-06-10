using UnityEngine;

public class AlertPulse : MonoBehaviour
{
    private SpriteRenderer sr;
    private float baseAlpha;
    public float pulseSpeed = 3f;
    public float pulseIntensity = 0.3f;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void OnEnable()
    {
        if (sr != null) baseAlpha = sr.color.a;
    }

    void Update()
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = baseAlpha + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
        sr.color = c;
    }
}
