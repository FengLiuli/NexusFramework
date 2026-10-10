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

        // 索引表管理
        /// <summary>注册索引表（以 index.Name 为键）。重名不同实例被拒绝（保留先者）。</summary>
        void RegisterIndex(ICarrierIndex index);

        /// <summary>按表名注销。返回是否确实移除。</summary>
        bool UnregisterIndex(string name);

        /// <summary>按表名获取（非泛型）。不存在返回 null。</summary>
        ICarrierIndex GetIndex(string name);

        /// <summary>按表名 + 类型安全获取。名字不存在或类型不符返回 false。</summary>
        bool TryGetIndex<TIndex>(string name, out TIndex index) where TIndex : class, ICarrierIndex;

        /// <summary>全部索引（快照副本，用于诊断）。</summary>
        IReadOnlyCollection<ICarrierIndex> GetAllIndexes();

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
                indexesByTable.Clear();
                disposed = true;
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
            disposed = false; // 供 Architecture 重启复用同一实例：清除关闭标志
            // 注意：不重建 indexesByTable——避免清掉先于本 Init 执行的 RegisterIndex（见 D005 §4.4）。
        }

        public void Deinit()
        {
            typeNameToId.Clear();
            typeIdToName.Clear();
            carrierTraits.Clear();
            carriersByType.Clear();
            nextUniqueIds.Clear();
            traitReverseIndex.Clear();
            lock (lockObject) indexesByTable.Clear();
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

        // 索引表注册表（表名 → 索引），生命周期托管：DestroyCarrier 派发清理。
        // 注意：不在 Init 中重建——IOCContainer 的 InitAll 遍历顺序不保证，
        // 若 CarrierManager.Init（清空）晚于 Model 的注册（RegisterCarrierIndex）会静默吞掉注册。
        // 故字段在声明处初始化，仅 Deinit/Dispose 清空。见 D005 §4.4。
        private readonly Dictionary<string, ICarrierIndex> indexesByTable = new();
        private bool disposed;

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

            // 索引快照：与移除在同一把锁内取，保证「快照 == 已移除」原子。
            ICarrierIndex[] indexSnapshot;
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

                indexSnapshot = new ICarrierIndex[indexesByTable.Count];
                indexesByTable.Values.CopyTo(indexSnapshot, 0);
            }

            // 锁外派发：接收方（业务索引）在自己的状态下清理，避免锁序嵌套。
            // 逐条隔离异常——某个业务索引抛异常不得连累其余索引与销毁广播。
            try
            {
                for (var i = 0; i < indexSnapshot.Length; i++)
                {
                    try
                    {
                        indexSnapshot[i].OnCarrierRemoved(carrierId);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"[CarrierManager] index '{indexSnapshot[i].Name}' OnCarrierRemoved failed: {ex.Message}");
                    }
                }
            }
            finally
            {
                // 无论索引清理是否失败，都必须广播——Tag 清理与订阅者依赖它。
                eventCallback?.OnCarrierDestroyed(carrierId);
            }

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

        #region 索引表管理

        public void RegisterIndex(ICarrierIndex index)
        {
            if (index == null || string.IsNullOrEmpty(index.Name))
            {
                Debug.LogWarning("[CarrierManager] RegisterIndex skipped: null index or empty name");
                return;
            }

            lock (lockObject)
            {
                if (disposed)
                {
                    Debug.LogWarning($"[CarrierManager] RegisterIndex skipped: manager disposed (table '{index.Name}')");
                    return;
                }

                if (indexesByTable.TryGetValue(index.Name, out var existing))
                {
                    if (ReferenceEquals(existing, index)) return; // 同实例重复注册 → no-op
                    // 重名不同实例 → 拒绝覆盖，保留先者：
                    // 「改表 = 改数据，不是换表」——变更表内条目，而非注册同名新表替换整表。
                    Debug.LogWarning(
                        $"[CarrierManager] RegisterIndex skipped: table name '{index.Name}' already registered by {existing.GetType().Name}");
                    return;
                }

                indexesByTable[index.Name] = index;
            }

            // 注入 owner，供索引在写入前校验载体有效性（见 ICarrierIndexOwner）。
            if (index is ICarrierIndexOwner owned) owned.SetOwner(this);
        }

        public bool UnregisterIndex(string name)
        {
            lock (lockObject)
            {
                return indexesByTable.Remove(name);
            }
        }

        public ICarrierIndex GetIndex(string name)
        {
            lock (lockObject)
            {
                return indexesByTable.GetValueOrDefault(name);
            }
        }

        public bool TryGetIndex<TIndex>(string name, out TIndex index) where TIndex : class, ICarrierIndex
        {
            lock (lockObject)
            {
                if (indexesByTable.TryGetValue(name, out var v) && v is TIndex typed)
                {
                    index = typed;
                    return true;
                }

                index = null;
                return false;
            }
        }

        public IReadOnlyCollection<ICarrierIndex> GetAllIndexes()
        {
            lock (lockObject)
            {
                return indexesByTable.Values.ToArray(); // 快照，非活集合
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