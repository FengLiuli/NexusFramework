using System;
using System.Collections.Generic;
using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.Services
{
    /// <summary>
    ///     <see cref="ITagService"/> 的 ECS 实现——GAS 模式下 ECS 是 Tag 状态的真相源。
    ///     <para>
    ///         Tag 的 ID 空间来自配置表（<c>exgas.gameplayTags</c>），
    ///         层级关系由 Tag 名的点分路径推导，烘焙进 <see cref="SingletonGameplayTagMap"/>
    ///         供 Burst 热路径使用。
    ///     </para>
    /// </summary>
    public class EcsTagService : AbstractService, ITagService
    {
        /// <summary>键 ↔ ID 双向映射，供托管 API 使用。</summary>
        private readonly Dictionary<string, int> mIdByKey = new();
        private readonly Dictionary<int, string> mKeyById = new();

        private ICarrierEventCallback mEventCallback;

        public EcsTagService(IArchitecture architecture)
        {
            // 本服务经 Architecture.CreateTagService() 创建后直接进 IOC 容器，
            // 不走 RegisterService<T>()，因此必须在此显式补上基类的 Architecture——
            // 否则 AbstractService 的 GetService/GetModel 扩展方法全部 NRE。
            Architecture = architecture;
        }

        #region 生命周期

        protected override void OnInit()
        {
            // 此处刻意不做任何事。
            // 单例创建需要 WorldService 已就绪，但 InitAll 的遍历顺序不保证
            // WorldService 先于本服务初始化——ECS 单例的建立与 Tag 层级烘焙
            // 一律推迟到 RebuildTagMap()（由 GASArchitecture.OnInitialized 调用，
            // 那时全部 Service/Model 均已完成初始化）。
        }

        protected override void OnDeinit()
        {
            mIdByKey.Clear();
            mKeyById.Clear();
        }

        public void Dispose()
        {
            // NativeContainer 由 WorldService.OnDeinit 统一回收，
            // 此处只清理托管状态。
            mIdByKey.Clear();
            mKeyById.Clear();
            mEventCallback = null;
        }

        public void SetEventCallback(ICarrierEventCallback callback)
        {
            mEventCallback = callback;
        }

        public bool IsCarrierKnown(CarrierId carrierId)
        {
            return Architecture.GetCarrierManager().IsCarrierValid(carrierId);
        }

        public void OnCarrierDestroyed(CarrierId carrierId)
        {
            // 无托管账本需要清理：Tag 状态始终以 ECS 为准。
            // 保留本方法是契约要求，实现方需对未知 Carrier 静默。
        }

        #endregion

        #region 烘焙

        /// <summary>
        ///     按配置表的 Tag 层级重建 <see cref="SingletonGameplayTagMap"/>。
        ///     必须在 <c>ConfigModel</c> 填充完毕、<c>WorldService</c> 就绪后调用。
        ///     <para>
        ///         修复说明：此前 <c>TagService.OnInit</c> 取到 hierarchy 后即丢弃，
        ///         只建了张空表，导致 <c>IsTagAIncludeTagB</c> 因查不到键恒返回 false，
        ///         所有层级匹配退化为"永不匹配"。
        ///     </para>
        /// </summary>
        public void RebuildTagMap()
        {
            var em = GetEntityManager();
            EnsureTagMapSingleton(em);

            var hierarchy = Architecture.GetModel<ConfigModel>().GetTagHierarchy();
            var nodes = hierarchy.Tags;
            if (nodes == null || nodes.Length == 0)
            {
                Debug.LogWarning("[EcsTagService] 标签层级为空，Tag 匹配将不可用");
                return;
            }

            BuildIdMaps(nodes);

            var parentsByCode = BuildParentLinks(nodes);
            var childrenByCode = BuildChildLinks(parentsByCode);
            WriteTagMap(nodes, parentsByCode, childrenByCode);

            Debug.Log($"[EcsTagService] 已烘焙 {nodes.Length} 个标签");
        }

        private static void EnsureTagMapSingleton(EntityManager em)
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<SingletonGameplayTagMap>());
            if (!query.IsEmptyIgnoreFilter) return;

            em.CreateSingleton(new SingletonGameplayTagMap
            {
                Map = new NativeHashMap<int, ComGameplayTag>(256, Allocator.Persistent)
            });
        }

        private void BuildIdMaps(TagNode[] nodes)
        {
            mIdByKey.Clear();
            mKeyById.Clear();

            foreach (var node in nodes)
            {
                if (node.Code <= 0 || string.IsNullOrEmpty(node.Name))
                {
                    Debug.LogError($"[EcsTagService] 非法标签定义: Code={node.Code}, Name='{node.Name}'");
                    continue;
                }

                mIdByKey[node.Name] = node.Code;
                mKeyById[node.Code] = node.Name;
            }
        }

        /// <summary>
        ///     按点分路径推导父链：<c>Effect.Buff.SpeedUp</c> 的父是 <c>Effect.Buff</c>，
        ///     再往上是 <c>Effect</c>。缺失的中间节点会报错——它意味着配置表漏了一行。
        /// </summary>
        private Dictionary<int, List<int>> BuildParentLinks(TagNode[] nodes)
        {
            var parentsByCode = new Dictionary<int, List<int>>();

            foreach (var node in nodes)
            {
                if (!mKeyById.ContainsKey(node.Code)) continue;

                var parents = new List<int>();
                var segments = node.Name.Split('.');

                for (var i = 1; i < segments.Length; i++)
                {
                    var parentName = string.Join(".", segments, 0, i);
                    if (mIdByKey.TryGetValue(parentName, out var parentCode))
                    {
                        parents.Add(parentCode);
                    }
                    else
                    {
                        Debug.LogError(
                            $"[EcsTagService] 标签 '{node.Name}' 的父节点 '{parentName}' 未在表中定义，" +
                            "层级匹配将不完整");
                    }
                }

                parentsByCode[node.Code] = parents;
            }

            return parentsByCode;
        }

        private static Dictionary<int, List<int>> BuildChildLinks(Dictionary<int, List<int>> parentsByCode)
        {
            var childrenByCode = new Dictionary<int, List<int>>();

            foreach (var kvp in parentsByCode)
            {
                if (!childrenByCode.TryGetValue(kvp.Key, out var own))
                {
                    own = new List<int>();
                    childrenByCode[kvp.Key] = own;
                }

                foreach (var parent in kvp.Value)
                {
                    if (!childrenByCode.TryGetValue(parent, out var children))
                    {
                        children = new List<int>();
                        childrenByCode[parent] = children;
                    }

                    children.Add(kvp.Key);
                }
            }

            return childrenByCode;
        }

        private void WriteTagMap(TagNode[] nodes, Dictionary<int, List<int>> parentsByCode,
            Dictionary<int, List<int>> childrenByCode)
        {
            var em = GetEntityManager();
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<SingletonGameplayTagMap>());
            if (query.IsEmptyIgnoreFilter)
            {
                Debug.LogError("[EcsTagService] SingletonGameplayTagMap 单例不存在，无法写入层级");
                return;
            }

            var singletonEntity = query.GetSingletonEntity();
            var oldMap = em.GetComponentData<SingletonGameplayTagMap>(singletonEntity);

            if (oldMap.Map.IsCreated)
            {
                foreach (var v in oldMap.Map.GetValueArray(Allocator.Temp))
                {
                    if (v.Parents.IsCreated) v.Parents.Dispose();
                    if (v.Children.IsCreated) v.Children.Dispose();
                }

                oldMap.Map.Dispose();
            }

            var map = new NativeHashMap<int, ComGameplayTag>(nodes.Length, Allocator.Persistent);
            foreach (var node in nodes)
            {
                if (!mKeyById.ContainsKey(node.Code)) continue;

                var parents = ToNativeArray(parentsByCode, node.Code);
                var children = ToNativeArray(childrenByCode, node.Code);

                map[node.Code] = new ComGameplayTag
                {
                    Code = node.Code,
                    Parents = parents,
                    Children = children
                };
            }

            em.SetComponentData(singletonEntity, new SingletonGameplayTagMap { Map = map });
        }

        private static NativeArray<int> ToNativeArray(Dictionary<int, List<int>> source, int code)
        {
            if (!source.TryGetValue(code, out var list) || list.Count == 0)
                return default;

            var array = new NativeArray<int>(list.Count, Allocator.Persistent);
            for (var i = 0; i < list.Count; i++)
                array[i] = list[i];
            return array;
        }

        #endregion

        #region ID 空间

        public int InternTag(string key)
        {
            if (!TryNormalize(key, out var normalized))
                return 0;

            if (mIdByKey.TryGetValue(normalized, out var id))
                return id;

            Debug.LogError($"[EcsTagService] 标签 '{normalized}' 未在配置表中定义");
            return 0;
        }

        public string GetTagKey(int tagId)
        {
            if (tagId <= 0) return null;
            return mKeyById.TryGetValue(tagId, out var key) ? key : null;
        }

        public bool TryGetTagId(string key, out int tagId)
        {
            tagId = 0;
            if (!TryNormalize(key, out var normalized))
                return false;

            return mIdByKey.TryGetValue(normalized, out tagId);
        }

        public bool MatchesOrIsAncestor(int actualTagId, int queryTagId)
        {
            if (actualTagId <= 0 || queryTagId <= 0) return false;
            if (actualTagId == queryTagId) return true;

            var map = GetTagMap();
            return map.HasValue && map.Value.IsTagAIncludeTagB(actualTagId, queryTagId);
        }

        #endregion

        #region 写入

        public bool AddTag(CarrierId carrierId, string key)
        {
            if (!IsCarrierKnown(carrierId)) return false;

            var id = InternTag(key);
            if (id <= 0) return false;

            var entity = GetEntity(carrierId);
            if (entity == Entity.Null) return false;

            var em = GetEntityManager();
            var buffer = em.GetBuffer<BFixedTag>(entity);
            foreach (var t in buffer)
                if (t.tag == id)
                    return false;

            buffer.Add(new BFixedTag { tag = id });

            mEventCallback?.OnTagAdded(carrierId, GetTagKey(id));
            return true;
        }

        public bool RemoveTag(CarrierId carrierId, string key)
        {
            if (!TryGetTagId(key, out var id)) return false;

            var entity = GetEntity(carrierId);
            if (entity == Entity.Null) return false;

            var em = GetEntityManager();
            var buffer = em.GetBuffer<BFixedTag>(entity);
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].tag != id) continue;

                buffer.RemoveAt(i);

                mEventCallback?.OnTagRemoved(carrierId, GetTagKey(id));
                return true;
            }

            return false;
        }

        public int AddTags(CarrierId carrierId, IEnumerable<string> keys)
        {
            if (keys == null) return 0;

            var added = new List<string>();
            foreach (var key in keys)
                if (AddTag(carrierId, key))
                    added.Add(TagKey.Normalize(key));

            return added.Count;
        }

        public int RemoveTags(CarrierId carrierId, IEnumerable<string> keys)
        {
            if (keys == null) return 0;

            var removed = 0;
            foreach (var key in keys)
                if (RemoveTag(carrierId, key))
                    removed++;

            return removed;
        }

        public int RestoreTags(CarrierId carrierId, IEnumerable<string> keys)
        {
            if (keys == null) return 0;

            var entity = GetEntity(carrierId);
            if (entity == Entity.Null) return 0;

            var em = GetEntityManager();
            var buffer = em.GetBuffer<BFixedTag>(entity);
            var restored = 0;

            foreach (var key in keys)
            {
                if (!TryGetTagId(key, out var id)) continue;

                var exists = false;
                foreach (var t in buffer)
                    if (t.tag == id)
                    {
                        exists = true;
                        break;
                    }

                if (!exists)
                {
                    buffer.Add(new BFixedTag { tag = id });
                    restored++;
                }

            }

            return restored;
        }

        #endregion

        #region 查询

        public bool HasExactTag(CarrierId carrierId, string key)
        {
            if (!TryGetTagId(key, out var id)) return false;
            return CarrierHasTag(carrierId, id, exact: true);
        }

        public bool HasTag(CarrierId carrierId, string key)
        {
            if (!TryGetTagId(key, out var id)) return false;
            return CarrierHasTag(carrierId, id, exact: false);
        }

        public bool HasAllTags(CarrierId carrierId, IEnumerable<string> keys)
        {
            if (keys == null) return false;
            foreach (var key in keys)
                if (!HasTag(carrierId, key))
                    return false;
            return true;
        }

        public bool HasAnyTag(CarrierId carrierId, IEnumerable<string> keys)
        {
            if (keys == null) return false;
            foreach (var key in keys)
                if (HasTag(carrierId, key))
                    return true;
            return false;
        }

        public bool HasNoneTags(CarrierId carrierId, IEnumerable<string> keys)
        {
            return !HasAnyTag(carrierId, keys);
        }

        public IEnumerable<string> GetTags(CarrierId carrierId)
        {
            var result = new List<string>();
            var entity = GetEntity(carrierId);
            if (entity == Entity.Null) return result;

            var em = GetEntityManager();
            foreach (var t in em.GetBuffer<BFixedTag>(entity))
            {
                var key = GetTagKey(t.tag);
                if (key != null) result.Add(key);
            }

            foreach (var t in em.GetBuffer<BTemporaryTag>(entity))
            {
                var key = GetTagKey(t.tag);
                if (key != null) result.Add(key);
            }

            return result;
        }

        public int GetTagCount(CarrierId carrierId)
        {
            var entity = GetEntity(carrierId);
            if (entity == Entity.Null) return 0;

            var em = GetEntityManager();
            return em.GetBuffer<BFixedTag>(entity).Length + em.GetBuffer<BTemporaryTag>(entity).Length;
        }

        #endregion

        #region 反查

        /// <summary>
        ///     遍历全部已知 Carrier 逐个匹配。当前 Carrier 规模是个位数，全扫可接受；
        ///     若将来规模增长，应在 ECS 侧建反向索引而非在托管层记账本
        ///     （账本会与绕过本服务的写入路径失同步）。
        /// </summary>
        public IEnumerable<CarrierId> FindCarriersWithTag(string key)
        {
            var result = new List<CarrierId>();
            if (!TryGetTagId(key, out _)) return result;

            foreach (var carrierId in GetAllCarrierIds())
                if (HasTag(carrierId, key))
                    result.Add(carrierId);

            return result;
        }

        public IEnumerable<CarrierId> FindCarriersWithTags(TagQuery query)
        {
            var result = new List<CarrierId>();
            if (query == null || query.IsEmpty) return result;

            foreach (var carrierId in GetAllCarrierIds())
                if (MatchesQuery(carrierId, query))
                    result.Add(carrierId);

            return result;
        }

        /// <summary>枚举全部已知载体，用于跨类型遍历。</summary>
        private IEnumerable<CarrierId> GetAllCarrierIds()
        {
            return Architecture.GetCarrierManager().GetAllCarriers();
        }

        private bool MatchesQuery(CarrierId carrierId, TagQuery query)
        {
            foreach (var key in query.All)
                if (!HasTag(carrierId, key))
                    return false;

            if (query.Any.Count > 0)
            {
                var any = false;
                foreach (var key in query.Any)
                    if (HasTag(carrierId, key))
                    {
                        any = true;
                        break;
                    }

                if (!any) return false;
            }

            foreach (var key in query.None)
                if (HasTag(carrierId, key))
                    return false;

            return true;
        }

        #endregion

        #region 私有辅助

        private static bool TryNormalize(string key, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(key)) return false;

            try
            {
                normalized = TagKey.Normalize(key);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }


        /// <summary>同时查永久标签与临时标签（GE 授予的）。</summary>
        private bool CarrierHasTag(CarrierId carrierId, int tagId, bool exact)
        {
            var entity = GetEntity(carrierId);
            if (entity == Entity.Null) return false;

            var em = GetEntityManager();

            var fixedTags = em.GetBuffer<BFixedTag>(entity);
            foreach (var t in fixedTags)
                if (exact ? t.tag == tagId : t.tag == tagId || MatchesOrIsAncestor(t.tag, tagId))
                    return true;

            var tempTags = em.GetBuffer<BTemporaryTag>(entity);
            foreach (var t in tempTags)
                if (exact ? t.tag == tagId : t.tag == tagId || MatchesOrIsAncestor(t.tag, tagId))
                    return true;

            return false;
        }

        private Entity GetEntity(CarrierId carrierId)
        {
            return Architecture.GetModel<GASEntityMapModel>().GetGASEntity(carrierId);
        }

        private EntityManager GetEntityManager()
        {
            return Architecture.GetService<WorldService>().EntityManager;
        }

        private SingletonGameplayTagMap? GetTagMap()
        {
            var em = GetEntityManager();
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<SingletonGameplayTagMap>());
            if (query.IsEmptyIgnoreFilter) return null;

            return em.GetComponentData<SingletonGameplayTagMap>(query.GetSingletonEntity());
        }

        #endregion
    }
}
