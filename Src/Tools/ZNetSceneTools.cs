using System.Collections.Generic;
using System.Linq;
using Lib.GAB.Tools;
using UnityEngine;

namespace ValBridgeServer.Tools
{
    public class ZNetSceneTools
    {
        [Tool("znetscene_search_prefabs", Description = "Search for prefabs by name in ZNetScene")]
        public object SearchPrefabs(
            [ToolParameter(Description = "Name filter (partial match, case-insensitive)")] string nameFilter,
            [ToolParameter(Description = "Maximum number of results to return")] int maxResults = 50)
        {
            if (ZNetScene.instance == null)
                return new { success = false, error = "ZNetScene not available. Are you in-game?" };

            var prefabs = ZNetScene.instance.m_prefabs;
            if (prefabs.Count == 0)
                return new { success = false, error = "No prefabs loaded in ZNetScene" };

            var filter = nameFilter?.ToLowerInvariant() ?? "";
            var matches = new List<object>();

            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;
                if (string.IsNullOrEmpty(filter) || prefab.name.ToLowerInvariant().Contains(filter))
                {
                    matches.Add(SerializePrefab(prefab));
                    if (matches.Count >= maxResults) break;
                }
            }

            return new
            {
                success = true,
                totalPrefabs = prefabs.Count,
                matchCount = matches.Count,
                prefabs = matches
            };
        }

        [Tool("znetscene_get_prefab", Description = "Get a prefab by exact name from ZNetScene")]
        public object GetPrefab(
            [ToolParameter(Description = "Exact prefab name")] string prefabName)
        {
            if (ZNetScene.instance == null)
                return new { success = false, error = "ZNetScene not available. Are you in-game?" };

            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
                return new { success = false, error = $"Prefab '{prefabName}' not found" };

            return new { success = true, prefab = SerializePrefab(prefab, includeComponents: true) };
        }

        [Tool("znetscene_list_prefab_names", Description = "List all prefab names in ZNetScene")]
        public object ListPrefabNames(
            [ToolParameter(Description = "Optional prefix filter")] string? prefix = null,
            [ToolParameter(Description = "Maximum number of results")] int maxResults = 100)
        {
            if (ZNetScene.instance == null)
                return new { success = false, error = "ZNetScene not available. Are you in-game?" };

            var names = ZNetScene.instance.GetPrefabNames();
            if (names == null)
                return new { success = false, error = "Could not get prefab names" };

            var filtered = string.IsNullOrEmpty(prefix)
                ? names
                : names.Where(n => n.ToLowerInvariant().StartsWith(prefix!.ToLowerInvariant()));

            var result = filtered.Take(maxResults).ToList();

            return new
            {
                success = true,
                totalPrefabs = names.Count,
                returnedCount = result.Count,
                names = result
            };
        }

        private object SerializePrefab(GameObject prefab, bool includeComponents = false)
        {
            var result = new Dictionary<string, object?>
            {
                ["instanceId"] = prefab.GetInstanceID(),
                ["name"] = prefab.name,
                ["layer"] = LayerMask.LayerToName(prefab.layer),
                ["tag"] = prefab.tag,
                ["isStatic"] = prefab.isStatic,
                ["childCount"] = prefab.transform.childCount
            };

            if (includeComponents)
            {
                result["components"] = prefab.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => new Dictionary<string, object?>
                    {
                        ["instanceId"] = c.GetInstanceID(),
                        ["type"] = c.GetType().FullName,
                        ["typeName"] = c.GetType().Name,
                        ["enabled"] = (c is Behaviour b) ? b.enabled : (bool?)null
                    })
                    .ToList();
            }

            return result;
        }
    }
}
