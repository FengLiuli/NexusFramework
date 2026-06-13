using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    public abstract class TargetCatcherBase
    {
        public Entity Owner;
        protected IGASEntityResolver _entityResolver;

        public virtual void Init(Entity owner)
        {
            Owner = owner;
        }

        /// <summary>注入 Entity-GameObject 解析器，应在 Init/InitParameters 之前调用</summary>
        public void SetEntityResolver(IGASEntityResolver resolver)
        {
            _entityResolver = resolver;
        }

        /// <summary>通过注入的 resolver 将 Entity 解析为 GameObject，未注入或未绑定返回 null</summary>
        protected GameObject ResolveGameObject(Entity entity)
        {
            if (_entityResolver == null || entity == Entity.Null) return null;
            return _entityResolver.GetGameObject(entity);
        }

        /// <summary>通过注入的 resolver 将 GameObject 解析为 Entity，未注入或未绑定返回 Entity.Null</summary>
        protected Entity ResolveEntity(GameObject go)
        {
            if (_entityResolver == null || go == null) return Entity.Null;
            return _entityResolver.GetEntity(go);
        }

        [Obsolete("请使用CatchTargetsNonAlloc方法来避免产生垃圾收集（GC）。")]  
        public List<Entity> CatchTargets(Entity mainTarget)  
        {  
            var result = new List<Entity>();  
            CatchTargetsNonAlloc(mainTarget, result);  
            return result;  
        }  

        public void CatchTargetsNonAllocSafe(Entity mainTarget, ref List<Entity> results)  
        {  
            results.Clear();  
            CatchTargetsNonAlloc(mainTarget, results);  
        }  

        protected abstract void CatchTargetsNonAlloc(Entity mainTarget, List<Entity> results);  

        public virtual void InitParameters(XParam parameter) { }  
        
        public virtual void OnEditorPreview(GameObject obj) { }  
    }
    
    public abstract class TargetCatcherBase<T> : TargetCatcherBase where T : XParam
    {
        public T Parameter { get; private set; }

        public override void InitParameters(XParam parameter)
        {
            if (parameter is T t)
                Parameter = t;
#if UNITY_EDITOR
            else
                Debug.LogError($"Parameter type mismatch: expected {typeof(T)}, but got {parameter.GetType()}");
#endif
        }
    }
}