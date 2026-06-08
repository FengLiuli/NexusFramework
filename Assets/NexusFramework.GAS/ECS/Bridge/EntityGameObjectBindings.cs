using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    /// [Obsolete] 使用 IGASEntityResolver（由 GASEntityMapModel 实现）替代。
    /// 内部委托给 GASEntityMapModel，提供迁移窗口期。
    /// </summary>
    [System.Obsolete("Use IGASEntityResolver from GASEntityMapModel instead.", false)]
    internal static class EntityGameObjectBindings
    {
        private static GAS.Models.GASEntityMapModel GetModel()
        {
            foreach (var arch in ArchitectureFactory.GetAllArchitectures())
            {
                if (arch is GAS.GASArchitecture gasArch)
                {
                    var model = gasArch.GetModel<GAS.Models.GASEntityMapModel>();
                    if (model != null) return model;
                }
            }
            return null;
        }

        public static void Bind(Entity entity, GameObject go)
        {
            Debug.LogWarning("[NF.GAS] EntityGameObjectBindings is obsolete, use IGASEntityResolver.BindGameObject");
            GetModel()?.BindGameObject(entity, go);
        }

        public static void Unbind(Entity entity)
        {
            Debug.LogWarning("[NF.GAS] EntityGameObjectBindings is obsolete, use IGASEntityResolver.UnbindGameObject");
            GetModel()?.UnbindGameObject(entity);
        }

        public static GameObject GetGameObject(Entity entity)
        {
            Debug.LogWarning("[NF.GAS] EntityGameObjectBindings is obsolete, use IGASEntityResolver.GetGameObject");
            return GetModel()?.GetGameObject(entity);
        }

        public static Entity GetEntity(GameObject go)
        {
            Debug.LogWarning("[NF.GAS] EntityGameObjectBindings is obsolete, use IGASEntityResolver.GetEntity");
            return GetModel()?.GetEntity(go) ?? Entity.Null;
        }

        public static void Clear()
        {
            Debug.LogWarning("[NF.GAS] EntityGameObjectBindings is obsolete, use IGASEntityResolver.Clear");
            GetModel()?.Clear();
        }
    }
}
