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
        [Tooltip("Boca del caño: origen del chorro.")]
        public Transform nozzle;

        bool _selected, _pouring;

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

        void Refresh()
        {
            if (handle == null) return;
            handle.sprite = _pouring ? pulled : _selected ? selected : idle;
        }

        void OnValidate() => Refresh();
    }
}
