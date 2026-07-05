using System;
using UnityEngine;

[DisallowMultipleComponent]
public class QuickSaveEntity : MonoBehaviour
{
    public enum SaveEntityKind
    {
        StableSceneObject,
        RuntimeNPC,
        RuntimeClone
    }

    [Header("Identity")]
    [SerializeField] private string saveId;

    [Header("Rules")]
    [SerializeField] private SaveEntityKind entityKind = SaveEntityKind.StableSceneObject;

    private string runtimeSaveId;

    public string SaveId
    {
        get
        {
            if (Application.isPlaying && !string.IsNullOrWhiteSpace(runtimeSaveId))
                return runtimeSaveId;

            EnsureSerializedId();
            return saveId;
        }
    }

    public SaveEntityKind EntityKind => entityKind;

    private void Awake()
    {
        if (Application.isPlaying && ShouldUseRuntimeId())
        {
            runtimeSaveId = Guid.NewGuid().ToString("N");
            return;
        }

        EnsureSerializedId();
    }

    private void Reset()
    {
        AutoDetectKind();
        EnsureSerializedId();
    }

    private void OnValidate()
    {
        AutoDetectKind();
        EnsureSerializedId();
    }

    private bool ShouldUseRuntimeId()
    {
        if (entityKind == SaveEntityKind.RuntimeNPC)
            return true;

        if (entityKind == SaveEntityKind.RuntimeClone)
            return true;

        if (gameObject.name.Contains("(Clone)"))
            return true;

        return false;
    }

    private void AutoDetectKind()
    {
        if (Application.isPlaying)
            return;

        if (IsNPCEntity())
        {
            entityKind = SaveEntityKind.RuntimeNPC;
            return;
        }

        if (gameObject.name.Contains("(Clone)"))
        {
            entityKind = SaveEntityKind.RuntimeClone;
            return;
        }

        entityKind = SaveEntityKind.StableSceneObject;
    }

    private void EnsureSerializedId()
    {
        if (!string.IsNullOrWhiteSpace(saveId))
            return;

        saveId = Guid.NewGuid().ToString("N");
    }

    private bool IsNPCEntity()
    {
        return GetComponent<NPCCore>() != null ||
               GetComponent<NPCController>() != null ||
               GetComponent<NPCMelee>() != null;
    }

    public void OverrideSaveIdForRestore(string restoredId)
    {
        if (string.IsNullOrWhiteSpace(restoredId))
            return;

        saveId = restoredId;
        runtimeSaveId = restoredId;
    }

#if UNITY_EDITOR
    [ContextMenu("Regenerate Save Id")]
    private void RegenerateSaveId()
    {
        saveId = Guid.NewGuid().ToString("N");
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}