using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class LadderZone : MonoBehaviour
{
    [Header("Ladder Area")]
    [SerializeField] private BoxCollider zoneCollider;

    [Tooltip("Kierunek w prawo po drabinie. Jeœli puste, u¿yje transform.right.")]
    [SerializeField] private Transform ladderRightReference;

    [Tooltip("Margines od krawêdzi triggera, ¿eby CharacterController nie wypada³ zbyt ³atwo.")]
    [SerializeField] private float edgePadding = 0.15f;

    public Vector3 Right =>
        ladderRightReference != null ? ladderRightReference.right : transform.right;

    public Vector3 Up => Vector3.up;

    private void Awake()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<BoxCollider>();

        zoneCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerMovement pm = other.GetComponent<PlayerMovement>();
        if (pm != null)
            pm.EnterLadder(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerMovement pm = other.GetComponent<PlayerMovement>();
        if (pm != null)
            pm.ExitLadder(this);
    }

    public Vector3 ClampWorldPositionToZone(Vector3 worldPosition, float playerRadius)
    {
        if (zoneCollider == null)
            return worldPosition;

        Transform t = zoneCollider.transform;

        Vector3 local = t.InverseTransformPoint(worldPosition);

        Vector3 center = zoneCollider.center;
        Vector3 half = zoneCollider.size * 0.5f;

        float padding = Mathf.Max(edgePadding, playerRadius * 0.35f);

        local.x = Mathf.Clamp(local.x, center.x - half.x + padding, center.x + half.x - padding);
        local.y = Mathf.Clamp(local.y, center.y - half.y + padding, center.y + half.y - padding);
        local.z = Mathf.Clamp(local.z, center.z - half.z + padding, center.z + half.z - padding);

        return t.TransformPoint(local);
    }
}