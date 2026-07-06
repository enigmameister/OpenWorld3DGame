using UnityEngine;

[DisallowMultipleComponent]
public class VehicleInputController : MonoBehaviour
{
    [Header("Input Source")]
    [SerializeField] private bool useExternalInput = false;

    [Header("Keyboard / Player")]
    [SerializeField] private KeyCode driftKey = KeyCode.Space;
    [SerializeField] private float inputDeadzone = 0.05f;

    private InputActions input;
    private Vector2 externalMoveInput;
    private bool externalHandbrakeInput;

    public bool UseExternalInput
    {
        get => useExternalInput;
        set => useExternalInput = value;
    }

    public VehicleDriveInput CurrentInput { get; private set; }

    private void Awake()
    {
        input = new InputActions();
    }

    private void OnEnable()
    {
        input?.Enable();
    }

    private void OnDisable()
    {
        input?.Disable();
        CurrentInput = VehicleDriveInput.Zero;
    }

    private void Update()
    {
        CurrentInput = ReadInput();
    }

    public void SetExternalInput(float steer, float throttle)
    {
        externalMoveInput = new Vector2(
            Mathf.Clamp(steer, -1f, 1f),
            Mathf.Clamp(throttle, -1f, 1f)
        );

        if (Mathf.Abs(steer) < 0.01f && Mathf.Abs(throttle) < 0.01f)
            externalHandbrakeInput = false;
    }

    public void SetExternalHandbrake(bool value)
    {
        externalHandbrakeInput = value;
    }

    private VehicleDriveInput ReadInput()
    {
        Vector2 moveInput;

        if (useExternalInput)
        {
            moveInput = externalMoveInput;
        }
        else
        {
            moveInput = input.Car.Movement.ReadValue<Vector2>();
        }

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        if (Mathf.Abs(moveInput.x) < inputDeadzone)
            moveInput.x = 0f;

        if (Mathf.Abs(moveInput.y) < inputDeadzone)
            moveInput.y = 0f;

        bool handbrake = useExternalInput
            ? externalHandbrakeInput
            : Input.GetKey(driftKey);

        return new VehicleDriveInput
        {
            steer = moveInput.x,
            throttle = moveInput.y,
            handbrake = handbrake
        };
    }
}