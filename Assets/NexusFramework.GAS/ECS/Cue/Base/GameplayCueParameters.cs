using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public enum CueSourceType
    {
        None,
        AbilitySystem,
        GameplayEffect
    }
    
    public class GameplayCueParametersBase
    {
        public CueSourceType SourceType;
        public Entity entity;
    }
}