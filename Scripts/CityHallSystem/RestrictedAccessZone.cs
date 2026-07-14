using System.Collections.Generic;
using UnityEngine;

public class RestrictedAccessZone : MonoBehaviour
{
    public enum PermissionCheckMode
    {
        AnyRequiredPermission,
        AllRequiredPermissions
    }

    [Header("Permission")]
    [SerializeField]
    private AccessPermission requiredPermissions =
        AccessPermission.Staff |
        AccessPermission.Mission |
        AccessPermission.LawEnforcement;

    [SerializeField]
    private PermissionCheckMode checkMode =
        PermissionCheckMode.AnyRequiredPermission;

    [Header("Physical Blocking")]
    [Tooltip("Zwyk³y collider blokuj¹cy przejœcie.")]
    [SerializeField] private Collider physicalBlocker;

    [Header("Message")]
    [TextArea(2, 4)]
    [SerializeField]
    private string deniedMessage =
        "YOU ARE NOT AUTHORIZED TO ENTER THIS AREA.";

    [SerializeField, Min(0.1f)] private float messageDuration = 3f;
    [SerializeField, Min(0.1f)] private float messageCooldown = 1.5f;

    [Header("Optional Player Push Back")]
    [SerializeField] private Transform deniedReturnPoint;
    [SerializeField] private bool pushPlayerBack = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private float nextMessageTime;

    // Aktor -> collidery, dla których wy³¹czyliœmy kolizjê.
    private readonly Dictionary<AccessPermissionHolder, Collider[]>
        ignoredActorColliders = new();

    private void Awake()
    {
        if (physicalBlocker != null)
            physicalBlocker.isTrigger = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleActor(other, entered: true);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleActor(other, entered: false);
    }

    private void OnTriggerExit(Collider other)
    {
        AccessPermissionHolder holder =
            other.GetComponentInParent<AccessPermissionHolder>();

        if (holder == null)
            return;

        RestoreActorCollision(holder);
    }

    private void HandleActor(Collider other, bool entered)
    {
        if (!TryResolveActor(
                other,
                out AccessPermissionHolder holder,
                out Transform actorRoot))
        {
            return;
        }

        bool authorized = IsAuthorized(holder);

        if (authorized)
        {
            IgnoreActorCollision(holder);

            if (entered && debugLogs)
            {
                Debug.Log(
                    $"[ACCESS ZONE] Access granted: {name}, " +
                    $"actor={actorRoot.name}, " +
                    $"permissions={holder.AllPermissions}"
                );
            }

            return;
        }

        // Nieuprawniony aktor musi zderzaæ siê z blockerem.
        RestoreActorCollision(holder);

        bool isPlayer = actorRoot.CompareTag("Player");

        if (isPlayer)
        {
            ShowDeniedMessage();

            if (pushPlayerBack)
                PushPlayerBack(actorRoot);
        }

        if (entered && debugLogs)
        {
            Debug.Log(
                $"[ACCESS ZONE] Access denied: {name}, " +
                $"actor={actorRoot.name}, " +
                $"required={requiredPermissions}, " +
                $"permissions={holder.AllPermissions}"
            );
        }
    }

    private bool IsAuthorized(AccessPermissionHolder holder)
    {
        if (holder == null)
            return false;

        return checkMode switch
        {
            PermissionCheckMode.AllRequiredPermissions =>
                holder.HasAll(requiredPermissions),

            _ =>
                holder.HasAny(requiredPermissions)
        };
    }

    private void IgnoreActorCollision(AccessPermissionHolder holder)
    {
        if (holder == null ||
            physicalBlocker == null ||
            ignoredActorColliders.ContainsKey(holder))
        {
            return;
        }

        Collider[] actorColliders =
            holder.GetComponentsInChildren<Collider>(
                includeInactive: true
            );

        for (int i = 0; i < actorColliders.Length; i++)
        {
            Collider actorCollider = actorColliders[i];

            if (actorCollider == null ||
                actorCollider == physicalBlocker ||
                actorCollider.isTrigger)
            {
                continue;
            }

            Physics.IgnoreCollision(
                actorCollider,
                physicalBlocker,
                true
            );
        }

        ignoredActorColliders.Add(holder, actorColliders);
    }

    private void RestoreActorCollision(AccessPermissionHolder holder)
    {
        if (holder == null ||
            physicalBlocker == null ||
            !ignoredActorColliders.TryGetValue(
                holder,
                out Collider[] actorColliders))
        {
            return;
        }

        for (int i = 0; i < actorColliders.Length; i++)
        {
            Collider actorCollider = actorColliders[i];

            if (actorCollider == null ||
                actorCollider == physicalBlocker ||
                actorCollider.isTrigger)
            {
                continue;
            }

            Physics.IgnoreCollision(
                actorCollider,
                physicalBlocker,
                false
            );
        }

        ignoredActorColliders.Remove(holder);
    }

    private void ShowDeniedMessage()
    {
        if (Time.unscaledTime < nextMessageTime)
            return;

        nextMessageTime =
            Time.unscaledTime + messageCooldown;

        if (CommunicateUI.Instance != null)
        {
            CommunicateUI.Instance.Show(
                deniedMessage,
                messageDuration
            );
        }
        else
        {
            Debug.LogWarning(
                "[ACCESS ZONE] CommunicateUI.Instance is missing."
            );
        }
    }

    private void PushPlayerBack(Transform playerRoot)
    {
        if (playerRoot == null ||
            deniedReturnPoint == null)
        {
            return;
        }

        CharacterController controller =
            playerRoot.GetComponent<CharacterController>();

        bool controllerWasEnabled =
            controller != null && controller.enabled;

        if (controllerWasEnabled)
            controller.enabled = false;

        Vector3 current = playerRoot.position;
        Vector3 target = deniedReturnPoint.position;

        playerRoot.position = new Vector3(
            target.x,
            current.y,
            target.z
        );

        if (controllerWasEnabled)
            controller.enabled = true;
    }

    private bool TryResolveActor(
        Collider other,
        out AccessPermissionHolder holder,
        out Transform actorRoot)
    {
        holder = null;
        actorRoot = null;

        if (other == null)
            return false;

        holder =
            other.GetComponentInParent<AccessPermissionHolder>();

        if (holder == null)
            return false;

        actorRoot = holder.transform;
        return true;
    }

    private void OnDisable()
    {
        if (physicalBlocker == null)
        {
            ignoredActorColliders.Clear();
            return;
        }

        foreach (
            KeyValuePair<AccessPermissionHolder, Collider[]> pair
            in ignoredActorColliders)
        {
            Collider[] colliders = pair.Value;

            if (colliders == null)
                continue;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider actorCollider = colliders[i];

                if (actorCollider == null ||
                    actorCollider == physicalBlocker)
                {
                    continue;
                }

                Physics.IgnoreCollision(
                    actorCollider,
                    physicalBlocker,
                    false
                );
            }
        }

        ignoredActorColliders.Clear();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (physicalBlocker != null)
            physicalBlocker.isTrigger = false;
    }
#endif
}