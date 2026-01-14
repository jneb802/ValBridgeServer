using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lib.GAB.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ValBridgeServer.Tools
{
    public class UnityExplorerTools
    {
        private readonly Assembly? _ueAssembly;
        private readonly Type? _searchProviderType;
        private readonly MethodInfo? _unityObjectSearchMethod;
        private readonly MethodInfo? _instanceSearchMethod;
        private readonly MethodInfo? _classSearchMethod;
        private readonly Type? _sceneHandlerType;
        private readonly PropertyInfo? _currentRootObjectsProperty;
        private readonly PropertyInfo? _loadedScenesProperty;
        private readonly PropertyInfo? _selectedSceneProperty;
        private readonly Type? _childFilterType;
        private readonly Type? _sceneFilterType;
        private static readonly Dictionary<int, WeakReference> _objectCache = new();
        
        public bool IsInitialized { get; }

        public UnityExplorerTools()
        {
            try
            {
                _ueAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .First(a => a.GetName().Name.StartsWith("UnityExplorer"));
                
                _searchProviderType = _ueAssembly.GetType("UnityExplorer.ObjectExplorer.SearchProvider");
                if (_searchProviderType != null)
                {
                    _unityObjectSearchMethod = _searchProviderType.GetMethod("UnityObjectSearch",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    _instanceSearchMethod = _searchProviderType.GetMethod("InstanceSearch",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    _classSearchMethod = _searchProviderType.GetMethod("ClassSearch",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }
                
                _sceneHandlerType = _ueAssembly.GetType("UnityExplorer.ObjectExplorer.SceneHandler");
                if (_sceneHandlerType != null)
                {
                    _currentRootObjectsProperty = _sceneHandlerType.GetProperty("CurrentRootObjects",
                        BindingFlags.Static | BindingFlags.Public);
                    _loadedScenesProperty = _sceneHandlerType.GetProperty("LoadedScenes",
                        BindingFlags.Static | BindingFlags.Public);
                    _selectedSceneProperty = _sceneHandlerType.GetProperty("SelectedScene",
                        BindingFlags.Static | BindingFlags.Public);
                }
                
                _childFilterType = _ueAssembly.GetType("UnityExplorer.ObjectExplorer.ChildFilter");
                _sceneFilterType = _ueAssembly.GetType("UnityExplorer.ObjectExplorer.SceneFilter");
                
                IsInitialized = _searchProviderType != null && _sceneHandlerType != null;
            }
            catch
            {
                IsInitialized = false;
            }
        }

        private int CacheObject(UnityEngine.Object obj)
        {
            var id = obj.GetInstanceID();
            _objectCache[id] = new WeakReference(obj);
            return id;
        }
        
        private UnityEngine.Object? GetCachedObject(int instanceId)
        {
            if (_objectCache.TryGetValue(instanceId, out var weakRef) && weakRef.IsAlive)
                return weakRef.Target as UnityEngine.Object;
            return null;
        }
        
        private void CleanupCache()
        {
            var deadKeys = _objectCache
                .Where(kvp => !kvp.Value.IsAlive)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in deadKeys)
                _objectCache.Remove(key);
        }

        private object SerializeGameObject(GameObject go, bool includeComponents = true)
        {
            CacheObject(go);
            
            var result = new Dictionary<string, object?>
            {
                ["instanceId"] = go.GetInstanceID(),
                ["name"] = go.name,
                ["path"] = GetGameObjectPath(go),
                ["active"] = go.activeSelf,
                ["activeInHierarchy"] = go.activeInHierarchy,
                ["layer"] = LayerMask.LayerToName(go.layer),
                ["tag"] = go.tag,
                ["isStatic"] = go.isStatic
            };
            
            if (includeComponents)
            {
                var components = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => new Dictionary<string, object?>
                    {
                        ["instanceId"] = c.GetInstanceID(),
                        ["type"] = c.GetType().FullName,
                        ["typeName"] = c.GetType().Name,
                        ["enabled"] = (c is Behaviour b) ? b.enabled : (bool?)null
                    })
                    .ToList();
                result["components"] = components;
            }
            
            result["childCount"] = go.transform.childCount;
            
            return result;
        }
        
        private string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
        
        private object SerializeComponent(Component component, bool includeMembers = false)
        {
            CacheObject(component);
            
            var result = new Dictionary<string, object?>
            {
                ["instanceId"] = component.GetInstanceID(),
                ["type"] = component.GetType().FullName,
                ["typeName"] = component.GetType().Name,
                ["gameObjectName"] = component.gameObject.name,
                ["gameObjectInstanceId"] = component.gameObject.GetInstanceID()
            };
            
            if (component is Behaviour behaviour)
            {
                result["enabled"] = behaviour.enabled;
            }
            
            if (includeMembers)
            {
                result["members"] = GetMemberValues(component);
            }
            
            return result;
        }
        
        private object GetMemberValues(object obj, int maxDepth = 1)
        {
            var type = obj.GetType();
            var members = new Dictionary<string, object?>();
            
            // Get fields
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields.Take(50)) // Limit to prevent huge outputs
            {
                try
                {
                    var value = field.GetValue(obj);
                    members[field.Name] = SerializeValue(value, maxDepth - 1);
                }
                catch
                {
                    members[field.Name] = "<error reading>";
                }
            }
            
            // Get properties
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var prop in properties.Where(p => p.CanRead && p.GetIndexParameters().Length == 0).Take(50))
            {
                try
                {
                    var value = prop.GetValue(obj);
                    members[prop.Name] = SerializeValue(value, maxDepth - 1);
                }
                catch
                {
                    members[prop.Name] = "<error reading>";
                }
            }
            
            return members;
        }
        
        private object? SerializeValue(object? value, int depth)
        {
            if (value == null) return null;
            
            var type = value.GetType();
            
            // Primitives and strings
            if (type.IsPrimitive || value is string || value is decimal)
                return value;
            
            // Enums
            if (type.IsEnum)
                return value.ToString();
            
            // Unity types
            if (value is Vector3 v3)
                return new { x = v3.x, y = v3.y, z = v3.z };
            if (value is Vector2 v2)
                return new { x = v2.x, y = v2.y };
            if (value is Quaternion q)
                return new { x = q.x, y = q.y, z = q.z, w = q.w };
            if (value is Color c)
                return new { r = c.r, g = c.g, b = c.b, a = c.a };
            
            // Unity objects
            if (value is UnityEngine.Object unityObj)
            {
                if (unityObj == null) return null; // Destroyed
                CacheObject(unityObj);
                return new { instanceId = unityObj.GetInstanceID(), type = type.Name, name = unityObj.name };
            }
            
            // Collections (limited)
            if (depth > 0 && value is IList list && list.Count <= 20)
            {
                return list.Cast<object>().Select(item => SerializeValue(item, depth - 1)).ToList();
            }
            
            return new { type = type.Name, value = value.ToString() };
        }

        [Tool("unity_search_objects", Description = "Search for Unity objects (GameObjects, Components, etc.) by name and optional type filter")]
        public object SearchObjects(
            [ToolParameter(Description = "Name filter (partial match, case-insensitive)")] string? nameFilter = null,
            [ToolParameter(Description = "Type filter (e.g., 'GameObject', 'Camera', 'Player')")] string? typeFilter = null,
            [ToolParameter(Description = "Scene filter: Any, ActivelyLoaded, DontDestroyOnLoad, HideAndDontSave")] string sceneFilter = "Any",
            [ToolParameter(Description = "Child filter: Any, RootObject, HasParent")] string childFilter = "Any",
            [ToolParameter(Description = "Maximum number of results to return")] int maxResults = 50)
        {
            if (!IsInitialized || _unityObjectSearchMethod == null)
                return new { success = false, error = "UnityExplorer APIs not available" };

            object childFilterEnum = 0;
            object sceneFilterEnum = 0;
            
            if (_childFilterType != null && !string.IsNullOrEmpty(childFilter))
                childFilterEnum = Enum.Parse(_childFilterType, childFilter, true);
            if (_sceneFilterType != null && !string.IsNullOrEmpty(sceneFilter))
                sceneFilterEnum = Enum.Parse(_sceneFilterType, sceneFilter, true);

            var results = _unityObjectSearchMethod.Invoke(null, new object?[]
            {
                nameFilter ?? "",
                typeFilter ?? "",
                childFilterEnum,
                sceneFilterEnum
            }) as IList;

            if (results == null)
                return new { success = true, count = 0, results = new List<object>() };

            var serializedResults = new List<object>();
            foreach (var obj in results.Cast<object>().Take(maxResults))
            {
                if (obj is GameObject go)
                    serializedResults.Add(SerializeGameObject(go, includeComponents: false));
                else if (obj is Component comp)
                    serializedResults.Add(SerializeComponent(comp, includeMembers: false));
                else if (obj is UnityEngine.Object unityObj)
                {
                    CacheObject(unityObj);
                    serializedResults.Add(new
                    {
                        instanceId = unityObj.GetInstanceID(),
                        type = unityObj.GetType().FullName,
                        typeName = unityObj.GetType().Name,
                        name = unityObj.name
                    });
                }
            }

            CleanupCache();

            return new
            {
                success = true,
                count = results.Count,
                returnedCount = serializedResults.Count,
                results = serializedResults
            };
        }

        [Tool("unity_search_singletons", Description = "Search for singleton instances by type name")]
        public object SearchSingletons(
            [ToolParameter(Description = "Type name filter (partial match)")] string? typeFilter = null,
            [ToolParameter(Description = "Maximum number of results to return")] int maxResults = 50)
        {
            if (!IsInitialized || _instanceSearchMethod == null)
                return new { success = false, error = "UnityExplorer APIs not available" };

            var results = _instanceSearchMethod.Invoke(null, new object?[] { typeFilter ?? "" }) as IList;

            if (results == null)
                return new { success = true, count = 0, results = new List<object>() };

            var serializedResults = new List<object>();
            foreach (var obj in results.Cast<object>().Take(maxResults))
            {
                if (obj == null) continue;
                
                var type = obj.GetType();
                
                if (obj is UnityEngine.Object unityObj)
                {
                    CacheObject(unityObj);
                    serializedResults.Add(new
                    {
                        instanceId = unityObj.GetInstanceID(),
                        type = type.FullName,
                        typeName = type.Name,
                        name = unityObj.name,
                        isUnityObject = true
                    });
                }
                else
                {
                    serializedResults.Add(new
                    {
                        type = type.FullName,
                        typeName = type.Name,
                        isUnityObject = false,
                        hashCode = obj.GetHashCode()
                    });
                }
            }

            CleanupCache();

            return new
            {
                success = true,
                count = results.Count,
                returnedCount = serializedResults.Count,
                results = serializedResults
            };
        }

        [Tool("unity_get_loaded_scenes", Description = "Get a list of all currently loaded scenes")]
        public object GetLoadedScenes()
        {
            if (!IsInitialized || _loadedScenesProperty == null)
            {
                var scenes = new List<object>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    scenes.Add(new
                    {
                        name = scene.name,
                        path = scene.path,
                        buildIndex = scene.buildIndex,
                        isLoaded = scene.isLoaded,
                        rootCount = scene.rootCount,
                        handle = scene.handle
                    });
                }
                return new { success = true, count = scenes.Count, scenes };
            }

            var loadedScenes = _loadedScenesProperty.GetValue(null) as IList;
            
            if (loadedScenes == null)
                return new { success = true, count = 0, scenes = new List<object>() };

            var serializedScenes = new List<object>();
            foreach (var sceneObj in loadedScenes)
            {
                if (sceneObj is Scene scene)
                {
                    string sceneName = scene.name ?? "";
                    string sceneType = "Normal";
                    
                    if (scene.handle == -12)
                    {
                        sceneName = "DontDestroyOnLoad";
                        sceneType = "DontDestroyOnLoad";
                    }
                    else if (scene.handle == -1 || !scene.IsValid())
                    {
                        sceneName = "HideAndDontSave";
                        sceneType = "HideAndDontSave";
                    }
                    
                    serializedScenes.Add(new
                    {
                        name = sceneName,
                        path = scene.IsValid() ? scene.path : "",
                        buildIndex = scene.buildIndex,
                        isLoaded = scene.isLoaded,
                        rootCount = scene.IsValid() ? scene.rootCount : 0,
                        handle = scene.handle,
                        sceneType
                    });
                }
            }

            return new { success = true, count = serializedScenes.Count, scenes = serializedScenes };
        }

        [Tool("unity_get_scene_roots", Description = "Get root GameObjects in a scene")]
        public object GetSceneRoots(
            [ToolParameter(Description = "Scene handle (from unity_get_loaded_scenes). If not specified, uses active scene.")] int? sceneHandle = null,
            [ToolParameter(Description = "Maximum number of results to return")] int maxResults = 100)
        {
            IEnumerable<GameObject> rootObjects;

            if (sceneHandle.HasValue)
            {
                if (sceneHandle.Value == -12 || sceneHandle.Value == -1)
                {
                    if (IsInitialized && _currentRootObjectsProperty != null && _selectedSceneProperty != null)
                    {
                        var currentRoots = _currentRootObjectsProperty.GetValue(null) as IEnumerable<GameObject>;
                        rootObjects = currentRoots ?? Enumerable.Empty<GameObject>();
                    }
                    else
                    {
                        rootObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                            .Where(go => go.transform.parent == null && 
                                        (go.scene.handle == sceneHandle.Value || !go.scene.IsValid()));
                    }
                }
                else
                {
                    Scene targetScene = default;
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        var scene = SceneManager.GetSceneAt(i);
                        if (scene.handle == sceneHandle.Value)
                        {
                            targetScene = scene;
                            break;
                        }
                    }
                    
                    if (!targetScene.IsValid())
                        return new { success = false, error = $"Scene with handle {sceneHandle.Value} not found" };
                    
                    rootObjects = targetScene.GetRootGameObjects();
                }
            }
            else
            {
                var activeScene = SceneManager.GetActiveScene();
                rootObjects = activeScene.GetRootGameObjects();
            }

            var serializedRoots = rootObjects
                .Take(maxResults)
                .Select(go => SerializeGameObject(go, includeComponents: false))
                .ToList();

            CleanupCache();

            return new
            {
                success = true,
                count = serializedRoots.Count,
                roots = serializedRoots
            };
        }

        [Tool("unity_inspect_object", Description = "Get detailed information about a Unity object by its instance ID")]
        public object InspectObject(
            [ToolParameter(Description = "Instance ID of the object (from previous search/scene results)")] int instanceId,
            [ToolParameter(Description = "Include member values (fields/properties)")] bool includeMembers = true)
        {
            var obj = GetCachedObject(instanceId);
            
            if (obj == null)
            {
                obj = Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                    .FirstOrDefault(o => o.GetInstanceID() == instanceId);
                
                if (obj != null)
                    CacheObject(obj);
            }
            
            if (obj == null)
                return new { success = false, error = $"Object with instanceId {instanceId} not found or was destroyed" };

            object result;
            
            if (obj is GameObject go)
            {
                var goData = SerializeGameObject(go, includeComponents: true) as Dictionary<string, object?>;
                
                goData!["transform"] = new
                {
                    position = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z },
                    localPosition = new { x = go.transform.localPosition.x, y = go.transform.localPosition.y, z = go.transform.localPosition.z },
                    rotation = new { x = go.transform.rotation.eulerAngles.x, y = go.transform.rotation.eulerAngles.y, z = go.transform.rotation.eulerAngles.z },
                    localScale = new { x = go.transform.localScale.x, y = go.transform.localScale.y, z = go.transform.localScale.z }
                };
                
                var children = new List<object>();
                for (int i = 0; i < Math.Min(go.transform.childCount, 20); i++)
                {
                    var child = go.transform.GetChild(i).gameObject;
                    CacheObject(child);
                    children.Add(new
                    {
                        instanceId = child.GetInstanceID(),
                        name = child.name,
                        active = child.activeSelf,
                        siblingIndex = i
                    });
                }
                goData["children"] = children;
                goData["totalChildren"] = go.transform.childCount;
                
                result = goData;
            }
            else if (obj is Component comp)
            {
                result = SerializeComponent(comp, includeMembers);
            }
            else
            {
                var data = new Dictionary<string, object?>
                {
                    ["instanceId"] = obj.GetInstanceID(),
                    ["type"] = obj.GetType().FullName,
                    ["typeName"] = obj.GetType().Name,
                    ["name"] = obj.name
                };
                
                if (includeMembers)
                    data["members"] = GetMemberValues(obj);
                
                result = data;
            }

            return new { success = true, data = result };
        }

        [Tool("unity_read_component", Description = "Read component data from a GameObject")]
        public object ReadComponent(
            [ToolParameter(Description = "Instance ID of the GameObject")] int gameObjectInstanceId,
            [ToolParameter(Description = "Component type name (e.g., 'Rigidbody', 'Player', 'Character')")] string componentType,
            [ToolParameter(Description = "Include member values (fields/properties)")] bool includeMembers = true)
        {
            var obj = GetCachedObject(gameObjectInstanceId);
            
            if (obj == null)
            {
                obj = Resources.FindObjectsOfTypeAll<GameObject>()
                    .FirstOrDefault(o => o.GetInstanceID() == gameObjectInstanceId);
                
                if (obj != null)
                    CacheObject(obj);
            }
            
            if (obj is not GameObject go)
                return new { success = false, error = $"GameObject with instanceId {gameObjectInstanceId} not found or was destroyed" };

            var components = go.GetComponents<Component>();
            Component? targetComponent = null;
            
            foreach (var comp in components)
            {
                if (comp == null) continue;
                
                var type = comp.GetType();
                if (type.Name.Equals(componentType, StringComparison.OrdinalIgnoreCase) ||
                    type.FullName?.Equals(componentType, StringComparison.OrdinalIgnoreCase) == true)
                {
                    targetComponent = comp;
                    break;
                }
            }
            
            if (targetComponent == null)
            {
                var availableTypes = components
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .Distinct()
                    .ToList();
                
                return new
                {
                    success = false,
                    error = $"Component '{componentType}' not found on GameObject",
                    availableComponents = availableTypes
                };
            }

            var result = SerializeComponent(targetComponent, includeMembers);
            return new { success = true, data = result };
        }

        [Tool("unity_get_children", Description = "Get child GameObjects of a parent")]
        public object GetChildren(
            [ToolParameter(Description = "Instance ID of the parent GameObject")] int parentInstanceId,
            [ToolParameter(Description = "Include components in child data")] bool includeComponents = false,
            [ToolParameter(Description = "Maximum number of children to return")] int maxResults = 50)
        {
            var obj = GetCachedObject(parentInstanceId);
            
            if (obj == null)
            {
                obj = Resources.FindObjectsOfTypeAll<GameObject>()
                    .FirstOrDefault(o => o.GetInstanceID() == parentInstanceId);
                
                if (obj != null)
                    CacheObject(obj);
            }
            
            if (obj is not GameObject go)
                return new { success = false, error = $"GameObject with instanceId {parentInstanceId} not found or was destroyed" };

            var children = new List<object>();
            for (int i = 0; i < Math.Min(go.transform.childCount, maxResults); i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                children.Add(SerializeGameObject(child, includeComponents));
            }

            CleanupCache();

            return new
            {
                success = true,
                parentName = go.name,
                totalChildren = go.transform.childCount,
                returnedCount = children.Count,
                children
            };
        }
    }
}
