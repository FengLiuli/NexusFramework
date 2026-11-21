using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NexusFramework.Identity;
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
        int RegisterType(string typeName);
        bool IsTypeRegistered(string typeName);
        int GetTypeId(string typeName);
        string GetTypeName(int typeId);

        // 载体管理
        int CreateCarrier(int typeId);
        int CreateCarrier(string typeName);
        bool CreateCarrier(int typeId, ulong uniqueId);
        bool DestroyCarrier(int carrierId);
        bool IsCarrierValid(int carrierId);
        int GetCarrierTypeId(int carrierId);
        IEnumerable<int> GetCarriersByType(int typeId);
        int GetCarrierCount(int typeId);

        /// <summary>全部载体。用于需要跨类型遍历的场景（如按 Tag 反查）。</summary>
        IEnumerable<int> GetAllCarriers();

        // 特征管理
        bool AddTrait<T>(int carrierId, T trait) where T : class, IDataTrait;
        bool RemoveTrait<T>(int carrierId) where T : class, IDataTrait;
        T GetTrait<T>(int carrierId) where T : class, IDataTrait;
        bool HasTrait<T>(int carrierId) where T : class, IDataTrait;
        IEnumerable<IDataTrait> GetAllTraits(int carrierId);
        IEnumerable<Type> GetTraitTypes(int carrierId);

        // 查询功能
        IEnumerable<int> FindCarriersWithTrait<T>() where T : class, IDataTrait;
        IEnumerable<int> FindCarriersWithTraits(params Type[] traitTypes);

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
        string SerializeCarrier(int carrierId);
        int DeserializeCarrier(string json);
        void SaveToFile(string filePath);
        void LoadFromFile(string filePath);
    }

    /// <summary>
    ///     载体管理器实现。
    ///     类型走 <see cref="KeyIndex" />，实例走 <see cref="TypedAllocator" />；
    ///     实例 id 是运行时槽位，不进入持久化格式。
    /// </summary>
    public class CarrierManager : ICarrierManager
    {
        private const string TypeDomain = "carrier.type";
        private const string InstanceDomain = "carrier.instance";

        #region 构造函数

        /// <summary>自建 <see cref="IdRegistry" />；用于独立测试或未接入架构的场景。</summary>
        public CarrierManager() : this(new IdRegistry())
        {
        }

        /// <summary>兼容旧签名；<paramref name="frameworkId" /> 不再参与标识。</summary>
        public CarrierManager(byte frameworkId) : this(new IdRegistry())
        {
        }

        public CarrierManager(IdRegistry registry)
        {
            mRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
            mTypeIndex = mRegistry.GetOrCreateIndex(TypeDomain)
                         ?? throw new InvalidOperationException(
                             $"IdRegistry domain '{TypeDomain}' is occupied by a different primitive.");
            mInstances = mRegistry.GetOrCreateTypedAllocator(InstanceDomain)
                         ?? throw new InvalidOperationException(
                             $"IdRegistry domain '{InstanceDomain}' is occupied by a different primitive.");

            carrierTraits = new Dictionary<int, Dictionary<Type, IDataTrait>>();
            carriersByType = new Dictionary<int, HashSet<int>>();
            traitReverseIndex = new Dictionary<Type, HashSet<int>>();
        }

        /// <summary>注入事件回调，以便在载体生命周期变更后通过外部事件系统广播。</summary>
        public void SetEventCallback(ICarrierEventCallback callback)
        {
            eventCallback = callback;
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
                carrierTraits.Clear();
                carriersByType.Clear();
                traitReverseIndex.Clear();
                indexesByTable.Clear();
                mTypeIndex.Clear();
                mInstances.Clear();
                disposed = true;
            }
        }

        #endregion

        public bool Initialized { get; set; }

        public void Init()
        {
            lock (lockObject)
            {
                carrierTraits = new Dictionary<int, Dictionary<Type, IDataTrait>>();
                carriersByType = new Dictionary<int, HashSet<int>>();
                traitReverseIndex = new Dictionary<Type, HashSet<int>>();
                mTypeIndex.Clear();
                mInstances.Clear();
                disposed = false;
            }

            // 注意：不重建 indexesByTable——避免清掉先于本 Init 执行的 RegisterIndex（见 D005 §4.4）。
        }

        public void Deinit()
        {
            lock (lockObject)
            {
                carrierTraits.Clear();
                carriersByType.Clear();
                traitReverseIndex.Clear();
                mTypeIndex.Clear();
                mInstances.Clear();
                indexesByTable.Clear();
            }
        }

        #region 数据存储

        private readonly IdRegistry mRegistry;
        private readonly KeyIndex mTypeIndex;
        private readonly TypedAllocator mInstances;

        private Dictionary<int, Dictionary<Type, IDataTrait>> carrierTraits;
        private Dictionary<int, HashSet<int>> carriersByType;
        private Dictionary<Type, HashSet<int>> traitReverseIndex;

        private readonly object lockObject = new();

        // 索引表注册表（表名 → 索引），生命周期托管：DestroyCarrier 派发清理。
        private readonly Dictionary<string, ICarrierIndex> indexesByTable = new();

        private bool disposed;
        private ICarrierEventCallback eventCallback;
        private ITagService tagService;

        #endregion

        #region 类型注册

        public int RegisterType(string typeName)
        {
            lock (lockObject)
            {
                if (disposed)
                    return 0;

                var id = mTypeIndex.Intern(typeName);
                if (id <= 0)
                    return 0;

                if (!carriersByType.ContainsKey(id))
                    carriersByType[id] = new HashSet<int>();

                return id;
            }
        }

        public bool IsTypeRegistered(string typeName)
        {
            return mTypeIndex.TryGet(typeName, out _);
        }

        public int GetTypeId(string typeName)
        {
            return mTypeIndex.TryGet(typeName, out var id) ? id : 0;
        }

        public string GetTypeName(int typeId)
        {
            return mTypeIndex.TryGetKey(typeId, out var key) ? key : null;
        }

        #endregion

        #region 载体管理

        public int CreateCarrier(int typeId)
        {
            if (typeId == 0)
            {
                Debug.LogError("Cannot create carrier with Invalid type");
                return 0;
            }

            lock (lockObject)
            {
                if (!mTypeIndex.IsAlive(typeId))
                {
                    Debug.LogError($"Type ID {typeId} is not registered");
                    return 0;
                }

                var id = mInstances.Allocate(typeId);
                if (id == 0)
                {
                    Debug.LogError("Carrier instance allocator exhausted");
                    return 0;
                }

                var carrierId = id;
                carrierTraits[carrierId] = new Dictionary<Type, IDataTrait>();

                if (!carriersByType.TryGetValue(typeId, out var carriers))
                {
                    carriers = new HashSet<int>();
                    carriersByType[typeId] = carriers;
                }

                carriers.Add(carrierId);
                return carrierId;
            }
        }

        public int CreateCarrier(string typeName)
        {
            var typeId = RegisterType(typeName);
            return typeId == 0 ? 0 : CreateCarrier(typeId);
        }

        /// <summary>
        ///     指定 id 的创建不再支持；实例 id 由分配器统一发放。
        ///     保留签名以兼容旧调用，<paramref name="uniqueId" /> 被忽略。
        /// </summary>
        public bool CreateCarrier(int typeId, ulong uniqueId)
        {
            return CreateCarrier(typeId) > 0;
        }

        public bool DestroyCarrier(int carrierId)
        {
            if (carrierId <= 0)
                return false;

            // 索引快照：与移除在同一把锁内取，保证「快照 == 已移除」原子。
            ICarrierIndex[] indexSnapshot;
            lock (lockObject)
            {
                // 一次 TryGetValue 同时完成存在性检查与 Trait 集合获取，
                // 避免销毁路径上对同一键做两次哈希查找。
                if (!carrierTraits.TryGetValue(carrierId, out var traits))
                    return false;

                foreach (var traitType in traits.Keys)
                    if (traitReverseIndex.TryGetValue(traitType, out var carriersById))
                        carriersById.Remove(carrierId);

                carrierTraits.Remove(carrierId);

                if (mInstances.TryGetType(carrierId, out var typeId)
                    && carriersByType.TryGetValue(typeId, out var carriersOfType))
                    carriersOfType.Remove(carrierId);

                mInstances.Free(carrierId);

                // 空索引表是常态，用共享空数组避免每销毁一个载体就分配一次。
                if (indexesByTable.Count == 0)
                {
                    indexSnapshot = Array.Empty<ICarrierIndex>();
                }
                else
                {
                    indexSnapshot = new ICarrierIndex[indexesByTable.Count];
                    indexesByTable.Values.CopyTo(indexSnapshot, 0);
                }
            }

            // 锁外派发：接收方（业务索引）在自己的状态下清理，避免锁序嵌套。
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

        public bool IsCarrierValid(int carrierId)
        {
            return carrierId > 0 && mInstances.IsAlive(carrierId);
        }

        public int GetCarrierTypeId(int carrierId)
        {
            return mInstances.TryGetType(carrierId, out var typeId) ? typeId : 0;
        }

        public IEnumerable<int> GetCarriersByType(int typeId)
        {
            lock (lockObject)
            {
                return carriersByType.TryGetValue(typeId, out var carriers)
                    ? carriers.ToList()
                    : Enumerable.Empty<int>();
            }
        }

        public int GetCarrierCount(int typeId)
        {
            lock (lockObject)
            {
                return carriersByType.TryGetValue(typeId, out var carriers) ? carriers.Count : 0;
            }
        }

        public IEnumerable<int> GetAllCarriers()
        {
            lock (lockObject)
            {
                return carrierTraits.Keys.ToList();
            }
        }

        #endregion

        #region 特征管理

        public bool AddTrait<T>(int carrierId, T trait) where T : class, IDataTrait
        {
            if (trait == null)
                return false;

            lock (lockObject)
            {
                // 存在性检查与写入必须在同一把锁内：锁外判活后并发销毁会抛 KeyNotFound。
                if (!carrierTraits.TryGetValue(carrierId, out var traits))
                    return false;

                var traitType = typeof(T);

                traits[traitType] = trait;

                if (!traitReverseIndex.TryGetValue(traitType, out var carriers))
                {
                    carriers = new HashSet<int>();
                    traitReverseIndex[traitType] = carriers;
                }

                carriers.Add(carrierId);

                return true;
            }
        }

        public bool RemoveTrait<T>(int carrierId) where T : class, IDataTrait
        {
            lock (lockObject)
            {
                if (!carrierTraits.TryGetValue(carrierId, out var traits))
                    return false;

                var traitType = typeof(T);

                if (traitReverseIndex.TryGetValue(traitType, out var carriers))
                    carriers.Remove(carrierId);

                return traits.Remove(traitType);
            }
        }

        public T GetTrait<T>(int carrierId) where T : class, IDataTrait
        {
            lock (lockObject)
            {
                if (!carrierTraits.TryGetValue(carrierId, out var traits))
                    return null;

                var traitType = typeof(T);

                return traits.TryGetValue(traitType, out var trait)
                    ? trait as T
                    : null;
            }
        }

        public bool HasTrait<T>(int carrierId) where T : class, IDataTrait
        {
            lock (lockObject)
            {
                if (!carrierTraits.TryGetValue(carrierId, out var traits))
                    return false;

                return traits.ContainsKey(typeof(T));
            }
        }

        public IEnumerable<IDataTrait> GetAllTraits(int carrierId)
        {
            lock (lockObject)
            {
                return carrierTraits.TryGetValue(carrierId, out var traits)
                    ? traits.Values.ToList()
                    : Enumerable.Empty<IDataTrait>();
            }
        }

        public IEnumerable<Type> GetTraitTypes(int carrierId)
        {
            lock (lockObject)
            {
                return carrierTraits.TryGetValue(carrierId, out var traits)
                    ? traits.Keys.ToList()
                    : Enumerable.Empty<Type>();
            }
        }

        #endregion

        #region 查询功能

        public IEnumerable<int> FindCarriersWithTrait<T>() where T : class, IDataTrait
        {
            lock (lockObject)
            {
                var traitType = typeof(T);
                return traitReverseIndex.TryGetValue(traitType, out var carriers)
                    ? carriers.ToList()
                    : Enumerable.Empty<int>();
            }
        }

        public IEnumerable<int> FindCarriersWithTraits(params Type[] traitTypes)
        {
            if (traitTypes == null || traitTypes.Length == 0)
                return Enumerable.Empty<int>();

            lock (lockObject)
            {
                IEnumerable<int> result = null;
                foreach (var traitType in traitTypes)
                {
                    if (!traitReverseIndex.TryGetValue(traitType, out var carriers))
                        return Enumerable.Empty<int>();

                    if (result == null)
                        result = carriers;
                    else
                        result = result.Intersect(carriers);
                }

                return result?.ToList() ?? Enumerable.Empty<int>();
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
                    if (ReferenceEquals(existing, index)) return;
                    Debug.LogWarning(
                        $"[CarrierManager] RegisterIndex skipped: table name '{index.Name}' already registered by {existing.GetType().Name}");
                    return;
                }

                indexesByTable[index.Name] = index;
            }

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
                return indexesByTable.Values.ToArray();
            }
        }

        #endregion

        #region 序列化

        public string SerializeCarrier(int carrierId)
        {
            if (carrierId <= 0)
                return null;

            // Tag 从 Tag 服务读取，且必须在进入本类锁之前取完。
            var tags = tagService?.GetTags(carrierId)
                           .OrderBy(key => key, StringComparer.Ordinal)
                           .ToArray()
                       ?? Array.Empty<string>();

            lock (lockObject)
            {
                // 存在性检查放进锁内：锁外判活后并发销毁会让下面的索引抛 KeyNotFound。
                if (!carrierTraits.TryGetValue(carrierId, out var traits))
                    return null;

                var typeId = GetCarrierTypeId(carrierId);
                var typeKey = mTypeIndex.TryGetKey(typeId, out var key) ? key : null;
                if (string.IsNullOrEmpty(typeKey))
                {
                    Debug.LogWarning($"[Carrier] Cannot serialize {carrierId}: type key not found.");
                    return null;
                }

                var carrierData = new CarrierSerializationData
                {
                    typeKey = typeKey,
                    traits = traits.Values.Select(SerializeTrait).ToArray(),
                    tags = tags
                };

                return JsonUtility.ToJson(carrierData);
            }
        }

        public int DeserializeCarrier(string json)
        {
            try
            {
                var carrierData = JsonUtility.FromJson<CarrierSerializationData>(json);
                if (carrierData == null || string.IsNullOrEmpty(carrierData.typeKey))
                {
                    Debug.LogWarning("[Carrier] Deserialization rejected: missing typeKey.");
                    return 0;
                }

                var typeId = RegisterType(carrierData.typeKey);
                if (typeId == 0)
                {
                    Debug.LogWarning($"[Carrier] Unknown typeKey '{carrierData.typeKey}'.");
                    return 0;
                }

                var carrierId = CreateCarrier(typeId);
                if (carrierId <= 0)
                    return 0;

                lock (lockObject)
                {
                    // 恢复特征数据。单个 Trait 失败不应拖垮整个载体。
                    foreach (var traitData in carrierData.traits ?? Array.Empty<TraitSerializationData>())
                    {
                        var traitType = ResolveTraitType(traitData);
                        if (traitType == null || !typeof(IDataTrait).IsAssignableFrom(traitType))
                        {
                            Debug.LogWarning(
                                $"[Carrier] Unknown trait type '{traitData.typeName}' " +
                                $"when restoring {carrierId}, skipped");
                            continue;
                        }

                        var trait = CreateTraitInstance(traitType);
                        if (trait == null) continue;

                        try
                        {
                            trait.FromJson(traitData.data);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning(
                                $"[Carrier] Failed to restore trait '{traitType.Name}' " +
                                $"on {carrierId}: {ex.Message}");
                            continue;
                        }

                        // 读档：按 key 重建运行时 id，写回 Trait 字段。
                        if (trait is IIdRefTrait idRefTrait && traitData.idRefs is { Length: > 0 })
                        {
                            var refs = ResolveSerializedRefs(traitData.idRefs);
                            try
                            {
                                idRefTrait.ApplyIdRefs(refs);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning(
                                    $"[Carrier] Failed to apply id refs to trait '{traitType.Name}' " +
                                    $"on {carrierId}: {ex.Message}");
                            }
                        }

                        carrierTraits[carrierId][traitType] = trait;

                        if (!traitReverseIndex.TryGetValue(traitType, out var carriers))
                        {
                            carriers = new HashSet<int>();
                            traitReverseIndex[traitType] = carriers;
                        }

                        carriers.Add(carrierId);
                    }
                }

                // 锁外恢复 Tag。反序列化路径不广播 TagAdded。
                if (carrierData.tags is { Length: > 0 })
                    tagService?.RestoreTags(carrierId, carrierData.tags);

                return carrierId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to deserialize carrier: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        ///     序列化单个 Trait。<see cref="IIdRefTrait" /> 会先把运行时 id 转成稳定 key
        ///     写入 idRefs，并把 id 字段置 0 后再序列化 data；完成后恢复原对象。
        /// </summary>
        private TraitSerializationData SerializeTrait(IDataTrait trait)
        {
            var data = new TraitSerializationData
            {
                typeName = trait.TraitTypeName,
                assemblyQualifiedName = trait.GetType().AssemblyQualifiedName
            };

            if (trait is not IIdRefTrait idRefTrait)
            {
                data.data = trait.ToJson();
                return data;
            }

            var refs = new List<IdRef>();
            try
            {
                idRefTrait.CaptureIdRefs(refs);
            }
            catch (Exception ex)
            {
                // Capture 失败时退回普通序列化，至少不丢数据。
                Debug.LogWarning(
                    $"[Carrier] Trait '{data.typeName}' CaptureIdRefs failed, saved as-is: {ex.Message}");
                data.data = trait.ToJson();
                return data;
            }

            try
            {
                data.idRefs = BuildSerializedRefs(refs);
                data.data = trait.ToJson();
            }
            finally
            {
                try
                {
                    idRefTrait.ApplyIdRefs(refs);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[Carrier] Trait '{data.typeName}' ApplyIdRefs failed after serialize: {ex.Message}");
                }
            }

            return data;
        }

        private IdRefSerializationData[] BuildSerializedRefs(List<IdRef> refs)
        {
            if (refs.Count == 0)
                return null;

            var result = new IdRefSerializationData[refs.Count];
            for (var i = 0; i < refs.Count; i++)
            {
                var reference = refs[i];
                string key = null;
                if (mRegistry.TryGetIndex(reference.Domain, out var index))
                    index.TryGetKey(reference.Id, out key);

                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning(
                        $"[Carrier] Cannot resolve id ref '{reference.Domain}' #{reference.Id}; " +
                        "it will not survive a save/load round trip.");
                }

                result[i] = new IdRefSerializationData { domain = reference.Domain, key = key };
            }

            return result;
        }

        private List<IdRef> ResolveSerializedRefs(IdRefSerializationData[] serialized)
        {
            var refs = new List<IdRef>(serialized.Length);
            foreach (var item in serialized)
            {
                var id = 0;
                var resolved = !string.IsNullOrEmpty(item.domain)
                               && !string.IsNullOrEmpty(item.key)
                               && mRegistry.TryGetIndex(item.domain, out var index)
                               && index.TryGet(item.key, out id);

                if (!resolved)
                {
                    Debug.LogWarning(
                        $"[Carrier] Cannot resolve id ref key '{item.domain}:{item.key}'; " +
                        "the reference will be restored as 0.");
                    id = 0;
                }

                refs.Add(new IdRef { Domain = item.domain, Id = id, Key = item.key });
            }

            return refs;
        }

        private static Type ResolveTraitType(TraitSerializationData traitData)
        {
            if (!string.IsNullOrEmpty(traitData.assemblyQualifiedName))
            {
                var qualified = Type.GetType(traitData.assemblyQualifiedName, false);
                if (qualified != null) return qualified;
            }

            return TraitTypeRegistry.Resolve(traitData.typeName);
        }

        private static IDataTrait CreateTraitInstance(Type traitType)
        {
            try
            {
                return Activator.CreateInstance(traitType) as IDataTrait;
            }
            catch (MissingMethodException)
            {
#pragma warning disable 0618
                return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(traitType) as IDataTrait;
#pragma warning restore 0618
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[Carrier] Failed to instantiate trait type '{traitType.FullName}': {ex.Message}");
                return null;
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
        /// <summary>短名，兼容旧存档。</summary>
        public string typeName;

        /// <summary>程序集限定名，新存档优先使用；旧存档缺此字段时为 null。</summary>
        public string assemblyQualifiedName;

        public string data;

        /// <summary>
        ///     Trait 内运行时 id 引用对应的稳定 key。旧存档缺此字段时为 null。
        /// </summary>
        public IdRefSerializationData[] idRefs;
    }

    /// <summary>Trait 内一处 id 引用的存档形态：只写域名与稳定 key。</summary>
    [Serializable]
    public class IdRefSerializationData
    {
        public string domain;
        public string key;
    }

    [Serializable]
    public class CarrierSerializationData
    {
        /// <summary>稳定的类型键；读档时据此重建类型 id。</summary>
        public string typeKey;

        public TraitSerializationData[] traits;

        /// <summary>归一化后的 Tag 键，按序写入以保证确定性。</summary>
        public string[] tags;
    }

    [Serializable]
    public class CarrierManagerSaveData
    {
        public string[] carriers;
    }

    #endregion
}
