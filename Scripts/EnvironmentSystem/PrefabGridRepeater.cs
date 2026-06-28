// Assets/Scripts/Enviroment/PrefabGridRepeater.cs
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PrefabGridRepeater : MonoBehaviour
{
    public GameObject prefab;
    [Min(1)] public int countX = 10;
    [Min(1)] public int countY = 1;
    public bool centerGrid = false;
    public bool autoSpacingFromPrefabBounds = true;   // auto: z Renderer.bounds
    public Vector2 spacing = new Vector2(1, 1);       // u¿ywane gdy autoSpacing=false
    public bool clearBeforeBuild = true;

    // Jeœli bardzo chcesz podgl¹d „na ¿ywo”, w³¹cz to, ale robimy to
    // po walidacji (delayCall), by unikn¹æ b³êdu SendMessage.
    public bool autoRebuildInEditor = false;

#if UNITY_EDITOR
    static bool _scheduled;
#endif

    const string RootName = "_GridRoot";

    Transform EnsureRoot()
    {
        var t = transform.Find(RootName);
        if (t == null)
        {
            var go = new GameObject(RootName);
            t = go.transform;
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(go, "Create Grid Root");
#endif
            t.SetParent(transform, false);
        }
        return t;
    }

    public void Clear()
    {
        var t = transform.Find(RootName);
        if (!t) return;
#if UNITY_EDITOR
        Undo.DestroyObjectImmediate(t.gameObject);
#else
        DestroyImmediate(t.gameObject);
#endif
    }

    Vector2 CalcStep()
    {
        if (!autoSpacingFromPrefabBounds || !prefab) return spacing;
        var r = prefab.GetComponentInChildren<Renderer>();
        if (!r) return spacing;
        var s = r.bounds.size;
        return new Vector2(Mathf.Max(0.0001f, s.x), Mathf.Max(0.0001f, s.z));
    }

    public void Rebuild()
    {
        if (!prefab) { Debug.LogWarning("PrefabGridRepeater: assign a Prefab."); return; }

        var root = EnsureRoot();

        if (clearBeforeBuild) Clear(); // kasujemy stare, tworzymy nowy root
        root = EnsureRoot();

        Vector2 step = CalcStep();
        Vector3 origin = centerGrid
            ? new Vector3(-(countX - 1) * step.x * 0.5f, 0f, -(countY - 1) * step.y * 0.5f)
            : Vector3.zero;

#if UNITY_EDITOR
        Undo.IncrementCurrentGroup();
#endif

        for (int y = 0; y < countY; y++)
            for (int x = 0; x < countX; x++)
            {
                Vector3 lp = origin + new Vector3(x * step.x, 0f, y * step.y);

#if UNITY_EDITOR
                // KLUCZ: instancjuj OD RAZU pod parentem -> brak SetParent w walidacji
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                Undo.RegisterCreatedObjectUndo(go, "Grid place");
#else
            var go = Instantiate(prefab, root);
#endif
                go.name = $"{prefab.name}_{x}_{y}";
                var t = go.transform;
                t.localPosition = lp;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
            }

#if UNITY_EDITOR
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        countX = Mathf.Max(1, countX);
        countY = Mathf.Max(1, countY);

        if (!autoRebuildInEditor) return;
        if (!prefab) return;

        // Nie rebuildujemy NATYCHMIAST (to generuje b³¹d),
        // tylko odk³adamy na nastêpn¹ pêtlê edytora.
        if (_scheduled) return;
        _scheduled = true;
        EditorApplication.delayCall += () =>
        {
            _scheduled = false;
            if (!this) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            Rebuild();
        };
    }

    [CustomEditor(typeof(PrefabGridRepeater))]
    public class PrefabGridRepeaterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var r = (PrefabGridRepeater)target;

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Grid")) r.Rebuild();
                if (GUILayout.Button("Clear Grid")) r.Clear();
            }

            if (r.autoSpacingFromPrefabBounds && r.prefab)
            {
                var rr = r.prefab.GetComponentInChildren<Renderer>();
                if (rr)
                {
                    var s = rr.bounds.size;
                    EditorGUILayout.HelpBox($"Auto spacing from prefab bounds: {s.x:0.###} x {s.z:0.###}", MessageType.Info);
                }
            }
        }
    }
#endif
}
