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
    /// Intro animada + tutorial guiado por Pipe (sección 7 del MVP).
    /// 1) Bar a oscuras → se encienden lámparas y neón mientras la cámara recorre la barra.
    /// 2) Pipe y Nata se presentan.
    /// 3) Tutorial interactivo: elegir llave, servir con HOLD hasta la línea, entregar.
    /// </summary>
    public class IntroTutorialDirector : MonoBehaviour
    {
        public BarScene bar;
        public UiSkin skin;
        public PixelText caption;
        public SpriteRenderer arrow;
        public SpriteRenderer[] errorIcons;

        [Header("Servido del tutorial")]
        public float pourRate = 0.3f;
        public float targetLevel = 0.92f;
        public float tolerance = 0.07f;

        enum Phase { Cutscene, Tutorial, Done }

        Phase _phase = Phase.Cutscene;
        PixelButton _skip;
        Coroutine _main;
        Transform _arrowTarget;
        Vector3 _arrowOffset;
        int _selectedTap = -1;
        bool _pouring;
        float _liquid, _foam;
        bool _released;

        void Start()
        {
            AudioManager.PlayMusic(MusicTrack.None);
            AudioManager.SetAmbience(true);
            _skip = PixelButton.Create(transform, skin, "SALTAR", new Vector2Int(48, 14), Scr(290, 170), Skip, SortOrder.Overlay + 20);
            _skip.clickSound = Sfx.UiBack;
            arrow.enabled = false;
            foreach (var e in errorIcons) e.enabled = false;
            bar.glass.gameObject.SetActive(false);
            foreach (var c in bar.customers) c.HideBehindCounter();
            bar.HideStaff();
            _main = StartCoroutine(Cutscene());
        }

        // ------------------------------------------------------------------ intro

        IEnumerator Cutscene()
        {
            bar.LightsOff();
            var cam = bar.cam.transform;
            var camHome = cam.position;
            cam.position = camHome + Vector3.left * 1.75f; // el fondo tiene 32 px de margen

            yield return Type("UNA NOCHE CUALQUIERA...", 1.4f);
            yield return Type("EN EL BAR DEL PIU", 1.2f);
            caption.Set("");

            StartCoroutine(PanCamera(cam, camHome, 3.2f));
            yield return bar.TurnOnLightsRoutine();
            AudioManager.PlayMusic(MusicTrack.Menu);
            yield return new WaitForSeconds(0.4f);

            yield return Presentation();
            _main = StartCoroutine(Tutorial());
        }

        /// <summary>Quien enseña: Pipe, salvo que esté ebrio por las travesuras del menú.</summary>
        static Speaker Teacher => BarMischief.Available(Speaker.Pipe);

        /// <summary>Quien explica los pedidos: Nata, salvo que esté ebria.</summary>
        static Speaker Helper => BarMischief.Available(Speaker.Nata);

        /// <summary>Presentación de Pipe y Nata; si uno quedó ebrio en el menú, el otro lo cubre.</summary>
        IEnumerator Presentation()
        {
            if (BarMischief.IsDrunk(Speaker.Pipe))
            {
                yield return bar.PopUp(Speaker.Nata, StaffExpression.Feliz);
                yield return Say(Speaker.Nata, StaffExpression.Feliz, "¡HOLA! SOY NATA. ¡YO ME ENCARGO DE LOS PEDIDOS!");
                yield return Say(Speaker.Nata, StaffExpression.Sorpresa, "PIPE ESTÁ... INDISPUESTO. ¡HOY TE ENSEÑO YO!");
                yield return bar.HideDown(Speaker.Nata);
                yield break;
            }
            if (BarMischief.IsDrunk(Speaker.Nata))
            {
                yield return bar.PopUp(Speaker.Pipe, StaffExpression.Feliz);
                yield return Say(Speaker.Pipe, StaffExpression.Feliz, "¡HOLA! SOY PIPE, EL MAESTRO DE LA BARRA.");
                yield return Say(Speaker.Pipe, StaffExpression.Sorpresa, "NATA SE TOMÓ LA NOCHE LIBRE... HOY ANOTO YO LOS PEDIDOS.");
                yield return bar.HideDown(Speaker.Pipe);
                yield break;
            }
            yield return bar.PopUp(Speaker.Pipe, StaffExpression.Feliz);
            yield return Say(Speaker.Pipe, StaffExpression.Feliz, "¡HOLA! SOY PIPE, EL MAESTRO DE LA BARRA.");
            yield return bar.PopUp(Speaker.Nata, StaffExpression.Neutral);
            bar.SetExpression(Speaker.Nata, StaffExpression.Feliz);
            yield return Say(Speaker.Nata, StaffExpression.Feliz, "Y YO SOY NATA. ¡YO ME ENCARGO DE LOS PEDIDOS!");
            bar.SetExpression(Speaker.Pipe, StaffExpression.Sorpresa);
            yield return Say(Speaker.Pipe, StaffExpression.Sorpresa, "¡SE VIENE UNA NOCHE LARGA! TE ENSEÑO A SERVIR.");
            bar.SetExpression(Speaker.Pipe, StaffExpression.Feliz);
            yield return new WaitForSeconds(0.3f);
            bar.HideDown(Speaker.Nata);
            yield return bar.HideDown(Speaker.Pipe);
        }

        IEnumerator PanCamera(Transform cam, Vector3 to, float time)
        {
            var from = cam.position;
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                cam.position = Vector3.Lerp(from, to, PixelTween.EaseOutCubic(t / time));
                yield return null;
            }
            cam.position = to;
        }

        IEnumerator Type(string text, float hold)
        {
            caption.visibleCharacters = 0;
            caption.Set(text);
            caption.Rebuild();
            for (int i = 1; i <= text.Length; i++)
            {
                caption.visibleCharacters = i;
                caption.Rebuild();
                if (i % 2 == 0) AudioManager.Play(Sfx.DialogBlip, 0.6f, 0.7f);
                yield return new WaitForSeconds(0.045f);
            }
            yield return new WaitForSeconds(hold);
        }

        IEnumerator Say(Speaker who, StaffExpression e, string line, float minTime = 1.6f)
        {
            bool done = false;
            bar.dialogue.Say(who, e, line, 0f, () => done = true);
            float t = 0f;
            while (!done || t < minTime)
            {
                t += Time.deltaTime;
                if (PixelPointer.Pressed && bar.dialogue.IsTyping) bar.dialogue.Skip();
                yield return null;
            }
            yield return new WaitForSeconds(0.6f);
        }

        // ------------------------------------------------------------------ tutorial

        IEnumerator Tutorial()
        {
            _phase = Phase.Tutorial;
            _skip.SetText("SALTAR");
            bar.LightsOnInstant();
            AudioManager.PlayMusic(MusicTrack.Game);

            var customer = bar.customers[0];
            customer.Setup(3, BeerKind.Rubia);
            customer.PlayArrive();
            customer.SetPatience(1f);
            yield return new WaitForSeconds(0.6f);

            bar.glass.gameObject.SetActive(true);
            bar.glass.SetBeer(BeerKind.Rubia);
            bar.glass.SetLevels(0, 0);
            bar.glass.SetTargetLevel(targetLevel, true);
            AudioManager.Play(Sfx.GlassPlace);

            // Paso 1: elegir la llave correcta
            PointAt(customer.orderBubble.transform, new Vector3(0.2f, 1.5f, 0));
            yield return Say(Teacher, StaffExpression.Neutral, "MIRA EL PEDIDO: ESTE CLIENTE QUIERE UNA RUBIA (R).", 1.2f);
            PointAt(bar.taps[0].handle.transform, new Vector3(0, 1.6f, 0));
            bar.dialogue.Say(Teacher, StaffExpression.Feliz, "TOCA LA LLAVE CON LA R. (O APRIETA 1, 2, 3)", 0f);
            _selectedTap = -1;
            while (_selectedTap != 0)
            {
                int pick = ReadTapSelection();
                if (pick >= 0)
                {
                    SelectTap(pick);
                    if (pick != 0) bar.dialogue.Say(Teacher, StaffExpression.Sorpresa, "¡ESA NO! BUSCA LA LETRA R.", 0f);
                }
                else if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    bar.dialogue.Say(Teacher, StaffExpression.Sorpresa, "¡PRIMERO LA LLAVE! TOCA LA R O APRIETA 1.", 0f);
                    PixelTween.Punch(this, bar.taps[0].handle.transform, 0.4f, 0.25f);
                }
                yield return null;
            }
            AudioManager.Play(Sfx.ServeGood);
            while (PixelPointer.Down) yield return null; // que el clic de elegir no empiece a servir

            // Paso 2: servir con HOLD hasta la línea
            int tries = 0;
            while (true)
            {
                _liquid = _foam = 0f;
                bar.glass.SetLevels(0, 0);
                PointAt(bar.glass.transform, new Vector3(0, 2.1f, 0));
                bar.dialogue.Say(Teacher, StaffExpression.Feliz,
                    tries == 0 ? "MANTÉN PRESIONADO EL VASO PARA SERVIR. ¡SUELTA EN LA LÍNEA!" : "¡OTRA VEZ! SUELTA JUSTO EN LA LÍNEA.", 0f);
                _released = false;
                while (!_released) { UpdatePour(); yield return null; }
                float total = _liquid + _foam;
                tries++;
                if (Mathf.Abs(total - targetLevel) <= tolerance || tries >= 3)
                {
                    FeedbackDirector.Instance.ServeResult(Mathf.Abs(total - targetLevel) <= tolerance * 0.5f ? ServeGrade.Perfecto : ServeGrade.Bueno,
                        bar.glass.transform.position + Vector3.up * 2f, 0, 0);
                    break;
                }
                bar.dialogue.Say(Teacher, StaffExpression.Sorpresa,
                    total > targetLevel ? "¡ESA ESPUMA PARECE UNA MONTAÑA!" : "TE QUEDASTE CORTO...", 0f);
                AudioManager.Play(Sfx.ServeRegular);
                yield return new WaitForSeconds(1.6f);
            }
            yield return new WaitForSeconds(0.8f);

            // Paso 3: la espuma baja → entregar rápido
            PointAt(customer.body.transform, new Vector3(0, 3.6f, 0));
            bar.dialogue.Say(Teacher, StaffExpression.Feliz, "LA ESPUMA BAJA SOLA. ¡ENTREGA RÁPIDO! TOCA AL CLIENTE.", 0f);
            float patience = 1f;
            while (true)
            {
                SettleFoam();
                patience = Mathf.Max(0.35f, patience - Time.deltaTime * 0.05f);
                customer.SetPatience(patience);
                var kb = Keyboard.current;
                bool deliver = (kb != null && kb.eKey.wasPressedThisFrame) ||
                               (PixelPointer.Released && IsOver(customer.body.transform, PixelPointer.World));
                if (deliver) break;
                yield return null;
            }
            arrow.enabled = false;
            AudioManager.Play(Sfx.GlassSlide);
            bar.glass.Flash();
            customer.PlayServed();
            var pos = customer.transform.position + Vector3.up * 3.2f;
            FeedbackDirector.Instance.ServeResult(ServeGrade.Perfecto, pos, 100, 0);
            yield return new WaitForSeconds(0.5f);
            FeedbackDirector.Instance.Tip(pos + Vector3.down, 30);
            FeedbackDirector.Instance.CustomerHappy(pos);
            customer.PlayLeave(true);
            bar.glass.SetLevels(0, 0);
            yield return new WaitForSeconds(1f);

            // Paso 4: Nata explica paciencia y errores
            yield return bar.PopUp(Helper, StaffExpression.Neutral);
            foreach (var e in errorIcons)
            {
                e.enabled = true;
                PixelTween.Punch(this, e.transform, 0.4f, 0.2f);
                AudioManager.Play(Sfx.Pop);
                yield return new WaitForSeconds(0.12f);
            }
            yield return Say(Helper, StaffExpression.Neutral, "OJO: SI UN CLIENTE PIERDE LA PACIENCIA, SE VA.");
            yield return Say(Helper, StaffExpression.Sorpresa, "PEDIDO EQUIVOCADO O SERVIDO MALO TAMBIÉN ES ERROR. ¡CON 3 CERRAMOS!");
            yield return Say(Helper, StaffExpression.Feliz, "LOS PERFECTOS SEGUIDOS SUMAN COMBO. ¡HASTA X20!");
            yield return bar.PopUp(Teacher, StaffExpression.Feliz);
            yield return Say(Teacher, StaffExpression.Feliz, "¡LISTO! ¡VAMOS QUE SALE OTRA RONDA!");
            Finish();
        }

        int ReadTapSelection()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) return 0;
                if (kb.digit2Key.wasPressedThisFrame) return 1;
                if (kb.digit3Key.wasPressedThisFrame) return 2;
            }
            if (PixelPointer.Pressed)
            {
                // Toda la columna de la llave cuenta: manija, caño y el vaso que está debajo
                for (int i = 0; i < bar.taps.Length; i++)
                    if (bar.taps[i].Contains(PixelPointer.World)) return i;
            }
            return -1;
        }

        void SelectTap(int i)
        {
            _selectedTap = i;
            for (int t = 0; t < bar.taps.Length; t++) bar.taps[t].SetSelected(t == i);
            var nozzle = bar.taps[i].NozzlePosition;
            var g = bar.glass.transform;
            PixelTween.MoveLocal(this, g, g.parent.InverseTransformPoint(new Vector3(nozzle.x, g.position.y, 0)), 0.12f);
            bar.glass.Slosh(1f);
            bar.glass.SetBeer(bar.taps[i].kind);
        }

        void UpdatePour()
        {
            var kb = Keyboard.current;
            bool want = (kb != null && kb.spaceKey.isPressed) ||
                        (PixelPointer.Down && bar.glassHitArea.OverlapPoint(PixelPointer.World));
            if (want != _pouring)
            {
                _pouring = want;
                bar.taps[0].SetPouring(want);
                bar.glass.SetPouring(want, bar.taps[0].NozzlePosition);
                if (!want && _liquid + _foam > 0.05f) _released = true;
            }
            if (_pouring)
            {
                float add = pourRate * Time.deltaTime;
                _liquid += add * 0.75f;
                _foam += add * 0.25f;
                float over = _liquid + _foam - 1f;
                if (over > 0f) _foam -= over;
            }
            bar.glass.SetLevels(_liquid, _foam);
        }

        void SettleFoam()
        {
            float s = Mathf.Min(_foam, 0.03f * Time.deltaTime);
            _foam -= s;
            _liquid += s * 0.4f;
            bar.glass.SetLevels(_liquid, _foam);
        }

        static bool IsOver(Transform body, Vector3 world)
        {
            var p = body.position;
            return Mathf.Abs(world.x - p.x) < 1.1f && world.y > p.y && world.y < p.y + 4f;
        }

        void PointAt(Transform target, Vector3 offset)
        {
            _arrowTarget = target;
            _arrowOffset = offset;
            arrow.enabled = true;
            AudioManager.Play(Sfx.Pop, 0.4f, 1.4f);
        }

        void LateUpdate()
        {
            if (arrow.enabled && _arrowTarget)
            {
                float bounce = Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 3f;
                arrow.transform.position = Px.Snap(_arrowTarget.position + _arrowOffset + Vector3.up * Mathf.Round(bounce) * Px.Unit);
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Skip();
        }

        // ------------------------------------------------------------------ salida

        void Skip()
        {
            if (_phase == Phase.Cutscene)
            {
                StopAllCoroutines();
                bar.StopAllCoroutines(); // corta encendidos y apariciones a medio camino
                caption.Set("");
                bar.cam.transform.position = new Vector3(0, 0, bar.cam.transform.position.z);
                bar.dialogue.Hide();
                bar.HideStaff();
                _main = StartCoroutine(Tutorial());
            }
            else if (_phase == Phase.Tutorial)
            {
                Finish();
            }
        }

        void Finish()
        {
            if (_phase == Phase.Done) return;
            _phase = Phase.Done;
            if (_pouring) bar.glass.SetPouring(false);
            GameSession.TutorialDone = true;
            SceneFlow.Load(GameSession.TutorialFromMenu ? GameSession.MenuScene : GameSession.GameScene);
        }

        static Vector3 Scr(float px, float py) => new Vector3((px - 160f) / 16f, (py - 90f) / 16f, 0f);
    }
}
