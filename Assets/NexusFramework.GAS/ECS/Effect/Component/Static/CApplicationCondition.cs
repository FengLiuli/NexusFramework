using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CApplicationCondition : IComponentData
    {
        public NativeArray<int> conditions;
    }
    
    public sealed class ConfApplicationCondition:GameplayEffectComponentConfig
    {
        public int[] tags;
        
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<CApplicationCondition>(ge);
            _entityManager.SetComponentData(ge, new CApplicationCondition
            {
                conditions = new NativeArray<int>(tags, Allocator.Persistent)
            });
        }
    }
}