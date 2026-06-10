using UnityEngine;

[CreateAssetMenu(menuName = "Bank/Card Variant Database")]
public class BankCardVariantDatabase : ScriptableObject
{
    public Color[] variants = new Color[]
    {
        Color.white,
        new Color(0.2f, 0.6f, 1f),   // blue
        new Color(0.9f, 0.2f, 0.2f), // red
        new Color(0.1f, 0.8f, 0.4f), // green
        new Color(1f, 0.75f, 0.1f),  // gold
    };

    public int Count => variants != null ? variants.Length : 0;

    public Color Get(int index)
    {
        if (variants == null || variants.Length == 0) return Color.white;
        return variants[Mathf.Clamp(index, 0, variants.Length - 1)];
    }
}
