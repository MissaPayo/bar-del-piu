using UnityEngine;

namespace BarDelPiu.Visual
{
    public enum FxKind
    {
        Sparkles,
        FoamBurst,
        Coins,
        Puff,
        Hearts,
        PourSplash,
        Confetti,
    }

    /// <summary>
    /// Arma los sistemas de partículas pixel art. El menú de configuración lo usa para guardar
    /// prefabs (que después se pueden retocar a mano en el Inspector).
    /// </summary>
    public static class FxBuilder
    {
        public static ParticleSystem Build(FxKind kind, Sprite[] sprites, Material material)
        {
            var go = new GameObject("FX_" + kind);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.sortingOrder = SortOrder.Fx;
            r.renderMode = ParticleSystemRenderMode.Billboard;

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            main.startRotation = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.rotation = new Vector3(-90f, 0f, 0f); // hacia arriba (+Y)
            shape.radius = 0.1f;

            var sheet = ps.textureSheetAnimation;
            sheet.enabled = sprites != null && sprites.Length > 0;
            if (sheet.enabled)
            {
                sheet.mode = ParticleSystemAnimationMode.Sprites;
                foreach (var s in sprites) sheet.AddSprite(s);
            }

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;

            float px = Px.Unit;
            switch (kind)
            {
                case FxKind.Sparkles:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
                    main.startSize = 7 * px;
                    main.gravityModifier = 0.35f;
                    shape.angle = 70f;
                    sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.99f, 1f, 0f));
                    break;
                case FxKind.FoamBurst:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(2 * px, 3 * px);
                    main.gravityModifier = 1.2f;
                    shape.angle = 40f;
                    main.startColor = BarPalette.Cream;
                    break;
                case FxKind.Coins:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 0.95f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
                    main.startSize = 6 * px;
                    main.gravityModifier = 2.2f;
                    shape.angle = 25f;
                    sheet.cycleCount = 3;
                    break;
                case FxKind.Puff:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(5 * px, 7 * px);
                    main.gravityModifier = -0.15f;
                    shape.angle = 60f;
                    var size = ps.sizeOverLifetime;
                    size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.4f));
                    break;
                case FxKind.Hearts:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.1f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                    main.startSize = 5 * px;
                    main.gravityModifier = -0.1f;
                    shape.angle = 30f;
                    break;
                case FxKind.PourSplash:
                    main.loop = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1 * px, 2 * px);
                    main.gravityModifier = 1.6f;
                    shape.angle = 55f;
                    shape.radius = 0.05f;
                    emission.rateOverTime = 28f;
                    break;
                case FxKind.Confetti:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1 * px, 2 * px);
                    main.gravityModifier = 0.55f;
                    main.maxParticles = 300;
                    shape.angle = 38f;
                    var colors = new Gradient();
                    colors.SetKeys(
                        new[]
                        {
                            new GradientColorKey(BarPalette.Red, 0f), new GradientColorKey(BarPalette.AmberLight, 0.25f),
                            new GradientColorKey(BarPalette.Mint, 0.5f), new GradientColorKey(BarPalette.Neon, 0.75f),
                            new GradientColorKey(BarPalette.Cream, 1f),
                        },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                    main.startColor = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };
                    var drag = ps.limitVelocityOverLifetime;
                    drag.enabled = true;
                    drag.drag = 1.8f;
                    var noise = ps.noise;
                    noise.enabled = true;
                    noise.strength = 0.6f;
                    noise.frequency = 1.2f;
                    break;
            }
            return ps;
        }
    }
}
