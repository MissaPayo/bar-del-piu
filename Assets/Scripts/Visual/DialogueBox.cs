using System;
using System.Collections;
using BarDelPiu.Audio;
using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>
    /// Caja de diálogo de Pipe y Nata con efecto máquina de escribir y «voz» de blips.
    /// Ej.: dialogue.Say(Speaker.Nata, StaffExpression.Sorpresa, "¡Pipe, se nos está llenando la barra!");
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        public StaffArt staff;
        public SpriteRenderer panel;
        public SpriteRenderer portrait;
        public PixelText nameText;
        public PixelText bodyText;
        public float charsPerSecond = 40f;
        [Tooltip("Un blip cada N letras.")]
        public int blipEvery = 2;

        Coroutine _routine;
        Vector3 _home;
        bool _skip;

        public bool IsTyping { get; private set; }

        public bool IsShowing => panel != null && panel.enabled;

        static readonly System.Collections.Generic.List<DialogueBox> Visible = new System.Collections.Generic.List<DialogueBox>();

        /// <summary>¿Hay una caja de diálogo visible bajo este punto? (los botones de abajo no reciben el toque)</summary>
        public static bool IsBlocking(Vector3 world)
        {
            foreach (var d in Visible)
            {
                if (d == null || d.panel == null || !d.panel.enabled) continue;
                var b = d.panel.bounds;
                if (world.x >= b.min.x && world.x <= b.max.x && world.y >= b.min.y && world.y <= b.max.y) return true;
            }
            return false;
        }

        void OnDisable() => Visible.Remove(this);

        void Awake()
        {
            _home = transform.localPosition;
            SetVisible(false);
        }

        public void Say(Speaker who, StaffExpression expression, string line, float hideAfter = 3f, Action done = null)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(who, expression, line, hideAfter, done));
        }

        /// <summary>Muestra todo el texto de una vez (para un toque del jugador).</summary>
        public void Skip() => _skip = true;

        public void Hide()
        {
            if (_routine != null) StopCoroutine(_routine);
            SetVisible(false);
        }

        IEnumerator Run(Speaker who, StaffExpression expression, string line, float hideAfter, Action done)
        {
            SetVisible(true);
            _skip = false;
            if (portrait && staff) portrait.sprite = staff.Get(who, expression);
            if (nameText) nameText.Set(who == Speaker.Pipe ? "PIPE" : "NATA");
            bodyText.visibleCharacters = 0;
            bodyText.Set(line);
            bodyText.Rebuild();

            // Entrada: sube 6 px desde abajo
            transform.localPosition = _home + Vector3.down * 6 * Px.Unit;
            yield return PixelTween.MoveLocal(this, transform, _home, 0.15f, PixelTween.EaseOutBack);
            if (portrait) PixelTween.Punch(this, portrait.transform, 0.2f, 0.2f);

            IsTyping = true;
            float pitch = staff ? staff.Pitch(who) : 1f;
            float shown = 0f;
            int last = 0;
            int total = bodyText.VisibleLength;
            while (last < total && !_skip)
            {
                shown += Time.deltaTime * charsPerSecond;
                int now = Mathf.Min(total, Mathf.FloorToInt(shown));
                if (now != last)
                {
                    if (now / blipEvery != last / blipEvery) AudioManager.PlayDialogBlip(pitch);
                    last = now;
                    bodyText.visibleCharacters = now;
                    bodyText.Rebuild();
                }
                yield return null;
            }
            bodyText.visibleCharacters = -1;
            bodyText.Rebuild();
            IsTyping = false;

            if (hideAfter > 0f)
            {
                yield return new WaitForSeconds(hideAfter);
                SetVisible(false);
            }
            done?.Invoke();
        }

        void SetVisible(bool v)
        {
            Visible.Remove(this);
            if (v) Visible.Add(this);
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = v;
            if (nameText) nameText.enabled = v;
            if (bodyText) bodyText.enabled = v;
            if (v)
            {
                nameText?.Rebuild();
                bodyText?.Rebuild();
            }
        }
    }
}
