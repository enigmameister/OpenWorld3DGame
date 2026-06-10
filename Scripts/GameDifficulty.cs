public enum Difficulty { Easy, Normal, Hard }

public static class GameDifficulty
{
    // by³o: Difficulty.Easy
    public static Difficulty Current = Difficulty.Normal;

    // mno¿nik tylko dla obra¿eñ zadawanych przez NPC
    public static float NpcDamageMultiplier =>
        Current switch
        {
            Difficulty.Easy => 0.2f,
            Difficulty.Normal => 0.5f,
            Difficulty.Hard => 0.7f,
            _ => 0.5f
        };
}
