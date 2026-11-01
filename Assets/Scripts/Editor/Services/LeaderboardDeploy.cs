using System.Threading.Tasks;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;

namespace BarDelPiu.EditorTools
{
    /// <summary>
    /// Crea (o actualiza) en Unity Cloud la tabla del ranking a partir de Assets/Services/bar_del_piu_puntaje.lb.
    /// Equivale a abrir Services ▸ Deployment, marcar el archivo y presionar Deploy.
    /// </summary>
    public static class LeaderboardDeploy
    {
        const string LbPath = "Assets/Services/bar_del_piu_puntaje.lb";
        const string LogFile = "Logs/ranking_deploy.txt";

        static void Log(string msg, bool error = false)
        {
            if (error) Debug.LogError(msg);
            else Debug.Log(msg);
            try { System.IO.File.AppendAllText(LogFile, System.DateTime.Now.ToString("HH:mm:ss ") + msg + System.Environment.NewLine); }
            catch (System.Exception) { }
        }

        [MenuItem("Bar del Piu/Servicios/Crear tabla de ranking", priority = 20)]
        public static void Deploy() => _ = DeployAsync();

        [MenuItem("Bar del Piu/Servicios/Estado de Unity Cloud", priority = 21)]
        public static void Status()
        {
            var d = Deployments.Instance;
            Log($"[Ranking] Proyecto Unity Cloud: {CloudProjectSettings.projectId} ({CloudProjectSettings.projectName}), " +
                      $"usuario: {CloudProjectSettings.userName}, entorno: {d.EnvironmentProvider?.Current ?? "(desconocido)"}");
        }

        public static async Task DeployAsync()
        {
            Status();
            var env = Deployments.Instance.EnvironmentProvider?.Current;
            if (string.IsNullOrEmpty(env) || env == System.Guid.Empty.ToString())
            {
                Log("[Ranking] No hay entorno de Unity Cloud elegido. Ve a Edit ▸ Project Settings ▸ Services ▸ Environments, " +
                    "elige «production» y vuelve a usar este menú.", true);
                SettingsService.OpenProjectSettings("Project/Services/Environments");
                return;
            }
            var window = Deployments.Instance.DeploymentWindow;
            window?.OpenWindow(); // la API exige la ventana abierta
            if (window == null)
            {
                Log("[Ranking] La ventana de Deployment no está disponible. Abre Services ▸ Deployment una vez y reintenta.", true);
                return;
            }
            var items = window.GetFromFiles(new[] { LbPath });
            if (items.Count == 0)
            {
                Log("[Ranking] No se reconoció " + LbPath + " como recurso desplegable.", true);
                return;
            }
            Log("[Ranking] Desplegando la tabla «bar_del_piu_puntaje»...");
            try
            {
                await window.Deploy(items);
                bool ok = true;
                foreach (var item in items)
                {
                    bool failed = item.Status.MessageSeverity == SeverityLevel.Error;
                    ok &= !failed;
                    var msg = $"[Ranking] {(failed ? "Falló" : "OK")}: {item.Name} → {item.Status.Message} {item.Status.MessageDetail}";
                    Log(msg, failed);
                }
                if (ok) Log("[Ranking] ¡Tabla lista! Ya se pueden enviar puntajes.");
            }
            catch (System.Exception e)
            {
                Log("[Ranking] Error al desplegar: " + e, true);
            }
        }
    }
}
