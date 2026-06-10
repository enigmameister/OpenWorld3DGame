using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ContaminationZone : MonoBehaviour
{
    [Header("Ustawienia skażenia")]
    [Tooltip("Czas między kolejnymi tikami obrażeń (sekundy).")]
    public float tickInterval = 10f;

    [Tooltip("Kolejne wartości obrażeń. Ostatnia powtarza się w nieskończoność.")]
    public int[] damageSequence = new int[] { 20, 15, 5 };

    [Tooltip("Czy zatrucie ma dalej działać po wyjściu ze strefy.")]
    public bool continueAfterExit = false;

    private PlayerStats _player;
    private Coroutine _routine;
    private int _currentIndex = 0;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void SetBiohazardIcon(bool active)
    {
        if (DamageIndicatorUI.Instance != null)
            DamageIndicatorUI.Instance.SetContaminationIcon(active);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _player = other.GetComponentInParent<PlayerStats>();
        if (_player == null) return;

        // 👉 ZA KAŻDYM RAZEM restartujemy cykl zatrucia
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        _currentIndex = 0;
        _routine = StartCoroutine(DamageRoutine());

        // 🔥 pokaż ikonę skażenia
        SetBiohazardIcon(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (!continueAfterExit && _routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;

            // 🔥 koniec skażenia → schowaj ikonę
            SetBiohazardIcon(false);
        }
    }

    private IEnumerator DamageRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(tickInterval);

            if (_player == null || _player.IsDead)
            {
                _routine = null;
                SetBiohazardIcon(false);
                yield break;
            }

            if (damageSequence == null || damageSequence.Length == 0)
            {
                _routine = null;
                SetBiohazardIcon(false);
                yield break;
            }

            int dmg = damageSequence[Mathf.Min(_currentIndex, damageSequence.Length - 1)];

            // 👉 WSZYSTKO (HP + UI) robi PlayerStats
            _player.ApplyEnvironmentalDamage(dmg, "Contamination");

            if (_currentIndex < damageSequence.Length - 1)
                _currentIndex++;   // 20 → 15 → 5 → 5 → 5...
        }
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        SetBiohazardIcon(false);
    }

}
