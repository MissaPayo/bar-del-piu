using BarDelPiu.Audio;
using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>Manija de una llave: reposo, seleccionada (contorno crema) y tirada (sirviendo).</summary>
    public class TapVisual : MonoBehaviour
    {
        public BeerKind kind;
        public SpriteRenderer handle;
        public Sprite idle;
        public Sprite selected;
        public Sprite pulled;
        [Tooltip("Contorno de aviso: un cliente que espera pidió esta cerveza.")]
        public Sprite hint;
        [Tooltip("Boca del caño: origen del chorro.")]
        public Transform nozzle;

        bool _selected, _pouring;
        int _hint; // 0 = nada, 1 = cliente esperando, 2 = el cliente más urgente (pulsa)

        public Vector3 NozzlePosition => nozzle ? nozzle.position : transform.position;

        [Tooltip("Columna táctil de la llave (manija, caño y vaso). Ancha para dedos en celular.")]
        public Collider2D hitArea;

        /// <summary>¿El punto (mundo) cae en la columna de esta llave?</summary>
        public bool Contains(Vector3 world) => hitArea != null && hitArea.OverlapPoint(world);

        public void SetSelected(bool value, bool playSound = true)
        {
            if (value && !_selected)
            {
                PixelTween.Punch(this, handle.transform, 0.25f, 0.16f);
                if (playSound) AudioManager.Play(Sfx.TapSelect);
            }
            _selected = value;
            Refresh();
        }

        public void SetPouring(bool value)
        {
            _pouring = value;
            Refresh();
        }

        /// <summary>Resalta la llave: 1 = la pide un cliente que espera, 2 = la pide el más urgente (parpadea).</summary>
        public void SetHint(int level)
        {
            if (level == _hint) return;
            _hint = level;
            Refresh();
        }

        void Update()
        {
            if (_hint == 2 && !_pouring) Refresh(); // el contorno del urgente pulsa
        }

        void Refresh()
        {
            if (handle == null) return;
            bool pulseOn = _hint == 1 || (_hint == 2 && Mathf.Repeat(Time.time * 3f, 1f) < 0.6f);
            handle.sprite = _pouring ? pulled : (pulseOn && hint) ? hint : _selected ? selected : idle;
        }

        void OnValidate() => Refresh();
    }
}
