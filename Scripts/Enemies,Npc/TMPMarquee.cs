using TMPro;
using UnityEngine;

public class TMPMarquee : MonoBehaviour
{
    public float speed = 40f;
    public float pause = 0.6f;

    private TextMeshProUGUI _tmp;
    private RectTransform _rt;
    private float _startX;
    private float _timer;

    void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
        _rt = _tmp ? _tmp.rectTransform : null;
        if (_rt) _startX = _rt.anchoredPosition.x;
    }

    void OnEnable()
    {
        _timer = pause;
        if (_rt) _rt.anchoredPosition = new Vector2(_startX, _rt.anchoredPosition.y);
    }

    void Update()
    {
        if (_tmp == null || _rt == null) return;

        // tylko jeœli tekst siê nie mieœci
        if (_tmp.preferredWidth <= _rt.rect.width + 0.5f) return;

        if (_timer > 0f) { _timer -= Time.unscaledDeltaTime; return; }

        float x = _rt.anchoredPosition.x - speed * Time.unscaledDeltaTime;

        // gdy dojedzie do koñca -> reset + pauza
        float minX = _startX - (_tmp.preferredWidth - _rt.rect.width);
        if (x <= minX)
        {
            x = _startX;
            _timer = pause;
        }

        _rt.anchoredPosition = new Vector2(x, _rt.anchoredPosition.y);
    }
}
