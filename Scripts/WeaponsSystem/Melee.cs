using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Melee : MonoBehaviour, IInventoryItemInstanceProvider
{
    public InventoryItemInstance inventoryInstance;

    // ====== Statystyki ataku ======
    [Header("Statystyki ataku")]
    public float fastDamage = 35f;
    public float strongDamage = 60f;
    public float fastCooldown = 0.35f;
    public float strongCooldown = 0.8f;
    public float fastRange = 1.8f;
    public float strongRange = 2.2f;

    // ====== Proceduralny ruch (bez animacji) ======
    [Header("Procedural motion (bez animacji)")]
    [Tooltip("Transform, który będzie przesuwany/obracany (root noża w dłoni).")]
    public Transform weaponRoot;

    [Tooltip("Lokalna oś 'naprzód' dla weaponRoot (zwykle Z+).")]
    public Vector3 localForwardAxis = new Vector3(0, 0, 1);

    [Tooltip("Odległość thrustu przy LPM (metry).")]
    public float thrustFast = 0.55f;

    [Tooltip("Odległość thrustu przy PPM (metry).")]
    public float thrustStrong = 0.85f;

    [Tooltip("Czas fazy wysunięcia (LPM).")]
    public float fastForwardTime = 0.12f;

    [Tooltip("Czas powrotu (LPM).")]
    public float fastReturnTime = 0.08f;

    [Tooltip("Czas fazy wysunięcia (PPM).")]
    public float strongForwardTime = 0.18f;

    [Tooltip("Czas powrotu (PPM).")]
    public float strongReturnTime = 0.12f;

    [Tooltip("Skręt w lewo (ujemny yaw) przy PPM.")]
    public float strongTwistYawDeg = -35f;

    [Tooltip("Krzywa ease-in/out dla ruchu broni.")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ====== Hit detection ======
    [Header("Hit detection")]
    [Tooltip("Czubek ostrza / punkt kontaktu (używany do sweepa).")]
    public Transform hitPoint;

    [Tooltip("Promień 'pędzla' do wykrywania trafień.")]
    public float hitRadius = 0.35f;

    [Tooltip("Warstwy możliwe do trafienia (NPC/Enemy + świat).")]
    public LayerMask hitMask = ~0;

    [Tooltip("Czy bić również w triggery (gdy NPC mają trigger-collidery).")]
    public bool hitTriggersToo = false;

    // ====== Debug / Gizmos ======
    [Header("Debug & Gizmos")]
    [Tooltip("Rysowanie gizmos/trasy w Play/Scene i logi trafień.")]
    public bool debugGizmos = false;

    [Tooltip("W edytorze: LPM/PPM od razu wywołuje hit (bez czekania na ruch) – pomocne do testów mask/forward.")]
    public bool debugTestHitOnClick = false;

    [Tooltip("Ile próbek 'kulek' narysować wzdłuż zasięgu w gizmach.")]
    public int previewSteps = 6;

    public Color fastColor = new Color(0f, 1f, 0f, 0.8f);
    public Color strongColor = new Color(1f, 0.5f, 0f, 0.8f);
    public Color hitColor = new Color(1f, 0f, 0f, 0.9f);

    [Header("Backstab (PPM z pleców)")]
    public bool enableBackstab = true;
    [Tooltip("Dot(npc.forward, kierunek do gracza) <= próg → liczymy, że jesteś z tyłu. -1 = idealnie za plecami.")]
    [Range(-1f, 1f)] public float backstabDotThreshold = -0.6f; // ~ >120° za plecami
    [Tooltip("Ile DMG zadaje backstab (9999 = praktycznie instant-kill).")]
    public int backstabDamage = 9999;
    [Tooltip("Transform gracza/atakującego (jeśli null, użyje transform.root).")]
    public Transform attackerRoot;

    // ====== Runtime ======
    private float _nextReadyTime = 0f;
    private readonly HashSet<Collider> _alreadyHit = new();
    private Coroutine _attackRoutine;
    private Vector3 _initialLocalPos;
    private Quaternion _initialLocalRot;
    private bool _isStrongSwing;

    // ====== Inventory glue ======
    public void SetInventoryInstance(InventoryItemInstance instance) => inventoryInstance = instance;
    public InventoryItemInstance GetInstance() => inventoryInstance;

    // ====== Konfiguracja z itemu (opcjonalnie animacje – ignorujemy) ======
    public void ApplyMeleeData(MeleeItemData data)
    {
        if (data == null) return;
        fastDamage = data.fastDamage;
        strongDamage = data.strongDamage;
        fastCooldown = data.fastCooldown;
        strongCooldown = data.strongCooldown;
        fastRange = strongRange = data.range;
        // data.fastAttackAnim / data.strongAttackAnim – pomijamy w wersji proceduralnej
    }

    // ====== Unity ======
    void OnEnable()
    {
        if (weaponRoot != null)
        {
            _initialLocalPos = weaponRoot.localPosition;
            _initialLocalRot = weaponRoot.localRotation;
        }
        if (attackerRoot == null) attackerRoot = transform.root; // domyślnie gracz
        _alreadyHit.Clear();
        _attackRoutine = null;
    }

    // ====== API: wywoływane przez WeaponManager ======
    public void TryFastAttack()
    {
        if (Time.time < _nextReadyTime) return;
        if (weaponRoot == null || hitPoint == null) return;

        _isStrongSwing = false; // ⬅️ szybki
        _nextReadyTime = Time.time + fastCooldown;
        _alreadyHit.Clear();

        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        _attackRoutine = StartCoroutine(CoAttack(
            damage: fastDamage, range: fastRange,
            thrustDistance: thrustFast, forwardTime: fastForwardTime,
            returnTime: fastReturnTime, twistYawDeg: 0f
        ));
    }
    public void TryStrongAttack()
    {
        if (Time.time < _nextReadyTime) return;
        if (weaponRoot == null || hitPoint == null) return;

        _isStrongSwing = true; // ⬅️ silny (PPM)
        _nextReadyTime = Time.time + strongCooldown;
        _alreadyHit.Clear();

        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        _attackRoutine = StartCoroutine(CoAttack(
            damage: strongDamage, range: strongRange,
            thrustDistance: thrustStrong, forwardTime: strongForwardTime,
            returnTime: strongReturnTime, twistYawDeg: strongTwistYawDeg
        ));
    }


    // ====== Ruch i detekcja trafień ======
    private IEnumerator CoAttack(float damage, float range, float thrustDistance, float forwardTime, float returnTime, float twistYawDeg)
    {
        // reset startu
        weaponRoot.localPosition = _initialLocalPos;
        weaponRoot.localRotation = _initialLocalRot;

        // faza „naprzód” – pełne obrażenia
        yield return MoveAndHit(damage, range, thrustDistance, forwardTime, twistYawDeg, goingForward: true);

        // faza „powrót” – domyślnie bez obrażeń (możesz chcieć lekki „powrótowy” dmg)
        yield return MoveAndHit(0f, range * 0.6f, -thrustDistance, returnTime, -twistYawDeg * 0.35f, goingForward: false);

        // powrót do pozy wyjściowej
        weaponRoot.localPosition = _initialLocalPos;
        weaponRoot.localRotation = _initialLocalRot;

        _attackRoutine = null;
    }

    private IEnumerator MoveAndHit(float damage, float range, float thrustDistance, float duration, float twistYawDeg, bool goingForward)
    {
        if (duration <= 0f) duration = 0.001f;

        Vector3 localDir = localForwardAxis.normalized;

        // do sweepa: poprzednia pozycja czubka
        Vector3 prevHP = hitPoint.position;

        float t = 0f;
        while (t < duration)
        {
            float u = t / duration;
            float k = ease != null ? Mathf.Clamp01(ease.Evaluate(u)) : u;

            // translacja lokalna (proceduralny thrust)
            Vector3 offset = localDir * (thrustDistance * k);
            weaponRoot.localPosition = _initialLocalPos + offset;

            // twist yaw (lokalnie wokół Y broni)
            Quaternion twist = Quaternion.Euler(0f, twistYawDeg * k, 0f);
            weaponRoot.localRotation = _initialLocalRot * twist;

            // DETEKCJA TRAFIENIA podczas ruchu
            Vector3 curHP = hitPoint.position;

            // sweep: kilka próbek między prevHP a curHP
            const int sweepSamples = 3;
            for (int i = 0; i < sweepSamples; i++)
            {
                Vector3 p = Vector3.Lerp(prevHP, curHP, (i + 1) / (float)sweepSamples);
                SweepAtPoint(p, damage, range);
            }

            prevHP = curHP;

            t += Time.deltaTime;
            yield return null;
        }
    }

    private void SweepAtPoint(Vector3 p, float damage, float range)
    {
        var qti = hitTriggersToo ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        // Overlap w pobliżu czubka
        var cols = Physics.OverlapSphere(p, hitRadius, hitMask, qti);
        if (cols != null && damage > 0f)
        {
            foreach (var c in cols)
                TryApplyDamage(c, damage);
        }

        // Dodatkowy spherecast 'do przodu' czubka – łapie minimalne odstępy
        if (damage > 0f)
        {
            Vector3 dir = hitPoint.forward;
            if (Physics.SphereCast(p, hitRadius * 0.8f, dir, out RaycastHit hit, Mathf.Min(range * 0.25f, 0.75f), hitMask, qti))
                TryApplyDamage(hit.collider, damage);
        }
    }

    private void TryApplyDamage(Collider col, float damage)
    {
        if (col == null || _alreadyHit.Contains(col)) return;

        // nie bij własnego gracza
        if (col.GetComponentInParent<PlayerStats>() != null) return;

        int finalDamage = Mathf.RoundToInt(damage);

        var npc = col.GetComponentInParent<NPCController>();
        bool isBackstabKill = false;

        if (enableBackstab && _isStrongSwing && npc != null && attackerRoot != null &&
            IsBackstab(npc.transform, attackerRoot.position))
        {
            isBackstabKill = true;
            finalDamage = backstabDamage;
        }

        if (npc != null)
        {
            if (isBackstabKill)
            {
                npc.TakeBackstabKill("Player (Melee Backstab)");
            }
            else
            {
                Vector3 attackerPos = attackerRoot != null ? attackerRoot.position : transform.position;
                npc.TakeMeleeDamage(finalDamage, "Player (Melee)", attackerPos);
            }

            _alreadyHit.Add(col);
            return;
        }

        var dmg = col.GetComponentInParent<IDamageable>();
        if (dmg != null)
        {
            dmg.TakeDamage(finalDamage, "Player (Melee)");
            _alreadyHit.Add(col);
        }
    }

    private bool IsBackstab(Transform targetRoot, Vector3 attackerPos)
    {
        // wektor od NPC do gracza
        Vector3 toAttacker = (attackerPos - targetRoot.position);
        toAttacker.y = 0f; // oceniaj w płaszczyźnie poziomej
        if (toAttacker.sqrMagnitude < 0.0001f) return false;

        toAttacker.Normalize();
        Vector3 npcForward = targetRoot.forward; npcForward.y = 0f; npcForward.Normalize();

        // dot = -1 (idealnie za plecami) … 0 (bok) … +1 (z przodu)
        float dot = Vector3.Dot(npcForward, toAttacker);

        // backstab jeśli jesteśmy "za plecami" wg progu
        return dot <= backstabDotThreshold;
    }
}
