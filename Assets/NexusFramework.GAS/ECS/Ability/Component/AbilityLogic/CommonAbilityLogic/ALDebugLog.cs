using Unity.Entities;
using UnityEngine;
using NexusFramework;

namespace NexusFramework.GAS.ECS
{
    public class ALDebugLog : AbilityLogicBase<XParamString>
    {
        public ALDebugLog(Entity ability, IArchitecture architecture) : base(ability, architecture)
        {
        }
        
        public override void AbilityTick(GlobalTimer timer)
        {
            Debug.Log($"Entity:{_abilityEntity} AbilityTick: {_param.Value}");
        }

        public override void ActivateAbility(GlobalTimer timer)
        {
            Debug.Log($"Entity:{_abilityEntity}  ActivateAbility");
        }

        public override void CancelAbility(GlobalTimer timer)
        {
            Debug.Log($"Entity:{_abilityEntity}  CancelAbility");
        }

        public override void EndAbility(GlobalTimer timer)
        {
            Debug.Log($"Entity:{_abilityEntity}  EndAbility");
        }
    }
}