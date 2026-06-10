using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ButtonPlatformController : MonoBehaviour
{
    [Header("Platforma")]
    public Transform platform;
    public Transform startPoint;
    public Transform endPoint;
    public float moveSpeed = 3f;

    [Header("Wygl¹d przycisku")]
    public Renderer buttonRenderer;
    public Color idleColor = Color.red;
    public Color activeColor = Color.green;   // start -> end
    public Color returnColor = Color.blue;    // end -> start

    private bool _playerInRange = false;
    private bool _isMoving = false;
    private bool _toEnd = true;

    void Start()
    {
        if (buttonRenderer != null)
            buttonRenderer.material.color = idleColor;
    }

    void Update()
    {
        if (!_playerInRange || _isMoving) return;
        if (PlayerInputHandler.Instance == null) return;

        if (PlayerInputHandler.Instance.InteractPressedThisFrame)
        {
            StartCoroutine(MovePlatform());
        }
    }

    IEnumerator MovePlatform()
    {
        _isMoving = true;

        // ustaw kolor w zale¿noœci od kierunku
        if (buttonRenderer != null)
            buttonRenderer.material.color = _toEnd ? activeColor : returnColor;

        Vector3 from = _toEnd ? startPoint.position : endPoint.position;
        Vector3 to = _toEnd ? endPoint.position : startPoint.position;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            platform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        platform.position = to;
        _toEnd = !_toEnd;
        _isMoving = false;

        if (buttonRenderer != null)
            buttonRenderer.material.color = idleColor;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = false;
    }
}
