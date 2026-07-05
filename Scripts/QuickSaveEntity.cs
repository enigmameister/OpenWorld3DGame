using System;
using UnityEngine;

[DisallowMultipleComponent]
public class QuickSaveEntity : MonoBehaviour
{
    [SerializeField] private string saveId;

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

    private void Awake()
    {
        if (Application.isPlaying && IsNPCEntity())
        {
            runtimeSaveId = Guid.NewGuid().ToString("N");
            return;
        }

        if (Application.isPlaying && gameObject.name.Contains("(Clone)"))
        {
            runtimeSaveId = Guid.NewGuid().ToString("N");
            return;
        }

        EnsureSerializedId();
    }

    private void Reset()
    {
        EnsureSerializedId();
    }

    private void OnValidate()
    {
        EnsureSerializedId();
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