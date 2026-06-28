using UnityEngine;

public class PuzzleCodeManager : MonoBehaviour
{
    public static PuzzleCodeManager Instance;

    private int[] codeDigits = new int[4];
    public int[] CodeDigits => codeDigits; // tylko getter

    void Start()
    {
        // opcjonalnie wyczyść startowy kod
        for (int i = 0; i < codeDigits.Length; i++)
            codeDigits[i] = -1;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public string GetFullCode()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < codeDigits.Length; i++)
            sb.Append(Mathf.Clamp(codeDigits[i], 0, 9)); // <- upewnij się że nie -1

        return sb.ToString();
    }

    public void SetDigit(int index, int value)
    {
        if (index < 0 || index >= codeDigits.Length)
            return;

        if (codeDigits[index] != -1)
        {
            Debug.LogWarning($"🔁 Nadpisanie kodu [{index}] = {codeDigits[index]} ➜ {value}");
        }

        codeDigits[index] = Mathf.Clamp(value, 0, 9);
        Debug.Log($"🔐 Kod[{index}] = {codeDigits[index]}");
    }

    public bool IsCodeComplete()
    {
        for (int i = 0; i < codeDigits.Length; i++)
            if (codeDigits[i] == -1)
                return false;

        return true;
    }

}