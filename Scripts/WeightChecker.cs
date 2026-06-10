using UnityEngine;
using TMPro;
using System.Collections.Generic;

[RequireComponent(typeof(Renderer))]
public class WeightCheck : MonoBehaviour
{
    [Header("Wygląd")]
    public Color inactiveColor = Color.red;
    public Color activeColor = Color.green;
    public TextMeshPro weightText;  // <- poprawione na TextMeshPro (3D tekst)

    [Header("Wymagania")]
    [Tooltip("Docelowa suma kg wszystkich obiektów z MovableObject.")]
    public float requiredMass = 30f;
    [Tooltip("Tolerancja porównania masy (kg).")]
    public float massTolerance = 0.1f;

    [Header("Collidery wagi")]
    public Collider physicsCollider;
    public Collider detectionTrigger;

    [Header("Płynne wyświetlanie")]
    public float weightChangeSpeed = 10f;

    [Header("Drzwi / Animator")]
    public Animator doorAnimator;
    public string doorOpenParam = "Open";
    public bool doorOpenIsTrigger = true;

    private Renderer platformRenderer;
    private float displayedMass = 0f;
    private float targetMass = 0f;
    private bool isValid = false;
    private bool doorOpenedOnce = false;

    private readonly List<Collider> inside = new();

    void Start()
    {
        platformRenderer = GetComponent<Renderer>();

        // automatyczne przypisanie colliderów
        if (!detectionTrigger)
        {
            foreach (var col in GetComponents<Collider>())
                if (col.isTrigger) { detectionTrigger = col; break; }
        }
        if (!physicsCollider)
        {
            foreach (var col in GetComponents<Collider>())
                if (!col.isTrigger) { physicsCollider = col; break; }
        }

        platformRenderer.material.color = inactiveColor;
        if (weightText) weightText.text = "0 kg";
    }

    void Update()
    {
        targetMass = 0f;

        if (detectionTrigger && inside.Count > 0)
        {
            Bounds plat = detectionTrigger.bounds;

            // 1) zbuduj zestaw unikalnych RB z colliderów będących w triggerze
            var uniqueRBs = new HashSet<Rigidbody>();
            for (int i = inside.Count - 1; i >= 0; i--)
            {
                var c = inside[i];
                if (!c || !c.gameObject.activeInHierarchy) { inside.RemoveAt(i); continue; }
                if (c.attachedRigidbody) uniqueRBs.Add(c.attachedRigidbody);
            }

            // 2) iteruj po unikalnych RB
            foreach (var rb in uniqueRBs)
            {
                if (!rb) continue;

                var movable = rb.GetComponent<MovableObject>();
                if (!movable) continue;                // musi mieć MovableObject

                if (!movable.IsResting()) continue;    // liczymy tylko stojące

                // Czy któryś collider tego RB mieści się w pełni w XZ w obrębie platformy?
                if (!AnyColliderInsideXZ(rb, plat)) continue;

                targetMass += rb.mass;                 // <-- DODAJEMY MASĘ TYLKO RAZ
            }
        }

        // — płynne wyświetlanie —
        if (Mathf.Abs(displayedMass - targetMass) > 0.01f)
        {
            displayedMass = Mathf.MoveTowards(displayedMass, targetMass, weightChangeSpeed * Time.deltaTime);
            if (weightText) weightText.text = $"{Mathf.RoundToInt(displayedMass)} kg";
        }
        else
        {
            if (weightText) weightText.text = $"{Mathf.RoundToInt(targetMass)} kg";
        }

        // logika na rzeczywistej sumie (bez wygładzania)
        float massForLogic = targetMass;

        // zielono jeśli masa >= wymagana - tolerancja
        bool massOk = massForLogic + massTolerance >= requiredMass;

        // opcjonalnie: nie pokazuj zielonego przy całkowitym braku masy
        isValid = massOk && massForLogic > 0.01f;

        platformRenderer.material.color = isValid ? activeColor : inactiveColor;

        // jednorazowe otwarcie
        if (isValid && !doorOpenedOnce && doorAnimator)
        {
            doorOpenedOnce = true;
            if (doorOpenIsTrigger) doorAnimator.SetTrigger(doorOpenParam);
            else doorAnimator.SetBool(doorOpenParam, true);
        }
    }

    // Helper: czy JAKIKOLWIEK collider z danego RB jest w pełni w obrysie platformy (po X i Z)
    bool AnyColliderInsideXZ(Rigidbody rb, Bounds plat)
    {
        // bierzemy tylko nie-triggerowe collidery „bryły” (te od geometrii)
        var cols = rb.GetComponentsInChildren<Collider>();
        foreach (var col in cols)
        {
            if (!col || col.isTrigger) continue;
            Bounds obj = col.bounds;

            bool insideXZ =
                obj.min.x >= plat.min.x && obj.max.x <= plat.max.x &&
                obj.min.z >= plat.min.z && obj.max.z <= plat.max.z;

            if (insideXZ) return true;
        }
        return false;
    }


    void OnTriggerEnter(Collider other)
    {
        if (!detectionTrigger) return;
        if (!other || !other.gameObject.activeInHierarchy) return;

        if (!inside.Contains(other))
            inside.Add(other);
    }

    void OnTriggerExit(Collider other)
    {
        inside.Remove(other);
        if (inside.Count == 0 && weightText) weightText.text = "0 kg";
    }
}
