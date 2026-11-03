using System.Collections;
using BarDelPiu.Audio;
using BarDelPiu.Core;
using BarDelPiu.Leaderboard;
using BarDelPiu.UI;
using BarDelPiu.Visual;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BarDelPiu.Screens
{
    /// <summary>
    /// Menú principal: el logo cae con rebote, Pipe y Nata asoman detrás de la barra y aparecen
    /// Jugar, Instrucciones, Ranking, Ajustes y Créditos. Muestra el récord y el botón de sonido.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public BarScene bar;
        public UiSkin skin;
        public SpriteRenderer logo;
        public PixelText recordText;
        public LeaderboardView leaderboard;
        public MenuPlayground playground;
        [Tooltip("Sello «MODO HARDCORE» que aparece junto a JUGAR cuando hay una ampolleta rota.")]
        public SpriteRenderer hardcoreStamp;
        public IdleWatcher idleWatcher;

        enum Overlay { None, Ranking, Settings, Credits, Birthday }

        Overlay _overlay = Overlay.None;
        PixelButton _play, _howTo, _ranking, _settingsBtn, _creditsBtn, _rankingBack;
        PixelButton[] _menuButtons;
        SettingsPanel _settings;
        CreditsRoll _credits;
        BirthdayEasterEgg _birthday;
        Vector3 _logoHome;

        static readonly string[] Quips =
        {
            "¡VAMOS QUE SALE OTRA RONDA!",
            "¡LA BARRA TE ESPERA!",
            "UNA BUENA CERVEZA NO SE SIRVE SOLA.",
        };

        IEnumerator Start()
        {
            AudioManager.PlayMusic(MenuMusic);
            AudioManager.SetMusicIntensity(0f);
            AudioManager.SetAmbience(!BarMischief.AnyLampBroken); // en Hardcore solo la música pesada
            bar.LightsOnInstant();
            bar.HideStaff();
            foreach (var c in bar.customers) c.HideBehindCounter();

            float u = Px.Unit;
            _play = PixelButton.Create(transform, skin, "JUGAR", new Vector2Int(96, 18), Scr(160, 98), GameSession.Play);
            _play.labelColor = BarPalette.GreenDark;
            _howTo = PixelButton.Create(transform, skin, "INSTRUCCIONES", new Vector2Int(96, 16), Scr(160, 79), () =>
            {
                GameSession.TutorialFromMenu = true;
                SceneFlow.Load(GameSession.IntroScene);
            });
            _ranking = PixelButton.Create(transform, skin, "RANKING", new Vector2Int(96, 16), Scr(160, 61), OpenRanking);
            _settingsBtn = PixelButton.Create(transform, skin, "AJUSTES", new Vector2Int(56, 16), Scr(214, 42), OpenSettings);
            _creditsBtn = PixelButton.Create(transform, skin, "CRÉDITOS", new Vector2Int(56, 16), Scr(274, 42), OpenCredits);
            _menuButtons = new[] { _play, _howTo, _ranking, _settingsBtn, _creditsBtn };

            _rankingBack = PixelButton.Create(transform, skin, "VOLVER", new Vector2Int(64, 16), Scr(160, 12), CloseOverlay,
                SortOrder.Overlay + 20);
            _rankingBack.clickSound = Sfx.UiBack;
            _rankingBack.gameObject.SetActive(false);
            leaderboard.gameObject.SetActive(false);

            _settings = SettingsPanel.Create(transform, skin, CloseOverlay, OpenCredits, StartBirthday);
            _credits = CreditsRoll.Create(transform, skin, CloseOverlay);
            _birthday = BirthdayEasterEgg.Create(transform, skin, bar, CloseOverlay);

            RefreshRecord();
            ApplyHardcoreLook();
            if (playground) playground.HardcoreActivated += OnHardcoreActivated;

            // Entrada animada
            foreach (var b in _menuButtons) b.gameObject.SetActive(false);
            recordText.gameObject.SetActive(false);
            _logoHome = logo.transform.localPosition;
            logo.transform.localPosition = _logoHome + Vector3.up * 80 * u;

            yield return new WaitForSeconds(0.3f);
            AudioManager.Play(Sfx.Whoosh);
            yield return PixelTween.MoveLocal(this, logo.transform, _logoHome, 0.45f, PixelTween.EaseOutBack);
            AudioManager.Play(Sfx.GlassPlace);
            PixelTween.Punch(this, logo.transform, 0.15f, 0.25f);

            foreach (var b in _menuButtons)
            {
                if (_overlay != Overlay.None) break;
                b.gameObject.SetActive(true);
                var home = b.transform.localPosition;
                b.transform.localPosition = home + Vector3.down * 12 * u;
                PixelTween.MoveLocal(this, b.transform, home, 0.2f, PixelTween.EaseOutBack);
                AudioManager.Play(Sfx.Pop, 0.5f, 1.2f);
                yield return new WaitForSeconds(0.07f);
            }
            if (_overlay == Overlay.None)
            {
                recordText.gameObject.SetActive(true);
                _play.Focus();
            }

            // Pipe y Nata asoman (el que está ebrio no vuelve hasta jugar una partida)
            yield return new WaitForSeconds(0.3f);
            foreach (var who in new[] { Speaker.Pipe, Speaker.Nata })
            {
                if (BarMischief.IsDrunk(who)) continue;
                yield return bar.PopUp(who, MenuPlayground.MoodFor(who));
                yield return new WaitForSeconds(0.15f);
            }
            StartCoroutine(StaffBanter());
            if (BarMischief.AnyLampBroken && hardcoreStamp) StartCoroutine(StampPulse());
        }

        /// <summary>Pipe y Nata cambian de expresión de vez en cuando y dicen algo.</summary>
        IEnumerator StaffBanter()
        {
            int i = 0;
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(7f, 11f));
                if (_overlay != Overlay.None || bar.dialogue.IsShowing) continue;
                var who = BarMischief.Available(i % 2 == 0 ? Speaker.Nata : Speaker.Pipe);
                if (BarMischief.IsDrunk(who)) continue;
                var mood = MenuPlayground.MoodFor(who);
                bar.SetExpression(who, StaffExpression.Sorpresa);
                yield return new WaitForSeconds(0.4f);
                if (_overlay != Overlay.None || bar.dialogue.IsShowing) continue;
                bar.SetExpression(who, mood);
                bar.dialogue.Say(who, mood, Quips[i % Quips.Length], 2.5f);
                i++;
            }
        }

        void Update()
        {
            if (_overlay != Overlay.Birthday) PixelButton.UpdateKeyboardNavigation();
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && (_overlay == Overlay.Ranking || _overlay == Overlay.Settings))
                CloseOverlay();
        }

        // ------------------------------------------------------------------ paneles

        void Show(Overlay o)
        {
            _overlay = o;
            bool menu = o == Overlay.None;
            foreach (var b in _menuButtons) b.gameObject.SetActive(menu);
            logo.enabled = menu;
            recordText.gameObject.SetActive(menu);
            if (hardcoreStamp) hardcoreStamp.enabled = menu && BarMischief.AnyLampBroken;
            if (playground) playground.Interactive = menu;
            leaderboard.gameObject.SetActive(o == Overlay.Ranking);
            _rankingBack.gameObject.SetActive(o == Overlay.Ranking);
            _settings.gameObject.SetActive(o == Overlay.Settings);
            _credits.gameObject.SetActive(o == Overlay.Credits);
            if (idleWatcher) idleWatcher.suspended = o == Overlay.Credits || o == Overlay.Birthday;
            if (!menu && bar.dialogue && o != Overlay.Birthday) bar.dialogue.Hide();
        }

        async void OpenRanking()
        {
            Show(Overlay.Ranking);
            _rankingBack.Focus();
            leaderboard.SetTitle("RANKING DEL BAR");
            leaderboard.ShowLoading();
            var top = await LeaderboardClient.GetTopAsync(10);
            var me = LeaderboardClient.IsOnline ? await LeaderboardClient.GetPlayerAsync() : null;
            if (this == null || _overlay != Overlay.Ranking) return;
            leaderboard.Show(top, LeaderboardClient.IsOnline, me);
        }

        void OpenSettings()
        {
            Show(Overlay.Settings);
            _settings.Open();
        }

        void OpenCredits()
        {
            Show(Overlay.Credits);
            _credits.Open();
            AudioManager.PlayMusic(MusicTrack.Results);
        }

        void StartBirthday()
        {
            Show(Overlay.Birthday);
            _birthday.Play();
        }

        void CloseOverlay()
        {
            var was = _overlay;
            Show(Overlay.None);
            if (was == Overlay.Credits) AudioManager.PlayMusic(MenuMusic);
            if (was == Overlay.Birthday)
                foreach (var who in new[] { Speaker.Pipe, Speaker.Nata })
                    if (BarMischief.IsDrunk(who)) bar.HideDown(who); // el ebrio vuelve a dormir
            RefreshRecord();
            (was == Overlay.Ranking ? _ranking : was == Overlay.Settings || was == Overlay.Birthday ? _settingsBtn : was == Overlay.Credits ? _creditsBtn : _play).Focus();
        }

        static MusicTrack MenuMusic => BarMischief.AnyLampBroken ? MusicTrack.Hardcore : MusicTrack.Menu;

        /// <summary>Con una ampolleta rota el menú se ve punk: así queda claro que la próxima partida es Hardcore.</summary>
        void ApplyHardcoreLook()
        {
            bool hc = BarMischief.AnyLampBroken;
            bar.SetHardcoreLook(hc);
            if (hardcoreStamp) hardcoreStamp.enabled = hc && _overlay == Overlay.None;
        }

        void OnHardcoreActivated()
        {
            AudioManager.Play(Sfx.HardcoreSting);
            AudioManager.PlayMusic(MusicTrack.Hardcore);
            AudioManager.SetAmbience(false);
            ApplyHardcoreLook();
            if (hardcoreStamp)
            {
                PixelTween.Punch(this, hardcoreStamp.transform, 0.5f, 0.35f);
                StartCoroutine(StampPulse());
            }
        }

        IEnumerator StampPulse()
        {
            while (hardcoreStamp && BarMischief.AnyLampBroken)
            {
                yield return new WaitForSeconds(1.6f);
                if (hardcoreStamp.enabled) PixelTween.Punch(this, hardcoreStamp.transform, 0.15f, 0.2f);
            }
        }

        void RefreshRecord()
        {
            int record = GameSession.Record;
            recordText.Set(record > 0 ? "RÉCORD: " + record : "¡TODAVÍA SIN RÉCORD!");
        }

        static Vector3 Scr(float px, float py) => new Vector3((px - 160f) / 16f, (py - 90f) / 16f, 0f);
    }
}
