using UnityEngine;

public class NPCProfileApplier : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private NPCProfile profile;

    [Header("Apply")]
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool debugLogs = false;

    public NPCProfile Profile => profile;

    private void Awake()
    {
        if (applyOnAwake)
            ApplyProfile();
    }

    public void ApplyProfile()
    {
        if (profile == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[NPCProfileApplier] {name}: Profile is null.");

            return;
        }

        NPCCore core = GetComponent<NPCCore>();
        NPCController controller = GetComponent<NPCController>();
        NPCMelee melee = GetComponent<NPCMelee>();
        NPCReactive reactive = GetComponent<NPCReactive>();

        // Core zawsze dzia³a: HP / odpornoœæ / StoryCritical / Mission.
        if (core != null)
            core.ApplyProfile(profile);

        // Controller tylko dla profili, które go faktycznie u¿ywaj¹.
        if (controller != null)
        {
            controller.enabled = profile.useNPCController;

            if (profile.useNPCController)
                controller.ApplyProfile(profile);
        }

        // Melee tylko dla melee profilu.
        if (melee != null)
        {
            melee.enabled = profile.useMelee;

            if (profile.useMelee)
                melee.ApplyProfile(profile);
        }

        // Reactive zostawiamy wed³ug profilu.
        if (reactive != null)
        {
            reactive.enabled = profile.allowReactiveInteraction;
        }

        if (debugLogs)
            Debug.Log($"[NPCProfileApplier] Applied profile '{profile.name}' to {name}");
    }

    public void SetProfile(NPCProfile newProfile, bool applyImmediately = true)
    {
        profile = newProfile;

        if (applyImmediately)
            ApplyProfile();
    }
}