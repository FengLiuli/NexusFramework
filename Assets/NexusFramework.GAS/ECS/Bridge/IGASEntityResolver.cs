using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    /// ASC Entity 与 GameObject 的双向解析契约。
    /// 仅用于解析 ASC Entity（从 Carrier 派生的 Entity），不处理 Cue/Effect 等纯 ECS 内部实体。
    /// </summary>
    public interface IGASEntityResolver
    {
        /// <summary>Entity → GameObject 前向查询，过期或未绑定返回 null</summary>
        GameObject GetGameObject(Entity entity);

        /// <summary>GameObject → Entity 反向查询，未绑定返回 Entity.Null</summary>
        Entity GetEntity(GameObject go);

        /// <summary>建立 Entity ↔ GameObject 双向绑定，重复绑定同一 Entity 会覆盖旧绑定</summary>
        void BindGameObject(Entity entity, GameObject go);

        /// <summary>移除 Entity 绑定，实体不存在时静默返回</summary>
        void UnbindGameObject(Entity entity);

        /// <summary>Entity 是否已绑定且 GameObject 仍存活</summary>
        bool IsEntityBound(Entity entity);

        /// <summary>GameObject 是否已绑定</summary>
        bool IsGameObjectBound(GameObject go);
    }
}
