using System.Collections.Generic;
using UnityEngine;

public class VehicleNpcImpactDetector : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody vehicleRb;
    [SerializeField] private CarControll carController;
    [SerializeField] private Transform vehicleRoot;

    [Header("Rules")]
    [SerializeField] private bool requirePlayerDriving = true;
    [SerializeField] private float minImpactSpeedKmh = 8f;
    [SerializeField] private float fatalSpeedKmh = 24f;
    [SerializeField] private float damagePerKmh = 4f;

    [Header("Repeated hit protection")]
    [SerializeField] private float hitCooldownPerNpc = 1.0f;

    [Header("Slow crush")]
    [SerializeField] private bool allowSlowCrushOnStay = true;
    [SerializeField] private float slowCrushMinSpeedKmh = 4f;
    [SerializeField] private float slowCrushDamagePerSecond = 55f;

    [Header("Visual Impact Push")]
    [SerializeField] private bool pushNpcOutBeforeDamage = true;
    [SerializeField] private float pushNpcForwardDistance = 1.15f;
    [SerializeField] private float pushNpcUpDistance = 0.15f;
    [SerializeField] private float highSpeedExtraUp = 0.45f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly Dictionary<Component, float> nextHitTimeByTarget = new();

    private void Awake()
    {
        if (vehicleRoot == null)
            vehicleRoot = transform.root;

        if (vehicleRb == null)
            vehicleRb = GetComponentInParent<Rigidbody>();

        if (carController == null)
            carController = GetComponentInParent<CarControll>();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHitNpc(other, isStay: false);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!allowSlowCrushOnStay)
            return;

        TryHitNpc(other, isStay: true);
    }

    private void TryHitNpc(Collider other, bool isStay)
    {
        if (other == null)
            return;

        if (requirePlayerDriving && !IsPlayerDrivingThisVehicle())
            return;

        NPCController npc = other.GetComponentInParent<NPCController>();
        NPCMelee melee = other.GetComponentInParent<NPCMelee>();
        NPCCore core = other.GetComponentInParent<NPCCore>();

        IDamageable damageable = null;

        if (npc != null)
            damageable = npc as IDamageable;
        else if (melee != null)
            damageable = melee;

        if (npc == null && melee == null && core == null)
            return;

        if (core != null)
        {
            if (core.Importance != NPCCore.NPCImportance.Ambient)
                return;

            if (core.IsInvulnerable || core.PreventDeath)
                return;
        }

        if (npc != null && npc.IsDead)
            return;

        if (melee != null && melee.IsDead)
            return;

        float speedKmh = GetVehicleSpeedKmh();

        if (!isStay && speedKmh < minImpactSpeedKmh)
            return;

        if (isStay && speedKmh < slowCrushMinSpeedKmh)
            return;

        Component cooldownKey = npc != null
            ? npc
            : damageable as Component;

        if (cooldownKey == null)
            return;

        if (nextHitTimeByTarget.TryGetValue(cooldownKey, out float nextTime) && Time.time < nextTime)
            return;

        nextHitTimeByTarget[cooldownKey] = Time.time + hitCooldownPerNpc;

        Vector3 velocity = vehicleRb != null
            ? vehicleRb.linearVelocity
            : transform.forward * (speedKmh / 3.6f);

        Component targetComponent = npc != null ? npc : melee != null ? melee : null;

        if (pushNpcOutBeforeDamage && targetComponent != null)
        {
            PushTargetToImpactSide(targetComponent.transform, velocity, speedKmh);
        }

        Vector3 hitPoint = other.bounds.ClosestPoint(transform.position);

        float damage;

        if (isStay && speedKmh < fatalSpeedKmh)
        {
            damage = slowCrushDamagePerSecond * hitCooldownPerNpc;
        }
        else
        {
            damage = speedKmh >= fatalSpeedKmh
                ? 99999f
                : speedKmh * damagePerKmh;
        }

        if (npc != null)
        {
            npc.ReceiveVehicleImpact(
                damage,
                speedKmh,
                velocity,
                hitPoint,
                attackerName: "PlayerVehicle"
            );
        }
        else if (melee != null)
        {
            melee.ReceiveVehicleImpact(
                damage,
                speedKmh,
                velocity,
                hitPoint,
                "PlayerVehicle"
            );
        }

        else if (damageable != null)
        {
            damageable.TakeDamage(Mathf.CeilToInt(damage), "PlayerVehicle");
        }

        if (debugLogs)
        {
            Debug.Log(
                $"[VehicleNpcImpactDetector] Hit {cooldownKey.name}, speed={speedKmh:0.0}, damage={damage:0.0}, stay={isStay}"
            );
        }
    }

    private void PushTargetToImpactSide(Transform target, Vector3 vehicleVelocity, float speedKmh)
    {
        if (target == null)
            return;

        Vector3 dir = vehicleVelocity;

        if (dir.sqrMagnitude < 0.01f)
            dir = vehicleRoot != null ? vehicleRoot.forward : transform.forward;

        dir.y = 0f;

        if (dir.sqrMagnitude < 0.01f)
            dir = transform.forward;

        dir.Normalize();

        float speed01 = Mathf.InverseLerp(15f, 90f, speedKmh);
        float up = pushNpcUpDistance + highSpeedExtraUp * speed01;

        // NPC zostaje wizualnie przesuniêty przed maskê / w kierunku uderzenia,
        // ¿eby nie gin¹³ wewn¹trz modelu auta.
        target.position += dir * pushNpcForwardDistance + Vector3.up * up;
    }

    private float GetVehicleSpeedKmh()
    {
        if (vehicleRb != null)
            return vehicleRb.linearVelocity.magnitude * 3.6f;

        if (carController != null)
            return Mathf.Max(0f, carController.currentSpeedKPH);

        return 0f;
    }

    private bool IsPlayerDrivingThisVehicle()
    {
        if (carController != null && carController.isControlled)
            return true;

        if (vehicleRoot != null &&
            CarInteraction.ActiveVehicleTransform != null &&
            CarInteraction.ActiveVehicleTransform == vehicleRoot)
        {
            return true;
        }

        return false;
    }
}