using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Lib.GAB;
using Newtonsoft.Json;
using ValBridgeServer.Tools;

namespace ValBridgeServer
{
    /// <summary>
    /// GABP bridge configuration from environment variables or bridge.json
    /// </summary>
    public class BridgeConfig
    {
        [JsonProperty("port")]
        public int Port { get; set; }
        
        [JsonProperty("token")]
        public string Token { get; set; } = "";
        
        [JsonProperty("gameId")]
        public string GameId { get; set; } = "valheim";
    }

    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ValBridgeServerPlugin : BaseUnityPlugin
    {
        private const string ModName = "ValBridgeServer";
        private const string ModVersion = "1.0.0";
        private const string Author = "warpalicious";
        private const string ModGUID = Author + "." + ModName;

        public static readonly ManualLogSource ModLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        private GabpServer? _server;

        public void Start()
        {
            ModLogger.LogInfo($"{ModName} v{ModVersion} is loading...");

            try
            {
                // Read the bridge config (following GABP docs pattern)
                var config = ReadBridgeConfig();
                ModLogger.LogInfo($"GABP config loaded - GameId: {config.GameId}, Port: {config.Port}");

                // Create tools instance with [Tool] attributed methods
                var tools = new ValheimTools();

                // Start GABP server using external config (port/token from GABS)
                _server = Gabp.CreateServerWithInstanceAndExternalConfig(
                    "Valheim",
                    ModVersion,
                    tools,
                    config.Port,
                    config.Token,
                    config.GameId
                );

                // Register event channels
                _server.Events.RegisterChannel("player/death", "Player death events");
                _server.Events.RegisterChannel("player/health_changed", "Player health change events");

                // Start listening for GABS connections
                _server.StartAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        ModLogger.LogError($"GABP server failed to start: {task.Exception?.InnerException?.Message}");
                    }
                    else
                    {
                        ModLogger.LogInfo($"GABP server listening on 127.0.0.1:{_server.Port}");
                        ModLogger.LogInfo($"{ModName} loaded successfully!");
                    }
                });
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Failed to initialize GABP server: {ex.Message}");
                ModLogger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Read GABP server configuration following the priority from GABP docs:
        /// 1. Environment variables (recommended)
        /// 2. Bridge file path from GABS_BRIDGE_PATH env var
        /// 3. Legacy fallback to ~/.gabs/{gameId}/bridge.json
        /// </summary>
        private BridgeConfig ReadBridgeConfig()
        {
            // Method 1: Use environment variables (recommended)
            var gameId = Environment.GetEnvironmentVariable("GABS_GAME_ID");
            var portStr = Environment.GetEnvironmentVariable("GABP_SERVER_PORT");
            var token = Environment.GetEnvironmentVariable("GABP_TOKEN");

            if (!string.IsNullOrEmpty(portStr) && !string.IsNullOrEmpty(token) &&
                int.TryParse(portStr, out int port))
            {
                ModLogger.LogInfo("Config loaded from environment variables");
                return new BridgeConfig
                {
                    Port = port,
                    Token = token,
                    GameId = gameId ?? "valheim"
                };
            }

            // Method 2: Use bridge file path (fallback for debugging)
            var bridgePath = Environment.GetEnvironmentVariable("GABS_BRIDGE_PATH");
            if (!string.IsNullOrEmpty(bridgePath) && File.Exists(bridgePath))
            {
                ModLogger.LogInfo($"Config loaded from bridge file: {bridgePath}");
                var json = File.ReadAllText(bridgePath);
                return JsonConvert.DeserializeObject<BridgeConfig>(json)
                    ?? throw new Exception("Failed to parse bridge.json");
            }

            // Method 3: Legacy fallback to ~/.gabs/{gameId}/bridge.json
            var effectiveGameId = gameId ?? "valheim";
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var legacyPath = Path.Combine(homeDir, ".gabs", effectiveGameId, "bridge.json");

            if (File.Exists(legacyPath))
            {
                ModLogger.LogInfo($"Config loaded from legacy path: {legacyPath}");
                var json = File.ReadAllText(legacyPath);
                return JsonConvert.DeserializeObject<BridgeConfig>(json)
                    ?? throw new Exception("Failed to parse legacy bridge.json");
            }

            throw new Exception(
                "GABP server config not found. Ensure GABS is running and game was started via GABS. " +
                $"Expected: GABP_SERVER_PORT and GABP_TOKEN environment variables, or bridge.json at {legacyPath}");
        }

        private void OnDestroy()
        {
            ModLogger.LogInfo($"{ModName} is unloading...");

            if (_server != null)
            {
                try
                {
                    _server.StopAsync().ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            ModLogger.LogError($"Error stopping GABP server: {task.Exception?.InnerException?.Message}");
                        }
                        else
                        {
                            ModLogger.LogInfo("GABP server stopped");
                        }
                    });
                    _server.Dispose();
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"Error disposing GABP server: {ex.Message}");
                }
            }
        }
    }
}
