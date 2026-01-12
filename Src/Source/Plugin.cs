using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ValBridgeServer
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ValBridgeServerPlugin : BaseUnityPlugin
    {
        private const string ModName = "ValBridgeServer";
        private const string ModVersion = "1.0.0";
        private const string Author = "modAuthorName";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = BepInEx.Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        private readonly Harmony HarmonyInstance = new(ModGUID);

        public static readonly ManualLogSource TemplateLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        public void Awake()
        {
            // TODO: re-implement bepinex configuration for the mod
            // BepinexConfiguration.Instance.Config = Config;
            Assembly assembly = Assembly.GetExecutingAssembly();
            HarmonyInstance.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }
        
        private void SetupWatcher()
        {
            _lastReloadTime = DateTime.Now;
            FileSystemWatcher watcher = new(BepInEx.Paths.ConfigPath, ConfigFileName);
            // Due to limitations of technology this can trigger twice in a row
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
        }

        private DateTime _lastReloadTime;
        private const long RELOAD_DELAY = 10000000; // One second

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            var now = DateTime.Now;
            var time = now.Ticks - _lastReloadTime.Ticks;
            if (!File.Exists(ConfigFileFullPath) || time < RELOAD_DELAY) return;

            try
            {
                TemplateLogger.LogInfo("Attempting to reload configuration...");
                Config.Reload();
                TemplateLogger.LogInfo("Configuration reloaded successfully!");
            }
            catch
            {
                TemplateLogger.LogError($"There was an issue loading {ConfigFileName}");
                return;
            }

            _lastReloadTime = now;

            // Update any runtime configurations here
            if (ZNet.instance != null && !ZNet.instance.IsDedicated())
            {
                TemplateLogger.LogInfo("Updating runtime configurations...");
                // Add your configuration update logic here
            }
        }
    }
} 