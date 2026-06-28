using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class Target : MonoBehaviour, IDamageable
{
    public enum Direction { Up, Down, Left, Right }

    [Header("Ruch")]
    public Direction[] possibleDirections;
    public float moveSpeed = 1f;
    public float moveRange = 1f;

    [Header("Stan")]
    public bool IsHit { get; private set; }

    private Vector3 initialPosition;
    private Vector3 moveDir;
    private float oscillationTime;

    public System.Action<Target> OnTargetHit;

    private Renderer targetRenderer;
    private Color originalColor;

    [Header("Efekt trafienia")]
    public Color hitColor = Color.red;
    public float hitFlashDuration = 0.2f;

    void Awake()
    {
        initialPosition = transform.position;
        enabled = false;

        targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
            originalColor = targetRenderer.material.color;
    }

    void Update()
    {
        if (IsHit) return;

        oscillationTime += Time.deltaTime;
        float offset = Mathf.Sin(oscillationTime * moveSpeed);
        Vector3 offsetVec = moveRange * offset * moveDir;
        Vector3 targetPos = initialPosition + offsetVec;

        if (moveDir == Vector3.up || moveDir == Vector3.down)
            targetPos.y = Mathf.Clamp(targetPos.y, initialPosition.y, initialPosition.y + moveRange);
        else
            targetPos.z = Mathf.Clamp(targetPos.z, initialPosition.z - moveRange, initialPosition.z + moveRange);

        transform.position = targetPos;
    }

    public void Hit()
    {
        if (IsHit) return;

        IsHit = true;
        Debug.Log($"💥 Tarcza trafiona: {name}");
        enabled = false;

        if (targetRenderer != null)
            StartCoroutine(FlashAndDisable());

        OnTargetHit?.Invoke(this);
    }

    private IEnumerator FlashAndDisable()
    {
        targetRenderer.material.color = hitColor;
        yield return new WaitForSeconds(hitFlashDuration);
        targetRenderer.material.color = originalColor;
        gameObject.SetActive(false);
    }

    public void StartMovement()
    {
        IsHit = false;
        gameObject.SetActive(true);
        oscillationTime = 0f;
        transform.position = initialPosition;
        ChooseRandomDirection();
        enabled = true;
    }

    public void ResetTarget()
    {
        IsHit = false;
        transform.position = initialPosition;
        enabled = false;
        gameObject.SetActive(false);

        if (targetRenderer != null)
            targetRenderer.material.color = originalColor;
    }

    private void ChooseRandomDirection()
    {
        if (possibleDirections.Length == 0)
        {
            moveDir = Vector3.zero;
            return;
        }

        Direction dir = possibleDirections[Random.Range(0, possibleDirections.Length)];
        moveDir = dir switch
        {
            Direction.Up => Vector3.up,
            Direction.Down => Vector3.down,
            Direction.Left => Vector3.back,
            Direction.Right => Vector3.forward,
            _ => Vector3.zero
        };
    }

    // ➕ Implementacja interfejsu IDamageable
    public void TakeDamage(int damage, string attackerName)
    {
        Debug.Log($"🎯 Target trafiony przez {attackerName} za {damage} dmg");
        Hit();
    }
}
