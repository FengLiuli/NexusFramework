using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public class MCAbilityLogic : IComponentData
    {
        public AbilityLogicBase logic;

        public MCAbilityLogic(AbilityLogicBase logic)
        {
            this.logic = logic;
        }

        public MCAbilityLogic()
        {
        }
    }

    public sealed class MCConfAbilityLogic : AbilityComponentConfig
    {
        public string AbilityLogicType;
        public XParam Param;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            var logic = AbilityLogicFactory.TryCreateAbilityLogic(AbilityLogicType, ability);
            if (logic != null)
            {
                logic.SetParam(Param);
                _entityManager.AddComponent<MCAbilityLogic>(ability);
                _entityManager.SetComponentData(ability, new MCAbilityLogic(logic));
            }
        }
    }
}