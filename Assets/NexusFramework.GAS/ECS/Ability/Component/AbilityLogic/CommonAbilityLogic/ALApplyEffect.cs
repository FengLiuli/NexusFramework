using Unity.Entities;
using NexusFramework;

namespace NexusFramework.GAS.ECS
{
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
            foreach (var effectCode in _param.IDs)
            {
                // TODO: [NF.GAS] Config lookup via IConfigLoader(effectCode), then CreateGameplayEffectEntity + ApplyGameplayEffectTo
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