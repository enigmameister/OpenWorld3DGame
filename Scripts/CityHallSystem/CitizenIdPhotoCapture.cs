using System;
using System.Collections;
using System.IO;
using UnityEngine;

public class CitizenIdPhotoCapture : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera photoCamera;
    [SerializeField] private RenderTexture renderTexture;

    [Header("Output")]
    [SerializeField]
    private string outputFolderName =
        "CitizenIdPhotos";

    [SerializeField]
    private string filePrefix =
        "CitizenIdPhoto";

    [Header("Capture")]
    [Tooltip("Opcjonalne opóŸnienie przed renderem, np. na aktualizacjê pozy modelu.")]
    [SerializeField, Min(0f)] private float captureDelay = 0f;

    [SerializeField]
    private TextureFormat textureFormat =
        TextureFormat.RGB24;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    public Texture2D LastCapturedTexture { get; private set; }
    public string LastCapturedFilePath { get; private set; }

    public bool IsCapturing { get; private set; }

    public event Action<Texture2D, string> PhotoCaptured;
    public event Action<string> CaptureFailed;

    private void Awake()
    {
        ResolveReferences();

        if (photoCamera != null)
            photoCamera.enabled = false;
    }

    public void Capture(
        string holderName,
        Action<Texture2D, string> onCompleted = null,
        Action<string> onFailed = null)
    {
        if (IsCapturing)
        {
            string reason = "PHOTO_CAPTURE_ALREADY_RUNNING";

            onFailed?.Invoke(reason);
            CaptureFailed?.Invoke(reason);
            return;
        }

        StartCoroutine(
            CaptureRoutine(
                holderName,
                onCompleted,
                onFailed
            )
        );
    }

    private IEnumerator CaptureRoutine(
        string holderName,
        Action<Texture2D, string> onCompleted,
        Action<string> onFailed)
    {
        IsCapturing = true;

        if (!ValidateReferences(out string validationError))
        {
            FinishWithFailure(
                validationError,
                onFailed
            );

            yield break;
        }

        if (captureDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                captureDelay
            );
        }

        // Pozwala Unity zakoñczyæ aktualizacjê transformów,
        // animatorów i kamery w bie¿¹cej klatce.
        yield return new WaitForEndOfFrame();

        RenderTexture previousActive =
            RenderTexture.active;

        RenderTexture previousTarget =
            photoCamera.targetTexture;

        Texture2D capturedTexture = null;

        try
        {
            photoCamera.targetTexture = renderTexture;

            renderTexture.DiscardContents();

            photoCamera.Render();

            RenderTexture.active = renderTexture;

            capturedTexture = new Texture2D(
                renderTexture.width,
                renderTexture.height,
                textureFormat,
                mipChain: false
            );

            capturedTexture.ReadPixels(
                new Rect(
                    0,
                    0,
                    renderTexture.width,
                    renderTexture.height
                ),
                0,
                0,
                recalculateMipMaps: false
            );

            capturedTexture.Apply(
                updateMipmaps: false,
                makeNoLongerReadable: false
            );

            byte[] pngBytes =
                capturedTexture.EncodeToPNG();

            if (pngBytes == null || pngBytes.Length == 0)
            {
                throw new InvalidOperationException(
                    "Texture could not be encoded to PNG."
                );
            }

            string directoryPath =
                GetOutputDirectoryPath();

            Directory.CreateDirectory(directoryPath);

            string safeHolderName =
                SanitizeFileName(holderName);

            string fileName =
                $"{filePrefix}_{safeHolderName}.png";

            string filePath =
                Path.Combine(directoryPath, fileName);

            // Nadpisuje poprzedni plik tego profilu.
            File.WriteAllBytes(filePath, pngBytes);

            ReplaceLastCapturedTexture(capturedTexture);

            LastCapturedFilePath = filePath;

            capturedTexture = null;
            IsCapturing = false;

            if (debugLogs)
            {
                Debug.Log(
                    $"[CITIZEN ID PHOTO CAPTURE] Photo saved: " +
                    $"{LastCapturedFilePath}"
                );
            }

            onCompleted?.Invoke(
                LastCapturedTexture,
                LastCapturedFilePath
            );

            PhotoCaptured?.Invoke(
                LastCapturedTexture,
                LastCapturedFilePath
            );
        }
        catch (Exception exception)
        {
            if (capturedTexture != null)
                Destroy(capturedTexture);

            FinishWithFailure(
                exception.Message,
                onFailed
            );
        }
        finally
        {
            RenderTexture.active = previousActive;
            photoCamera.targetTexture = previousTarget;
        }
    }

    private void ReplaceLastCapturedTexture(
        Texture2D newTexture)
    {
        if (LastCapturedTexture != null &&
            LastCapturedTexture != newTexture)
        {
            Destroy(LastCapturedTexture);
        }

        LastCapturedTexture = newTexture;
    }

    private void FinishWithFailure(
        string reason,
        Action<string> onFailed)
    {
        IsCapturing = false;

        Debug.LogWarning(
            $"[CITIZEN ID PHOTO CAPTURE] Capture failed: {reason}"
        );

        onFailed?.Invoke(reason);
        CaptureFailed?.Invoke(reason);
    }

    private bool ValidateReferences(
        out string failureReason)
    {
        ResolveReferences();

        if (photoCamera == null)
        {
            failureReason = "PHOTO_CAMERA_MISSING";
            return false;
        }

        if (renderTexture == null)
        {
            failureReason = "RENDER_TEXTURE_MISSING";
            return false;
        }

        if (renderTexture.width <= 0 ||
            renderTexture.height <= 0)
        {
            failureReason = "INVALID_RENDER_TEXTURE_SIZE";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private void ResolveReferences()
    {
        if (photoCamera == null)
            photoCamera = GetComponentInChildren<Camera>(true);

        if (renderTexture == null &&
            photoCamera != null)
        {
            renderTexture = photoCamera.targetTexture;
        }
    }

    private string GetOutputDirectoryPath()
    {
        return Path.Combine(
            Application.persistentDataPath,
            outputFolderName
        );
    }

    private string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unknown";

        string sanitized = value.Trim();

        char[] invalidCharacters =
            Path.GetInvalidFileNameChars();

        for (int i = 0; i < invalidCharacters.Length; i++)
        {
            sanitized = sanitized.Replace(
                invalidCharacters[i],
                '_'
            );
        }

        sanitized = sanitized.Replace(' ', '_');

        return string.IsNullOrWhiteSpace(sanitized)
            ? "Unknown"
            : sanitized;
    }

    private void OnDestroy()
    {
        if (LastCapturedTexture != null)
        {
            Destroy(LastCapturedTexture);
            LastCapturedTexture = null;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Capture Test Photo")]
    private void DebugCaptureTestPhoto()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[CITIZEN ID PHOTO CAPTURE] " +
                "Enter Play Mode before capturing."
            );

            return;
        }

        Capture("TestPlayer");
    }
#endif
}