using System;
using UnityEngine;

[CreateAssetMenu(
    menuName = "City Hall/Citizen ID Variant Database",
    fileName = "CitizenIdVariantDatabase")]
public class CitizenIdVariantDatabase : ScriptableObject
{
    [Serializable]
    public class Variant
    {
        public string displayName;
        public Color color = Color.white;
    }

    [SerializeField]
    private Variant[] variants;

    public int Count =>
        variants != null
            ? variants.Length
            : 0;

    public Color Get(int index)
    {
        if (variants == null ||
            variants.Length == 0)
        {
            return Color.white;
        }

        index = Mathf.Clamp(
            index,
            0,
            variants.Length - 1
        );

        Variant variant = variants[index];

        return variant != null
            ? variant.color
            : Color.white;
    }

    public string GetDisplayName(int index)
    {
        if (variants == null ||
            variants.Length == 0)
        {
            return string.Empty;
        }

        index = Mathf.Clamp(
            index,
            0,
            variants.Length - 1
        );

        Variant variant = variants[index];

        return variant != null
            ? variant.displayName
            : string.Empty;
    }
}