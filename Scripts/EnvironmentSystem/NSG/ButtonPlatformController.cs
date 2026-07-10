using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ButtonPlatformController : MonoBehaviour, IPressable
{
    [Header("Platforma")]
    public Transform platform;
    public Transform startPoint;
    public Transform endPoint;
    public float moveSpeed = 3f;

    [Header("Wygl¹d przycisku")]
    public Renderer buttonRenderer;
    public Color idleColor = Color.red;
    public Color activeColor = Color.green;
    public Color returnColor = Color.blue;

    [SerializeField] private string label = "U¿yj przycisku platformy";

    private bool _isMoving = false;
    private bool _toEnd = true;

    public string Label => _isMoving ? "Platforma w ruchu" : label;

    void Start()
    {
        if (buttonRenderer != null)
            buttonRenderer.material.color = idleColor;
    }

    public void Press()
    {
        if (_isMoving)
            return;

        if (platform == null || startPoint == null || endPoint == null)
            return;

        StartCoroutine(MovePlatform());
    }

    IEnumerator MovePlatform()
    {
        _isMoving = true;

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
}