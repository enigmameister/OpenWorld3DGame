using UnityEngine;
using System.Collections;

public class VehicleDestructible : MonoBehaviour
{
    [Header("Health")]
    public float maxHP = 1000f;
    public float currentHP = 1000f;

    [Header("Efekty")]
    public GameObject smokeEffect;
    public GameObject explosionEffect;

    [Header("Autozniszczenie")]
    public float delayBeforeExplosion = 5f;
    public float destroyDelayAfterExplosion = 5f;

    [HideInInspector] public bool isPermanentlyDestroyed = false;
    public bool isInSmokePhase = false; // czy pojawił sie dym przed eksplozją

    [Header("Materiał po zniszczeniu")]
    public Material destroyedMaterial;

    private PlayerStats playerScriptRef;
    private bool isDestroyed = false;

    [Header("Warstwy")]
    public LayerMask playerMask;

    [Header("Tryb specjalny")]
    public bool isInvincible = false; // Zmienna do misji/wyścigów

    [SerializeField] bool killDriverOnExplosion = true;
    [SerializeField] float driverExplosionDamage = 9999f;

    private PlayerStats _playerRef;

    void Start()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        if (isDestroyed || isInvincible) return;

        currentHP -= amount;
        Debug.Log($"🚗 HP auta: {currentHP}/{maxHP}");

        if (currentHP <= 0f)
            StartDestructionSequence();
    }

    public void StartDestructionSequence()
    {
        if (isDestroyed || isInvincible) return;

        isDestroyed = true;
        StartCoroutine(SmokePhaseRoutine());
    }

    IEnumerator SmokePhaseRoutine()
    {
        isInSmokePhase = true;

        if (smokeEffect != null)
        {
            smokeEffect.SetActive(true);
            var ps = smokeEffect.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        Debug.Log("💨 Auto uszkodzone — dym...");
        yield return new WaitForSeconds(delayBeforeExplosion);
        isInSmokePhase = false;

        StartCoroutine(ExplosionRoutine());
    }

    IEnumerator ExplosionRoutine()
    {
        isPermanentlyDestroyed = true;

        // Zatrzymaj dym
        if (smokeEffect != null)
        {
            var ps = smokeEffect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop();
                Destroy(smokeEffect, 3f);
            }
            else
            {
                smokeEffect.SetActive(false);
            }
        }

        // Dezaktywacja sterowania
        var controller = GetComponent<CarControll>();
        if (controller != null)
            controller.enabled = false;

        // Efekt eksplozji
        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position + Vector3.up * 1f, Quaternion.identity);

        Debug.Log("💥 Eksplozja auta!");

        // Zabij gracza jeśli w środku
        // --- Zabij kierowcę, jeśli siedzi w aucie (CC wyłączony) ---
        bool killed = false;

        if (killDriverOnExplosion && playerScriptRef != null && !playerScriptRef.IsDead)
        {
            var cc = playerScriptRef.GetComponent<CharacterController>();
            bool driverLikelyInCar = (cc == null || !cc.enabled); // w aucie wyłączasz CC

            if (driverLikelyInCar)
            {
                playerScriptRef.TakeDamage(Mathf.CeilToInt(driverExplosionDamage));
                killed = true;
                Debug.Log("☠️ Kierowca zginął w eksplozji (direct).");
            }
        }

        // --- jeśli jeszcze żyje: obrażenia obszarowe dla gracza w pobliżu (poza autem) ---
        if (!killed && playerScriptRef != null && !playerScriptRef.IsDead)
        {
            Collider[] playerHits = Physics.OverlapSphere(transform.position, 6f, playerMask);
            foreach (var hit in playerHits)
            {
                var stats = hit.GetComponent<PlayerStats>();
                if (stats != null && !stats.IsDead)
                {
                    stats.TakeDamage(stats.maxHP);
                    killed = true;
                    Debug.Log("🔥 Gracz zginął od eksplozji (OverlapSphere + maska).");
                    break;
                }
            }

            if (!killed) Debug.Log("✅ Gracz przeżył eksplozję auta (OverlapSphere).");
        }

        // Obrażenia obszarowe dla przeciwników
        float explosionRadius = 5f;
        float explosionDamage = 100f;

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                var damageable = hit.GetComponent<IDamageable>();
                damageable?.TakeDamage(explosionDamage);
            }
        }

        // Fizyczny efekt rozpadu
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
            rb.AddExplosionForce(500f, transform.position, explosionRadius);
        }

        // Zmiana materiału pojazdu
        var renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var rend in renderers)
        {
            Material[] newMats = new Material[rend.materials.Length];
            for (int i = 0; i < newMats.Length; i++)
                newMats[i] = destroyedMaterial;

            rend.materials = newMats;
        }

        // Zniszczenie obiektu po czasie
        Destroy(gameObject, destroyDelayAfterExplosion);

        yield return null;
    }

    public void AssignPlayerRef(PlayerStats player)
    {
        if (player != null)
        {
            playerScriptRef = player;
            Debug.Log($"🧍 Gracz przypisany do auta ({player.name})");
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isInvincible)
            return;

        if (collision.collider.CompareTag("Bullet"))
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed > 10f)
        {
            float damage = impactSpeed * 10f;
            Debug.Log($"💥 Kolizja z {collision.collider.name} (Siła: {impactSpeed:F2}) → Obrażenia: {damage:F0}");
            TakeDamage(damage);
        }
    }

    public interface IDamageable
    {
        void TakeDamage(float amount);
    }
}
