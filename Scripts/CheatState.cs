// CheatState.cs
using UnityEngine;

public static class CheatState
{
    // aktywacja konsoli-cheatów
    public static bool CheatsUnlocked { get; private set; } = false;

    // flagi cheatów
    public static bool Invincible { get; set; } = false;       // SAIYAN
    public static bool InfiniteStamina { get; set; } = false;   // CO2
    public static bool InfiniteAmmo { get; set; } = false;      // ELWRAY
    public static bool Alliance { get; set; } = false;          // ALLIANCE (NPC nie atakuj¹)

    // prêdkoœæ gracza (mno¿nik 1.0 = domyœlnie)
    public static float PlayerSpeedMultiplier { get; set; } = 1f;

    public static void UnlockCheats() => CheatsUnlocked = true;

    // RESET: zeruje wszystko oprócz samej aktywacji FRAUD
    public static void ResetAll()
    {
        Invincible = false;
        InfiniteStamina = false;
        InfiniteAmmo = false;
        Alliance = false;
        PlayerSpeedMultiplier = 1f;
    }
}
