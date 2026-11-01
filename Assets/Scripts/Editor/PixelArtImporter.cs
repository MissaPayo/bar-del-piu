using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BarDelPiu.EditorTools
{
    /// <summary>
    /// Importa automáticamente el arte de Assets/Art/Sprites como pixel art (16 PPU, Point,
    /// sin compresión ni mipmaps) y aplica pivotes y bordes 9-slice desde sprite_meta.json.
    /// También configura el audio de Assets/Audio según su tipo.
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        const string SpriteRoot = "Assets/Art/Sprites/";
        const string AudioRoot = "Assets/Audio/";
        const float PPU = 16f;

        [System.Serializable]
        class MetaEntry
        {
            public int[] pivot;
            public int[] border;
        }

        static Dictionary<string, MetaEntry> _meta;
        static System.DateTime _metaStamp;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteRoot)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PPU;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;

            var size = ReadPngSize(assetPath);
            var meta = GetMeta(assetPath.Substring(SpriteRoot.Length));
            if (meta?.pivot != null && meta.pivot.Length == 2 && size.x > 0)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(meta.pivot[0] / (float)size.x, meta.pivot[1] / (float)size.y);
            }
            else settings.spriteAlignment = (int)SpriteAlignment.Center;
            if (meta?.border != null && meta.border.Length == 4)
                settings.spriteBorder = new Vector4(meta.border[0], meta.border[1], meta.border[2], meta.border[3]);
            ti.SetTextureSettings(settings);
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioRoot)) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            if (assetPath.Contains("/SFX/"))
            {
                ai.forceToMono = true;
                ai.loadInBackground = false;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
            }
            else
            {
                // Música y ambiente: comprimidos en memoria (WebGL no soporta streaming)
                ai.forceToMono = false;
                ai.loadInBackground = true;
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.5f;
            }
            s.preloadAudioData = true;
            ai.defaultSampleSettings = s;
        }

        static MetaEntry GetMeta(string rel)
        {
            string path = SpriteRoot + "sprite_meta.json";
            if (!File.Exists(path)) return null;
            var stamp = File.GetLastWriteTimeUtc(path);
            if (_meta == null || stamp != _metaStamp)
            {
                _meta = new Dictionary<string, MetaEntry>();
                _metaStamp = stamp;
                // JSON plano {"ruta": {"pivot": [x, y], "border": [l, b, r, t]}}
                var json = File.ReadAllText(path);
                int i = 0;
                while ((i = json.IndexOf('"', i)) >= 0)
                {
                    int end = json.IndexOf('"', i + 1);
                    string key = json.Substring(i + 1, end - i - 1);
                    int open = json.IndexOf('{', end);
                    int close = json.IndexOf('}', open);
                    var entry = JsonUtility.FromJson<MetaEntry>(json.Substring(open, close - open + 1));
                    _meta[key] = entry;
                    i = close + 1;
                }
            }
            _meta.TryGetValue(rel, out var e);
            return e;
        }

        static Vector2Int ReadPngSize(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var b = new byte[24];
                if (fs.Read(b, 0, 24) < 24) return default;
                int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                return new Vector2Int(w, h);
            }
            catch (IOException)
            {
                return default;
            }
        }
    }
}
