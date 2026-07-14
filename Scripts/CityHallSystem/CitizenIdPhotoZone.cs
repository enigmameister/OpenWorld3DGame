using UnityEngine;

public class CitizenIdPhotoZone : MonoBehaviour
{
    [SerializeField] private CitizenIdPhotoSequence sequence;

    private int playerColliderCount;

    private void Awake()
    {
        if (sequence == null)
            sequence = GetComponentInParent<CitizenIdPhotoSequence>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerColliderCount++;

        if (playerColliderCount == 1)
            sequence?.NotifyPlayerEntered();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerColliderCount = Mathf.Max(0, playerColliderCount - 1);

        if (playerColliderCount == 0)
            sequence?.NotifyPlayerExited();
    }
}