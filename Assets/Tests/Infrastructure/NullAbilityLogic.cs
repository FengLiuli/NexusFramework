using Unity.Entities;
using NexusFramework.GAS.ECS;

namespace NexusFramework.GAS.Tests
{
    public class NullAbilityLogic : AbilityLogicBase
    {
        public NullAbilityLogic(Entity ability, IArchitecture architecture) : base(ability, architecture) { }
        public NullAbilityLogic(Entity ability, EntityManager em) : base(ability, em) { }
        public override void ActivateAbility(GlobalTimer timer) { }
        public override void CancelAbility(GlobalTimer timer) { }
        public override void EndAbility(GlobalTimer timer) { }
        public override void AbilityTick(GlobalTimer timer) { }
    }
}
