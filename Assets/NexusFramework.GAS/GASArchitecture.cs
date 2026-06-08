using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Services;
using NexusFramework.GAS.Models;
using Unity.Entities;
using UnityEngine;

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

        public CarrierId CreateGASCarrier(string typeName, GameObject go = null)
        {
            var carrierId = GetCarrierManager().CreateCarrier(typeName);
            var worldService = this.GetService<WorldService>();
            var entity = worldService.EntityManager.CreateEntity();
            worldService.SetupGASEntity(entity);
            var model = this.GetModel<GASEntityMapModel>();
            model.Bind(carrierId, entity);

            if (go != null)
                model.BindGameObject(entity, go);

            return carrierId;
        }

        /// <summary>
        /// 为已有 Carrier 绑定对应的 GameObject。
        /// 由拥有 GameObject 的游戏上层逻辑调用。
        /// </summary>
        public void BindGameObjectForCarrier(CarrierId carrierId, GameObject go)
        {
            var model = this.GetModel<GASEntityMapModel>();
            var entity = model.GetGASEntity(carrierId);
            if (entity == Entity.Null) return;
            model.BindGameObject(entity, go);
        }

        public void DestroyGASCarrier(CarrierId carrierId)
        {
            var model = this.GetModel<GASEntityMapModel>();
            if (!model.ContainsCarrier(carrierId)) return;

            var entity = model.GetGASEntity(carrierId);

            // 清理 Entity↔GameObject 绑定
            model.UnbindGameObject(entity);
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
