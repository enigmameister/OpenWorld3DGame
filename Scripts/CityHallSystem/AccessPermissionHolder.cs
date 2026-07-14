using UnityEngine;

public class AccessPermissionHolder : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private AccessPermission permanentPermissions;
    [SerializeField] private AccessPermission temporaryPermissions;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    public AccessPermission PermanentPermissions =>
        permanentPermissions;

    public AccessPermission TemporaryPermissions =>
        temporaryPermissions;

    public AccessPermission AllPermissions =>
        permanentPermissions | temporaryPermissions;

    public bool HasAny(AccessPermission required)
    {
        if (required == AccessPermission.None)
            return true;

        return (AllPermissions & required) != 0;
    }

    public bool HasAll(AccessPermission required)
    {
        if (required == AccessPermission.None)
            return true;

        return (AllPermissions & required) == required;
    }

    public void GrantPermanent(AccessPermission permission)
    {
        permanentPermissions |= permission;

        if (debugLogs)
            Debug.Log($"[ACCESS] Permanent permission granted: {permission}");
    }

    public void RevokePermanent(AccessPermission permission)
    {
        permanentPermissions &= ~permission;

        if (debugLogs)
            Debug.Log($"[ACCESS] Permanent permission revoked: {permission}");
    }

    public void GrantTemporary(AccessPermission permission)
    {
        temporaryPermissions |= permission;

        if (debugLogs)
            Debug.Log($"[ACCESS] Temporary permission granted: {permission}");
    }

    public void RevokeTemporary(AccessPermission permission)
    {
        temporaryPermissions &= ~permission;

        if (debugLogs)
            Debug.Log($"[ACCESS] Temporary permission revoked: {permission}");
    }

    public void ClearTemporaryPermissions()
    {
        temporaryPermissions = AccessPermission.None;

        if (debugLogs)
            Debug.Log("[ACCESS] Temporary permissions cleared.");
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Set Visitor")]
    private void DebugSetVisitor()
    {
        permanentPermissions = AccessPermission.Visitor;
    }

    [ContextMenu("Debug/Set Staff")]
    private void DebugSetStaff()
    {
        permanentPermissions = AccessPermission.Staff;
    }

    [ContextMenu("Debug/Grant Mission Access")]
    private void DebugGrantMission()
    {
        GrantTemporary(AccessPermission.Mission);
    }

    [ContextMenu("Debug/Clear Temporary")]
    private void DebugClearTemporary()
    {
        ClearTemporaryPermissions();
    }
#endif
}