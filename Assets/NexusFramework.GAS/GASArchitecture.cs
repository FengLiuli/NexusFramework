using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Services;
using NexusFramework.GAS.Models;

namespace NexusFramework.GAS
{
    public abstract class GASArchitecture : Architecture
    {
        public override string ArchitectureType => "GAS";

        protected virtual IConfigLoader CreateConfigLoader() => new JsonConfigLoader();

        protected override void OnInit()
        {
            RegisterModel(new GASEntityMapModel());
            RegisterModel(new ConfigModel());
            RegisterService(new WorldService());
            RegisterService(new TimerService());
            RegisterService(new EventBridgeService());
            RegisterService(new TagService());
            RegisterService(new EffectService());
            RegisterService(new AbilityService());
            RegisterService(new CueService());
            RegisterService(new AttributeService());
            RegisterUtility(CreateConfigLoader());
        }

        public CarrierId CreateGASCarrier(string typeName)
        {
            var carrierId = GetCarrierManager().CreateCarrier(typeName);
            var worldService = this.GetService<WorldService>();
            var entity = worldService.EntityManager.CreateEntity();
            worldService.SetupGASEntity(entity);
            this.GetModel<GASEntityMapModel>().Bind(carrierId, entity);
            return carrierId;
        }

        public void DestroyGASCarrier(CarrierId carrierId)
        {
            var model = this.GetModel<GASEntityMapModel>();
            if (!model.ContainsCarrier(carrierId)) return;

            var entity = model.GetGASEntity(carrierId);
            model.Unbind(carrierId);
            var ws = this.GetService<WorldService>();
            if (ws.EntityManager.Exists(entity))
            {
                var em = ws.EntityManager;
                try
                {
                    if (em.HasBuffer<ECS.BEAttrSet>(entity))
                    {
                        var attrSets = em.GetBuffer<ECS.BEAttrSet>(entity);
                        for (int i = 0; i < attrSets.Length; i++)
                        {
                            var attrs = attrSets[i].Attributes;
                            if (attrs.IsCreated) attrs.Dispose();
                        }
                    }
                }
                finally
                {
                    em.DestroyEntity(entity);
                }
            }

            GetCarrierManager().DestroyCarrier(carrierId);
        }

        protected override void OnShutdown()
        {
            GASInternalBridge.Clear();
        }
    }
}
