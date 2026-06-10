using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AutoDoor : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;
    [Tooltip("Nazwa boola steruj¹cego otwieraniem w Animatorze.")]
    public string openParam = "Open";

    [Header("Kto otwiera drzwi")]
    public LayerMask actors = -1;   // domyœlnie wszyscy
    public bool requireTag = true;
    public string[] allowedTags = { "Player" };   // dopisz np. "NPC"

    [Header("Timingi")]
    public float openDelay = 0f;        // zw³oka przy otwieraniu
    public float closeDelay = 0.2f;     // zw³oka przy zamykaniu

    public string openStateName = "ExitDoorsCity_Open";
    public string closeStateName = "ExitDoorsCity_Close";
    public float crossFade = 0.05f; // sekundy

    // runtime
    readonly HashSet<Transform> _inside = new();  // œledzimy kto jest w triggerze
    float _closeAt = -1f, _openAt = -1f;

    void Reset() { animator = GetComponent<Animator>(); }

    void Update()
    {
        // prosta obs³uga opóŸnieñ
        if (_openAt > 0f && Time.time >= _openAt) { SetOpen(true); _openAt = -1f; }
        if (_closeAt > 0f && Time.time >= _closeAt) { SetOpen(false); _closeAt = -1f; }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!Accept(other)) return;
        var key = Root(other.transform);
        if (_inside.Add(key))
        {
            // ktoœ wszed³ -> planuj otwarcie
            _closeAt = -1f;
            if (openDelay <= 0f) SetOpen(true);
            else _openAt = Time.time + openDelay;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!Accept(other)) return;
        var key = Root(other.transform);
        if (_inside.Remove(key))
        {
            // ostatni wyszed³ -> planuj zamkniêcie
            if (_inside.Count == 0)
            {
                _openAt = -1f;
                if (closeDelay <= 0f) SetOpen(false);
                else _closeAt = Time.time + closeDelay;
            }
        }
    }

    bool Accept(Collider c)
    {
        // filtr warstw
        if (((1 << c.gameObject.layer) & actors) == 0) return false;
        // filtr tagów (opcjonalny)
        if (requireTag)
        {
            foreach (var t in allowedTags)
                if (c.CompareTag(t)) return true;
            return false;
        }
        return true;
    }

    Transform Root(Transform t)
    {
        // spina dzieci (wiele colliderów na tej samej postaci)
        return t.root;
    }

    void SetOpen(bool open)
    {
        if (!animator) return;
        animator.SetBool(openParam, open);

        var s = animator.GetCurrentAnimatorStateInfo(0);

        if (open && !s.IsName(openStateName))
            animator.CrossFadeInFixedTime(openStateName, crossFade);
        else if (!open && !s.IsName(closeStateName))
            animator.CrossFadeInFixedTime(closeStateName, crossFade);
    }
}
