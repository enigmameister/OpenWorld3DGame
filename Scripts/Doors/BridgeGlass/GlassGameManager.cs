using UnityEngine;

public class GlassGameManager : MonoBehaviour
{
    [System.Serializable]
    public class GlassPair
    {
        public GlassTile tileA;
        public GlassTile tileB;
    }

    public GlassPair[] pairs;
    public int randomSeed = -1;

    private void Awake()
    {
        if (randomSeed != -1)
            Random.InitState(randomSeed);

        foreach (var pair in pairs)
        {
            if (pair.tileA == null || pair.tileB == null)
            {
                Debug.LogWarning("GlassGameManager: para ma puste referencje!", this);
                continue;
            }

            bool aIsFragile = Random.value < 0.5f;

            pair.tileA.SetFragile(aIsFragile);
            pair.tileB.SetFragile(!aIsFragile);
        }
    }

    // === HELPER: pokazuje lub chowa wszystkie kafelki ===
    public void RevealAll(bool reveal)
    {
        foreach (var pair in pairs)
        {
            if (pair.tileA != null) pair.tileA.Reveal(reveal);
            if (pair.tileB != null) pair.tileB.Reveal(reveal);
        }
    }
}
