using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GlassTile : MonoBehaviour
{
    [Header("Parametry zapadania")]
    public float delayBeforeFall = 0.2f;
    public float fallDistance = 3f;
    public float fallSpeed = 3f;
    public bool destroyAfterFall = true;

    [Header("Wygl¹d / podgl¹d")]
    public Renderer tileRenderer;       // renderer szk³a (np. Visual MeshRenderer)
    public Material safeMaterial;       // ZIELONY
    public Material fragileMaterial;    // CZERWONY

    private Material _originalMaterial;

    // czy ta szyba jest „z³a”
    private bool _isFragile = false;
    private bool _broken = false;

    // co ma spadaæ (ca³y kafel)
    private Transform _tileRoot;
    private Collider[] _allColliders;

    public void SetFragile(bool fragile)
    {
        _isFragile = fragile;
    }

    private void Awake()
    {
        _tileRoot = transform.parent != null ? transform.parent : transform;
        _allColliders = _tileRoot.GetComponentsInChildren<Collider>();

        if (tileRenderer == null)
            tileRenderer = _tileRoot.GetComponentInChildren<Renderer>();

        if (tileRenderer != null)
            _originalMaterial = tileRenderer.material;
    }

    // === HELPER: pokazuje / ukrywa podgl¹d dobre/z³e szk³o ===
    public void Reveal(bool reveal)
    {
        if (tileRenderer == null) return;

        if (reveal)
        {
            // poka¿ kolor zale¿ny od tego czy p³ytka jest z³a
            if (_isFragile && fragileMaterial != null)
                tileRenderer.material = fragileMaterial;
            else if (!_isFragile && safeMaterial != null)
                tileRenderer.material = safeMaterial;
        }
        else
        {
            // wróæ do oryginalnego materia³u szk³a
            if (_originalMaterial != null)
                tileRenderer.material = _originalMaterial;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_broken) return;
        if (!other.CompareTag("Player")) return;

        if (_isFragile)
        {
            StartCoroutine(BreakRoutine());
        }
    }

    private IEnumerator BreakRoutine()
    {
        _broken = true;

        yield return new WaitForSeconds(delayBeforeFall);

        foreach (var col in _allColliders)
        {
            if (col != null)
                col.enabled = false;
        }

        Vector3 start = _tileRoot.position;
        Vector3 end = start + Vector3.down * fallDistance;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * fallSpeed;
            _tileRoot.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        if (destroyAfterFall)
        {
            Destroy(_tileRoot.gameObject);
        }
        else if (tileRenderer != null)
        {
            tileRenderer.enabled = false;
        }
    }
}
