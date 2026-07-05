using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class VehicleParkingController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody vehicleRb;
    [SerializeField] private GameObject vehicleObject;
    [SerializeField] private GameObject playerObject;

    [Header("Park brake")]
    [SerializeField] private bool useParkingMass = true;
    [SerializeField] private float parkedMass = 10000f;
    [SerializeField] private float parkedDrag = 5f;
    [SerializeField] private float parkedAngularDrag = 5f;
    [SerializeField] private bool freezeRotXZWhenParked = true;
    [SerializeField] private bool hardStopOnPark = false;

    [Header("Player collision protection")]
    [SerializeField] private bool ignorePlayerCollisionWhenParked = false;

    private float defaultMass;
    private float defaultDrag;
    private float defaultAngularDrag;
    private RigidbodyConstraints defaultConstraints;

    private Collider[] vehicleColliders;
    private Collider[] playerColliders;

    public bool IsParked { get; private set; }

    private void Awake()
    {
        ResolveRefs();
        CacheDefaults();
        CacheColliders();
    }

    public void SetContext(GameObject newVehicleObject, GameObject newPlayerObject)
    {
        if (newVehicleObject != null)
            vehicleObject = newVehicleObject;

        if (newPlayerObject != null)
            playerObject = newPlayerObject;

        ResolveRefs();
        CacheColliders();
    }

    private void ResolveRefs()
    {
        if (vehicleObject == null)
            vehicleObject = gameObject;

        if (vehicleRb == null)
            vehicleRb = vehicleObject.GetComponent<Rigidbody>();

        if (vehicleRb == null)
            vehicleRb = GetComponent<Rigidbody>();
    }

    private void CacheDefaults()
    {
        if (vehicleRb == null)
            return;

        defaultMass = vehicleRb.mass;
        defaultDrag = vehicleRb.linearDamping;
        defaultAngularDrag = vehicleRb.angularDamping;
        defaultConstraints = vehicleRb.constraints;

        vehicleRb.isKinematic = false;
        vehicleRb.useGravity = true;

        vehicleRb.constraints &= ~(
            RigidbodyConstraints.FreezePositionX |
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezePositionZ
        );
    }

    private void CacheColliders()
    {
        if (vehicleObject != null)
            vehicleColliders = vehicleObject.GetComponentsInChildren<Collider>(true);

        if (playerObject != null)
            playerColliders = playerObject.GetComponentsInChildren<Collider>(true);
    }

    public void SetParked(bool parked)
    {
        if (vehicleRb == null)
            return;

        IsParked = parked;

        if (parked)
            ApplyParkedState();
        else
            ApplyUnparkedState();

        ApplyPlayerCollisionIgnore(parked);
    }

    private void ApplyParkedState()
    {
        if (hardStopOnPark)
        {
            vehicleRb.linearVelocity = Vector3.zero;
            vehicleRb.angularVelocity = Vector3.zero;
        }

        vehicleRb.isKinematic = true;
        vehicleRb.useGravity = false;

        if (useParkingMass)
        {
            vehicleRb.mass = parkedMass;
            vehicleRb.linearDamping = parkedDrag;
            vehicleRb.angularDamping = parkedAngularDrag;
        }

        if (freezeRotXZWhenParked)
            vehicleRb.constraints = RigidbodyConstraints.FreezeAll;
        else
            vehicleRb.constraints = defaultConstraints;

        vehicleRb.Sleep();
    }

    private void ApplyUnparkedState()
    {
        vehicleRb.isKinematic = false;
        vehicleRb.useGravity = true;

        if (useParkingMass)
        {
            vehicleRb.mass = defaultMass;
            vehicleRb.linearDamping = defaultDrag;
            vehicleRb.angularDamping = defaultAngularDrag;
        }

        vehicleRb.constraints = defaultConstraints;
        vehicleRb.WakeUp();
    }

    private void ApplyPlayerCollisionIgnore(bool ignore)
    {
        if (!ignorePlayerCollisionWhenParked)
            ignore = false;

        if (vehicleColliders == null || playerColliders == null)
            return;

        for (int i = 0; i < vehicleColliders.Length; i++)
        {
            Collider vehicleCollider = vehicleColliders[i];

            if (vehicleCollider == null || vehicleCollider.isTrigger)
                continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCollider = playerColliders[j];

                if (playerCollider == null || playerCollider.isTrigger)
                    continue;

                Physics.IgnoreCollision(vehicleCollider, playerCollider, ignore);
            }
        }
    }
}