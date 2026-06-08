using System.Collections.Generic;
using Unity.Entities;
using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.ECS;
using UnityEngine;

namespace NexusFramework.GAS.Models
{
    public class GASEntityMapModel : AbstractModel, IGASEntityResolver
    {
        // ── CarrierId ↔ Entity ──────────────────────────
        private readonly Dictionary<CarrierId, Entity> _carrierToEntity = new();
        private readonly Dictionary<Entity, CarrierId> _entityToCarrier = new();

        // ── Entity ↔ GameObject ──────────────────────────
        private readonly Dictionary<Entity, GameObject> _entityToGameObject = new();
        private readonly Dictionary<GameObject, Entity> _gameObjectToEntity = new();

        // ═══════════════════════════════════════════════════
        //  CarrierId ↔ Entity  (已有 API，字段名已更新)
        // ═══════════════════════════════════════════════════

        public void Bind(CarrierId carrierId, Entity gasEntity)
        {
            _carrierToEntity[carrierId] = gasEntity;
            _entityToCarrier[gasEntity] = carrierId;
        }

        public void Unbind(CarrierId carrierId)
        {
            if (_carrierToEntity.TryGetValue(carrierId, out var entity))
            {
                _entityToCarrier.Remove(entity);
                _carrierToEntity.Remove(carrierId);
            }
        }

        public Entity GetGASEntity(CarrierId carrierId)
            => _carrierToEntity.GetValueOrDefault(carrierId, Entity.Null);

        public CarrierId GetCarrierId(Entity gasEntity)
            => _entityToCarrier.GetValueOrDefault(gasEntity, new CarrierId());

        public bool ContainsCarrier(CarrierId carrierId)
            => _carrierToEntity.ContainsKey(carrierId);

        public bool ContainsEntity(Entity gasEntity)
            => _entityToCarrier.ContainsKey(gasEntity);

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

            // 同一 GO 已绑定不同 Entity → 拒绝
            if (_gameObjectToEntity.TryGetValue(go, out var existingEntity) && existingEntity != entity)
            {
                Debug.LogWarning($"[GASEntityMapModel] BindGameObject skipped: GameObject '{go.name}' is already bound to a different Entity, ignoring rebind request");
                return;
            }

            // 重复绑定同一 Entity → 覆盖（先解绑旧 GO）
            if (_entityToGameObject.TryGetValue(entity, out var oldGo))
            {
                if (oldGo == go) return; // 完全相同的绑定，no-op
                Debug.LogWarning($"[GASEntityMapModel] Entity already bound to '{oldGo?.name}', replacing with '{go.name}'");
                _gameObjectToEntity.Remove(oldGo);
                var oldRef = oldGo != null ? oldGo.GetComponent<GASEntityRef>() : null;
                if (oldRef != null) oldRef.MarkUnbound();
            }

            _entityToGameObject[entity] = go;
            _gameObjectToEntity[go] = entity;

            // 自动添加/更新 GASEntityRef
            var refComp = go.GetComponent<GASEntityRef>();
            if (refComp == null) refComp = go.AddComponent<GASEntityRef>();
            refComp.Initialize(entity, this);
        }

        public void UnbindGameObject(Entity entity)
        {
            if (!_entityToGameObject.TryGetValue(entity, out var go))
                return;

            _entityToGameObject.Remove(entity);
            if (go != null)
            {
                _gameObjectToEntity.Remove(go);
                var refComp = go.GetComponent<GASEntityRef>();
                if (refComp != null) refComp.MarkUnbound();
            }
        }

        public GameObject GetGameObject(Entity entity)
        {
            if (entity == Entity.Null) return null;
            if (!_entityToGameObject.TryGetValue(entity, out var go)) return null;

            // 过期检测：GameObject 被外部 Destroy 后 Unity 重载 null
            if (go == null)
            {
                _entityToGameObject.Remove(entity);
                return null;
            }
            return go;
        }

        public Entity GetEntity(GameObject go)
        {
            if (go == null) return Entity.Null;
            return _gameObjectToEntity.GetValueOrDefault(go, Entity.Null);
        }

        public bool IsEntityBound(Entity entity)
            => _entityToGameObject.ContainsKey(entity) && _entityToGameObject[entity] != null;

        public bool IsGameObjectBound(GameObject go)
            => go != null && _gameObjectToEntity.ContainsKey(go);

        // ═══════════════════════════════════════════════════
        //  生命周期
        // ═══════════════════════════════════════════════════

        public new void Clear()
        {
            // 清理所有 GASEntityRef
            foreach (var kv in _entityToGameObject)
            {
                if (kv.Value != null)
                {
                    var refComp = kv.Value.GetComponent<GASEntityRef>();
                    if (refComp != null) refComp.MarkUnbound();
                }
            }

            _carrierToEntity.Clear();
            _entityToCarrier.Clear();
            _entityToGameObject.Clear();
            _gameObjectToEntity.Clear();
        }

        protected override void OnInit() { }

        protected override void OnDeinit()
        {
            Clear();
        }
    }
}
