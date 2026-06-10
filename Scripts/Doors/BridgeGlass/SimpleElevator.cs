using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SimpleElevator : MonoBehaviour
{
    public enum StartPosition
    {
        Auto,
        Bottom,
        Top
    }

    [Header("Ustawienia windy")]
    [Tooltip("Obiekt, który ma siê poruszaæ (np. parent 'Winda').")]
    public Transform elevatorRoot;

    [Tooltip("Pozycja górna windy.")]
    public Transform topPoint;

    [Tooltip("Pozycja dolna windy.")]
    public Transform bottomPoint;

    [Tooltip("Prêdkoœæ ruchu (jednostki na sekundê).")]
    public float moveSpeed = 2f;

    [Tooltip("Czas postoju po dojechaniu (opcjonalnie).")]
    public float waitTimeAtEnd = 0f;

    [Tooltip("Pozycja startowa windy.")]
    public StartPosition startPosition = StartPosition.Auto;

    [Header("Crush – zmia¿d¿enie gracza")]
    [Tooltip("Punkt referencyjny pod pod³og¹ windy (dziecko elevatorRoot).")]
    public Transform crushCheck;
    [Tooltip("Pó³-rozmiary boxa do sprawdzania kolizji (œwiat).")]
    public Vector3 crushHalfExtents = new Vector3(0.5f, 0.1f, 0.5f);
    [Tooltip("Warstwy, które mog¹ zostaæ zmia¿d¿one (np. Player).")]
    public LayerMask crushMask;
    [Tooltip("Obra¿enia zadawane przy zmia¿d¿eniu.")]
    public int crushDamage = 999;

    [Header("Jump-boost na koñcu jazdy")]
    [Tooltip("Dodatkowe podbicie do góry gdy gracz trzyma skok w momencie zatrzymania windy.")]
    public float jumpBoostHeight = 0.5f;

    private bool _moving = false;
    private bool _atTop = false;          // true = stoi na górze, false = na dole
    private CharacterController _currentRider;   // gracz stoj¹cy na platformie (jeœli jest)

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void Start()
    {
        if (elevatorRoot == null)
            elevatorRoot = transform.parent;

        if (elevatorRoot == null || topPoint == null || bottomPoint == null)
        {
            Debug.LogWarning("SimpleElevator: brak referencji!", this);
            enabled = false;
            return;
        }

        // ustawienie pozycji startowej
        switch (startPosition)
        {
            case StartPosition.Top:
                _atTop = true;
                elevatorRoot.position = topPoint.position;
                break;
            case StartPosition.Bottom:
                _atTop = false;
                elevatorRoot.position = bottomPoint.position;
                break;
            case StartPosition.Auto:
            default:
                float distToTop = Vector3.Distance(elevatorRoot.position, topPoint.position);
                float distToBottom = Vector3.Distance(elevatorRoot.position, bottomPoint.position);
                _atTop = distToTop < distToBottom;
                break;
        }
    }

    // === PUBLICZNE API – wywo³ywane z przycisku ===

    public void CallToBottom()
    {
        if (_moving || elevatorRoot == null || bottomPoint == null) return;
        if (!_atTop) return; // ju¿ jest na dole
        StartCoroutine(MoveElevatorTo(false));
    }

    public void CallToTop()
    {
        if (_moving || elevatorRoot == null || topPoint == null) return;
        if (_atTop) return; // ju¿ jest na górze
        StartCoroutine(MoveElevatorTo(true));
    }

    // === WEJŒCIE NA PLATFORMÊ – toggle góra/dó³ ===

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _currentRider = other.GetComponent<CharacterController>() ??
                        other.GetComponentInParent<CharacterController>();

        // Wejœcie na stoj¹c¹ windê = jedŸ w przeciwn¹ stronê (góra <-> dó³)
        if (!_moving)
        {
            bool targetTop = !_atTop;
            StartCoroutine(MoveElevatorTo(targetTop));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var cc = other.GetComponent<CharacterController>() ??
                 other.GetComponentInParent<CharacterController>();

        if (cc == _currentRider)
            _currentRider = null;
    }

    // === G£ÓWNY RUCH WINDY ===

    private IEnumerator MoveElevatorTo(bool toTop)
    {
        _moving = true;

        Vector3 startPos = elevatorRoot.position;
        Vector3 targetPos = toTop ? topPoint.position : bottomPoint.position;

        float distance = Vector3.Distance(startPos, targetPos);
        if (distance < 0.01f)
        {
            elevatorRoot.position = targetPos;
            _atTop = toTop;
            _moving = false;
            yield break;
        }

        float duration = distance / Mathf.Max(0.01f, moveSpeed);
        float t = 0f;

        bool movingDown = startPos.y > targetPos.y + 0.01f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            elevatorRoot.position = Vector3.Lerp(startPos, targetPos, t);

            if (movingDown)
                CheckCrush();

            yield return null;
        }

        elevatorRoot.position = targetPos;
        _atTop = toTop;

        // jump-boost gdy gracz stoi na platformie i trzyma skok
        TryApplyJumpBoost();

        if (waitTimeAtEnd > 0f)
            yield return new WaitForSeconds(waitTimeAtEnd);

        _moving = false;
    }

    // === CRUSH LOGIC ===

    private void CheckCrush()
    {
        if (crushCheck == null) return;
        if (crushMask == 0) return; // nie ustawiono masek – nic nie zgniatamy

        Collider[] hits = Physics.OverlapBox(
            crushCheck.position,
            crushHalfExtents,
            elevatorRoot.rotation,
            crushMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in hits)
        {
            var dmg = hit.GetComponentInParent<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(crushDamage, "Winda");

                // opcjonalnie flash na ekranie, jeœli chcesz:
                if (DamageIndicatorUI.Instance != null)
                    DamageIndicatorUI.Instance.TriggerFlash(crushDamage);

                // zak³adamy, ¿e jeden raz wystarczy
                break;
            }
        }
    }

    // === JUMP-BOOST ===

    private void TryApplyJumpBoost()
    {
        if (_currentRider == null) return;
        if (jumpBoostHeight <= 0f) return;

        var input = PlayerInputHandler.Instance;
        if (input == null) return;

        // gracz musi JEDNOCZEŒNIE u¿yæ skoku
        if (!input.JumpHeld && !input.JumpPressed)
            return;

        // ma³y „podrzut” CharacterControllera
        _currentRider.Move(Vector3.up * jumpBoostHeight);
    }

    // gizmo do podgl¹du crush-boxa
    private void OnDrawGizmosSelected()
    {
        if (crushCheck == null) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.matrix = Matrix4x4.TRS(crushCheck.position, elevatorRoot ? elevatorRoot.rotation : Quaternion.identity, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, crushHalfExtents * 2f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, crushHalfExtents * 2f);
    }
}
