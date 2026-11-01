using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>
    /// Chorro de cerveza desde la boca de la llave hasta la superficie del vaso.
    /// Al abrir, el chorro «cae»; al cerrar, la cola cae y desaparece.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BeerPourStream : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 14f;
        public float fallSpeed = 14f; // unidades por segundo

        SpriteRenderer _sr;
        Vector3 _from, _to;
        bool _active;
        float _head, _tail; // distancias desde la boca (en unidades)
        float _frameT;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _sr.drawMode = SpriteDrawMode.Tiled;
            _sr.enabled = false;
        }

        public void SetColor(Color c)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            _sr.color = c;
        }

        public void SetFrom(Vector3 from)
        {
            if (from != default) _from = from;
        }

        public void SetTo(Vector3 to) => _to = to;

        public void SetActive(bool active, Vector3 to)
        {
            _to = to;
            if (active && !_active)
            {
                _head = 0f;
                _tail = 0f;
            }
            _active = active;
        }

        void Update()
        {
            float length = Mathf.Max(0f, _from.y - _to.y);
            float dt = Time.deltaTime;
            if (_active)
            {
                _head = Mathf.Min(length, _head + fallSpeed * dt);
                _tail = 0f;
            }
            else
            {
                _tail += fallSpeed * dt;
                _head = Mathf.Min(length, _head + fallSpeed * dt);
            }

            float top = _tail, bottom = _head;
            bool visible = bottom - top > Px.Unit && (_active || _tail < length);
            _sr.enabled = visible;
            if (!visible) return;

            if (frames != null && frames.Length > 0)
            {
                _frameT += dt * fps;
                _sr.sprite = frames[(int)_frameT % frames.Length];
            }
            float h = Px.Snap(bottom - top);
            transform.position = Px.Snap(new Vector3(_from.x, _from.y - top, transform.position.z));
            _sr.size = new Vector2(2 * Px.Unit, h);
        }
    }
}
