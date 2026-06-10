using UnityEngine;

public class ShootingMinigameTrigger : MonoBehaviour
{
    public ShootingMinigameController minigameController;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (minigameController == null) return;

        if (!minigameController.CanTriggerUI()) return;

        minigameController.ShowMinigameUI();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (minigameController == null) return;

        // ✅ NIE chowaj UI jeśli gra już trwa
        if (minigameController.isGameRunning) return;

        minigameController.HideMinigameUI();
    }
}
