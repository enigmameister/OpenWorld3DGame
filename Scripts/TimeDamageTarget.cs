using UnityEngine;
using TMPro;

public class TimedDamageTarget : MonoBehaviour, IDamageable
{
    public float requiredDamage = 500f;
    public float decayDelay = 1.5f;
    public float decayRate = 100f;

    public Light feedbackLight;
    public Color completeColor = Color.green;

    public TextMeshPro requiredText;
    public TextMeshPro currentText;

    private float currentDamage = 0f;
    private float lastHitTime = 0f;
    private bool isComplete = false;

    void Start()
    {
        UpdateText();
        if (feedbackLight != null)
            feedbackLight.color = Color.red;
    }

    void Update()
    {
        if (isComplete) return;

        if (Time.time - lastHitTime > decayDelay && currentDamage > 0f)
        {
            currentDamage -= decayRate * Time.deltaTime;
            currentDamage = Mathf.Max(0f, currentDamage);
            UpdateText();
        }
    }

    public void TakeDamage(float damage)
    {
        if (isComplete) return;

        currentDamage += damage;
        lastHitTime = Time.time;

        if (currentDamage >= requiredDamage)
        {
            currentDamage = requiredDamage;
            isComplete = true;

            if (feedbackLight != null)
                feedbackLight.color = completeColor;
        }

        UpdateText();
    }

    // Implementacja IDamageable
    public void TakeDamage(int damage, string attackerName)
    {
        Debug.Log($"🎯 TimedDamageTarget trafiony przez {attackerName} za {damage} dmg");
        TakeDamage((float)damage);
    }

    private void UpdateText()
    {
        if (requiredText != null)
            requiredText.text = $"{Mathf.CeilToInt(requiredDamage)}";

        if (currentText != null)
            currentText.text = $"{Mathf.CeilToInt(currentDamage)}";
    }
}
