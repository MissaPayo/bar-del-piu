using System.Collections.Generic;
using BarDelPiu.UI;
using BarDelPiu.Visual;
using UnityEngine;

namespace BarDelPiu.Leaderboard
{
    /// <summary>
    /// Tabla de ranking pixel art: título, filas animadas (entran una por una) y estado
    /// (cargando / sin conexión). Se usa en el menú y en resultados.
    /// </summary>
    public class LeaderboardView : MonoBehaviour
    {
        public UiSkin skin;
        public int rows = 10;
        public int widthPx = 200;
        public int rowHeightPx = 11;

        SpriteRenderer _panel;
        PixelText _title, _status;
        readonly List<PixelText[]> _rowTexts = new List<PixelText[]>();
        readonly List<SpriteRenderer> _rowBg = new List<SpriteRenderer>();
        bool _built;

        public int HeightPx => 30 + rows * rowHeightPx + 6;

        void Build()
        {
            if (_built) return;
            _built = true;
            float u = Px.Unit;
            int h = HeightPx;

            _panel = new GameObject("Panel").AddComponent<SpriteRenderer>();
            _panel.transform.SetParent(transform, false);
            _panel.sprite = skin.panelDark;
            _panel.drawMode = SpriteDrawMode.Sliced;
            _panel.size = new Vector2(widthPx, h) * u;
            _panel.transform.localPosition = new Vector3(0, -h / 2f * u, 0);
            _panel.sortingOrder = SortOrder.Overlay;
            _panel.sharedMaterial = skin.unlit;

            _title = MakeText("Title", new Vector3(0, -15 * u, 0), "RANKING DEL BAR", BarPalette.AmberLight, PixelText.Align.Center);
            _status = MakeText("Status", new Vector3(0, -(30 + rowHeightPx * 4) * u, 0), "", BarPalette.CreamDark, PixelText.Align.Center);

            int x0 = -widthPx / 2 + 10;
            for (int i = 0; i < rows; i++)
            {
                float y = -(30 + i * rowHeightPx + 8) * u;
                var bg = new GameObject("RowBg").AddComponent<SpriteRenderer>();
                bg.transform.SetParent(transform, false);
                bg.sprite = skin.pixel;
                bg.transform.localPosition = new Vector3((x0 - 4) * u, y - 2 * u, 0);
                bg.transform.localScale = new Vector3(widthPx - 12, rowHeightPx - 1, 1);
                bg.sortingOrder = SortOrder.Overlay + 1;
                bg.sharedMaterial = skin.unlit;
                bg.enabled = false;
                _rowBg.Add(bg);
                _rowTexts.Add(new[]
                {
                    MakeText("Rank", new Vector3((x0 + 14) * u, y, 0), "", BarPalette.CreamDark, PixelText.Align.Right),
                    MakeText("Name", new Vector3((x0 + 24) * u, y, 0), "", BarPalette.Cream, PixelText.Align.Left),
                    MakeText("Score", new Vector3((widthPx / 2 - 10) * u, y, 0), "", BarPalette.AmberLight, PixelText.Align.Right),
                });
            }
        }

        PixelText MakeText(string n, Vector3 pos, string text, Color c, PixelText.Align align)
        {
            var t = new GameObject(n).AddComponent<PixelText>();
            t.transform.SetParent(transform, false);
            t.transform.localPosition = pos;
            t.font = skin.font;
            t.material = skin.unlit;
            t.text = text;
            t.color = c;
            t.alignment = align;
            t.sortingOrder = SortOrder.Overlay + 3;
            return t;
        }

        public void SetTitle(string title)
        {
            Build();
            _title.Set(title);
        }

        public void ShowLoading()
        {
            Build();
            Clear();
            _status.Set("CARGANDO...");
        }

        public void Show(List<ScoreRow> data, bool online, ScoreRow? player = null)
        {
            Build();
            Clear();
            _status.Set(data.Count == 0 ? "¡TODAVÍA NO HAY PUNTAJES!" : "");
            bool playerShown = false;
            for (int i = 0; i < rows; i++)
            {
                if (i < data.Count)
                {
                    SetRow(i, data[i], i * 0.05f);
                    playerShown |= data[i].isPlayer;
                }
            }
            // Si el jugador quedó fuera del top, se muestra en la última fila
            if (player.HasValue && !playerShown && data.Count >= rows)
                SetRow(rows - 1, player.Value, rows * 0.05f);
            if (!online) _title.Set("RANKING LOCAL (SIN CONEXIÓN)");
        }

        void SetRow(int i, ScoreRow r, float delay)
        {
            var t = _rowTexts[i];
            Color nameColor = r.isPlayer ? BarPalette.AmberLight : BarPalette.Cream;
            t[0].Set(r.rank + ".");
            t[0].SetColor(r.rank <= 3 ? BarPalette.AmberLight : BarPalette.CreamDark);
            t[1].Set(r.name);
            t[1].SetColor(nameColor);
            t[2].Set(r.score.ToString());
            _rowBg[i].enabled = r.isPlayer || i % 2 == 0;
            _rowBg[i].color = r.isPlayer ? new Color(0.85f, 0.6f, 0.2f, 0.35f) : new Color(1f, 1f, 1f, 0.05f);
            foreach (var pt in t)
            {
                pt.gameObject.SetActive(false);
                StartCoroutine(Reveal(pt, delay));
            }
        }

        System.Collections.IEnumerator Reveal(PixelText pt, float delay)
        {
            yield return new WaitForSeconds(delay);
            pt.gameObject.SetActive(true);
            var home = pt.transform.localPosition;
            pt.transform.localPosition = home + Vector3.left * 8 * Px.Unit;
            yield return PixelTween.MoveLocal(this, pt.transform, home, 0.15f);
        }

        void Clear()
        {
            StopAllCoroutines();
            for (int i = 0; i < _rowTexts.Count; i++)
            {
                foreach (var t in _rowTexts[i]) t.Set("");
                _rowBg[i].enabled = false;
            }
        }
    }
}
