using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NexusFramework
{
    /// <summary>
    ///     架构工厂管理器
    /// </summary>
    public static class ArchitectureFactory
    {
        
        #region 架构注册表

        private static readonly Dictionary<string, Type> AllArchitectureTypes = new();
        
        // 架构实例管理
        private static readonly Dictionary<byte, IArchitecture> ArchitectureInstances = new();

        #endregion

        #region 架构类型注册

        /// <summary>
        ///     注册架构类型
        /// </summary>
        public static void RegisterArchitecture<T>(string typeName) where T : IArchitecture
        {
            AllArchitectureTypes[typeName] = typeof(T);
            Debug.Log($"Registered Frame architecture: {typeName} -> {typeof(T).Name}");
        }

        #endregion

        #region 通用架构获取

        /// <summary>
        ///     获取架构实例（通用方法）
        /// </summary>
        public static T GetArchitecture<T>() where T : class, IArchitecture
        {
            // 查找已创建的实例
            foreach (var instance in ArchitectureInstances.Values)
                if (instance is T targetInstance)
                    return targetInstance;

            return null;
        }

        /// <summary>
        ///     获取指定ID的架构实例
        /// </summary>
        public static IArchitecture GetArchitecture(byte instanceId)
        {
            ArchitectureInstances.TryGetValue(instanceId, out var architecture);
            return architecture;
        }

        #endregion

        #region 架构实例创建

        /// <summary>
        ///     创建架构实例
        /// </summary>
        public static IArchitecture CreateArchitecture(string typeName = "Default", byte? instanceId = null)
        {
            if (!AllArchitectureTypes.TryGetValue(typeName, out var architectureType))
            {
                Debug.LogError($"Frame architecture type '{typeName}' not registered");
                return null;
            }

            var id = instanceId ?? 0;

            return CreateArchitectureInstance<IArchitecture>(architectureType, typeName, id);
        }

        /// <summary>
        ///     通用架构创建方法 - 支持自定义ID生成
        /// </summary>
        private static T CreateArchitectureInstance<T>(Type architectureType, string typeName, byte instanceId)
            where T : class, IArchitecture
        {
            try
            {
                // 生成有意义的实例ID
                var architectureTypeName = typeof(T).Name.Replace("I", "").Replace("Architecture", "");
                var finalInstanceId = instanceId;

                // 使用带参数的构造器
                var instance = (T)Activator.CreateInstance(architectureType, finalInstanceId);
                instance.Initialize();

                ArchitectureInstances[instance.ArchitectureId] = instance;
                Debug.Log($"Created {architectureTypeName} architecture instance: {instance.ArchitectureId}");

                return instance;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create {typeof(T).Name} '{typeName}': {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 架构实例管理

        /// <summary>
        ///     获取架构实例
        /// </summary>
        public static T GetArchitecture<T>(byte architectureId) where T : class, IArchitecture
        {
            if (ArchitectureInstances.TryGetValue(architectureId, out var instance)) return instance as T;
            return null;
        }

        /// <summary>
        ///     销毁架构实例
        /// </summary>
        public static bool DestroyArchitecture(byte architectureId)
        {
            if (ArchitectureInstances.TryGetValue(architectureId, out var instance))
            {
                instance.Shutdown();
                ArchitectureInstances.Remove(architectureId);
                Debug.Log($"Destroyed architecture instance: {architectureId}");
                return true;
            }

            return false;
        }

        /// <summary>
        ///     获取所有架构实例
        /// </summary>
        public static IEnumerable<IArchitecture> GetAllArchitectures()
        {
            return ArchitectureInstances.Values.ToList();
        }

        /// <summary>
        ///     获取指定类型的所有架构实例
        /// </summary>
        public static IEnumerable<T> GetArchitecturesByType<T>() where T : class, IArchitecture
        {
            return ArchitectureInstances.Values.OfType<T>().ToList();
        }

        #endregion

        #region 架构类型查询

        /// <summary>
        ///     获取已注册的架构类型
        /// </summary>
        public static IEnumerable<string> GetRegisteredFrameTypes()
        {
            return AllArchitectureTypes.Keys.ToList();
        }

        /// <summary>
        ///     检查架构类型是否已注册
        /// </summary>
        public static bool IsFrameTypeRegistered(string typeName)
        {
            return AllArchitectureTypes.ContainsKey(typeName);
        }
        
        #endregion

        #region 清理和重置
        
        /// <summary>
        ///     重置架构工厂（清理实例和注册表）
        /// </summary>
        public static void Reset()
        {
            foreach (var instance in ArchitectureInstances.Values) instance.Shutdown();
            ArchitectureInstances.Clear();
            Debug.Log("Cleared all architecture instances");

            AllArchitectureTypes.Clear();

            Debug.Log("Reset ArchitectureFactory");
        }

        #endregion
    }

    
}