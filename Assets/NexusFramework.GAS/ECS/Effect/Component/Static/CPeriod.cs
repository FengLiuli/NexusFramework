using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CPeriod : IComponentData
    {
        public int Period;
        public bool ResetTimeCountWhenDeactivated;
        
        public NativeArray<Entity> GameplayEffects;
        
        // -------------------------------------以下是RUNTIME数据，不需要初始化---------------------------------------//
        public int StartTime;
    }
    
    public sealed class ConfPeriod:GameplayEffectComponentConfig
    {
        public int Period;
        public bool ResetTimeCountWhenDeactivated;
        public GameplayEffectComponentConfig[][] GameplayEffectSettings;

        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var geEntities = new NativeArray<Entity>(GameplayEffectSettings.Length, Allocator.Persistent);
            for (var i = 0; i < GameplayEffectSettings.Length; i++)
            {
                var comConfigs = GameplayEffectSettings[i];
                geEntities[i] = GEConfigHelper.CreateGameplayEffectEntity(_entityManager, comConfigs);
            }
            
            _entityManager.AddComponent<CPeriod>(ge);
            _entityManager.SetComponentData(ge, new CPeriod
            {
                Period = Period,
                ResetTimeCountWhenDeactivated = ResetTimeCountWhenDeactivated,
                GameplayEffects = geEntities,
            });
        }
    }
}