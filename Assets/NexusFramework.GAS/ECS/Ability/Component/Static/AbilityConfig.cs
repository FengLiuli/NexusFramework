namespace NexusFramework.GAS.ECS
{
    public class AbilityConfig
    {
        public AbilityComponentConfig[] ComponentConfigs { get; }

        public AbilityConfig(AbilityComponentConfig[] configs)
        {
            ComponentConfigs = configs;
        }
    }
}
