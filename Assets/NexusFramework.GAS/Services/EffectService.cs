using NexusFramework;
using NexusFramework.DataCarrier;
using Unity.Entities;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;

namespace NexusFramework.GAS.Services
{
    public class EffectService : AbstractService
    {
        protected override void OnInit() { }
        protected override void OnDeinit() { }

        public void ApplyEffect(int configId, CarrierId target, CarrierId source)
        {
            var model = this.GetModel<GASEntityMapModel>();
            var targetEntity = model.GetGASEntity(target);
            var sourceEntity = model.GetGASEntity(source);
            var em = this.GetService<WorldService>().EntityManager;
            if (targetEntity == Entity.Null) return;

            GameplayEffectComponentConfig.SetEntityManager(em);
            var loader = this.GetModel<ConfigModel>();
            var configs = loader.GetGameplayEffectConfig(configId);
            if (configs == null) return;

            var geEntity = em.CreateEntity();
            foreach (var config in configs)
                config.LoadToGameplayEffectEntity(geEntity);

            em.AddComponent<CEffectInUsage>(geEntity);
            em.AddComponent<WipInstantiateEffect>(geEntity);
            em.SetComponentData(geEntity, new CEffectInUsage
            {
                Source = sourceEntity,
                Target = targetEntity
            });
        }
    }
}