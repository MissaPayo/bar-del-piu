using System.Collections;
using BarDelPiu.Audio;
using BarDelPiu.Visual;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BarDelPiu.Screens
{
    /// <summary>
    /// Referencias al escenario del bar que comparten las pantallas (menú, intro, resultados)
    /// y utilidades de puesta en escena: apagar/encender luces, neón, mostrar a Pipe y Nata.
    /// </summary>
    public class BarScene : MonoBehaviour
    {
        public Camera cam;
        public Light2D globalLight;
        public Light2D[] lampLights;
        public SpriteRenderer[] lamps;
        public NeonFlicker neon;
        public NeonFlicker openSign;
        public TapVisual[] taps;
        public BeerGlassVisual glass;
        public Collider2D glassHitArea;
        public CustomerVisual[] customers;
        public DialogueBox dialogue;
        public StaffArt staff;
        public SpriteRenderer pipe;
        public SpriteRenderer nata;
        [Tooltip("Ampolleta rota (travesura del menú; se repara al jugar una partida).")]
        public Sprite lampBroken;

        [HideInInspector] public bool pipeTipsy, nataTipsy;

        [Header("Valores con las luces encendidas")]
        public float globalOn = 0.8f;
        public float lampOn = 1.1f;
        public float neonLightOn = 0.9f;
        public float openLightOn = 0.6f;

        Vector3 _pipeHome, _nataHome;
        const int HideDepthPx = 52;

        void Awake()
        {
            if (pipe) _pipeHome = pipe.transform.localPosition;
            if (nata) _nataHome = nata.transform.localPosition;
        }

        // ------------------------------------------------------------------ luces

        public void LightsOff()
        {
            if (globalLight) globalLight.intensity = 0.12f;
            for (int i = 0; i < lampLights.Length; i++) SetLamp(i, false);
            SetNeon(neon, false, neonLightOn);
            SetNeon(openSign, false, openLightOn);
        }

        public void LightsOnInstant()
        {
            if (globalLight) globalLight.intensity = globalOn - 0.18f * BrokenLamps();
            for (int i = 0; i < lampLights.Length; i++) SetLamp(i, true);
            SetNeon(neon, true, neonLightOn);
            SetNeon(openSign, true, openLightOn);
        }

        public int BrokenLamps()
        {
            int n = 0;
            for (int i = 0; i < lamps.Length && i < Core.BarMischief.LampCount; i++)
                if (Core.BarMischief.IsLampBroken(i)) n++;
            return n;
        }

        public void SetLamp(int i, bool on)
        {
            if (i < lamps.Length && i < Core.BarMischief.LampCount && Core.BarMischief.IsLampBroken(i))
            {
                if (lampLights[i]) lampLights[i].intensity = 0f;
                if (lamps[i] && lampBroken) lamps[i].sprite = lampBroken;
                if (lamps[i]) lamps[i].color = Color.white;
                return;
            }
            if (i < lampLights.Length && lampLights[i]) lampLights[i].intensity = on ? lampOn : 0f;
            if (i < lamps.Length && lamps[i]) lamps[i].color = on ? Color.white : new Color(0.18f, 0.15f, 0.15f);
        }

        static void SetNeon(NeonFlicker n, bool on, float lightOn)
        {
            if (!n) return;
            n.sign.sprite = on ? n.on : n.off;
            if (n.glow) n.glow.intensity = on ? lightOn : 0f;
            n.enabled = on; // NeonFlicker toma la intensidad base al activarse por primera vez
        }

        /// <summary>Encendido teatral: lámparas una por una con «clic», luego el neón que parpadea.</summary>
        public IEnumerator TurnOnLightsRoutine()
        {
            for (int i = 0; i < lampLights.Length; i++)
            {
                AudioManager.Play(Sfx.LightOn);
                SetLamp(i, true);
                yield return new WaitForSeconds(0.08f);
                SetLamp(i, false);
                yield return new WaitForSeconds(0.06f);
                SetLamp(i, true);
                StartCoroutine(Ramp(globalLight, globalLight.intensity, Mathf.Lerp(0.12f, globalOn, (i + 1f) / (lampLights.Length + 1f)), 0.3f));
                yield return new WaitForSeconds(0.45f);
            }
            yield return new WaitForSeconds(0.25f);
            AudioManager.Play(Sfx.NeonOn);
            float[] pattern = { 0.06f, 0.08f, 0.05f, 0.12f, 0.04f, 0.1f };
            bool state = false;
            foreach (var t in pattern)
            {
                state = !state;
                neon.sign.sprite = state ? neon.on : neon.off;
                if (neon.glow) neon.glow.intensity = state ? neonLightOn : 0f;
                yield return new WaitForSeconds(t);
            }
            SetNeon(neon, true, neonLightOn);
            yield return Ramp(globalLight, globalLight.intensity, globalOn, 0.4f);
            yield return new WaitForSeconds(0.3f);
            AudioManager.Play(Sfx.NeonOn, 0.5f, 1.3f);
            SetNeon(openSign, true, openLightOn);
        }

        static IEnumerator Ramp(Light2D l, float from, float to, float time)
        {
            if (!l) yield break;
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                l.intensity = Mathf.Lerp(from, to, t / time);
                yield return null;
            }
            l.intensity = to;
        }

        // ------------------------------------------------------------------ Pipe y Nata

        public void HideStaff()
        {
            if (pipe) pipe.transform.localPosition = _pipeHome + Vector3.down * HideDepthPx * Px.Unit;
            if (nata) nata.transform.localPosition = _nataHome + Vector3.down * HideDepthPx * Px.Unit;
        }

        /// <summary>Sube a Pipe o Nata desde detrás de la barra con rebote.</summary>
        public Coroutine PopUp(Speaker who, StaffExpression expr = StaffExpression.Feliz)
        {
            var sr = who == Speaker.Pipe ? pipe : nata;
            if (!sr) return null;
            sr.sprite = staff.Get(who, expr);
            AudioManager.Play(Sfx.Pop, 1f, who == Speaker.Pipe ? 0.9f : 1.15f);
            var home = who == Speaker.Pipe ? _pipeHome : _nataHome;
            return PixelTween.MoveLocal(this, sr.transform, home, 0.35f, PixelTween.EaseOutBack);
        }

        public Coroutine HideDown(Speaker who)
        {
            var sr = who == Speaker.Pipe ? pipe : nata;
            if (!sr) return null;
            var home = who == Speaker.Pipe ? _pipeHome : _nataHome;
            return PixelTween.MoveLocal(this, sr.transform, home + Vector3.down * HideDepthPx * Px.Unit, 0.25f, PixelTween.EaseInCubic);
        }

        public void SetExpression(Speaker who, StaffExpression expr)
        {
            var sr = who == Speaker.Pipe ? pipe : nata;
            if (sr) sr.sprite = staff.Get(who, expr);
        }

        /// <summary>Respiración de 1 px de Pipe y Nata cuando están arriba.</summary>
        void Update()
        {
            float t = Time.time;
            Bob(pipe, _pipeHome, t * 1.7f, pipeTipsy);
            Bob(nata, _nataHome, t * 1.9f + 1.3f, nataTipsy);
        }

        static void Bob(SpriteRenderer sr, Vector3 home, float t, bool tipsy)
        {
            if (!sr) return;
            var p = sr.transform.localPosition;
            if (Mathf.Abs(p.y - home.y) > Px.Unit * 1.1f) return; // se está moviendo
            // Achispado: se balancea de lado a lado
            float sway = tipsy ? Mathf.Round(Mathf.Sin(t * 1.3f) * 1.6f) * Px.Unit : 0f;
            sr.transform.localPosition = home + Vector3.right * sway +
                                         (Mathf.Sin(t * Mathf.PI) > 0.4f ? Vector3.up * Px.Unit : Vector3.zero);
        }
    }
}
