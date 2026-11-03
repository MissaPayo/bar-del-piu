using System.Collections.Generic;
using BarDelPiu.Audio;
using BarDelPiu.Core;
using BarDelPiu.Visual;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BarDelPiu.Dev
{
    /// <summary>
    /// Escena de prueba de arte y audio (NO es la lógica final del juego).
    /// Simula el ciclo de servido para validar sprites, shader, partículas y sonidos.
    ///
    /// Controles: 1/2/3 o clic en una manija = elegir llave · mantener Espacio o clic/toque sobre el vaso = servir ·
    /// E o soltar sobre un cliente = entregar · M = música · R = capa rush · D = diálogo · T = línea objetivo.
    /// </summary>
    public class ArtAudioTestController : MonoBehaviour
    {
        public Camera cam;
        public TapVisual[] taps;
        public BeerGlassVisual glass;
        public Collider2D glassHitArea;
        public CustomerVisual[] customers;
        public DialogueBox dialogue;
        public PixelText scoreText;
        public PixelText timerText;
        public PixelText comboText;
        public SpriteRenderer[] errorIcons;
        public Sprite errorEmpty, errorFull;
        [Tooltip("Flecha que marca al cliente más urgente.")]
        public SpriteRenderer urgentArrow;
        [Tooltip("Destello a pantalla completa (aviso del modo Hardcore).")]
        public SpriteRenderer screenFlash;

        [Header("Simulación de servido")]
        public float pourRate = 0.32f;
        [Tooltip("Segundos que aguanta un cliente con la paciencia llena.")]
        public float patienceSeconds = 22f;
        [Tooltip("En modo Hardcore la paciencia se agota este factor más rápido (1.2 = 20 %).")]
        public float hardcorePatienceSpeed = 1.2f;

        [Header("Tiempos justos")]
        [Tooltip("Segundos mínimos entre la llegada de un cliente y la del siguiente (evita oleadas).")]
        public float minArrivalGap = 2.5f;
        [Tooltip("Segundos de gracia: la paciencia no baja apenas llega el cliente.")]
        public float arrivalGrace = 1f;
        [Tooltip("Segundos extra de paciencia por cada cliente que ya está esperando cuando llega uno nuevo.")]
        public float extraPatiencePerWaiting = 2.5f;
        [Tooltip("Mientras se llena la jarra (clientes tapados) la paciencia baja a este ritmo.")]
        public float patienceSpeedWhilePouring = 0.5f;
        [Tooltip("Segundos esperando sin ser atendido antes de resaltar su pedido y su llave.")]
        public float hintAfterSeconds = 4f;
        public float foamSettlePerSecond = 0.02f;
        public float idealLiquid = 0.75f;
        public float idealFoam = 0.2f;

        int _selected;
        float _liquid, _foam;
        bool _pouring;
        readonly BeerKind[] _orders = new BeerKind[4];
        readonly float[] _patience = new float[4];
        readonly bool[] _busy = new bool[4];
        readonly float[] _patienceDur = new float[4];
        readonly float[] _waited = new float[4];
        float _nextArrivalAt;
        readonly bool[] _warned = new bool[3];
        int _score, _combo, _errors;
        float _time = 90f;
        bool _rush, _showTarget = true;
        int _lastTick, _perfects, _hardcoreStreak;
        bool _ended;

        static readonly string[] PipeLines =
        {
            "¡BIENVENIDO! ELIGE LA LLAVE Y MANTÉN PARA SERVIR.",
            "¡ESA ESPUMA PARECE UNA MONTAÑA!",
            "¡BIEN AHÍ! ESE SERVIDO ESTUVO PERFECTO.",
        };

        static readonly string[] NataLines =
        {
            "¡PIPE, SE NOS ESTÁ LLENANDO LA BARRA!",
            "¡VAMOS QUE SALE OTRA RONDA!",
        };

        void Start()
        {
            if (!cam) cam = Camera.main;
            bool hardcore = GameSession.BeginMatch();
            AudioManager.PlayMusic(hardcore ? MusicTrack.Hardcore : MusicTrack.Game);
            AudioManager.SetAmbience(!hardcore); // en Hardcore manda la música pesada, sin murmullo encima
            if (urgentArrow) urgentArrow.enabled = false;
            if (screenFlash) screenFlash.enabled = false;
            for (int i = 0; i < customers.Length; i++) NewCustomer(i, 0.4f);
            SelectTap(0, false);
            glass.SetTargetLevel(idealLiquid + idealFoam, _showTarget);
            if (hardcore) StartCoroutine(HardcoreIntro());
            else if (dialogue) dialogue.Say(BarMischief.Available(Speaker.Pipe), StaffExpression.Feliz, PipeLines[0], 3.5f);
            RefreshHud();
        }

        /// <summary>
        /// Aviso del modo Hardcore: golpe de guitarra, destello rojo, letrero, las luces se cortan
        /// y Nata (o Pipe) explica el multiplicador. Música pesada y clientes punk/metaleros.
        /// </summary>
        System.Collections.IEnumerator HardcoreIntro()
        {
            var bar = FindFirstObjectByType<BarDelPiu.Screens.BarScene>();
            if (bar) bar.SetHardcoreLook(true); // carteles, grafitis, neón HARDCORE y luz roja
            AudioManager.Play(Sfx.HardcoreSting);
            AudioManager.Duck(0.7f, 1.2f);
            if (FeedbackDirector.Instance && FeedbackDirector.Instance.cameraShake) FeedbackDirector.Instance.cameraShake.Shake(3, 0.5f);
            StartCoroutine(Flash(BarPalette.Red, 0.6f));
            FeedbackDirector.Instance.Message(new Vector3(0f, 2.2f, 0f), "¡MODO HARDCORE!", BarPalette.Red, 2);
            // las ampolletas chispean y mueren una por una
            if (bar)
            {
                for (int i = 0; i < bar.lamps.Length; i++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        bar.lampLights[i].intensity = k % 2 == 0 ? 0f : bar.lampOn;
                        yield return new WaitForSeconds(0.06f);
                    }
                    if (bar.lampBroken) bar.lamps[i].sprite = bar.lampBroken;
                    bar.lamps[i].color = Color.white;
                    bar.lampLights[i].intensity = 0f;
                    AudioManager.Play(Sfx.GlassBreak, 0.5f, 1f + i * 0.1f);
                    if (FeedbackDirector.Instance) FeedbackDirector.Instance.Sparks(bar.lamps[i].transform.position + Vector3.down * 1.7f, 10);
                    yield return new WaitForSeconds(0.15f);
                }
                for (int i = bar.lamps.Length; i < bar.lampLights.Length; i++)
                    if (bar.lampLights[i]) bar.lampLights[i].intensity = 0.35f; // solo queda la luz de las llaves
                if (bar.globalLight) bar.globalLight.intensity = 0.22f;
            }
            yield return new WaitForSeconds(0.4f);
            if (dialogue)
                dialogue.Say(BarMischief.Available(Speaker.Nata), StaffExpression.Sorpresa,
                    "¡SIN LUZ! CADA ACIERTO SEGUIDO MULTIPLICA X3. ¡SUERTE!", 3.5f);
        }

        System.Collections.IEnumerator Flash(Color color, float duration)
        {
            if (!screenFlash) yield break;
            screenFlash.enabled = true;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float a = Mathf.Ceil((1f - t / duration) * 4f) / 4f * 0.6f; // fundido en pasos
                screenFlash.color = new Color(color.r, color.g, color.b, a);
                yield return null;
            }
            screenFlash.enabled = false;
        }

        void NewCustomer(int i, float delay)
        {
            _busy[i] = true;
            _orders[i] = (BeerKind)Random.Range(0, 3);
            _patience[i] = 1f;
            _waited[i] = 0f;
            var c = customers[i];
            c.Setup(PickVariant(i), _orders[i], GameSession.Hardcore);
            // Nunca llegan dos clientes casi juntos: se respeta un espacio mínimo entre llegadas
            float at = Mathf.Max(Time.time + delay, _nextArrivalAt);
            _nextArrivalAt = at + minArrivalGap;
            StartCoroutine(ArriveLater(i, at - Time.time));
        }

        readonly int[] _variant = { -1, -1, -1, -1 };

        /// <summary>Elige un cliente distinto a los que ya están en la barra (sin gemelos lado a lado).</summary>
        int PickVariant(int slot)
        {
            int count = GameSession.Hardcore ? 6 : 8;
            for (int tries = 0; tries < 12; tries++)
            {
                int v = Random.Range(0, count);
                if (System.Array.IndexOf(_variant, v) < 0)
                {
                    _variant[slot] = v;
                    return v;
                }
            }
            _variant[slot] = Random.Range(0, count);
            return _variant[slot];
        }

        int WaitingCount()
        {
            int n = 0;
            for (int i = 0; i < customers.Length; i++)
                if (!_busy[i]) n++;
            return n;
        }

        System.Collections.IEnumerator ArriveLater(int i, float delay)
        {
            customers[i].HideBehindCounter();
            yield return new WaitForSeconds(delay);
            customers[i].PlayArrive();
            // Más clientes esperando = un poco más de paciencia para cada nuevo (más justo, no más difícil)
            _patienceDur[i] = patienceSeconds + extraPatiencePerWaiting * Mathf.Min(3, WaitingCount());
            _waited[i] = -arrivalGrace;
            _busy[i] = false;
        }

        void SelectTap(int i, bool sound = true)
        {
            if (_pouring) return;
            bool changed = i != _selected;
            _selected = i;
            for (int t = 0; t < taps.Length; t++) taps[t].SetSelected(t == i, sound && changed);
            var target = taps[i].NozzlePosition;
            if (changed && (_liquid > 0f || _foam > 0f))
            {
                _liquid = _foam = 0f; // vaso nuevo al cambiar de cerveza
                AudioManager.Play(Sfx.GlassPlace);
            }
            glass.SetBeer(taps[i].kind);
            PixelTween.MoveLocal(this, glass.transform,
                glass.transform.parent.InverseTransformPoint(new Vector3(target.x, glass.transform.position.y, 0f)), 0.12f);
            glass.Slosh(1f);
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var touch = Touchscreen.current;

            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) SelectTap(0);
                if (kb.digit2Key.wasPressedThisFrame) SelectTap(1);
                if (kb.digit3Key.wasPressedThisFrame) SelectTap(2);
                if (kb.escapeKey.wasPressedThisFrame) SceneFlow.Load(GameSession.MenuScene);
                if (kb.eKey.wasPressedThisFrame) Deliver(FirstWaitingFor(taps[_selected].kind));
                if (kb.mKey.wasPressedThisFrame) AudioManager.PlayMusic(Random.value < 0.5f ? MusicTrack.Menu : MusicTrack.Game);
                if (kb.rKey.wasPressedThisFrame) AudioManager.SetMusicIntensity((_rush = !_rush) ? 1f : 0f);
                if (kb.tKey.wasPressedThisFrame) glass.SetTargetLevel(idealLiquid + idealFoam, _showTarget = !_showTarget);
                if (kb.dKey.wasPressedThisFrame && dialogue)
                {
                    bool pipe = Random.value < 0.5f;
                    var lines = pipe ? PipeLines : NataLines;
                    dialogue.Say(BarMischief.Available(pipe ? Speaker.Pipe : Speaker.Nata),
                        (StaffExpression)Random.Range(0, 3), lines[Random.Range(0, lines.Length)], 2.5f);
                }
            }

            // Puntero (mouse o primer toque)
            bool pressed = false, down = false, released = false;
            Vector2 screen = default;
            if (touch != null && touch.primaryTouch.press.isPressed || touch != null && touch.primaryTouch.press.wasReleasedThisFrame)
            {
                screen = touch.primaryTouch.position.ReadValue();
                pressed = touch.primaryTouch.press.wasPressedThisFrame;
                down = touch.primaryTouch.press.isPressed;
                released = touch.primaryTouch.press.wasReleasedThisFrame;
            }
            else if (mouse != null)
            {
                screen = mouse.position.ReadValue();
                pressed = mouse.leftButton.wasPressedThisFrame;
                down = mouse.leftButton.isPressed;
                released = mouse.leftButton.wasReleasedThisFrame;
            }
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));

            if (pressed)
            {
                for (int t = 0; t < taps.Length; t++)
                    if (taps[t].Contains(world) && t != _selected) SelectTap(t);
            }
            bool pointerPour = down && glassHitArea && glassHitArea.OverlapPoint(world);
            if (released && !pointerPour)
            {
                int hit = CustomerAt(world);
                if (hit >= 0) Deliver(hit);
            }

            bool wantPour = (kb != null && kb.spaceKey.isPressed) || pointerPour;
            if (wantPour != _pouring)
            {
                _pouring = wantPour;
                taps[_selected].SetPouring(wantPour);
                glass.SetPouring(wantPour, taps[_selected].NozzlePosition);
            }

            SimulateGlass(Time.deltaTime);
            SimulateCustomers(Time.deltaTime);
            SimulateTimer(Time.deltaTime);
        }

        void SimulateGlass(float dt)
        {
            if (_pouring)
            {
                float foamShare = taps[_selected].kind == BeerKind.Negra ? 0.32f : taps[_selected].kind == BeerKind.Ambar ? 0.22f : 0.26f;
                float add = pourRate * dt;
                _liquid += add * (1f - foamShare);
                _foam += add * foamShare;
                float over = _liquid + _foam - 1f;
                if (over > 0f) _foam = Mathf.Max(0f, _foam - over); // rebalsa la espuma
            }
            else if (_foam > 0f)
            {
                float settle = Mathf.Min(_foam, foamSettlePerSecond * dt);
                _foam -= settle;
                _liquid += settle * 0.4f;
            }
            glass.SetLevels(_liquid, _foam);
        }

        void SimulateCustomers(float dt)
        {
            for (int i = 0; i < customers.Length; i++)
            {
                if (_busy[i]) continue;
                _waited[i] += dt;
                if (_waited[i] < 0f) continue; // gracia de llegada
                float speed = GameSession.Hardcore ? hardcorePatienceSpeed : 1f;
                if (_pouring) speed *= patienceSpeedWhilePouring; // clientes tapados: baja más lento
                float dur = _patienceDur[i] > 0f ? _patienceDur[i] : patienceSeconds;
                _patience[i] -= dt * speed / dur;
                customers[i].SetPatience(_patience[i]);
                if (_patience[i] <= 0f)
                {
                    _busy[i] = true;
                    AddError(customers[i].transform.position + Vector3.up * 3f, "¡SE FUE!");
                    int idx = i;
                    customers[i].PlayLeave(false, () => NewCustomer(idx, 1.2f));
                }
            }
            UpdateHints();
        }

        /// <summary>
        /// Resaltado: quien lleva más de 4 s esperando muestra contorno en su pedido y en la llave de su cerveza.
        /// El más urgente (el que tiene menos tiempo restante) además late y lleva una flecha.
        /// </summary>
        void UpdateHints()
        {
            int urgent = -1;
            float urgentLeft = float.MaxValue;
            for (int i = 0; i < customers.Length; i++)
            {
                if (_busy[i] || _waited[i] < hintAfterSeconds) continue;
                float left = _patience[i] * (_patienceDur[i] > 0f ? _patienceDur[i] : patienceSeconds);
                if (left < urgentLeft)
                {
                    urgentLeft = left;
                    urgent = i;
                }
            }
            var tapHint = new int[taps.Length];
            for (int i = 0; i < customers.Length; i++)
            {
                int level = _busy[i] || _waited[i] < hintAfterSeconds ? 0 : i == urgent ? 2 : 1;
                customers[i].SetHint(level);
                if (level == 0) continue;
                int t = (int)_orders[i];
                if (t < tapHint.Length) tapHint[t] = Mathf.Max(tapHint[t], level);
            }
            for (int t = 0; t < taps.Length; t++) taps[t].SetHint(tapHint[t]);

            if (urgentArrow)
            {
                urgentArrow.enabled = urgent >= 0 && !_pouring;
                if (urgent >= 0)
                {
                    float bounce = Mathf.Round(Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 3f);
                    urgentArrow.transform.position = Px.Snap(customers[urgent].orderBubble.transform.position +
                                                             new Vector3(0.35f, 1.65f + bounce * Px.Unit, 0f));
                }
            }
        }

        void SimulateTimer(float dt)
        {
            _time = Mathf.Max(0f, _time - dt);
            int sec = Mathf.CeilToInt(_time);
            if (sec <= 10 && sec != _lastTick && sec > 0) AudioManager.Play(Sfx.TimerTick);
            _lastTick = sec;
            CheckTimeWarning(60, 0, BarPalette.AmberLight);
            CheckTimeWarning(30, 1, BarPalette.Neon);
            CheckTimeWarning(10, 2, BarPalette.Red);
            if (_time <= 20f && !_rush)
            {
                _rush = true;
                AudioManager.SetMusicIntensity(1f);
            }
            if (_time <= 0f) EndGame();
            RefreshHud();
        }

        /// <summary>Aviso al quedar 60, 30 y 10 segundos: letrero, campanadas y el reloj late (normal y Hardcore).</summary>
        void CheckTimeWarning(int seconds, int slot, Color color)
        {
            if (_warned[slot] || _time > seconds) return;
            _warned[slot] = true;
            AudioManager.Play(Sfx.TimeWarning);
            AudioManager.Duck(0.4f, 0.8f);
            FeedbackDirector.Instance.Message(new Vector3(0f, 1.4f, 0f), "¡QUEDAN " + seconds + " SEGUNDOS!", color, 2);
            if (timerText) StartCoroutine(PulseTimer(color, seconds <= 10 ? 6 : 3));
            if (seconds <= 10 && FeedbackDirector.Instance.cameraShake) FeedbackDirector.Instance.cameraShake.Shake(1, 0.3f);
            if (seconds <= 30) AudioManager.SetMusicIntensity(1f);
        }

        System.Collections.IEnumerator PulseTimer(Color color, int beats)
        {
            for (int b = 0; b < beats; b++)
            {
                PixelTween.Punch(this, timerText.transform, 0.35f, 0.2f);
                _timerFlash = color;
                yield return new WaitForSeconds(0.15f);
                _timerFlash = default;
                yield return new WaitForSeconds(0.15f);
            }
        }

        Color _timerFlash;

        int FirstWaitingFor(BeerKind kind)
        {
            int any = -1;
            for (int i = 0; i < customers.Length; i++)
            {
                if (_busy[i]) continue;
                if (_orders[i] == kind) return i;
                if (any < 0) any = i;
            }
            return any;
        }

        int CustomerAt(Vector3 world)
        {
            for (int i = 0; i < customers.Length; i++)
            {
                var p = customers[i].transform.position;
                if (!_busy[i] && Mathf.Abs(world.x - p.x) < 1f && world.y > p.y && world.y < p.y + 4f) return i;
            }
            return -1;
        }

        void Deliver(int i)
        {
            if (i < 0 || _busy[i] || _liquid + _foam < 0.05f || _pouring) return;
            var pos = customers[i].transform.position + Vector3.up * 3.2f;
            AudioManager.Play(Sfx.GlassSlide);
            glass.Flash();

            if (_orders[i] != taps[_selected].kind)
            {
                AddError(pos, "¡ESA NO ERA!");
                _combo = 0;
                ResetGlass();
                return;
            }

            float acc = 1f - (Mathf.Abs(_liquid - idealLiquid) + Mathf.Abs(_foam - idealFoam)) * 1.6f;
            var grade = acc >= 0.95f ? ServeGrade.Perfecto : acc >= 0.8f ? ServeGrade.Bueno : acc >= 0.6f ? ServeGrade.Regular : ServeGrade.Malo;
            int basePts = grade switch { ServeGrade.Perfecto => 100, ServeGrade.Bueno => 70, ServeGrade.Regular => 40, _ => 10 };
            _combo = grade == ServeGrade.Perfecto ? Mathf.Min(_combo + 1, 20) : 0;
            if (grade == ServeGrade.Perfecto) _perfects++;
            int pts = basePts * Mathf.Max(1, _combo);
            if (GameSession.Hardcore)
            {
                // Hardcore: cada cerveza correcta seguida multiplica por 3 (x3, x9, x27...)
                _hardcoreStreak = grade == ServeGrade.Malo ? 0 : _hardcoreStreak + 1;
                pts = basePts * GameSession.HardcoreMultiplier(_hardcoreStreak);
            }
            _score += pts;

            var fd = FeedbackDirector.Instance;
            fd.ServeResult(grade, pos, pts, _combo);
            if (GameSession.Hardcore && _hardcoreStreak > 0)
                fd.Message(pos + Vector3.up * 1.6f, "X" + GameSession.HardcoreMultiplier(_hardcoreStreak), BarPalette.Red, 2);
            if (grade == ServeGrade.Malo) AddError(pos + Vector3.up, null);

            _busy[i] = true;
            customers[i].PlayServed();
            int idx = i;
            bool tip = grade <= ServeGrade.Bueno;
            StartCoroutine(LeaveLater(idx, tip, pos));
            if (grade == ServeGrade.Perfecto && _combo == 3 && dialogue)
                dialogue.Say(BarMischief.Available(Speaker.Pipe), StaffExpression.Feliz, PipeLines[2], 2.5f);
            ResetGlass();
        }

        System.Collections.IEnumerator LeaveLater(int i, bool tip, Vector3 pos)
        {
            yield return new WaitForSeconds(0.5f);
            if (tip)
            {
                FeedbackDirector.Instance.Tip(pos + Vector3.down * 1.2f, Random.Range(2, 6) * 10);
                FeedbackDirector.Instance.CustomerHappy(pos);
            }
            customers[i].PlayLeave(true, () => NewCustomer(i, 0.8f));
        }

        void ResetGlass()
        {
            _liquid = _foam = 0f;
            glass.SetLevels(0f, 0f);
            glass.Slosh(1.5f);
            AudioManager.Play(Sfx.GlassPlace);
        }

        void AddError(Vector3 pos, string label)
        {
            _hardcoreStreak = 0;
            _errors++;
            _combo = 0;
            if (label != null) FeedbackDirector.Instance.Error(pos, label);
            if (_errors >= 3) EndGame();
            RefreshHud();
        }

        void EndGame()
        {
            if (_ended) return;
            _ended = true;
            enabled = false;
            if (_pouring) glass.SetPouring(false);
            AudioManager.Play(Sfx.GameOver);
            AudioManager.SetMusicIntensity(0f);
            GameSession.FinishGame(_score, _perfects);
        }

        void RefreshHud()
        {
            if (scoreText) scoreText.Set(_score.ToString("000000"));
            if (timerText)
            {
                int s = Mathf.CeilToInt(_time);
                timerText.Set($"{s / 60}:{s % 60:00}");
                timerText.SetColor(_timerFlash.a > 0f ? _timerFlash
                    : s <= 10 ? BarPalette.Red : s <= 30 ? BarPalette.Neon : s <= 60 ? BarPalette.AmberLight : BarPalette.Cream);
            }
            if (comboText)
                comboText.Set(GameSession.Hardcore
                    ? (_hardcoreStreak > 0 ? "X" + GameSession.HardcoreMultiplier(_hardcoreStreak) : "HARDCORE")
                    : _combo >= 2 ? "X" + _combo : "");
            if (errorIcons != null)
                for (int i = 0; i < errorIcons.Length; i++)
                    errorIcons[i].sprite = i < _errors ? errorFull : errorEmpty;
        }
    }
}
