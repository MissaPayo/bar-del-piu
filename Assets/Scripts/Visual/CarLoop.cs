using System;
using BarDelPiu.Audio;
using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>
    /// Autos que pasan por la calle en un ciclo fijo (por defecto 20 s) que se repite.
    /// Cada auto tiene su momento de salida, carril, sentido y duración del cruce.
    /// </summary>
    public class CarLoop : MonoBehaviour
    {
        [Serializable]
        public class Car
        {
            public SpriteRenderer renderer;
            public Sprite[] frames;
            [Tooltip("Segundo del ciclo en que entra a la pantalla.")]
            public float start;
            [Tooltip("Segundos que tarda en cruzar.")]
            public float duration = 4f;
            public bool leftToRight = true;
            [Tooltip("Altura del carril en unidades de mundo.")]
            public float laneY;
            [NonSerialized] public bool wasActive;
        }

        public float loopSeconds = 20f;
        [Tooltip("Desde/hasta dónde viajan los autos (fuera de pantalla).")]
        public float edgeX = 12.5f;
        public Car[] cars;

        float _time;

        void Update()
        {
            _time = (_time + Time.deltaTime) % loopSeconds;
            foreach (var car in cars)
            {
                float t = _time - car.start;
                if (t < 0f) t += loopSeconds;
                bool active = t < car.duration;
                car.renderer.enabled = active;
                if (active && !car.wasActive) AudioManager.Play(Sfx.CarPass, 0.8f);
                car.wasActive = active;
                if (!active) continue;

                float k = t / car.duration;
                float x = car.leftToRight ? Mathf.Lerp(-edgeX, edgeX, k) : Mathf.Lerp(edgeX, -edgeX, k);
                car.renderer.flipX = !car.leftToRight;
                car.renderer.transform.position = Px.Snap(new Vector3(x, car.laneY, 0f));
                if (car.frames != null && car.frames.Length > 0)
                    car.renderer.sprite = car.frames[(int)(t * 10f) % car.frames.Length]; // giro de ruedas
            }
        }
    }
}
