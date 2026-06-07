using UnityEngine;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public abstract class ModMagnitudeCalculationBase
    {
        public string Description;
        
        public abstract void InitParameters(XParam parameter);
        
        public abstract float CalculateMagnitude(MmcContext mmcContext, float magnitude);

        public void OnAddMmc(Entity gameplayEffect, EntityManager em, int targetAttrSetCode, int targetAttrCode)
        {
            if (!em.HasComponent<CEffectInUsage>(gameplayEffect)) return;
            var inUsage = em.GetComponentData<CEffectInUsage>(gameplayEffect);
            var context = new MmcContext
            {
                EffectEntity = gameplayEffect,
                Source = inUsage.Source,
                Target = inUsage.Target
            };
            OnAdded(context, targetAttrSetCode, targetAttrCode);
        }  
        
        public void OnRemoveMmc()
        {
            OnRemoved();
        }  
        
        
        protected virtual void OnAdded(MmcContext context, int targetAttrSetCode, int targetAttrCode) { }
        
        protected virtual void OnRemoved() { }
        
    }
    
    public abstract class ModMagnitudeCalculationBase<T> : ModMagnitudeCalculationBase
        where T : XParam
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