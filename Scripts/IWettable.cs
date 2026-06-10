using UnityEngine;

public interface IWettable
{
    void EnterWater(float surfaceY);
    void ExitWater();
    void HandleSwimming(Vector3 waterNormal, float surfaceY);
}
