using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;

namespace NexusFramework.GAS.Services
{
    public class AbilityService : AbstractService
    {
        private readonly Dictionary<CarrierId, List<int>> _grantedAbilities = new();

        protected override void OnInit()
        {
            ScanAndRegisterAll();
        }

        protected override void OnDeinit() { }

        /// <summary>自动扫描 Architecture 所在程序集中所有 AbilityLogicBase 子类并注册</summary>
        public void ScanAndRegisterAll()
        {
            var assembly = Architecture.GetType().Assembly;
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(AbilityLogicBase).IsAssignableFrom(type)) continue;
                AbilityLogicFactory.Register(type.Name, type);

                var paramType = InferParamType(type, typeof(AbilityLogicBase<>));
                if (paramType != null)
                    AbilityLogicFactory.RegisterAbilityLogicParam(type.Name, paramType);
            }
        }

        private static Type InferParamType(Type subType, Type genericBaseDef)
        {
            var baseType = subType.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == genericBaseDef)
                    return baseType.GetGenericArguments()[0];
                baseType = baseType.BaseType;
            }
            return null;
        }

        public void GrantAbility(CarrierId carrier, int abilityCode, IArchitecture architecture)
        {
            var model = this.GetModel<GASEntityMapModel>();
            var ownerEntity = model.GetGASEntity(carrier);
            if (ownerEntity == Entity.Null) return;

            var em = this.GetService<WorldService>().EntityManager;
            AbilityComponentConfig.SetEntityManager(em);

            var configs = this.GetModel<ConfigModel>().GetAbilityConfig(abilityCode);
            if (configs == null) return;

            var abilityEntity = em.CreateEntity();
            em.SetName(abilityEntity, $"Ability_{abilityCode}_{abilityEntity.Index}");

            foreach (var config in configs)
                config.LoadToGameplayAbilityEntity(abilityEntity);

            em.AddComponent<CAbilityBaseInfo>(abilityEntity);
            em.SetComponentData(abilityEntity, new CAbilityBaseInfo
            {
                Code = abilityCode,
                Owner = ownerEntity,
                Level = 1
            });

            var ascBuffer = em.GetBuffer<BAbility>(ownerEntity);
            ascBuffer.Add(new BAbility { Ability = abilityEntity });

            if (!_grantedAbilities.ContainsKey(carrier))
                _grantedAbilities[carrier] = new List<int>();
            _grantedAbilities[carrier].Add(abilityCode);
        }

        public bool TryActivate(CarrierId carrier, int abilityCode, XParam param = null)
        {
            var model = this.GetModel<GASEntityMapModel>();
            var ownerEntity = model.GetGASEntity(carrier);
            if (ownerEntity == Entity.Null) return false;

            var em = this.GetService<WorldService>().EntityManager;
            var ascBuffer = em.GetBuffer<BAbility>(ownerEntity);

            for (var i = 0; i < ascBuffer.Length; i++)
            {
                var abiElem = ascBuffer[i];
                if (!em.HasComponent<CAbilityBaseInfo>(abiElem.Ability)) continue;

                var info = em.GetComponentData<CAbilityBaseInfo>(abiElem.Ability);
                if (info.Code != abilityCode) continue;

                if (em.HasComponent<MCAbilityLogic>(abiElem.Ability) && param != null)
                {
                    var mcLogic = em.GetComponentData<MCAbilityLogic>(abiElem.Ability);
                    mcLogic.logic?.SetParam(param);
                }

                em.AddComponent<CAbilityInTryActivate>(abiElem.Ability);
                return true;
            }

            return false;
        }

        public void TryEnd(CarrierId carrier, int abilityCode)
        {
            MarkAbilityAction(carrier, abilityCode, abilityEntity =>
            {
                var em = this.GetService<WorldService>().EntityManager;
                em.AddComponent<CAbilityInTryEnd>(abilityEntity);
                return true;
            });
        }

        public void TryCancel(CarrierId carrier, int abilityCode)
        {
            MarkAbilityAction(carrier, abilityCode, abilityEntity =>
            {
                var em = this.GetService<WorldService>().EntityManager;
                em.AddComponent<CAbilityInTryCancel>(abilityEntity);
                return true;
            });
        }

        private void MarkAbilityAction(CarrierId carrier, int abilityCode, System.Func<Entity, bool> action)
        {
            var model = this.GetModel<GASEntityMapModel>();
            var ownerEntity = model.GetGASEntity(carrier);
            if (ownerEntity == Entity.Null) return;

            var em = this.GetService<WorldService>().EntityManager;
            var ascBuffer = em.GetBuffer<BAbility>(ownerEntity);

            for (var i = 0; i < ascBuffer.Length; i++)
            {
                var abilityEntity = ascBuffer[i].Ability;
                if (!em.HasComponent<CAbilityBaseInfo>(abilityEntity)) continue;
                var info = em.GetComponentData<CAbilityBaseInfo>(abilityEntity);
                if (info.Code != abilityCode) continue;
                action(abilityEntity);
                return;
            }
        }

        public void RemoveAbility(CarrierId carrier, int abilityCode)
        {
            var model = this.GetModel<GASEntityMapModel>();
            var ownerEntity = model.GetGASEntity(carrier);
            if (ownerEntity == Entity.Null) return;

            var em = this.GetService<WorldService>().EntityManager;
            var ascBuffer = em.GetBuffer<BAbility>(ownerEntity);

            var toRemoveIndices = new System.Collections.Generic.List<int>();
            for (var i = 0; i < ascBuffer.Length; i++)
            {
                var abilityEntity = ascBuffer[i].Ability;
                if (em.HasComponent<CAbilityBaseInfo>(abilityEntity))
                {
                    var info = em.GetComponentData<CAbilityBaseInfo>(abilityEntity);
                    if (info.Code == abilityCode)
                        toRemoveIndices.Add(i);
                }
            }

            var entitiesToDestroy = new System.Collections.Generic.List<Entity>();
            for (var j = toRemoveIndices.Count - 1; j >= 0; j--)
            {
                var idx = toRemoveIndices[j];
                entitiesToDestroy.Add(ascBuffer[idx].Ability);
                ascBuffer.RemoveAt(idx);
            }

            foreach (var entity in entitiesToDestroy)
            {
                if (em.Exists(entity))
                {
                    ECS.CleanupAbilityHelper.DisposeAllAbilityNativeArrays(em, entity);
                    em.DestroyEntity(entity);
                }
            }

            if (_grantedAbilities.TryGetValue(carrier, out var list))
                list.Remove(abilityCode);
        }

        public bool IsActive(CarrierId carrier, int abilityCode)
        {
            var model = this.GetModel<GASEntityMapModel>();
            var ownerEntity = model.GetGASEntity(carrier);
            if (ownerEntity == Entity.Null) return false;

            var em = this.GetService<WorldService>().EntityManager;
            var ascBuffer = em.GetBuffer<BAbility>(ownerEntity);

            for (var i = 0; i < ascBuffer.Length; i++)
            {
                var abilityEntity = ascBuffer[i].Ability;
                if (!em.HasComponent<CAbilityBaseInfo>(abilityEntity)) continue;
                var info = em.GetComponentData<CAbilityBaseInfo>(abilityEntity);
                if (info.Code != abilityCode) continue;
                return em.HasComponent<CAbilityActive>(abilityEntity);
            }
            return false;
        }
    }
}
