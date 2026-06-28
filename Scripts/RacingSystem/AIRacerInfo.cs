using System.Collections.Generic;
using UnityEngine;

public class AIRacerInfo : MonoBehaviour
{
    [Header("Driver")]
    public string driverName;

    private static readonly string[] possibleNames =
    {
        "VOLTAGE", "FLOW", "ROMAN", "BORIS", "NIKITA",
        "SERGEY", "VIKTOR", "SLAYER", "MAX", "LIEBE",
        "AXEL", "MARIO", "SHANG", "RICO", "PYTKENS",
        "SJOW", "DANTE", "X-STATIC", "OSKAR", "MIKE",
        "POWER", "JACK", "BLAKE", "NATE", "ADAM",
        "IWILLDOMINATE", "WASIA", "NOAH", "HANS", "SLIDER"
    };

    private static readonly List<string> usedNames = new();

    public void AssignRandomName()
    {
        List<string> available = new();

        for (int i = 0; i < possibleNames.Length; i++)
        {
            if (!usedNames.Contains(possibleNames[i]))
                available.Add(possibleNames[i]);
        }

        if (available.Count == 0)
        {
            usedNames.Clear();
            available.AddRange(possibleNames);
        }

        driverName = available[Random.Range(0, available.Count)];
        usedNames.Add(driverName);
    }

    public static void ResetUsedNames()
    {
        usedNames.Clear();
    }
}