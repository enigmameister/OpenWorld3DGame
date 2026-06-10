using UnityEngine;

public class CarInfo : MonoBehaviour
{
    public string carDisplayName = "Audi A4";  // ← To musi istnieć

    // (opcjonalnie) Alias, jeśli używasz też `carName`
    public string carName => carDisplayName;
}
