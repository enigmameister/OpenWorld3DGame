using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;

public class CitizenIdCardVisual : MonoBehaviour
{
    [Header("Card Variant")]
    [SerializeField] private Renderer cardColorRenderer;
    [SerializeField] private CitizenIdVariantDatabase variantDatabase;

    [Header("Photo")]
    [SerializeField] private SpriteRenderer photoRenderer;

    [Header("Optional Text")]
    [SerializeField] private TMP_Text holderNameText;
    [SerializeField] private TMP_Text citizenNumberText;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private MaterialPropertyBlock propertyBlock;
    private Texture2D loadedPhotoTexture;
    private Sprite loadedPhotoSprite;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    public void Apply(InventoryItemInstance instance)
    {
        if (instance == null ||
            instance.data == null ||
            !instance.hasCitizenIdMeta)
        {
            ClearVisual();
            return;
        }

        CitizenIdMeta meta = instance.citizenId;

        if (holderNameText != null)
            holderNameText.text = meta.holderName;

        if (citizenNumberText != null)
            citizenNumberText.text = meta.citizenIdNumber;

        ApplyVariant(meta.colorVariant);

        StopAllCoroutines();
        StartCoroutine(LoadPhotoRoutine(meta.photoFilePath));
    }

    private void ApplyVariant(int variantIndex)
    {
        if (cardColorRenderer == null)
            return;

        Color color =
            variantDatabase != null
                ? variantDatabase.Get(variantIndex)
                : Color.white;

        cardColorRenderer.GetPropertyBlock(propertyBlock);

        Material material =
            cardColorRenderer.sharedMaterial;

        int propertyId =
            material != null &&
            material.HasProperty(BaseColorId)
                ? BaseColorId
                : ColorId;

        propertyBlock.SetColor(propertyId, color);

        cardColorRenderer.SetPropertyBlock(propertyBlock);
    }

    private IEnumerator LoadPhotoRoutine(string filePath)
    {
        ReleasePhoto();

        if (string.IsNullOrWhiteSpace(filePath) ||
            !File.Exists(filePath))
        {
            if (debugLogs)
            {
                Debug.LogWarning(
                    $"[CITIZEN ID VISUAL] Photo missing: {filePath}",
                    this
                );
            }

            yield break;
        }

        byte[] bytes;

        try
        {
            bytes = File.ReadAllBytes(filePath);
        }
        catch (System.Exception exception)
        {
            if (debugLogs)
            {
                Debug.LogWarning(
                    $"[CITIZEN ID VISUAL] Photo load failed: " +
                    $"{exception.Message}",
                    this
                );
            }

            yield break;
        }

        yield return null;

        loadedPhotoTexture = new Texture2D(
            2,
            2,
            TextureFormat.RGB24,
            false
        );

        if (!loadedPhotoTexture.LoadImage(bytes))
        {
            ReleasePhoto();
            yield break;
        }

        loadedPhotoTexture.wrapMode =
            TextureWrapMode.Clamp;

        loadedPhotoTexture.filterMode =
            FilterMode.Bilinear;

        loadedPhotoSprite = Sprite.Create(
            loadedPhotoTexture,
            new Rect(
                0,
                0,
                loadedPhotoTexture.width,
                loadedPhotoTexture.height
            ),
            new Vector2(0.5f, 0.5f),
            100f
        );

        if (photoRenderer != null)
        {
            photoRenderer.sprite =
                loadedPhotoSprite;

            photoRenderer.enabled = true;
        }
    }

    private void ClearVisual()
    {
        if (holderNameText != null)
            holderNameText.text = string.Empty;

        if (citizenNumberText != null)
            citizenNumberText.text = string.Empty;

        ReleasePhoto();
    }

    private void ReleasePhoto()
    {
        if (photoRenderer != null)
        {
            photoRenderer.sprite = null;
            photoRenderer.enabled = false;
        }

        if (loadedPhotoSprite != null)
        {
            Destroy(loadedPhotoSprite);
            loadedPhotoSprite = null;
        }

        if (loadedPhotoTexture != null)
        {
            Destroy(loadedPhotoTexture);
            loadedPhotoTexture = null;
        }
    }

    private void OnDestroy()
    {
        ReleasePhoto();
    }
}