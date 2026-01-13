using System;
using BepInEx;
using BepInEx.Logging;
using Lib.GAB;
using Lib.GAB.Server;
using ValBridgeServer.Tools;

namespace ValBridgeServer
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ValBridgeServerPlugin : BaseUnityPlugin
    {
        private const string ModName = "ValBridgeServer";
        private const string ModVersion = "1.0.0";
        private const string Author = "warpalicious";
        private const string ModGUID = Author + "." + ModName;

        public static readonly ManualLogSource ModLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        private GabpServer? _server;

        public void Awake()
        {
            ModLogger.LogInfo($"{ModName} v{ModVersion} is loading...");

            try
            {
                var tools = new ValheimTools();
                _server = Gabp.CreateGabsAwareServerWithInstance("Valheim", ModVersion, tools);
                
                // Register event channels
                _server.Events.RegisterChannel("player/death", "Player death events");
                _server.Events.RegisterChannel("player/health_changed", "Player health change events");
                
                _server.StartAsync().Wait();
                
                ModLogger.LogInfo($"GABP server started on port {_server.Port}");
                ModLogger.LogInfo($"{ModName} loaded successfully!");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Failed to start GABP server: {ex.Message}");
                ModLogger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private void OnDestroy()
        {
            ModLogger.LogInfo($"{ModName} is unloading...");
            
            if (_server != null)
            {
                try
                {
                    _server.StopAsync().Wait();
                    _server.Dispose();
                    ModLogger.LogInfo("GABP server stopped successfully");
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"Error stopping GABP server: {ex.Message}");
                }
            }
        }
    }
}
 