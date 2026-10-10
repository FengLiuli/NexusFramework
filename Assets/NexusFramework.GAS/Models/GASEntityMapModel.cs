using System.Collections.Generic;
using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.ECS;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.Models
{
    /// <summary>
    ///     Carrier ↔ Entity ↔ GameObject 三向映射。
    ///
    ///     两条主链（CarrierId ↔ Entity、CarrierId ↔ GameObject）由框架的
    ///     <see cref="CarrierIndex{TValue}" /> 承载并注册进 <see cref="ICarrierManager" />，
    ///     载体销毁时自动清理。Entity ↔ GameObject 为二者派生——通过 <c>Entity→CarrierId→GameObject</c> 组合推导，
    ///     仅需一张 <c>GameObject → Entity</c> 查表。
    ///
    ///     GameObject 是 Unity 引用类型，销毁后重载为「假 null」，故 <see cref="GetGameObject" /> 保留惰性过期剔除——
    ///     这是索引容器不提供的 Model 层策略。
    /// </summary>
    public class GASEntityMapModel : AbstractModel, IGASEntityResolver
    {
        // 索引表名（本 Model 私有；表随 Architecture 存活，可经名字跨组件取用）。
        private const string EntityIndexName = "gas.entityMap.carrier.entity";
        private const string GameObjectIndexName = "gas.entityMap.carrier.gameObject";

        // ── 主链：CarrierId ↔ Entity ──────────────────────────
        private readonly CarrierIndex<Entity> _entities;

        // ── 主链：CarrierId ↔ GameObject ─────────────────────
        private readonly CarrierIndex<GameObject> _gameObjects;

        // ── 派生：GameObject → Entity（配合 _entities 反查给出 Entity↔GameObject 双向）──
        private readonly Dictionary<GameObject, Entity> _goToEntity = new();

        public GASEntityMapModel()
        {
            _entities = new CarrierIndex<Entity>(EntityIndexName);
            _gameObjects = new CarrierIndex<GameObject>(GameObjectIndexName);
        }

        // ═══════════════════════════════════════════════════
        //  CarrierId ↔ Entity
        // ═══════════════════════════════════════════════════

        public void Bind(CarrierId carrierId, Entity gasEntity)
        {
            if (!carrierId.IsValid || gasEntity == Entity.Null)
            {
                Debug.LogWarning($"[GASEntityMapModel] Bind skipped: invalid carrier {carrierId} or null entity");
                return;
            }

            _entities.Bind(carrierId, gasEntity);
        }

        /// <summary>解除 Carrier↔Entity 绑定。会联动解绑该载体名下的 GameObject 绑定。</summary>
        public void Unbind(CarrierId carrierId)
        {
            _entities.Unbind(carrierId); // 触发 OnBindingRemoved → TeardownGameObject
        }

        public Entity GetGASEntity(CarrierId carrierId)
        {
            return _entities.TryGet(carrierId, out var e) ? e : Entity.Null;
        }

        public CarrierId GetCarrierId(Entity gasEntity)
        {
            if (gasEntity == Entity.Null) return CarrierId.Invalid;
            return _entities.TryGetCarrier(gasEntity);
        }

        public bool ContainsCarrier(CarrierId carrierId)
        {
            return _entities.Contains(carrierId);
        }

        public bool ContainsEntity(Entity gasEntity)
        {
            return gasEntity != Entity.Null && _entities.TryGetCarrier(gasEntity).IsValid;
        }

        // ═══════════════════════════════════════════════════
        //  IGASEntityResolver — Entity ↔ GameObject
        // ═══════════════════════════════════════════════════

        public void BindGameObject(Entity entity, GameObject go)
        {
            if (entity == Entity.Null)
            {
                Debug.LogWarning("[GASEntityMapModel] BindGameObject skipped: entity is Entity.Null");
                return;
            }

            if (go == null)
            {
                Debug.LogWarning("[GASEntityMapModel] BindGameObject skipped: GameObject is null");
                return;
            }

            var carrier = _entities.TryGetCarrier(entity);
            if (!carrier.IsValid)
            {
                Debug.LogWarning(
                    $"[GASEntityMapModel] BindGameObject skipped: entity {entity} is not bound to any carrier");
                return;
            }

            // 同一 GO 已绑定不同 Entity → 拒绝
            if (_goToEntity.TryGetValue(go, out var existingEntity) && existingEntity != entity)
            {
                Debug.LogWarning(
                    $"[GASEntityMapModel] BindGameObject skipped: GameObject '{go.name}' is already bound to a different Entity, ignoring rebind request");
                return;
            }

            // 该载体已绑定的 GO
            var alreadyBound = _gameObjects.TryGet(carrier, out var oldGo);
            if (alreadyBound && ReferenceEquals(oldGo, go)) return; // 完全相同 → no-op

            // 重复绑定同一载体 → 覆盖（先解绑旧 GO）
            if (alreadyBound && oldGo != null)
            {
                Debug.LogWarning($"[GASEntityMapModel] Entity already bound to '{oldGo.name}', replacing with '{go.name}'");
                _goToEntity.Remove(oldGo);
                var oldRef = oldGo.GetComponent<GASEntityRef>();
                if (oldRef != null) oldRef.MarkUnbound();
            }

            _gameObjects.Bind(carrier, go);
            _goToEntity[go] = entity;

            // 自动添加/更新 GASEntityRef
            var refComp = go.GetComponent<GASEntityRef>();
            if (refComp == null) refComp = go.AddComponent<GASEntityRef>();
            refComp.Initialize(entity, this);
        }

        public void UnbindGameObject(Entity entity)
        {
            if (entity == Entity.Null) return;

            var carrier = _entities.TryGetCarrier(entity);
            if (!carrier.IsValid) return;

            TeardownGameObject(carrier);
        }

        public GameObject GetGameObject(Entity entity)
        {
            if (entity == Entity.Null) return null;

            var carrier = _entities.TryGetCarrier(entity);
            if (!carrier.IsValid) return null;

            if (!_gameObjects.TryGet(carrier, out var go)) return null;

            // 过期检测：GameObject 被外部 Destroy 后 Unity 重载 null
            if (go == null)
            {
                TeardownGameObject(carrier);
                return null;
            }

            return go;
        }

        public Entity GetEntity(GameObject go)
        {
            if (go == null) return Entity.Null;
            return _goToEntity.GetValueOrDefault(go, Entity.Null);
        }

        public bool IsEntityBound(Entity entity)
        {
            return GetGameObject(entity) != null;
        }

        public bool IsGameObjectBound(GameObject go)
        {
            return go != null && _goToEntity.ContainsKey(go);
        }

        // ═══════════════════════════════════════════════════
        //  内部
        // ═══════════════════════════════════════════════════

        /// <summary>解除某载体的 GameObject 绑定：清主链条目、派生查表，并标记 GASEntityRef 失效。</summary>
        private void TeardownGameObject(CarrierId carrier)
        {
            if (!_gameObjects.TryGet(carrier, out var go)) return;

            _gameObjects.Unbind(carrier);
            if (go != null)
            {
                _goToEntity.Remove(go);
                var refComp = go.GetComponent<GASEntityRef>();
                if (refComp != null) refComp.MarkUnbound();
            }
        }

        // ═══════════════════════════════════════════════════
        //  生命周期
        // ═══════════════════════════════════════════════════

        public void Clear()
        {
            // 先清 GameObject 侧（移除会改集合，故遍历快照）
            foreach (var carrier in new List<CarrierId>(_gameObjects.Carriers))
                TeardownGameObject(carrier);

            // 再清 Entity 侧（其 OnBindingRemoved 会再调 TeardownGameObject，此时已空，幂等）
            foreach (var carrier in new List<CarrierId>(_entities.Carriers))
                _entities.Unbind(carrier);

            _goToEntity.Clear();
        }

        protected override void OnInit()
        {
            // 托管进 CarrierManager：载体销毁时自动清理两条主链
            this.RegisterCarrierIndex(_entities);
            this.RegisterCarrierIndex(_gameObjects);

            // 载体与 Entity 解绑/销毁、或 Entity 被覆盖驱逐时，联动清理 GameObject 侧，保持三向一致
            _entities.OnBindingRemoved += (carrier, _) => TeardownGameObject(carrier);
        }

        protected override void OnDeinit()
        {
            Clear();
        }
    }
}
