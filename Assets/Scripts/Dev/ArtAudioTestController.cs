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

        [Header("Simulación de servido")]
        public float pourRate = 0.32f;
        [Tooltip("Segundos que aguanta un cliente con la paciencia llena.")]
        public float patienceSeconds = 22f;
        [Tooltip("En modo Hardcore la paciencia se agota este factor más rápido (1.2 = 20 %).")]
        public float hardcorePatienceSpeed = 1.2f;
        public float foamSettlePerSecond = 0.02f;
        public float idealLiquid = 0.75f;
        public float idealFoam = 0.2f;

        int _selected;
        float _liquid, _foam;
        bool _pouring;
        readonly BeerKind[] _orders = new BeerKind[4];
        readonly float[] _patience = new float[4];
        readonly bool[] _busy = new bool[4];
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
            AudioManager.PlayMusic(MusicTrack.Game);
            AudioManager.SetAmbience(true);
            for (int i = 0; i < customers.Length; i++) NewCustomer(i, 0.4f * i);
            SelectTap(0, false);
            glass.SetTargetLevel(idealLiquid + idealFoam, _showTarget);
            if (GameSession.BeginMatch()) StartHardcore();
            else if (dialogue) dialogue.Say(BarMischief.Available(Speaker.Pipe), StaffExpression.Feliz, PipeLines[0], 3.5f);
            RefreshHud();
        }

        /// <summary>Modo Hardcore: ampolletas rotas en el menú → bar a oscuras y multiplicador por potencias de 3.</summary>
        void StartHardcore()
        {
            var bar = FindFirstObjectByType<BarDelPiu.Screens.BarScene>();
            if (bar)
            {
                for (int i = 0; i < bar.lamps.Length; i++)
                {
                    if (bar.lampBroken) bar.lamps[i].sprite = bar.lampBroken;
                    bar.lamps[i].color = Color.white;
                }
                for (int i = 0; i < bar.lampLights.Length; i++)
                    if (bar.lampLights[i]) bar.lampLights[i].intensity = i == bar.lampLights.Length - 1 ? 0.35f : 0f; // solo queda la luz de las llaves
                if (bar.globalLight) bar.globalLight.intensity = 0.22f;
            }
            AudioManager.Play(Sfx.GlassBreak, 0.6f);
            FeedbackDirector.Instance.Message(new Vector3(0f, 2.5f, 0f), "¡MODO HARDCORE!", BarPalette.Red, 2);
            if (dialogue)
                dialogue.Say(BarMischief.Available(Speaker.Nata), StaffExpression.Sorpresa,
                    "¡SIN LUZ! CADA ACIERTO SEGUIDO MULTIPLICA X3. ¡SUERTE!", 3.5f);
        }

        void NewCustomer(int i, float delay)
        {
            _busy[i] = true;
            _orders[i] = (BeerKind)Random.Range(0, 3);
            _patience[i] = 1f;
            var c = customers[i];
            c.Setup(Random.Range(0, 8), _orders[i]);
            StartCoroutine(ArriveLater(i, delay));
        }

        System.Collections.IEnumerator ArriveLater(int i, float delay)
        {
            customers[i].HideBehindCounter();
            yield return new WaitForSeconds(delay);
            customers[i].PlayArrive();
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
                float speed = GameSession.Hardcore ? hardcorePatienceSpeed : 1f;
                _patience[i] -= dt * speed / patienceSeconds;
                customers[i].SetPatience(_patience[i]);
                if (_patience[i] <= 0f)
                {
                    _busy[i] = true;
                    AddError(customers[i].transform.position + Vector3.up * 3f, "¡SE FUE!");
                    int idx = i;
                    customers[i].PlayLeave(false, () => NewCustomer(idx, 1.2f));
                }
            }
        }

        void SimulateTimer(float dt)
        {
            _time = Mathf.Max(0f, _time - dt);
            int sec = Mathf.CeilToInt(_time);
            if (sec <= 10 && sec != _lastTick && sec > 0) AudioManager.Play(Sfx.TimerTick);
            _lastTick = sec;
            if (_time <= 20f && !_rush)
            {
                _rush = true;
                AudioManager.SetMusicIntensity(1f);
            }
            if (_time <= 0f) EndGame();
            RefreshHud();
        }

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
                timerText.color = s <= 10 ? BarPalette.Red : BarPalette.Cream;
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
