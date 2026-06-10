using UnityEngine;

public class PlatformMiniGameArea : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        PlatformMiniGameManager.Instance?.OnPlayerEnteredArea();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        PlatformMiniGameManager.Instance?.OnPlayerExitedArea();
    }
}