using UnityEngine;

public class DialogueWheelPointer : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform centerRef;     // nierotuj¹cy punkt odniesienia (Center)
    public RectTransform visualToRotate; // rotuj¹ca grafika (Visual)
    public Canvas canvas;

    [Header("Tuning")]
    public float smooth = 0f;           // 0 = idealnie 1:1 bez wyg³adzania
    public float angleOffset = 0f;      // korekta grafiki
    public bool useUnscaledTime = true;

    Camera _uiCam;

    void Awake()
    {
        if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            _uiCam = canvas.worldCamera;
    }

    void Update()
    {
        if (!centerRef || !visualToRotate || !canvas) return;

        // kierunek w SCREEN SPACE: kursor - œrodek
        Vector2 centerScreen = RectTransformUtility.WorldToScreenPoint(_uiCam, centerRef.position);
        Vector2 mouse = Input.mousePosition;
        Vector2 dir = mouse - centerScreen;

        if (dir.sqrMagnitude < 0.001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // jeœli Twoja grafika “0°” ma byæ do góry:
        angle -= 90f;

        angle += angleOffset;

        Quaternion target = Quaternion.Euler(0f, 0f, angle);

        if (smooth <= 0f)
        {
            visualToRotate.rotation = target;
        }
        else
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            visualToRotate.rotation = Quaternion.Slerp(visualToRotate.rotation, target, dt * smooth);
        }
    }
}
