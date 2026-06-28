using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    public Animator doorAnimator;
    public Light statusLight;
    public Color openedColor = Color.green;
    public Color flashingColorA = Color.red;
    public Color flashingColorB = new Color(0.5f, 0f, 0f); // ciemnoczerwony
    public float flashSpeed = 20f;

    private bool playerInZone = false;
    private bool doorOpened = false;

    void Update()
    {
        if (!doorOpened && statusLight != null)
        {
            float t = Mathf.PingPong(Time.time * flashSpeed, 1f);
            statusLight.color = Color.Lerp(flashingColorA, flashingColorB, t);
        }

        if (playerInZone && !doorOpened && PlayerInputHandler.Instance != null && PlayerInputHandler.Instance.InteractPressedThisFrame)
        {
            doorAnimator.SetBool("Key", true);
            doorOpened = true;

            if (statusLight != null)
                statusLight.color = openedColor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInZone = false;
    }
}
