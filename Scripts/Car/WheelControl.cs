using UnityEngine;

public class WheelControl : MonoBehaviour
{
    public Transform wheelModel;

    [HideInInspector] public WheelCollider WheelCollider;

    public bool steerable;
    public bool motorized;
    public Vector3 rotationOffsetEuler;

    private Quaternion startLocalRotation;

    void Start()
    {
        WheelCollider = GetComponent<WheelCollider>();

        if (wheelModel != null)
            startLocalRotation = wheelModel.localRotation;
    }

    void LateUpdate()
    {
        if (WheelCollider == null || wheelModel == null)
            return;

        WheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);

        wheelModel.position = pos;
        wheelModel.rotation = rot * startLocalRotation * Quaternion.Euler(rotationOffsetEuler);
    }
}