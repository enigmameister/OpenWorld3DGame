using UnityEngine;

public class ShelfTrigger : MonoBehaviour
{
    public ShelfCodePanel codePanel;

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && Input.GetKeyDown(KeyCode.E))
        {
            if (!codePanel.gameObject.activeSelf && !codePanel.enabled)
                return;

            if (!codePanel.gameObject.activeSelf)
                codePanel.gameObject.SetActive(true);
        }
    }
}
