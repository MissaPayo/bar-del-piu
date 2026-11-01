using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BarDelPiu.EditorTools
{
    /// <summary>
    /// Build WebGL lista para GitHub Pages en Build/WebGL.
    /// GitHub Pages no envía Content-Encoding para .gz/.br, por eso se usa Gzip con
    /// descompresión de respaldo (el navegador la descomprime en JavaScript).
    /// </summary>
    public static class WebGLBuilder
    {
        public const string OutputDir = "Build/WebGL";
        const string LogFile = "Logs/webgl_build.txt";

        [MenuItem("Bar del Piu/Publicar/Build WebGL", priority = 40)]
        public static void BuildFromMenu() => Build();

        public static bool Build()
        {
            File.WriteAllText(LogFile, "");
            Log("Configurando WebGL...");
            PlayerSettings.productName = "Bar del Piu";
            PlayerSettings.companyName = "Unravel Vision";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:BarDelPiu"; // Assets/WebGLTemplates/BarDelPiu: se adapta a la pantalla
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;

            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) scenes.Add(s.path);
            Log("Escenas: " + string.Join(", ", scenes));

            if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var sum = report.summary;
            Log($"Resultado: {sum.result} · {sum.totalSize / (1024f * 1024f):0.0} MB · {sum.totalTime.TotalSeconds:0} s · errores: {sum.totalErrors}");
            if (sum.result != BuildResult.Succeeded)
                foreach (var step in report.steps)
                    foreach (var m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception) Log("ERROR: " + m.content);
            return sum.result == BuildResult.Succeeded;
        }

        static void Log(string msg)
        {
            Debug.Log("[WebGL] " + msg);
            File.AppendAllText(LogFile, msg + System.Environment.NewLine);
        }
    }
}
