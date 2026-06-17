using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Services;
using NexusFramework.GAS.Models;
using Unity.Entities;
using Unity.Collections;
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
        /// 创建 Carrier + ECS Entity，并从 ConfigModel 读取 ASC 配置自动初始化。
        /// 等级、标签、属性集、技能授予一步完成。
        /// 当 ascId 不存在时，返回骨架 Entity（无标签/属性/技能），不抛异常。
        /// </summary>
        public CarrierId CreateGASCarrier(string typeName, int ascId, GameObject go = null)
        {
            // 1. 搭骨架（复用基础重载）
            var carrierId = CreateGASCarrier(typeName, go);
            var entity = this.GetModel<GASEntityMapModel>().GetGASEntity(carrierId);

            // 2. 读取 ASC 配置
            var configModel = this.GetModel<ConfigModel>();
            var ascConfig = configModel.GetAscConfig(ascId);
            if (ascConfig == null) return carrierId;
            var ac = ascConfig.Value;

            var em = this.GetService<WorldService>().EntityManager;

            // 3. 等级
            em.SetComponentData(entity, new CAscBasicData { Level = ac.Level });

            // 4. 标签
            if (ac.Tags is { Length: > 0 })
            {
                var tagBuf = em.GetBuffer<BFixedTag>(entity);
                foreach (var tag in ac.Tags)
                    tagBuf.Add(new BFixedTag { tag = tag });
            }

            // 5. 属性集
            if (ac.AttrSetIds is { Length: > 0 })
            {
                var attrSetBuf = em.GetBuffer<BEAttrSet>(entity);
                foreach (var setId in ac.AttrSetIds)
                {
                    var setDef = configModel.GetAttrSetDef(setId);
                    if (setDef == null) continue;

                    var attrs = new NativeArray<CAttributeData>(setDef.Value.Attributes.Length, Allocator.Persistent);
                    for (int i = 0; i < setDef.Value.Attributes.Length; i++)
                    {
                        var src = setDef.Value.Attributes[i];
                        attrs[i] = new CAttributeData
                        {
                            Code = src.Code,
                            BaseValue = src.InitValue,
                            CurrentValue = src.InitValue,
                            IsClampMin = src.UseMinValue,
                            IsClampMax = src.UseMaxValue,
                            MinValue = src.MinValue,
                            MaxValue = src.MaxValue,
                            Dirty = false
                        };
                    }
                    attrSetBuf.Add(new BEAttrSet { Code = setId, Attributes = attrs });
                }
            }

            // 6. 技能授予
            if (ac.AbilityIds is { Length: > 0 })
            {
                var abilityService = this.GetService<AbilityService>();
                foreach (var abilityId in ac.AbilityIds)
                    abilityService.GrantAbility(carrierId, abilityId, this);
            }

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
