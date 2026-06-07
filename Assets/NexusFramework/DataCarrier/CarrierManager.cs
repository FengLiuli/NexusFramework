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
        }

        public void Deinit()
        {
            typeNameToId.Clear();
            typeIdToName.Clear();
            carrierTraits.Clear();
            carriersByType.Clear();
            nextUniqueIds.Clear();
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

            if (uniqueId > 0x000FFFFFFFFFFFFF)
            {
                Debug.LogError("UniqueId exceeds 52-bit limit");
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
            if (!IsCarrierValid(carrierId))
                return false;

            lock (lockObject)
            {
                carrierTraits.Remove(carrierId);
                carriersByType[carrierId.TypeId].Remove(carrierId);

                return true;
            }
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
                return carrierTraits
                    .Where(kvp => kvp.Value.ContainsKey(traitType))
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }

        public IEnumerable<CarrierId> FindCarriersWithTraits(params Type[] traitTypes)
        {
            if (traitTypes == null || traitTypes.Length == 0)
                return Enumerable.Empty<CarrierId>();

            lock (lockObject)
            {
                return carrierTraits
                    .Where(kvp => traitTypes.All(type => kvp.Value.ContainsKey(type)))
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }

        #endregion

        #region 序列化

        public string SerializeCarrier(CarrierId carrierId)
        {
            if (!IsCarrierValid(carrierId))
                return null;

            lock (lockObject)
            {
                var carrierData = new CarrierSerializationData
                {
                    carrierId = carrierId.RawValue,
                    traits = carrierTraits[carrierId].Values.Select(trait => new TraitSerializationData
                    {
                        typeName = trait.TraitTypeName,
                        data = trait.ToJson()
                    }).ToArray()
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
                        }
                    }
                }

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
    }

    [Serializable]
    public class CarrierManagerSaveData
    {
        public string[] carriers;
    }

    #endregion
}