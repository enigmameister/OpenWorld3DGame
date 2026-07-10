using UnityEngine;

public class DoorTrigger : MonoBehaviour, IPressable
{
    public Animator doorAnimator;
    public Light statusLight;
    public Color openedColor = Color.green;
    public Color flashingColorA = Color.red;
    public Color flashingColorB = new Color(0.5f, 0f, 0f);
    public float flashSpeed = 20f;

    [SerializeField] private string label = "Open door";

    private bool doorOpened = false;

    public string Label => doorOpened ? "Doors open" : label;

    void Update()
    {
        if (!doorOpened && statusLight != null)
        {
            float t = Mathf.PingPong(Time.time * flashSpeed, 1f);
            statusLight.color = Color.Lerp(flashingColorA, flashingColorB, t);
        }
    }

    public void Press()
    {
        if (doorOpened)
            return;

        if (doorAnimator != null)
            doorAnimator.SetBool("Key", true);

        doorOpened = true;

        if (statusLight != null)
            statusLight.color = openedColor;
    }
}