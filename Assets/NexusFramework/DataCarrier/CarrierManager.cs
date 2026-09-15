using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     载体管理器接口
    /// </summary>
    public interface ICarrierManager : IDisposable, ICanInit
    {
        // 统计信息
        int TotalCarrierCount { get; }

        int TotalTraitCount { get; }

        // 类型注册
        ushort RegisterType(string typeName);
        bool IsTypeRegistered(string typeName);
        ushort GetTypeId(string typeName);
        string GetTypeName(ushort typeId);

        // 载体管理
        CarrierId CreateCarrier(ushort typeId);
        CarrierId CreateCarrier(string typeName);
        bool CreateCarrier(ushort typeId, ulong uniqueId); // 添加指定ID创建载体的方法
        bool DestroyCarrier(CarrierId carrierId);
        bool IsCarrierValid(CarrierId carrierId);
        ushort GetCarrierTypeId(CarrierId carrierId);
        IEnumerable<CarrierId> GetCarriersByType(ushort typeId);
        int GetCarrierCount(ushort typeId);

        /// <summary>全部载体。用于需要跨类型遍历的场景（如按 Tag 反查）。</summary>
        IEnumerable<CarrierId> GetAllCarriers();

        // 特征管理
        bool AddTrait<T>(CarrierId carrierId, T trait) where T : class, IDataTrait;
        bool RemoveTrait<T>(CarrierId carrierId) where T : class, IDataTrait;
        T GetTrait<T>(CarrierId carrierId) where T : class, IDataTrait;
        bool HasTrait<T>(CarrierId carrierId) where T : class, IDataTrait;
        IEnumerable<IDataTrait> GetAllTraits(CarrierId carrierId);
        IEnumerable<Type> GetTraitTypes(CarrierId carrierId);

        // 查询功能
        IEnumerable<CarrierId> FindCarriersWithTrait<T>() where T : class, IDataTrait;
        IEnumerable<CarrierId> FindCarriersWithTraits(params Type[] traitTypes);

        // 序列化
        string SerializeCarrier(CarrierId carrierId);
        CarrierId DeserializeCarrier(string json);
        void SaveToFile(string filePath);
        void LoadFromFile(string filePath);
    }

    /// <summary>
    ///     载体管理器实现
    /// </summary>
    public class CarrierManager : ICarrierManager
    {
        #region 构造函数

        public CarrierManager(byte frameworkId = 0)
        {
            this.frameworkId = frameworkId;
        }

        /// <summary>
        ///     注入事件回调，以便在载体生命周期变更后通过外部事件系统广播。
        /// </summary>
        public void SetEventCallback(ICarrierEventCallback callback)
        {
            this.eventCallback = callback;
        }

        /// <summary>
        ///     注入 Tag 服务。CarrierManager 自身不再持有 Tag 状态，
        ///     仅在序列化/反序列化时经此读写 Tag。
        /// </summary>
        public void SetTagService(ITagService service)
        {
            tagService = service;
        }

        #endregion

        #region 释放资源

        public void Dispose()
        {
            lock (lockObject)
            {
                typeNameToId.Clear();
                typeIdToName.Clear();
                carrierTraits.Clear();
                carriersByType.Clear();
                nextUniqueIds.Clear();
                traitReverseIndex.Clear();
            }
        }

        #endregion

        public bool Initialized { get; set; }

        public void Init()
        {
            typeNameToId = new Dictionary<string, ushort>();
            typeIdToName = new Dictionary<ushort, string>();
            carrierTraits = new Dictionary<CarrierId, Dictionary<Type, IDataTrait>>();
            carriersByType = new Dictionary<ushort, HashSet<CarrierId>>();
            nextUniqueIds = new Dictionary<ushort, ulong>();
            traitReverseIndex = new Dictionary<Type, HashSet<CarrierId>>();
        }

        public void Deinit()
        {
            typeNameToId.Clear();
            typeIdToName.Clear();
            carrierTraits.Clear();
            carriersByType.Clear();
            nextUniqueIds.Clear();
            traitReverseIndex.Clear();
        }

        #region 数据存储

        private readonly byte frameworkId;
        private Dictionary<string, ushort> typeNameToId;
        private Dictionary<ushort, string> typeIdToName;
        private Dictionary<CarrierId, Dictionary<Type, IDataTrait>> carrierTraits;
        private Dictionary<ushort, HashSet<CarrierId>> carriersByType;
        private Dictionary<ushort, ulong> nextUniqueIds;
        private readonly object lockObject = new();
        private ushort nextTypeId = 1;

        // Trait 类型反向索引（优化 FindCarriersWithTrait）
        private Dictionary<Type, HashSet<CarrierId>> traitReverseIndex;

        // 事件回调（由外部 Architecture 注入）
        private ICarrierEventCallback eventCallback;

        // Tag 服务（由外部 Architecture 注入，仅供序列化读写 Tag）
        private ITagService tagService;

        #endregion

        #region 类型注册

        public ushort RegisterType(string typeName)
        {
            lock (lockObject)
            {
                if (typeNameToId.TryGetValue(typeName, out var existingId)) return existingId;

                var typeId = nextTypeId++;
                typeNameToId[typeName] = typeId;
                typeIdToName[typeId] = typeName;
                carriersByType[typeId] = new HashSet<CarrierId>();
                nextUniqueIds[typeId] = 1;
                return typeId;
            }
        }

        public bool IsTypeRegistered(string typeName)
        {
            lock (lockObject)
            {
                return typeNameToId.ContainsKey(typeName);
            }
        }

        public ushort GetTypeId(string typeName)
        {
            lock (lockObject)
            {
                return typeNameToId.GetValueOrDefault(typeName, (ushort)0);
            }
        }

        public string GetTypeName(ushort typeId)
        {
            lock (lockObject)
            {
                return typeIdToName.GetValueOrDefault(typeId);
            }
        }

        #endregion

        #region 载体管理

        public CarrierId CreateCarrier(ushort typeId)
        {
            if (typeId == 0)
            {
                Debug.LogError("Cannot create carrier with Invalid type");
                return CarrierId.Invalid;
            }

            lock (lockObject)
            {
                if (!typeIdToName.ContainsKey(typeId))
                {
                    Debug.LogError($"Type ID {typeId} is not registered");
                    return CarrierId.Invalid;
                }

                // 生成唯一ID并检查是否重复
                ulong uniqueId;
                do
                {
                    uniqueId = nextUniqueIds[typeId]++;
                } while (IsUniqueIdUsed(typeId, uniqueId)); // 检查ID是否已被使用

                var carrierId = new CarrierId(frameworkId, typeId, uniqueId);

                carrierTraits[carrierId] = new Dictionary<Type, IDataTrait>();
                carriersByType[typeId].Add(carrierId);

                return carrierId;
            }
        }

        public CarrierId CreateCarrier(string typeName) => CreateCarrier(typeNameToId[typeName]);

        // 新增指定ID创建载体的方法
        public bool CreateCarrier(ushort typeId, ulong uniqueId)
        {
            // 参数验证
            if (typeId == 0)
            {
                Debug.LogError("Cannot create carrier with Invalid type");
                return false;
            }

            if (uniqueId > 0x00000FFFFFFFFFF)
            {
                Debug.LogError("UniqueId exceeds 40-bit limit");
                return false;
            }

            lock (lockObject)
            {
                // 检查类型是否已注册
                if (!typeIdToName.ContainsKey(typeId))
                {
                    Debug.LogError($"Type ID {typeId} is not registered");
                    return false;
                }

                // 检查ID是否已被使用
                var carrierId = new CarrierId(frameworkId, typeId, uniqueId);
                if (IsCarrierValid(carrierId))
                {
                    Debug.LogWarning($"Carrier with ID {carrierId} already exists");
                    return false;
                }

                // 更新nextUniqueIds确保不会生成重复ID
                if (nextUniqueIds[typeId] <= uniqueId) nextUniqueIds[typeId] = uniqueId + 1;

                // 创建载体
                carrierTraits[carrierId] = new Dictionary<Type, IDataTrait>();
                carriersByType[typeId].Add(carrierId);

                Debug.Log($"Created carrier: {carrierId}");
                return true;
            }
        }

        // 辅助方法：检查uniqueId是否已被使用
        private bool IsUniqueIdUsed(ushort typeId, ulong uniqueId)
        {
            var carrierId = new CarrierId(frameworkId, typeId, uniqueId);
            return carrierTraits.ContainsKey(carrierId);
        }

        public bool DestroyCarrier(CarrierId carrierId)
        {
            if (!carrierId.IsValid || carrierId.FrameworkId != frameworkId)
                return false;

            lock (lockObject)
            {
                // 存在性检查必须在锁内：IsCarrierValid 读的 carrierTraits
                // 正是本方法要改的集合，锁外检查会留下竞态窗口——
                // 两个线程可同时通过检查，第二个在条目已删后继续执行。
                if (!carrierTraits.ContainsKey(carrierId))
                    return false;

                // 清理 Trait 反向索引
                if (carrierTraits.TryGetValue(carrierId, out var traits))
                {
                    foreach (var traitType in traits.Keys)
                    {
                        if (traitReverseIndex.TryGetValue(traitType, out var carriersById))
                            carriersById.Remove(carrierId);
                    }
                }

                carrierTraits.Remove(carrierId);

                // 用 TryGetValue 而非索引器：索引器在键缺失时会插入空集合，
                // 给该 typeId 留下会被 GetCarrierCount 计入的幽灵条目。
                if (carriersByType.TryGetValue(carrierId.TypeId, out var carriersOfType))
                    carriersOfType.Remove(carrierId);
            }

            // 锁外广播：接收方（如 ITagService）在自己的锁内清理索引。
            // 在锁内调用会把两个独立的锁嵌套起来，徒增死锁面。
            eventCallback?.OnCarrierDestroyed(carrierId);
            return true;
        }

        public bool IsCarrierValid(CarrierId carrierId)
        {
            return carrierId.IsValid && carrierId.FrameworkId == frameworkId && carrierTraits.ContainsKey(carrierId);
        }

        public ushort GetCarrierTypeId(CarrierId carrierId)
        {
            return carrierId.TypeId;
        }

        public IEnumerable<CarrierId> GetCarriersByType(ushort typeId)
        {
            lock (lockObject)
            {
                return carriersByType.TryGetValue(typeId, out var carriers)
                    ? carriers.ToList()
                    : Enumerable.Empty<CarrierId>();
            }
        }

        public int GetCarrierCount(ushort typeId)
        {
            lock (lockObject)
            {
                return carriersByType.TryGetValue(typeId, out var carriers) ? carriers.Count : 0;
            }
        }

        public IEnumerable<CarrierId> GetAllCarriers()
        {
            lock (lockObject)
            {
                return carrierTraits.Keys.ToList();
            }
        }

        #endregion

        #region 特征管理

        public bool AddTrait<T>(CarrierId carrierId, T trait) where T : class, IDataTrait
        {
            if (!IsCarrierValid(carrierId) || trait == null)
                return false;

            lock (lockObject)
            {
                var traitType = typeof(T);

                carrierTraits[carrierId][traitType] = trait;

                // 维护 Trait 反向索引
                if (!traitReverseIndex.TryGetValue(traitType, out var carriers))
                {
                    carriers = new HashSet<CarrierId>();
                    traitReverseIndex[traitType] = carriers;
                }

                carriers.Add(carrierId);

                return true;
            }
        }

        public bool RemoveTrait<T>(CarrierId carrierId) where T : class, IDataTrait
        {
            if (!IsCarrierValid(carrierId))
                return false;

            lock (lockObject)
            {
                var traitType = typeof(T);

                // 清理 Trait 反向索引
                if (traitReverseIndex.TryGetValue(traitType, out var carriers))
                    carriers.Remove(carrierId);

                return carrierTraits[carrierId].Remove(traitType);
            }
        }

        public T GetTrait<T>(CarrierId carrierId) where T : class, IDataTrait
        {
            if (!IsCarrierValid(carrierId))
                return null;

            lock (lockObject)
            {
                var traitType = typeof(T);


                return carrierTraits[carrierId].TryGetValue(traitType, out var trait)
                    ? trait as T
                    : null;
            }
        }

        public bool HasTrait<T>(CarrierId carrierId) where T : class, IDataTrait
        {
            if (!IsCarrierValid(carrierId))
                return false;

            lock (lockObject)
            {
                var traitType = typeof(T);
                return carrierTraits[carrierId].ContainsKey(traitType);
            }
        }

        public IEnumerable<IDataTrait> GetAllTraits(CarrierId carrierId)
        {
            if (!IsCarrierValid(carrierId))
                return Enumerable.Empty<IDataTrait>();

            lock (lockObject)
            {
                return carrierTraits[carrierId].Values.ToList();
            }
        }

        public IEnumerable<Type> GetTraitTypes(CarrierId carrierId)
        {
            if (!IsCarrierValid(carrierId))
                return Enumerable.Empty<Type>();

            lock (lockObject)
            {
                return carrierTraits[carrierId].Keys.ToList();
            }
        }

        #endregion

        #region 查询功能

        public IEnumerable<CarrierId> FindCarriersWithTrait<T>() where T : class, IDataTrait
        {
            lock (lockObject)
            {
                var traitType = typeof(T);
                return traitReverseIndex.TryGetValue(traitType, out var carriers)
                    ? carriers.ToList()
                    : Enumerable.Empty<CarrierId>();
            }
        }

        public IEnumerable<CarrierId> FindCarriersWithTraits(params Type[] traitTypes)
        {
            if (traitTypes == null || traitTypes.Length == 0)
                return Enumerable.Empty<CarrierId>();

            lock (lockObject)
            {
                if (traitTypes.Length == 0)
                    return Enumerable.Empty<CarrierId>();

                // 从最小反向索引开始交集
                IEnumerable<CarrierId> result = null;
                foreach (var traitType in traitTypes)
                {
                    if (!traitReverseIndex.TryGetValue(traitType, out var carriers))
                        return Enumerable.Empty<CarrierId>();

                    if (result == null)
                        result = carriers;
                    else
                        result = result.Intersect(carriers);
                }

                return result?.ToList() ?? Enumerable.Empty<CarrierId>();
            }
        }

        #endregion

        #region 序列化

        public string SerializeCarrier(CarrierId carrierId)
        {
            if (!IsCarrierValid(carrierId))
                return null;

            // Tag 从 Tag 服务读取，且必须在进入本类锁之前取完——
            // 服务有自己的锁，嵌套会扩大死锁面。
            var tags = tagService?.GetTags(carrierId)
                           .OrderBy(key => key, StringComparer.Ordinal)
                           .ToArray()
                       ?? Array.Empty<string>();

            lock (lockObject)
            {
                var carrierData = new CarrierSerializationData
                {
                    carrierId = carrierId.RawValue,
                    traits = carrierTraits[carrierId].Values.Select(trait => new TraitSerializationData
                    {
                        typeName = trait.TraitTypeName,
                        data = trait.ToJson()
                    }).ToArray(),
                    tags = tags
                };

                return JsonUtility.ToJson(carrierData);
            }
        }

        public CarrierId DeserializeCarrier(string json)
        {
            try
            {
                var carrierData = JsonUtility.FromJson<CarrierSerializationData>(json);
                var carrierId = (CarrierId)carrierData.carrierId;

                if (carrierId.FrameworkId != frameworkId)
                {
                    Debug.LogWarning(
                        $"Carrier framework ID mismatch: expected {frameworkId}, got {carrierId.FrameworkId}");
                    return CarrierId.Invalid;
                }

                if (IsCarrierValid(carrierId))
                {
                    Debug.LogWarning($"Carrier {carrierId} already exists, skipping deserialization");
                    return carrierId;
                }

                // 重新创建载体
                lock (lockObject)
                {
                    carrierTraits[carrierId] = new Dictionary<Type, IDataTrait>();
                    carriersByType[carrierId.TypeId].Add(carrierId);

                    // 恢复特征数据
                    foreach (var traitData in carrierData.traits)
                    {
                        var traitType = Type.GetType(traitData.typeName);
                        if (traitType != null && typeof(IDataTrait).IsAssignableFrom(traitType))
                        {
                            var trait = (IDataTrait)Activator.CreateInstance(traitType);
                            trait.FromJson(traitData.data);
                            carrierTraits[carrierId][traitType] = trait;

                            // 维护 Trait 反向索引
                            if (!traitReverseIndex.TryGetValue(traitType, out var carriers))
                            {
                                carriers = new HashSet<CarrierId>();
                                traitReverseIndex[traitType] = carriers;
                            }
                            carriers.Add(carrierId);
                        }
                    }
                }

                // 锁外恢复 Tag。反序列化路径不广播 TagAdded——
                // 加载存档不应引发一连串变更事件。
                if (carrierData.tags is { Length: > 0 })
                    tagService?.RestoreTags(carrierId, carrierData.tags);

                return carrierId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to deserialize carrier: {ex.Message}");
                return CarrierId.Invalid;
            }
        }

        public void SaveToFile(string filePath)
        {
            try
            {
                var allCarriers = carrierTraits.Keys.Select(SerializeCarrier).ToArray();
                var saveData = new CarrierManagerSaveData { carriers = allCarriers };
                var json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(filePath, json);
                Debug.Log($"Saved {allCarriers.Length} carriers to {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save carriers to file: {ex.Message}");
            }
        }

        public void LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"Save file not found: {filePath}");
                    return;
                }

                var json = File.ReadAllText(filePath);
                var saveData = JsonUtility.FromJson<CarrierManagerSaveData>(json);

                foreach (var carrierJson in saveData.carriers) DeserializeCarrier(carrierJson);

                Debug.Log($"Loaded {saveData.carriers.Length} carriers from {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load carriers from file: {ex.Message}");
            }
        }

        #endregion

        #region 统计信息

        public int TotalCarrierCount
        {
            get
            {
                lock (lockObject)
                {
                    return carrierTraits.Count;
                }
            }
        }

        public int TotalTraitCount
        {
            get
            {
                lock (lockObject)
                {
                    return carrierTraits.Values.Sum(traits => traits.Count);
                }
            }
        }

        #endregion
    }

    #region 序列化数据结构

    [Serializable]
    public class TraitSerializationData
    {
        public string typeName;
        public string data;
    }

    [Serializable]
    public class CarrierSerializationData
    {
        public ulong carrierId;
        public TraitSerializationData[] traits;

        /// <summary>归一化后的 Tag 键，按序写入以保证确定性。旧存档缺此字段时为 null。</summary>
        public string[] tags;
    }

    [Serializable]
    public class CarrierManagerSaveData
    {
        public string[] carriers;
    }

    #endregion
}