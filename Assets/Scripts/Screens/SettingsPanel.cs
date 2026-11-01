using System;
using BarDelPiu.Audio;
using BarDelPiu.Core;
using BarDelPiu.UI;
using BarDelPiu.Visual;
using UnityEngine;

namespace BarDelPiu.Screens
{
    /// <summary>
    /// Panel de ajustes: volumen de música y efectos, sacudida de cámara, parpadeo de neones y créditos.
    /// Secreto: tocar 5 veces la versión abre la sorpresa de cumpleaños.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        const int Steps = 5;
        const int TapsForSecret = 5;

        UiSkin _skin;
        Action _onClose, _onCredits, _onSecret;
        SpriteRenderer[] _musicBars, _sfxBars;
        PixelButton _soundBtn, _shakeBtn, _flickerBtn, _back;
        PixelText _version;
        BoxCollider2D _versionHit;
        int _versionTaps;
        float _lastVersionTap;

        public static SettingsPanel Create(Transform parent, UiSkin skin, Action onClose, Action onCredits, Action onSecret)
        {
            var go = new GameObject("Settings");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<SettingsPanel>();
            p._skin = skin;
            p._onClose = onClose;
            p._onCredits = onCredits;
            p._onSecret = onSecret;
            p.Build();
            go.SetActive(false);
            return p;
        }

        public PixelButton BackButton => _back;

        void Build()
        {
            float u = Px.Unit;
            int o = SortOrder.Overlay;
            var panel = Sprite("Panel", _skin.panelDark, Scr(160, 91), o);
            panel.drawMode = SpriteDrawMode.Sliced;
            panel.size = new Vector2(200, 172) * u;

            Text("Title", Scr(160, 158), "AJUSTES", BarPalette.AmberLight, PixelText.Align.Center, 2);

            // Sonido general (antes era un ícono en el menú)
            Text("SoundLbl", Scr(76, 133), "SONIDO", BarPalette.Cream, PixelText.Align.Left);
            _soundBtn = PixelButton.Create(transform, _skin, "", new Vector2Int(36, 16), Scr(226, 136), () =>
            {
                bool muted = AudioManager.GetVolume(AudioBus.Master) < 0.01f;
                AudioManager.SetVolume(AudioBus.Master, muted ? 1f : 0f);
                Refresh();
            }, o + 10);

            _musicBars = VolumeRow("MÚSICA", 117, AudioBus.Music);
            _sfxBars = VolumeRow("EFECTOS", 98, AudioBus.Sfx);

            Text("ShakeLbl", Scr(76, 76), "SACUDIDA", BarPalette.Cream, PixelText.Align.Left);
            _shakeBtn = PixelButton.Create(transform, _skin, "", new Vector2Int(36, 16), Scr(226, 79), () =>
            {
                GameSettings.ScreenShake = !GameSettings.ScreenShake;
                Refresh();
            }, o + 10);
            Text("FlickerLbl", Scr(76, 57), "NEÓN PARPADEA", BarPalette.Cream, PixelText.Align.Left);
            _flickerBtn = PixelButton.Create(transform, _skin, "", new Vector2Int(36, 16), Scr(226, 60), () =>
            {
                GameSettings.NeonFlicker = !GameSettings.NeonFlicker;
                Refresh();
            }, o + 10);

            PixelButton.Create(transform, _skin, "CRÉDITOS", new Vector2Int(72, 16), Scr(120, 36), () => _onCredits?.Invoke(), o + 10);
            _back = PixelButton.Create(transform, _skin, "VOLVER", new Vector2Int(72, 16), Scr(200, 36), () => _onClose?.Invoke(), o + 10);
            _back.clickSound = Sfx.UiBack;

            _version = Text("Version", Scr(160, 12), "VERSIÓN " + GameSettings.Version, BarPalette.GreenHighlight, PixelText.Align.Center);
            _versionHit = _version.gameObject.AddComponent<BoxCollider2D>();
            _versionHit.size = new Vector2(70, 18) * u;
            _versionHit.offset = new Vector2(0, 4) * u;
            _versionHit.isTrigger = true;
        }

