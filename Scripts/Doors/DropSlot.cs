using UnityEngine.EventSystems;
using UnityEngine;
using System.Linq;
using TMPro;

public class DropSlot : MonoBehaviour, IDropHandler
{
    public bool AcceptsOnlyOne = true;
    public static GameObject LastDroppedCodon; // ← TO DODAJ

    public void OnDrop(PointerEventData eventData)
    {
        var dragged = eventData.pointerDrag;
        if (dragged == null) return;

        bool slotAlreadyHasCodon = GetComponentsInChildren<DraggableCodon>(true).Length > 0;
        if (AcceptsOnlyOne && slotAlreadyHasCodon) return;

        dragged.transform.SetParent(transform);
        dragged.transform.localPosition = Vector3.zero;

        LastDroppedCodon = dragged;
    }
    public string GetCodon()
    {
        if (transform.childCount == 0) return null;

        foreach (Transform child in transform)
        {
            // Znajdź obiekt z komponentem TMP_Text
            var tmp = child.GetComponentInChildren<TMP_Text>();
            if (tmp != null && tmp.text.Length == 3)
            {
                return tmp.text;
            }
        }

        return null;
    }

    public void ClearSlot()
    {
        if (transform.childCount > 0)
        {
            Transform codon = transform.GetChild(0);
            var drag = codon.GetComponent<DraggableCodon>();
            if (drag != null)
            {
                codon.SetParent(drag.originalParent);
                codon.localPosition = Vector3.zero;
            }
        }
    }
}
