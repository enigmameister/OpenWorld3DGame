using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerFallDamage : MonoBehaviour
{
    [Header("Parametry obrażeń od upadku")]
    public float minFallHeight = 3f;     // wysokość, od której zaczynają się obrażenia
    public float fatalFallHeight = 10f;  // wysokość, przy której obrażenia = max
    public float maxFallDamage = 100f;   // max obrażenia

    private float fallStartY;
    private bool isFalling = false;
    private bool wasGrounded = true;

    private CharacterController controller;
    private PlayerStats stats;
    private FallImpactCamera impactCamera;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();

        // zwykle FallImpactCamera jest na CameraHolder → szukamy w dzieciach
        impactCamera = GetComponentInChildren<FallImpactCamera>(true);
        if (impactCamera == null)
        {
            // awaryjnie: jeśli ktoś przypiął na MainCamera
            var cam = Camera.main;
            if (cam != null) impactCamera = cam.GetComponent<FallImpactCamera>();
        }
    }

    void Update()
    {
        bool grounded = controller.isGrounded;

        // start spadania: było na ziemi, teraz nie jest
        if (!grounded && wasGrounded)
        {
            fallStartY = transform.position.y;
            isFalling = true;
        }


        // lądowanie po spadaniu
        if (grounded && isFalling)
        {
            if (GetComponent<PlayerStats>()?.isUnderwater == true) return;

            float fallDistance = Mathf.Max(0f, fallStartY - transform.position.y);

            if (fallDistance > minFallHeight)
            {
                float t = Mathf.InverseLerp(minFallHeight, fatalFallHeight, fallDistance);
                float damage = Mathf.Lerp(0f, maxFallDamage, t);

                // zadaj obrażenia
                stats?.TakeDamage(Mathf.RoundToInt(damage));

                // 🔻 wskaźnik obrażeń dla upadku – wszystkie kierunki
                DamageIndicatorUI.Instance?.TriggerFlash(Mathf.RoundToInt(damage), Color.gray);
                // efekt przechyłu kamery
                if (impactCamera != null)
                    impactCamera?.DoTilt();
                DamageIndicatorUI.Instance?.TriggerAll(damage);

            }

            isFalling = false;
        }

        wasGrounded = grounded;
    }
}
