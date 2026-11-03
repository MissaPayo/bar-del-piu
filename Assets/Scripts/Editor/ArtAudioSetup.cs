using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BarDelPiu.Audio;
using BarDelPiu.Core;
using BarDelPiu.Dev;
using BarDelPiu.Leaderboard;
using BarDelPiu.Screens;
using BarDelPiu.UI;
using BarDelPiu.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BarDelPiu.EditorTools
{
    /// <summary>
    /// Menú «Bar del Piu ▸ Arte y Audio ▸ Configurar todo»: genera materiales, librerías de audio,
    /// fuente, prefabs, post-procesado y la escena de prueba ArtAudioTest a partir de los assets generados.
    /// Se puede volver a ejecutar cuando cambie el arte o el audio.
    /// </summary>
    public static class ArtAudioSetup
    {
        const string Sprites = "Assets/Art/Sprites/";
        const string Materials = "Assets/Art/Materials/";
        const string Data = "Assets/Art/Data/";
        const string AudioData = "Assets/Audio/Data/";
        const string PrefabDir = "Assets/Prefabs/";
        const string ScenePath = "Assets/Scenes/ArtAudioTest.unity";

        [MenuItem("Bar del Piu/Arte y Audio/Configurar todo", priority = 0)]
        public static void SetupAll()
        {
            AssetDatabase.Refresh();
            Ensure(Materials); Ensure(Data); Ensure(AudioData); Ensure(PrefabDir + "FX");
            Ensure("Assets/Resources/BarDelPiu");

            var mats = CreateMaterials();
            var font = CreatePixelFont();
            var customerArt = CreateCustomerArt();
            var staffArt = CreateStaffArt();
            var sfx = CreateSfxLibrary();
            var music = CreateMusicLibrary();
            CreateAudioManagerPrefab(sfx, music);
            var fx = CreateFxPrefabs(mats);
            var profile = CreatePostProfile();
            var skin = CreateUiSkin(mats, font);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildTestScene(mats, font, customerArt, staffArt, fx, profile);
            BuildResultsScene(mats, font, customerArt, staffArt, fx, profile, skin);
            BuildIntroScene(mats, font, customerArt, staffArt, fx, profile, skin);
            BuildIdleScene(mats, font, profile);
            BuildMenuScene(mats, font, customerArt, staffArt, fx, profile, skin);
            SetBuildScenes();
            Debug.Log("[Bar del Piu] Arte, audio y pantallas configurados. Escena inicial: Menu");
        }

        // ------------------------------------------------------------------ utilidades

        static void Ensure(string dir)
        {
            dir = dir.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
        }

        static Sprite S(string rel)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + rel);
            if (s == null) Debug.LogWarning("[Bar del Piu] Falta sprite: " + rel);
            return s;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static Material Mat(string name, Shader shader)
        {
            string path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            return m;
        }

        static string Snake(string pascal) => Regex.Replace(pascal, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();

        /// <summary>Convierte coordenadas de pantalla en píxeles (0..320, 0..180) a mundo.</summary>
        static Vector3 Scr(float px, float py) => new Vector3((px - 160f) / 16f, (py - 90f) / 16f, 0f);

        static SpriteRenderer SR(string name, Transform parent, Sprite sprite, Vector3 localPos, int order, Material mat = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (mat) sr.sharedMaterial = mat;
            return sr;
        }

        static PixelText Text(string name, Transform parent, Vector3 localPos, PixelFont font, Material mat, string text,
            Color color, PixelText.Align align, int scale = 1, bool shadow = true, int maxWidth = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<PixelText>();
            t.font = font;
            t.material = mat;
            t.text = text;
            t.color = color;
            t.alignment = align;
            t.scale = scale;
            t.shadow = shadow;
            t.maxWidth = maxWidth;
            return t;
        }

        static Light2D Light(string name, Transform parent, Vector3 pos, Color color, float intensity, float radius,
            Light2D.LightType type = Light2D.LightType.Point)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light2D>();
            l.lightType = type;
            l.color = color;
            l.intensity = intensity;
            if (type == Light2D.LightType.Point)
            {
                l.pointLightOuterRadius = radius;
                l.pointLightInnerRadius = radius * 0.15f;
                l.falloffIntensity = 0.6f;
            }
            return l;
        }

        // ------------------------------------------------------------------ assets

        class Mats
        {
            public Material beer, unlit, glowNeon, glowLamp, glowFx;
        }

        static Mats CreateMaterials()
        {
            var m = new Mats();
            m.beer = Mat("BeerLiquid", Shader.Find("BarDelPiu/BeerLiquid"));
            var glassJson = File.ReadAllText(Sprites + "Glass/glass.json");
            var g = JsonUtility.FromJson<GlassMeta>(glassJson);
            m.beer.SetFloat("_InnerBottom", g.innerBottomPx);
            m.beer.SetFloat("_InnerTop", g.innerTopPx);
            m.beer.SetVector("_MaskSize", new Vector4(g.width, g.height, 0, 0));
            m.unlit = Mat("SpriteUnlit", Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            m.glowNeon = Mat("Glow_Neon", Shader.Find("BarDelPiu/SpriteGlow"));
            m.glowNeon.SetFloat("_Intensity", 2.4f);
            m.glowLamp = Mat("Glow_Lamp", Shader.Find("BarDelPiu/SpriteGlow"));
            m.glowLamp.SetFloat("_Intensity", 1.5f);
            m.glowFx = Mat("Glow_Fx", Shader.Find("BarDelPiu/SpriteGlow"));
            m.glowFx.SetFloat("_Intensity", 1.8f);
            foreach (var mat in new[] { m.beer, m.unlit, m.glowNeon, m.glowLamp, m.glowFx }) EditorUtility.SetDirty(mat);
            return m;
        }

        [Serializable] class GlassMeta { public int innerBottomPx, innerTopPx, width, height; }

        [Serializable]
        class TowerMeta
        {
            public int[] spouts_flat, handles_flat;
            public int column_w_px = 22, handle_h_px = 30;
            public float SpoutX(int i) => spouts_flat[i * 2];
            public float SpoutY(int i) => spouts_flat[i * 2 + 1];
            public float HandleY(int i) => handles_flat[i * 2 + 1];
        }

        [Serializable] class FontJson { public int height, spacing, spaceWidth; public GlyphJson[] glyphs; }

        [Serializable] class GlyphJson { public string character, file; public int width; }

        static PixelFont CreatePixelFont()
        {
            var json = JsonUtility.FromJson<FontJson>(File.ReadAllText(Sprites + "Font/font.json"));
            var font = LoadOrCreate<PixelFont>(Data + "PixelFont.asset");
            font.height = json.height;
            font.spacing = json.spacing;
            font.spaceWidth = json.spaceWidth;
            font.lineSpacing = 1;
            font.glyphs.Clear();
            foreach (var g in json.glyphs)
                font.glyphs.Add(new PixelFont.Glyph { character = g.character, width = g.width, sprite = S("Font/" + g.file) });
            EditorUtility.SetDirty(font);
            return font;
        }

        static CustomerArt CreateCustomerArt()
        {
            var art = LoadOrCreate<CustomerArt>(Data + "CustomerArt.asset");
            art.variants.Clear();
            for (int i = 0; i < 8; i++)
            {
                art.variants.Add(new CustomerArt.Variant
                {
                    neutral = S($"Characters/Customers/customer_{i}_neutral.png"),
                    feliz = S($"Characters/Customers/customer_{i}_feliz.png"),
                    impaciente = S($"Characters/Customers/customer_{i}_impaciente.png"),
                    enojado = S($"Characters/Customers/customer_{i}_enojado.png"),
                });
            }
            art.orderRubia = S("UI/order_rubia.png");
            art.orderAmbar = S("UI/order_ambar.png");
            art.orderNegra = S("UI/order_negra.png");
            art.orderRubiaHint = S("UI/order_rubia_hint.png");
            art.orderAmbarHint = S("UI/order_ambar_hint.png");
            art.orderNegraHint = S("UI/order_negra_hint.png");
            art.hardcoreVariants.Clear();
            for (int i = 0; i < 6; i++)
            {
                art.hardcoreVariants.Add(new CustomerArt.Variant
                {
                    neutral = S($"Characters/Customers/customer_hc_{i}_neutral.png"),
                    feliz = S($"Characters/Customers/customer_hc_{i}_feliz.png"),
                    impaciente = S($"Characters/Customers/customer_hc_{i}_impaciente.png"),
                    enojado = S($"Characters/Customers/customer_hc_{i}_enojado.png"),
                });
            }
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(BarPalette.Red, 0f), new GradientColorKey(BarPalette.Red, 0.25f),
                    new GradientColorKey(BarPalette.Amber, 0.45f), new GradientColorKey(BarPalette.GreenHighlight, 0.7f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            grad.mode = GradientMode.Fixed; // bandas de color, más «pixel»
            art.patienceColor = grad;
            EditorUtility.SetDirty(art);
            return art;
        }

        static StaffArt CreateStaffArt()
        {
            var st = LoadOrCreate<StaffArt>(Data + "StaffArt.asset");
            string[] ex = { "neutral", "feliz", "sorpresa", "ebrio" };
            st.pipe = new Sprite[ex.Length];
            st.nata = new Sprite[ex.Length];
            st.pipeHardcore = new Sprite[ex.Length];
            st.nataHardcore = new Sprite[ex.Length];
            for (int i = 0; i < ex.Length; i++)
            {
                st.pipe[i] = S($"Characters/Staff/pipe_{ex[i]}.png");
                st.nata[i] = S($"Characters/Staff/nata_{ex[i]}.png");
                st.pipeHardcore[i] = S($"Characters/Staff/pipe_hc_{ex[i]}.png");
                st.nataHardcore[i] = S($"Characters/Staff/nata_hc_{ex[i]}.png");
            }
            EditorUtility.SetDirty(st);
            return st;
        }

        static SfxLibrary CreateSfxLibrary()
        {
            var lib = LoadOrCreate<SfxLibrary>(AudioData + "SfxLibrary.asset");
            var old = new Dictionary<Sfx, SfxEntry>();
            foreach (var e in lib.entries) old[e.id] = e;
            lib.entries.Clear();
            foreach (Sfx id in Enum.GetValues(typeof(Sfx)))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/" + Snake(id.ToString()) + ".wav");
                if (clip == null) Debug.LogWarning("[Bar del Piu] Falta audio: " + id);
                // Conserva los ajustes hechos a mano en el Inspector
                var e = old.TryGetValue(id, out var prev) ? prev : DefaultEntry(id);
                e.id = id;
                e.clips = clip ? new[] { clip } : Array.Empty<AudioClip>();
                lib.entries.Add(e);
            }
            EditorUtility.SetDirty(lib);
            return lib;
        }

        static SfxEntry DefaultEntry(Sfx id)
        {
            var e = new SfxEntry { id = id };
            switch (id)
            {
                case Sfx.UiClick: case Sfx.UiHover: case Sfx.UiBack: case Sfx.CountdownBeep: case Sfx.CountdownGo:
                    e.bus = AudioBus.Ui; break;
                case Sfx.DialogBlip:
                    e.bus = AudioBus.Ui; e.cooldown = 0.02f; e.maxVoices = 2; e.volume = 0.7f; break;
                case Sfx.PourLoop: case Sfx.FoamFizzLoop:
                    e.pitch = Vector2.one; e.volume = 0.8f; break;
                case Sfx.ComboUp: case Sfx.ServePerfect: case Sfx.ServeGood: case Sfx.ServeRegular: case Sfx.ServeBad:
                case Sfx.GameOver: case Sfx.NewRecord: case Sfx.TimerTick:
                    e.pitch = Vector2.one; break;
                case Sfx.Error:
                    e.volume = 0.75f; e.pitch = Vector2.one; break;
                case Sfx.CustomerArrive:
                    e.volume = 0.6f; e.cooldown = 0.25f; break;
                case Sfx.TipCoin:
                    e.pitch = new Vector2(0.95f, 1.08f); break;
            }
            return e;
        }

        static MusicLibrary CreateMusicLibrary()
        {
            var lib = LoadOrCreate<MusicLibrary>(AudioData + "MusicLibrary.asset");
            AudioClip A(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + n);
            lib.tracks.Clear();
            lib.tracks.Add(new MusicEntry { track = MusicTrack.Menu, main = A("Music/music_menu.wav"), volume = 0.9f });
            lib.tracks.Add(new MusicEntry
            {
                track = MusicTrack.Game, main = A("Music/music_game.wav"), rush = A("Music/music_game_rush.wav"), volume = 0.8f,
            });
            lib.tracks.Add(new MusicEntry { track = MusicTrack.Results, main = A("Music/music_results.wav"), loop = false });
            lib.tracks.Add(new MusicEntry { track = MusicTrack.Birthday, main = A("Music/music_birthday.wav"), loop = false });
            lib.tracks.Add(new MusicEntry { track = MusicTrack.Hardcore, main = A("Music/music_hardcore.wav"), volume = 0.85f });
            lib.tracks.Add(new MusicEntry { track = MusicTrack.BirthdayMetal, main = A("Music/music_birthday_metal.wav"), loop = false });
            lib.ambience = A("Ambience/ambience_bar.wav");
            EditorUtility.SetDirty(lib);
            return lib;
        }

        static void CreateAudioManagerPrefab(SfxLibrary sfx, MusicLibrary music)
        {
            var go = new GameObject("AudioManager");
            var am = go.AddComponent<AudioManager>();
            am.sfx = sfx;
            am.music = music;
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/Resources/BarDelPiu/AudioManager.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        static Dictionary<FxKind, ParticleSystem> CreateFxPrefabs(Mats m)
        {
            var result = new Dictionary<FxKind, ParticleSystem>();
            void Make(FxKind k, Material mat, params string[] sprites)
            {
                var arr = Array.ConvertAll(sprites, s => S("FX/" + s));
                var ps = FxBuilder.Build(k, arr, mat);
                string path = PrefabDir + "FX/FX_" + k + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(ps.gameObject, path);
                UnityEngine.Object.DestroyImmediate(ps.gameObject);
                result[k] = prefab.GetComponent<ParticleSystem>();
            }
            Make(FxKind.Sparkles, m.glowFx, "fx_sparkle_0.png", "fx_sparkle_1.png", "fx_sparkle_2.png");
            Make(FxKind.FoamBurst, m.unlit, "fx_foam.png", "fx_bubble.png");
            Make(FxKind.Coins, m.unlit, "fx_coin_0.png", "fx_coin_1.png", "fx_coin_2.png", "fx_coin_3.png");
            Make(FxKind.Puff, m.unlit, "fx_puff.png");
            Make(FxKind.Hearts, m.unlit, "fx_heart.png");
            Make(FxKind.PourSplash, m.unlit, "fx_drop.png", "fx_foam.png");
            return result;
        }

        static VolumeProfile CreatePostProfile()
        {
            const string path = "Assets/Settings/BarPostFX.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, path);
            }
            T Comp<T>() where T : VolumeComponent
            {
                if (!p.TryGet<T>(out var c))
                {
                    c = p.Add<T>(true);
                    c.name = typeof(T).Name;
                    AssetDatabase.AddObjectToAsset(c, p);
                }
                return c;
            }
            var bloom = Comp<Bloom>();
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.85f, 0.75f));
            var vig = Comp<Vignette>();
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(BarPalette.Ink);
            var ca = Comp<ColorAdjustments>();
            ca.contrast.Override(6f);
            ca.saturation.Override(4f);
            ca.colorFilter.Override(new Color(1f, 0.97f, 0.92f));
            EditorUtility.SetDirty(p);
            return p;
        }

        // ------------------------------------------------------------------ escenario compartido

        class Backdrop
        {
            public BarScene bar;
            public CameraShake shake;
            public Transform hud;
            public FeedbackDirector feedback;
        }

        static Backdrop BuildBackdrop(Mats m, PixelFont font, CustomerArt customerArt, StaffArt staffArt,
            Dictionary<FxKind, ParticleSystem> fx, VolumeProfile profile, float pipeX = 84, float nataX = 236, bool savePrefabs = false)
        {
            // Cámara pixel perfect 320x180
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 180f / 2f / 16f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BarPalette.Ink;
            cam.allowHDR = true;
            cam.allowMSAA = false;
            cam.allowDynamicResolution = false;
            camGo.AddComponent<AudioListener>();
            var urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = true;
            urpCam.antialiasing = AntialiasingMode.None;
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = 16;
            ppc.refResolutionX = 320;
            ppc.refResolutionY = 180;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.StretchFill; // siempre muestra 320x180 completo, a cualquier tamaño de pantalla
            var shake = camGo.AddComponent<CameraShake>();

            var volGo = new GameObject("PostFX");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            // Luces
            var lights = new GameObject("Lights").transform;
            var global = Light("Global", lights, Vector3.zero, new Color(1f, 0.9f, 0.78f), 0.8f, 0, Light2D.LightType.Global);

            // Fondo y barra
            var world = new GameObject("Bar").transform;
            var bar = world.gameObject.AddComponent<BarScene>();
            bar.cam = cam;
            bar.globalLight = global;
            bar.staff = staffArt;
            bar.lampBroken = S("Bar/lamp_broken.png");
            bar.lampSpriteOn = S("Bar/lamp.png");
            bar.lampSpriteOff = S("Bar/lamp_off.png");
            SR("Background", world, S("Background/bg_bar_room.png"), Vector3.zero, SortOrder.Background);
            // Modo Hardcore: carteles de bandas y grafitis sobre la pared (se activan con BarScene.SetHardcoreLook)
            bar.hardcoreDecor = SR("HardcoreDecor", world, S("Background/bg_hardcore_decor.png"), Vector3.zero, SortOrder.Background + 5);
            bar.neonHardcoreOn = S("Bar/neon_hc_on.png");
            bar.neonHardcoreOff = S("Bar/neon_hc_off.png");
            SR("Underbar", world, S("Bar/underbar.png"), Scr(160, 44), SortOrder.Underbar);
            SR("Counter", world, S("Bar/counter.png"), Scr(160, 56), SortOrder.Counter);

            var neon = SR("NeonSign", world, S("Bar/neon_sign_on.png"), Scr(160, 146), SortOrder.WallDecor, m.glowNeon);
            var neonFlicker = neon.gameObject.AddComponent<NeonFlicker>();
            neonFlicker.sign = neon;
            neonFlicker.on = S("Bar/neon_sign_on.png");
            neonFlicker.off = S("Bar/neon_sign_off.png");
            neonFlicker.glow = Light("NeonLight", neon.transform, Vector3.zero, BarPalette.Neon, 0.9f, 6f);
            bar.neon = neonFlicker;

            var open = SR("NeonOpen", world, S("Bar/neon_open_on.png"), Scr(270, 141), SortOrder.WallDecor, m.glowNeon);
            var openFlicker = open.gameObject.AddComponent<NeonFlicker>();
            openFlicker.sign = open;
            openFlicker.on = S("Bar/neon_open_on.png");
            openFlicker.off = S("Bar/neon_open_off.png");
            openFlicker.glow = Light("OpenLight", open.transform, Vector3.zero, BarPalette.Mint, 0.6f, 3.5f);
            openFlicker.secondsBetweenFlickers = new Vector2(7f, 16f);
            bar.openSign = openFlicker;

            var lampXs = new[] { 64f, 256f };
            bar.lamps = new SpriteRenderer[lampXs.Length];
            bar.lampLights = new Light2D[lampXs.Length + 1];
            for (int i = 0; i < lampXs.Length; i++)
            {
                bar.lamps[i] = SR("Lamp", world, S("Bar/lamp.png"), Scr(lampXs[i], 186), SortOrder.WallDecor + 1, m.glowLamp);
                bar.lampLights[i] = Light("LampLight", lights, Scr(lampXs[i], 158), new Color(1f, 0.78f, 0.45f), 1.1f, 7f);
            }
            bar.lampLights[lampXs.Length] = Light("TapLight", lights, Scr(48, 100), new Color(1f, 0.82f, 0.55f), 0.7f, 4f);

            // Torre de llaves y vaso
            // Torre ancha: 24 px entre llaves para que cada columna se pueda tocar con el dedo en celular
            var towerMeta = JsonUtility.FromJson<TowerMeta>(File.ReadAllText(Sprites + "Taps/tap_tower.json"));
            var tower = SR("TapTower", world, S("Taps/tap_tower.png"), Scr(48, 50), SortOrder.Props);
            string[] keys = { "rubia", "ambar", "negra" };
            var taps = new TapVisual[3];
            for (int i = 0; i < 3; i++)
            {
                var tapGo = new GameObject("Tap_" + keys[i]);
                tapGo.transform.SetParent(tower.transform, false);
                tapGo.transform.localPosition = new Vector3(towerMeta.SpoutX(i) / 16f, 0f, 0f);
                var col = tapGo.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                float colH = towerMeta.HandleY(i) + towerMeta.handle_h_px;
                col.size = new Vector2(towerMeta.column_w_px, colH) / 16f;
                col.offset = new Vector2(0f, colH / 2f) / 16f;
                var tap = tapGo.AddComponent<TapVisual>();
                tap.kind = (BeerKind)i;
                tap.hitArea = col;
                tap.handle = SR("Handle", tapGo.transform, S($"Taps/handle_{keys[i]}_idle.png"), new Vector3(0, towerMeta.HandleY(i) / 16f, 0),
                    SortOrder.Props + 1);
                tap.idle = S($"Taps/handle_{keys[i]}_idle.png");
                tap.selected = S($"Taps/handle_{keys[i]}_sel.png");
                tap.pulled = S($"Taps/handle_{keys[i]}_pull.png");
                tap.hint = S($"Taps/handle_{keys[i]}_hint.png");
                var nozzle = new GameObject("Nozzle").transform;
                nozzle.SetParent(tapGo.transform, false);
                nozzle.localPosition = new Vector3(0, towerMeta.SpoutY(i) / 16f, 0);
                tap.nozzle = nozzle;
                taps[i] = tap;
            }
            bar.taps = taps;

            var glass = BuildGlass(world, m, fx);
            glass.transform.position = Scr(48 + towerMeta.SpoutX(0), 55);
            var hit = glass.gameObject.AddComponent<BoxCollider2D>();
            hit.size = new Vector2(1.5f, 2.9f); // más grande que el vaso: dedo en celular
            hit.offset = new Vector2(0, 0.95f);
            hit.isTrigger = true;
            bar.glass = glass;
            bar.glassHitArea = hit;
            glass.focus = BuildPourFocus(world, glass, m, font);
            glass.mirrorLiquids = new[] { glass.focus.bigLiquid };
            if (savePrefabs)
            {
                SavePrefab(glass.gameObject, "Glass");
                SavePrefab(tower.gameObject, "TapTower");
            }

            // Pipe y Nata (bustos detrás de la barra)
            bar.pipe = SR("Pipe", world, staffArt.pipe[1], Scr(pipeX, 46), SortOrder.Customers + 5);
            bar.nata = SR("Nata", world, staffArt.nata[1], Scr(nataX, 46), SortOrder.Customers + 5);
            bar.pipePunk = SR("PunkLook", bar.pipe.transform, S("Characters/Staff/pipe_punk.png"), Vector3.zero, SortOrder.Customers + 6);
            bar.nataPunk = SR("PunkLook", bar.nata.transform, S("Characters/Staff/nata_punk.png"), Vector3.zero, SortOrder.Customers + 6);

            // Clientes
            var customersRoot = new GameObject("Customers").transform;
            var customers = new CustomerVisual[4];
            float[] slots = { 118, 168, 218, 268 };
            for (int i = 0; i < 4; i++)
            {
                customers[i] = BuildCustomer(customersRoot, customerArt, m, i);
                customers[i].transform.position = Scr(slots[i], 46);
            }
            if (savePrefabs) SavePrefab(customers[0].gameObject, "Customer");
            bar.customers = customers;

            // Diálogo
            var hud = new GameObject("HUD").transform;
            var dialogue = BuildDialogue(hud, font, staffArt, m);
            dialogue.transform.position = Scr(0, 0);
            if (savePrefabs) SavePrefab(dialogue.gameObject, "DialogueBox");
            bar.dialogue = dialogue;

            // Feedback
            var fbGo = new GameObject("FeedbackDirector");
            var fb = fbGo.AddComponent<FeedbackDirector>();
            fb.font = font;
            fb.textMaterial = m.unlit;
            fb.cameraShake = shake;
            fb.sparkles = fx[FxKind.Sparkles];
            fb.foamBurst = fx[FxKind.FoamBurst];
            fb.coins = fx[FxKind.Coins];
            fb.puff = fx[FxKind.Puff];
            fb.hearts = fx[FxKind.Hearts];

            return new Backdrop { bar = bar, shake = shake, hud = hud, feedback = fb };
        }

        // ------------------------------------------------------------------ escena de prueba / juego

        static void BuildTestScene(Mats m, PixelFont font, CustomerArt customerArt, StaffArt staffArt,
            Dictionary<FxKind, ParticleSystem> fx, VolumeProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var b = BuildBackdrop(m, font, customerArt, staffArt, fx, profile, savePrefabs: true);
            var hud = b.hud;
            b.bar.pipe.gameObject.SetActive(false);
            b.bar.nata.gameObject.SetActive(false);

            var leftPanel = SR("ScorePanel", hud, S("UI/panel_dark.png"), Scr(46, 171), SortOrder.Ui, m.unlit);
            leftPanel.drawMode = SpriteDrawMode.Sliced;
            leftPanel.size = new Vector2(80f / 16f, 16f / 16f);
            SR("CoinIcon", hud, S("UI/icon_coin.png"), Scr(16, 171), SortOrder.Ui + 1, m.unlit);
            var score = Text("Score", hud, Scr(26, 168), font, m.unlit, "000000", BarPalette.AmberLight, PixelText.Align.Left, 1);

            var timePanel = SR("TimePanel", hud, S("UI/panel_dark.png"), Scr(160, 171), SortOrder.Ui, m.unlit);
            timePanel.drawMode = SpriteDrawMode.Sliced;
            timePanel.size = new Vector2(50f / 16f, 16f / 16f);
            SR("ClockIcon", hud, S("UI/icon_clock.png"), Scr(144, 171), SortOrder.Ui + 1, m.unlit);
            var timer = Text("Timer", hud, Scr(166, 168), font, m.unlit, "1:30", BarPalette.Cream, PixelText.Align.Center, 1);
            SR("FlameIcon", hud, S("UI/icon_flame.png"), Scr(196, 171), SortOrder.Ui + 1, m.unlit);
            var combo = Text("Combo", hud, Scr(204, 168), font, m.unlit, "", BarPalette.Neon, PixelText.Align.Left, 1);

            var errors = BuildErrorIcons(hud, m);

            var ctrl = new GameObject("ArtAudioTest").AddComponent<ArtAudioTestController>();
            ctrl.cam = b.bar.cam;
            ctrl.taps = b.bar.taps;
            ctrl.glass = b.bar.glass;
            ctrl.glassHitArea = b.bar.glassHitArea;
            ctrl.customers = b.bar.customers;
            ctrl.dialogue = b.bar.dialogue;
            ctrl.scoreText = score;
            ctrl.timerText = timer;
            ctrl.comboText = combo;
            ctrl.errorIcons = errors;
            ctrl.errorEmpty = S("UI/icon_error_empty.png");
            ctrl.errorFull = S("UI/icon_error_full.png");
            ctrl.urgentArrow = SR("UrgentArrow", hud, S("UI/arrow_down.png"), Vector3.zero, SortOrder.CustomerUi + 10, m.unlit);
            ctrl.screenFlash = SR("ScreenFlash", hud, S("UI/px_white.png"), new Vector3(-12f, -7f, 0f), SortOrder.Overlay + 40, m.unlit);
            ctrl.screenFlash.transform.localScale = new Vector3(24 * 16, 14 * 16, 1);
            ctrl.screenFlash.enabled = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static SpriteRenderer[] BuildErrorIcons(Transform hud, Mats m)
        {
            var errPanel = SR("ErrorPanel", hud, S("UI/panel_dark.png"), Scr(290, 171), SortOrder.Ui, m.unlit);
            errPanel.drawMode = SpriteDrawMode.Sliced;
            errPanel.size = new Vector2(46f / 16f, 16f / 16f);
            var errors = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
                errors[i] = SR("Error" + i, errPanel.transform, S("UI/icon_error_empty.png"),
                    Scr(277 + i * 13, 171) - Scr(290, 171), SortOrder.Ui + 1, m.unlit);
            return errors;
        }

        // ------------------------------------------------------------------ pantallas

        static UiSkin CreateUiSkin(Mats m, PixelFont font)
        {
            var skin = LoadOrCreate<UiSkin>(Data + "UiSkin.asset");
            skin.font = font;
            skin.unlit = m.unlit;
            skin.buttonNormal = S("UI/button_normal.png");
            skin.buttonPressed = S("UI/button_pressed.png");
            skin.panelCream = S("UI/panel_cream.png");
            skin.panelDark = S("UI/panel_dark.png");
            skin.arrowDown = S("UI/arrow_down.png");
            skin.soundOn = S("UI/icon_sound_on.png");
            skin.soundOff = S("UI/icon_sound_off.png");
            skin.pixel = S("UI/px_white.png");
            skin.gear = S("UI/icon_gear.png");
            skin.heart = S("UI/icon_heart.png");
            skin.cake = S("Party/cake.png");
            skin.candleFlame = new[] { S("Party/flame_0.png"), S("Party/flame_1.png") };
            skin.hatRed = S("Party/hat_red.png");
            skin.hatMint = S("Party/hat_mint.png");
            skin.confetti = S("Party/confetti.png");
            EditorUtility.SetDirty(skin);
            return skin;
        }

        static void BuildMenuScene(Mats m, PixelFont font, CustomerArt ca, StaffArt st, Dictionary<FxKind, ParticleSystem> fx,
            VolumeProfile profile, UiSkin skin)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var b = BuildBackdrop(m, font, ca, st, fx, profile, pipeX: 284, nataX: 230); // a la derecha: la torre de llaves tapaba a Pipe
            b.bar.neon.gameObject.SetActive(false); // el logo ocupa el lugar del letrero
            b.bar.glass.focus.gameObject.SetActive(false);
            b.bar.glass.focus = null;
            b.bar.neon = null;
            b.bar.openSign.gameObject.SetActive(false); // asomaba detrás del logo
            b.bar.openSign = null;
            var ctrl = new GameObject("MainMenu").AddComponent<MainMenuController>();
            ctrl.bar = b.bar;
            ctrl.skin = skin;
            ctrl.logo = SR("Logo", ctrl.transform, S("UI/logo.png"), Scr(160, 140), SortOrder.Ui, m.unlit);
            ctrl.recordText = Text("Record", ctrl.transform, Scr(250, 22), font, m.unlit, "RÉCORD: 0", BarPalette.AmberLight,
                PixelText.Align.Center);
            var lb = new GameObject("Leaderboard").AddComponent<LeaderboardView>();
            lb.transform.SetParent(ctrl.transform, false);
            lb.transform.position = Scr(160, 174);
            lb.skin = skin;
            lb.rows = 10;
            ctrl.leaderboard = lb;
            // Llaves que funcionan, cervezas para Pipe y Nata y ampolletas que se rompen
            var play = ctrl.gameObject.AddComponent<MenuPlayground>();
            play.bar = b.bar;
            play.lampBroken = S("Bar/lamp_broken.png");
            ctrl.playground = play;
            ctrl.hardcoreStamp = SR("HardcoreStamp", ctrl.transform, S("UI/stamp_hardcore.png"), Scr(244, 104), SortOrder.Ui + 20, m.unlit);
            ctrl.idleWatcher = ctrl.gameObject.AddComponent<IdleWatcher>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Menu.unity");
        }

        static void BuildIntroScene(Mats m, PixelFont font, CustomerArt ca, StaffArt st, Dictionary<FxKind, ParticleSystem> fx,
            VolumeProfile profile, UiSkin skin)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var b = BuildBackdrop(m, font, ca, st, fx, profile, pipeX: 132, nataX: 196);
            var d = new GameObject("IntroTutorial").AddComponent<IntroTutorialDirector>();
            d.bar = b.bar;
            d.skin = skin;
            d.caption = Text("Caption", d.transform, Scr(160, 100), font, m.unlit, "", BarPalette.Cream, PixelText.Align.Center, 1);
            d.caption.sortingOrder = SortOrder.Overlay + 5;
            d.arrow = SR("Arrow", d.transform, S("UI/arrow_down.png"), Vector3.zero, SortOrder.Overlay + 6, m.unlit);
            d.errorIcons = BuildErrorIcons(b.hud, m);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Intro.unity");
        }

        static void BuildResultsScene(Mats m, PixelFont font, CustomerArt ca, StaffArt st, Dictionary<FxKind, ParticleSystem> fx,
            VolumeProfile profile, UiSkin skin)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var b = BuildBackdrop(m, font, ca, st, fx, profile, pipeX: 84, nataX: 272);
            b.bar.glass.gameObject.SetActive(false);
            var r = new GameObject("Results").AddComponent<ResultsController>();
            r.bar = b.bar;
            r.skin = skin;
            r.title = Text("Title", r.transform, Scr(160, 126), font, m.unlit, "¡CERRAMOS!", BarPalette.AmberLight, PixelText.Align.Center, 2);
            r.scoreText = Text("Score", r.transform, Scr(160, 96), font, m.unlit, "0", BarPalette.Cream, PixelText.Align.Center, 3);
            r.recordText = Text("Record", r.transform, Scr(160, 80), font, m.unlit, "", BarPalette.CreamDark, PixelText.Align.Center, 1);
            foreach (var t in new[] { r.title, r.scoreText, r.recordText }) t.sortingOrder = SortOrder.Overlay + 5;
            var entry = new GameObject("NicknameEntry").AddComponent<NicknameEntry>();
            entry.transform.SetParent(r.transform, false);
            entry.transform.position = Scr(160, 172);
            entry.skin = skin;
            r.nicknameEntry = entry;
            var lb = new GameObject("Leaderboard").AddComponent<LeaderboardView>();
            lb.transform.SetParent(r.transform, false);
            lb.transform.position = Scr(160, 174);
            lb.skin = skin;
            r.leaderboard = lb;
            r.idleWatcher = r.gameObject.AddComponent<IdleWatcher>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Results.unity");
        }

        // ------------------------------------------------------------------ pantalla de espera

        [Serializable] class ExteriorMeta { public int[] lamp_px, sign_px; }

        static Camera BuildPixelCamera(VolumeProfile profile)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 180f / 2f / 16f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BarPalette.Ink;
            cam.allowHDR = true;
            cam.allowMSAA = false;
            camGo.AddComponent<AudioListener>();
            var urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = true;
            urpCam.antialiasing = AntialiasingMode.None;
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = 16;
            ppc.refResolutionX = 320;
            ppc.refResolutionY = 180;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.StretchFill; // siempre muestra 320x180 completo, a cualquier tamaño de pantalla
            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
            return cam;
        }

        static void BuildIdleScene(Mats m, PixelFont font, VolumeProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildPixelCamera(profile);
            var meta = JsonUtility.FromJson<ExteriorMeta>(File.ReadAllText(Sprites + "Exterior/exterior.json"));
            Vector3 P(float x, float y) => new Vector3(x / 16f, y / 16f, 0);

            var lights = new GameObject("Lights").transform;
            var night = Light("Night", lights, Vector3.zero, new Color(0.7f, 0.75f, 1f), 0.65f, 0, Light2D.LightType.Global);
            Light("StreetLamp", lights, P(meta.lamp_px[0], meta.lamp_px[1]), new Color(1f, 0.85f, 0.6f), 1.2f, 6f);
            var warm = new[]
            {
                Light("WindowL", lights, P(-74, -24), new Color(1f, 0.75f, 0.4f), 1.0f, 4.5f),
                Light("WindowR", lights, P(74, -24), new Color(1f, 0.75f, 0.4f), 1.0f, 4.5f),
                Light("Door", lights, P(0, -36), new Color(1f, 0.78f, 0.45f), 0.8f, 3.5f),
            };

            var street = new GameObject("Street").transform;
            SR("Exterior", street, S("Exterior/bg_exterior.png"), Vector3.zero, SortOrder.Background);
            var hcDecor = SR("HardcoreDecor", street, S("Exterior/exterior_hardcore_decor.png"), Vector3.zero, SortOrder.Background + 5);

            var neon = SR("NeonSign", street, S("Bar/neon_sign_on.png"), P(0, 72), SortOrder.WallDecor, m.glowNeon);
            var flicker = neon.gameObject.AddComponent<NeonFlicker>();
            flicker.sign = neon;
            flicker.on = S("Bar/neon_sign_on.png");
            flicker.off = S("Bar/neon_sign_off.png");
            flicker.glow = Light("NeonLight", neon.transform, Vector3.zero, BarPalette.Neon, 1.0f, 6f);

            var logo = SR("LogoKid", street, S("Exterior/logo_kid.png"), P(meta.sign_px[0], meta.sign_px[1]), SortOrder.WallDecor + 1);
            Light("LogoLight", logo.transform, Vector3.zero, new Color(1f, 0.9f, 0.7f), 0.7f, 3f);

            // Autos: ciclo de 20 s (dos carriles, ambos sentidos)
            var loop = new GameObject("Cars").AddComponent<CarLoop>();
            loop.loopSeconds = 20f;
            CarLoop.Car Car(string name, float start, float duration, bool ltr, float laneY, int order)
            {
                var frames = new[] { S($"Exterior/car_{name}_0.png"), S($"Exterior/car_{name}_1.png") };
                var sr = SR("Car_" + name, loop.transform, frames[0], Vector3.zero, order);
                sr.enabled = false;
                return new CarLoop.Car { renderer = sr, frames = frames, start = start, duration = duration, leftToRight = ltr, laneY = laneY };
            }
            loop.cars = new[]
            {
                Car("red", 0.5f, 3.6f, true, -88f / 16f, SortOrder.Props + 2),
                Car("taxi", 5.5f, 4.4f, false, -78f / 16f, SortOrder.Props),
                Car("van", 11f, 5.2f, true, -88f / 16f, SortOrder.Props + 2),
                Car("red", 15.5f, 3.4f, false, -78f / 16f, SortOrder.Props),
            };

            var ctrl = new GameObject("IdleScreen").AddComponent<IdleScreenController>();
            ctrl.logo = logo;
            ctrl.hardcoreDecor = hcDecor;
            ctrl.neon = flicker;
            ctrl.neonHardcoreOn = S("Bar/neon_hc_on.png");
            ctrl.neonHardcoreOff = S("Bar/neon_hc_off.png");
            ctrl.nightLight = night;
            ctrl.warmLights = warm;
            ctrl.cars = loop;
            ctrl.motoFrames = new[] { S("Exterior/moto_0.png"), S("Exterior/moto_1.png") };
            ctrl.prompt = Text("Prompt", ctrl.transform, P(0, -63), font, m.unlit, "TOCA LA PANTALLA PARA ENTRAR",
                BarPalette.Cream, PixelText.Align.Center);
            ctrl.prompt.sortingOrder = SortOrder.Overlay;
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Espera.unity");
        }

        static void SetBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Menu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Intro.unity", true),
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/Results.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Espera.unity", true),
            };
        }

        static BeerGlassVisual BuildGlass(Transform parent, Mats m, Dictionary<FxKind, ParticleSystem> fx)
        {
            var go = new GameObject("Glass");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<BeerGlassVisual>();
            g.back = SR("Back", go.transform, S("Glass/glass_back.png"), Vector3.zero, SortOrder.Props + 2);
            g.liquid = SR("Liquid", go.transform, S("Glass/glass_mask.png"), Vector3.zero, SortOrder.Props + 3, m.beer);
            g.front = SR("Front", go.transform, S("Glass/glass_front.png"), Vector3.zero, SortOrder.Props + 4);
            var json = JsonUtility.FromJson<GlassMeta>(File.ReadAllText(Sprites + "Glass/glass.json"));
            g.innerBottomPx = json.innerBottomPx;
            g.innerTopPx = json.innerTopPx;

            var streamSr = SR("PourStream", go.transform, S("Glass/pour_stream_0.png"), Vector3.zero, SortOrder.Props + 1, m.unlit);
            streamSr.drawMode = SpriteDrawMode.Tiled;
            streamSr.size = new Vector2(2f / 16f, 1f / 16f);
            var stream = streamSr.gameObject.AddComponent<BeerPourStream>();
            stream.frames = new[] { S("Glass/pour_stream_0.png"), S("Glass/pour_stream_1.png"), S("Glass/pour_stream_2.png") };
            g.stream = stream;

            var splash = (ParticleSystem)PrefabUtility.InstantiatePrefab(fx[FxKind.PourSplash], go.transform);
            g.splash = splash;
            return g;
        }

        static CustomerVisual BuildCustomer(Transform parent, CustomerArt art, Mats m, int index)
        {
            var go = new GameObject("Customer_" + index);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CustomerVisual>();
            c.art = art;
            c.body = SR("Body", go.transform, art.variants[index * 2 % art.variants.Count].neutral, Vector3.zero, SortOrder.Customers);
            var ui = new GameObject("UI").transform;
            ui.SetParent(go.transform, false);
            c.patienceFrame = SR("PatienceFrame", ui, S("UI/patience_frame.png"), new Vector3(0, 55f / 16f, 0), SortOrder.CustomerUi, m.unlit);
            c.patienceFill = SR("PatienceFill", ui, S("UI/px_white.png"), new Vector3(-9f / 16f, 56f / 16f, 0), SortOrder.CustomerUi + 1, m.unlit);
            c.patienceFill.transform.localScale = new Vector3(18, 3, 1);
            c.orderBubble = SR("OrderBubble", ui, art.orderRubia, new Vector3(4f / 16f, 61f / 16f, 0), SortOrder.CustomerUi + 2, m.unlit);
            return c;
        }

        static DialogueBox BuildDialogue(Transform parent, PixelFont font, StaffArt staff, Mats m)
        {
            var go = new GameObject("DialogueBox");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DialogueBox>();
            d.staff = staff;
            var px = 1f / 16f;
            d.panel = SR("Panel", go.transform, S("UI/panel_cream.png"), new Vector3(91 * px, 26 * px, 0), SortOrder.Dialogue, m.unlit);
            d.panel.drawMode = SpriteDrawMode.Sliced;
            d.panel.size = new Vector2(170 * px, 48 * px);
            d.portrait = SR("Portrait", go.transform, staff.pipe[0], new Vector3(28 * px, 2 * px, 0), SortOrder.Dialogue + 2, m.unlit);
            d.nameText = Text("Name", go.transform, new Vector3(56 * px, 39 * px, 0), font, m.unlit, "PIPE",
                BarPalette.GreenLight, PixelText.Align.Left, 1, false);
            d.bodyText = Text("Body", go.transform, new Vector3(56 * px, 27 * px, 0), font, m.unlit, "",
                BarPalette.CafeDark, PixelText.Align.Left, 1, false, 114);
            d.nameText.sortingOrder = d.bodyText.sortingOrder = SortOrder.Dialogue + 3;
            return d;
        }

        /// <summary>
        /// Capa oscura sobre los clientes + jarra x3 al centro mientras se sirve (se ve aunque el dedo tape el vaso).
        /// </summary>
        static PourFocus BuildPourFocus(Transform parent, BeerGlassVisual glass, Mats m, PixelFont font)
        {
            const int order = 250; // sobre clientes (100-150) y barra (200), bajo llaves y vaso real (300+)
            var go = new GameObject("PourFocus");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<PourFocus>();
            // Velo tramado sepia: los clientes quedan como una foto antigua punteada (x 92..324, y 50..164)
            f.overlay = SR("Veil", go.transform, S("UI/veil_dither.png"), Scr(208, 107), order, m.unlit);
            f.overlay.drawMode = SpriteDrawMode.Tiled;
            f.overlay.size = new Vector2(232, 114) / 16f;
            f.overlay.color = new Color(1f, 1f, 1f, 0f);
            f.overlayAlpha = 1f;
            var big = new GameObject("BigGlass").transform;
            big.SetParent(go.transform, false);
            big.position = Scr(208, 64);
            big.localScale = new Vector3(3, 3, 1);
            f.bigCard = SR("Card", go.transform, S("UI/panel_dark.png"), Scr(208, 106), order + 1, m.unlit);
            f.bigCard.drawMode = SpriteDrawMode.Sliced;
            f.bigCard.size = new Vector2(70, 96) / 16f;
            f.bigBack = SR("Back", big, S("Glass/glass_back.png"), Vector3.zero, order + 2, m.unlit);
            f.bigLiquid = SR("Liquid", big, S("Glass/glass_mask.png"), Vector3.zero, order + 3, m.beer);
            f.bigFront = SR("Front", big, S("Glass/glass_front.png"), Vector3.zero, order + 4, m.unlit);
            f.hint = Text("Hint", go.transform, Scr(208, 152), font, m.unlit, "¡SUELTA EN LA LÍNEA!", BarPalette.Cream,
                PixelText.Align.Center);
            f.hint.sortingOrder = order + 5;
            return f;
        }

        static void SavePrefab(GameObject go, string name)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabDir + name + ".prefab", InteractionMode.AutomatedAction);
        }
    }
}
