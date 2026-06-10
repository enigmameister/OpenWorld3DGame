using UnityEngine;

public static class BankCardColors
{
    // fallback tylko dla sytuacji, gdy BankSystem jeszcze nie istnieje (np. edit-time / test scene)
    private static readonly Color[] Fallback =
    {
        Color.white,
        new Color(0.2f, 0.6f, 1f),   // blue
        new Color(0.9f, 0.2f, 0.2f), // red
        new Color(0.1f, 0.8f, 0.4f), // green
        new Color(1f, 0.75f, 0.1f),  // gold
    };

    public static Color Get(int index)
    {
        // ✅ source of truth: BankSystem -> Variant DB
        if (BankSystem.Instance != null)
            return BankSystem.Instance.GetVariantColor(index);

        // fallback
        if (Fallback == null || Fallback.Length == 0) return Color.white;
        return Fallback[Mathf.Clamp(index, 0, Fallback.Length - 1)];
    }
}
