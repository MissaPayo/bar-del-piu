using BarDelPiu.Audio;
using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>
    /// Representación del vaso: líquido + espuma (shader BeerLiquid), chorro, oleaje, salpicadura
    /// y sonido del servido. La lógica de juego solo le pasa niveles; no contiene reglas.
    /// </summary>
    public class BeerGlassVisual : MonoBehaviour
    {
        [Header("Partes")]
        public SpriteRenderer back;
        public SpriteRenderer liquid;
        public SpriteRenderer front;
        public BeerPourStream stream;
        public ParticleSystem splash;
        [Tooltip("Pantalla de concentración con la jarra grande (se muestra mientras se sirve).")]
        public PourFocus focus;
        [Tooltip("Otros renderers de líquido que copian este vaso (la jarra grande de la concentración).")]
        public SpriteRenderer[] mirrorLiquids;

        [Header("Geometría (px, ver glass.json)")]
        public int innerBottomPx = 3;
        public int innerTopPx = 22;

        [Header("Sensación")]
        public float wobbleWhilePouring = 1f;
        public float wobbleDecay = 2.5f;
        public bool autoAudio = true;
        [Tooltip("Pitch del chorro con el vaso vacío y lleno: el jugador «escucha» el nivel.")]
        public Vector2 pourPitch = new Vector2(0.85f, 1.35f);

        static readonly int LiquidId = Shader.PropertyToID("_Liquid");
        static readonly int FoamId = Shader.PropertyToID("_Foam");
        static readonly int LightId = Shader.PropertyToID("_LiquidLight");
        static readonly int DarkId = Shader.PropertyToID("_LiquidDark");
        static readonly int FoamColId = Shader.PropertyToID("_FoamColor");
        static readonly int FoamShadeId = Shader.PropertyToID("_FoamShade");
        static readonly int WobbleId = Shader.PropertyToID("_Wobble");
        static readonly int TargetId = Shader.PropertyToID("_Target");
        static readonly int ShowTargetId = Shader.PropertyToID("_ShowTarget");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int BubbleId = Shader.PropertyToID("_BubbleRate");
        static readonly int BottomId = Shader.PropertyToID("_InnerBottom");
        static readonly int TopId = Shader.PropertyToID("_InnerTop");

        MaterialPropertyBlock _mpb;
        float _liquid, _foam, _wobble, _flash, _target = 0.8f;
        bool _showTarget, _pouring;
        BeerKind _kind;
        AudioLoopHandle _pourLoop;

        public BeerKind Kind => _kind;
        public bool IsPouring => _pouring;
        public float Liquid => _liquid;
        public float Foam => _foam;

        /// <summary>Altura de la superficie en coordenadas de mundo (para chorro y partículas).</summary>
        public Vector3 SurfaceWorld
        {
            get
            {
                float innerH = innerTopPx - innerBottomPx + 1;
                float py = innerBottomPx + Mathf.Clamp01(_liquid + _foam) * innerH;
                return transform.TransformPoint(new Vector3(0f, py * Px.Unit, 0f));
            }
        }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            SetBeer(BeerKind.Rubia);
            Push();
        }

        void OnDisable() => StopPourAudio();

        public void SetBeer(BeerKind kind)
        {
            _kind = kind;
            if (stream) stream.SetColor(BarPalette.LiquidLight(kind));
            if (splash)
            {
                var main = splash.main;
                main.startColor = BarPalette.Foam(kind);
            }
            Push();
        }

        /// <summary>Niveles como fracción de la capacidad. Líquido + espuma se recortan a 1.</summary>
        public void SetLevels(float liquid01, float foam01)
        {
            _liquid = Mathf.Clamp01(liquid01);
            _foam = Mathf.Clamp(foam01, 0f, 1f - _liquid);
            if (_pourLoop != null && _pourLoop.IsPlaying)
                _pourLoop.SetPitch(Mathf.Lerp(pourPitch.x, pourPitch.y, _liquid + _foam));
        }

        /// <summary>Activa/desactiva el chorro. from = boca de la llave en coordenadas de mundo.</summary>
        public void SetPouring(bool pouring, Vector3 from = default)
        {
            if (stream) stream.SetFrom(from);
            if (pouring == _pouring) return;
            _pouring = pouring;
            if (stream) stream.SetActive(pouring, SurfaceWorld);
            if (focus && focus.isActiveAndEnabled)
            {
                if (pouring) focus.Show();
                else focus.HideSoon();
            }
            if (splash)
            {
                if (pouring) splash.Play();
                else splash.Stop();
            }
            if (!autoAudio) return;
            if (pouring)
            {
                AudioManager.Play(Sfx.TapOpen);
                _pourLoop?.Stop(0.02f);
                _pourLoop = AudioManager.StartLoop(Sfx.PourLoop, 1f, Mathf.Lerp(pourPitch.x, pourPitch.y, _liquid + _foam));
            }
            else
            {
                AudioManager.Play(Sfx.TapClose);
                StopPourAudio();
            }
        }

        public void SetTargetLevel(float level01, bool show)
        {
            _target = Mathf.Clamp01(level01);
            _showTarget = show;
        }

        /// <summary>Destello blanco corto (al evaluar o entregar).</summary>
        public void Flash(float amount = 0.8f) => _flash = amount;

        /// <summary>Sacudida del líquido (al apoyar o mover el vaso).</summary>
        public void Slosh(float amount = 1.5f) => _wobble = Mathf.Max(_wobble, amount);

        public void Clear()
        {
            SetPouring(false);
            SetLevels(0f, 0f);
            _wobble = 0f;
        }

        void StopPourAudio()
        {
            _pourLoop?.Stop(0.06f);
            _pourLoop = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float targetWobble = _pouring ? wobbleWhilePouring : 0f;
            _wobble = _wobble > targetWobble ? Mathf.MoveTowards(_wobble, targetWobble, wobbleDecay * dt) : targetWobble;
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 5f);

            if (_pouring)
            {
                var surface = SurfaceWorld;
                if (stream) stream.SetTo(surface);
                if (splash) splash.transform.position = surface;
            }
            Push();
        }

        void Push()
        {
            if (liquid == null) return;
            _mpb ??= new MaterialPropertyBlock();
            liquid.GetPropertyBlock(_mpb);
            _mpb.SetFloat(LiquidId, _liquid);
            _mpb.SetFloat(FoamId, _foam);
            _mpb.SetColor(LightId, BarPalette.LiquidLight(_kind));
            _mpb.SetColor(DarkId, BarPalette.LiquidDark(_kind));
            _mpb.SetColor(FoamColId, BarPalette.Foam(_kind));
            _mpb.SetColor(FoamShadeId, BarPalette.FoamShade(_kind));
            _mpb.SetFloat(WobbleId, _wobble);
            _mpb.SetFloat(TargetId, _target);
            _mpb.SetFloat(ShowTargetId, _showTarget ? 1f : 0f);
            _mpb.SetFloat(FlashId, _flash);
            _mpb.SetFloat(BubbleId, _kind == BeerKind.Negra ? 0.2f : 0.6f);
            _mpb.SetFloat(BottomId, innerBottomPx);
            _mpb.SetFloat(TopId, innerTopPx);
            liquid.SetPropertyBlock(_mpb);
            if (mirrorLiquids != null)
                foreach (var m in mirrorLiquids)
                    if (m) m.SetPropertyBlock(_mpb);
        }
    }
}
