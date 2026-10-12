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
        #region 架构ID分配器

        // 架构ID的宽度（byte）由 CarrierId 的框架位（高8位）决定，不可扩展。
        private const int MaxArchitectureId = 256;

        // 每个ID的占用位图。被架构实例占用或作为固定ID显式分配即为 true。
        private static readonly bool[] mIdOccupied = new bool[MaxArchitectureId];

        /// <summary>
        ///     显式登记一个固定/保留的架构ID，使其不参与动态分配。
        ///     适用于 Game/Session 等常驻框架——它们的ID必须稳定（如 UI 层硬编码 GetArchitecture(0)）。
        /// </summary>
        public static void ReserveId(byte id)
        {
            if (mIdOccupied[id]) Debug.LogWarning($"[ArchitectureFactory] 架构ID {id} 被重复保留");
            mIdOccupied[id] = true;
        }

        /// <summary>
        ///     释放显式保留的架构ID，使其重新可被动态分配。
        /// </summary>
        public static void ReleaseId(byte id)
        {
            mIdOccupied[id] = false;
        }

        /// <summary>
        ///     该ID当前是否已被占用（实例占用或显式保留）。
        /// </summary>
        public static bool IsIdOccupied(byte id)
        {
            return mIdOccupied[id];
        }

        /// <summary>
        ///     分配一个可用的架构ID——从1起寻找首个空闲位（0 保留给常驻框架的固定ID）。
        ///     池耗尽（256个全占用）时返回 false。
        ///     注意：byte 上限由 CarrierId 框架位决定，因此动态分配的架构数量不得超过256。
        /// </summary>
        public static bool TryAllocateId(out byte id)
        {
            for (var i = 1; i < MaxArchitectureId; i++)
            {
                if (mIdOccupied[i]) continue;
                mIdOccupied[i] = true;
                id = (byte)i;
                return true;
            }

            id = 0;
            Debug.LogError($"[ArchitectureFactory] 架构ID池已耗尽（上限 {MaxArchitectureId}）——无法分配新架构");
            return false;
        }

        #endregion

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
        ///     创建架构实例。
        ///     instanceId 为 null 时从动态池自动分配一个空闲ID（不占用0——0默认作为常驻框架的固定ID）；
        ///     传入显式 instanceId 时以该固定ID创建。若ID已被占用则拒绝创建，避免静默覆盖。
        /// </summary>
        public static IArchitecture CreateArchitecture(string typeName = "Default", byte? instanceId = null)
        {
            if (!AllArchitectureTypes.TryGetValue(typeName, out var architectureType))
            {
                Debug.LogError($"Frame architecture type '{typeName}' not registered");
                return null;
            }

            if (instanceId.HasValue)
            {
                var fixedId = instanceId.Value;
                if (mIdOccupied[fixedId])
                {
                    Debug.LogError($"[ArchitectureFactory] 架构ID {fixedId} 已被占用，拒绝创建 '{typeName}'");
                    return null;
                }

                mIdOccupied[fixedId] = true;
                return CreateArchitectureInstance<IArchitecture>(architectureType, typeName, fixedId);
            }

            if (!TryAllocateId(out var newId)) return null;
            return CreateArchitectureInstance<IArchitecture>(architectureType, typeName, newId);
        }

        /// <summary>
        ///     通用架构创建方法 - 支持自定义ID生成。
        ///     创建失败时归还ID，避免泄漏。
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
                mIdOccupied[instanceId] = false; // 创建失败，归还ID
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
        ///     销毁架构实例并归还其ID。
        ///     顺序严格为：Shutdown（销毁Carrier/释放ECS资源）→ 移出注册表 → 最后归还ID。
        ///     必须先完成 Shutdown，否则该ID被立即复用时会与尚存活的旧Carrier撞号。
        /// </summary>
        public static bool DestroyArchitecture(byte architectureId)
        {
            if (ArchitectureInstances.TryGetValue(architectureId, out var instance))
            {
                instance.Shutdown();
                ArchitectureInstances.Remove(architectureId);
                mIdOccupied[architectureId] = false; // 归还ID供后续分配
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
        ///     重置架构工厂（清理实例、注册表与ID分配状态）
        /// </summary>
        public static void Reset()
        {
            foreach (var instance in ArchitectureInstances.Values) instance.Shutdown();
            ArchitectureInstances.Clear();
            Array.Clear(mIdOccupied, 0, mIdOccupied.Length);
            Debug.Log("Cleared all architecture instances");

            AllArchitectureTypes.Clear();

            Debug.Log("Reset ArchitectureFactory");
        }

        #endregion
    }

    
}