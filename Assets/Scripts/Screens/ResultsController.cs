using System.Collections;
using BarDelPiu.Audio;
using BarDelPiu.Core;
using BarDelPiu.Leaderboard;
using BarDelPiu.UI;
using BarDelPiu.Visual;
using UnityEngine;

namespace BarDelPiu.Screens
{
    /// <summary>
    /// Resultados: «¡CERRAMOS!», puntaje que cuenta hacia arriba, récord, mensaje de Nata,
    /// ingreso de apodo y ranking online. Botones Reintentar y Menú.
    /// </summary>
    public class ResultsController : MonoBehaviour
    {
        public BarScene bar;
        public UiSkin skin;
        public PixelText title;
        public PixelText scoreText;
        public PixelText recordText;
        public NicknameEntry nicknameEntry;
        public LeaderboardView leaderboard;
        public IdleWatcher idleWatcher;

        PixelButton _retry, _menu;
        bool _entryOpen;

        IEnumerator Start()
        {
            AudioManager.PlayMusic(MusicTrack.Results);
            AudioManager.SetMusicIntensity(0f);
            AudioManager.SetAmbience(true);
            bar.LightsOnInstant();
            bar.globalLight.intensity = bar.globalOn * 0.7f; // fin de la noche: más tenue
            bar.HideStaff();
            foreach (var c in bar.customers) c.HideBehindCounter();
            nicknameEntry.gameObject.SetActive(false);
            leaderboard.gameObject.SetActive(false);

            _retry = PixelButton.Create(transform, skin, "REINTENTAR", new Vector2Int(76, 16), Scr(116, 12), () => SceneFlow.Load(GameSession.GameScene), SortOrder.Overlay + 20);
            _retry.labelColor = BarPalette.GreenDark;
            _menu = PixelButton.Create(transform, skin, "MENÚ", new Vector2Int(56, 16), Scr(204, 12), () => SceneFlow.Load(GameSession.MenuScene), SortOrder.Overlay + 20);
            _menu.clickSound = Sfx.UiBack;
            _retry.gameObject.SetActive(false);
            _menu.gameObject.SetActive(false);

            int score = GameSession.LastScore;
            title.Set("¡CERRAMOS!");
            scoreText.Set("0");
            recordText.Set("");
            var titleHome = title.transform.localPosition;
            title.transform.localPosition = titleHome + Vector3.up * 40 * Px.Unit;
            yield return PixelTween.MoveLocal(this, title.transform, titleHome, 0.4f, PixelTween.EaseOutBack);
            AudioManager.Play(Sfx.GlassPlace);

            // Conteo del puntaje
            float t = 0f, dur = Mathf.Clamp(score / 3000f, 0.6f, 1.8f);
            int shown = -1;
            while (t < dur)
            {
                t += Time.deltaTime;
                int v = Mathf.RoundToInt(Mathf.Lerp(0, score, PixelTween.EaseOutCubic(t / dur)));
                if (v != shown)
                {
                    shown = v;
                    scoreText.Set(v.ToString());
                    if (Time.frameCount % 3 == 0) AudioManager.Play(Sfx.ScoreTick);
                }
                yield return null;
            }
            scoreText.Set(score.ToString());
            PixelTween.Punch(this, scoreText.transform, 0.3f, 0.25f);

            if (GameSession.LastWasRecord && score > 0)
            {
                recordText.Set("¡NUEVO RÉCORD!");
                recordText.SetColor(BarPalette.AmberLight);
                AudioManager.Play(Sfx.NewRecord);
                AudioManager.Duck(0.6f, 1.5f);
                FeedbackDirector.Instance.ServeResult(ServeGrade.Perfecto, scoreText.transform.position + Vector3.up * 0.5f, 0, 0);
                StartCoroutine(Blink(recordText));
            }
            else
            {
                recordText.Set("RÉCORD: " + GameSession.Record);
                recordText.SetColor(BarPalette.CreamDark);
            }

            yield return bar.PopUp(Speaker.Nata, StaffExpression.Feliz);
            bool spoke = false;
            bar.dialogue.Say(Speaker.Nata, ClosingExpression(score), ClosingLine(score), 0f, () => spoke = true);
            while (!spoke) yield return null;
            yield return new WaitForSeconds(1.2f);

            if (score > 0)
            {
                _entryOpen = true;
                bar.dialogue.Say(Speaker.Nata, StaffExpression.Feliz, "¿CÓMO TE ANOTO EN LA PIZARRA?", 0f);
                SetScoreVisible(false);
                nicknameEntry.Open(GameSession.Nickname, OnNickname);
            }
            else ShowLeaderboard(null);
        }

        void OnNickname(string nick)
        {
            _entryOpen = false;
            GameSession.Nickname = nick;
            ShowLeaderboard(nick);
        }

        async void ShowLeaderboard(string nick)
        {
            bar.dialogue.Hide();
            bar.HideDown(Speaker.Nata);
            SetScoreVisible(false);
            leaderboard.gameObject.SetActive(true);
            leaderboard.SetTitle("RANKING DEL BAR");
            leaderboard.ShowLoading();
            int rank = 0;
            if (nick != null) rank = await LeaderboardClient.SubmitAsync(GameSession.LastScore, nick);
            var top = await LeaderboardClient.GetTopAsync(10);
            var me = LeaderboardClient.IsOnline ? await LeaderboardClient.GetPlayerAsync() : null;
            if (this == null) return;
            leaderboard.Show(top, LeaderboardClient.IsOnline, me);
            if (rank > 0 && rank <= 3) AudioManager.Play(Sfx.ServePerfect);
            _retry.gameObject.SetActive(true);
            _menu.gameObject.SetActive(true);
            _retry.Focus();
        }

        void Update()
        {
            // No se va a la pantalla de espera mientras se escribe el apodo
            if (idleWatcher) idleWatcher.suspended = _entryOpen;
            if (!_entryOpen) PixelButton.UpdateKeyboardNavigation();
        }

        void SetScoreVisible(bool v)
        {
            title.gameObject.SetActive(v);
            scoreText.gameObject.SetActive(v);
            recordText.gameObject.SetActive(v);
        }

        IEnumerator Blink(PixelText t)
        {
            while (t && t.gameObject.activeInHierarchy)
            {
                t.SetColor(Time.time % 0.5f < 0.25f ? BarPalette.AmberLight : BarPalette.Neon);
                yield return new WaitForSeconds(0.25f);
            }
        }

        static string ClosingLine(int score) => score switch
        {
            0 => "¡UF! MAÑANA SERÁ OTRA NOCHE...",
            < 1000 => "NADA MAL PARA UNA PRIMERA NOCHE.",
            < 4000 => "¡BIEN AHÍ! LA BARRA QUEDÓ CONTENTA.",
            _ => "¡QUÉ NOCHE! ¡ERES UNA LEYENDA DEL PIU!",
        };

        static StaffExpression ClosingExpression(int score) => score == 0 ? StaffExpression.Sorpresa : StaffExpression.Feliz;

        static Vector3 Scr(float px, float py) => new Vector3((px - 160f) / 16f, (py - 90f) / 16f, 0f);
    }
}
