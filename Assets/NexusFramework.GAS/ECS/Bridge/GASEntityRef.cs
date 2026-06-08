using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    /// 轻量 MonoBehaviour，挂载在 GameObject 上，缓存关联的 ECS Entity 和 IGASEntityResolver。
    /// 供游戏层通过 GetComponent&lt;GASEntityRef&gt;() / GetComponentInParent&lt;GASEntityRef&gt;() 快速反查 Entity。
    /// 由 GASEntityMapModel.BindGameObject() 自动添加，不应手动挂载。
    /// GameObject 销毁时自动调用 OnDestroy 通知 Model 解绑。
    /// </summary>
    public class GASEntityRef : MonoBehaviour
    {
        /// <summary>绑定的 ECS Entity，解绑后为 Entity.Null</summary>
        public Entity Entity { get; private set; }

        /// <summary>所属绑定 Model，用于 GameObject 销毁时自动解绑</summary>
        public IGASEntityResolver Resolver { get; private set; }

        internal void Initialize(Entity entity, IGASEntityResolver resolver)
        {
            Entity = entity;
            Resolver = resolver;
        }

        /// <summary>标记为已解绑，不触发 OnDestroy 中的解绑逻辑</summary>
        internal void MarkUnbound()
        {
            Entity = Entity.Null;
            Resolver = null;
        }

        private void OnDestroy()
        {
            // GameObject 被外部 Destroy 时，自动通知 Model 解绑
            if (Entity != Entity.Null && Resolver != null)
            {
                Resolver.UnbindGameObject(Entity);
            }
        }
    }
}
