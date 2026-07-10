using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.UI.WindowSystem
{
    [CreateAssetMenu(menuName = "Game/UI/Window Database", fileName = "WindowDatabase")]
    public sealed class WindowDatabase : ScriptableObject
    {
        [Tooltip("Assign all your WindowConfig assets here.")]
        public List<WindowConfig> Configs = new();

        private Dictionary<Type, WindowConfig> _configMap;

        public WindowConfig GetConfig<TViewModel>() where TViewModel : class
        {
            if (_configMap == null) BuildCache();

            var type = typeof(TViewModel);
            if (_configMap.TryGetValue(type, out var config))
            {
                return config;
            }

            Debug.LogError($"[WindowDatabase] No WindowConfig found for ViewModel type '{type.Name}'. Did you add its config to the list?");
            return null;
        }
        
        public WindowConfig GetConfigByName(string configName)
        {
            return Configs.FirstOrDefault(w => w.name == configName); 
        }

        private void BuildCache()
        {
            Debug.Log("[WindowDatabase] Building cache for WindowConfigs");
            _configMap = new Dictionary<Type, WindowConfig>();

            foreach (var config in Configs)
            {
                if (config == null || config.Prefab == null) continue;

                // Scan the prefab to find the WindowComposer<T> and extract the TViewModel type
                foreach (var component in config.Prefab.GetComponents<MonoBehaviour>())
                {
                    if (component == null) continue;

                    var currentType = component.GetType();
                    while (currentType != null && currentType != typeof(MonoBehaviour))
                    {
                        if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(WindowComposer<>))
                        {
                            var viewModelType = currentType.GetGenericArguments()[0];
                            _configMap.TryAdd(viewModelType, config);
                            Debug.Log($"  > Adding WindowComposer<{viewModelType}>");
                            break;
                        }
                        currentType = currentType.BaseType;
                    }
                }
            }
        }
    }
}