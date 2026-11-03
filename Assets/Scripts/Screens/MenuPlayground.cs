using System.Collections;
using BarDelPiu.Audio;
using BarDelPiu.Core;
using BarDelPiu.UI;
using BarDelPiu.Visual;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BarDelPiu.Screens
{
    /// <summary>
    /// Detalles jugables del menú principal:
    /// - Las llaves funcionan: se sirve manteniendo el vaso y se le da la cerveza a Pipe o Nata tocándolos.
    ///   Cada uno comenta distinto; con la cerveza 11 queda ebrio y se va hasta la próxima partida.
    ///   Si uno está ebrio, el otro acepta solo 9 y después se niega (tiene que trabajar).
    /// - Las ampolletas parpadean al tocarlas y se rompen si se insiste: eso activa el modo Hardcore.
    /// </summary>
    public class MenuPlayground : MonoBehaviour
    {
        public BarScene bar;
        public Sprite lampBroken;
        [Tooltip("Toques para romper una ampolleta.")]
        public int tapsToBreakLamp = 6;
        public float pourRate = 0.35f;

        static readonly string[] PipeLines =
        {
            "¡SALUD! ASÍ SE SIRVE UNA CERVEZA.",
            "ESPUMA PAREJITA. ¡ME GUSTA!",
            "OTRA MÁS Y CIERRO LA LLAVE, EH.",
            "UF... YA SIENTO ESPUMA EN EL BIGOTE.",
            "¿ESTA ERA LA QUINTA O LA CUARTA?",
            "¡NATA, LA BARRA SE MUEVE SOLA!",
            "TE QUIERO MUCHO, ¿SABÍAS? *HIP*",
            "YO... SOY EL MAESTRO DE LA BARRA. *HIP*",
            "NO VEO LAS LLAVES... ¿CUÁL ERA LA R?",
            "UNA MÁS Y ME VOY A DORMIR... *HIP*",
        };

        static readonly string[] NataLines =
        {
            "¡GRACIAS! LO ANOTO EN LA CUENTA DE PIPE.",
            "MMM, NADA MAL. ¡SIGUE PRACTICANDO!",
            "OJO QUE YO ANOTO TODO, ¿EH?",
            "¿CUÁNTAS VAN? PERDÍ LA CUENTA... ¡YO!",
            "¡LOS PEDIDOS SE ME ESTÁN MEZCLANDO!",
            "¡ESTA RONDA LA PAGA PIPE! JAJA.",
            "¡TODOS LOS CLIENTES SON MIS AMIGOS!",
            "ME DUELE LA CARA DE TANTO REÍR. *HIP*",
            "¿QUIÉN PUSO DOS BARRAS? *HIP*",
            "UNA MÁS Y NO RESPONDO... *HIP*",
        };

        static readonly string[] PipeLinesHardcore =
        {
            "¡SALUD, METALERO! ESTA SABE A DISTORSIÓN.",
            "ESPUMA NEGRA COMO MI CHAQUETA. ¡ME GUSTA!",
            "¡SUBE EL VOLUMEN QUE NO ESCUCHO LA ESPUMA!",
            "UF... YA VEO TRES LLAVES Y UNA GUITARRA.",
            "¿ESTA ERA LA QUINTA O EL QUINTO SOLO?",
            "¡NATA, HAGAMOS POGO DETRÁS DE LA BARRA!",
            "TE QUIERO, HERMANO DE RUIDO. *HIP*",
            "YO... SOY EL REY DEL MOSH. *HIP*",
            "NO VEO LAS LLAVES... TODO ESTÁ A OSCURAS.",
            "UNA MÁS Y ME TIRO DEL ESCENARIO... *HIP*",
        };

        static readonly string[] NataLinesHardcore =
        {
            "¡GRACIAS! ¡ESTO ES MÁS PUNK QUE MI CRESTA!",
            "MMM, AMARGA COMO UNA BALADA METALERA.",
            "OJO QUE ANOTO TODO... CON SPRAY.",
            "¿CUÁNTAS VAN? PERDÍ LA CUENTA EN EL POGO.",
            "¡LOS PEDIDOS SUENAN A RIFF DE GUITARRA!",
            "¡ESTA RONDA LA PAGA PIPE! ¡OI! ¡OI! ¡OI!",
            "¡TODOS LOS PUNKS SON MIS AMIGOS!",
            "ME DUELE EL CUELLO DE TANTO CABECEAR. *HIP*",
            "¿QUIÉN PUSO DOS ESCENARIOS? *HIP*",
            "UNA MÁS Y ME LANZO AL PÚBLICO... *HIP*",
        };

        static bool Hardcore => BarMischief.AnyLampBroken;

        int _selected;
        bool _pouring;
        float _liquid, _foam;
        readonly int[] _lampTaps = new int[BarMischief.LampCount];
        readonly float[] _lastLampTap = new float[BarMischief.LampCount];
        BoxCollider2D _pipeHit, _nataHit;
        BoxCollider2D[] _lampHits;
        bool _busy;
        float _nextHiccup;

        public bool Interactive { get; set; } = true;

        /// <summary>Se rompió la primera ampolleta: el menú se pone en modo Hardcore.</summary>
        public event System.Action HardcoreActivated;

        void Start()
        {
            bar.glass.gameObject.SetActive(true);
            bar.glass.SetBeer(BeerKind.Rubia);
            bar.glass.SetLevels(0f, 0f);
            for (int i = 0; i < bar.taps.Length; i++) bar.taps[i].SetSelected(i == 0, false);

            _pipeHit = AddHit(bar.pipe.transform, new Vector2(40, 40), new Vector2(0, 26));
            _nataHit = AddHit(bar.nata.transform, new Vector2(40, 40), new Vector2(0, 26));
            _lampHits = new BoxCollider2D[bar.lamps.Length];
            for (int i = 0; i < bar.lamps.Length; i++)
                _lampHits[i] = AddHit(bar.lamps[i].transform, new Vector2(18, 30), new Vector2(0, -14));
            ApplyLampState();
            _nextHiccup = Time.time + 4f;
        }

        static BoxCollider2D AddHit(Transform t, Vector2 sizePx, Vector2 offsetPx)
        {
            var c = t.gameObject.AddComponent<BoxCollider2D>();
            c.isTrigger = true;
            c.size = sizePx / 16f;
            c.offset = offsetPx / 16f;
            return c;
        }

        /// <summary>Ampolletas rotas en partidas anteriores siguen rotas hasta jugar.</summary>
        public void ApplyLampState()
        {
            for (int i = 0; i < bar.lamps.Length && i < BarMischief.LampCount; i++)
            {
                if (!BarMischief.IsLampBroken(i)) continue;
                bar.lamps[i].sprite = lampBroken;
                bar.lamps[i].color = Color.white;
                if (i < bar.lampLights.Length && bar.lampLights[i]) bar.lampLights[i].intensity = 0f;
            }
            bar.RefreshAmbient();
        }

        /// <summary>Expresión según cuántas lleva encima.</summary>
        public static StaffExpression MoodFor(Speaker who)
        {
            int n = BarMischief.Beers(who);
            return n >= 7 ? StaffExpression.Ebrio : StaffExpression.Feliz;
        }

        void Update()
        {
            UpdateTipsy();
            if (!Interactive || _busy || SceneFlow.IsBusy)
            {
                StopPour();
                return;
            }

            var kb = Keyboard.current;
            var world = PixelPointer.World;
            bool overDialogue = DialogueBox.IsBlocking(world);

            // Elegir llave
            int pick = -1;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) pick = 0;
                if (kb.digit2Key.wasPressedThisFrame) pick = 1;
                if (kb.digit3Key.wasPressedThisFrame) pick = 2;
            }
            if (PixelPointer.Pressed && !overDialogue)
                for (int i = 0; i < bar.taps.Length; i++)
                    if (bar.taps[i].Contains(world) && i != _selected) pick = i;
            if (pick >= 0 && pick != _selected && !_pouring) SelectTap(pick);

            // Servir (mantener)
            bool want = (kb != null && kb.spaceKey.isPressed) ||
                        (PixelPointer.Down && !overDialogue && bar.glassHitArea.OverlapPoint(world));
            if (want != _pouring)
            {
                _pouring = want;
                bar.taps[_selected].SetPouring(want);
                bar.glass.SetPouring(want, bar.taps[_selected].NozzlePosition);
            }
            if (_pouring)
            {
                float add = pourRate * Time.deltaTime;
                _liquid += add * 0.75f;
                _foam += add * 0.25f;
                float over = _liquid + _foam - 1f;
                if (over > 0f) _foam -= over;
            }
            else if (_foam > 0f)
            {
                float s = Mathf.Min(_foam, 0.03f * Time.deltaTime);
                _foam -= s;
                _liquid += s * 0.4f;
            }
            bar.glass.SetLevels(_liquid, _foam);

            if (PixelPointer.Pressed && !overDialogue)
            {
                if (_pipeHit.OverlapPoint(world) && !BarMischief.IsDrunk(Speaker.Pipe)) StartCoroutine(Give(Speaker.Pipe));
                else if (_nataHit.OverlapPoint(world) && !BarMischief.IsDrunk(Speaker.Nata)) StartCoroutine(Give(Speaker.Nata));
                for (int i = 0; i < _lampHits.Length; i++)
                    if (_lampHits[i].OverlapPoint(world)) TapLamp(i);
            }
        }

        void StopPour()
        {
            if (!_pouring) return;
            _pouring = false;
            bar.taps[_selected].SetPouring(false);
            bar.glass.SetPouring(false);
        }

        void SelectTap(int i)
        {
            _selected = i;
            for (int t = 0; t < bar.taps.Length; t++) bar.taps[t].SetSelected(t == i);
            var g = bar.glass.transform;
            var nozzle = bar.taps[i].NozzlePosition;
            PixelTween.MoveLocal(this, g, g.parent.InverseTransformPoint(new Vector3(nozzle.x, g.position.y, 0)), 0.12f);
            bar.glass.SetBeer(bar.taps[i].kind);
            bar.glass.Slosh(1f);
            if (_liquid + _foam > 0f)
            {
                _liquid = _foam = 0f; // otra cerveza: vaso nuevo
                AudioManager.Play(Sfx.GlassPlace);
            }
        }

        // ------------------------------------------------------------------ dar cervezas

        IEnumerator Give(Speaker who)
        {
            _busy = true;
            var sr = who == Speaker.Pipe ? bar.pipe : bar.nata;
            if (_liquid + _foam < 0.4f)
            {
                bar.SetExpression(who, StaffExpression.Sorpresa);
                bar.dialogue.Say(who, StaffExpression.Sorpresa,
                    who == Speaker.Pipe ? "¿Y LA CERVEZA? ESO ES PURO VASO." : "¡ESTÁ CASI VACÍO! ¿ME ESTÁS PROBANDO?", 2.2f);
                yield return new WaitForSeconds(0.6f);
                bar.SetExpression(who, MoodFor(who));
                _busy = false;
                yield break;
            }

            var result = BarMischief.Give(who);
            if (result == BarMischief.GiveResult.Refused)
            {
                bar.SetExpression(who, StaffExpression.Sorpresa);
                PixelTween.Punch(this, sr.transform, 0.2f, 0.2f);
                AudioManager.Play(Sfx.UiBack);
                bar.dialogue.Say(who, StaffExpression.Sorpresa, who == Speaker.Pipe
                    ? (Hardcore ? "NO, NO. ALGUIEN TIENE QUE CUIDAR EL ESCENARIO." : "NO, NO. ALGUIEN TIENE QUE ATENDER ESTE BAR.")
                    : (Hardcore ? "¡NI HABLAR! CON PIPE ASÍ, ALGUIEN TIENE QUE PONER LA MÚSICA."
                                : "¡NI HABLAR! CON PIPE ASÍ, ALGUIEN TIENE QUE TRABAJAR."), 2.8f);
                yield return new WaitForSeconds(0.8f);
                bar.SetExpression(who, MoodFor(who));
                _busy = false;
                yield break;
            }

            // Se toma la cerveza
            AudioManager.Play(Sfx.GlassSlide);
            bar.glass.Flash();
            _liquid = _foam = 0f;
            bar.glass.SetLevels(0f, 0f);
            yield return new WaitForSeconds(0.25f);
            AudioManager.Play(Sfx.Gulp, 1f, who == Speaker.Pipe ? 0.9f : 1.15f);
            PixelTween.Punch(this, sr.transform, 0.25f, 0.3f);
            if (FeedbackDirector.Instance) FeedbackDirector.Instance.CustomerHappy(sr.transform.position + Vector3.up * 3f);

            int n = BarMischief.Beers(who);
            if (result == BarMischief.GiveResult.BecameDrunk)
            {
                yield return GetDrunkAndLeave(who);
                _busy = false;
                yield break;
            }
            var mood = MoodFor(who);
            bar.SetExpression(who, mood);
            var lines = Hardcore
                ? (who == Speaker.Pipe ? PipeLinesHardcore : NataLinesHardcore)
                : (who == Speaker.Pipe ? PipeLines : NataLines);
            bar.dialogue.Say(who, mood, lines[Mathf.Clamp(n - 1, 0, lines.Length - 1)], 2.6f);
            if (mood == StaffExpression.Ebrio) AudioManager.Play(Sfx.Hiccup, 1f, who == Speaker.Pipe ? 0.85f : 1.1f);
            yield return new WaitForSeconds(0.5f);
            _busy = false;
        }

        IEnumerator GetDrunkAndLeave(Speaker who)
        {
            var sr = who == Speaker.Pipe ? bar.pipe : bar.nata;
            bar.SetExpression(who, StaffExpression.Ebrio);
            bar.dialogue.Say(who, StaffExpression.Ebrio, who == Speaker.Pipe
                ? (Hardcore ? "ME VOY AL CAMARÍN... NATA, TOCA TÚ EL ÚLTIMO TEMA. *HIP*" : "ZZZ... NATA, TE TOCA SERVIR A TI... *HIP*")
                : (Hardcore ? "ME VOY A DORMIR EN EL AMPLIFICADOR... PIPE, CUBRE LA BARRA. *HIP*"
                            : "ME VOY A ACOSTAR... PIPE, CUBRE LA BARRA. *HIP*"), 0f);
            for (int i = 0; i < 4; i++)
            {
                AudioManager.Play(Sfx.Hiccup, 1f, (who == Speaker.Pipe ? 0.85f : 1.1f) + i * 0.03f);
                PixelTween.Punch(this, sr.transform, 0.3f, 0.25f);
                yield return new WaitForSeconds(0.55f);
            }
            AudioManager.Play(Sfx.Whoosh);
            yield return bar.HideDown(who);
            bar.dialogue.Hide();
            var other = BarMischief.Other(who);
            yield return new WaitForSeconds(0.4f);
            bar.SetExpression(other, StaffExpression.Sorpresa);
            bar.dialogue.Say(other, StaffExpression.Sorpresa, other == Speaker.Pipe
                ? "¿SE FUE NATA? BUENO... HOY ATIENDO YO."
                : "¡PIPE SE QUEDÓ DORMIDO! HOY ATIENDO YO.", 3f);
            yield return new WaitForSeconds(1f);
            bar.SetExpression(other, MoodFor(other));
        }

        /// <summary>Respiración «achispada»: hipo de vez en cuando.</summary>
        void UpdateTipsy()
        {
            bar.pipeTipsy = !BarMischief.IsDrunk(Speaker.Pipe) && BarMischief.Beers(Speaker.Pipe) >= 7;
            bar.nataTipsy = !BarMischief.IsDrunk(Speaker.Nata) && BarMischief.Beers(Speaker.Nata) >= 7;
            if (Time.time < _nextHiccup || _busy) return;
            _nextHiccup = Time.time + Random.Range(4f, 8f);
            if (bar.pipeTipsy) AudioManager.Play(Sfx.Hiccup, 0.6f, 0.85f);
            else if (bar.nataTipsy) AudioManager.Play(Sfx.Hiccup, 0.6f, 1.1f);
        }

        // ------------------------------------------------------------------ ampolletas

        /// <summary>
        /// Cada toque prende o apaga la ampolleta (textura y luz juntas). Muchos toques seguidos la rompen.
        /// </summary>
        void TapLamp(int i)
        {
            if (i >= BarMischief.LampCount || BarMischief.IsLampBroken(i)) return;
            _lampTaps[i] = Time.time - _lastLampTap[i] < 0.9f ? _lampTaps[i] + 1 : 1;
            _lastLampTap[i] = Time.time;
            if (_lampTaps[i] >= tapsToBreakLamp)
            {
                BreakLamp(i);
                return;
            }
            bool on = !bar.IsLampOn(i);
            bar.SetLamp(i, on);
            bar.RefreshAmbient();
            AudioManager.Play(on ? Sfx.LightOn : Sfx.LampFlicker, 0.8f, 1f + _lampTaps[i] * 0.05f);
            PixelTween.Punch(this, bar.lamps[i].transform, 0.12f, 0.15f);
        }

        void BreakLamp(int i)
        {
            bool first = !BarMischief.AnyLampBroken;
            BarMischief.BreakLamp(i);
            if (first) HardcoreActivated?.Invoke();
            AudioManager.Play(Sfx.GlassBreak);
            AudioManager.Duck(0.5f, 0.8f);
            var bulb = bar.lamps[i].transform.position + Vector3.down * 1.7f;
            if (FeedbackDirector.Instance)
            {
                FeedbackDirector.Instance.Sparks(bulb, 16);
                FeedbackDirector.Instance.Puff(bulb, 5);
                if (FeedbackDirector.Instance.cameraShake) FeedbackDirector.Instance.cameraShake.Shake(2, 0.25f);
            }
            ApplyLampState();
            var who = BarMischief.Available(Speaker.Nata);
            if (BarMischief.IsDrunk(who)) return;
            bar.SetExpression(who, StaffExpression.Sorpresa);
            bar.dialogue.Say(who, StaffExpression.Sorpresa, BarMischief.AnyLampBroken && CountBroken() > 1
                ? "¡LA OTRA TAMBIÉN! HOY SE SIRVE A CIEGAS..."
                : "¡EY, ESO SE PAGA! HOY VAMOS A SERVIR A OSCURAS...", 3f);
        }

        static int CountBroken()
        {
            int n = 0;
            for (int i = 0; i < BarMischief.LampCount; i++)
                if (BarMischief.IsLampBroken(i)) n++;
            return n;
        }
    }
}
