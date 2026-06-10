using UnityEngine;

public static class GameObjectUtil
{
    public static void CopyTagAndLayer(GameObject src, GameObject dst, bool includeChildren = true)
    {
        if (!src || !dst) return;

        // Tag
        try { dst.tag = src.tag; } catch { /* tag móg³ nie istnieæ w Tag Managerze */ }

        // Layer
        dst.layer = src.layer;

        if (!includeChildren) return;

        // Rekurencyjnie po dzieciach – parujemy po indeksie
        int childCount = Mathf.Min(src.transform.childCount, dst.transform.childCount);
        for (int i = 0; i < childCount; i++)
        {
            var sc = src.transform.GetChild(i).gameObject;
            var dc = dst.transform.GetChild(i).gameObject;
            CopyTagAndLayer(sc, dc, true);
        }
    }
}
