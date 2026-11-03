using System;
using System.Collections;
using System.Collections.Generic;
using BarDelPiu.Audio;
using BarDelPiu.UI;
using BarDelPiu.Visual;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace BarDelPiu.Screens
{
    /// <summary>
    /// Sorpresa secreta: ¡Feliz cumpleaños Pipe y Nata! Se abre tocando 5 veces la versión en Ajustes.
    /// Se apagan las luces, llega una torta por la barra, Pipe y Nata con gorros de fiesta,
    /// confeti, canción de cumpleaños y hay que tocar la torta para soplar las velas.
    /// </summary>
    public class BirthdayEasterEgg : MonoBehaviour
    {
        UiSkin _skin;
        BarScene _bar;
        Action _onDone;
        readonly List<GameObject> _spawned = new List<GameObject>();
        SpriteRenderer[] _flames;
        Light2D[] _candleLights;
        PixelText _line1, _line2, _hint;
        ParticleSystem _confetti;
        BoxCollider2D _cakeHit;
        bool _blown, _running;

        public bool IsRunning => _running;

        public static BirthdayEasterEgg Create(Transform parent, UiSkin skin, BarScene bar, Action onDone)
        {
            var go = new GameObject("BirthdayEasterEgg");
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<BirthdayEasterEgg>();
            e._skin = skin;
            e._bar = bar;
            e._onDone = onDone;
            return e;
        }

        public void Play()
        {
            if (_running) return;
            _running = true;
            _blown = false;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            float u = Px.Unit;
            AudioManager.PlayMusic(MusicTrack.None);
            AudioManager.Play(Sfx.LightOn);
            _bar.dialogue.Hide();

            // Se apaga el bar
            for (int i = 0; i < _bar.lampLights.Length; i++) _bar.SetLamp(i, false);
            float g0 = _bar.globalLight.intensity;
            yield return Fade(g0, 0.25f, 0.4f);
            yield return new WaitForSeconds(0.6f);

            // Llega la torta deslizándose por la barra
            var cake = Sprite("Cake", _skin.cake, Scr(352, 50), SortOrder.Props + 20);
            _cakeHit = cake.gameObject.AddComponent<BoxCollider2D>();
            _cakeHit.size = new Vector2(52, 44) * u; // grande para tocar en celular
            _cakeHit.offset = new Vector2(0, 16) * u;
            _cakeHit.isTrigger = true;
            _flames = new SpriteRenderer[2];
            _candleLights = new Light2D[2];
            int[] candleX = { -4, 5 };
            for (int i = 0; i < 2; i++)
            {
                _flames[i] = Sprite("Flame", _skin.candleFlame[0], Vector3.zero, SortOrder.Props + 21, cake.transform);
                _flames[i].sharedMaterial = _skin.unlit; // la llama brilla aunque el bar esté a oscuras
                _flames[i].transform.localPosition = new Vector3(candleX[i] * u, 25 * u, 0);
                var l = new GameObject("CandleLight").AddComponent<Light2D>();
                l.transform.SetParent(_flames[i].transform, false);
                l.lightType = Light2D.LightType.Point;
                l.color = new Color(1f, 0.75f, 0.4f);
                l.intensity = 0.9f;
                l.pointLightOuterRadius = 3.5f;
                l.pointLightInnerRadius = 0.3f;
                _candleLights[i] = l;
            }
            AudioManager.Play(Sfx.GlassSlide);
            yield return PixelTween.MoveLocal(this, cake.transform, Scr(160, 50), 0.9f, PixelTween.EaseOutCubic);
            AudioManager.Play(Sfx.GlassPlace);
            PixelTween.Punch(this, cake.transform, 0.2f, 0.25f);
            StartCoroutine(FlickerFlames());

            // Pipe y Nata asoman con gorros
            AddHat(_bar.pipe, _skin.hatRed);
            AddHat(_bar.nata, _skin.hatMint);
            _bar.PopUp(Speaker.Pipe, StaffExpression.Sorpresa);
            yield return _bar.PopUp(Speaker.Nata, StaffExpression.Sorpresa);

            // ¡Sorpresa!
            bool hardcore = Core.BarMischief.AnyLampBroken;
            AudioManager.Play(hardcore ? Sfx.HardcoreSting : Sfx.PartyHorn);
            AudioManager.PlayMusic(hardcore ? MusicTrack.BirthdayMetal : MusicTrack.Birthday);
            if (hardcore) StartCoroutine(FillTheBar()); // en Hardcore: el bar se llena de punks que cabecean
            _confetti = FxBuilder.Build(FxKind.Confetti, new[] { _skin.confetti }, _skin.unlit);
            _confetti.transform.SetParent(transform, false);
            _spawned.Add(_confetti.gameObject);
            Confetti(Scr(60, 40), 70);
            Confetti(Scr(260, 40), 70);
            _line1 = Text("Line1", Scr(160, 146), "¡FELIZ CUMPLEAÑOS!", 2);
            _line2 = Text("Line2", Scr(160, 124), "PIPE Y NATA", 2);
            PixelTween.Punch(this, _line1.transform, 0.3f, 0.3f);
            StartCoroutine(Rainbow());
            yield return new WaitForSeconds(0.8f);

            yield return Say(Speaker.Nata, StaffExpression.Sorpresa, "¿¡QUÉ!? ¿TODO ESTO ES PARA NOSOTROS?");
            _bar.SetExpression(Speaker.Nata, StaffExpression.Feliz);
            yield return Say(Speaker.Pipe, StaffExpression.Feliz, "¡HAY QUE SOPLAR LAS VELAS!");

            // Soplar las velas
            _hint = Text("Hint", Scr(160, 102), "TOCA LA TORTA PARA SOPLAR", 1);
            float wait = 0f;
            while (!_blown && wait < 25f)
            {
                wait += Time.deltaTime;
                var kb = Keyboard.current;
                bool tap = PixelPointer.Pressed && _cakeHit.OverlapPoint(PixelPointer.World);
                bool key = kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
                if (tap || key) _blown = true;
                _hint.SetAlpha(Mathf.Repeat(Time.time * 2f, 1f) < 0.6f ? 1f : 0.3f);
                yield return null;
            }
            _hint.gameObject.SetActive(false);
            AudioManager.Play(Sfx.CandleBlow);
            for (int i = 0; i < 2; i++)
            {
                _flames[i].enabled = false;
                _candleLights[i].intensity = 0f;
                if (FeedbackDirector.Instance) FeedbackDirector.Instance.Puff(_flames[i].transform.position, 3);
            }
            yield return new WaitForSeconds(0.5f);

            // ¡Celebración!
            AudioManager.Play(Sfx.Cheer);
            AudioManager.Play(Sfx.PartyHorn, 1f, 1.12f);
            Confetti(Scr(40, 30), 120);
            Confetti(Scr(160, 30), 120);
            Confetti(Scr(280, 30), 120);
            if (FeedbackDirector.Instance) FeedbackDirector.Instance.CustomerHappy(Scr(160, 90));
            for (int i = 0; i < _bar.lampLights.Length; i++)
            {
                _bar.SetLamp(i, true);
                AudioManager.Play(Sfx.LightOn, 0.6f);
                yield return new WaitForSeconds(0.12f);
            }
            yield return Fade(_bar.globalLight.intensity, _bar.globalOn, 0.5f);
            _bar.SetExpression(Speaker.Pipe, StaffExpression.Feliz);
            _bar.SetExpression(Speaker.Nata, StaffExpression.Feliz);
            yield return Say(Speaker.Nata, StaffExpression.Feliz, "¡GRACIAS! ¡QUE SIGA LA FIESTA EN EL PIU!");
            yield return Say(Speaker.Pipe, StaffExpression.Feliz, "¡SALUD POR OTRO AÑO MÁS DETRÁS DE LA BARRA!");

            var back = PixelButton.Create(transform, _skin, "VOLVER", new Vector2Int(64, 16), Scr(160, 102), Finish, SortOrder.Overlay + 20);
            back.clickSound = Sfx.UiBack;
            back.Focus();
            _spawned.Add(back.gameObject);
        }

        /// <summary>Público punk/metalero que llena la barra y cabecea durante la canción.</summary>
        IEnumerator FillTheBar()
        {
            for (int i = 0; i < _bar.customers.Length; i++)
            {
                var c = _bar.customers[i];
                c.Setup(UnityEngine.Random.Range(0, 6), BeerKind.Negra, true);
                c.SetMood(Mood.Feliz);
                c.headbang = true;
                c.PlayArrive(false);
                yield return new WaitForSeconds(0.18f);
            }
        }

        void ClearCrowd()
        {
            foreach (var c in _bar.customers)
            {
                if (!c) continue;
                c.headbang = false;
                c.HideBehindCounter();
            }
        }

        void Finish()
        {
            StopAllCoroutines();
            ClearCrowd();
            foreach (var go in _spawned)
                if (go) Destroy(go);
            _spawned.Clear();
            RemoveHat(_bar.pipe);
            RemoveHat(_bar.nata);
            _bar.dialogue.Hide();
            _bar.LightsOnInstant();
            _running = false;
            AudioManager.PlayMusic(MusicTrack.Menu);
            _onDone?.Invoke();
        }

        void Update()
        {
            if (_running && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Finish();
        }

        IEnumerator Say(Speaker who, StaffExpression e, string line)
        {
            bool done = false;
            _bar.dialogue.Say(who, e, line, 0f, () => done = true);
            while (!done)
            {
                if (PixelPointer.Pressed) _bar.dialogue.Skip();
                yield return null;
            }
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator Fade(float from, float to, float time)
        {
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                _bar.globalLight.intensity = Mathf.Lerp(from, to, t / time);
                yield return null;
            }
            _bar.globalLight.intensity = to;
        }

        IEnumerator FlickerFlames()
        {
            int frame = 0;
            while (!_blown)
            {
                frame++;
                for (int i = 0; i < _flames.Length; i++)
                {
                    _flames[i].sprite = _skin.candleFlame[(frame + i) % _skin.candleFlame.Length];
                    _candleLights[i].intensity = 0.8f + UnityEngine.Random.Range(-0.15f, 0.15f);
                }
                yield return new WaitForSeconds(0.12f);
            }
        }

        IEnumerator Rainbow()
        {
            Color[] cols = { BarPalette.AmberLight, BarPalette.Neon, BarPalette.Mint, BarPalette.Cream };
            int i = 0;
            while (_line1)
            {
                _line1.SetColor(cols[i % cols.Length]);
                _line2.SetColor(cols[(i + 2) % cols.Length]);
                i++;
                yield return new WaitForSeconds(0.18f);
            }
        }

        void Confetti(Vector3 pos, int count)
        {
            _confetti.transform.position = pos;
            _confetti.Emit(count);
        }

        void AddHat(SpriteRenderer bust, Sprite hat)
        {
            if (!bust) return;
            RemoveHat(bust);
            var h = Sprite("PartyHat", hat, Vector3.zero, bust.sortingOrder + 1, bust.transform);
            h.transform.localPosition = new Vector3(-1 * Px.Unit, 40 * Px.Unit, 0);
        }

        static void RemoveHat(SpriteRenderer bust)
        {
            if (!bust) return;
            var h = bust.transform.Find("PartyHat");
            if (h) Destroy(h.gameObject);
        }

        SpriteRenderer Sprite(string n, Sprite s, Vector3 pos, int order, Transform parent = null)
        {
            var sr = new GameObject(n).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent ? parent : transform, false);
            sr.transform.localPosition = pos;
            sr.sprite = s;
            sr.sortingOrder = order;
            if (!parent) _spawned.Add(sr.gameObject);
            return sr;
        }

        PixelText Text(string n, Vector3 pos, string text, int scale)
        {
            var t = new GameObject(n).AddComponent<PixelText>();
            t.transform.SetParent(transform, false);
            t.transform.localPosition = pos;
            t.font = _skin.font;
            t.material = _skin.unlit;
            t.text = text;
            t.scale = scale;
            t.color = BarPalette.AmberLight;
            t.alignment = PixelText.Align.Center;
            t.sortingOrder = SortOrder.Overlay + 5;
            _spawned.Add(t.gameObject);
            return t;
        }

        static Vector3 Scr(float px, float py) => new Vector3((px - 160f) / 16f, (py - 90f) / 16f, 0f);
    }
}
