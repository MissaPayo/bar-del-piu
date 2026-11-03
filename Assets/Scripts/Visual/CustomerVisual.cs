using System;
using BarDelPiu.Audio;
using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>
    /// Cliente en la barra: aparece desde atrás de la barra, muestra pedido y paciencia,
    /// cambia de expresión según la paciencia y se va contento o enojado.
    /// </summary>
    public class CustomerVisual : MonoBehaviour
    {
        public CustomerArt art;
        public SpriteRenderer body;
        public SpriteRenderer orderBubble;
        public SpriteRenderer patienceFrame;
        public SpriteRenderer patienceFill;
        [Tooltip("Ancho interior de la barra de paciencia (px).")]
        public int patienceWidthPx = 18;
        [Tooltip("Cuánto baja el cliente para esconderse detrás de la barra (px).")]
        public int hideDepthPx = 56;

        int _variant;
        bool _hardcore;
        BeerKind _order;
        int _hint; // 0 = nada, 1 = espera hace rato, 2 = el más urgente
        float _hintPulse;
        float _patience = 1f;
        Mood _mood = Mood.Neutral;
        bool _served, _leaving, _warned, _uiVisible;
        float _bobT;
        Vector3 _bodyHome;
        Coroutine _move;

        public bool IsLeaving => _leaving;

        void Awake()
        {
            if (body) _bodyHome = body.transform.localPosition;
        }

        public void Setup(int variant, BeerKind order, bool hardcore = false)
        {
            _hardcore = hardcore && art && art.hardcoreVariants.Count > 0; // punks y metaleros
            var list = Variants;
            _variant = list != null && list.Count > 0 ? Mathf.Abs(variant) % list.Count : 0;
            _served = _leaving = _warned = false;
            _patience = 1f;
            _order = order;
            _hint = 0;
            if (orderBubble && art) orderBubble.sprite = art.Order(order);
            SetUiVisible(false);
            SetMood(Mood.Neutral);
            SetPatience(1f);
        }

        System.Collections.Generic.List<CustomerArt.Variant> Variants => !art ? null : _hardcore ? art.hardcoreVariants : art.variants;

        public void SetOrder(BeerKind order)
        {
            _order = order;
            RefreshBubble();
        }

        /// <summary>
        /// Resalta el pedido: 1 = lleva rato esperando (contorno), 2 = es el más urgente (contorno que late).
        /// </summary>
        public void SetHint(int level)
        {
            if (level == _hint) return;
            _hint = level;
            RefreshBubble();
            if (level == 2 && orderBubble) PixelTween.Punch(this, orderBubble.transform, 0.3f, 0.2f);
        }

        void RefreshBubble()
        {
            if (!orderBubble || !art) return;
            var hintSprite = art.OrderHint(_order);
            orderBubble.sprite = _hint > 0 && hintSprite ? hintSprite : art.Order(_order);
        }

        public void SetPatience(float value01)
        {
            _patience = Mathf.Clamp01(value01);
            if (patienceFill)
            {
                int w = Mathf.CeilToInt(_patience * patienceWidthPx);
                patienceFill.enabled = w > 0 && _uiVisible;
                patienceFill.transform.localScale = new Vector3(w, 3f, 1f);
                if (art && art.patienceColor != null) patienceFill.color = art.patienceColor.Evaluate(_patience);
            }
            if (_served || _leaving || !art) return;
            var mood = _patience < art.angryBelow ? Mood.Enojado
                : _patience < art.impatientBelow ? Mood.Impaciente : Mood.Neutral;
            if (mood != _mood)
            {
                SetMood(mood);
                if (body) PixelTween.Punch(this, body.transform, 0.15f, 0.2f);
            }
            if (_patience < art.angryBelow && !_warned)
            {
                _warned = true;
                AudioManager.Play(Sfx.CustomerImpatient);
            }
        }

        public void SetMood(Mood mood)
        {
            _mood = mood;
            var list = Variants;
            if (body && list != null && list.Count > 0) body.sprite = list[_variant].Get(mood);
        }

        /// <summary>Esconde al cliente detrás de la barra (antes de PlayArrive).</summary>
        public void HideBehindCounter()
        {
            if (body) body.transform.localPosition = _bodyHome + Vector3.down * hideDepthPx * Px.Unit;
            SetUiVisible(false);
        }

        /// <summary>Sube desde detrás de la barra con rebote y campanita.</summary>
        /// <summary>Cabecea al ritmo (público del cumpleaños metalero).</summary>
        public bool headbang;

        public void PlayArrive(bool showUi = true)
        {
            SetUiVisible(false);
            if (!body) return;
            body.transform.localPosition = _bodyHome + Vector3.down * hideDepthPx * Px.Unit;
            Restart(PixelTween.MoveLocal(this, body.transform, _bodyHome, 0.35f, PixelTween.EaseOutBack, () =>
            {
                if (!showUi) return; // solo público: sin pedido ni paciencia
                SetUiVisible(true);
                if (orderBubble) PixelTween.Punch(this, orderBubble.transform, 0.35f, 0.25f);
            }));
            AudioManager.Play(Sfx.CustomerArrive);
        }

        /// <summary>Pedido entregado: cara feliz, se oculta la paciencia.</summary>
        public void PlayServed()
        {
            SetHint(0);
            _served = true;
            SetMood(Mood.Feliz);
            SetUiVisible(false);
            if (body) PixelTween.Punch(this, body.transform, 0.3f, 0.25f);
            AudioManager.Play(Sfx.CustomerHappy);
        }

        /// <summary>Se va: feliz (saltito) o enojado (temblor + humo). onDone al terminar.</summary>
        public void PlayLeave(bool happy, Action onDone = null)
        {
            SetHint(0);
            _leaving = true;
            SetUiVisible(false);
            SetMood(happy ? Mood.Feliz : Mood.Enojado);
            if (!happy)
            {
                AudioManager.Play(Sfx.CustomerLeaveAngry);
                if (FeedbackDirector.Instance) FeedbackDirector.Instance.CustomerAngry(transform.position + Vector3.up * 2.6f);
            }
            if (!body)
            {
                onDone?.Invoke();
                return;
            }
            var hop = _bodyHome + Vector3.up * (happy ? 4 : 0) * Px.Unit;
            Restart(PixelTween.Value(this, happy ? 0.2f : 0.35f, t =>
            {
                float shake = happy ? 0f : Mathf.Round(Mathf.Sin(t * 60f) * 1.5f) * Px.Unit;
                body.transform.localPosition = Vector3.Lerp(_bodyHome, hop, Mathf.Sin(t * Mathf.PI)) + Vector3.right * shake;
            }, () =>
            {
                Restart(PixelTween.MoveLocal(this, body.transform, _bodyHome + Vector3.down * hideDepthPx * Px.Unit, 0.3f,
                    PixelTween.EaseInCubic, () => onDone?.Invoke()));
            }));
        }

        void Restart(Coroutine c)
        {
            if (_move != null) StopCoroutine(_move);
            _move = c;
        }

        void SetUiVisible(bool v)
        {
            _uiVisible = v;
            if (orderBubble) orderBubble.enabled = v;
            if (patienceFrame) patienceFrame.enabled = v;
            if (patienceFill) patienceFill.enabled = v && _patience > 0f;
        }

        void Update()
        {
            // El pedido del cliente más urgente «late» para llamar la atención
            if (_hint == 2 && _uiVisible && orderBubble)
            {
                _hintPulse += Time.deltaTime;
                if (_hintPulse >= 0.7f)
                {
                    _hintPulse = 0f;
                    PixelTween.Punch(this, orderBubble.transform, 0.22f, 0.18f);
                }
            }
            // Respiración de 1 px; más rápida cuando se impacienta. Parpadeo de la barra al final.
            if (_leaving || !body) return;
            float speed = headbang ? 9f : _mood == Mood.Enojado ? 6f : _mood == Mood.Impaciente ? 3.5f : 1.6f;
            _bobT += Time.deltaTime * speed;
            if (_move == null || body.transform.localPosition.y >= _bodyHome.y - Px.Unit * 0.5f)
            {
                float bob = Mathf.Sin(_bobT * Mathf.PI) > 0.3f ? Px.Unit : 0f;
                var p = body.transform.localPosition;
                if (Mathf.Abs(p.y - _bodyHome.y) <= Px.Unit + 0.001f) body.transform.localPosition = _bodyHome + Vector3.up * bob;
            }
            if (patienceFill && _uiVisible)
                patienceFill.enabled = _patience > 0f && (_patience > 0.2f || Mathf.Repeat(Time.time * 6f, 1f) > 0.35f);
        }
    }
}
