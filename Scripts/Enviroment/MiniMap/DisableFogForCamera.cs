using UnityEngine;

[RequireComponent(typeof(Camera))]
public class DisableFogForCamera : MonoBehaviour
{
    private bool oldFog;

    void OnPreRender()
    {
        oldFog = RenderSettings.fog;
        RenderSettings.fog = false;
    }

    void OnPostRender()
    {
        RenderSettings.fog = oldFog;
    }
}