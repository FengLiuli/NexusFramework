using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    internal static class EntityGameObjectBindings
    {
        private static readonly Dictionary<Entity, GameObject> _forward = new();
        private static readonly Dictionary<GameObject, Entity> _reverse = new();

        public static void Bind(Entity entity, GameObject go)
        {
            _forward[entity] = go;
            _reverse[go] = entity;
        }

        public static void Unbind(Entity entity)
        {
            if (_forward.TryGetValue(entity, out var go))
            {
                _forward.Remove(entity);
                _reverse.Remove(go);
            }
        }

        public static GameObject GetGameObject(Entity entity)
        {
            return _forward.TryGetValue(entity, out var go) ? go : null;
        }

        public static Entity GetEntity(GameObject go)
        {
            if (go == null) return Entity.Null;
            return _reverse.TryGetValue(go, out var entity) ? entity : Entity.Null;
        }

        public static void Clear()
        {
            _forward.Clear();
            _reverse.Clear();
        }
    }
}
