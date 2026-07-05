#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class QuickSaveEntityValidator
{
    [MenuItem("Tools/Quick Save/Validate Save IDs")]
    public static void ValidateSaveIds()
    {
        QuickSaveEntity[] entities = Object.FindObjectsByType<QuickSaveEntity>(FindObjectsSortMode.None);

        Dictionary<string, List<QuickSaveEntity>> byId = new Dictionary<string, List<QuickSaveEntity>>();

        for (int i = 0; i < entities.Length; i++)
        {
            QuickSaveEntity entity = entities[i];

            if (entity == null)
                continue;

            string id = entity.SaveId;

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!byId.TryGetValue(id, out List<QuickSaveEntity> list))
            {
                list = new List<QuickSaveEntity>();
                byId.Add(id, list);
            }

            list.Add(entity);
        }

        int duplicateGroups = 0;

        foreach (var pair in byId)
        {
            if (pair.Value.Count <= 1)
                continue;

            duplicateGroups++;

            Debug.LogWarning(
                $"[QuickSaveEntityValidator] Duplicate SaveId={pair.Key}, count={pair.Value.Count}"
            );

            for (int i = 0; i < pair.Value.Count; i++)
            {
                QuickSaveEntity entity = pair.Value[i];

                Debug.LogWarning(
                    $"Duplicate object: {GetHierarchyPath(entity.transform)}",
                    entity
                );
            }
        }

        if (duplicateGroups == 0)
        {
            Debug.Log($"[QuickSaveEntityValidator] OK. Checked {entities.Length} QuickSaveEntity objects. No duplicates.");
        }
        else
        {
            Debug.LogWarning($"[QuickSaveEntityValidator] Found {duplicateGroups} duplicate SaveId groups.");
        }
    }

    [MenuItem("Tools/Quick Save/Regenerate Duplicate Save IDs")]
    public static void RegenerateDuplicateSaveIds()
    {
        QuickSaveEntity[] entities = Object.FindObjectsByType<QuickSaveEntity>(FindObjectsSortMode.None);

        Dictionary<string, List<QuickSaveEntity>> byId = new Dictionary<string, List<QuickSaveEntity>>();

        for (int i = 0; i < entities.Length; i++)
        {
            QuickSaveEntity entity = entities[i];

            if (entity == null)
                continue;

            string id = entity.SaveId;

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!byId.TryGetValue(id, out List<QuickSaveEntity> list))
            {
                list = new List<QuickSaveEntity>();
                byId.Add(id, list);
            }

            list.Add(entity);
        }

        int changed = 0;

        foreach (var pair in byId)
        {
            List<QuickSaveEntity> duplicates = pair.Value;

            if (duplicates.Count <= 1)
                continue;

            // Pierwszy obiekt zostawiamy bez zmian, reszcie generujemy nowe ID.
            for (int i = 1; i < duplicates.Count; i++)
            {
                QuickSaveEntity entity = duplicates[i];

                SerializedObject so = new SerializedObject(entity);
                SerializedProperty saveIdProp = so.FindProperty("saveId");

                if (saveIdProp == null)
                    continue;

                saveIdProp.stringValue = System.Guid.NewGuid().ToString("N");
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(entity);
                changed++;
            }
        }

        Debug.Log($"[QuickSaveEntityValidator] Regenerated duplicate Save IDs: {changed}");
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
            return "<null>";

        string path = t.name;

        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }
}
#endif