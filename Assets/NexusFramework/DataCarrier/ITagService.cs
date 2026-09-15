using System;
using System.Collections.Generic;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     Carrier Tag 服务契约。
    ///     槽位空间由实现自行定义：托管实现为 intern 槽位，GAS 实现为配置表的 Tag ID。
    ///     所有方法以 string 键与 int 槽位交互，不暴露 <see cref="RuntimeTagSlot"/>——该类型是
    ///     托管实现的内部细节，其他程序集无法构造。
    /// </summary>
    public interface ITagService : ICanInit, IDisposable
    {
        // ───────────────────────── 生命周期 ─────────────────────────

        /// <summary>
        ///     Carrier 被销毁时由 CarrierManager 经 <see cref="ICarrierEventCallback"/> 通知。
        ///     实现方必须清理该 Carrier 的正向与反向索引残留，且对未知 Carrier 静默。
        /// </summary>
        void OnCarrierDestroyed(CarrierId carrierId);

        /// <summary>
        ///     该 Carrier 是否存在于载体管理器中。实现方应以载体管理器为准，
        ///     不依赖"是否收到过创建通知"——反序列化会重建载体但不触发创建通知。
        /// </summary>
        bool IsCarrierKnown(CarrierId carrierId);

        /// <summary>
        ///     注入事件回调。应与 CarrierManager 使用同一个回调实例，
        ///     使 Tag 变更事件与载体生命周期事件走同一条广播路径。
        /// </summary>
        void SetEventCallback(ICarrierEventCallback callback);

        // ───────────────────────── ID 空间 ─────────────────────────

        /// <summary>
        ///     将 Tag 键归一化后取到槽位，不存在则创建。返回值大于 0 为有效槽位，
        ///     0 表示无效（调用方必须判断）。ECS 实现下键必须已存在于配置表。
        /// </summary>
        int InternTag(string key);

        /// <summary>
        ///     槽位还原为归一化后的 Tag 键。槽位不属于本服务时返回 null。
        /// </summary>
        string GetTagKey(int tagId);

        /// <summary>
        ///     查询已存在的槽位，不产生副作用。与 <see cref="InternTag"/> 的区别是不会创建。
        /// </summary>
        bool TryGetTagId(string key, out int tagId);

        /// <summary>
        ///     判断 actual 是否等于 query 或为 query 的后代（父 Tag 命中子 Tag）。
        ///     层级匹配的唯一判据。
        /// </summary>
        bool MatchesOrIsAncestor(int actualTagId, int queryTagId);

        // ───────────────────────── 写入 ─────────────────────────

        bool AddTag(CarrierId carrierId, string key);

        bool RemoveTag(CarrierId carrierId, string key);

        /// <summary>批量添加，返回成功添加的条数。</summary>
        int AddTags(CarrierId carrierId, IEnumerable<string> keys);

        /// <summary>批量移除，返回成功移除的条数。</summary>
        int RemoveTags(CarrierId carrierId, IEnumerable<string> keys);

        /// <summary>
        ///     反序列化专用：批量写入且不触发 TagAdded 事件。
        ///     加载存档时不应引发一连串 Tag 变更广播。
        /// </summary>
        int RestoreTags(CarrierId carrierId, IEnumerable<string> keys);

        // ───────────────────────── 查询 ─────────────────────────

        bool HasExactTag(CarrierId carrierId, string key);

        /// <summary>层级匹配：父 Tag 命中子 Tag。</summary>
        bool HasTag(CarrierId carrierId, string key);

        bool HasAllTags(CarrierId carrierId, IEnumerable<string> keys);

        bool HasAnyTag(CarrierId carrierId, IEnumerable<string> keys);

        bool HasNoneTags(CarrierId carrierId, IEnumerable<string> keys);

        /// <summary>该 Carrier 当前全部 Tag 键。顺序未定义，序列化时调用方自行排序。</summary>
        IEnumerable<string> GetTags(CarrierId carrierId);

        int GetTagCount(CarrierId carrierId);

        // ───────────────────────── 反查 ─────────────────────────

        IEnumerable<CarrierId> FindCarriersWithTag(string key);

        IEnumerable<CarrierId> FindCarriersWithTags(TagQuery query);
    }
}
