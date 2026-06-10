using UnityEngine;

public class RaceRingEffect : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z,
        Custom
    }

    [Header("Rotation")]
    public bool rotate = true;
    public RotationAxis rotationAxis = RotationAxis.Z;
    public Vector3 customRotationAxis = Vector3.forward;
    public float rotationSpeed = 40f;
    public Space rotationSpace = Space.Self;

    [Header("Pulse")]
    public bool pulse = true;
    public float pulseSpeed = 2f;
    public float pulseAmount = 0.1f;

    [Header("Base Transform")]
    public bool useStartTransformAsBase = true;
    public Vector3 baseEulerRotation = new Vector3(-90f, 0f, 0f);

    private Vector3 baseScale;
    private Quaternion baseRotation;

    void Start()
    {
        baseScale = transform.localScale;

        if (useStartTransformAsBase)
            baseRotation = transform.localRotation;
        else
            baseRotation = Quaternion.Euler(baseEulerRotation);
    }

    void Update()
    {
        if (pulse)
        {
            float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = baseScale * scale;
        }

        if (rotate)
        {
            Vector3 axis = GetAxis();
            Quaternion spin = Quaternion.AngleAxis(Time.time * rotationSpeed, axis);

            if (rotationSpace == Space.Self)
                transform.localRotation = baseRotation * spin;
            else
                transform.rotation = spin * baseRotation;
        }
        else
        {
            transform.localRotation = baseRotation;
        }
    }

    Vector3 GetAxis()
    {
        return rotationAxis switch
        {
            RotationAxis.X => Vector3.right,
            RotationAxis.Y => Vector3.up,
            RotationAxis.Z => Vector3.forward,
            RotationAxis.Custom => customRotationAxis.normalized,
            _ => Vector3.forward
        };
    }
}