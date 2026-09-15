using Unity.Entities;
using NexusFramework;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.Models;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     激活时对自身施加一组 GameplayEffect。
    /// </summary>
    public class ALApplyEffect : AbilityLogicBase<XParamEffectIDs>
    {
        public ALApplyEffect(Entity ability, IArchitecture architecture) : base(ability, architecture)
        {
        }

        public override void AbilityTick(GlobalTimer timer)
        {
        }

        public override void ActivateAbility(GlobalTimer timer)
        {
            var owner = OwnerEntity;
            if (owner == Entity.Null)
            {
                Debug.LogError($"[NF.GAS] ALApplyEffect 无法解析所属 ASC（Ability={_abilityEntity}）");
                return;
            }

            if (_param?.IDs == null || _param.IDs.Length == 0)
            {
                Debug.LogWarning($"[NF.GAS] ALApplyEffect 配置的参数为空（Ability={_abilityEntity}），无事可做");
                return;
            }

            var configModel = Architecture.GetModel<ConfigModel>();

            foreach (var effectCode in _param.IDs)
            {
                var configs = configModel.GetGameplayEffectConfig(effectCode);
                if (configs == null || configs.Length == 0)
                {
                    Debug.LogError($"[NF.GAS] ALApplyEffect 找不到效果配置: ID={effectCode}");
                    continue;
                }

                var geEntity = CreateGameplayEffectEntity(configs);
                ApplyGameplayEffectTo(geEntity, owner, owner);
            }
        }

        public override void CancelAbility(GlobalTimer timer)
        {
            EndAbility(timer);
        }

        public override void EndAbility(GlobalTimer timer)
        {
            var ownerAsc = GetOwnerAscEntity();
            var geEntities = _entityManager.GetBuffer<BGameplayEffect>(ownerAsc);
            foreach (var beEffect in geEntities)
            {
                var effect = beEffect.GameplayEffect;
                if (_entityManager.HasComponent<CCreatedByAbility>(effect))
                {
                    var createdByAbility = _entityManager.GetComponentData<CCreatedByAbility>(effect);
                    if (createdByAbility.sourceAbility == _abilityEntity)
                        RemoveGameplayEffect(effect);
                }
            }
        }
    }
}