        SpriteRenderer[] VolumeRow(string label, int y, AudioBus bus)
        {
            int o = SortOrder.Overlay;
            Text(label + "Lbl", Scr(76, y - 3), label, BarPalette.Cream, PixelText.Align.Left);
            PixelButton.Create(transform, _skin, "-", new Vector2Int(16, 16), Scr(160, y), () => Step(bus, -1), o + 10);
            PixelButton.Create(transform, _skin, "+", new Vector2Int(16, 16), Scr(244, y), () => Step(bus, +1), o + 10);
            var bars = new SpriteRenderer[Steps];
            for (int i = 0; i < Steps; i++)
            {
                var sr = Sprite("Bar" + i, _skin.pixel, Scr(173 + i * 12, y - 4), o + 5);
                sr.transform.localScale = new Vector3(9, 4 + i * 2, 1);
                bars[i] = sr;
            }
            return bars;
        }

        void Step(AudioBus bus, int dir)
        {
            float v = Mathf.Clamp01(Mathf.Round(AudioManager.GetVolume(bus) * Steps + dir) / Steps);
            AudioManager.SetVolume(bus, v);
            if (bus == AudioBus.Sfx) AudioManager.Play(Sfx.TipCoin);
            Refresh();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            _versionTaps = 0;
            Refresh();
            _back.Focus();
        }

        void Refresh()
        {
            Fill(_musicBars, AudioManager.GetVolume(AudioBus.Music));
            Fill(_sfxBars, AudioManager.GetVolume(AudioBus.Sfx));
            _soundBtn.SetText(AudioManager.GetVolume(AudioBus.Master) < 0.01f ? "NO" : "SÍ");
            _shakeBtn.SetText(GameSettings.ScreenShake ? "SÍ" : "NO");
            _flickerBtn.SetText(GameSettings.NeonFlicker ? "SÍ" : "NO");
        }

        static void Fill(SpriteRenderer[] bars, float v)
        {
            int on = Mathf.RoundToInt(v * Steps);
            for (int i = 0; i < bars.Length; i++)
                bars[i].color = i < on ? BarPalette.AmberLight : new Color(1, 1, 1, 0.12f);
        }

        void Update()
        {
            // Toques en la versión: el secreto
            if (PixelPointer.Pressed && _versionHit.OverlapPoint(PixelPointer.World))
            {
                if (Time.unscaledTime - _lastVersionTap > 1.5f) _versionTaps = 0;
                _lastVersionTap = Time.unscaledTime;
                _versionTaps++;
                AudioManager.Play(Sfx.ScoreTick, 1f, 1f + _versionTaps * 0.12f);
                PixelTween.Punch(this, _version.transform, 0.3f, 0.15f);
                if (_versionTaps >= TapsForSecret)
                {
                    _versionTaps = 0;
                    _onSecret?.Invoke();
                }
            }
        }

        SpriteRenderer Sprite(string n, Sprite s, Vector3 pos, int order)
        {
            var sr = new GameObject(n).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.position = pos;
            sr.sprite = s;
            sr.sortingOrder = order;
            sr.sharedMaterial = _skin.unlit;
            return sr;
        }

        PixelText Text(string n, Vector3 pos, string text, Color c, PixelText.Align align, int scale = 1)
        {
            var t = new GameObject(n).AddComponent<PixelText>();
            t.transform.SetParent(transform, false);
            t.transform.position = pos;
            t.font = _skin.font;
            t.material = _skin.unlit;
            t.text = text;
            t.color = c;
            t.alignment = align;
            t.scale = scale;
            t.sortingOrder = SortOrder.Overlay + 12;
            return t;
        }

        static Vector3 Scr(float px, float py) => new Vector3((px - 160f) / 16f, (py - 90f) / 16f, 0f);
    }
}
