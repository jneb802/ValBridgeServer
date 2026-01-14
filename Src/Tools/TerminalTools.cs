using System;
using System.Collections.Concurrent;
using Lib.GAB.Tools;
using UnityEngine;

namespace ValBridgeServer.Tools
{
    // Executes actions on Unity's main thread.
    // Required because GABP tool handlers run on background TCP threads,
    // but Unity GameObject operations must execute on the main thread.
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher? _instance;
        private static readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("ValBridgeServer_MainThreadDispatcher");
                    _instance = go.AddComponent<MainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        public void Enqueue(Action action) => _executionQueue.Enqueue(action);

        private void Update()
        {
            while (_executionQueue.TryDequeue(out var action))
                action.Invoke();
        }
    }

    public class TerminalTools
    {
        [Tool("run_command", Description = "Execute a Valheim console command (e.g. 'spawn Boar 1 1', 'god', 'heal')")]
        public object RunCommand(
            [ToolParameter(Description = "The console command to execute")] string command)
        {
            if (Console.instance == null)
                return new { success = false, error = "Console.instance is null" };

            MainThreadDispatcher.Instance.Enqueue(() =>
                Console.instance.TryRunCommand(command, silentFail: false, skipAllowedCheck: true));

            return new { success = true, command, queued = true };
        }
    }
}
