#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;
using UnityEngine.Splines;

[ExecuteAlways]
public class SplineRoadSideGenerator : MonoBehaviour
{
    [Header("References")]
    public SplineContainer splineContainer;
    public GameObject leftSidePrefab;
    public GameObject rightSidePrefab;

    [Header("Placement")]
    public float sideOffset = 7f;
    public float yOffset = 0.05f;
    public float spacing = 4f;

    [Header("Rotation")]
    public bool alignToSplineDirection = true;
    public Vector3 rotationOffset;

    [Header("Scale")]
    public Vector3 sideScale = Vector3.one;
    public bool randomScaleVariation = false;
    public Vector2 randomScaleRange = new Vector2(0.9f, 1.1f);

    [Header("Parent")]
    public Transform generatedParent;
    public string generatedParentName = "Generated Road Sides";

    [Header("Options")]
    public bool generateLeft = true;
    public bool generateRight = true;
    public bool clearBeforeGenerate = true;

    [Header("Debug")]
    public bool drawDebug = true;
    public float debugSphereSize = 0.4f;

    [ContextMenu("GENERATE ROAD SIDES")]
    public void Generate()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();

        if (splineContainer == null)
        {
            Debug.LogWarning("Brakuje SplineContainer.");
            return;
        }

        if (leftSidePrefab == null && rightSidePrefab == null)
        {
            Debug.LogWarning("Brakuje prefabów do generowania.");
            return;
        }

        if (spacing <= 0.01f)
        {
            Debug.LogWarning("Spacing jest za ma³y.");
            return;
        }

        PrepareParent();

        if (clearBeforeGenerate)
            ClearGenerated();

        float length = splineContainer.CalculateLength();

        if (length <= 0.01f)
        {
            Debug.LogWarning("Spline ma zerow¹ d³ugoœæ.");
            return;
        }

        int count = Mathf.CeilToInt(length / spacing);

        for (int i = 0; i <= count; i++)
        {
            float distance = Mathf.Min(i * spacing, length);
            float t = distance / length;

            Vector3 center = GetWorldPosition(t);
            Vector3 forward = GetWorldForward(t);

            if (forward.sqrMagnitude < 0.001f)
                continue;

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            if (right.sqrMagnitude < 0.001f)
                right = splineContainer.transform.right;

            if (generateLeft && leftSidePrefab != null)
            {
                Vector3 pos = center - right * sideOffset + Vector3.up * yOffset;
                Quaternion rot = GetRotation(forward, false);
                SpawnPrefab(leftSidePrefab, pos, rot);
            }

            if (generateRight && rightSidePrefab != null)
            {
                Vector3 pos = center + right * sideOffset + Vector3.up * yOffset;
                Quaternion rot = GetRotation(forward, true);
                SpawnPrefab(rightSidePrefab, pos, rot);
            }
        }

        Debug.Log("Road sides generated.");
    }

    [ContextMenu("CLEAR GENERATED ROAD SIDES")]
    public void ClearGenerated()
    {
        if (generatedParent == null)
            return;

        for (int i = generatedParent.childCount - 1; i >= 0; i--)
        {
            Transform child = generatedParent.GetChild(i);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }

    void PrepareParent()
    {
        if (generatedParent != null)
            return;

        Transform existing = transform.Find(generatedParentName);

        if (existing != null)
        {
            generatedParent = existing;
            return;
        }

        GameObject parentObj = new GameObject(generatedParentName);
        parentObj.transform.SetParent(transform);
        parentObj.transform.localPosition = Vector3.zero;
        parentObj.transform.localRotation = Quaternion.identity;
        parentObj.transform.localScale = Vector3.one;

        generatedParent = parentObj.transform;
    }

    Vector3 GetWorldPosition(float t)
    {
        Vector3 localPos = splineContainer.Spline.EvaluatePosition(t);

        return splineContainer.transform.TransformPoint(localPos);
    }

    Vector3 GetWorldForward(float t)
    {
        Vector3 localForward = splineContainer.Spline.EvaluateTangent(t);

        Vector3 worldForward = splineContainer.transform.TransformDirection(localForward);
        worldForward.y = 0f;

        if (worldForward.sqrMagnitude < 0.001f)
            return transform.forward;

        return worldForward.normalized;
    }

    Quaternion GetRotation(Vector3 forward, bool rightSide)
    {
        if (!alignToSplineDirection)
            return Quaternion.Euler(rotationOffset);

        Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);

        if (!rightSide)
            rot *= Quaternion.Euler(0f, 180f, 0f);

        rot *= Quaternion.Euler(rotationOffset);

        return rot;
    }

    void SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation)
    {
#if UNITY_EDITOR
        GameObject obj;

        if (!Application.isPlaying)
        {
            obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (obj != null)
            {
                Undo.RegisterCreatedObjectUndo(obj, "Generate Road Side");

                obj.transform.SetPositionAndRotation(position, rotation);
                obj.transform.SetParent(generatedParent);

                ApplyScale(obj.transform);
            }

            return;
        }
#endif

        GameObject runtimeObj = Instantiate(prefab, position, rotation, generatedParent);
        ApplyScale(runtimeObj.transform);
    }

    void ApplyScale(Transform t)
    {
        Vector3 finalScale = sideScale;

        if (randomScaleVariation)
        {
            float rand = Random.Range(randomScaleRange.x, randomScaleRange.y);
            finalScale *= rand;
        }

        t.localScale = finalScale;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawDebug)
            return;

        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();

        if (splineContainer == null)
            return;

        float length = splineContainer.CalculateLength();

        if (length <= 0.01f)
            return;

        int count = Mathf.CeilToInt(length / Mathf.Max(0.1f, spacing));

        for (int i = 0; i <= count; i++)
        {
            float distance = Mathf.Min(i * spacing, length);
            float t = distance / length;

            Vector3 center = GetWorldPosition(t);
            Vector3 forward = GetWorldForward(t);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 leftPos = center - right * sideOffset + Vector3.up * yOffset;
            Vector3 rightPos = center + right * sideOffset + Vector3.up * yOffset;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(leftPos, debugSphereSize);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(rightPos, debugSphereSize);
        }
    }
}