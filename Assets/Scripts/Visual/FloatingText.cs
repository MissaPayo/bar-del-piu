using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>Texto que aparece con «pop», sube y se desvanece. Se crea con FloatingText.Spawn.</summary>
    public class FloatingText : MonoBehaviour
    {
        PixelText _text;
        float _t, _duration, _delay;
        int _risePx;
        bool _pop;
        Vector3 _start;

        public static FloatingText Spawn(MonoBehaviour owner, PixelFont font, Material mat, string text, Vector3 pos,
            Color color, int scale = 1, int rise = 10, float duration = 0.9f, float delay = 0f, bool pop = false)
        {
            if (font == null) return null;
            var go = new GameObject("FloatingText");
            go.transform.SetParent(owner.transform, false);
            go.transform.position = Px.Snap(new Vector3(pos.x, pos.y, 0f));
            var pt = go.AddComponent<PixelText>();
            pt.font = font;
            pt.material = mat;
            pt.text = text;
            pt.color = color;
            pt.scale = scale;
            pt.sortingOrder = SortOrder.Overlay;
            var ft = go.AddComponent<FloatingText>();
            ft._text = pt;
            ft._duration = duration;
            ft._delay = delay;
            ft._risePx = rise;
            ft._pop = pop;
            ft._start = go.transform.position;
            pt.Rebuild();
            go.SetActive(delay <= 0f);
            if (delay > 0f) owner.StartCoroutine(ft.ActivateLater(delay));
            return ft;
        }

        System.Collections.IEnumerator ActivateLater(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (this) gameObject.SetActive(true);
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _duration);
            float rise = PixelTween.EaseOutCubic(k) * _risePx;
            transform.position = Px.Snap(_start + Vector3.up * rise * Px.Unit);

            if (_pop)
            {
                float s = _t < 0.1f ? Mathf.Lerp(0.6f, 1.25f, _t / 0.1f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((_t - 0.1f) / 0.1f));
                transform.localScale = new Vector3(s, s, 1f);
            }
            if (k > 0.65f) _text.SetAlpha(1f - (k - 0.65f) / 0.35f);
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
